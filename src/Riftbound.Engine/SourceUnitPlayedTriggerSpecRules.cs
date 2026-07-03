using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class SourceUnitPlayedTriggerSpecRules
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

    public static bool IsSupportedSourceUnitPlayedTrigger(TriggerSpec trigger)
    {
        if (!string.Equals(trigger.Timing, TriggerTimings.SourceUnitPlayed, StringComparison.Ordinal))
        {
            return false;
        }

        return trigger.Kind switch
        {
            TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle => string.Equals(
                    trigger.TargetScope,
                    TriggerTargetScopes.ControlledSpellInGraveyard,
                    StringComparison.Ordinal)
                && trigger.PlayCount is > 0
                && string.Equals(trigger.PlayOriginZone, TriggerZones.Graveyard, StringComparison.Ordinal)
                && string.Equals(trigger.PlayDestinationZone, TriggerZones.Stack, StringComparison.Ordinal)
                && string.Equals(
                    trigger.PlayCardFilter,
                    TriggerCardFilters.TagPrefix + CardObjectTags.SpellCard,
                    StringComparison.Ordinal)
                && trigger.MaximumPlayedCardManaCost is >= 0
                && trigger.IgnorePlayManaCost == true
                && trigger.PayPlayPowerCosts == true
                && trigger.RecyclePlayedCardOnResolution == true,
            _ => false
        };
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
