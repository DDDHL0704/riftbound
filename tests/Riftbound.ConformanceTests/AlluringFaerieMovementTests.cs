using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class AlluringFaerieMovementTests
{
    private const string Faerie = "UNL-112/219";
    private static MatchState Position(bool inField = false)
    {
        var s = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·278/298"), "SOURCE", Faerie,
            "P1", inField ? "BATTLEFIELD" : "BASE", inField ? "BF" : null);
        s = RadGearTestPosition.Put(s, "RETURN", "OGN·104/298", "P1", "HAND");
        s = RadGearTestPosition.Put(s, "BLINK", "OGS·011/024", "P1", "HAND");
        return RadGearTestPosition.Put(s, "THEIR-MOVE", "OGN·043/298", "P2", "HAND");
    }
    private static Task Move(Driver game) => game.Act("P1", new MoveUnitCommand("SOURCE", "BASE", "BATTLEFIELD:BF"));
    private static Task Choose(Driver game, params string[] ids)
        => game.Act("P1", new ChooseCardsCommand(game.State.PendingCardChoice!.ChoiceId,
            game.State.PendingCardChoice.ChoiceWindow, ids));

    [Fact]
    public async Task AnActualArrivalChoosesAnyEnemyThenMovesItAfterResponsesWithoutAnExhaustCost()
    {
        var game = new Driver(Position()); await Move(game);
        Assert.Equal(0, game.State.PendingCardChoice!.RequiredCount); Assert.Null(game.State.PendingPayment);
        await Choose(game, "ENEMY"); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.True(game.State.CardObjects["SOURCE"].IsExhausted); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheOfficialArrivalBattlefieldSurvivesAnActualReturnOrMovementBackToBase(bool returnToHand)
    {
        var game = new Driver(Position()); await Move(game); await Choose(game, "ENEMY");
        await game.Act("P1", new PlayCardCommand(returnToHand ? "RETURN" : "BLINK",
            returnToHand ? "OGN·104/298" : "OGS·011/024", ["SOURCE"])); await game.Top();
        Assert.Contains("SOURCE", returnToHand ? game.State.PlayerZones["P1"].Hand : game.State.PlayerZones["P1"].Base);
        if (returnToHand) Assert.DoesNotContain("SOURCE", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsRealMovementStillEarnsTheFaeriesControllersSkill()
    {
        var game = new Driver(Position() with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        await game.Act("P2", new PlayCardCommand("THEIR-MOVE", "OGN·043/298", ["SOURCE"], Destination: "BATTLEFIELD:BF"));
        await game.Top(); Assert.Equal("P1", game.State.PendingCardChoice!.PlayerId);
        await Choose(game, "ENEMY"); Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task MovingBackToBaseDoesNotTriggerTheBattlefieldArrivalSkill()
    {
        var game = new Driver(Position(true)); await game.Act("P1", new MoveUnitCommand("SOURCE", "BATTLEFIELD", "BASE"));
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        await game.Replay();
    }

    [Fact]
    public async Task TheOptionalSkillMayBeDeclinedWithoutMovingTheEnemy()
    {
        var game = new Driver(Position()); await Move(game); await Choose(game);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("SOURCE")] [InlineData("RETURN")] [InlineData("ABSENT")]
    public async Task FriendlyHiddenAndUnknownTargetsAreAtomicRejections(string invalid)
    {
        var game = new Driver(Position()); await Move(game); var choice = game.State.PendingCardChoice!;
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [invalid]));
        await Choose(game, "ENEMY"); await game.Top(); await game.Replay();
    }
}
