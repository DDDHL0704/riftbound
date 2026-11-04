using System.Text.Json;
using Riftbound.Api;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SealedPackGeneratorTests
{
    [Fact]
    public void SetsEndpointExposesOnlyEnabledConfiguredSets()
    {
        Assert.Equal(["OGN", "SFD", "UNL", "VEN", "RAD"], SealedEndpoints.ListSets().Select(set => set.SetCode));
        Assert.True(SealedSetCatalog.TryGet(" ogn ", out var origins));
        Assert.Equal("OGN(进阶补充包)", origins.BoosterProductCode);
    }

    [Theory]
    [InlineData("OGN")]
    [InlineData("SFD")]
    [InlineData("UNL")]
    [InlineData("VEN")]
    [InlineData("RAD")]
    public async Task SixPacksPreservePhysicalPrintsSlotsAndDeterministicPrivatePool(string setCode)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var first = SealedPackGenerator.Generate(catalog, setCode, "private-seed-1");
        var second = SealedPackGenerator.Generate(catalog, setCode, "private-seed-1");
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        var restored = JsonSerializer.Deserialize<SealedPoolGeneration>(JsonSerializer.Serialize(first));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(restored));
        Assert.Equal(1, first.ConfigurationVersion);
        Assert.Equal(64, first.ConfigurationSha256.Length);
        Assert.Equal(SealedPackGenerator.Version, first.GeneratorVersion);
        Assert.Equal(6, first.Packs.Count);
        if (setCode == "OGN")
            Assert.Equal(["OGN·177/298", "OGN·055/298", "OGN·050/298", "OGN·128/298", "OGN·167/298", "OGN·011/298", "OGN·087/298"],
                first.Packs[0].Cards.Take(7).Select(card => card.CardNo));
        Assert.Equal(first.Pool.Keys.Order(StringComparer.Ordinal), first.Pool.Keys);
        var byNo = catalog.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        foreach (var pack in first.Packs)
        {
            Assert.Equal(14, pack.Cards.Count);
            Assert.Equal(13, pack.PoolCardNos.Count);
            Assert.Equal(7, pack.Cards.Count(card => card.Slot == "common" && !card.Foil));
            Assert.Equal(3, pack.Cards.Count(card => card.Slot == "uncommon" && !card.Foil));
            Assert.Equal(2, pack.Cards.Count(card => card.Slot == "rare_or_better" && card.Foil));
            Assert.Single(pack.Cards, card => card.Slot == "foil" && card.Foil);
            var rune = Assert.Single(pack.Cards, card => card.Slot == "rune_or_token");
            Assert.DoesNotContain(rune.CardNo, pack.PoolCardNos);
            Assert.All(pack.Cards, item =>
            {
                var card = byNo[item.CardNo];
                if (setCode == "UNL")
                {
                    Assert.True(item.CardNo.StartsWith("UNL-", StringComparison.Ordinal)
                        || item.CardNo.StartsWith("UNL·", StringComparison.Ordinal));
                    Assert.Contains("UNL(进阶补充包)", card.ProductCodeList);
                }
                else
                {
                    Assert.StartsWith(setCode + "·", item.CardNo);
                    if (setCode == "RAD")
                        Assert.DoesNotContain(item.CardNo, new[] { "RAD·SP1/005", "RAD·SP2/005", "RAD·SP3/005", "RAD·SP4/005", "RAD·SP5/005" });
                    else
                        Assert.Contains(setCode + "(进阶补充包)", card.ProductCodeList);
                }
            });
            var physical = pack.Cards.Select(card => byNo[card.CardNo]).ToArray();
            Assert.All(FunctionalUnitBuilder.Build(physical), unit => Assert.Single(unit.Cards));
        }
        Assert.Equal(78, first.Pool.Values.Sum());
        Assert.Equal(first.Pool.OrderBy(entry => entry.Key), first.Packs.SelectMany(pack => pack.PoolCardNos)
            .GroupBy(no => no).ToDictionary(group => group.Key, group => group.Count()).OrderBy(entry => entry.Key));
        Assert.NotEqual(JsonSerializer.Serialize(first.Packs), JsonSerializer.Serialize(
            SealedPackGenerator.Generate(catalog, setCode, "private-seed-2").Packs));
    }

    [Fact]
    public async Task EveryEnabledPhysicalPrintingLoadsAndHasAnImplementedBehavior()
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
        foreach (var set in SealedSetCatalog.Available)
        {
            var officialCandidates = SealedPackGenerator.EligibleCards(completeCatalog, set);
            var runtimeCandidates = SealedPackGenerator.EligibleCards(catalog, set);
            Assert.Equal(officialCandidates.Select(card => card.CardNo), runtimeCandidates.Select(card => card.CardNo));
            Assert.NotEmpty(runtimeCandidates);
            foreach (var card in runtimeCandidates)
            {
                Assert.True(PrintedCardFactory.TryCreate("sealed-card", "P1", card.CardNo, out var physical), card.CardNo);
                Assert.Equal(card.CardNo, physical.CardNo);
                if (card.CardCategoryName is "单位" or "英雄单位" or "专属单位" or "法术" or "专属法术" or "装备" or "专属装备")
                    Assert.True(CardBehaviorRegistry.TryGetByCardNo(card.CardNo, out _), card.CardNo);
                Assert.True(specs[card.CardNo].Status == BehaviorImplementationStatuses.Implemented,
                    $"{card.CardNo} {card.CardName}: {specs[card.CardNo].Status}");
            }
        }
    }

    [Fact]
    public async Task VendettaCandidateEntriesNotInItsSupplementalListComeFromExistingReviewedSources()
    {
        var runtime = await OfficialCardCatalog.LoadRuntimeAsync();
        var official = VendettaCardSources.GetOfficialCards();
        var original = await OfficialCardCatalog.LoadDefaultAsync();
        Assert.True(SealedSetCatalog.TryGet("VEN", out var set));
        var candidates = SealedPackGenerator.EligibleCards(new OfficialCardCatalog(
            official.First().CardNo, string.Empty, string.Empty, official.Count, official), set);
        var explicitlyAdmitted = VendettaCardSources.GetAdmittedCards().Select(card => card.CardNo)
            .ToHashSet(StringComparer.Ordinal);
        var missingFromSourceList = candidates.Where(card => !explicitlyAdmitted.Contains(card.CardNo)).ToArray();
        Assert.Equal(78, missingFromSourceList.Length);
        var originalNos = original.Cards.Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(73, missingFromSourceList.Count(card => originalNos.Contains(card.CardNo)));
        var separatelyReviewed = ReviewedRuneCardSources.GetAll().Concat(ReviewedResourceLegendSources.GetPhysicalPrints())
            .Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal);
        var missingFromOriginal = missingFromSourceList.Where(card => !originalNos.Contains(card.CardNo)).ToArray();
        Assert.Equal(["VEN·005", "VEN·006", "VEN·141", "VEN·190", "VEN·190*"],
            missingFromOriginal.Select(card => card.CardNo).Order(StringComparer.Ordinal));
        Assert.All(missingFromOriginal, card => Assert.Contains(card.CardNo, separatelyReviewed));
        Assert.All(missingFromSourceList, card => Assert.Contains(runtime.Cards, item => item.CardNo == card.CardNo));
    }

    [Fact]
    public async Task SpecialPrintingTargetsAreBackedByActualBoosterPrints()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.True(SealedSetCatalog.TryGet("VEN", out var ven));
        Assert.Equal(["VEN·SP1", "VEN·SP2", "VEN·SP3", "VEN·SP4", "VEN·SP5", "VEN·SP6"], ven.SpecialAltArtCardNos);
        var venCandidates = SealedPackGenerator.EligibleCards(catalog, ven).ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        foreach (var cardNo in ven.SpecialAltArtCardNos)
        {
            var card = venCandidates[cardNo];
            Assert.Equal("legacy_etching", card.ExtendRarity);
            Assert.Contains(ven.BoosterProductCode, card.ProductCodeList);
        }
        var zero = ven.Rates.AltArtPerPack with { Value = 0 };
        var forced = SealedPackGenerator.Generate(catalog, ven with { Rates = ven.Rates with {
            AltArtPerPack = zero,
            OvernumberedPerPack = zero,
            SpecialAltArtPerPack = ven.Rates.SpecialAltArtPerPack with { Value = 1 }
        }}, "forced-ven-special-alt-art");
        Assert.Equal(6, forced.Packs.Count);
        Assert.All(forced.Packs, pack => Assert.Contains(pack.Cards,
            card => ven.SpecialAltArtCardNos.Contains(card.CardNo, StringComparer.Ordinal)));

        Assert.True(SealedSetCatalog.TryGet("RAD", out var radiant));
        var forcedAltRune = SealedPackGenerator.Generate(catalog, radiant with { Rates = radiant.Rates with {
            AltRunePerPack = radiant.Rates.AltRunePerPack with { Value = 1 }
        }}, "forced-rad-alt-rune");
        Assert.Equal(78, forcedAltRune.Pool.Values.Sum());
        Assert.All(forcedAltRune.Packs, pack => Assert.Contains(
            Assert.Single(pack.Cards, card => card.Slot == "rune_or_token").CardNo,
            new[] { "RAD·R01a", "RAD·R02a", "RAD·R03a", "RAD·R04a", "RAD·R05a", "RAD·R06a" }));

        Assert.True(SealedSetCatalog.TryGet("UNL", out var unl));
        Assert.Empty(unl.UltimateCardNos);
        Assert.Equal(0, unl.Rates.UltimatePerPack.Value);
        Assert.Contains("待确认", unl.Rates.UltimatePerPack.Note);
        Assert.True(SealedSetCatalog.TryGet("RAD", out var rad));
        Assert.Empty(rad.SpecialAltArtCardNos);
        Assert.Empty(rad.UltimateCardNos);
        Assert.Equal(0, rad.Rates.SpecialAltArtPerPack.Value);
        Assert.Equal(0, rad.Rates.UltimatePerPack.Value);
        Assert.Contains("待确认", rad.Rates.SpecialAltArtPerPack.Note);
        Assert.Contains("待确认", rad.Rates.UltimatePerPack.Note);
    }

    [Theory]
    [InlineData("legacy_etching")]
    [InlineData("exalted_script")]
    [InlineData("runemaster_mark")]
    public async Task RareSlotReplacementsKeepActualPrintingInsteadOfBaseCardNumber(string printing)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.True(SealedSetCatalog.TryGet("OGN", out var set));
        var zero = set.Rates.AltArtPerPack with { Value = 0 };
        set = set with { Rates = set.Rates with {
            AltArtPerPack = zero with { Value = printing == "legacy_etching" ? 1 : 0 },
            OvernumberedPerPack = zero with { Value = printing == "legacy_etching" ? 0 : 1 },
            SignedFraction = zero with { Value = printing == "runemaster_mark" ? 1 : 0 }
        }};
        var generated = SealedPackGenerator.Generate(catalog, set, "forced-physical-print");
        var byNo = catalog.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        foreach (var pack in generated.Packs)
        {
            var replacement = Assert.Single(pack.Cards, item => byNo[item.CardNo].ExtendRarity == printing);
            Assert.Equal("rare_or_better", replacement.Slot);
            Assert.True(replacement.Foil);
            Assert.True(generated.Pool.ContainsKey(replacement.CardNo));
        }
    }

    [Fact]
    public async Task RareSlotEpicRateDoesNotAccidentallyIncludeUniformEpicsAndBothSlotsCanUpgrade()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.True(SealedSetCatalog.TryGet("OGN", out var set));
        var zero = set.Rates.EpicPerRareSlot with { Value = 0 };
        var rates = set.Rates with { EpicPerRareSlot = zero, AltArtPerPack = zero, OvernumberedPerPack = zero };
        var byNo = catalog.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        foreach (var probability in new[] { 0m, 1m })
        {
            var generated = SealedPackGenerator.Generate(catalog, set with { Rates = rates with {
                EpicPerRareSlot = zero with { Value = probability }
            }}, "epic-boundaries");
            Assert.All(generated.Packs.SelectMany(pack => pack.Cards).Where(card => card.Slot == "rare_or_better"),
                card => Assert.Equal(probability == 0 ? "rune_shard" : "rune_core", byNo[card.CardNo].Rarity));
        }
    }

    [Fact]
    public async Task UnknownSetsAndEmptySeedsCannotGenerateAPubliclySelectedPool()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.Throws<ArgumentException>(() => SealedPackGenerator.Generate(catalog, "unknown", "private-seed"));
        Assert.Throws<ArgumentException>(() => SealedPackGenerator.Generate(catalog, "OGN", " "));
    }

    [Fact]
    public void FrontendContractsUseStableWebNamesAndDoNotExposeGenerationSeed()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var pool = new SealedPoolDto("OGN", [new("OGN", [new("OGN·001/298", "common", false)])], [new("OGN·001/298", 1)]);
        var json = JsonSerializer.Serialize(pool, options);
        Assert.Contains("\"setCode\":\"OGN\"", json);
        Assert.Contains("\"cards\":[{\"cardNo\":", json);
        Assert.Contains("\"slot\":\"common\",\"foil\":false", json);
        Assert.DoesNotContain("seed", json, StringComparison.OrdinalIgnoreCase);
        var room = new PublicMatchDto("room", "P1", 1, 2, "waiting", DateTimeOffset.UnixEpoch);
        Assert.Equal(MatchFormats.Constructed, room.Format);
        Assert.Null(room.SetCode);
        Assert.Equal(1, room.BestOf);

        var choices = new[] { new SealedPackChoiceDto("OGN", 3), new SealedPackChoiceDto("SFD", 3) };
        var createJson = JsonSerializer.Serialize(new CreatePublicMatchOptionsDto(MatchFormats.Sealed, null, 1, choices), options);
        Assert.Contains("\"packs\":[{\"setCode\":\"OGN\",\"count\":3}", createJson);
        var projected = new PublicMatchDto("room", "P1", 1, 2, "waiting", DateTimeOffset.UnixEpoch,
            MatchFormats.Sealed, null, 1, choices);
        Assert.Contains("\"sealedPacks\":[{\"setCode\":\"OGN\",\"count\":3}",
            JsonSerializer.Serialize(projected, options));
    }
}
