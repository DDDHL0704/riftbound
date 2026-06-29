using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
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
    public async Task TwoPlayersWithPreconstructedDecksReachMatchStartThroughReady()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var decks = PreconstructedDeckCatalog.Build(catalog);
        var session = NewSeatedSession("preconstructed-smoke-start");

        var p1Submit = await SubmitDeckAsync(session, "P1", decks[0], "smoke-submit-p1");
        var p2Submit = await SubmitDeckAsync(session, "P2", decks[1], "smoke-submit-p2");
        Assert.True(p1Submit.Accepted, p1Submit.ErrorMessage);
        Assert.True(p2Submit.Accepted, p2Submit.ErrorMessage);

        var p1Ready = await session.ReadyAsync("P1", "smoke-ready-p1", ReadyRaw(), CancellationToken.None);
        Assert.True(p1Ready.Accepted, p1Ready.ErrorMessage);
        var p2Ready = await session.ReadyAsync("P2", "smoke-ready-p2", ReadyRaw(), CancellationToken.None);
        Assert.True(p2Ready.Accepted, p2Ready.ErrorMessage);

        Assert.Contains(p2Ready.Events, gameEvent => string.Equals(gameEvent.Kind, "MATCH_STARTED", StringComparison.Ordinal));
        Assert.True(p2Ready.Snapshots.ContainsKey("P1") && p2Ready.Snapshots.ContainsKey("P2"),
            "both players must receive a snapshot once the match starts");
    }

    [Fact]
    public async Task EveryPreconstructedDeckIsAcceptedBySessionDeckSubmission()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var decks = PreconstructedDeckCatalog.Build(catalog);

        foreach (var deck in decks)
        {
            var session = NewSeatedSession($"preconstructed-accept-{deck.Id}");
            var submit = await SubmitDeckAsync(session, "P1", deck, $"accept-{deck.Id}");
            Assert.True(submit.Accepted, $"{deck.Id}: {submit.ErrorMessage}");
        }
    }

    private static MatchSession NewSeatedSession(string roomId)
    {
        var state = MatchReplayInitialStateBuilder.FromSeats(
            roomId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            });
        return new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
    }

    private static async Task<ResolutionResult> SubmitDeckAsync(
        MatchSession session,
        string playerId,
        PreconstructedDeck deck,
        string intentId)
    {
        var command = new SubmitDeckCommand(
            deck.Decklist.LegendCardNo,
            deck.Decklist.ChampionCardNo,
            deck.Decklist.MainDeck,
            deck.Decklist.RuneDeck,
            deck.Decklist.Battlefields);
        var raw = JsonSerializer.SerializeToElement(new
        {
            cmdType = "SUBMIT_DECK",
            legendCardNo = deck.Decklist.LegendCardNo,
            championCardNo = deck.Decklist.ChampionCardNo,
            mainDeck = deck.Decklist.MainDeck,
            runeDeck = deck.Decklist.RuneDeck,
            battlefields = deck.Decklist.Battlefields
        });
        return await session.SubmitDeckAsync(playerId, intentId, command, raw, CancellationToken.None);
    }

    private static JsonElement ReadyRaw()
    {
        return JsonSerializer.SerializeToElement(new { cmdType = "READY" });
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
