using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateOriginalEffectPlayLocation(MatchState state, IReadOnlyList<RecoveredEvent> events,
        bool historyRequired, List<string> errors)
    {
        if (!historyRequired || state.PendingEffectPlay is not { DestinationPolicy: "ORIGINAL_LOCATION" } pending) return;
        var source = pending.Sources.Count == 1 ? pending.Sources.Keys.Single() : null;
        var accepted = events.LastOrDefault(row => row.Event.Kind == "UNIT_BANISHED"
            && TryReadObjectString(row.Event.Payload, "parentStackItemId", out var parent) && parent == pending.Parent.StackItemId
            && TryReadObjectString(row.Event.Payload, "targetObjectId", out var target) && target == source)?.Event;
        if (source is null || accepted is null
            || !accepted.Payload.TryGetValue("originalLocation", out var location)
            || !accepted.Payload.TryGetValue("originalBattlefield", out var field)
            || !TryReadLocation<ObjectLocationState>(location, out var originalLocation)
            || !TryReadLocation<ObjectBinding>(field, out var originalField)
            || originalLocation != pending.OriginalLocation || originalField != pending.OriginalBattlefield)
            errors.Add("original effect-play location differs from accepted banishment");
    }

    private static bool TryReadLocation<T>(object? raw, out T? value)
    {
        try { value = JsonSerializer.SerializeToElement(raw).Deserialize<T>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); return true; }
        catch (JsonException) { value = default; return false; }
    }
}
