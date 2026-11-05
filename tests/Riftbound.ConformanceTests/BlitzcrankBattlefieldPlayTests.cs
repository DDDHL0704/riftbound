using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BlitzcrankBattlefieldPlayTests
{
    private const string Blitzcrank = "OGN·067/298";

    [Theory]
    [InlineData("BASE")]
    [InlineData("")]
    public async Task BasePlayDoesNotCreateABattlefieldTrigger(string destination)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Blitzcrank, [], Destination: destination));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingCardChoice);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BattlefieldPlaySelectsAnEnemyAndWaitsForResponsesBeforeMoving(bool fromBattlefield)
    {
        var initial = Position();
        if (fromBattlefield) initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Blitzcrank, [], Destination: "BATTLEFIELD:BF"));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(15, game.State.RunePools["P1"].Mana);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(0, choice.RequiredCount);
        Assert.Equal(new[] { "ENEMY" }, choice.LegalObjectIds);
        Assert.Null(game.State.PriorityPlayerId);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        Assert.NotNull(game.State.PriorityPlayerId);
        Assert.NotEqual("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        await game.Top();
        Assert.Equal("BF", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Battlefields);
        Assert.Equal("P2", game.State.CardObjects["ENEMY"].ControllerId);
        await game.Replay();
    }

    [Fact]
    public async Task DecliningTheOptionalPullKeepsBothUnitsInPlace()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Blitzcrank, [], Destination: "BATTLEFIELD:BF"));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Equal("BF", game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task NoEnemyOutsideTheDestinationDoesNotLeaveAnImpossibleChoice()
    {
        var game = new Driver(RadUnitEffectTestFixture.Place(Position(), "ENEMY", "P2", "BATTLEFIELD", "BF"));
        await game.Act("P1", new PlayCardCommand("UNIT", Blitzcrank, [], Destination: "BATTLEFIELD:BF"));
        Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingCardChoice);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceInResponsePreventsItsOldAbilityFromFindingANewHere()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETREAT", "OGN·104/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Blitzcrank, [], Destination: "BATTLEFIELD:BF"));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        if (game.State.PriorityPlayerId != "P1") await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETREAT", "OGN·104/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task ItsExistingHeldAbilityReturnsItToHandAtTheNextBeginningPhase()
    {
        var initial = RadUnitEffectTestFixture.Place(Position(), "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Blitzcrank, [], Destination: "BATTLEFIELD:BF"));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        Assert.Contains(game.State.StackItems, item => item.SourceObjectId == "UNIT" && item.EffectKind == "HOLD_RETURN_SELF");
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    private static MatchState Position()
        => RadUnitEffectTestFixture.Place(VenUnitEntryBasicTests.Position(Blitzcrank), "ALLY", "P1", "BATTLEFIELD", "BF");
}
