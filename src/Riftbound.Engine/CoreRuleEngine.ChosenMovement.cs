using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static IReadOnlyList<string> ChosenMovementTargets(CardBehaviorDefinition behavior, IReadOnlyList<string> targets)
        => behavior.ChosenMovementTargetIndex is { } index
            ? targets.Skip(index).Take(1).ToArray()
            : behavior.OptionalEquipmentAfterMovement ? targets.Take(1).ToArray() : targets;
    internal static bool IsChosenMovementTargetIndex(CardBehaviorDefinition behavior, int index)
        => behavior.ChosenMovementTargetIndex is { } chosen ? index == chosen
            : !behavior.OptionalEquipmentAfterMovement || index == 0;
    internal static IReadOnlyList<ActionPromptChoiceDto> ChosenMovementDestinations(MatchState state, string playerId)
        => state.Seats.Keys.Order(StringComparer.Ordinal)
            .Select(id => new ActionPromptChoiceDto("BASE:" + id, $"{state.Seats[id]} 的基地"))
            .Concat(state.CardObjects.Keys.Order(StringComparer.Ordinal)
                .Where(id => BattlefieldLocalRules.Battlefield(state, id) is not null)
                .Select(id => new ActionPromptChoiceDto("BATTLEFIELD:" + id,
                    CardBehaviorRegistry.TryGetByCardNo(state.CardObjects[id].CardNo ?? "", out var behavior)
                        ? $"{behavior.DisplayName}（{id}）" : id))).ToArray();

    internal static bool HasChosenMovementDestination(MatchState state, string player, string target)
        => ChosenMovementDestinations(state, player).Any(choice => IsUnitMovementDestination(state, target, choice.Id));

    internal static bool IsEquipmentMovementDestination(MatchState state, string equipmentObjectId, string destination)
    {
        if (!destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)) return false;
        var field = destination[12..];
        if (BattlefieldLocalRules.Battlefield(state, field) is null) return false;
        var location = FindFieldObjectLocation(state.PlayerZones, equipmentObjectId);
        return location is not null
            && (location.Value.Zone != "BATTLEFIELD"
                || state.ObjectLocations.GetValueOrDefault(equipmentObjectId)?.BattlefieldObjectId != field);
    }

    internal static bool IsEquipmentMovementDestinationAllowed(MatchState state, string playerId,
        string destination, IReadOnlyList<string> targets)
        => (targets.Count == 1 || targets.Count == 2)
            && ChosenMovementDestinations(state, playerId).Any(choice => choice.Id == destination)
            && (targets.Count != 2 || destination == "BATTLEFIELD:" + targets[1])
            && IsEquipmentMovementDestination(state, targets[0], destination);

    internal static string[] MoveFriendlyEquipmentToChosenBattlefield(
        MatchState state,
        Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards,
        Dictionary<string, ObjectLocationState> locations,
        Dictionary<string, RunePool> pools,
        StackItemState item,
        string destination,
        List<GameEvent> events)
    {
        var equipmentId = item.TargetObjectIds.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(equipmentId)
            || !IsFriendlyEquipmentMovementCandidate(state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations }, item.ControllerId, equipmentId)
            || !IsEquipmentMovementDestination(state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations }, equipmentId, destination))
            return [];

        var origin = FindFieldObjectLocation(zones, equipmentId);
        if (origin is null) return [];
        var originBattlefield = locations.GetValueOrDefault(equipmentId)?.BattlefieldObjectId;
        var field = destination[12..];
        RemoveFieldObjectFromLocation(zones, origin.Value.PlayerId, origin.Value.Zone, equipmentId);
        AddFieldObjectToLocation(zones, origin.Value.PlayerId, "BATTLEFIELD", equipmentId);
        locations[equipmentId] = new(origin.Value.PlayerId, "BATTLEFIELD", field);
        events.Add(new("EQUIPMENT_MOVED_TO_BATTLEFIELD", "效果移动装备",
            new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId,
                ["targetObjectId"] = equipmentId,
                ["equipmentObjectId"] = equipmentId,
                ["playerId"] = origin.Value.PlayerId,
                ["movingPlayerId"] = item.ControllerId,
                ["originZone"] = origin.Value.Zone,
                ["originBattlefieldObjectId"] = originBattlefield,
                ["destinationZone"] = "BATTLEFIELD",
                ["destination"] = destination,
                ["battlefieldObjectId"] = field
            }));
        return [equipmentId];
    }

    private static bool IsChosenMovementDestinationAllowed(MatchState state, string playerId, string destination, IReadOnlyList<string> targets)
    {
        // CN 355.4: declare the destination, excluding each unit's current location.
        // Choosing no units requires no movement destination.
        if (targets.Count == 0 && string.IsNullOrEmpty(destination)) return true;
        return ChosenMovementDestinations(state, playerId).Any(choice => choice.Id == destination)
            && targets.All(id => IsUnitMovementDestination(state, id, destination));
    }

    private static bool IsUnitMovementDestination(MatchState state, string id, string destination)
    {
        var location = FindFieldObjectLocation(state.PlayerZones, id);
        if (location is null) return false;
        if (destination.StartsWith("BASE:", StringComparison.Ordinal))
            return destination == "BASE:" + location.Value.PlayerId && location.Value.Zone != "BASE";
        if (!destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)) return false;
        var field = destination[12..];
        return BattlefieldLocalRules.Battlefield(state, field) is not null
            && (location.Value.Zone != "BATTLEFIELD" || state.ObjectLocations.GetValueOrDefault(id)?.BattlefieldObjectId != field);
    }

    private static string[] MoveFieldUnitsToChosenLocation(
        MatchState state,
        Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards,
        Dictionary<string, ObjectLocationState> locations,
        Dictionary<string, RunePool> pools,
        StackItemState item,
        IReadOnlyList<string> ids,
        string destination,
        List<GameEvent> events)
    {
        var toBase = destination.StartsWith("BASE:", StringComparison.Ordinal);
        var destinationZone = toBase ? "BASE" : "BATTLEFIELD";
        var field = toBase ? null : destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal) ? destination[12..] : null;
        var bindings = ids.Distinct(StringComparer.Ordinal).Where(cards.ContainsKey)
            .ToDictionary(id => id, id => (cards[id].ObjectGeneration, cards[id].ControllerId), StringComparer.Ordinal);
        string[] CurrentMovers(MatchState current) => bindings.Keys.Where(id =>
            current.CardObjects.TryGetValue(id, out var unit) && FieldObjectTypeRules.IsVisibleUnit(unit)
            && (unit.ObjectGeneration, unit.ControllerId) == bindings[id]
            && (item.TargetGenerations?.ContainsKey(id) != true || MovementTargetIsCurrent(current, item, id))
            && IsUnitMovementDestination(current, id, destination)
            && MovementRestrictionRules.CanMove(unit, item.ControllerId)
            && !(toBase && BattlefieldLocalRules.PreventsMoveToBase(current, id))).ToArray();
        var payment = PayImposedMovementCost(state, item.ControllerId, item.SourceObjectId, field, false,
            zones, cards, locations, pools, CurrentMovers, events);
        if (payment != ImposedMovementPaymentResult.Paid) return [];
        var before = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, RunePools = pools };
        var movable = CurrentMovers(before);
        var captured = CaptureMovementBatchObservers(before, cards, movable
            .Select(id => CaptureMovedUnitFact(before, id, item.ControllerId, destinationZone, field))
            .OfType<MovedUnitFact>().ToArray());
        // Facts and observers are immutable before any of the shared dictionaries mutate.
        foreach (var id in movable)
        {
            var origin = FindFieldObjectLocation(zones, id)!.Value;
            var originBattlefield = locations.GetValueOrDefault(id)?.BattlefieldObjectId;
            var equipment = AttachedEquipmentObjectIds(cards, id);
            RemoveFieldObjectFromLocation(zones, origin.PlayerId, origin.Zone, id);
            AddFieldObjectToLocation(zones, origin.PlayerId, destinationZone, id);
            locations[id] = new(origin.PlayerId, destinationZone, field);
            events.Add(new(toBase ? "UNIT_MOVED_TO_BASE" : "UNIT_MOVED_TO_BATTLEFIELD", "效果移动单位",
                new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id, ["unitObjectId"] = id,
                    ["playerId"] = origin.PlayerId, ["ownerPlayerId"] = origin.PlayerId, ["movingPlayerId"] = item.ControllerId,
                    ["originZone"] = origin.Zone, ["originBattlefieldObjectId"] = originBattlefield,
                    ["destinationZone"] = destinationZone, ["destination"] = destination, ["battlefieldObjectId"] = field,
                    ["simultaneousSourceObjectIds"] = movable
                }));
            events.AddRange(MoveAttachedEquipmentWithHost(zones, equipment, origin.PlayerId, id, destinationZone));
            foreach (var gear in equipment) locations[gear] = new(origin.PlayerId, destinationZone, field);
        }
        events.Add(captured);
        return movable;
    }
}
