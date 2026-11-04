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
        Assert.Equal(["OGN", "SFD"], SealedEndpoints.ListSets().Select(set => set.SetCode));
        Assert.DoesNotContain(SealedEndpoints.ListSets(), set => set.SetCode is "RAD" or "UNL" or "VEN");
        Assert.True(SealedSetCatalog.TryGet(" ogn ", out var origins));
        Assert.Equal("OGN(进阶补充包)", origins.BoosterProductCode);
    }

    [Theory]
    [InlineData("OGN")]
    [InlineData("SFD")]
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
                Assert.StartsWith(setCode + "·", item.CardNo);
                Assert.Contains(setCode + "(进阶补充包)", byNo[item.CardNo].ProductCodeList);
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
        var play = CardBehaviorRegistry.GetAll().Select(definition => new ImplementedCardBehavior(
            definition.CardNo, definition.EffectKind, definition.DisplayName,
            CardBehaviorRegistry.TriggerEffectKinds(definition))).ToArray();
        var implemented = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(catalog.Cards, play);
        var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, FunctionalUnitBuilder.Build(catalog.Cards), implemented)
            .ToDictionary(spec => spec.CardNo, StringComparer.Ordinal);
        foreach (var set in SealedSetCatalog.Available)
        {
            foreach (var card in SealedPackGenerator.EligibleCards(catalog, set))
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
    public async Task DisabledOrUnknownSetsAndEmptySeedsCannotGenerateAPubliclySelectedPool()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        foreach (var code in new[] { "RAD", "UNL", "VEN", "unknown" })
            Assert.Throws<ArgumentException>(() => SealedPackGenerator.Generate(catalog, code, "private-seed"));
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
