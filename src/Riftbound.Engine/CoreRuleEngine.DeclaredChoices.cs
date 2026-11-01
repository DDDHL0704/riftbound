using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Shared by initial declaration and effects that explicitly remake choices.
    // It does not grant play permission or move a finalized spell into a hand.
    private static bool HasLegalDeclaredTargets(MatchState state, string player, string source,
        CardBehaviorDefinition behavior, IReadOnlyList<string> targets, string scope)
        => HasIndependentPermanentTargets(behavior) && targets.Count == 0
            || HasValidTargetCount(state, player, behavior, targets)
                && PlayCardTargetsExposeKnownCardNumbers(state, targets)
                && CreatedBaseUnitCopyTargetAllowed(state, behavior, targets)
                && HasValidTargetGroup(state, behavior, targets)
                && HasValidDistributedSpellTargets(state, player, behavior, targets)
                && HasRequiredAnyTargetTag(state, behavior, targets)
                && HasValidSwapTargetLocations(state, behavior, targets)
                && AreAttachDetachTargetsAllowed(state, behavior, targets)
                && HasValidSacredJudgmentKeepTargets(state, source, behavior, targets)
                && IsMainDeckLookWindowControlledByPlayer(state, player, behavior)
                && targets.Select((id, index) => IsLegalChosenCardTarget(state, player, id, index, targets, scope, behavior)).All(valid => valid);

    private static bool HasLegalRemadeExecution(MatchState state, StackItemState spell, SpellExecutionState execution)
    {
        if (!CardBehaviorRegistry.TryGetByEffectKind(execution.EffectKind, out var behavior) || behavior.CardNo != spell.CardNo
            || execution.TargetObjectIds.Any(id => !CurrentChosenIncarnation(state, execution, id))) return false;
        behavior = ShowcaseDefinition(PermanentPlayDefinition(behavior, IsStackItemBattlefieldDestination(spell)), spell)
            with { StandbyPlayFrom = spell.PlayedFromStandby };
        var targets = execution.TargetObjectIds;
        if (!HasValidTargetEffectAdditionalCostTargets(behavior, targets, spell.OptionalCosts)) return false;
        if (IsStandbyReactionStackItem(behavior, spell))
        {
            var field = spell.PlayedFromStandby!.Battlefield.ObjectId;
            if (!HasValidStandbyReactionTargets(state, spell.ControllerId, behavior, spell.SourceObjectId, field, targets)
                || !targets.Select((id, index) => IsLegalChosenCardTarget(state, spell.ControllerId, id, index, targets,
                    behavior.StandbyReactionTargetScope, behavior)).All(valid => valid)) return false;
            // A face-down play retains its original battlefield restriction after control changes.
            return targets.All(id => RemadeStandbyLocationAllowed(state, behavior, field, id))
                && OpenBattlefieldModeAllowed(state, spell.ControllerId, behavior, execution.Destination)
                && (!behavior.MovesTargetsToChosenLocation || IsChosenMovementDestinationAllowed(state,
                    spell.ControllerId, execution.Destination, ChosenMovementTargets(behavior, targets)));
        }
        if (!HasLegalDeclaredTargets(state, spell.ControllerId, spell.SourceObjectId, behavior, targets, PlayCardTargetScopeForBehavior(behavior))
            || targets.Any(id => !PermanentPlayHereTargetAllowed(state, spell, behavior, id))) return false;
        var command = new PlayCardCommand(spell.SourceObjectId, spell.CardNo, targets, behavior.Mode,
            spell.OptionalCosts, execution.Destination, MovementDestinations: execution.MovementDestinations);
        return IsSequentialMovementPlanAllowed(state, spell.ControllerId, behavior, command)
            && OpenBattlefieldModeAllowed(state, spell.ControllerId, behavior, execution.Destination)
            && (behavior.CommandedMovement is not { } movement || IsCommandedMovementDestinationAllowed(state, spell.ControllerId,
                execution.Destination, targets, movement))
            && (!behavior.MovesTargetsToChosenLocation || IsChosenMovementDestinationAllowed(state, spell.ControllerId, execution.Destination, ChosenMovementTargets(behavior, targets)))
            && (!behavior.MovesFriendlyEquipmentToChosenBattlefield
                || IsEquipmentMovementDestinationAllowed(state, spell.ControllerId, execution.Destination, targets));
    }

    private static bool CurrentChosenIncarnation(MatchState state, SpellExecutionState execution, string id)
        => !execution.TargetGenerations.TryGetValue(id, out var generation)
            || state.CardObjects.TryGetValue(id, out var card) && card.ObjectGeneration == generation;

    private static bool RemadeStandbyLocationAllowed(MatchState state, CardBehaviorDefinition behavior, string field, string id)
        => string.IsNullOrEmpty(field) || behavior.StandbyReactionTargetScope is CardTargetScopes.Battlefield
            || state.ObjectLocations.TryGetValue(id, out var location) && location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId == field;
}
