using Riftbound.Contracts;

namespace Riftbound.Engine;

// The caster moves a declared unit first; the instructed opponent then chooses
// and moves their own unit to the same destination during resolution.
public sealed record CommandedMovementSpec(int FirstTargetIndex = 0, bool DestinationControlledByCaster = true);

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<ActionPromptChoiceDto> CommandedMovementDestinations(
        MatchState state, string player, CommandedMovementSpec spec)
        => state.BattlefieldStates.Values.Where(b => !spec.DestinationControlledByCaster || b.ControllerId == player)
            .Where(b => BattlefieldLocalRules.Battlefield(state, b.BattlefieldObjectId) is not null)
            .OrderBy(b => b.BattlefieldObjectId, StringComparer.Ordinal)
            .Select(b => new ActionPromptChoiceDto("BATTLEFIELD:" + b.BattlefieldObjectId,
                OfficialCardSourceIdentityGroups.DisplayName(b.CardNo ?? "") + "（" + b.BattlefieldObjectId + "）")).ToArray();

    private static bool IsCommandedMovementDestinationAllowed(MatchState state, string player,
        string destination, IReadOnlyList<string> targets, CommandedMovementSpec spec)
        => targets.Count > spec.FirstTargetIndex
            && CommandedMovementDestinations(state, player, spec).Any(d => d.Id == destination)
            && IsUnitMovementDestination(state, targets[spec.FirstTargetIndex], destination);

    private static void ResolveCommandedMovement(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, ObjectLocationState> locations,
        StackItemState item, CardBehaviorDefinition behavior, List<GameEvent> events)
    {
        var spec = behavior.CommandedMovement!;
        var destination = item.Destination;
        if (!destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
            || BattlefieldLocalRules.Battlefield(state, destination[12..]) is null) return;
        var current = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
        var target = item.TargetObjectIds.ElementAtOrDefault(spec.FirstTargetIndex);
        if (target is { Length: > 0 }
            && (!spec.DestinationControlledByCaster || current.BattlefieldStates.GetValueOrDefault(destination[12..])?.ControllerId == item.ControllerId))
            MoveFieldUnitsToChosenLocation(current, zones, cards, locations, item, [target], destination, events);

        var frame = RuleChoices.Value ?? throw new InvalidOperationException("Commanded movement needs a rule choice frame.");
        current = state with { PlayerZones = new Dictionary<string,PlayerZones>(zones),
            CardObjects = new Dictionary<string,CardObjectState>(cards), ObjectLocations = ReconcileObjectLocations(locations,zones) };
        var opponents = state.Seats.Keys.Where(id => id != item.ControllerId).Order(StringComparer.Ordinal).ToArray();
        if (opponents.Length == 0) return;
        var opponent = opponents.Length == 1 ? opponents[0] : frame.Choose(item.ControllerId,
            behavior.DisplayName + "：选择一名对手执行移动。",
            opponents.Select(id => new RuleChoiceOption(id, state.Seats[id])).ToArray(), kind: "MOVEMENT_SELECTION", preview: current);
        var legal = cards.Values.Where(c => FieldObjectTypeRules.IsVisibleUnit(c)
            && FindFieldObjectLocation(zones,c.ObjectId) is { } position && position.PlayerId == opponent
            && IsUnitMovementDestination(current,c.ObjectId,destination) && MovementRestrictionRules.CanMove(c,opponent))
            .OrderBy(c => c.ObjectId,StringComparer.Ordinal).ToArray();
        if (legal.Length == 0) return;
        var selected = frame.Choose(opponent, behavior.DisplayName + "：选择你控制的一名单位，移动至"
            + OfficialCardSourceIdentityGroups.DisplayName(current.CardObjects[destination[12..]].CardNo ?? "")
            + "（" + destination[12..] + "）。",
            legal.Select(c => new RuleChoiceOption("MOVE:" + c.ObjectId,
                OfficialCardSourceIdentityGroups.DisplayName(c.CardNo ?? "") + "（" + c.ObjectId + "）", [c.ObjectId])).ToArray(),
            kind: "MOVEMENT_SELECTION", preview: current);
        MoveFieldUnitsToChosenLocation(current, zones, cards, locations,
            item with { ControllerId = opponent }, [selected[5..]], destination, events);
    }
}
