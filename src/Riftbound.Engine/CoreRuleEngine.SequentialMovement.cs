using Riftbound.Contracts;
namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static bool UsesDeclaredMovementDestination(CardBehaviorDefinition behavior)
        => behavior.MovesTargetsToChosenLocation || behavior.CommandedMovement is not null || behavior.MovesTargetsSequentially;

    internal static IReadOnlyList<ActionPromptChoiceDto> SequentialMovementDestinations(MatchState state, string player, int index)
    {
        var controllers = index == 0 ? new[]{player} : state.Seats.Keys.Where(id => id != player);
        return controllers.Select(id => new ActionPromptChoiceDto("BASE:" + id, state.Seats[id] + " 的基地"))
            .Concat(state.CardObjects.Values.Where(c => BattlefieldLocalRules.Battlefield(state,c.ObjectId) is not null)
                .OrderBy(c => c.ObjectId,StringComparer.Ordinal).Select(c => new ActionPromptChoiceDto("BATTLEFIELD:"+c.ObjectId,
                    OfficialCardSourceIdentityGroups.DisplayName(c.CardNo??"")+"（"+c.ObjectId+"）"))).ToArray();
    }

    private static bool IsSequentialMovementPlanAllowed(MatchState state, string player, CardBehaviorDefinition behavior, PlayCardCommand command)
    {
        if (!behavior.MovesTargetsSequentially) return command.MovementDestinations is null or {Count:0};
        return string.IsNullOrEmpty(command.Destination) && command.MovementDestinations is { } destinations
            && destinations.Count == behavior.RequiredTargetCount && destinations.Count == command.TargetObjectIds.Count
            && destinations.Select((destination,index) => !string.IsNullOrWhiteSpace(destination)
                && SequentialMovementDestinations(state,player,index).Any(d=>d.Id==destination)
                && IsUnitMovementDestination(state,command.TargetObjectIds[index],destination)).All(valid=>valid);
    }

    internal static bool ValidSequentialMovementPlan(MatchState state,string effect,int targets,IReadOnlyList<string>? destinations)
    {
        if (!CardBehaviorRegistry.TryGetByEffectKind(effect,out var behavior) || !behavior.MovesTargetsSequentially)
            return destinations is null or {Count:0};
        return destinations is not null && destinations.Count == targets && targets == behavior.RequiredTargetCount
            && destinations.All(d => !string.IsNullOrWhiteSpace(d) && (d.StartsWith("BASE:",StringComparison.Ordinal)
                ? state.Seats.ContainsKey(d[5..]) : d.StartsWith("BATTLEFIELD:",StringComparison.Ordinal) && d.Length>12));
    }
}
