using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class AmbushInteractionSpecRules
{
    private static readonly Lazy<IReadOnlySet<string>> AmbushCardNos =
        new(BuildAmbushCardNos, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool HasAmbush(string? cardNo)
    {
        return !string.IsNullOrWhiteSpace(cardNo)
            && AmbushCardNos.Value.Contains(cardNo.Trim());
    }

    private static IReadOnlySet<string> BuildAmbushCardNos()
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
            .Where(HasAmbushSpec)
            .Select(spec => spec.CardNo)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool HasAmbushSpec(BehaviorSpec spec)
    {
        return spec.Keywords.Any(keyword => string.Equals(
                keyword.Keyword,
                CardInteractionKeywordNames.Ambush,
                StringComparison.Ordinal))
            || spec.TemplateIds.Contains(BehaviorTemplateIds.Ambush, StringComparer.Ordinal);
    }
}
