using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LightningRushSourceIdentityTests
{
    [Fact]
    public async Task ExactCn37859KeepsItsOwnFaceAndAnyPowerFlowCostSeparateFromItsPrintedBaseCost()
    {
        using var source = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "lightning-rush-source.json")));
        var fixture = source.RootElement; var raw = fixture.GetProperty("rawApiCard");
        Assert.Equal("CN", fixture.GetProperty("region").GetString());
        Assert.Equal("VEN-156", fixture.GetProperty("ledgerIdentity").GetString());
        Assert.Equal("VEN·156/166 SC", fixture.GetProperty("printedImageCardNo").GetString());
        Assert.Equal("VEN-156/166", fixture.GetProperty("separateGlobalIdentity").GetString());
        Assert.Equal(JsonValueKind.Null, fixture.GetProperty("publicationDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, fixture.GetProperty("effectiveDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, raw.GetProperty("cardQaList").ValueKind);
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == LightningRushDeckDrawFlowTests.LightningRush);
        Assert.Equal(37859, card.Id); Assert.Equal(raw.GetProperty("cardEffect").GetString(), card.CardEffect);
        Assert.Equal(1, card.Energy); Assert.Null(card.ReturnEnergy);
        Assert.Equal(new[] { "purple", "yellow" }, card.CardColorList);
        Assert.Equal("凯南", card.Hero); Assert.False(raw.GetProperty("isPreview").GetBoolean());
        Assert.Contains(KeywordParser.Parse(card.CardEffect, card.Tag), keyword => keyword.Keyword == "流转" && keyword.Value == "2A");
        Assert.Equal(0, PrintedPowerCostRules.ForCard(card.CardNo).Amount);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(card.CardNo, out var behavior));
        Assert.Equal(2, behavior.FlowCost!.Mana); Assert.Equal(1, behavior.FlowCost.Power); Assert.Empty(behavior.FlowCost.Traits);
        Assert.Null(behavior.AppliedFlowCost);
    }

    [Fact]
    public async Task EveryOriginalFunctionalUnitAndMemberIsPreservedBesideTheOneNewPrinting()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        // Retain the exact N47 historical comparison after later independent additions.
        using var historical = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "wild-claw-historical-1036-printings.json")));
        Assert.Equal("5edb12e57576358f3ef4a9f9d6da92c313f12437",
            historical.RootElement.GetProperty("sourceCommit").GetString());
        var originalCardNos = historical.RootElement.GetProperty("cardNos").EnumerateArray()
            .Select(card => card.GetString()!).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(1036, originalCardNos.Count);
        Assert.DoesNotContain(LightningRushDeckDrawFlowTests.LightningRush, originalCardNos);
        var n47Cards = catalog.Cards.Where(c => originalCardNos.Contains(c.CardNo)
            || c.CardNo == LightningRushDeckDrawFlowTests.LightningRush).ToArray();
        var oldCards = n47Cards.Where(c => c.CardNo != LightningRushDeckDrawFlowTests.LightningRush).ToArray();
        Assert.Equal(1036, oldCards.Length); Assert.Equal(1037, n47Cards.Length);
        var oldUnits = FunctionalUnitBuilder.Build(oldCards); var units = FunctionalUnitBuilder.Build(n47Cards);
        Assert.Equal(835, oldUnits.Count); Assert.Equal(836, units.Count);
        foreach (var old in oldUnits) {
            var current = Assert.Single(units, unit => unit.Id == old.Id);
            Assert.Equal(JsonSerializer.Serialize(old), JsonSerializer.Serialize(current));
        }
    }
}
