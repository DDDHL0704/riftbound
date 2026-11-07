namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedPublicRecycledBoonEffect(MatchState? state, string? triggerId, string? effect)
        => state is not null && effect == CoreRuleEngine.FieldTriggerEffect
            && state.TriggerQueue.Any(trigger => trigger.TriggerId == triggerId && trigger.EffectKind == effect
                && trigger.TriggeredByEventKind == "CARDS_RECYCLED"
                && trigger.FieldContext is { Kind: "RECYCLE_BOON" } context
                && CoreRuleEngine.ValidFieldContextState(state, context, trigger.SourceObjectId,
                    trigger.ControllerId, trigger.EffectKind));
}
