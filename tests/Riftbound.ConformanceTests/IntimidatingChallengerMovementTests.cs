using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class IntimidatingChallengerMovementTests
{
    private const string Challenger = "UNL-105/219";
    private static MatchState Position(string enemy = "OGN·219/298", bool enemyHere = true)
    {
        var s = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·278/298"), "SOURCE", Challenger, "P1", "BASE");
        s = RadGearTestPosition.Put(s, "ENEMY", enemy, "P2", enemyHere ? "BATTLEFIELD" : "BASE", enemyHere ? "BF" : null);
        s = RadGearTestPosition.Put(s, "BOOST", "SFD·034/221", "P2", "HAND");
        return RadGearTestPosition.Put(s, "BLINK", "OGS·011/024", "P1", "HAND");
    }
    private static Task Move(Driver game) => game.Act("P1", new MoveUnitCommand("SOURCE", "BASE", "BATTLEFIELD:BF"));
    private static async Task Confirm(Driver game)
    {
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal("MOVEMENT_DESTINATION", request.Kind);
        Assert.Equal("BATTLEFIELD:OTHER", Assert.Single(request.Options).Id);
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["BATTLEFIELD:OTHER"]));
    }

    [Fact]
    public async Task ActualMovementConfirmsAStrictlyWeakerEnemyHereAndAnotherRealBattlefieldBeforeResponses()
    {
        var game = new Driver(Position()); await Move(game); await Confirm(game);
        Assert.Equal("BATTLEFIELD:OTHER", Assert.Single(game.State.StackItems).Destination);
        Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        await game.Top(); Assert.Equal("OTHER", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsRealMovementStillEarnsTheChallengersControllersSkill()
    {
        var state = RadGearTestPosition.Put(Position(), "THEIR-MOVE", "OGN·043/298", "P2", "HAND");
        var game = new Driver(state with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        await game.Act("P2", new PlayCardCommand("THEIR-MOVE", "OGN·043/298", ["SOURCE"], Destination: "BATTLEFIELD:BF"));
        await game.Top(); Assert.Equal("P1", game.State.PendingCardChoice!.PlayerId);
        await Confirm(game); await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId); await game.Replay();
    }

    [Theory]
    [InlineData("UNL-037/219", true)] [InlineData("OGN·219/298", false)]
    public async Task EqualMightOrADifferentCurrentLocationCannotBeChosen(string enemy, bool here)
    {
        var game = new Driver(Position(enemy, here)); await Move(game);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task ARealResponseIncreasingTheTargetsMightInvalidatesThePendingMove()
    {
        var game = new Driver(Position()); await Move(game); await Confirm(game);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("BOOST", "SFD·034/221", ["ENEMY"])); await game.Top();
        Assert.True(game.State.CardObjects["ENEMY"].Power > game.State.CardObjects["SOURCE"].Power);
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task ARealSourceMoveToBaseChangesCurrentHereWhilePreservingTheOriginalMovementCapture()
    {
        var game = new Driver(Position()); await Move(game); await Confirm(game);
        await game.Act("P1", new PlayCardCommand("BLINK", "OGS·011/024", ["SOURCE"])); await game.Top();
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Base);
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task TheBaseAndCurrentBattlefieldCannotBeInventedAsMovementDestinations()
    {
        var game = new Driver(Position()); await Move(game); var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var request = game.State.PendingRuleChoice!.Request;
        foreach (var destination in new[] { "BASE:P2", "BATTLEFIELD:BF", "BATTLEFIELD:ABSENT" })
            await RadGearTestPosition.Reject(game.State, "P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [destination]));
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["BATTLEFIELD:OTHER"]));
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("erase")] [InlineData("arrival")] [InlineData("destination")]
    public async Task RecoveryRejectsErasingTheEarnedMoveOrChangingItsOriginalArrivalOrDestination(string change)
    {
        var game = new Driver(Position()); await Move(game); await Confirm(game);
        var item = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = change == "erase" ? [] : [change == "arrival"
            ? item with { UnitEvent = item.UnitEvent! with { Here = new("P1", "BASE") } }
            : item with { Destination = "BATTLEFIELD:BF" }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Replay();
    }
}
