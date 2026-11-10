using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ArmamentEnemyMovementProtectionTests
{
    private const string Gear = "VEN·073", Charm = "OGN·043/298", Wind = "OGN·173/298";
    private static MatchState Position(bool field = false, bool attached = true)
    {
        var state = RadGearTestPosition.Create("OGN·275/298");
        if (field) state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "GEAR", Gear, "P1", field ? "BATTLEFIELD" : "BASE", field ? "BF" : null);
        if (attached) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = "ALLY" } } };
        state = RadGearTestPosition.Put(state, "CHARM", Charm, "P2", "HAND");
        return CoreRuleEngine.RecomputeRuleDerivedKeywords(state) with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
    }
    [Theory]
    [InlineData(false, "BATTLEFIELD:BF")] [InlineData(true, "BASE:P1")]
    public async Task ARealEnemySpellCanTargetTheHostButCannotMoveIt(bool field, string destination)
    {
        var game = new Driver(Position(field)); var origin = game.State.ObjectLocations["ALLY"];
        await game.Act("P2", new PlayCardCommand("CHARM", Charm, ["ALLY"], Destination: destination)); await game.Top();
        Assert.Equal(origin, game.State.ObjectLocations["ALLY"]); Assert.Equal(origin, game.State.ObjectLocations["GEAR"]);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD"
            && Equals(e.Event.Payload.GetValueOrDefault("unitObjectId"), "ALLY")); await game.Replay();
    }
    [Fact]
    public async Task TheControllersOwnSpellAndStandardMovementRemainLegal()
    {
        var initial = RadGearTestPosition.Put(Position(), "WIND", Wind, "P1", "HAND")
            with { TurnPlayerId = "P1", ActivePlayerId = "P1" };
        var spell = new Driver(initial); await spell.Act("P1", new PlayCardCommand("WIND", Wind, ["ALLY"], Destination: "BATTLEFIELD:BF"));
        await spell.Top(); Assert.Equal("BF", spell.State.ObjectLocations["ALLY"].BattlefieldObjectId); await spell.Replay();
        var standard = new Driver(initial); await standard.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        Assert.Equal("BF", standard.State.ObjectLocations["ALLY"].BattlefieldObjectId); await standard.Replay();
    }
    [Fact]
    public async Task DetachingInARealResponseMakesThePendingEnemyMovePossible()
    {
        var initial = RadGearTestPosition.Put(Position(), "DETACH", "SFD·011/221", "P1", "HAND");
        var game = new Driver(initial); await game.Act("P2", new PlayCardCommand("CHARM", Charm, ["ALLY"], Destination: "BATTLEFIELD:BF"));
        if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["ALLY", "GEAR"])); await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); Assert.Equal("BASE", game.State.ObjectLocations["GEAR"].Zone);
        await game.Replay();
    }
    [Fact]
    public async Task AnUnattachedCutlassDoesNotPreventTheEnemyMove()
    {
        var game = new Driver(Position(attached: false)); await game.Act("P2", new PlayCardCommand("CHARM", Charm, ["ALLY"], Destination: "BATTLEFIELD:BF"));
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); await game.Replay();
    }
    [Fact]
    public async Task TheEquipmentOwnerDoesNotDecideWhoIsAnEnemyOfItsHost()
    {
        var state = Position(); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { OwnerId = "P2" } } };
        var game = new Driver(state); await game.Act("P2", new PlayCardCommand("CHARM", Charm, ["ALLY"], Destination: "BATTLEFIELD:BF"));
        await game.Top(); Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone); await game.Replay();
    }
    [Fact]
    public async Task AnEnemyIndependentMovementSkillUsesTheSameExecutionRestriction()
    {
        var initial = RadGearTestPosition.Put(Position(field: true), "FAERIE", "UNL-112/219", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "WIND", Wind, "P2", "HAND");
        var game = new Driver(initial); await game.Act("P2", new PlayCardCommand("WIND", Wind, ["FAERIE"], Destination: "BATTLEFIELD:OTHER"));
        await game.Top(); var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Contains("ALLY", choice.LegalObjectIds);
        await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"])); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); await game.Replay();
    }
    [Fact]
    public async Task AnUntargetedGroupMoveStillMovesOtherEligibleUnits()
    {
        var initial = RadGearTestPosition.Put(Position(field: true), "REFERENCE", "OGN·219/298", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "SMALL", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        initial = RadGearTestPosition.Put(initial, "AREA", "UNL-107/219", "P2", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["REFERENCE"] = initial.CardObjects["REFERENCE"] with { Power = 10 },
            ["SMALL"] = initial.CardObjects["SMALL"] with { Power = 1 } } };
        var game = new Driver(initial); await game.Act("P2", new PlayCardCommand("AREA", "UNL-107/219", ["REFERENCE", "BF"])); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId); Assert.Contains("SMALL", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }
    [Fact]
    public async Task AFixedMoveToBaseRestrictionStillExecutesTheIndependentAloneEnemyDraw()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(field: true), "ISOLATE", "UNL-124/219", "P2", "HAND"));
        var hand = game.State.PlayerZones["P2"].Hand.Where(id => id != "ISOLATE").ToArray();
        var top = game.State.PlayerZones["P2"].MainDeck[0];
        await game.Act("P2", new PlayCardCommand("ISOLATE", "UNL-124/219", ["ALLY"])); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Equal(hand.Append(top), game.State.PlayerZones["P2"].Hand);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BASE");
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ACommandedOpponentCannotBypassTheOriginalEnemySpellByPerformingItsMovement(bool protectBoth)
    {
        var initial = CommandedMovementTests.Position();
        initial = RadGearTestPosition.Put(initial, "CUTLASS", Gear, "P2", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["CUTLASS"] = initial.CardObjects["CUTLASS"] with { AttachedToObjectId = "D" } } };
        if (protectBoth)
        {
            initial = RadGearTestPosition.Put(initial, "SECOND-CUTLASS", Gear, "P2", "BATTLEFIELD", "BF");
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["SECOND-CUTLASS"] = initial.CardObjects["SECOND-CUTLASS"] with { AttachedToObjectId = "D2" } } };
        }
        var game = new Driver(CoreRuleEngine.RecomputeRuleDerivedKeywords(initial));
        await game.Act("P1", CommandedMovementTests.Command()); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["D"].BattlefieldObjectId);
        if (protectBoth)
        {
            Assert.Null(game.State.PendingRuleChoice);
            Assert.Equal("BF2", game.State.ObjectLocations["A"].BattlefieldObjectId);
            Assert.Equal("BF", game.State.ObjectLocations["D2"].BattlefieldObjectId);
        }
        else
        {
            var request = game.State.PendingRuleChoice!.Request;
            Assert.Equal("BF2", request.Preview!.ObjectLocations["A"].BattlefieldObjectId);
            Assert.Equal("P2", request.PlayerId); Assert.Equal("MOVE:D2", Assert.Single(request.Options).Id);
            await RadGearTestPosition.Reject(game.State, "P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["MOVE:D"]));
            await game.Act("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["MOVE:D2"]));
            Assert.Equal("BF2", game.State.ObjectLocations["A"].BattlefieldObjectId);
            Assert.Equal("BF2", game.State.ObjectLocations["D2"].BattlefieldObjectId);
            var moved = Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"
                && Equals(e.Event.Payload.GetValueOrDefault("unitObjectId"), "D2"));
            Assert.Equal("P2", moved.Event.Payload["movingPlayerId"]);
        }
        await game.Replay();
    }
}
