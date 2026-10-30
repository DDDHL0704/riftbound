using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateBattlefieldConquestHistory(MatchState state, IReadOnlyList<RecoveredEvent> events,
        List<string> errors)
    {
        var expected = new HashSet<BattlefieldConquestRecord>();
        foreach (var record in events)
        {
            var e = record.Event;
            if (e.Kind is not ("BATTLEFIELD_CONQUERED" or "BATTLEFIELD_CONTROL_RESOLVED")) continue;
            var payload = JsonSerializer.SerializeToElement(e.Payload);
            if (!payload.TryGetProperty("conquestTurnNumber", out var turn) || !turn.TryGetInt32(out var number)
                || number != state.TurnNumber) continue;
            if (!payload.TryGetProperty("playerId", out var player) || player.ValueKind != JsonValueKind.String
                || !(payload.TryGetProperty("battlefieldObjectId", out var field) || payload.TryGetProperty("battlefieldId", out field))
                || field.ValueKind != JsonValueKind.String
                || !payload.TryGetProperty("conqueredBattlefieldGeneration", out var generation) || !generation.TryGetInt64(out var gen))
            { errors.Add("invalid captured conquest history"); continue; }
            expected.Add(new(player.GetString()!, new(field.GetString()!, gen)));
        }
        var actual = state.BattlefieldConquests?.Entries ?? [];
        if (!expected.SetEquals(actual)) errors.Add("turn battlefield conquests differ from accepted history");
    }
}
