using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SealedCandidateReadinessTests
{
    [Theory]
    [InlineData(39708L, "RAD·R01", "base_inscript")]
    [InlineData(39709L, "RAD·R01a", "legacy_etching")]
    [InlineData(39710L, "RAD·R02", "base_inscript")]
    [InlineData(39711L, "RAD·R02a", "legacy_etching")]
    [InlineData(39712L, "RAD·R03", "base_inscript")]
    [InlineData(39713L, "RAD·R03a", "legacy_etching")]
    [InlineData(39714L, "RAD·R04", "base_inscript")]
    [InlineData(39715L, "RAD·R04a", "legacy_etching")]
    [InlineData(39716L, "RAD·R05", "base_inscript")]
    [InlineData(39717L, "RAD·R05a", "legacy_etching")]
    [InlineData(39718L, "RAD·R06", "base_inscript")]
    [InlineData(39719L, "RAD·R06a", "legacy_etching")]
    public async Task RadiantBoosterRunePrintsRetainTheirExactOfficialIdentity(
        long id, string cardNo, string printing)
    {
        var source = Assert.Single(VendettaCardSources.GetOfficialCards(), card => card.CardNo == cardNo);
        Assert.Equal(id, source.Id);
        Assert.Equal("rune", source.CardCategory);
        Assert.Equal(["符文"], source.CardCategoryNameList);
        Assert.Equal(printing, source.ExtendRarity);
        Assert.Equal(printing == "base_inscript" ? "rune_dust" : "rune_legend", source.Rarity);
        // The official Radiance overview lists normal and alternate-art runes in the
        // booster rune slot. The gallery leaves all RAD product memberships empty;
        // ADR 0001 therefore selects RAD printings by prefix, excluding hero packs.
        Assert.Empty(source.ProductCodeList);
        var admitted = Assert.Single(RadiantCardSources.GetAdmittedCards(), card => card.CardNo == cardNo);
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(admitted));
        var runtime = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(
            Assert.Single(runtime.Cards, card => card.CardNo == cardNo)));
        Assert.True(SealedSetCatalog.TryGet("RAD", out var set));
        Assert.True(set.SelectBySetPrefix);
        Assert.Contains(SealedPackGenerator.EligibleCards(runtime, set),
            card => card.Id == id && card.CardNo == cardNo);
    }

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
