using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record MatchState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MatchSeriesState? SeriesState { get; init; }

    // Preserve the terminal core frame in that game's journal row even when
    // the current room immediately moves to the next game's waiting state.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompletedSeriesGame? LastCompletedSeriesGame { get; init; }
}

public sealed record CompletedSeriesGame(int GameNumber, MatchState State);

// Room orchestration calls the shared tournament model. Core 110 and all card
// rules continue to operate on the ordinary single-game MatchState.
public static class RoomMatchSeries
{
    internal static MatchState SeatPlayers(MatchState state)
    {
        if (state.SessionOptions?.DeckFormat != OfficialDeckFormat.Sealed || state.Seats.Count != 2)
            return state;
        var players = state.Seats.OrderBy(entry => entry.Value, StringComparer.Ordinal).Select(entry => entry.Key).ToArray();
        return state with { SeriesState = MatchSeries.Create(state.SessionOptions.SealedBestOf, players, state.Seed).State };
    }

    internal static MatchSeries? Current(MatchState state)
        => state.SeriesState is { } series ? MatchSeries.Restore(series, state.Seed) : null;

    public static MatchState EventState(MatchState state)
        => state.Status == MatchStatuses.InProgress ? state : state.LastCompletedSeriesGame?.State ?? state;

    public static MatchSeriesDto? ViewFor(MatchState state, string? viewerPlayerId = null)
    {
        MatchSeries? series;
        try { series = Current(state); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return null; }
        if (series is null) return null;
        return new(series.State.BestOf, series.GameNumber, series.Wins,
            series.Phase switch
            {
                MatchSeriesPhase.ChoosingPlayOrder => MatchSeriesPhases.ChoosingPlayOrder,
                MatchSeriesPhase.SubmittingDecks => MatchSeriesPhases.SubmittingDecks,
                MatchSeriesPhase.Playing => MatchSeriesPhases.Playing,
                _ => MatchSeriesPhases.Finished
            },
            SealedSeriesDecks.UsedBattlefields(state),
            series.PlayOrderChooserPlayerId,
            series.Phase is MatchSeriesPhase.ChoosingPlayOrder or MatchSeriesPhase.SubmittingDecks
                ? SealedSeriesDecks.LockedDeckFor(state, viewerPlayerId) : null);
    }

    internal static IReadOnlyDictionary<string, ActionPromptDto>? ChoosingPrompts(MatchState state)
    {
        if (state.SessionOptions?.DeckFormat == OfficialDeckFormat.Sealed && state.Seats.Count < 2)
            return state.Seats.Keys.ToDictionary(player => player, player => ActionPromptBuilder.Build(
                state, player, false, "等待双方入座", ["WAIT"]), StringComparer.Ordinal);
        if (Current(state) is not { Phase: MatchSeriesPhase.ChoosingPlayOrder } series) return null;
        return state.Seats.Keys.ToDictionary(player => player, player => ActionPromptBuilder.Build(
            state, player, player == series.PlayOrderChooserPlayerId,
            player == series.PlayOrderChooserPlayerId ? "请选择本局先手或后手" : "等待对手选择先后手",
            player == series.PlayOrderChooserPlayerId ? [CommandTypes.ChoosePlayOrder] : ["WAIT"]), StringComparer.Ordinal);
    }

