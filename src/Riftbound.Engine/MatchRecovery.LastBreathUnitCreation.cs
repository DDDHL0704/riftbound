using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedPublicLastBreathUnitCreationEffect(MatchState? state, string? triggerId, string? effectKind)
        => state is not null && state.TriggerQueue.Any(item => item.TriggerId == triggerId
            && item.EffectKind == effectKind && item.LastBreathUnitCreation is not null
            && CoreRuleEngine.ValidLastBreathUnitCreationTrigger(state, item));

    private static void ValidateLastBreathUnitCreationState(MatchState state, List<string> errors)
    {
        bool NeedsCapturedCondition(string? cardNo, string kind)
            => UnitDestroyedTriggerSpecRules.BaseUnitCreationTriggers(cardNo).Any(t => t.Kind == kind && t.RequiresSourceEmpowered is not null);
        foreach (var item in state.StackItems)
            if (item.LastBreathUnitCreation is not null ? !CoreRuleEngine.ValidLastBreathUnitCreationStack(state, item)
                : NeedsCapturedCondition(item.CardNo, item.EffectKind))
                errors.Add("invalid captured last-breath unit creation stack");
        foreach (var item in state.TriggerQueue)
            if (item.LastBreathUnitCreation is not null ? !CoreRuleEngine.ValidLastBreathUnitCreationTrigger(state, item)
                : NeedsCapturedCondition(item.SourceCardNo ?? (string.IsNullOrWhiteSpace(item.SourceObjectId) ? null : state.CardObjects.GetValueOrDefault(item.SourceObjectId)?.CardNo), item.EffectKind))
                errors.Add("invalid captured last-breath unit creation trigger");
        var contexts = state.StackItems.Select(i => i.LastBreathUnitCreation)
            .Concat(state.TriggerQueue.Select(i => i.LastBreathUnitCreation)).OfType<LastBreathUnitCreationContext>().ToArray();
        if (contexts.Select(c => (c.DeathId, c.InstanceId, c.TriggerIndex)).Distinct().Count() != contexts.Length)
            errors.Add("duplicate captured last-breath unit creation instance");
    }

    private static void ValidateLastBreathUnitCreationViews(IReadOnlyList<object?> payloads, List<string> errors)
    {
        foreach (var payload in payloads)
        {
            var json = JsonSerializer.SerializeToElement(payload);
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("lastBreathUnitCreation", out _))
                errors.Add("public last-breath unit creation view exposes a private birth receipt");
        }
    }
}
