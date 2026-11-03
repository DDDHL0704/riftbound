using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record MatchState
{
    // Record the opening selection, never the possibly transformed terminal battlefield.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SealedBattlefieldSelection>? SealedBattlefieldHistory { get; init; }
}

public sealed record SealedBattlefieldSelection(
    int GameNumber, IReadOnlyDictionary<string, string> Cards, bool ReusesPrevious);

public static class SealedSeriesDecks
{
    private static bool AfterDraw(MatchState state) => state.SeriesState is { Games.Count: > 1 } series
        && series.Games[^2].Completed && series.Games[^2].WinnerPlayerId is null;

    private static bool AfterSkippedFirstGame(MatchState state) => state.SeriesState is { Games.Count: 2 } series
        && series.Games[0].Skipped;

    internal static string? ForcedBattlefield(MatchState state, string playerId) => AfterDraw(state)
        ? state.SealedBattlefieldHistory![^1].Cards[playerId] : null;

    internal static SealedLockedDeckDto? LockedDeckFor(MatchState state, string? playerId)
    {
        if (state.SessionOptions?.DeckFormat != OfficialDeckFormat.Sealed
            || playerId is null || !state.Seats.ContainsKey(playerId)) return null;
        var previous = state.LastCompletedSeriesGame?.State.PlayerDecklists.GetValueOrDefault(playerId);
        // A skipped opening has not revealed or consumed a battlefield. Keep
        // the player's latest legal selection while their main configuration
        // remains locked; an unregistered player has nothing to recover yet.
        var deck = AfterDraw(state) ? previous : AfterSkippedFirstGame(state)
            ? state.PlayerDecklists.GetValueOrDefault(playerId) ?? previous : null;
        return deck is null ? null : new(deck.LegendCardNo, deck.ChampionCardNo,
            deck.MainDeck.ToArray(), deck.RuneDeck.ToArray(), deck.Battlefields.ToArray());
    }

    public static IReadOnlyList<string> SubmissionErrors(MatchState state, string playerId, OfficialDecklist deck)
    {
        if (state.SessionOptions?.DeckFormat != OfficialDeckFormat.Sealed) return [];
        if (state.PlayerSealedPools?.TryGetValue(playerId, out var pool) != true || pool is null)
            return ["现开卡组校验缺少服务端卡池，不能提交。"];
        var errors = new List<string>();
        if (AfterDraw(state))
        {
            if (state.LastCompletedSeriesGame?.State.PlayerDecklists.TryGetValue(playerId, out var previous) != true
                || previous is null || !SameConfiguration(previous, deck))
                errors.Add("平局后不能更换备牌，必须保留上一局的传奇、选定英雄、主牌、符文和战场配置（403.10、406.1.b）。");
            return errors;
        }
        if (AfterSkippedFirstGame(state))
        {
            if (state.LastCompletedSeriesGame is null) return ["缺少被跳过首局的初始配置记录。"];
            // Low-level sealed has no mandatory registration. If a player had
            // not submitted an initial configuration before the penalty, their
            // first legal submission becomes the initial configuration.
            state.LastCompletedSeriesGame!.State.PlayerDecklists.TryGetValue(playerId, out var initial);
            initial ??= state.PlayerDecklists.GetValueOrDefault(playerId);
            if (initial is not null && !SameConfiguration(initial, deck, includeBattlefields: false))
                errors.Add("第一局因惩罚被完全跳过后，下一局不能更换备牌（403.9）。");
        }
        if (state.SessionOptions.SealedBestOf == 3)
        {
            foreach (var cardNo in deck.Battlefields.Distinct(StringComparer.Ordinal))
            {
                if (cardNo == SealedDeckRules.BlankBattlefieldCardNo) continue;
                var used = (state.SealedBattlefieldHistory ?? []).Count(game => !game.ReusesPrevious
                    && game.Cards.GetValueOrDefault(playerId) == cardNo);
                var owned = pool.Pool.GetValueOrDefault(cardNo);
                if (used + deck.Battlefields.Count(no => no == cardNo) > owned)
                    errors.Add($"战场 {cardNo} 已使用 {used} 张，卡池共 {owned} 张，同一轮内每张实体只能使用一次（602.3.e.4.a、602.3.f）。");
            }
        }
        return errors;
    }

