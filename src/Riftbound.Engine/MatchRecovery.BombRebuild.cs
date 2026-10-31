using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedPublicBombRebuildEffect(
        MatchState? state,
        string? triggerId,
        string? effectKind)
        => state is not null
            && effectKind == CoreRuleEngine.BombRebuildEffect
            && state.TriggerQueue.Any(item => item.TriggerId == triggerId
                && item.BombRebuild is not null
                && CoreRuleEngine.ValidBombRebuildTrigger(state, item));

    private static void ValidateBombRebuildState(MatchState state, List<string> errors)
    {
        foreach (var marker in state.UntilEndOfTurnEffects?.Where(
                     effect => effect.StartsWith(CoreRuleEngine.BombRebuildPrefix, StringComparison.Ordinal))
                 ?? [])
        {
            var context = CoreRuleEngine.ReadBombRebuild(marker);
            if (context is null || !CoreRuleEngine.ValidBombRebuild(state, context))
                errors.Add("invalid bomb rebuild instruction");
        }

        foreach (var item in state.StackItems)
        {
            if (item.BombRebuild is not null
                ? !CoreRuleEngine.ValidBombRebuildStack(state, item)
                : item.EffectKind == CoreRuleEngine.BombRebuildEffect)
                errors.Add("invalid captured bomb rebuild stack");
        }

        foreach (var trigger in state.TriggerQueue)
        {
            if (trigger.BombRebuild is not null
                ? !CoreRuleEngine.ValidBombRebuildTrigger(state, trigger)
                : trigger.EffectKind == CoreRuleEngine.BombRebuildEffect)
                errors.Add("invalid captured bomb rebuild trigger");
        }

        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.BombRebuildChoiceWindow } choice
            && !CoreRuleEngine.ValidBombRebuildChoice(state, choice))
            errors.Add("invalid bomb rebuild choice");
    }

    private static void ValidateBombRebuildViews(
        IReadOnlyList<object?> payloads,
        List<string> errors)
    {
        foreach (var payload in payloads)
        {
            var json = JsonSerializer.SerializeToElement(payload);
            if (json.ValueKind != JsonValueKind.Object
                || !json.TryGetProperty("bombRebuild", out var rebuild)
                || rebuild.ValueKind != JsonValueKind.Object)
                continue;

            if (rebuild.TryGetProperty("source", out _)
                || rebuild.TryGetProperty("generation", out _)
                || rebuild.TryGetProperty("sourceGeneration", out _)
                || rebuild.TryGetProperty("creatingStackItemId", out _)
                || rebuild.TryGetProperty("instanceId", out _))
                errors.Add("public bomb rebuild view exposes a private generation receipt");
        }
    }
}
