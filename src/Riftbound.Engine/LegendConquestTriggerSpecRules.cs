using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class LegendConquestTriggerSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggersByCardNo =
        new(BuildTriggerMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<TriggerSpec> TriggersForCard(string? cardNo)
    {
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            return [];
        }

        return TriggersByCardNo.Value.TryGetValue(cardNo.Trim(), out var triggers)
            ? triggers
            : [];
    }

    public static bool TryGetTrigger(string? cardNo, Func<TriggerSpec, bool> predicate, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = TriggersForCard(cardNo).FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        trigger = match;
        return true;
    }

    public static bool IsLegendConquestPayReadySelfTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.LegendConquestPayReadySelf, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldConquered, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceLegend, StringComparison.Ordinal)
            && trigger.ManaCost is > 0
            && trigger.LegendReadyCount is 1
            && trigger.ReadiesSource is true;
    }

    public static bool IsLegendConquestReadySelfTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.LegendConquestReadySelf, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldConquered, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceLegend, StringComparison.Ordinal)
            && trigger.LegendReadyCount is 1
            && trigger.ReadiesSource is true;
    }

    public static bool IsLegendConquestOverkillExhaustReadyUnitTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.LegendConquestOverkillExhaustReadyUnit, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldConquered, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.ExhaustedUnitOnField, StringComparison.Ordinal)
            && trigger.RequiredOverkillDamage is > 0
            && trigger.ExhaustsSource is true
            && trigger.UnitReadyCount is 1;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>> BuildTriggerMap()
    {
        var catalog = OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var playCardBehaviors = CardBehaviorRegistry.GetAll()
            .Select(behavior => new ImplementedCardBehavior(
                behavior.CardNo,
                behavior.EffectKind,
                behavior.DisplayName))
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
