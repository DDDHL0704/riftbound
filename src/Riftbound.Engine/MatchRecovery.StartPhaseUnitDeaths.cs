using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateStartPhaseUnitDeathLedgerShape(MatchState state, List<string> errors)
    {
        if (state.DeathLedger?.StartPhaseUnitControllers is not { } players) return;
        if (players.Count == 0 || players.Any(player => !state.Seats.ContainsKey(player))
            || players.Distinct(StringComparer.Ordinal).Count() != players.Count
            || state.DeathLedger.TurnNumber != state.TurnNumber)
            errors.Add("invalid start-phase unit death ledger");
    }

    private static void ValidateStartPhaseUnitDeathHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        foreach (var record in events)
        {
            if (record.Sequence <= boundary
                || !record.Event.Payload.ContainsKey("startPhaseUnitDestroyedForPlayerId")) continue;
            var payload = JsonSerializer.SerializeToElement(record.Event.Payload);
            if (record.Event.Kind != "UNIT_DESTROYED"
                || !commands.Any(command => command.Accepted && command.CompletedTick == record.Tick
                    && command.StartedEventSequence < record.Sequence && command.CompletedEventSequence >= record.Sequence)
                || !payload.TryGetProperty("startPhaseUnitDestroyedForPlayerId", out var player)
                || player.ValueKind != JsonValueKind.String || !state.Seats.ContainsKey(player.GetString()!)
                || !payload.TryGetProperty("startPhaseUnitDestroyedTurnNumber", out var turn) || !turn.TryGetInt32(out var number)
                || number < 0)
            { errors.Add("invalid original start-phase unit death fact"); continue; }
            if (number == state.TurnNumber) expected.Add(player.GetString()!);
        }
        var actual = state.DeathLedger.StartPhaseUnitControllers ?? [];
        if (!expected.SetEquals(actual)) errors.Add("start-phase unit deaths differ from original accepted history");
    }
}
