using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public enum MatchSeriesPhase
{
    ChoosingPlayOrder,
    SubmittingDecks,
    Playing,
    Finished
}

// These records are private persistence data, not a client projection. In
// particular, the caller must keep the opening seed in its authoritative frame.
public sealed record MatchSeriesGameState(
    int GameNumber,
    string? FirstPlayerId = null,
    bool Started = false,
    bool Completed = false,
    string? WinnerPlayerId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Skipped = false);

public sealed record MatchSeriesState(
    int BestOf,
    IReadOnlyList<string> PlayerIds,
    string InitialPlayOrderChooserPlayerId,
    IReadOnlyList<MatchSeriesGameState> Games);

/// <summary>
/// Shared, immutable match orchestration under tournament rules 404 and 407.
/// Deck admission, battlefield selection and the core game are owned by the caller.
/// </summary>
public sealed class MatchSeries
{
    private readonly long openingSeed;

    private MatchSeries(MatchSeriesState state, long openingSeed)
    {
        Validate(state, openingSeed);
        this.openingSeed = openingSeed;
        State = state with
        {
            PlayerIds = Array.AsReadOnly(state.PlayerIds.ToArray()),
            Games = Array.AsReadOnly(state.Games.ToArray())
        };
        Wins = new ReadOnlyDictionary<string, int>(State.PlayerIds.ToDictionary(
            player => player,
            player => State.Games.Count(game => game.Completed && game.WinnerPlayerId == player),
            StringComparer.Ordinal));
    }

    public MatchSeriesState State { get; }
    public IReadOnlyDictionary<string, int> Wins { get; }
    public int GameNumber => Current.GameNumber;
    public string? FirstPlayerId => Current.FirstPlayerId;
    public string? WinnerPlayerId => Wins.FirstOrDefault(entry => entry.Value >= RequiredWins).Key;
    public MatchSeriesPhase Phase => Current.Completed ? MatchSeriesPhase.Finished
        : Current.Started ? MatchSeriesPhase.Playing
        : Current.FirstPlayerId is not null ? MatchSeriesPhase.SubmittingDecks
        : MatchSeriesPhase.ChoosingPlayOrder;
    public string? PlayOrderChooserPlayerId => Phase != MatchSeriesPhase.ChoosingPlayOrder ? null
        : State.Games.Count == 1 ? State.InitialPlayOrderChooserPlayerId
        : Opponent(State.Games[^2].WinnerPlayerId!);

    private int RequiredWins => (State.BestOf + 1) / 2;
    private MatchSeriesGameState Current => State.Games[^1];

