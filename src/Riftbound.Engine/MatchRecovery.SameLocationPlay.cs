using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateOriginalEffectPlayLocation(MatchState state, IReadOnlyList<RecoveredEvent> events,
        bool historyRequired, List<string> errors)
    {
        if (historyRequired && state.PendingEffectPlay?.Parent is { DeckPlaySequence.OwnedReplay: { } replay } parent)
        {
            if (replay.Units is null) errors.Add("owned replay facts are missing");
            foreach (var fact in replay.Units ?? [])
            {
                if (fact?.Unit is null)
                {
                    errors.Add("owned replay fact has no unit binding");
                    continue;
                }
                var acceptedFact = events.LastOrDefault(row => row.Event.Kind == "UNIT_BANISHED"
                    && TryReadObjectString(row.Event.Payload, "parentStackItemId", out var parentId) && parentId == parent.StackItemId
                    && TryReadObjectString(row.Event.Payload, "targetObjectId", out var id) && id == fact.Unit.ObjectId)?.Event;
                if (acceptedFact is null
                    || !TryReadObjectLong(acceptedFact.Payload, "targetGeneration", out var generation) || generation != fact.Unit.Generation
                    || !TryReadObjectInt(acceptedFact.Payload, "executionIndex", out var round) || round != fact.ExecutionIndex
                    || !TryReadObjectString(acceptedFact.Payload, "ownerPlayerId", out var owner) || owner != fact.OwnerId
                    || !TryReadObjectString(acceptedFact.Payload, "cardNo", out var card) || card != fact.CardNo
                    || !acceptedFact.Payload.TryGetValue("originalLocation", out var at)
                    || !TryReadLocation<ObjectLocationState>(at, out var expectedAt) || expectedAt != fact.Location
                    || !acceptedFact.Payload.TryGetValue("originalBattlefield", out var original)
                    || !TryReadLocation<ObjectBinding>(original, out var expectedField) || expectedField != fact.Battlefield)
                    errors.Add("owned replay fact differs from accepted banishment");
            }
        }
        if (!historyRequired || state.PendingEffectPlay is not { DestinationPolicy: "ORIGINAL_LOCATION" or "ORIGINAL_LOCATION_OR_BASE" } pending) return;
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
