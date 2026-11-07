using System.Text.Json;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateBattlefieldHolds(MatchState state, List<string> errors)
    {
        if (state.BattlefieldHolds is not { } holds) return;
        if (holds.TurnNumber != state.TurnNumber || holds.Entries is null || holds.Entries.Count == 0
            || holds.Entries.Distinct().Count() != holds.Entries.Count
            || holds.Entries.Any(entry => entry is null || string.IsNullOrWhiteSpace(entry.PlayerId)
                || !state.Seats.ContainsKey(entry.PlayerId) || entry.Battlefield is null
                || string.IsNullOrWhiteSpace(entry.Battlefield.ObjectId) || entry.Battlefield.Generation < 0
                || state.CardObjects.TryGetValue(entry.Battlefield.ObjectId, out var field)
                    && (!field.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag)
                        || field.ObjectGeneration < entry.Battlefield.Generation)))
            errors.Add("invalid turn battlefield holds");
    }

    private static void ValidateBattlefieldHoldHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var expected = new HashSet<BattlefieldHoldRecord>();
        foreach (var record in events)
        {
            if (record.Event.Kind != "BATTLEFIELD_HELD") continue;
            var payload = JsonSerializer.SerializeToElement(record.Event.Payload);
            if (!payload.TryGetProperty("holdTurnNumber", out var turn) || !turn.TryGetInt32(out var number)
                || number != state.TurnNumber) continue;
            if (!commands.Any(c => c.Accepted && c.CompletedTick == record.Tick
                    && c.StartedEventSequence < record.Sequence && c.CompletedEventSequence >= record.Sequence)
                || !payload.TryGetProperty("playerId", out var player) || player.ValueKind != JsonValueKind.String
                || !(payload.TryGetProperty("battlefieldObjectId", out var field) || payload.TryGetProperty("battlefieldId", out field))
                || field.ValueKind != JsonValueKind.String
                || !payload.TryGetProperty("heldBattlefieldGeneration", out var generation) || !generation.TryGetInt64(out var gen))
            { errors.Add("invalid captured hold history"); continue; }
            expected.Add(new(player.GetString()!, new(field.GetString()!, gen)));
        }
        if (!expected.SetEquals(state.BattlefieldHolds?.Entries ?? []))
            errors.Add("turn battlefield holds differ from original accepted history");
    }
}
