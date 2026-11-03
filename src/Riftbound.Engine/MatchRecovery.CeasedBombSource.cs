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
            && CoreRuleEngine.ValidBombDestructionTrigger(trigger)
            && trigger.TriggeredByEventKind is "EQUIPMENT_DESTROYED" or "OBJECT_DESTROYED" or "UNIT_DESTROYED"
            && CoreRuleEngine.ValidFieldContextState(state, context, trigger.SourceObjectId,
                trigger.ControllerId, trigger.EffectKind, trigger.SourceCardNo);
    }

    private static void ValidateBombDestructionState(MatchState state, List<string> errors)
    {
        var origins = state.TriggerQueue.Select(i => i.FieldContext?.BombDestruction)
            .Concat(state.StackItems.Select(i => i.FieldContext?.BombDestruction))
            .OfType<BombDestructionOrigin>().ToArray();
        // A token incarnation can leave once. Changing its capture tick cannot
        // create another legal occurrence of the same last-breath skill.
        if (origins.Select(c => (c.Bomb?.ObjectId, c.Bomb?.Generation, c.Repetition?.Occurrence ?? 0))
            .Distinct().Count() != origins.Length)
            errors.Add("duplicate captured bomb destruction instance");
    }
}
