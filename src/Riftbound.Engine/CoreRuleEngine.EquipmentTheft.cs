using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool TryResolveSourceStealEnemyEquipment(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, CardBehaviorDefinition behavior, StackItemState item,
        List<GameEvent> events, out IReadOnlyDictionary<string, ObjectLocationState> resolvedLocations)
    {
        resolvedLocations = state.ObjectLocations;
        if (!behavior.StealsTargetEquipmentUntilSourceLeaves || !item.SourceConfirmed
            || !TargetEffectAdditionalCostPaid(behavior, item.OptionalCosts)
            || item.TargetObjectIds.Count != 1) return false;
        var target = item.TargetObjectIds[0];
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        MatchState Current() => state with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations, RunePools = pools };
        bool Movable(MatchState current) => MovementTargetIsCurrent(current, item, target)
            && IsEnemyEquipmentObject(current, item.ControllerId, target)
            && current.CardObjects.TryGetValue(target, out var gear) && !gear.IsFaceDown
            && MovementRestrictionRules.CanMove(gear, item.ControllerId)
            && (!FieldObjectTypeRules.IsVisibleUnit(gear)
                || CanMoveUnitWithAttachedEquipmentForEffect(current, item.ControllerId, target)
                    && !BattlefieldLocalRules.PreventsMoveToBase(current, target));
        if (!Movable(Current())) return false;
        if (FieldObjectTypeRules.IsVisibleUnit(cards[target])
            && PayImposedMovementCost(state, item.ControllerId, item.SourceObjectId, null, false,
                zones, cards, locations, pools, current => Movable(current) ? [target] : [], events)
                != ImposedMovementPaymentResult.Paid) return false;
        var before = Current();
        if (!Movable(before) || FindFieldObjectLocation(zones, target) is not { } origin) return false;
        var source = PlayInstructionSource(state, item);
        var hasSource = source is { } binding && cards.TryGetValue(binding.ObjectId, out var unit)
            && unit.ObjectGeneration == binding.Generation && IsObjectOnField(zones, binding.ObjectId);
        var publicSource = ResolutionResult.VisibleObjectIdForViewer(before, item.SourceObjectId, ProjectionViewers.Spectator);
        // Move first, retaining the former controller for all movement observers.
        var moved = CaptureMovedUnitFact(before, target, item.ControllerId, MoveUnitBaseZone,
            destinationPlayerId: item.ControllerId);
        var movement = moved is null ? null : CaptureMovementBatchObservers(before, cards, [moved]);
        var nonUnit = moved is null ? CaptureUnitEvents(zones, cards, [target], "MOVE")
            .Select(observation => observation with { Context = observation.Context with {
                Here = new(item.ControllerId, MoveUnitBaseZone) } }).ToArray() : [];
        RemoveFieldObjectFromLocation(zones, origin.PlayerId, origin.Zone, target);
        AddFieldObjectToLocation(zones, item.ControllerId, MoveUnitBaseZone, target);
        var equipment = cards[target] with { AttachedToObjectId = null };
        cards[target] = equipment;
        locations[target] = new(item.ControllerId, MoveUnitBaseZone);
        events.Add(WithUnitEvents(new(moved is null ? "EQUIPMENT_MOVED_TO_BASE" : "UNIT_MOVED_TO_BASE", "效果移动装备到基地",
            new Dictionary<string, object?> {
                ["sourceObjectId"] = publicSource, ["targetObjectId"] = target, ["equipmentObjectId"] = target,
                ["unitObjectId"] = moved?.ObjectId, ["playerId"] = origin.PlayerId,
                ["movingPlayerId"] = item.ControllerId, ["originZone"] = origin.Zone,
                ["originBattlefieldObjectId"] = before.ObjectLocations.GetValueOrDefault(target)?.BattlefieldObjectId,
                ["destinationZone"] = MoveUnitBaseZone, ["destinationPlayerId"] = item.ControllerId,
                ["destination"] = "BASE:" + item.ControllerId }), nonUnit));
        if (movement is not null) events.Add(movement);
        // CN 359.3.e.16 explicitly uses this instruction as its example:
        // a departed source cannot create the delayed control effect.
        if (hasSource)
        {
            var reason = SourceStealEnemyEquipmentReason(behavior);
            cards[target] = equipment with {
                ControllerId = item.ControllerId,
                Tags = AddSourceStolenEquipmentMarker(equipment.Tags, item.SourceObjectId, reason) };
            events.Add(new("EQUIPMENT_CONTROL_CHANGED", "取得装备的控制权，直到来源离场",
                new Dictionary<string, object?> {
                    ["sourceObjectId"] = publicSource, ["equipmentObjectId"] = target,
                    ["previousControllerId"] = equipment.ControllerId, ["controllerId"] = item.ControllerId,
                    ["ownerId"] = equipment.OwnerId, ["equipmentCardNo"] = equipment.CardNo,
                    ["originZone"] = origin.Zone, ["destinationZone"] = MoveUnitBaseZone,
                    ["attachedToObjectId"] = (string?)null, ["reason"] = reason,
                    ["optionalCosts"] = item.OptionalCosts.ToArray() }));
            if (equipment.Tags.Contains(CardEquipmentKeywordNames.Weapon, StringComparer.Ordinal))
                TryAttachEquipment(before with { ObjectLocations = locations }, zones, cards, locations,
                    item.ControllerId, item.SourceObjectId, target, item.SourceObjectId, reason, events,
                    out resolvedLocations, new Dictionary<string, object?> {
                        ["unitCardNo"] = cards[item.SourceObjectId].CardNo, ["optionalCosts"] = item.OptionalCosts.ToArray() });
            else resolvedLocations = locations;
        }
        else resolvedLocations = locations;
        return true;
    }
}
