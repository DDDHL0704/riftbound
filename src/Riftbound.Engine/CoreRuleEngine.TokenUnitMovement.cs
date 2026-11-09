using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string FriendlyUnitTokensMoveHere = "MOVE_FRIENDLY_UNIT_TOKENS_HERE";

    private static StackResolutionResult ResolveFriendlyUnitTokenMovement(MatchState state, StackItemState item)
    {
        // CN359.3.f.2 / 359.3.e.12: read the original incarnation's current
        // location when executing the instruction. An off-field source has no
        // location; a different incarnation cannot supply a new battlefield.
        var destination = UnitEventHereDestination(state, item);
        if (destination is null) return NoopStackResolutionResult(state);
        var legal = UnitEventTargets(state, item).ToHashSet(StringComparer.Ordinal);
        var selected = item.TargetObjectIds.Where(id => legal.Contains(id)
            && StackObjectRules.ReferencesCurrentObject(state, item, id)).ToArray();
        if (selected.Length == 0) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        var pools = state.RunePools.ToDictionary();
        var events = new List<GameEvent>();
        // One batch preserves simultaneous observations and multi-unit taxes.
        MoveFieldUnitsToChosenLocation(state, zones, cards, locations, pools,
            item, selected, destination, events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones,
            CardObjects = cards, ObjectLocations = ReconcileObjectLocations(locations, zones),
            RunePools = pools, Events = events };
    }
}
