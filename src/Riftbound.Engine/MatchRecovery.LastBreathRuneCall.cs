using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedPublicLastBreathRuneCallEffect(MatchState? state, string? triggerId, string? effectKind)
        => state is not null && state.TriggerQueue.Any(item => item.TriggerId == triggerId
            && item.EffectKind == effectKind && item.LastBreathRuneCall is not null
            && CoreRuleEngine.ValidLastBreathRuneCallTrigger(state, item));

    private static void ValidateLastBreathRuneCallState(MatchState state, List<string> errors)
    {
        bool NeedsCapturedBirth(string? cardNo, string kind)
            => kind == Riftbound.Contracts.TriggerKinds.UnitLastBreathCallRunesThenDraw
                || UnitDestroyedTriggerSpecRules.LastBreathRuneCallTriggers(cardNo).Any(t => t.Kind == kind && t.RequiresSourceEmpowered is not null);
        foreach (var item in state.StackItems)
            if (item.LastBreathRuneCall is not null ? !CoreRuleEngine.ValidLastBreathRuneCallStack(state, item)
                : NeedsCapturedBirth(item.CardNo, item.EffectKind)
                    || NeedsCapturedBirth(string.IsNullOrWhiteSpace(item.SourceObjectId) ? null : state.CardObjects.GetValueOrDefault(item.SourceObjectId)?.CardNo, item.EffectKind))
                errors.Add("invalid captured last-breath rune call stack");
        foreach (var item in state.TriggerQueue)
            if (item.LastBreathRuneCall is not null ? !CoreRuleEngine.ValidLastBreathRuneCallTrigger(state, item)
                : NeedsCapturedBirth(item.SourceCardNo, item.EffectKind)
                    || NeedsCapturedBirth(string.IsNullOrWhiteSpace(item.SourceObjectId) ? null : state.CardObjects.GetValueOrDefault(item.SourceObjectId)?.CardNo, item.EffectKind))
                errors.Add("invalid captured last-breath rune call trigger");
        var contexts = state.StackItems.Select(i => i.LastBreathRuneCall)
            .Concat(state.TriggerQueue.Select(i => i.LastBreathRuneCall)).OfType<LastBreathRuneCallContext>().ToArray();
        if (contexts.Select(c => (c.DeathId, c.InstanceId, c.TriggerIndex)).Distinct().Count() != contexts.Length)
            errors.Add("duplicate captured last-breath rune call instance");
    }

    private static void ValidateLastBreathRuneCallViews(IReadOnlyList<object?> payloads, List<string> errors)
    {
        foreach (var payload in payloads)
        {
            var json = JsonSerializer.SerializeToElement(payload);
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("lastBreathRuneCall", out _))
                errors.Add("public last-breath rune call view exposes a private birth receipt");
        }
    }
}