    internal static ResolutionResult Choose(MatchState state, string playerId, ChoosePlayOrderCommand command)
    {
        if (Current(state) is not { Phase: MatchSeriesPhase.ChoosingPlayOrder } series)
            return ResolutionResult.Rejected(state, "当前不在选择先后手阶段。", ErrorCodes.PhaseNotAllowed);
        if (playerId != series.PlayOrderChooserPlayerId)
            return ResolutionResult.Rejected(state, "只有本局指定玩家可以选择先后手。", ErrorCodes.PhaseNotAllowed);
        var chosen = series.ChoosePlayOrder(playerId, command.GoFirst);
        var next = state with { Tick = state.Tick + 1, SeriesState = chosen.State };
        var events = new[] { new GameEvent("PLAY_ORDER_CHOSEN", "本局先后手已确定",
            new Dictionary<string, object?> { ["playerId"] = playerId, ["goFirst"] = command.GoFirst,
                ["firstPlayerId"] = chosen.FirstPlayerId, ["gameNumber"] = chosen.GameNumber }) };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), ResolutionResult.BuildPrompts(next));
    }

    public static IReadOnlyList<string> Validate(MatchState state)
    {
        if (state.SessionOptions?.DeckFormat != OfficialDeckFormat.Sealed)
            return state.SeriesState is null && state.LastCompletedSeriesGame is null
                ? SealedSeriesDecks.Validate(state) : ["non-sealed room contains a match series"];
        if (state.Seats.Count != 2)
            return state.SeriesState is null ? SealedSeriesDecks.Validate(state) : ["match series requires both room seats"];
        if (state.SeriesState is null) return ["sealed room is missing its match series"];
        try
        {
            var series = Current(state)!;
            var players = state.Seats.OrderBy(entry => entry.Value, StringComparer.Ordinal).Select(entry => entry.Key);
            if (series.State.BestOf != state.SessionOptions.SealedBestOf || !players.SequenceEqual(series.State.PlayerIds))
                return ["match series disagrees with room configuration or seats"];
            if (series.Phase == MatchSeriesPhase.Playing && state.Status != MatchStatuses.InProgress
                || series.Phase == MatchSeriesPhase.Finished && (state.Status != MatchStatuses.Finished || state.WinnerPlayerId != series.WinnerPlayerId)
                || series.Phase is MatchSeriesPhase.ChoosingPlayOrder or MatchSeriesPhase.SubmittingDecks
                    && (state.Status != MatchStatuses.Seating || state.Phase != MatchPhases.Room || state.WinnerPlayerId is not null))
                return ["match series phase disagrees with its core game"];
            if (series.Phase == MatchSeriesPhase.ChoosingPlayOrder && state.ReadyPlayerIds.Count > 0)
                return ["players cannot be ready before play order selection"];
            if (SealedSeriesDecks.Validate(state) is { Count: > 0 } battlefieldErrors) return battlefieldErrors;
            var last = series.State.Games.LastOrDefault(game => game.Completed);
            var archive = state.LastCompletedSeriesGame;
            if (last is null && archive is not null || last is not null && archive is null)
                return ["completed game frame disagrees with the tournament ledger"];
            if (archive is not null)
            {
                var core = archive.State;
                if (core is null || archive.GameNumber != last!.GameNumber || core.Status != MatchStatuses.Finished
                    || core.WinnerPlayerId != last.WinnerPlayerId || core.RoomId != state.RoomId || core.Seed != state.Seed
                    || core.Tick > state.Tick || core.RngCursor > state.RngCursor || core.SeriesState is not null
                    || core.LastCompletedSeriesGame is not null || core.PlayerSealedPools is not null || core.SessionOptions is not null
                    || core.SealedBattlefieldHistory is not null
                    || !core.Seats.OrderBy(pair => pair.Key).SequenceEqual(state.Seats.OrderBy(pair => pair.Key)))
                    return ["invalid completed core game frame"];
                return MatchRecoveryValidator.ValidateCompletedCoreGame(core);
            }
            return [];
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return ["invalid match series: " + exception.Message];
        }
    }
}

public static partial class MatchRecoveryValidator
{
    internal static IReadOnlyList<string> ValidateCompletedCoreGame(MatchState state)
    {
        var errors = new List<string>();
        ValidateAuthoritativeState(state.RoomId, state.Tick, state,
            new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal), errors);
        return errors;
    }
}

public sealed partial class MatchSession
{
    private static ResolutionResult CompleteSeriesGame(ResolutionResult result)
    {
        if (!result.Accepted || result.State.Status != MatchStatuses.Finished
            || RoomMatchSeries.Current(result.State) is not { Phase: MatchSeriesPhase.Playing } series)
            return result;
        var completed = series.CompleteGame(series.GameNumber, result.State.WinnerPlayerId);
        return FinalizeSeriesGame(result, series, completed);
    }

    private static ResolutionResult FinalizeSeriesGame(ResolutionResult result, MatchSeries series, MatchSeries completed)
    {
        var terminal = new CompletedSeriesGame(series.GameNumber, result.State with
        {
            SeriesState = null, LastCompletedSeriesGame = null, PlayerSealedPools = null, SessionOptions = null,
            SealedBattlefieldHistory = null
        });
        var next = result.State with { SeriesState = completed.State, LastCompletedSeriesGame = terminal };
        if (completed.Phase != MatchSeriesPhase.Finished)
        {
            // Recreate every game-local field through the shared default state;
            // only room identity, private pools and the tournament ledger persist.
            var fresh = MatchState.Create(next.RoomId) with
            {
                Tick = next.Tick, Seed = next.Seed, RngCursor = next.RngCursor,
                SessionOptions = next.SessionOptions, PlayerSealedPools = next.PlayerSealedPools,
                SeriesState = completed.State, LastCompletedSeriesGame = terminal,
                SealedBattlefieldHistory = next.SealedBattlefieldHistory
            };
            next = StateWithRoomSeats(fresh, next.Seats, next.ActivePlayerId);
        }
        var events = result.Events.Select(item => item.Kind == "MATCH_WON" && completed.Phase != MatchSeriesPhase.Finished
            ? item with { Kind = "GAME_WON", Description = "本局结束" } : item).Append(new GameEvent(
                "SERIES_GAME_COMPLETED", "单局结果已记入比赛",
                new Dictionary<string, object?> { ["gameNumber"] = series.GameNumber,
                    ["winnerPlayerId"] = result.State.WinnerPlayerId, ["wins"] = completed.Wins })).ToArray();
        return result with { State = next, Events = events, Snapshots = ResolutionResult.BuildSnapshots(next),
            Prompts = ResolutionResult.BuildPrompts(next) };
    }
}
