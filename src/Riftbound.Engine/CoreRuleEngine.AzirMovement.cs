using Riftbound.CardCatalog;
using Riftbound.Contracts;
using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public sealed record AzirMovementContext(ObjectBinding Source, ObjectBinding? Equipment = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool CopiedInstance = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool MovementsCompleted = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? AttachmentChosen = null);

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
            || context.CopiedInstance != (context.Origin is not null)
            || !AttachedRuleTextRules.ValidCaptured(state, context.Origin, item.SourceObjectId, context.Source.Generation, item.CardNo!)
            || item.TargetObjectIds.Count != (context.Equipment is null ? 1 : 2) || item.TargetObjectIds[0] == item.SourceObjectId
            || item.TargetGenerations is not { } bindings || bindings.Count != item.TargetObjectIds.Count
            || !bindings.TryGetValue(item.TargetObjectIds[0], out var generation)
            || !BindingValid(new(item.TargetObjectIds[0], generation))
            || state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var target)
                && target.ObjectGeneration == generation && !target.Tags.Contains(CardObjectTags.UnitCard)
            || state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                && source.ObjectGeneration == context.Source.Generation && source.CardNo != item.CardNo)
            return false;
        if (item.OptionalCosts.Count != 0 || !context.MovementsCompleted && context.AttachmentChosen is not null
            || context.MovementsCompleted && context.Equipment is null) return false;
        return context.Equipment is null ? true
            : BindingValid(context.Equipment) && context.Equipment.ObjectId != item.SourceObjectId
                && context.Equipment.ObjectId != item.TargetObjectIds[0]
                && item.TargetObjectIds[1] == context.Equipment.ObjectId
                && bindings.TryGetValue(context.Equipment.ObjectId, out var gearGeneration) && gearGeneration == context.Equipment.Generation
                && (!state.CardObjects.TryGetValue(context.Equipment.ObjectId, out var gear)
                    || gear.ObjectGeneration != gearGeneration
                    || gear.Tags.Contains(CardObjectTags.EquipmentCard) && OfficialArmamentCatalog.Find(gear.CardNo) is not null);
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
        if (item.AzirMovement!.MovementsCompleted) return ResolveAzirAttachmentAfterMovements(state, item);
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
        if (sourceMoved || targetMoved)
            events.Add(new("UNIT_LOCATIONS_SWAPPED", "阿兹尔的两次移动指示完成", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                ["firstTargetObjectId"] = item.SourceObjectId, ["secondTargetObjectId"] = targetId,
                ["abilityId"] = P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, ["effectKind"] = item.EffectKind,
                ["stackItemId"] = item.StackItemId, ["sourceOriginLocation"] = BuildLocationPayload(sourceOrigin),
                ["sourceDestinationLocation"] = BuildLocationPayload(locations[item.SourceObjectId]),
                ["targetOriginLocation"] = BuildLocationPayload(targetOrigin), ["targetDestinationLocation"] = BuildLocationPayload(locations[targetId]),
                ["sourceMoved"] = sourceMoved, ["targetMoved"] = targetMoved,
                ["selectedArmamentObjectId"] = context.Equipment?.ObjectId, ["armamentReattachApplied"] = false,
                ["armamentReattachPolicy"] = P4ActivatedAbilityCatalog.AzirArmamentReattachPolicy }));
        var result = NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, Events = events };
        var movedState = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
        if (!CanExecuteAzirAttachment(movedState, item)) return result;
        var changed = item with { AzirMovement = context with { MovementsCompleted = true } };
        var choice = AzirAttachmentChoice(changed);
        events[0] = events[0] with { Kind = "ABILITY_MOVEMENTS_COMPLETED", Description = "阿兹尔移动指示已执行，等待可选贴附决定" };
        events.Add(new("CARD_CHOICE_REQUESTED", "移动完成后决定是否贴附已确认的武装", new Dictionary<string, object?> {
            ["choiceId"] = choice.ChoiceId, ["choiceWindow"] = choice.ChoiceWindow, ["playerId"] = choice.PlayerId,
            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectIds"] = item.TargetObjectIds.ToArray() }));
        return result with { PendingCardChoice = choice, StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(changed).ToArray(), Events = events };
    }
}
