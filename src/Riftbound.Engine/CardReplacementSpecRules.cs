using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class CardReplacementSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<ReplacementSpec>>> ReplacementsByCardNo =
        new(BuildReplacementMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool TryGetReplacement(
        string? cardNo,
        Func<ReplacementSpec, bool> predicate,
        out ReplacementSpec replacement)
    {
        replacement = default!;
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            return false;
        }

        if (!ReplacementsByCardNo.Value.TryGetValue(cardNo.Trim(), out var replacements))
        {
            return false;
        }

        var match = replacements.FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        replacement = match;
        return true;
    }

    public static bool IsFriendlyUnitDestroyedDestroySourceRecallExhaustedReplacement(
        ReplacementSpec replacement)
    {
        return string.Equals(
                replacement.Kind,
                ReplacementKinds.FriendlyUnitDestroyedDestroySourceRecallExhausted,
                StringComparison.Ordinal)
            && string.Equals(replacement.AppliesTo, "friendly-unit-destroyed", StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ReplacementSpec>> BuildReplacementMap()
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
            .Where(spec => spec.Replacements.Count > 0)
            .ToDictionary(
                spec => spec.CardNo,
                spec => (IReadOnlyList<ReplacementSpec>)spec.Replacements,
                StringComparer.Ordinal);
    }
}
