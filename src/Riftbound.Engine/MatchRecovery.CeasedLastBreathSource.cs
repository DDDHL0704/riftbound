namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedCeasedLastBreathSource(MatchState? state, string? id,
        string? controller, string? source, string? kind)
    {
        if (state is null || string.IsNullOrWhiteSpace(source) || source == "HIDDEN" || state.CardObjects.ContainsKey(source)) return false;
        var queue = state.TriggerQueue.FirstOrDefault(i => i.TriggerId == id && i.ControllerId == controller
            && i.SourceObjectId == source && i.EffectKind == kind);
        if (queue is not null && (
            queue.LastBreathDraw is { SourceTokenFactoryCardNo: not null } && CoreRuleEngine.ValidLastBreathDrawTrigger(state, queue)
            || queue.LastBreathRuneCall is { SourceTokenFactoryCardNo: not null } && CoreRuleEngine.ValidLastBreathRuneCallTrigger(state, queue)
            || queue.LastBreathUnitCreation is { SourceTokenFactoryCardNo: not null } && CoreRuleEngine.ValidLastBreathUnitCreationTrigger(state, queue)
            || queue.LastBreathBattlefieldDamage is { SourceTokenFactoryCardNo: not null } && CoreRuleEngine.ValidLastBreathBattlefieldDamageTrigger(state, queue))) return true;
        var stack = state.StackItems.FirstOrDefault(i => i.StackItemId == id && i.ControllerId == controller
            && i.SourceObjectId == source && i.EffectKind == kind);
        return stack is not null && (
            stack.LastBreathDraw is { SourceTokenFactoryCardNo: not null } && CoreRuleEngine.ValidLastBreathDrawStack(state, stack)
            || stack.LastBreathRuneCall is { SourceTokenFactoryCardNo: not null } && CoreRuleEngine.ValidLastBreathRuneCallStack(state, stack)
            || stack.LastBreathUnitCreation is { SourceTokenFactoryCardNo: not null } && CoreRuleEngine.ValidLastBreathUnitCreationStack(state, stack)
            || stack.LastBreathBattlefieldDamage is { SourceTokenFactoryCardNo: not null } && CoreRuleEngine.ValidLastBreathBattlefieldDamageStack(state, stack));
    }
}
