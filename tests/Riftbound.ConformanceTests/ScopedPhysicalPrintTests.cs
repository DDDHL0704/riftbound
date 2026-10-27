using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ScopedPhysicalPrintTests
{
    [Theory]
    [InlineData("RAD·054/167", 3, 3, "约德尔人")]
    [InlineData("RAD·055/167", 3, 2, "机械")]
    [InlineData("RAD·050/167", 5, 4, "")]
    public void ExactRetainedPrintHasItsFullPhysicalIdentityWithoutAPlayableBehavior(
        string cardNo, int mana, int might, string tag)
    {
        var accepted = TryCreate(cardNo, out var card);
        Evidence("create-" + cardNo, new { cardNo, accepted, card });
        Assert.True(accepted);
        AssertPrinted(card, cardNo, mana, might, tag);
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(cardNo, out _));
    }

    [Theory]
    [InlineData("RAD·054/167", "HAND", 3, 3, "约德尔人")]
    [InlineData("RAD·054/167", "GRAVEYARD", 3, 3, "约德尔人")]
    [InlineData("RAD·055/167", "HAND", 3, 2, "机械")]
    [InlineData("RAD·055/167", "GRAVEYARD", 3, 2, "机械")]
    [InlineData("RAD·050/167", "HAND", 5, 4, "")]
    [InlineData("RAD·050/167", "GRAVEYARD", 5, 4, "")]
    public void SharedOutsidePlayResetRetainsThePhysicalObjectAndResetsItsPrintedFace(
        string cardNo, string zone, int mana, int might, string tag)
    {
        var previous = new CardObjectState("CARD", power: 99, manaCost: 99,
            tags: [CardObjectTags.UnitCard, "COLOR:red", "TEMPORARY_TEST_TAG"],
            cardNo: cardNo, ownerId: "P1", controllerId: "P2")
            { Damage = 2, IsExhausted = true, ObjectGeneration = 9 };
        var zones = Zones(zone);
        var cards = new Dictionary<string, CardObjectState> { ["CARD"] = previous };
        Reset(zones, cards, previous);
        Evidence("reset-" + zone + "-" + cardNo, new { cardNo, zone, previous, cards, zones });
        var restored = Assert.Contains("CARD", cards);
        AssertPrinted(restored, cardNo, mana, might, tag);
        Assert.Equal(10, restored.ObjectGeneration);
        Assert.DoesNotContain("TEMPORARY_TEST_TAG", restored.Tags);
        Assert.Contains("CARD", zone == "HAND" ? zones["P1"].Hand : zones["P1"].Graveyard);
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(cardNo, out _));
    }

    [Theory]
    [InlineData("HAND")]
    [InlineData("GRAVEYARD")]
    [InlineData("BANISHED")]
    [InlineData("MAIN_DECK")]
    public void AnImageWithTheSamePrintedFaceStillCeasesInsteadOfBecomingARealCard(string zone)
    {
        var previous = new CardObjectState("CARD", power: 3, cardNo: "RAD·054/167",
            ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard])
            { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo };
        var cards = new Dictionary<string, CardObjectState> { ["CARD"] = previous };
        var zones = Zones(zone);
        Reset(zones, cards, previous);
        Evidence("image-" + zone, new { previous, cards, zones });
        Assert.DoesNotContain("CARD", cards.Keys);
        Assert.DoesNotContain("CARD", zones.Values.SelectMany(z => z.Hand.Concat(z.Graveyard).Concat(z.Banished).Concat(z.MainDeck)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("RAD·054")]
    [InlineData("RAD·054a/167")]
    [InlineData("饰品商贩")]
    public void UnverifiedPrintAliasesAndNamesRemainUnknown(string? cardNo)
        => Assert.False(TryCreate(cardNo, out _));

    [Theory]
    [InlineData("RAD·054/167")]
    [InlineData("RAD·055/167")]
    [InlineData("RAD·050/167")]
    public async Task PhysicalSourceKnowledgeDoesNotAuthorizePlayingAnIncompleteCard(string cardNo)
    {
        var card = new CardObjectState("CARD", power: 3, cardNo: cardNo, ownerId: "P1", controllerId: "P1",
            tags: [CardObjectTags.UnitCard]);
        var state = new MatchState("SCOPED-PRINT-PLAY-BLOCKED", 0, 3, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(100, 100), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["CARD"] }, ["P2"] = PlayerZones.Empty },
            cardObjects: new Dictionary<string, CardObjectState> { ["CARD"] = card },
            objectLocations: new Dictionary<string, ObjectLocationState> { ["CARD"] = new("P1", "HAND") });
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("scoped-print-play-blocked", "P1", CommandTypes.PlayCard), new PlayCardCommand("CARD", cardNo, []), default);
        Evidence("play-blocked-" + cardNo, new { state, result });
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(cardNo, out _));
    }

    [Fact]
    public async Task EveryOriginalRuntimePrintStillCreatesItsOwnPhysicalIdentity()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        Assert.Equal(1089, catalog.Cards.Count);
        foreach (var source in catalog.Cards)
        {
            Assert.True(TryCreate(source.CardNo, out var card), source.CardNo);
            Assert.Equal(source.CardNo, card.CardNo);
            Assert.Equal(Math.Max(0, source.Energy ?? 0), card.ManaCost);
            Assert.Equal(Math.Max(0, source.Power ?? 0), card.Power);
        }
    }

    private static Dictionary<string, PlayerZones> Zones(string zone)
        => new() { ["P1"] = PlayerZones.Empty with {
            Hand = zone == "HAND" ? ["CARD"] : [],
            Graveyard = zone == "GRAVEYARD" ? ["CARD"] : [],
            Banished = zone == "BANISHED" ? ["CARD"] : [],
            MainDeck = zone == "MAIN_DECK" ? ["CARD"] : [] }, ["P2"] = PlayerZones.Empty };

    private static bool TryCreate(string? cardNo, out CardObjectState card)
    {
        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
        object?[] args = ["CARD", "P1", cardNo, null];
        var accepted = (bool)factory.GetMethod("TryCreate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
        card = (CardObjectState)args[3]!;
        return accepted;
    }

    private static void Reset(Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, CardObjectState previous)
        => typeof(CoreRuleEngine).GetMethod("ResetCardOutsidePlay", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [zones, cards, "CARD", previous, "P1"]);

    private static void AssertPrinted(CardObjectState card, string cardNo, int mana, int might, string tag)
    {
        Assert.Equal("CARD", card.ObjectId);
        Assert.Equal(cardNo, card.CardNo);
        Assert.Equal("P1", card.OwnerId);
        Assert.Equal("P1", card.ControllerId);
        Assert.Equal(mana, card.ManaCost);
        Assert.Equal(might, card.Power);
        Assert.Equal(0, card.Damage);
        Assert.False(card.IsExhausted);
        Assert.Null(card.TokenFactoryCardNo);
        Assert.Contains(CardObjectTags.UnitCard, card.Tags);
        Assert.Contains("COLOR:blue", card.Tags);
        Assert.DoesNotContain("COLOR:red", card.Tags);
        if (tag.Length > 0) Assert.Contains(tag, card.Tags);
    }

    private static void Evidence(string name, object value)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_SCOPED_PHYSICAL_EVIDENCE") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name.Replace('·', '-').Replace('/', '-') + ".json"),
            JsonSerializer.Serialize(new { scope = "Exact physical identity and existing shared reset helper; no ordinary birth or whole-card gameplay certification.", value },
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
