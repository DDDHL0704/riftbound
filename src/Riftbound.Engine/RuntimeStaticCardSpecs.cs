using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class RuntimeStaticCardSpecs
{
    internal static readonly Lazy<(string CardNo, IReadOnlyList<StaticAbilitySpec> StaticAbilities, IReadOnlyList<StaticAuraSpec> StaticAuras)[]>
        Entries = new(BuildEntries);

    private static (string CardNo, IReadOnlyList<StaticAbilitySpec> StaticAbilities, IReadOnlyList<StaticAuraSpec> StaticAuras)[] BuildEntries()
    {
        var catalog = OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult();
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

        // Both indexes use the same inputs. Keep only the static rules they need.
        return BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, implementedBehaviors)
            .Where(spec => spec.StaticAbilities.Count > 0 || spec.StaticAuras.Count > 0)
            .Select(spec => (spec.CardNo, spec.StaticAbilities, spec.StaticAuras))
            .ToArray();
    }
}
