using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class UnitTriggerPaymentSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggersByCardNo =
        new(BuildTriggerMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool TryGetUnitArmamentAttachedPayDrawTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(cardNo, TriggerKinds.UnitArmamentAttachedPayDraw, out trigger)
            && IsUnitArmamentAttachedPayDrawTrigger(trigger);
    }

    public static bool TryGetUnitArmamentAttachedPayDrawTriggerByEffectKind(
        string? effectKind,
        out TriggerSpec trigger)
    {
        return TryGetTriggerByEffectKind(effectKind, IsUnitArmamentAttachedPayDrawTrigger, out trigger);
    }

    public static bool TryGetUnitControlledUnitPowerfulPayPowerReadyTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(cardNo, TriggerKinds.UnitControlledUnitPowerfulPayPowerReady, out trigger)
            && IsUnitControlledUnitPowerfulPayPowerReadyTrigger(trigger);
    }

    public static bool TryGetUnitControlledUnitPowerfulPayPowerReadyTriggerByEffectKind(
        string? effectKind,
        out TriggerSpec trigger)
    {
        return TryGetTriggerByEffectKind(effectKind, IsUnitControlledUnitPowerfulPayPowerReadyTrigger, out trigger);
    }

    public static bool TryGetUnitAttackPayPowerModifierTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(cardNo, TriggerKinds.UnitAttackPayPowerModifier, out trigger)
            && IsUnitAttackPayPowerModifierTrigger(trigger);
    }

    public static bool TryGetUnitAttackPayPowerModifierTriggerByEffectKind(
        string? effectKind,
        out TriggerSpec trigger)
    {
        return TryGetTriggerByEffectKind(effectKind, IsUnitAttackPayPowerModifierTrigger, out trigger);
    }

    private static bool IsUnitArmamentAttachedPayDrawTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitArmamentAttachedPayDraw, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitArmamentAttached, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.FriendlyEquipment, StringComparison.Ordinal)
            && trigger.Optional == true
            && trigger.ManaCost is > 0
            && trigger.DrawCount is > 0;
    }

    private static bool IsUnitControlledUnitPowerfulPayPowerReadyTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitControlledUnitPowerfulPayPowerReady, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.ControlledUnitBecamePowerful, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.ControlledUnitOnField, StringComparison.Ordinal)
            && trigger.Optional == true
            && trigger.PowerCost is > 0
            && !string.IsNullOrWhiteSpace(trigger.PowerCostTrait)
            && trigger.RequiredPowerThreshold is > 0
            && trigger.UnitReadyCount is > 0;
    }

    private static bool IsUnitAttackPayPowerModifierTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitAttackPayPowerModifier, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitAttack, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.UnitAtThisBattlefield, StringComparison.Ordinal)
            && trigger.Optional == true
            && trigger.ManaCost is > 0
            && trigger.PowerDelta is < 0
            && string.Equals(trigger.Duration, TriggerDurations.UntilEndOfTurn, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(trigger.EffectKind);
    }

    private static bool TryGetTriggerByEffectKind(
        string? effectKind,
        Func<TriggerSpec, bool> predicate,
        out TriggerSpec trigger)
    {
        trigger = default!;
        if (string.IsNullOrWhiteSpace(effectKind))
        {
            return false;
        }

        var normalized = effectKind.Trim();
        var match = TriggersByCardNo.Value
            .SelectMany(entry => entry.Value)
            .FirstOrDefault(candidate =>
                predicate(candidate)
                && string.Equals(RuntimeEffectKind(candidate), normalized, StringComparison.Ordinal));
        if (match is null)
        {
            return false;
        }

        trigger = match;
        return true;
    }

    private static string RuntimeEffectKind(TriggerSpec trigger)
    {
        return string.IsNullOrWhiteSpace(trigger.EffectKind)
            ? trigger.Kind
            : trigger.EffectKind!;
    }

    private static bool TryGetTrigger(string? cardNo, string kind, out TriggerSpec trigger)
    {
        trigger = default!;
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            return false;
        }

        if (!TriggersByCardNo.Value.TryGetValue(cardNo.Trim(), out var triggers))
        {
            return false;
        }

        var match = triggers.FirstOrDefault(candidate => string.Equals(candidate.Kind, kind, StringComparison.Ordinal));
        if (match is null)
        {
            return false;
        }

        trigger = match;
        return true;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>> BuildTriggerMap()
    {
        var catalog = OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var playCardBehaviors = CardBehaviorRegistry.GetAll()
            .Select(behavior => new ImplementedCardBehavior(
                behavior.CardNo,
                behavior.EffectKind,
                behavior.DisplayName,
                CardBehaviorRegistry.TriggerEffectKinds(behavior)))
            .ToArray();
        var implementedBehaviors = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(
            catalog.Cards,
            playCardBehaviors);

        return BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, implementedBehaviors)
            .Where(spec => spec.Triggers.Count > 0)
            .ToDictionary(
                spec => spec.CardNo,
                spec => (IReadOnlyList<TriggerSpec>)spec.Triggers,
                StringComparer.Ordinal);
    }
}
