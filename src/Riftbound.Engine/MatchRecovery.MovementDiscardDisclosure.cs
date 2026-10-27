namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedPublicMovementDiscardEffect(MatchState? state, string? triggerId, string? effectKind)
        => state is not null && effectKind == CoreRuleEngine.MovementDiscardInlineEffect
            && state.TriggerQueue.Any(item => item.TriggerId == triggerId && item.EffectKind == effectKind
                && item.TriggeredByEventKind == "CARD_DISCARDED"
                && item.MovementObserver is { DiscardInline: not null } context
                && CoreRuleEngine.ValidMovementObserver(state, context, item.EffectKind,
                    item.ControllerId, item.SourceObjectId, item.SourceCardNo)
                && CoreRuleEngine.ValidMovementInstanceId(context, item.TriggerId)
                && CoreRuleEngine.ValidMovementDiscardState(state));
}
