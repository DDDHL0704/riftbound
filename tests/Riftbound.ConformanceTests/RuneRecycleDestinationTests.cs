using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class RuneRecycleDestinationTests
{
    [Fact]
    public async Task OfficialThunderSigilMetadataRoutesTheRuneToItsRuneDeck()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = catalog.Cards.Single(c => c.CardNo == "OGN·287/298");
        var trigger = Assert.Single(TriggerParser.Parse(card.CardEffect));
        Assert.Equal("RUNE_DECK", trigger.RecycleDestinationZone);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task ConquestRuneReturnPreservesBothDeckPrefixesAndDoesNotProduceResourcePower(int runeCount, bool exhausted)
    {
        var initial = ConquestPosition(runeCount, exhausted);
        var before = initial.PlayerZones["P1"];
        var done = await Act(initial, "P1", Battle());
        Assert.Equal(before.MainDeck, done.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(runeCount == 0 ? before.RuneDeck : before.RuneDeck.Concat(["RUNE"]).ToArray(), done.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(initial.RunePools["P1"], done.State.RunePools["P1"]);
        Assert.DoesNotContain(done.Events, e => e.Kind is "POWER_GAINED" or "RUNE_RECYCLED");
        if (runeCount > 0)
        {
            Assert.Equal("RUNE_DECK", done.State.ObjectLocations["RUNE"].Zone);
            Assert.False(done.State.CardObjects["RUNE"].IsExhausted);
            var recycled = Assert.Single(done.Events, e => e.Kind == "CARDS_RECYCLED");
            Assert.Equal("RUNE_DECK", recycled.Payload["destinationZone"]);
            Assert.Equal(["RUNE"], Assert.IsAssignableFrom<IReadOnlyList<string>>(recycled.Payload["cardIds"]));
            Assert.Equal("TRIGGER_COST_CONFIRMATION", done.State.PendingCardChoice?.ChoiceWindow);
        }
        else Assert.DoesNotContain(done.Events, e => e.Kind == "CARDS_RECYCLED");
        Restore(done.State);
    }

    internal static DeclareBattleCommand Battle() => new("THUNDER", ["ATTACKER"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"], BattlefieldTargetObjectIds: []);

    internal static MatchState ConquestPosition(int runeCount = 1, bool exhausted = false)
    {
        var state = SivirOptionalRecycleTests.ResourcePosition();
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        if (runeCount == 0) { cards.Remove("RUNE"); locations.Remove("RUNE"); }
        else cards["RUNE"] = cards["RUNE"] with { IsExhausted = exhausted };
        if (runeCount == 2) { cards["RUNE2"] = cards["RUNE"] with { ObjectId = "RUNE2" }; locations["RUNE2"] = new("P1", "BASE"); }
        cards["BOTTOM"] = new("BOTTOM", cardNo: "UNL-R01", tags: [CardObjectTags.RuneCard, "COLOR:blue"], ownerId: "P1", controllerId: "P1");
        locations["BOTTOM"] = new("P1", "RUNE_DECK");
        cards["THUNDER"] = new("THUNDER", cardNo: "OGN·287/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P1", controllerId: "P1");
        locations["THUNDER"] = new("P1", "BATTLEFIELD");
        cards["ATTACKER"] = new("ATTACKER", cardNo: "SFD·125/221", power: 3, tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1");
        locations["ATTACKER"] = new("P1", "BATTLEFIELD", BattlefieldObjectId: "THUNDER");
        cards["DEFENDER"] = new("DEFENDER", cardNo: "SFD·125/221", power: 1, tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2");
        locations["DEFENDER"] = new("P2", "BATTLEFIELD", BattlefieldObjectId: "THUNDER");
        return state with { CardObjects = cards, ObjectLocations = locations,
            UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Append(BattlefieldTaskMarkers.SpellDuelCompleted("THUNDER")).ToArray(),
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = runeCount == 0 ? [] : runeCount == 1 ? ["RUNE"] : ["RUNE", "RUNE2"], RuneDeck = ["BOTTOM"], Battlefields = ["THUNDER", "ATTACKER"] },
                ["P2"] = state.PlayerZones["P2"] with { Battlefields = ["DEFENDER"] } } };
    }
}
