using Riftbound.Contracts;

namespace Riftbound.Engine;

// The selected moving unit and reference are confirmed before responses. The
// destination is the reference's legal current battlefield when executing.
public sealed record TargetReferencedMovementSpec(int MovingTargetIndex, int ReferenceTargetIndex,
    bool ReferenceIsBattlefield = false, bool ReferenceMustBeControlled = false, int MovingUnitPowerBonus = 0);

public sealed partial class CoreRuleEngine
{
    internal static bool IsTargetReferencedMovementRelationAllowed(MatchState state, string player,
        CardBehaviorDefinition behavior, IReadOnlyList<string> targets, int index, bool allowUnavailableReference = false)
    {
        if (behavior.TargetReferencedMovement is not { } spec || index != spec.MovingTargetIndex) return true;
        if (targets.Count <= Math.Max(spec.MovingTargetIndex, spec.ReferenceTargetIndex)) return false;
        if (ReferencedMovementBattlefield(state, player, targets[spec.ReferenceTargetIndex], spec) is not { } field)
            return allowUnavailableReference;
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
        if (item.TargetObjectIds.Count <= Math.Max(spec.MovingTargetIndex, spec.ReferenceTargetIndex)
            || !IsLegalChosenCardTarget(current, item.ControllerId, item.TargetObjectIds[spec.MovingTargetIndex],
                spec.MovingTargetIndex, item.TargetObjectIds, PlayCardTargetScopeForBehavior(behavior), behavior,
                allowUnavailableReference: true)) return;
        var unit = item.TargetObjectIds[spec.MovingTargetIndex];
        if (IsLegalChosenCardTarget(current, item.ControllerId, item.TargetObjectIds[spec.ReferenceTargetIndex],
                spec.ReferenceTargetIndex, item.TargetObjectIds, PlayCardTargetScopeForBehavior(behavior), behavior)
            && ReferencedMovementBattlefield(current, item.ControllerId, item.TargetObjectIds[spec.ReferenceTargetIndex], spec) is { } field)
            MoveFieldUnitsToChosenLocation(current, zones, cards, locations, pools, item, [unit], "BATTLEFIELD:" + field.ObjectId, events);
        // CN 359.3.e.5-8: the Might instruction is independent of successful
        // movement and of the other target remaining legal.
        if (spec.MovingUnitPowerBonus == 0 || !cards.TryGetValue(unit, out var target)
            || !IsObjectOnField(zones, unit) || !MovementTargetIsCurrent(current, item, unit)) return;
        using var instruction = new MightInstructionScope(current, zones, cards, events, locations);
        cards[unit] = ApplyPowerModifier(current, target, behavior, item, unit, spec.MovingUnitPowerBonus, out var modified);
        events.Add(modified);
    }

    private static CardObjectState? ReferencedMovementBattlefield(MatchState state, string player, string reference,
        TargetReferencedMovementSpec spec)
    {
        var field = spec.ReferenceIsBattlefield ? BattlefieldLocalRules.Battlefield(state, reference) : BattlefieldLocalRules.AtUnit(state, reference);
        return field is not null && (!spec.ReferenceMustBeControlled || field.ControllerId == player) ? field : null;
    }
}
