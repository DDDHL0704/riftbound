using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class Ven124FriendlyDestructionSourceTests
{
    private const string No = "VEN·124";
    private const string Text = "{{强化}} — 摧毁一名友方单位（支付此费用：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+2。";

    [Fact]
    public async Task ExactCn37824PreservesTheEntireReviewedRecordAndUnknownDates()
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "ven124-friendly-destruction-source.json")));
        var source = fixture.RootElement;
        Assert.Equal("CN", source.GetProperty("region").GetString());
        Assert.Equal("zh-CN", source.GetProperty("language").GetString());
        Assert.Equal(JsonValueKind.Null, source.GetProperty("publicationDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, source.GetProperty("effectiveDate").ValueKind);
        Assert.Equal("c3e59bd25b90264dcdac5a1cdeb1ffc39374959366a2c21198a69b7f76e60cc2", source.GetProperty("sourceRecordSha256").GetString());
        using var normal = JsonDocument.Parse(await File.ReadAllTextAsync(OfficialCardCatalog.DefaultCatalogPath()));
        var actual = Assert.Single(normal.RootElement.GetProperty("cards").EnumerateArray(), c => c.GetProperty("cardNo").GetString() == No);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(source.GetProperty("rawApiCard").GetRawText()), JsonNode.Parse(actual.GetRawText())));
        var addition = Assert.Single(normal.RootElement.GetProperty("scopedAdditions").EnumerateArray(), c => c.GetProperty("cardNo").GetString() == No);
        Assert.Equal(37824, addition.GetProperty("sourceId").GetInt32());
        Assert.Equal(source.GetProperty("sourceRecordSha256").GetString(), addition.GetProperty("sourceRecordSha256").GetString());
        Assert.Equal(source.GetProperty("frontImageSha256").GetString(), addition.GetProperty("frontImageSha256").GetString());
    }

    [Fact]
    public async Task NormalPrintedProfileKeepsTheRequiredDestructionSeparateFromFreeResourcesAndTheSelfBonus()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37824, card.Id); Assert.Equal("脱逃的灰背", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(3, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(3, card.Power);
        Assert.Equal(["unit"], card.CardCategoryList); Assert.Equal([RuneTrait.Yellow], card.CardColorList);
        Assert.Equal("诺克萨斯", card.Region); Assert.Empty(card.Tag);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(3, behavior.ManaCost); Assert.Equal(3, behavior.SourceUnitPower);
        Assert.Equal(0, PrintedPowerCostRules.ForCard(No).Amount);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.False(cost.SupportedResources); Assert.Equal("摧毁一名友方单位", cost.AdditionalCost);
        Assert.Equal(0, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower); Assert.False(cost.Exhaust);
        var self = EmpowerParser.Self(card.CardEffect); Assert.Equal(2, self.Might); Assert.Empty(self.Keywords);
    }

    [Fact]
    public async Task OneNewSourceGroupPreservesEveryPriorGroupAndMember()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var priorCards = catalog.Cards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1088, priorCards.Length); Assert.Equal(1089, catalog.Cards.Count);
        var prior = FunctionalUnitBuilder.Build(priorCards); var current = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(869, prior.Count); Assert.Equal(870, current.Count);
        foreach (var unit in prior)
            Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(current, c => c.Id == unit.Id)));
        Assert.Equal(No, Assert.Single(Assert.Single(current, c => c.Cards.Any(card => card.CardNo == No)).Cards).CardNo);
    }
}
