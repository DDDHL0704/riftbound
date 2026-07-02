using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class UnitBoonGrantedTriggerSpecRules
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

    public static bool IsUnitBoonGrantedReadySelfTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitBoonGrantedReadySelf, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitBoonGranted, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && trigger.ReadiesSource == true;
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