    public static MatchSeries Create(int bestOf, IReadOnlyList<string> playerIds, long openingSeed)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        ValidatePlayers(playerIds);
        var players = playerIds.ToArray();
        return new MatchSeries(new MatchSeriesState(bestOf, players,
            players[InitialChooserIndex(openingSeed)], [new(1)]), openingSeed);
    }

    public static MatchSeries Restore(MatchSeriesState state, long recordedOpeningSeed)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new MatchSeries(state, recordedOpeningSeed);
    }

    public MatchSeries ChoosePlayOrder(string playerId, bool playFirst)
    {
        if (Phase != MatchSeriesPhase.ChoosingPlayOrder)
            throw new InvalidOperationException("当前不在选择先后手阶段。");
        if (!string.Equals(playerId, PlayOrderChooserPlayerId, StringComparison.Ordinal))
            throw new InvalidOperationException("只有本局指定玩家可以选择先后手。");
        return ReplaceCurrent(Current with { FirstPlayerId = playFirst ? playerId : Opponent(playerId) });
    }

    // The room calls this only after both private deck submissions and the
    // simultaneous battlefield reveal have completed, immediately before core 110.
    public MatchSeries StartGame()
    {
        if (Phase != MatchSeriesPhase.SubmittingDecks)
            throw new InvalidOperationException("本局尚未完成先后手选择或已经开始。");
        return ReplaceCurrent(Current with { Started = true });
    }

    // A null winner means a draw. Game number makes a repeated result harmless
    // even after a later game has begun; a conflicting result is always rejected.
    public MatchSeries CompleteGame(int gameNumber, string? winnerPlayerId)
        => CompleteGameResult(gameNumber, winnerPlayerId, allowUnstarted: false);

    // Trusted tournament orchestration may award a loss before core 110 or
    // during a game. Player commands do not call this entry point.
    public MatchSeries AwardGameLoss(int gameNumber, string loserPlayerId)
    {
        if (!State.PlayerIds.Contains(loserPlayerId, StringComparer.Ordinal))
            throw new InvalidOperationException("单局判负对象必须是本场比赛的玩家。");
        return CompleteGameResult(gameNumber, Opponent(loserPlayerId), allowUnstarted: true);
    }

    private MatchSeries CompleteGameResult(int gameNumber, string? winnerPlayerId, bool allowUnstarted)
    {
        if (winnerPlayerId is not null && !State.PlayerIds.Contains(winnerPlayerId, StringComparer.Ordinal))
            throw new InvalidOperationException("单局胜者必须是本场比赛的玩家；平局请留空。");
        if (gameNumber < 1 || gameNumber > State.Games.Count)
            throw new InvalidOperationException("单局结果的局号无效。");
        var recorded = State.Games[gameNumber - 1];
        if (recorded.Completed)
        {
            if (recorded.WinnerPlayerId == winnerPlayerId) return this;
            throw new InvalidOperationException("已记录的单局结果不能更改。");
        }
        if (gameNumber != GameNumber || Phase != MatchSeriesPhase.Playing && !allowUnstarted)
            throw new InvalidOperationException("只有正在进行的对局可以结算结果。");

        var games = State.Games.ToArray();
        games[^1] = Current with { Completed = true, WinnerPlayerId = winnerPlayerId, Skipped = !Current.Started };
        var finished = winnerPlayerId is not null && Wins[winnerPlayerId] + 1 >= RequiredWins;
        if (!finished)
        {
            // 407.4: after a draw there is no new choice; the order is retained.
            games = [.. games, new(gameNumber + 1, winnerPlayerId is null ? Current.FirstPlayerId : null)];
        }
        return new MatchSeries(State with { Games = games }, openingSeed);
    }

    private MatchSeries ReplaceCurrent(MatchSeriesGameState game)
    {
        var games = State.Games.ToArray();
        games[^1] = game;
        return new MatchSeries(State with { Games = games }, openingSeed);
    }

    private string Opponent(string playerId) => State.PlayerIds.First(player => player != playerId);

    private static int InitialChooserIndex(long seed)
    {
        // Separate from the core shuffle stream. No public room ID, deck data,
        // runtime hash code or culture-dependent formatting affects the draw.
        var input = FormattableString.Invariant($"match-series-play-order-v1:{seed}");
        return (int)(BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(input))) & 1UL);
    }

    private static void ValidatePlayers(IReadOnlyList<string> players)
    {
        if (players.Count != 2 || players.Any(player => string.IsNullOrWhiteSpace(player) || player != player.Trim())
            || players.Distinct(StringComparer.Ordinal).Count() != 2)
            throw new ArgumentException("多局比赛需要两名不同且有效的玩家。");
    }

    private static void Validate(MatchSeriesState state, long seed)
    {
        if (state.BestOf is not (1 or 3))
            throw new ArgumentException("比赛局制只能为单局制或三局两胜。");
        if (state.PlayerIds is null || state.Games is null || state.Games.Count == 0)
            throw new ArgumentException("多局比赛恢复数据缺少玩家或对局记录。");
        ValidatePlayers(state.PlayerIds);
        if (state.InitialPlayOrderChooserPlayerId != state.PlayerIds[InitialChooserIndex(seed)])
            throw new ArgumentException("首局指定玩家与记录的随机种子不一致。");

        var wins = state.PlayerIds.ToDictionary(player => player, _ => 0, StringComparer.Ordinal);
        var requiredWins = (state.BestOf + 1) / 2;
        for (var index = 0; index < state.Games.Count; index++)
        {
            var game = state.Games[index];
            if (game is null || game.GameNumber != index + 1
                || (game.FirstPlayerId is not null && !wins.ContainsKey(game.FirstPlayerId))
                || (game.Started && game.FirstPlayerId is null)
                || (game.Completed && !game.Started && !game.Skipped)
                || (game.Skipped && (game.Started || !game.Completed || game.WinnerPlayerId is null))
                || (game.WinnerPlayerId is not null && (!game.Completed || !wins.ContainsKey(game.WinnerPlayerId)))
                || (index < state.Games.Count - 1 && !game.Completed))
                throw new ArgumentException("多局比赛的单局记录不一致。");
            if (index > 0 && state.Games[index - 1].WinnerPlayerId is null
                && game.FirstPlayerId != state.Games[index - 1].FirstPlayerId)
                throw new ArgumentException("平局后的对局必须沿用上一局的先后手顺序。");

            if (game.WinnerPlayerId is not null) wins[game.WinnerPlayerId]++;
            var finished = wins.Values.Any(count => count >= requiredWins);
            if ((finished && index != state.Games.Count - 1)
                || (index == state.Games.Count - 1 && game.Completed && !finished))
                throw new ArgumentException("胜局数与比赛结束位置不一致。");
        }
    }
}
