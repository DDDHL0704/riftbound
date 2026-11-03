using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 204.4: each of the two move instructions pays its own imposed costs.
    // Capture both destinations before the first move; declining one does not
    // cancel the other instruction or change its originally captured destination.
    private static void MoveFieldUnitsToEachOthersLocations(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, ObjectLocationState> locations, Dictionary<string, RunePool> pools,
        StackItemState item, CardBehaviorDefinition behavior, string first, string second, List<GameEvent> events)
    {
        var current = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, RunePools = pools };
        if (!CanSwapTargetLocations(current, first, second)
            || !TryGetPreciseFieldLocation(zones, locations, first, out var firstOrigin)
            || !TryGetPreciseFieldLocation(zones, locations, second, out var secondOrigin)
            || !cards.TryGetValue(first, out var firstUnit) || !IsFaceUpNonStandbyUnit(firstUnit)
            || !cards.TryGetValue(second, out var secondUnit) || !IsFaceUpNonStandbyUnit(secondUnit)
            || EffectiveFieldControllerId(zones, first, firstUnit) != item.ControllerId
            || EffectiveFieldControllerId(zones, second, secondUnit) != item.ControllerId
            || !string.IsNullOrEmpty(behavior.PairedMovementRequiredAnyTargetTag)
                && !new[] { first, second }.Any(id => CardObjectHasTag(cards, id, behavior.PairedMovementRequiredAnyTargetTag))) return;
        var generation = secondUnit.ObjectGeneration;
        bool SecondStillAtOrigin(MatchState frame) => frame.CardObjects.TryGetValue(second, out var unit)
            && unit.ObjectGeneration == generation && IsFaceUpNonStandbyUnit(unit)
            && EffectiveFieldControllerId(frame.PlayerZones, second, unit) == item.ControllerId
            && TryGetPreciseFieldLocation(frame.PlayerZones, frame.ObjectLocations, second, out var at) && at == secondOrigin;
        var firstMoved = TryMoveFieldUnitByEffect(current, zones, cards, locations, pools,
            first, secondOrigin, item.ControllerId, item, events, SecondStillAtOrigin);
        var secondMoved = SecondStillAtOrigin(current) && TryMoveFieldUnitByEffect(current, zones, cards, locations, pools,
            second, firstOrigin, item.ControllerId, item, events);
        if (firstMoved && secondMoved)
            events.Add(new("UNIT_LOCATIONS_SWAPPED", "两名单位分别移动到对方原来的位置", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["firstTargetObjectId"] = first, ["secondTargetObjectId"] = second,
                ["firstDestinationPlayerId"] = secondOrigin.PlayerId, ["firstDestinationZone"] = secondOrigin.Zone,
                ["firstDestinationBattlefieldObjectId"] = secondOrigin.BattlefieldObjectId,
                ["secondDestinationPlayerId"] = firstOrigin.PlayerId, ["secondDestinationZone"] = firstOrigin.Zone,
                ["secondDestinationBattlefieldObjectId"] = firstOrigin.BattlefieldObjectId }));
    }
}
