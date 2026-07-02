using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class SpellPlayedTriggerSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggersByCardNo =
        new(BuildTriggerMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool TryGetTrigger(string? cardNo, Func<TriggerSpec, bool> predicate, out TriggerSpec trigger)
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

        var match = triggers.FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        trigger = match;
        return true;
    }

    public static bool IsUnitSpellPlayedPowerModifierTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitSpellPlayedPowerModifier, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldSpellPlayed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && string.Equals(trigger.Duration, TriggerDurations.UntilEndOfTurn, StringComparison.Ordinal)
            && trigger.PowerDelta.GetValueOrDefault() != 0;
    }

    public static bool IsUnitHighCostSpellPowerModifierTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitHighCostSpellPowerModifier, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldSpellPlayed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && string.Equals(trigger.Duration, TriggerDurations.UntilEndOfTurn, StringComparison.Ordinal)
            && trigger.MinimumPaidMana.GetValueOrDefault() > 0
            && trigger.PowerDelta.GetValueOrDefault() != 0
            && !string.IsNullOrWhiteSpace(trigger.EffectKind);
    }

    public static bool IsLegendHighCostSpellDrawTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.LegendHighCostSpellDrawOne, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldSpellPlayed, StringComparison.Ordinal)
            && trigger.MinimumPaidMana.GetValueOrDefault() > 0
            && trigger.DrawCount.GetValueOrDefault() > 0;
    }

    public static bool IsLegendHighCostSpellBanishCompletionTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.LegendHighCostSpellBanishCompletion, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldSpellPlayed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceLegend, StringComparison.Ordinal)
            && trigger.MinimumPaidMana.GetValueOrDefault() > 0
            && trigger.BanishCount.GetValueOrDefault() > 0
            && trigger.RuneCallCount.GetValueOrDefault() > 0
            && trigger.DrawCount.GetValueOrDefault() > 0;
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
