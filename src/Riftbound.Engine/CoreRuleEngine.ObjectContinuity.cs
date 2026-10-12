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
            var movementCount = nextLocations.TryGetValue(id, out var nextLocation) && nextLocation.Zone is "BASE" or "BATTLEFIELD"
                ? card.MovementCountThisTurn : 0;
            var damageMarkers = crossedZone || card.Damage == 0 || !IsObjectOnField(after.PlayerZones, id)
                ? null : card.DamageMarkedByPlayerIds;
            var grantedTags = crossedZone || !IsObjectOnField(after.PlayerZones, id) ? null : card.GrantedTags;
            if ((!crossedZone || card.CopyFaceTransitions is null) && card.GrantedTags == grantedTags
                && card.ObjectGeneration == generation && card.MovementCountThisTurn == movementCount
                && card.DamageMarkedByPlayerIds == damageMarkers) continue;
            cards[id] = card with { CopyFaceTransitions = crossedZone ? null : card.CopyFaceTransitions,
                GrantedTags = grantedTags, ObjectGeneration = generation, MovementCountThisTurn = movementCount, DamageMarkedByPlayerIds = damageMarkers };
            changed = true;
        }

        // Ending an attachment removes its granted text. Reattaching preserves
        // the equipment object but creates a fresh text revision at the event.
        foreach (var gear in cards.Values.Where(c => c.AttachedRuleText is not null || c.AttachmentRevision > 0).ToArray())
        {
            var copy = gear.AttachedRuleText;
            if (copy is not null && cards.TryGetValue(copy.Unit.ObjectId, out var host)
                && before.CardObjects.TryGetValue(host.ObjectId, out var oldHost)
                && copy.Unit.Generation == oldHost.ObjectGeneration && host.ObjectGeneration != oldHost.ObjectGeneration
                && !AttachedRuleTextRules.Field(before, host.ObjectId) && AttachedRuleTextRules.Field(after, host.ObjectId))
                copy = copy with { Unit = copy.Unit with { Generation = host.ObjectGeneration } };
            var revised = gear with { AttachedRuleText = copy,
                AttachmentRevision = AttachedRuleTextRules.Field(after, gear.ObjectId) ? gear.AttachmentRevision : 0 };
            if (copy is not null && !AttachedRuleTextRules.Bound(after with { CardObjects = cards }, revised))
                revised = revised with { AttachedRuleText = null };
            if (revised == gear) continue;
            cards[gear.ObjectId] = revised; changed = true;
        }

        var previousItems = before.StackItems.ToDictionary(item => item.StackItemId, StringComparer.Ordinal);
        var stack = after.StackItems.Select(item =>
        {
            if (item.PermanentPlayTargetsPending) return item;
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
        // Generations are private; movement counts are public for visible units.
        return result with { State = after, Snapshots = ResolutionResult.BuildSnapshots(after), Prompts = BuildCorePrompts(after) };
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
            && IsTargetObjectInScope(state, playerId, objectId, scope, index, behavior.CanCounterAbilities)
            && IsSelectedBattlefieldTargetRelationAllowed(state, behavior, targets, index)
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
