using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class StarhoundGraveyardReturnTests
{
    private const string Hound = "UNL-167/219";
    private static MatchState Position(string target = "OGN·216/298")
    {
        var state = RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "SOURCE", Hound, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "WANTED", target, "P1", "GRAVEYARD");
        state = RadGearTestPosition.Put(state, "NOT-ANIMAL", "OGN·219/298", "P1", "GRAVEYARD");
        return state;
    }
    private static async Task Confirm(Driver game, string target = "WANTED")
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
    }

    [Theory]
    [InlineData("OGN·216/298")] [InlineData("OGN·135/298")] [InlineData("SFD·006/221")] [InlineData("UNL-160/219")]
    public async Task EachOfTheFourPrintedAnimalTagsIsAnOwnGraveyardTargetAndReturnsOnlyAfterResponses(string printing)
    {
        var game = new Driver(Position(printing)); await game.Act("P1", new PlayCardCommand("SOURCE", Hound, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(["WANTED"], choice.LegalObjectIds); Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Base);
        await Confirm(game); Assert.Contains("WANTED", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("WANTED", Assert.Single(game.State.StackItems).TargetObjectIds.Single());
        var generation = game.State.CardObjects["WANTED"].ObjectGeneration; await game.Top();
        Assert.Contains("WANTED", game.State.PlayerZones["P1"].Hand); Assert.Contains("NOT-ANIMAL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(generation + 1, game.State.CardObjects["WANTED"].ObjectGeneration); await game.Replay();
    }

    [Theory]
    [InlineData("opponent")] [InlineData("hand")] [InlineData("wrong-tag")] [InlineData("equipment")]
    public async Task OtherZonesPlayersTypesAndTagsCannotBeInjectedAsTheChosenTarget(string mode)
    {
        var state = Position();
        state = mode == "opponent" ? RadGearTestPosition.Put(state, "BAD", "OGN·216/298", "P2", "GRAVEYARD")
            : mode == "hand" ? RadGearTestPosition.Put(state, "BAD", "OGN·216/298", "P1", "HAND")
            : RadGearTestPosition.Put(state, "BAD", mode == "equipment" ? "OGN·017/298" : "OGN·219/298", "P1", "GRAVEYARD");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("SOURCE", Hound, []));
        var choice = game.State.PendingCardChoice!;
        await RadGearTestPosition.Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["BAD"]));
        await Confirm(game); await game.Top(); Assert.Contains("WANTED", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task NoEligibleCardLeavesTheUnitEnteredAndTheUnconfirmableSkillRetired()
    {
        var game = new Driver(RadUnitEffectTestFixture.Place(Position(), "WANTED", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("SOURCE", Hound, []));
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Fact]
    public async Task ARealSourceReturnCannotCancelTheCapturedGraveyardReturn()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("SOURCE", Hound, [])); await Confirm(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SOURCE"])); await game.Top(); await game.Top();
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Hand); Assert.Contains("WANTED", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task AnActualRecycleResponseMakesTheOriginalGraveyardTargetUnavailable()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RECYCLE", "UNL-103/219", "P2", "HAND"));
        await game.Act("P1", new PlayCardCommand("SOURCE", Hound, [])); await Confirm(game);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RECYCLE", "UNL-103/219", ["WANTED"], Mode: "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3"));
        await game.Top(); await game.Top(); Assert.Contains("WANTED", game.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain("WANTED", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RecoveryRejectsReplacingAnOriginallyConfirmedTargetWithAnotherLegalCardOrErasingTheSkill(bool erase)
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "SECOND", "OGN·135/298", "P1", "GRAVEYARD"));
        await game.Act("P1", new PlayCardCommand("SOURCE", Hound, [])); await Confirm(game);
        var item = Assert.Single(game.State.StackItems);
        var state = game.State with { StackItems = erase ? [] : [item with { TargetObjectIds = ["SECOND"],
            TargetGenerations = new Dictionary<string, long> { ["SECOND"] = game.State.CardObjects["SECOND"].ObjectGeneration } }] };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, state), error => error.Contains("original accepted", StringComparison.Ordinal));
    }
}