    internal static MatchState RecordOpening(MatchState state, IReadOnlyDictionary<string, string> selections)
    {
        if (state.SessionOptions?.DeckFormat != OfficialDeckFormat.Sealed) return state;
        var selection = new SealedBattlefieldSelection(state.SeriesState!.Games.Count,
            selections.ToImmutableSortedDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), AfterDraw(state));
        return state with { SealedBattlefieldHistory = [.. state.SealedBattlefieldHistory ?? [], selection] };
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> UsedBattlefields(MatchState state) =>
        state.Seats.Keys.ToDictionary(player => player, player => (IReadOnlyList<string>)
            (state.SealedBattlefieldHistory ?? []).Where(game => game is { ReusesPrevious: false, Cards: not null }
                && game.Cards.ContainsKey(player)).Select(game => game.Cards[player]).ToArray(), StringComparer.Ordinal);

    public static IReadOnlyList<string> Validate(MatchState state)
    {
        var history = state.SealedBattlefieldHistory ?? [];
        if (state.SessionOptions?.DeckFormat != OfficialDeckFormat.Sealed)
            return state.SealedBattlefieldHistory is null ? [] : ["non-sealed room contains sealed battlefield history"];
        var games = state.SeriesState?.Games ?? [];
        if (games.Any(game => game is null)) return ["invalid sealed battlefield game history"];
        if (history.Count != games.Count(game => game.Started))
            return ["sealed battlefield history disagrees with started games"];
        var startedGames = games.Where(game => game.Started).ToArray();
        var used = state.Seats.Keys.ToDictionary(player => player,
            _ => new Dictionary<string, int>(StringComparer.Ordinal), StringComparer.Ordinal);
        for (var index = 0; index < history.Count; index++)
        {
            var selection = history[index];
            var gameNumber = startedGames[index].GameNumber;
            var reuse = gameNumber > 1 && games[gameNumber - 2].Completed && games[gameNumber - 2].WinnerPlayerId is null;
            if (selection is null || selection.GameNumber != gameNumber || selection.Cards is null
                || !selection.Cards.Keys.Order(StringComparer.Ordinal).SequenceEqual(state.Seats.Keys.Order(StringComparer.Ordinal))
                || selection.ReusesPrevious != reuse)
                return ["invalid sealed battlefield selection"];
            foreach (var (player, cardNo) in selection.Cards)
            {
                if (string.IsNullOrWhiteSpace(cardNo) || reuse && (index == 0 || history[index - 1].Cards[player] != cardNo))
                    return ["draw must retain the original sealed battlefield"];
                var decklists = selection.GameNumber == state.LastCompletedSeriesGame?.GameNumber
                    ? state.LastCompletedSeriesGame.State.PlayerDecklists
                    : selection.GameNumber == games.Count ? state.PlayerDecklists : null;
                if (decklists is not null && (!decklists.TryGetValue(player, out var deck) || !deck.Battlefields.Contains(cardNo, StringComparer.Ordinal)))
                    return ["sealed battlefield selection disagrees with the submitted deck"];
                if (cardNo == SealedDeckRules.BlankBattlefieldCardNo) continue;
                if (state.PlayerSealedPools?.TryGetValue(player, out var pool) != true || pool is null)
                    return ["sealed battlefield history is missing its authoritative pool"];
                var owned = pool.Pool.GetValueOrDefault(cardNo);
                if (!reuse) used[player][cardNo] = used[player].GetValueOrDefault(cardNo) + 1;
                if (owned == 0 || state.SessionOptions.SealedBestOf == 3 && used[player].GetValueOrDefault(cardNo) > owned)
                    return ["sealed battlefield history exceeds the player's printed copies"];
            }
        }
        if (RoomMatchSeries.Current(state)?.Phase == MatchSeriesPhase.SubmittingDecks)
            foreach (var (player, deck) in state.PlayerDecklists)
                if (SubmissionErrors(state, player, deck).Count > 0)
                    return ["sealed intergame deck configuration is invalid"];
        return [];
    }

    private static bool SameConfiguration(OfficialDecklist left, OfficialDecklist right, bool includeBattlefields = true) =>
        left.LegendCardNo == right.LegendCardNo && left.ChampionCardNo == right.ChampionCardNo
        && SameCards(left.MainDeck, right.MainDeck) && SameCards(left.RuneDeck, right.RuneDeck)
        && (!includeBattlefields || SameCards(left.Battlefields, right.Battlefields));

    private static bool SameCards(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Order(StringComparer.Ordinal).SequenceEqual(right.Order(StringComparer.Ordinal));
}
