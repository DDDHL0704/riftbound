using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedPublicLastBreathDrawEffect(MatchState? state, string? triggerId, string? effectKind)
        => state is not null && state.TriggerQueue.Any(item => item.TriggerId == triggerId
            && item.EffectKind == effectKind && item.LastBreathDraw is not null
            && CoreRuleEngine.ValidLastBreathDrawTrigger(state, item));

    private static void ValidateLastBreathDrawState(MatchState state, List<string> errors)
    {
        ValidateLastBreathMightOccurrences(state, errors);
        bool MissingDiscardBirth(string id, string kind)
            => kind == Riftbound.Contracts.TriggerKinds.UnitLastBreathDiscardDraw
                && (id.StartsWith("TRIGGER-death-", StringComparison.Ordinal)
                    || id.StartsWith("ordered-TRIGGER-death-", StringComparison.Ordinal));
        foreach (var item in state.StackItems)
            if (item.LastBreathDraw is not null ? !CoreRuleEngine.ValidLastBreathDrawStack(state, item)
                : MissingDiscardBirth(item.StackItemId, item.EffectKind))
                errors.Add("invalid captured last-breath draw stack");
        foreach (var item in state.TriggerQueue)
            if (item.LastBreathDraw is not null ? !CoreRuleEngine.ValidLastBreathDrawTrigger(state, item)
                : MissingDiscardBirth(item.TriggerId, item.EffectKind))
                errors.Add("invalid captured last-breath draw trigger");
        if (state.PendingHandChoice is { } choice && CoreRuleEngine.NeedsCapturedLastBreathDiscardChoice(choice)
            && !CoreRuleEngine.ValidLastBreathDiscardChoice(state))
            errors.Add("invalid captured last-breath discard-draw continuation");
        var contexts = state.StackItems.Select(i => i.LastBreathDraw)
            .Concat(state.TriggerQueue.Select(i => i.LastBreathDraw))
            .Append(state.PendingHandChoice?.LastBreathDraw).OfType<LastBreathDrawContext>().ToArray();
        if (contexts.Select(c => (c.DeathId, c.InstanceId, c.TriggerIndex, c.Repetition?.Occurrence ?? 0)).Distinct().Count() != contexts.Length)
            errors.Add("duplicate captured last-breath draw instance");
    }

    private static void ValidateLastBreathMightOccurrences(MatchState state, List<string> errors)
    {
        foreach (var item in state.TriggerQueue)
            if (!CoreRuleEngine.ValidLastBreathMightTriggerIdentity(item))
                errors.Add("invalid captured last-breath Might identity");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidLastBreathMightStackIdentity(item))
                errors.Add("invalid captured last-breath Might stack identity");
        var contexts = state.TriggerQueue.Select(i => i.LastBreathMight)
            .Concat(state.StackItems.Select(i => i.LastBreathMight)).OfType<LastBreathMightContext>().ToArray();
        if (contexts.Select(c => (c.SourceObjectId, c.SourceGeneration, c.InstanceId, c.Repetition?.Occurrence ?? 0))
            .Distinct().Count() != contexts.Length)
            errors.Add("duplicate captured last-breath Might instance");
    }

    private static void ValidateLastBreathDrawViews(IReadOnlyList<object?> payloads, List<string> errors)
    {
        foreach (var payload in payloads)
        {
            var json = JsonSerializer.SerializeToElement(payload);
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("lastBreathDraw", out _))
                errors.Add("public last-breath draw view exposes a private birth receipt");
        }
    }
}
