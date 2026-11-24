using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class RuntimeCardBehaviorSpecs
{
    private static readonly Lazy<IReadOnlyList<BehaviorSpec>> Specs = new(Build);

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggerMap = new(() =>
        Entries.Where(spec => spec.Triggers.Count > 0)
            .ToDictionary(spec => spec.CardNo, spec => (IReadOnlyList<TriggerSpec>)spec.Triggers, StringComparer.Ordinal));

    internal static IReadOnlyList<BehaviorSpec> Entries => Specs.Value;

    internal static IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>> TriggersByCardNo => TriggerMap.Value;

    private static IReadOnlyList<BehaviorSpec> Build()
    {
        // These callers use the runtime catalog and the same implementation metadata.
        // Keep the build lazy because rules can be queried before a room has opened.
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

        return BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, implementedBehaviors);
    }
}
