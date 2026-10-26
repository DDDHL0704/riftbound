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
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var priorCards = catalog.Cards.Where(card => card.CardNo != "VEN·SP6").ToArray();
        Assert.Equal(1078, priorCards.Length);
        Assert.Equal(1079, catalog.Cards.Count);
        var priorUnits = FunctionalUnitBuilder.Build(priorCards);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(859, priorUnits.Count);
        Assert.Equal(860, units.Count);
        foreach (var prior in priorUnits)
        {
            var actual = Assert.Single(units, unit => unit.Id == prior.Id);
            Assert.Equal(JsonSerializer.Serialize(prior), JsonSerializer.Serialize(actual));
        }
    }
}
