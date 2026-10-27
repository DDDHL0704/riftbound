using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedPublicLastBreathBattlefieldDamageEffect(MatchState? state, string? id, string? kind)
        => state is not null && state.TriggerQueue.Any(i => i.TriggerId == id && i.EffectKind == kind
            && i.LastBreathBattlefieldDamage is not null && CoreRuleEngine.ValidLastBreathBattlefieldDamageTrigger(state, i));

    private static void ValidateLastBreathBattlefieldDamageState(MatchState state, List<string> errors)
    {
        bool MissingBirth(string id, string kind) => kind == TriggerKinds.UnitLastBreathDamageSourceBattlefieldUnits
            && (id.StartsWith("TRIGGER-death-", StringComparison.Ordinal) || id.StartsWith("ordered-TRIGGER-death-", StringComparison.Ordinal));
        foreach (var i in state.StackItems)
            if (i.LastBreathBattlefieldDamage is not null ? !CoreRuleEngine.ValidLastBreathBattlefieldDamageStack(state, i) : MissingBirth(i.StackItemId, i.EffectKind))
                errors.Add("invalid captured last-breath battlefield damage stack");
        foreach (var i in state.TriggerQueue)
            if (i.LastBreathBattlefieldDamage is not null ? !CoreRuleEngine.ValidLastBreathBattlefieldDamageTrigger(state, i) : MissingBirth(i.TriggerId, i.EffectKind))
                errors.Add("invalid captured last-breath battlefield damage trigger");
        var all = state.StackItems.Select(i => i.LastBreathBattlefieldDamage).Concat(state.TriggerQueue.Select(i => i.LastBreathBattlefieldDamage))
            .OfType<LastBreathBattlefieldDamageContext>().ToArray();
        if (all.Select(c => (c.DeathId, c.InstanceId, c.TriggerIndex)).Distinct().Count() != all.Length)
            errors.Add("duplicate captured last-breath battlefield damage instance");
    }

    private static void ValidateLastBreathBattlefieldDamageViews(IReadOnlyList<object?> payloads, List<string> errors)
    {
        foreach (var payload in payloads)
        {
            var json = JsonSerializer.SerializeToElement(payload);
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("lastBreathBattlefieldDamage", out _))
                errors.Add("public battlefield damage view exposes a private birth receipt");
        }
    }

    private static bool ValidateCapturedLastBreathBattlefieldDamageMetadata(MatchState? state, string? viewer,
        string label, string? id, string? controller, string? source, string? visibility, string? kind, string? eventKind, List<string> errors)
    {
        var item = state?.TriggerQueue.FirstOrDefault(i => i.TriggerId == id && i.LastBreathBattlefieldDamage is not null);
        if (state is null || item?.LastBreathBattlefieldDamage is not { } c) return false;
        if (!CoreRuleEngine.ValidLastBreathBattlefieldDamageTrigger(state, item))
        {
            errors.Add($"{label} captured last-breath battlefield damage metadata does not match authority");
            return true;
        }
        var expectedSource = viewer is null ? c.Source.ObjectId : ResolutionResult.VisibleObjectIdForViewer(state, c.Source.ObjectId, viewer);
        if (controller != c.ControllerId
            || source != expectedSource || kind != c.Kind || eventKind != TriggerTimings.UnitDestroyed
            || visibility is not null && visibility != (expectedSource == "HIDDEN" ? "HIDDEN" : "VISIBLE"))
            errors.Add($"{label} captured last-breath battlefield damage metadata does not match authority");
        return true;
    }
}
