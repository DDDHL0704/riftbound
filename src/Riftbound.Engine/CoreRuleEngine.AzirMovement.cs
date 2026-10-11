using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record AzirMovementContext(ObjectBinding Source, ObjectBinding? Equipment = null);

public sealed partial class CoreRuleEngine
{
    internal static bool CanActivateSwiftAbility(MatchState state, string player)
        => state.Status == MatchStatuses.InProgress && state.Phase == MatchPhases.Main
            && state.PendingPayment is null && state.PendingHandChoice is null && state.PendingCardChoice is null
            && state.PendingEffectPlay is null && !ResolutionResult.HasBlockingPendingTaskQueue(state)
            && (state.TimingState == TimingStates.NeutralOpen && state.ActivePlayerId == player && state.StackItems.Count == 0
                || state.TimingState == TimingStates.SpellDuelOpen && state.FocusPlayerId == player
                    && string.IsNullOrWhiteSpace(state.PriorityPlayerId));

    internal static bool ValidAzirMovement(MatchState state, StackItemState item)
    {
        if (item.AzirMovement is not { } context)
            return item.EffectKind != P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityEffectKind;
        bool BindingValid(ObjectBinding binding) => !string.IsNullOrWhiteSpace(binding.ObjectId) && binding.Generation >= 0
            && (!state.CardObjects.TryGetValue(binding.ObjectId, out var card) || binding.Generation <= card.ObjectGeneration);
        if (item.EffectKind != P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityEffectKind
            || !P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId(P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, item.CardNo)
            || !state.Seats.ContainsKey(item.ControllerId) || item.SourceConfirmed || item.EffectRepeatCount != 1
            || item.RepeatExecutions is not null || context.Source.ObjectId != item.SourceObjectId || !BindingValid(context.Source)
            || item.TargetObjectIds.Count != 1 || item.TargetObjectIds[0] == item.SourceObjectId
            || item.TargetGenerations is not { Count: 1 } bindings
            || !bindings.TryGetValue(item.TargetObjectIds[0], out var generation)
            || !BindingValid(new(item.TargetObjectIds[0], generation))
            || state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                && source.ObjectGeneration == context.Source.Generation && source.CardNo != item.CardNo)
            return false;
        return context.Equipment is null ? item.OptionalCosts.Count == 0
            : BindingValid(context.Equipment) && context.Equipment.ObjectId != item.SourceObjectId
                && context.Equipment.ObjectId != item.TargetObjectIds[0]
                && item.OptionalCosts.SequenceEqual([P4ActivatedAbilityCatalog.AzirArmamentReattachOptionalCostId(context.Equipment.ObjectId)]);
    }

