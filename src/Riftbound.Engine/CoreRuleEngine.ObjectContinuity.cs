namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 124: movement within the field preserves the object. Crossing into or
    // out of a nonfield zone starts a new game object, even for the same card ID.
    private static ResolutionResult ApplyObjectContinuity(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var after = result.State;
        var previousLocations = IdentityLocations(before);
        var nextLocations = IdentityLocations(after);
        var cards = after.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var changed = false;
        foreach (var (id, card) in after.CardObjects)
        {
            if (!before.CardObjects.TryGetValue(id, out var previous)) continue;
            var crossedZone = previousLocations.TryGetValue(id, out var oldLocation)
                && nextLocations.TryGetValue(id, out var newLocation)
                && oldLocation != newLocation
                && (!IsIdentityFieldZone(oldLocation.Zone) || !IsIdentityFieldZone(newLocation.Zone));
            var generation = Math.Max(card.ObjectGeneration, checked(previous.ObjectGeneration + (crossedZone ? 1 : 0)));
            if (card.ObjectGeneration == generation) continue;
            cards[id] = card with { ObjectGeneration = generation };
            changed = true;
        }

        var previousItems = before.StackItems.ToDictionary(item => item.StackItemId, StringComparer.Ordinal);
        var stack = after.StackItems.Select(item =>
        {
            if (previousItems.TryGetValue(item.StackItemId, out var previousItem)
                && previousItem.TargetObjectIds.SequenceEqual(item.TargetObjectIds, StringComparer.Ordinal)) return item;
            var bindings = item.TargetObjectIds.Distinct(StringComparer.Ordinal)
                .Where(id => cards.ContainsKey(id))
                .ToDictionary(id => id, id => before.CardObjects.TryGetValue(id, out var previous)
                    ? previous.ObjectGeneration : cards[id].ObjectGeneration, StringComparer.Ordinal);
            if (bindings.Count == 0) return item;
            changed = true;
            return item with { TargetGenerations = bindings };
        }).ToArray();
        if (!changed) return result;
        after = after with { CardObjects = cards, StackItems = stack };
        // Generations are private engine bookkeeping. The explicit snapshot and
        // prompt projections contain neither field, so retain their existing views.
        return result with { State = after };
    }

    private static Dictionary<string, ObjectLocationState> IdentityLocations(MatchState state)
    {
        var locations = ReconcileObjectLocations(new Dictionary<string, ObjectLocationState>(), state.PlayerZones);
        foreach (var item in state.StackItems)
            if (!string.IsNullOrWhiteSpace(item.SourceObjectId) && state.CardObjects.ContainsKey(item.SourceObjectId)
                && !locations.ContainsKey(item.SourceObjectId))
                locations[item.SourceObjectId] = new(item.ControllerId, "STACK");
        return locations;
    }

    private static bool IsIdentityFieldZone(string zone) => zone is "BASE" or "BATTLEFIELD" or "LEGEND";

    private static StackItemState MaskTargetsFromPreviousGenerations(MatchState state, StackItemState item)
    {
        if (item.TargetGenerations is null) return item;
        return item with
        {
            TargetObjectIds = item.TargetObjectIds.Select(id =>
                item.TargetGenerations.TryGetValue(id, out var generation)
                && (!state.CardObjects.TryGetValue(id, out var current) || current.ObjectGeneration != generation)
                    ? string.Empty : id).ToArray()
        };
    }

    private static StackItemState MaskTargetsNoLongerLegal(MatchState state, StackItemState item, CardBehaviorDefinition behavior)
    {
        var scope = IsStandbyReactionStackItem(behavior, item) && !string.IsNullOrWhiteSpace(behavior.StandbyReactionTargetScope)
            ? behavior.StandbyReactionTargetScope : PlayCardTargetScopeForBehavior(behavior);
        // Preserve target positions: "first friendly, second enemy" must not
        // silently shift when one target becomes illegal. Independent draw and
        // other untargeted instructions still resolve (CN 359.3.e.5-8).
        return item with { TargetObjectIds = item.TargetObjectIds.Select((id, index) =>
            IsLegalChosenCardTarget(state, item.ControllerId, id, index, item.TargetObjectIds, scope, behavior)
                ? id : string.Empty).ToArray() };
    }

    private static bool IsLegalChosenCardTarget(MatchState state, string playerId, string objectId, int index,
        IReadOnlyList<string> targets, string scope, CardBehaviorDefinition behavior)
        => !string.IsNullOrWhiteSpace(objectId)
            && IsTargetObjectInScope(state, playerId, objectId, scope, index)
            && TargetProtectionRules.IsLegalPlayCardSpellOrSkillTarget(state, playerId, behavior, objectId)
            && IsVisibleFieldUnitPrimitiveTargetAllowed(state, behavior, objectId)
            && IsPublicUnitMainDeckPrimitiveTargetAllowed(state, behavior, objectId)
            && IsMainDeckLookTargetAllowed(state, playerId, objectId, index, behavior)
            && IsMainDeckTargetTagAllowed(state, objectId, index, behavior)
            && IsTargetRequiredTagAllowed(state, objectId, behavior)
            && IsTargetTagAllowed(state, objectId, behavior)
            && IsTargetManaCostAllowed(state, playerId, objectId, behavior)
            && IsStackItemTargetConditionAllowed(state, playerId, objectId, index, targets, behavior)
            && IsTargetPowerAllowed(state, objectId, behavior);
}
