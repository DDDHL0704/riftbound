using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class StandbyReactionFixtureInputTests
{
    [Fact]
    public async Task RegisteredSteelClawKeepsEveryOriginalStandbyReactionDeckInput()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        Assert.Contains(catalog.Cards, card => card.CardNo == "VEN·043");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("VEN·043", out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit);
        var builder = typeof(FullGameEndToEndTests).GetMethod(
            "BuildStandbyReactionOfficialDeck", BindingFlags.Static | BindingFlags.NonPublic)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [catalog])!;
        using var original = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "preconstructed-original-4fca-inputs.json")));
        var format = Assert.Single(original.RootElement.GetProperty("formats").EnumerateArray(),
            item => item.GetProperty("format").GetString() == "CoreRules");
        var expected = Assert.Single(format.GetProperty("decks").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "vex-response").GetProperty("decklist");
        Assert.True(JsonElement.DeepEquals(expected,
            JsonSerializer.SerializeToElement(deck, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    }
}
