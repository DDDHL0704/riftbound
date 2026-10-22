using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LightningRushFlowPaymentTests
{
    [Theory]
    [InlineData("red")]
    [InlineData("blue")]
    [InlineData("orange")]
    public async Task FlowAnyPowerDoesNotRequireEitherOfTheCardsPrintedTraits(string trait)
    {
        var initial = LightningRushDeckDrawFlowTests.Position(graveyard: true);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(2, 0, new Dictionary<string, int> { [trait] = 1 }) } };
        var command = new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.GenericPower);
        Assert.Empty(quote.Cost.PowerByTrait); Assert.Equal(1, quote.Cost.PrintedMana); Assert.Equal(0, quote.Cost.PrintedPower);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", command);
        Assert.Equal(0, driver.State.RunePools["P1"].Mana); Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.NotNull(Assert.Single(driver.State.StackItems).FlowPlay);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(0, 0)]
    public async Task InsufficientFlowResourcesLeaveTheOriginalGraveyardAndEveryOtherFieldUnchanged(int mana, int power)
    {
        var initial = LightningRushDeckDrawFlowTests.Position(graveyard: true);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(mana, power) } };
        var command = new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.IsValid); Assert.False(quote.CanPay); Assert.Equal(2, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.GenericPower);
        var rejected = await new CoreRuleEngine().ResolveAsync(initial, new("unpaid-flow", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.InsufficientCost, rejected.ErrorCode);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("opponent-turn")]
    [InlineData("response")]
    [InlineData("turn-start")]
    [InlineData("spell-prohibition")]
    public async Task FlowGrantsOnlyTheSourceZoneAndPreservesOrdinaryTimingAndSpellProhibitions(string branch)
    {
        var initial = LightningRushDeckDrawFlowTests.Position(graveyard: true);
        if (branch == "opponent-turn") initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "turn-start") initial = initial with { Phase = MatchPhases.TurnStart };
        if (branch == "spell-prohibition") initial = initial with { UntilEndOfTurnEffects = [CardPermissionKeywordRules.SpellPlayProhibitionPrefix + "P1"] };
        if (branch == "response") {
            initial = AttackDamageDistributionTests.Spell(initial, "OPENER", "OGN·048/298", "P2")
                with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
            var driver = new StandbyDefendObserverTests.Driver(initial);
            await driver.Act("P2", new PlayCardCommand("OPENER", "OGN·048/298", []));
            await driver.Act("P2", new PassPriorityCommand()); initial = driver.State;
            Assert.Equal("P1", initial.PriorityPlayerId);
        }
        var command = new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, []);
        var rejected = await new CoreRuleEngine().ResolveAsync(initial, new("no-new-flow-timing", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("BANISHED")]
    [InlineData("MAIN_DECK")]
    [InlineData("opponent-graveyard")]
    [InlineData("wrong-owner")]
    [InlineData("wrong-card")]
    public async Task FlowDoesNotPermitAnArbitraryZoneOwnerOrClaimedCardIdentity(string branch)
    {
        var initial = LightningRushDeckDrawFlowTests.Position(graveyard: true);
        var zones = initial.PlayerZones.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary(); var cards = initial.CardObjects.ToDictionary();
        if (branch == "BANISHED") { zones["P1"] = zones["P1"] with { Graveyard = [], Banished = ["RUSH"] }; locations["RUSH"] = new("P1", "BANISHED"); }
        if (branch == "MAIN_DECK") { zones["P1"] = zones["P1"] with { Graveyard = [], MainDeck = zones["P1"].MainDeck.Append("RUSH").ToArray() }; locations["RUSH"] = new("P1", "MAIN_DECK"); }
        if (branch == "opponent-graveyard") { zones["P1"] = zones["P1"] with { Graveyard = [] }; zones["P2"] = zones["P2"] with { Graveyard = ["RUSH"] }; locations["RUSH"] = new("P2", "GRAVEYARD"); cards["RUSH"] = cards["RUSH"] with { OwnerId = "P2", ControllerId = "P2" }; }
        if (branch == "wrong-owner") cards["RUSH"] = cards["RUSH"] with { OwnerId = "P2" };
        if (branch == "wrong-card") cards["RUSH"] = cards["RUSH"] with { CardNo = "OGN·048/298" };
        initial = initial with { PlayerZones = zones, ObjectLocations = locations, CardObjects = cards };
        var rejected = await new CoreRuleEngine().ResolveAsync(initial, new("invalid-flow-source", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, []), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("OGN·064/298")]
    [InlineData("UNL-131/219")]
    public async Task CounteringFlowReplacesBothGraveyardAndReturnToHandWithOneBanishWithoutExecutingTheLook(string counter)
    {
        var initial = AttackDamageDistributionTests.Spell(LightningRushDeckDrawFlowTests.Position(graveyard: true), "COUNTER", counter, "P2");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, []));
        var target = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", counter, [target]));
        var done = await driver.Top();
        Assert.Empty(driver.State.StackItems); Assert.Null(driver.State.PendingCardChoice);
        Assert.Equal(new[] { "RUSH" }, driver.State.PlayerZones["P1"].Banished);
        Assert.Empty(driver.State.PlayerZones["P1"].Hand); Assert.Empty(driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, driver.State.PlayerZones["P1"].MainDeck);
        Assert.Equal("BANISHED", Assert.Single(done.Events, e => e.Kind == "STACK_ITEM_COUNTERED").Payload["destinationZone"]);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind is "CARD_DRAWN" or "CARDS_PUT_IN_GRAVEYARD");
        Assert.Equal(2, driver.State.CardObjects["RUSH"].ObjectGeneration);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task GrantedEchoStillPaysThePrintedBaseCostInAdditionToFlowAndBanishesTheOnePhysicalSpellOnce()
    {
        var initial = LightningRushDeckDrawFlowTests.Position(graveyard: true)
            with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var command = new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, [], OptionalCosts: ["ECHO"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(3, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.GenericPower);
        var driver = new StandbyDefendObserverTests.Driver(initial); await driver.Act("P1", command); await driver.Top();
        var first = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, ["SPELL3"]));
        var second = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(new[] { "SPELL5", "TAIL" }, second.ContextObjectIds);
        Assert.Empty(driver.State.PlayerZones["P1"].Banished);
        await driver.Act("P1", new ChooseCardsCommand(second.ChoiceId, second.ChoiceWindow, []));
        Assert.Empty(driver.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(new[] { "RUSH" }, driver.State.PlayerZones["P1"].Banished);
        Assert.Equal(new[] { "SPELL3" }, driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(4, driver.State.PlayerZones["P1"].Graveyard.Count);
        Assert.Equal(2, driver.Events().Count(e => e.Event.Kind == "CARD_CHOICE_RESOLVED"));
        Assert.Single(driver.Events(), e => e.Event.Kind == "STACK_ITEM_RESOLVED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }
}
