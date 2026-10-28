namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedCeasedBeginningRuneSource(MatchState? state, string? id,
        string? controller, string? source, string? kind)
    {
        if (state is null || string.IsNullOrWhiteSpace(source) || source == "HIDDEN" || state.CardObjects.ContainsKey(source)) return false;
        var queue = state.TriggerQueue.FirstOrDefault(item => item.TriggerId == id && item.ControllerId == controller
            && item.SourceObjectId == source && item.EffectKind == kind);
        if (queue is not null && CoreRuleEngine.ValidCeasedBeginningRuneSource(state, queue.BeginningRune,
                queue.EffectKind, queue.ControllerId, queue.SourceObjectId)) return true;
        var stack = state.StackItems.FirstOrDefault(item => item.StackItemId == id && item.ControllerId == controller
            && item.SourceObjectId == source && item.EffectKind == kind);
        return stack is not null && CoreRuleEngine.ValidCeasedBeginningRuneSource(state, stack.BeginningRune,
            stack.EffectKind, stack.ControllerId, stack.SourceObjectId, stack.CardNo);
    }
}
