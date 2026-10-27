using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LuxRestrictedManaSourceTests
{
    [Fact]
    public async Task Sp6KeepsTheEntireReviewedOfficialRecord()
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "lux-sp6-source.json")));
        var source = fixture.RootElement;
        Assert.Equal("CN", source.GetProperty("region").GetString());
        Assert.Equal("zh-CN", source.GetProperty("language").GetString());
        Assert.Equal(JsonValueKind.Null, source.GetProperty("publicationDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, source.GetProperty("effectiveDate").ValueKind);
        var expected = source.GetProperty("rawApiCard");
        Assert.Equal(37921, expected.GetProperty("id").GetInt32());
        Assert.Equal("VEN·SP6", expected.GetProperty("cardNo").GetString());

        using var catalog = JsonDocument.Parse(await File.ReadAllTextAsync(OfficialCardCatalog.DefaultCatalogPath()));
        var actual = Assert.Single(catalog.RootElement.GetProperty("cards").EnumerateArray(),
            card => card.GetProperty("cardNo").GetString() == "VEN·SP6");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected.GetRawText()), JsonNode.Parse(actual.GetRawText())),
            "The packaged SP6 record must preserve every field of the reviewed source.");
    }

    [Fact]
    public async Task Sp6AddsOneFunctionalUnitWithoutChangingAnyPriorUnitOrMember()
    {
        var latestCatalog = await OfficialCardCatalog.LoadDefaultAsync();
        var catalog = latestCatalog with { Total = 1088, Cards = latestCatalog.Cards.Where(c => c.CardNo != "VEN·124").ToArray() };
        var controlCards = catalog.Cards.Where(card => card.CardNo is not ("VEN·128" or "VEN·078")).ToArray();
        var priorCards = controlCards.Where(card => card.CardNo != "VEN·SP6").ToArray();
        Assert.Equal(1085, priorCards.Length);
        Assert.Equal(1086, controlCards.Length);
        var priorUnits = FunctionalUnitBuilder.Build(priorCards);
        var units = FunctionalUnitBuilder.Build(controlCards);
        Assert.Equal(866, priorUnits.Count);
        Assert.Equal(867, units.Count);
        foreach (var prior in priorUnits)
        {
            var actual = Assert.Single(units, unit => unit.Id == prior.Id);
            Assert.Equal(JsonSerializer.Serialize(prior), JsonSerializer.Serialize(actual));
        }
        var cumulativePriorCards = catalog.Cards.Where(card => card.CardNo != "VEN·SP6").ToArray();
        Assert.Equal(1087, cumulativePriorCards.Length); Assert.Equal(1088, catalog.Cards.Count);
        var cumulativePrior = FunctionalUnitBuilder.Build(cumulativePriorCards); var cumulative = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(868, cumulativePrior.Count); Assert.Equal(869, cumulative.Count);
        foreach (var unit in cumulativePrior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(cumulative, c => c.Id == unit.Id)));
        foreach (var unit in units) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(cumulative, c => c.Id == unit.Id)));
        Assert.Equal(1089, latestCatalog.Cards.Count);
        var latestUnits = FunctionalUnitBuilder.Build(latestCatalog.Cards);
        Assert.Equal(870, latestUnits.Count);
        foreach (var unit in FunctionalUnitBuilder.Build(catalog.Cards))
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(unit), System.Text.Json.JsonSerializer.Serialize(Assert.Single(latestUnits, c => c.Id == unit.Id)));
        Assert.Equal("VEN·124", Assert.Single(Assert.Single(latestUnits, c => c.Cards.Any(card => card.CardNo == "VEN·124")).Cards).CardNo);
    }
}
