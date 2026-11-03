using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveBorrowedMovement(MatchState state, StackItemState item, BorrowedExhaustSpec spec)
    {
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "获得的移动技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["stackItemId"] = item.StackItemId }) };
        var id = item.TargetObjectIds[0];
        if (!StackObjectRules.ReferencesCurrentObject(state, item, id)
            || !IsBorrowedExhaustTarget(state, item.ControllerId, id, spec, item.SourceObjectId))
            return NoopStackResolutionResult(state) with { Events = events };
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        var pools = state.RunePools.ToDictionary();
        var destination = new ObjectLocationState(item.ControllerId, "BASE");
        if (spec.Kind == "MOVE_BETWEEN_BASE_AND_BATTLEFIELD"
            && TryGetPreciseFieldLocation(zones, locations, id, out var origin) && origin.Zone == "BASE")
        {
            var choices = ChosenMovementDestinations(state, item.ControllerId)
                .Where(choice => choice.Id.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)).ToArray();
            if (choices.Length == 0) return NoopStackResolutionResult(state) with { Events = events };
            var selected = RuleChoices.Value!.Choose(item.ControllerId, "选择此单位移动到的战场。",
                choices.Select(choice => new RuleChoiceOption(choice.Id, choice.Label)).ToArray(),
                kind: "MOVEMENT_DESTINATION", preview: state);
            destination = new(item.ControllerId, "BATTLEFIELD", selected[12..]);
        }
        TryMoveFieldUnitByEffect(state, zones, cards, locations, pools, id, destination,
            item.ControllerId, item, events, frame => StackObjectRules.ReferencesCurrentObject(frame, item, id)
                && IsBorrowedExhaustTarget(frame, item.ControllerId, id, spec, item.SourceObjectId));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations, RunePools = pools, Events = events };
    }
}
