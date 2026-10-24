using System.Reflection;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PreconstructedFillerScopeTests
{
    [Theory]
    [InlineData("SFD·170/221", "SFD·010/221", false)]
    [InlineData("SFD·170/221", "SFD·164/221", false)]
    [InlineData("SFD·029/221", "SFD·154/221", false)]
    [InlineData("SFD·029/221", "UNL-170/219", false)]
    [InlineData("SFD·029/221", "UNL-170/219", true)]
    public async Task CustomRedYellowOpeningRecipesKeepTheFullLegalFillerPool(
        string champion, string required, bool includeUnit)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var allowed = catalog with
        {
            Cards = catalog.Cards.Where(card => ChinaStandardBanList.IsAllowed(
                card, OfficialDeckFormat.ChinaStandard20260724)).ToArray()
        };
        string Rune(string trait) => allowed.Cards
            .Where(card => card.CardCategory == "rune" && card.CardColorList.Contains(trait))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal).First().CardNo;
        var requiredCards = Enumerable.Repeat(required, 3)
            .Concat(includeUnit ? Enumerable.Repeat("UNL-009/219", 3) : []).ToArray();
        var builder = typeof(PreconstructedDeckCatalog).GetMethod(
            "BuildLowCurveDeck", BindingFlags.Static | BindingFlags.NonPublic)!;
        var deck = (OfficialDecklist)builder.Invoke(null,
        [
            allowed, allowed.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal),
            "SFD·187/221", champion, requiredCards,
            new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("yellow"), 6).Concat(Enumerable.Repeat(Rune("red"), 6)).ToArray()
        ])!;
        var validation = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        Assert.Equal(40, deck.MainDeck.Count);
        Assert.Equal(3, deck.MainDeck.Count(card => card == required));
        Assert.Equal(12, deck.RuneDeck.Count);
        Assert.Equal(3, deck.Battlefields.Count);
    }
}
