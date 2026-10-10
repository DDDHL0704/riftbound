using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<ActionPromptChoiceDto> ChosenMovementDestinations(MatchState state, string playerId)
        => state.Seats.Keys.Where(id => id != playerId).Order(StringComparer.Ordinal)
            .Select(id => new ActionPromptChoiceDto("BASE:" + id, $"{state.Seats[id]} 的基地"))
            .Concat(state.CardObjects.Keys.Order(StringComparer.Ordinal)
                .Where(id => BattlefieldLocalRules.Battlefield(state, id) is not null)
                .Select(id => new ActionPromptChoiceDto("BATTLEFIELD:" + id,
                    CardBehaviorRegistry.TryGetByCardNo(state.CardObjects[id].CardNo ?? "", out var behavior)
                        ? $"{behavior.DisplayName}（{id}）" : id))).ToArray();

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

    private static void MoveFieldUnitsToChosenLocation(
        MatchState state,
        Dictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards,
        Dictionary<string, ObjectLocationState> locations,
        StackItemState item,
        IReadOnlyList<string> ids,
        string destination,
        List<GameEvent> events)
    {
        var toBase = destination.StartsWith("BASE:", StringComparison.Ordinal);
        var destinationZone = toBase ? "BASE" : "BATTLEFIELD";
        var field = toBase ? null : destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal) ? destination[12..] : null;
        // All selected units relocate before downstream cleanup/contest processing.
        foreach (var id in ids)
        {
            if (!IsUnitMovementDestination(state, id, destination)
                || !MovementRestrictionRules.CanMove(cards.GetValueOrDefault(id), item.ControllerId)
                || (toBase && BattlefieldLocalRules.PreventsMoveToBase(state, id))) continue;
            var origin = FindFieldObjectLocation(zones, id)!.Value;
            var equipment = AttachedEquipmentObjectIds(cards, id);
            RemoveFieldObjectFromLocation(zones, origin.PlayerId, origin.Zone, id);
            AddFieldObjectToLocation(zones, origin.PlayerId, destinationZone, id);
            locations[id] = new(origin.PlayerId, destinationZone, field);
            events.Add(new(toBase ? "UNIT_MOVED_TO_BASE" : "UNIT_MOVED_TO_BATTLEFIELD", "效果移动单位",
                new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id, ["unitObjectId"] = id,
                    ["playerId"] = origin.PlayerId, ["ownerPlayerId"] = origin.PlayerId, ["movingPlayerId"] = item.ControllerId,
                    ["originZone"] = origin.Zone, ["originBattlefieldObjectId"] = state.ObjectLocations.GetValueOrDefault(id)?.BattlefieldObjectId,
                    ["destinationZone"] = destinationZone, ["destination"] = destination, ["battlefieldObjectId"] = field,
                    ["simultaneousSourceObjectIds"] = ids
                }));
            events.AddRange(MoveAttachedEquipmentWithHost(zones, equipment, origin.PlayerId, id, destinationZone));
            foreach (var gear in equipment) locations[gear] = new(origin.PlayerId, destinationZone, field);
        }
    }
}
