using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PreconstructedDeckCatalogTests
{
    [Fact]
    public async Task BuildReturnsAtLeastThreeDistinctPreconstructedDecks()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);

        var decks = PreconstructedDeckCatalog.Build(catalog);

        Assert.True(decks.Count >= 3, $"expected at least 3 preconstructed decks, found {decks.Count}");
        Assert.Equal(decks.Count, decks.Select(deck => deck.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(decks, deck =>
        {
            Assert.False(string.IsNullOrWhiteSpace(deck.Id));
            Assert.False(string.IsNullOrWhiteSpace(deck.Name));
        });
    }

    [Fact]
    public async Task EveryPreconstructedDeckPassesOfficialDeckValidation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);

        var decks = PreconstructedDeckCatalog.Build(catalog);

        Assert.All(decks, deck =>
        {
            var validation = OfficialDeckValidator.Validate(deck.Decklist, catalog);
            Assert.True(validation.IsValid, $"{deck.Id} invalid: {string.Join("; ", validation.Errors)}");
            Assert.Equal(OfficialDeckValidator.MinimumMainDeckCount, deck.Decklist.MainDeck.Count);
            Assert.Equal(OfficialDeckValidator.RuneDeckCount, deck.Decklist.RuneDeck.Count);
            Assert.Equal(OfficialDeckValidator.BattlefieldCount, deck.Decklist.Battlefields.Count);
        });
    }

    [Fact]
    public async Task PreconstructedDecksOnlyReferenceImplementedMainDeckUnitsForPlayability()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var implementedUnitCardNos = CardBehaviorRegistry.GetAll()
            .Where(behavior => behavior.PlaysSourceToBaseAsUnit)
            .Select(behavior => behavior.CardNo)
            .ToHashSet(StringComparer.Ordinal);

        var decks = PreconstructedDeckCatalog.Build(catalog);

        Assert.All(decks, deck =>
        {
            var nonChampionUnits = deck.Decklist.MainDeck
                .Where(cardNo => !string.Equals(cardNo, deck.Decklist.ChampionCardNo, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal);
            Assert.All(nonChampionUnits, cardNo =>
                Assert.True(
                    implementedUnitCardNos.Contains(cardNo),
                    $"{deck.Id} main-deck card {cardNo} is not an implemented playable unit"));
        });
    }
}
