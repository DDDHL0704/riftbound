using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OptionalEquipmentDeckLookTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task AttackOrderMayDeclineEvenWithEquipmentAndRecyclesAllLookedCardsWithoutRevealingThem(int equipmentCount)
    {
        const string no = "RAD·033/167";
        var initial = AttackDamageDistributionTests.Spell(VenUnitEntryBasicTests.Position("VEN·026"), "SPELL", no, "P1");
        for (var i = 4; i <= 8; i++)
            initial = RadGearTestPosition.Put(initial, "P1-DRAW-" + i, "OGN·219/298", "P1", "MAIN_DECK");
        var deck = initial.PlayerZones["P1"].MainDeck.ToArray();
        var cards = initial.CardObjects.ToDictionary();
        for (var i = 0; i < equipmentCount; i++)
            cards[deck[i]] = OfficialGarenConquestBattlefieldCountTests.Printed(deck[i], "SFD·022/221", "P1");
        initial = initial with { CardObjects = cards };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", no, [], Mode: "LOOK_EQUIPMENT"));
        Assert.Null(game.State.PendingCardChoice);
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount);
        Assert.Equal(equipmentCount == 0 ? 0 : 1, choice.MaxCount);
        Assert.Equal(deck.Take(equipmentCount), choice.LegalObjectIds);
        Assert.Equal(deck.Take(5), choice.ContextObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Null(game.State.PendingCardChoice);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(initial.PlayerZones["P1"].Hand.Where(id => id != "SPELL"), game.State.PlayerZones["P1"].Hand);
        var finalDeck = game.State.PlayerZones["P1"].MainDeck;
        Assert.Equal(deck.Length, finalDeck.Count);
        Assert.Equal(deck.Skip(5), finalDeck.Take(deck.Length - 5));
        Assert.Equal(deck.Take(5).Order(StringComparer.Ordinal), finalDeck.Skip(deck.Length - 5).Order(StringComparer.Ordinal));
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "CARD_REVEALED" or "CARD_DRAWN" or "CARD_ADDED_TO_HAND");
        Assert.Contains(game.Events(), e => e.Event.Kind == "CARD_CHOICE_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("chosenCount"), 0));
        await game.Replay();
    }
}
