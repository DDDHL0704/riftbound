using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LightningRushDeckDrawFlowTests
{
    internal const string LightningRush = "VEN·156";

    [Fact]
    public async Task ExistingCardTrickControlKeepsItsPutInHandAndRecycleInstructions()
    {
        var initial = Position("OGN·183/298");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("RUSH", "OGN·183/298", []));
        await driver.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        var done = await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SPELL3"]));
        Assert.Equal(new[] { "SPELL3" }, driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(new[] { "SPELL5", "TAIL" }, driver.State.PlayerZones["P1"].MainDeck.Take(2));
        Assert.Equal(new[] { "UNIT4", "SPELL4" }.Order(), driver.State.PlayerZones["P1"].MainDeck.Skip(2).Order());
        Assert.Contains(done.Events, e => e.Kind == "CARD_ADDED_TO_HAND");
        Assert.Contains(done.Events, e => e.Kind == "CARDS_RECYCLED");
        Assert.DoesNotContain(done.Events, e => e.Kind == "CARD_DRAWN");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PrivatelyChooseAtResolutionDrawAtMostOneAndPutEveryOtherLookedCardDirectlyInGraveyard(bool choose)
    {
        var initial = Position();
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("RUSH", LightningRush, []));
        Assert.Null(driver.State.PendingCardChoice);
        await driver.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("DECK_EFFECT", choice.ChoiceWindow);
        Assert.Equal(new[] { "UNIT4", "SPELL3", "SPELL4" }, choice.ContextObjectIds);
        Assert.Equal(choice.ContextObjectIds, choice.LegalObjectIds);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.DoesNotContain("\"SPELL3\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
        Assert.DoesNotContain("\"SPELL3\"", JsonSerializer.Serialize(ResolutionResult.BuildPrompts(driver.State)["P2"]));
        var done = await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, choose ? ["SPELL3"] : []));
        Assert.Equal(choose ? new[] { "SPELL3" } : [], driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(new[] { "SPELL5", "TAIL" }, driver.State.PlayerZones["P1"].MainDeck);
        Assert.Equal((choose ? new[] { "RUSH", "UNIT4", "SPELL4" } : ["RUSH", "UNIT4", "SPELL3", "SPELL4"]).Order(), driver.State.PlayerZones["P1"].Graveyard.Order());
        Assert.Equal(choose, done.Events.Any(e => e.Kind == "CARD_DRAWN"));
        Assert.DoesNotContain(done.Events, e => e.Kind is "CARD_ADDED_TO_HAND" or "CARDS_RECYCLED" or "CARD_DISCARDED" or "UNIT_DESTROYED");
        Assert.Empty(driver.State.StackItems); Assert.Null(driver.State.PendingCardChoice);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task OwnGraveyardFlowPaysTwoManaAndOneAnyPowerThenBanishesOnlyAfterTheDrawChoiceCompletes()
    {
        var initial = Position(graveyard: true);
        var command = new PlayCardCommand("RUSH", LightningRush, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.GenericPower);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", command);
        Assert.Equal(18, driver.State.RunePools["P1"].Mana);
        Assert.Equal(19, driver.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("RUSH", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(driver.State.PlayerZones["P1"].Banished);
        Assert.Equal(1, Assert.Single(driver.State.StackItems).PlayCost!.CardMana);
        Assert.Equal(2, Assert.Single(driver.State.StackItems).PlayCost!.PaidMana);
        await driver.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Empty(driver.State.PlayerZones["P1"].Banished);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SPELL4"]));
        Assert.Equal(new[] { "RUSH" }, driver.State.PlayerZones["P1"].Banished);
        Assert.DoesNotContain("RUSH", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(new[] { "SPELL4" }, driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(new[] { "UNIT4", "SPELL3" }.Order(), driver.State.PlayerZones["P1"].Graveyard.Order());
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    internal static MatchState Position(string cardNo = LightningRush, bool graveyard = false)
    {
        var control = FateWeaverDeckChoiceTests.Position();
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var printed));
        var cards = control.CardObjects.Where(p => p.Key != "WEAVER").ToDictionary();
        cards["RUSH"] = new("RUSH", cardNo: cardNo, manaCost: printed.ManaCost, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]);
        var locations = control.ObjectLocations.Where(p => p.Key != "WEAVER").ToDictionary();
        locations["RUSH"] = new("P1", graveyard ? "GRAVEYARD" : "HAND");
        return control with { RoomId = "LIGHTNING-RUSH-CN156", CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(control.PlayerZones) {
                ["P1"] = control.PlayerZones["P1"] with { Hand = graveyard ? [] : ["RUSH"], Graveyard = graveyard ? ["RUSH"] : [] } } };
    }
}
