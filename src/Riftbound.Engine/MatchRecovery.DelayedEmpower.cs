using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedPublicDelayedEmpowerEffect(MatchState? state, string? triggerId, string? effectKind)
        => state is not null && effectKind == CoreRuleEngine.DelayedEmpowerEffect
            && state.TriggerQueue.Any(item => item.TriggerId == triggerId && item.DelayedEmpower is not null
                && CoreRuleEngine.ValidDelayedEmpowerTrigger(state, item));

    private static void ValidateDelayedEmpowerState(MatchState state, List<string> errors)
    {
        if (!CoreRuleEngine.ValidCurrentDelayedEmpowers(state)) errors.Add("invalid delayed empower instruction");
        foreach (var item in state.StackItems)
            if (item.DelayedEmpower is not null ? !CoreRuleEngine.ValidDelayedEmpowerStack(state, item)
                : item.EffectKind == CoreRuleEngine.DelayedEmpowerEffect) errors.Add("invalid captured delayed empower stack");
        foreach (var item in state.TriggerQueue)
            if (item.DelayedEmpower is not null ? !CoreRuleEngine.ValidDelayedEmpowerTrigger(state, item)
                : item.EffectKind == CoreRuleEngine.DelayedEmpowerEffect) errors.Add("invalid captured delayed empower trigger");
    }
    private static void ValidateDelayedEmpowerViews(IReadOnlyList<object?> payloads, List<string> errors)
    {
        foreach (var payload in payloads)
        {
            var json = JsonSerializer.SerializeToElement(payload);
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("delayedEmpower", out _))
                errors.Add("public delayed empower view exposes a private generation receipt");
        }
    }

}