    // One move instruction, including all equipment following its host. Equipment
    // keeps its own owner/controller and never creates a unit-movement event.
    private static bool TryMoveFieldUnitByEffect(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, ObjectLocationState> locations,
        string unitId, ObjectLocationState destination, string actor, StackItemState item, List<GameEvent> events)
    {
        var before = state with { PlayerZones = zones.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
            CardObjects = cards.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
            ObjectLocations = locations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal) };
        if (!cards.TryGetValue(unitId, out var unit) || !IsFaceUpNonStandbyUnit(unit)
            || !TryGetPreciseFieldLocation(zones, locations, unitId, out var origin)
            || !MovementRestrictionRules.CanMove(unit, actor)) return false;
        var controller = EffectiveFieldControllerId(zones, unitId, unit);
        if (destination.Zone == "BASE")
        {
            if (destination.PlayerId != controller || BattlefieldLocalRules.PreventsMoveToBase(before, unitId)) return false;
        }
        else if (destination.Zone != "BATTLEFIELD" || BattlefieldLocalRules.Battlefield(before, destination.BattlefieldObjectId) is null)
            return false;
        var next = new ObjectLocationState(controller, destination.Zone, destination.BattlefieldObjectId);
        if (origin.Zone == next.Zone && origin.BattlefieldObjectId == next.BattlefieldObjectId
            && (origin.Zone != "BASE" || origin.PlayerId == next.PlayerId)) return false;
        var captured = CaptureMovementObservers(before, cards, unitId, actor, next.Zone, next.BattlefieldObjectId);
        RemoveFieldObjectFromLocation(zones, origin.PlayerId, origin.Zone, unitId);
        AddFieldObjectToLocation(zones, controller, next.Zone, unitId);
        locations[unitId] = next;
        foreach (var gear in cards.Values.Where(c => c.Tags.Contains(CardObjectTags.EquipmentCard)
            && c.AttachedToObjectId == unitId && IsObjectOnField(zones, c.ObjectId)).ToArray())
        {
            var gearController = EffectiveFieldControllerId(zones, gear.ObjectId, gear);
            var gearOrigin = FindFieldObjectLocation(zones, gear.ObjectId)!.Value;
            RemoveFieldObjectFromLocation(zones, gearOrigin.PlayerId, gearOrigin.Zone, gear.ObjectId);
            AddFieldObjectToLocation(zones, gearController, next.Zone, gear.ObjectId);
            locations[gear.ObjectId] = new(gearController, next.Zone, next.BattlefieldObjectId);
            events.Add(new("ATTACHED_EQUIPMENT_FOLLOWED", "已贴附装备随宿主移动", new Dictionary<string, object?> {
                ["equipmentObjectId"] = gear.ObjectId, ["unitObjectId"] = unitId,
                ["controllerId"] = gearController, ["ownerId"] = gear.OwnerId,
                ["originLocation"] = BuildLocationPayload(before.ObjectLocations[gear.ObjectId]),
                ["destinationLocation"] = BuildLocationPayload(locations[gear.ObjectId]) }));
        }
        events.Add(new(next.Zone == "BASE" ? "UNIT_MOVED_TO_BASE" : "UNIT_MOVED_TO_BATTLEFIELD", "技能移动单位", new Dictionary<string, object?> {
            ["playerId"] = actor, ["movingPlayerId"] = actor, ["controllerId"] = controller,
            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = unitId,
            ["battlefieldObjectId"] = next.BattlefieldObjectId, ["stackItemId"] = item.StackItemId,
            ["effectKind"] = item.EffectKind, ["originLocation"] = BuildLocationPayload(origin),
            ["destinationLocation"] = BuildLocationPayload(next) }));
        events.Add(captured);
        return true;
    }

    private static StackResolutionResult ResolveAzirMovement(MatchState state, StackItemState item)
    {
        if (!ValidAzirMovement(state, item)) return NoopStackResolutionResult(state);
        var context = item.AzirMovement!;
        var targetId = item.TargetObjectIds[0];
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "阿兹尔的迅捷移动技能结算", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["controllerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
            ["cardNo"] = item.CardNo, ["stackItemId"] = item.StackItemId, ["abilityId"] = P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId,
            ["effectKind"] = item.EffectKind, ["targetObjectIds"] = item.TargetObjectIds.ToArray(),
            ["optionalCosts"] = item.OptionalCosts.ToArray(), ["selectedArmamentObjectId"] = context.Equipment?.ObjectId,
            ["armamentReattachPolicy"] = P4ActivatedAbilityCatalog.AzirArmamentReattachPolicy }) };
        if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var source) || !IsFaceUpNonStandbyUnit(source)
            || source.ObjectGeneration != context.Source.Generation
            || !state.CardObjects.TryGetValue(targetId, out var target) || target.ObjectGeneration != item.TargetGenerations![targetId]
            || !IsLegalAzirSwiftSwapTarget(state, item.ControllerId, item.SourceObjectId, targetId)
            || !TryGetPreciseFieldLocation(state.PlayerZones, state.ObjectLocations, item.SourceObjectId, out var sourceOrigin)
            || !TryGetPreciseFieldLocation(state.PlayerZones, state.ObjectLocations, targetId, out var targetOrigin))
        {
            events.Add(new("ABILITY_NO_EFFECT", "移动对象不再合法，已支付的费用不退回", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                ["abilityId"] = P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, ["stackItemId"] = item.StackItemId,
                ["armamentReattachApplied"] = false, ["reason"] = "TARGET_NO_LONGER_LEGAL" }));
            return NoopStackResolutionResult(state) with { Events = events };
        }
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var sourceMoved = TryMoveFieldUnitByEffect(state, zones, cards, locations, item.SourceObjectId, targetOrigin, item.ControllerId, item, events);
        var targetMoved = TryMoveFieldUnitByEffect(state, zones, cards, locations, targetId, sourceOrigin, item.ControllerId, item, events);
        var attached = false;
        if (context.Equipment is { } binding && cards.TryGetValue(binding.ObjectId, out var gear)
            && gear.ObjectGeneration == binding.Generation && IsLegalAzirArmamentReattachChoice(cards, targetId, binding.ObjectId))
        {
            attached = TryAttachEquipment(zones, cards, locations, item.ControllerId, item.SourceObjectId,
                binding.ObjectId, item.SourceObjectId, item.EffectKind, events, out var nextLocations,
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                    ["abilityId"] = P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, ["stackItemId"] = item.StackItemId,
                    ["targetObjectId"] = targetId });
            locations = nextLocations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        }
        if (sourceMoved || targetMoved)
            events.Add(new("UNIT_LOCATIONS_SWAPPED", "阿兹尔的两次移动指示完成", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                ["firstTargetObjectId"] = item.SourceObjectId, ["secondTargetObjectId"] = targetId,
                ["abilityId"] = P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, ["effectKind"] = item.EffectKind,
                ["stackItemId"] = item.StackItemId, ["sourceOriginLocation"] = BuildLocationPayload(sourceOrigin),
                ["sourceDestinationLocation"] = BuildLocationPayload(locations[item.SourceObjectId]),
                ["targetOriginLocation"] = BuildLocationPayload(targetOrigin), ["targetDestinationLocation"] = BuildLocationPayload(locations[targetId]),
                ["sourceMoved"] = sourceMoved, ["targetMoved"] = targetMoved,
                ["selectedArmamentObjectId"] = context.Equipment?.ObjectId, ["armamentReattachApplied"] = attached,
                ["armamentReattachPolicy"] = P4ActivatedAbilityCatalog.AzirArmamentReattachPolicy }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, Events = events };
    }
}
