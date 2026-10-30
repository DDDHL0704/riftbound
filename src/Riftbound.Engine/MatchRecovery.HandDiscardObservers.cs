using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static HandDiscardObserverView? ReadHandDiscardObserverView(object? payload)
    {
        try {
            var json = JsonSerializer.SerializeToElement(payload);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("handDiscardObserver", out var raw)
                && raw.ValueKind != JsonValueKind.Null
                ? raw.Deserialize<HandDiscardObserverView>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
        } catch (JsonException) { return null; }
    }

    private static bool ValidateHandDiscardObserverView(object? payload, string? effect,
        string? controller, string? source, List<string> errors)
    {
        var c = ReadHandDiscardObserverView(payload);
        if (effect != CoreRuleEngine.HandDiscardObserverEffect && c is null) return false;
        if (c is null || effect != CoreRuleEngine.HandDiscardObserverEffect || c.ControllerId != controller
            || c.DiscardedCount <= 0 || !HandDiscardTriggerSpecRules.TryGetTrigger(c.CardNo,
                HandDiscardTriggerSpecRules.IsHandCardsDiscardedReadySourcePowerTrigger, out var spec) || spec.Kind != c.Kind)
        { errors.Add("invalid public captured hand discard observer"); return false; }
        return true;
    }

    private static void CompareHandDiscardObserverView(object? payload, HandDiscardObserverContext? expected,
        List<string> errors, string label)
    {
        if (MatchStateHasher.HashValue(ReadHandDiscardObserverView(payload)) != MatchStateHasher.HashValue(CoreRuleEngine.PublicHandDiscardObserver(expected)))
            errors.Add($"{label} captured hand discard observer differs from authoritative state");
    }

    private static void ValidateHandDiscardObserverStackViews(IReadOnlyList<object?> payloads,
        MatchState state, List<string> errors, string label)
    {
        foreach (var payload in payloads)
        {
            ValidatePrivateHandDiscardCostView(payload, errors);
            TryReadObjectString(payload, "stackItemId", out var id);
            TryReadObjectString(payload, "effectKind", out var effect);
            TryReadObjectString(payload, "controllerId", out var controller);
            TryReadObjectString(payload, "sourceObjectId", out var source);
            ValidateHandDiscardObserverView(payload, effect, controller, source, errors);
            CompareHandDiscardObserverView(payload, state.StackItems.FirstOrDefault(i => i.StackItemId == id)?.HandDiscardObserver,
                errors, label);
        }
    }

    private static void ValidateHandDiscardObservers(MatchState state, List<string> errors)
    {
        foreach (var trigger in state.TriggerQueue)
            if (trigger.HandDiscardObserver is { } c
                ? !CoreRuleEngine.ValidHandDiscardObserver(state, c, trigger.EffectKind,
                    trigger.SourceObjectId, trigger.ControllerId, trigger.SourceCardNo)
                    || trigger.TriggeredByEventKind != c.TriggeredByEventKind
                    || trigger.EnqueuedAtTick != c.CapturedTick
                : trigger.EffectKind == CoreRuleEngine.HandDiscardObserverEffect)
                errors.Add("invalid captured hand discard observer queue");
        foreach (var item in state.StackItems)
            if (item.HandDiscardObserver is { } c
                ? !CoreRuleEngine.ValidHandDiscardObserver(state, c, item.EffectKind,
                    item.SourceObjectId, item.ControllerId, item.CardNo)
                    || item.TargetObjectIds.Count != 0 || item.DamageAmount != 0 || item.OptionalCosts.Count != 0
                    || item.SourceConfirmed || item.EffectPlayCompleted
                : item.EffectKind == CoreRuleEngine.HandDiscardObserverEffect)
                errors.Add("invalid captured hand discard observer stack");
    }
}
