namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateLastBreathInsightOccurrences(MatchState state, List<string> errors)
    {
        foreach (var item in state.TriggerQueue)
            if (!CoreRuleEngine.ValidLastBreathInsightTriggerIdentity(item))
                errors.Add("invalid captured last-breath Insight identity");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidLastBreathInsightStackIdentity(item))
                errors.Add("invalid captured last-breath Insight stack identity");
        var contexts = state.TriggerQueue.Select(i => (Source: i.SourceObjectId, Context: i.InsightContext))
            .Concat(state.StackItems.Select(i => (Source: i.SourceObjectId, Context: i.InsightContext)))
            .Where(i => i.Context?.Kind == "LAST_BREATH")
            .GroupBy(i => (i.Source, i.Context!.SourceGeneration))
            .Where(group => group.Any(i => i.Context!.Repetition is not null))
            .SelectMany(group => group).ToArray();
        if (contexts.Select(i => (i.Source, i.Context!.SourceGeneration,
            CoreRuleEngine.LastBreathInsightInstanceId(i.Context, i.Source), i.Context.Repetition?.Occurrence ?? 0))
            .Distinct().Count() != contexts.Length)
            errors.Add("duplicate captured last-breath Insight instance");
    }
}
