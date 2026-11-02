using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadGoodStuffTests
{
    private const string GoodStuff = "RAD·106/167";

    [Fact]
    public async Task OfficialPrintingEntersBeforePrivatelyDiscardingThenDrawingThree()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == GoodStuff);
        Assert.Equal(39608, card.Id); Assert.Equal(3, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        var game = new Driver(Hand(2));
        await game.Act("P1", new PlayCardCommand("GEAR", GoodStuff, [], Destination: "BATTLEFIELD:OTHER"));
        Assert.Equal("OTHER", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(6, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Single(game.State.StackItems);
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        Assert.Equal("PRIVATE_HAND_DISCARD", choice.Request.Kind);
        var selected = Assert.Single(choice.Request.Options, o => o.ObjectIds!.Contains("F2"));
        var command = new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [selected.Id]);
        await Reject(game.State, "P2", command);
        await game.Act("P1", command);
        Assert.Contains("F2", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("F1", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(4, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(3, game.State.PlayerZones["P1"].MainDeck.Count);
        var kinds = game.Events().Select(e => e.Event.Kind).ToArray();
        Assert.True(Array.FindIndex(kinds, k => k == "CARD_DISCARDED") < Array.FindIndex(kinds, k => k == "CARD_DRAWN"));
        await game.Replay();
    }

    [Fact]
    public async Task EmptyHandDoesNotPreventTheThreeCardDraw()
    {
        var game = new Driver(Hand(0));
        await game.Act("P1", new PlayCardCommand("GEAR", GoodStuff, [], Destination: "BATTLEFIELD:BF"));
        await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(3, game.State.PlayerZones["P1"].Hand.Count);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DISCARDED");
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:missing")]
    public Task IllegalDeploymentCannotEnterOrSpendResources(string destination)
        => Reject(Hand(2), "P1", new PlayCardCommand("GEAR", GoodStuff, [], Destination: destination));

    [Fact]
    public Task TheClientCannotPreselectAPrivateDiscardDuringPlay()
        => Reject(Hand(2), "P1", new PlayCardCommand("GEAR", GoodStuff, ["F1"], Destination: "BATTLEFIELD:BF"));

    [Fact]
    public Task InsufficientManaCannotDiscardOrDraw()
    {
        var state = Hand(2);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(2, 10) } };
        return Reject(state, "P1", new PlayCardCommand("GEAR", GoodStuff, [], Destination: "BATTLEFIELD:BF"));
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task HoldingItsBattlefieldMustReturnItToItsOwnerWithoutAnOptionalChoice(string owner)
    {
        var state = Gear(OfficialHoldSequenceTests.State("OGN·278/298"), owner, "F");
        state = RadUnitEffectTestFixture.Place(state, "GEAR", "P1", "BATTLEFIELD", "F");
        var game = new Driver(state);
        await game.Act("P1", new PassPriorityCommand());
        Assert.Single(game.State.StackItems, i => i.SourceObjectId == "GEAR");
        await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains("GEAR", game.State.PlayerZones[owner].Hand);
        Assert.DoesNotContain("GEAR", game.State.PlayerZones["P1"].Battlefields);
        await game.Replay();
    }

    [Fact]
    public async Task HoldingAnotherBattlefieldDoesNotReturnThisEquipment()
    {
        var game = new Driver(Gear(OfficialHoldSequenceTests.State("OGN·278/298"), "P1", "OTHER"));
        await game.Act("P1", new PassPriorityCommand());
        Assert.DoesNotContain(game.State.StackItems, i => i.SourceObjectId == "GEAR");
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Battlefields);
        await game.Replay();
    }

    private static MatchState Hand(int count)
    {
        var state = Put(Create(), "GEAR", GoodStuff, "P1", "HAND");
        for (var i = 1; i <= count; i++) state = Put(state, "F" + i, "OGN·004/298", "P1", "HAND");
        return state;
    }

    private static MatchState Gear(MatchState state, string owner, string field)
    {
        state = Put(state, "GEAR", GoodStuff, owner, "BATTLEFIELD", field);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { Tags = [..state.CardObjects["GEAR"].Tags, CardEquipmentKeywordNames.Deploy] } } };
    }
}
