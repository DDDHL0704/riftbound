namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedCeasedBombSource(MatchState? state, string? id,
        string? controller, string? source, string? kind)
    {
        if (state is null || string.IsNullOrWhiteSpace(source) || source == "HIDDEN"
            || state.CardObjects.ContainsKey(source)) return false;
        var trigger = state.TriggerQueue.FirstOrDefault(item => item.TriggerId == id
            && item.ControllerId == controller && item.SourceObjectId == source && item.EffectKind == kind);
        return trigger?.FieldContext is { Kind: CoreRuleEngine.BombDestructionKind } context
            && trigger.SourceCardNo == context.CardNo
            && trigger.TriggeredByEventKind is "EQUIPMENT_DESTROYED" or "OBJECT_DESTROYED" or "UNIT_DESTROYED"
            && CoreRuleEngine.ValidFieldContextState(state, context, trigger.SourceObjectId,
                trigger.ControllerId, trigger.EffectKind, trigger.SourceCardNo);
    }
}
