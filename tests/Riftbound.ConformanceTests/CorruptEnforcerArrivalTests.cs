using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class CorruptEnforcerArrivalTests
{
    private const string Enforcer = "SFD·123/221";
    private static MatchState Position(bool inField = false, bool hand = true)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "ENFORCER", Enforcer,
            "P1", inField ? "BATTLEFIELD" : "BASE", inField ? "BF" : null);
        state = RadGearTestPosition.Put(state, "FOOTHOLD", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        if (hand)
        {
            state = RadGearTestPosition.Put(state, "KEEP", "OGN·219/298", "P1", "HAND");
            state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        }
        return state;
    }
    private static async Task Move(Driver game, bool toBase = false)
    {
        await game.Act("P1", new MoveUnitCommand("ENFORCER", toBase ? "BATTLEFIELD" : "BASE",
            toBase ? "BASE" : "BATTLEFIELD:BF"));
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
                game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
    }
    private static async Task Discard(Driver game, string option)
    {
        await game.Top();
        var pending = game.State.PendingRuleChoice!;
        Assert.NotNull(pending); Assert.Equal("PRIVATE_HAND_DISCARD", pending.Request.Kind);
        await game.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [option]));
    }

    [Fact]
    public async Task ActualBattlefieldArrivalDiscardsExactlyOneHandCardWithoutDrawing()
    {
        var game = new Driver(Position()); var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        await Move(game); Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        var skill = Assert.Single(game.State.StackItems); Assert.Equal(Enforcer, skill.CardNo);
        Assert.Equal("P1", skill.ControllerId); Assert.Equal("BF", skill.UnitEvent!.Here!.BattlefieldObjectId);
        await Discard(game, "HAND:0");
        Assert.Contains("KEEP", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(new[] { "RETURN" }, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck.Count); await game.Replay();
    }

    [Fact]
    public async Task ArrivalWithAnEmptyHandStillResolvesTheMandatoryInstructionWithoutDrawing()
    {
        var game = new Driver(Position(hand: false)); await Move(game);
        Assert.Single(game.State.StackItems); var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        await game.Top(); Assert.Null(game.State.PendingRuleChoice); Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.PlayerZones["P1"].Hand); Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck.Count);
        await game.Replay();
    }

    [Fact]
    public async Task MovementToBaseDoesNotRequireADiscard()
    {
        var game = new Driver(Position(inField: true)); await Move(game, toBase: true);
        Assert.Contains("ENFORCER", game.State.PlayerZones["P1"].Base);
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
    }

    [Fact]
    public async Task MovementByTheOpponentStillDiscardsFromTheEnforcersControllersHand()
    {
        var state = RadGearTestPosition.Put(Position(), "THEIR-MOVE", "OGN·043/298", "P2", "HAND");
        var game = new Driver(state with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        await game.Act("P2", new PlayCardCommand("THEIR-MOVE", "OGN·043/298", ["ENFORCER"], Destination: "BATTLEFIELD:BF"));
        await game.Top();
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
                game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
        Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        await Discard(game, "HAND:0"); Assert.Contains("KEEP", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.PlayerZones["P2"].Hand); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceLeavesAnIndependentDiscardWithAPrivateSource()
    {
        var game = new Driver(Position()); await Move(game);
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ENFORCER"])); await game.Top();
        Assert.Contains("ENFORCER", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"ENFORCER\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await Discard(game, "HAND:1");
        Assert.Contains("ENFORCER", game.State.PlayerZones["P1"].Hand);
        Assert.Contains("KEEP", game.State.PlayerZones["P1"].Graveyard);
        var discarded = Assert.Single(game.Events(), entry => entry.Event.Kind == "CARD_DISCARDED");
        Assert.Equal("HIDDEN", discarded.Event.Payload["sourceObjectId"]); await game.Replay();
    }

    [Theory]
    [InlineData("destination")] [InlineData("erase")] [InlineData("duplicate")]
    public async Task RecoveryRejectsAChangedArrivalFieldOrAnErasedOrRepeatedAcceptedSkill(string branch)
    {
        var game = new Driver(Position()); await Move(game); var item = Assert.Single(game.State.StackItems);
        var changed = item with { UnitEvent = item.UnitEvent! with { Here = new("P1", "BATTLEFIELD", "OTHER") } };
        var forged = game.State with { StackItems = branch switch {
            "erase" => [], "duplicate" => [item, item], _ => [changed] } };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await Discard(game, "HAND:0"); await game.Replay();
    }
}
