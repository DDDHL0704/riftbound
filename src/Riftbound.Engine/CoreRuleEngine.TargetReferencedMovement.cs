using Riftbound.Contracts;

namespace Riftbound.Engine;

// The selected moving unit and reference are confirmed before responses. The
// destination is the reference's legal current battlefield when executing.
public sealed record TargetReferencedMovementSpec(int MovingTargetIndex, int ReferenceTargetIndex,
    bool ReferenceIsBattlefield = false);

public sealed partial class CoreRuleEngine
{
    internal static bool IsTargetReferencedMovementRelationAllowed(MatchState state, string player,
        CardBehaviorDefinition behavior, IReadOnlyList<string> targets, int index)
    {
        if (behavior.TargetReferencedMovement is not { } spec || index != spec.MovingTargetIndex) return true;
        if (targets.Count <= Math.Max(spec.MovingTargetIndex, spec.ReferenceTargetIndex)
            || ReferencedMovementBattlefield(state, targets[spec.ReferenceTargetIndex], spec) is not { } field) return false;
        var moving = targets[spec.MovingTargetIndex];
        // Destination legality and ability to execute movement are separate.
        // Shared execution applies prohibitions after confirmation (CN359.3.e.6).
        return IsUnitMovementDestination(state, moving, "BATTLEFIELD:" + field.ObjectId);
    }

    private static void ResolveTargetReferencedMovement(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, ObjectLocationState> locations,
        Dictionary<string, RunePool> pools, StackItemState item, CardBehaviorDefinition behavior, List<GameEvent> events)
    {
        var spec = behavior.TargetReferencedMovement!;
        var current = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, RunePools = pools };
        if (!IsTargetReferencedMovementRelationAllowed(current, item.ControllerId, behavior, item.TargetObjectIds, spec.MovingTargetIndex)
            || !IsLegalChosenCardTarget(current, item.ControllerId, item.TargetObjectIds[spec.ReferenceTargetIndex],
                spec.ReferenceTargetIndex, item.TargetObjectIds, PlayCardTargetScopeForBehavior(behavior), behavior)
            || !IsLegalChosenCardTarget(current, item.ControllerId, item.TargetObjectIds[spec.MovingTargetIndex],
                spec.MovingTargetIndex, item.TargetObjectIds, PlayCardTargetScopeForBehavior(behavior), behavior)) return;
        var field = ReferencedMovementBattlefield(current, item.TargetObjectIds[spec.ReferenceTargetIndex], spec)!;
        MoveFieldUnitsToChosenLocation(current, zones, cards, locations, pools, item,
            [item.TargetObjectIds[spec.MovingTargetIndex]], "BATTLEFIELD:" + field.ObjectId, events);
    }

    private static CardObjectState? ReferencedMovementBattlefield(MatchState state, string reference,
        TargetReferencedMovementSpec spec) => spec.ReferenceIsBattlefield
            ? BattlefieldLocalRules.Battlefield(state, reference) : BattlefieldLocalRules.AtUnit(state, reference);
}
