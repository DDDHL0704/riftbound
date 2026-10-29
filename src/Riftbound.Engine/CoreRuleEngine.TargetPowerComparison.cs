namespace Riftbound.Engine;

public sealed record TargetPowerComparisonSpec(int TargetIndex, int ReferenceIndex);

public sealed partial class CoreRuleEngine
{
    internal static bool IsTargetPowerComparisonAllowed(MatchState state, string player,
        CardBehaviorDefinition behavior, IReadOnlyList<string> targets, int index)
    {
        if (behavior.TargetPowerComparison is not { } spec || index != spec.TargetIndex) return true;
        if (spec.TargetIndex == spec.ReferenceIndex
            || targets.Count <= Math.Max(spec.TargetIndex, spec.ReferenceIndex)
            || !IsFieldUnitObject(state, targets[spec.ReferenceIndex])
            || !IsLegalChosenCardTarget(state, player, targets[spec.ReferenceIndex], spec.ReferenceIndex,
                targets, PlayCardTargetScopeForBehavior(behavior), behavior)
            || !IsFieldUnitObject(state, targets[spec.TargetIndex])) return false;
        return ResolveCurrentFieldUnitPower(state, state.CardObjects[targets[spec.TargetIndex]])
            < ResolveCurrentFieldUnitPower(state, state.CardObjects[targets[spec.ReferenceIndex]]);
    }

    private static bool ShouldDestroyChosenTarget(CardBehaviorDefinition behavior, int index)
        => behavior.DestroysTarget && (behavior.DestroyTargetIndex is null || behavior.DestroyTargetIndex == index);
}
