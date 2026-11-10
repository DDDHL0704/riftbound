using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN 358.3.a / 359.3.e.6 keep a movement spell legal but cancel the
// prohibited instruction; 458.3 excludes recall from movement restrictions.
public sealed class SteadfastSentinelMovementTests
{
    private const string Sentinel = "UNL-111/219";
    private static MatchState Position(string controller = "P1")
        => RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "SENTINEL", Sentinel, controller,
            "BATTLEFIELD", controller == "P1" ? "BF" : "OTHER");

    [Fact]
    public async Task ActualPlayInBaseAndMoveToABattlefieldAreLegalButAStandardReturnIsNot()
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "SENTINEL", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "READY1", "UNL-009/219", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "READY2", "UNL-009/219", "P1", "HAND");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("SENTINEL", Sentinel, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("READY1", "UNL-009/219", ["SENTINEL"])); await game.Top();
        await game.Act("P1", new MoveUnitCommand("SENTINEL", "BASE", "BATTLEFIELD:BF", []));
        for (var i = 0; game.State.FocusPlayerId is { } focus; i++) {
            Assert.InRange(i, 0, 3); await game.Act(focus, new PassFocusCommand());
        }
        await game.Act("P1", new PlayCardCommand("READY2", "UNL-009/219", ["SENTINEL"])); await game.Top();
        await RadGearTestPosition.Reject(game.State, "P1", new MoveUnitCommand("SENTINEL", "BATTLEFIELD", "BASE", []));
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(game.State)["P1"].Candidates ?? [], candidate => candidate.Action == CommandTypes.MoveUnit
            && candidate.Enabled && candidate.Sources?.Any(source => source.Id == "SENTINEL") == true);
        Assert.Equal("BF", game.State.ObjectLocations["SENTINEL"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task AGroupCannotMoveTheSentinelToBaseWhileAnotherUnitCanMoveByItself()
    {
        var game = new Driver(RadUnitEffectTestFixture.Place(Position(), "ALLY", "P1", "BATTLEFIELD", "BF"));
        await RadGearTestPosition.Reject(game.State, "P1", new MoveUnitCommand("SENTINEL", "BATTLEFIELD:BF", "BASE", [],
            SourceObjectIds: ["SENTINEL", "ALLY"]));
        await game.Act("P1", new MoveUnitCommand("ALLY", "BATTLEFIELD", "BASE", []));
        Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone); Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["SENTINEL"].Zone);
        Assert.False(game.State.CardObjects["SENTINEL"].IsExhausted); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·168/298")] [InlineData("OGS·011/024")]
    public async Task FixedBaseMovementEffectsCanBeCastButDoNotMoveTheSentinel(string spell)
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "MOVE", spell, "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("MOVE", spell, ["SENTINEL"])); await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["SENTINEL"].BattlefieldObjectId);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_MOVED_TO_BASE"); await game.Replay();
    }

    [Theory]
    [InlineData("BASE:P2", "OTHER")] [InlineData("BATTLEFIELD:BF", "BF")]
    public async Task EnemyChosenMovementCancelsOnlyTheProhibitedBaseInstruction(string destination, string expectedField)
    {
        var game = new Driver(RadGearTestPosition.Put(Position("P2"), "MOVE", "OGN·043/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("MOVE", "OGN·043/298", ["SENTINEL"], Destination: destination)); await game.Top();
        Assert.Equal(expectedField, game.State.ObjectLocations["SENTINEL"].BattlefieldObjectId);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_MOVED_TO_BASE"); await game.Replay();
    }

    [Fact]
    public async Task AnActualControlAndRecallSpellStillPlacesTheSentinelInItsNewControllersBase()
    {
        var game = new Driver(RadGearTestPosition.Put(Position("P2"), "RECALL", "OGN·203/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("RECALL", "OGN·203/298", ["SENTINEL"])); await game.Top();
        Assert.Equal("P1", game.State.CardObjects["SENTINEL"].ControllerId); Assert.Contains("SENTINEL", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_MOVED_TO_BASE"); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSentinelToHandIsNotAProhibitedMovement()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SENTINEL"])); await game.Top();
        Assert.Contains("SENTINEL", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }
}
