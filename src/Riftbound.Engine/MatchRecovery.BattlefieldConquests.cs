using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateBattlefieldConquestHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events,
        List<string> errors)
    {
        var expected = new HashSet<BattlefieldConquestRecord>();
        var expectedUnits = new HashSet<ObjectBinding>();
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        foreach (var record in events)
        {
            var e = record.Event;
            if (record.Sequence <= boundary || e.Kind != "BATTLEFIELD_CONQUERED") continue;
            var payload = JsonSerializer.SerializeToElement(e.Payload);
            if (!payload.TryGetProperty("conquestTurnNumber", out var turn) || !turn.TryGetInt32(out var number)
                || number != state.TurnNumber) continue;
            if (!commands.Any(command => command.Accepted && command.CompletedTick == record.Tick
                    && command.StartedEventSequence < record.Sequence && command.CompletedEventSequence >= record.Sequence)
                || !payload.TryGetProperty("playerId", out var player) || player.ValueKind != JsonValueKind.String
                || !(payload.TryGetProperty("battlefieldObjectId", out var field) || payload.TryGetProperty("battlefieldId", out field))
                || field.ValueKind != JsonValueKind.String
                || !payload.TryGetProperty("conqueredBattlefieldGeneration", out var generation) || !generation.TryGetInt64(out var gen))
            { errors.Add("invalid captured conquest history"); continue; }
            expected.Add(new(player.GetString()!, new(field.GetString()!, gen)));
            if (NaturalUnitConquestHistory.ReadBindings(e.Payload) is { } units) expectedUnits.UnionWith(units);
            else errors.Add("invalid original unit conquest bindings");
        }
        var actual = state.BattlefieldConquests?.Entries ?? [];
        if (!expected.SetEquals(actual)) errors.Add("turn battlefield conquests differ from accepted history");
        var actualUnits = state.BattlefieldConquests?.Units ?? [];
        if (actualUnits.Count != actualUnits.Distinct().Count() || !expectedUnits.SetEquals(actualUnits))
            errors.Add("turn unit conquests differ from their original accepted action bindings");
    }
}
