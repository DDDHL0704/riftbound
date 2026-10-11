using Riftbound.Contracts;
namespace Riftbound.Engine;

public sealed record LocationDamageContext(string CardNo, IReadOnlyDictionary<string,ObjectLocationState>? TargetLocations = null);

public sealed partial class CoreRuleEngine
{
    private static string LocationDamageKey(ObjectLocationState location)
        => location.Zone == "BASE" ? "BASE:" + location.PlayerId : "BATTLEFIELD:" + location.BattlefieldObjectId;
    private static string[] LocationDamageTargets(MatchState state, StackItemState item)
        => state.CardObjects.Values.Where(card => FieldObjectTypeRules.IsVisibleUnit(card)
            && IsFieldObjectControlledByZonePlayer(state.PlayerZones, state.CardObjects, card.ObjectId)
            && FindFieldObjectLocation(state.PlayerZones, card.ObjectId) is { } field && field.PlayerId != item.ControllerId
            && state.ObjectLocations.TryGetValue(card.ObjectId, out var location) && location.Zone is "BASE" or "BATTLEFIELD"
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, card.ObjectId))
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();
    private static PendingCardChoiceState LocationDamageChoice(MatchState state, StackItemState item)
    {
        var legal = LocationDamageTargets(state, item);
        var count = legal.Select(id => LocationDamageKey(state.ObjectLocations[id])).Distinct(StringComparer.Ordinal).Count();
        return new("LOCATION-DAMAGE:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId, 0, count,
            legal, legal, "每个位置至多选择一名敌方单位，或不选以放弃。确认并支付适用法盾后双方可以响应。", item.SourceObjectId, item.EffectKind)
            { ResolvingStackItemId = item.StackItemId };
    }
    private static ResolutionResult PrepareLocationDamage(ResolutionResult result, StackItemState item)
    {
        var choice = LocationDamageChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var state = result.State with { PendingCardChoice = choice, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
    private static bool ValidLocationDamageChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.LocationDamage is null || item.TargetGenerations is not null || !ValidLocationDamageStack(state, item)) return false;
        var expected = LocationDamageChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow && choice.PlayerId == expected.PlayerId
            && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.RequiredCount == 0 && choice.MaxCount == expected.MaxCount && choice.MaxCount > 0
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }
    internal static bool ValidLocationDamageStack(MatchState state, StackItemState item)
    {
        if (item.LocationDamage is not { } context) return !item.SourceConfirmed
            || !CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var expected) || expected.PlayDamagePerLocation == 0;
        if (!item.SourceConfirmed || item.CardNo != context.CardNo
            || !CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var behavior)
            || behavior.PlayDamagePerLocation <= 0 || behavior.EffectKind != item.EffectKind) return false;
        if (context.TargetLocations is null) return item.TargetObjectIds.Count == 0 && item.TargetGenerations is null;
        return item.TargetObjectIds.Count > 0 && context.TargetLocations.Count == item.TargetObjectIds.Count
            && item.TargetGenerations is { } generations && generations.Count == item.TargetObjectIds.Count
            && item.TargetObjectIds.All(id => generations.GetValueOrDefault(id, -1) >= 0 && context.TargetLocations.ContainsKey(id))
            && context.TargetLocations.Values.All(location => location is not null && state.Seats.ContainsKey(location.PlayerId)
                && (location.Zone == "BASE" && location.BattlefieldObjectId is null
                    || location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId is { } id && state.CardObjects.TryGetValue(id, out var field)
                        && field.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag)))
            && context.TargetLocations.Values.Select(LocationDamageKey).Distinct(StringComparer.Ordinal).Count() == item.TargetObjectIds.Count;
    }
    private static StackResolutionResult ResolveLocationDamage(MatchState state, StackItemState item)
    {
        var result = NoopStackResolutionResult(state);
        if (item.LocationDamage?.TargetLocations is not { } captured
            || !CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var behavior)) return result;
        var legal = LocationDamageTargets(state, item).ToHashSet(StringComparer.Ordinal);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value); var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent>(); var destroyed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in item.TargetObjectIds)
        {
            if (!legal.Contains(id) || item.TargetGenerations?.GetValueOrDefault(id, -1) != cards[id].ObjectGeneration
                || !captured.TryGetValue(id, out var prior) || LocationDamageKey(prior) != LocationDamageKey(state.ObjectLocations[id])) continue;
            var damage = ApplyDamageToCardObject(cards, item.ControllerId, id, behavior.PlayDamagePerLocation, destroyed,
                state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
            events.Add(new("DAMAGE_APPLIED", "打出技能对每个位置的目标造成伤害", BuildDamagePayload(item.SourceObjectId, id, damage)));
        }
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools, damageTriggeredDestroyTargetObjectIds: destroyed,
            objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return result with { PlayerZones = zones, CardObjects = cards, Events = events, RunePools = cleanup.RunePools,
            TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds, ObjectLocations = ReconcileObjectLocations(locations, zones) };
    }
}
