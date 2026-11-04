using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SealedCandidateReadinessTests
{
    [Fact]
    public async Task EveryEnabledBoosterCandidateIsLoadedAndImplemented()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var completeCards = VendettaCardSources.GetOfficialCards().Concat(catalog.Cards)
            .GroupBy(card => card.CardNo, StringComparer.Ordinal)
            .Select(group => group.Last()).ToArray();
        var completeCatalog = catalog with { Total = completeCards.Length, Cards = completeCards };
        var play = CardBehaviorRegistry.GetAll().Select(definition => new ImplementedCardBehavior(
            definition.CardNo, definition.EffectKind, definition.DisplayName,
            CardBehaviorRegistry.TriggerEffectKinds(definition))).ToArray();
        var implemented = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(completeCards, play);
        var specs = BehaviorSpecCatalogBuilder.Build(completeCards, FunctionalUnitBuilder.Build(completeCards), implemented)
            .ToDictionary(spec => spec.CardNo, StringComparer.Ordinal);

        foreach (var set in SealedSetCatalog.Available.Where(set => set.SetCode is "UNL" or "VEN" or "RAD"))
        {
            var completeCandidates = SealedPackGenerator.EligibleCards(completeCatalog, set);
            var runtimeCandidates = SealedPackGenerator.EligibleCards(catalog, set);
            Assert.Equal(completeCandidates.Select(card => card.CardNo), runtimeCandidates.Select(card => card.CardNo));
            Assert.NotEmpty(runtimeCandidates);
            Assert.All(runtimeCandidates, card =>
            {
                Assert.True(specs.TryGetValue(card.CardNo, out var spec), card.CardNo);
                Assert.Equal(BehaviorImplementationStatuses.Implemented, spec.Status);
                Assert.True(PrintedCardFactory.TryCreate("sealed-card", "P1", card.CardNo, out _), card.CardNo);
            });
        }
    }
}
