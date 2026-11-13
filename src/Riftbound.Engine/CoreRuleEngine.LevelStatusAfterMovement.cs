using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN824: confirmation sees the controller's current dependent text.
    // Its declared targets remain fixed when later responses change XP.
    internal static int ActiveLevelAdditionalTargetCount(MatchState state, string player, CardBehaviorDefinition behavior)
        => behavior.AdditionalTargetsAtLevel > 0
            && ControllerMeetsLevelExperienceThreshold(behavior, player, state.PlayerExperience)
                ? behavior.AdditionalTargetsAtLevel : 0;

    private static void ApplyLevelStatusAfterMovement(MatchState state, StackItemState item,
        CardBehaviorDefinition behavior, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        if (!behavior.MovesTargetsToChosenLocation || behavior.StatusEffectId != "STUNNED"
            || behavior.StatusEffectTargetIndex < 0 || behavior.LevelExperienceThreshold <= 0)
            throw new InvalidOperationException("Invalid ordered dependent status instruction.");
        if (!ControllerMeetsLevelExperienceThreshold(behavior, item.ControllerId, state.PlayerExperience)) return;
        var index = behavior.StatusEffectTargetIndex;
        if (item.TargetObjectIds.ElementAtOrDefault(index) is not { Length: > 0 } target
            || !StackObjectRules.ReferencesCurrentObject(state, item, target)
            || !IsLegalChosenCardTarget(state, item.ControllerId, target, index, item.TargetObjectIds,
                PlayCardTargetScopeForBehavior(behavior), behavior)) return;
        // Reuse the ordinary stun replacement and observer instruction. The
        // preceding move need not succeed for this separate instruction.
        ApplyUnitEventStun(state, item, zones, cards, events, target);
    }
}
