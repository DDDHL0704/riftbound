using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record EquipmentControlGrant(string SkillId, ObjectBinding Source, string ControllerId, string Reason, long CreatedAtTick = 0);
public sealed record EquipmentControlLease(ObjectBinding Equipment, string UnderlyingControllerId,
    IReadOnlyList<EquipmentControlGrant> Grants);

public sealed partial class CoreRuleEngine
{
    internal static bool IsEquipmentTheftPlaySkill(StackItemState item)
        => item.SourceConfirmed && CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
            && behavior.StealsTargetEquipmentUntilSourceLeaves;

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
        var source = item.PermanentPlaySource ?? PlayInstructionSource(state, item);
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
        if (moved is not null)
        {
            var attached = AttachedEquipmentObjectIds(cards, target);
            events.AddRange(MoveAttachedEquipmentWithHost(zones, attached, item.ControllerId, target, MoveUnitBaseZone));
            foreach (var id in attached) locations[id] = new(item.ControllerId, MoveUnitBaseZone);
        }
        // CN 359.3.e.16 explicitly uses this instruction as its example:
        // a departed source cannot create the delayed control effect.
        if (hasSource)
        {
            var reason = SourceStealEnemyEquipmentReason(behavior);
            var lease = equipment.EquipmentControl;
            var original = new ObjectBinding(target, equipment.ObjectGeneration);
            var grants = lease?.Equipment == original ? lease.Grants : [];
            var underlying = lease?.Equipment == original ? lease.UnderlyingControllerId
                : equipment.UnitControl?.UnderlyingControllerId ?? equipment.ControllerId ?? origin.PlayerId;
            // The grant can appear in an attachment-choice preview. The
            // instruction cursor is stable when that choice is replayed;
            // the eventual confirmation command's completion tick is not.
            cards[target] = equipment with {
                ControllerId = item.ControllerId,
                EquipmentControl = new(original, underlying,
                    [..grants, new(item.StackItemId, source!, item.ControllerId, reason, RuleChoices.Value!.AnswerTick)]) };
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

    internal static void ReturnSourceStolenEquipmentForMissingSources(Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        foreach (var gear in cards.Values.Where(card => card.EquipmentControl is not null)
            .OrderBy(card => card.ObjectId, StringComparer.Ordinal).ToArray())
        {
            var lease = gear.EquipmentControl!;
            if (lease.Equipment != new ObjectBinding(gear.ObjectId, gear.ObjectGeneration)
                || !IsObjectOnField(zones, gear.ObjectId))
            {
                cards[gear.ObjectId] = gear with { EquipmentControl = null };
                continue;
            }
            var active = lease.Grants.Where(grant => cards.TryGetValue(grant.Source.ObjectId, out var source)
                && source.ObjectGeneration == grant.Source.Generation && IsObjectOnField(zones, source.ObjectId)).ToArray();
            if (active.Length == lease.Grants.Count) continue;
            var attachment = gear.AttachedToObjectId is { } host && IsObjectOnField(zones, host)
                ? host : null;
            var remaining = gear with { AttachedToObjectId = attachment,
                EquipmentControl = active.Length == 0 ? null : lease with { Grants = active } };
            var controller = LayeredControlController(remaining, lease.UnderlyingControllerId);
            cards[gear.ObjectId] = remaining with { ControllerId = controller };
            if (controller == gear.ControllerId) continue;
            var ended = lease.Grants[^1];
            events.Add(new("EQUIPMENT_CONTROL_RETURNED", "装备控制效果的持续时间结束",
                new Dictionary<string, object?> {
                    ["sourceObjectId"] = ended.Source.ObjectId, ["equipmentObjectId"] = gear.ObjectId,
                    ["previousControllerId"] = gear.ControllerId, ["controllerId"] = controller,
                    ["ownerId"] = gear.OwnerId, ["equipmentCardNo"] = gear.CardNo,
                    ["originZone"] = FindFieldObjectLocation(zones, gear.ObjectId)?.Zone,
                    ["attachedToObjectId"] = attachment, ["reason"] = ended.Reason }));
            // Ending control does not itself detach or move equipment. CN 107.1.c.1
            // permits attachment across controllers; CN 149.3 handles loose gear.
        }
    }
}
