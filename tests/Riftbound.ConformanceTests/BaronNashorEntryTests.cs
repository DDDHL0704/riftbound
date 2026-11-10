using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BaronNashorEntryTests
{
    internal static MatchState Position(string printing = "UNL-147/219")
        => RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "BARON", printing, "P1", "HAND");

    internal static MatchState Nest(MatchState state, string owner = "P2", string? controller = "P2")
    {
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.BaronNestTokenCardNo, out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["NEST"] = factory.CreateObject("NEST", owner, controller ?? owner) with { ControllerId = controller } } };
        return RadUnitEffectTestFixture.Place(state, "NEST", owner, "BATTLEFIELD", "NEST");
    }

    private static async Task CloseShowdown(Driver game)
    {
        for (var i = 0; game.State.FocusPlayerId is { } focus; i++) {
            Assert.InRange(i, 0, 3); await game.Act(focus, new PassFocusCommand());
        }
    }

    [Fact]
    public async Task AnActualEffectPlayToADeclaredBattlefieldKeepsItsReplacedEntryLocation()
    {
        var initial = OfficialRevealedHandPlayTests.Position("UNL-147/219") with { Tick = 0 };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("C", "UNL-139/219", ["BF"])); await game.Top();
        await game.Act("P1", new ChooseCardsCommand(game.State.PendingEffectPlay!.ChoiceId, "REVEALED_HAND_PLAY", ["U"]));
        await game.Act("P2", new PlayCardCommand("U", "UNL-147/219", [], Destination: "BATTLEFIELD:BF"));
        var nest = Assert.Single(game.State.CardObjects.Values, card =>
            card.TokenFactoryCardNo == P6TokenFactoryCatalog.BaronNestTokenCardNo);
        Assert.NotEqual("BF", nest.ObjectId);
        Assert.Equal(nest.ObjectId, game.State.ObjectLocations["U"].BattlefieldObjectId);
        var entered = Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_PLAYED_TO_BATTLEFIELD");
        Assert.Equal("BATTLEFIELD:" + nest.ObjectId, entered.Event.Payload["destination"]);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("UNL-147/219")] [InlineData("UNL-238/219")] [InlineData("UNL-147a/219")]
    public async Task ActualPlayCreatesOneMissingNestAndReplacesEntryBeforeTheNormalShowdown(string printing)
    {
        var game = new Driver(Position(printing)); var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("BARON", printing, []));
        var birth = Assert.Single(game.Events(), entry => entry.Event.Kind == "BATTLEFIELD_TOKEN_CREATED");
        var id = Assert.IsType<string>(birth.Event.Payload["tokenObjectId"]);
        Assert.Equal(P6TokenFactoryCatalog.BaronNestTokenCardNo, game.State.CardObjects[id].TokenFactoryCardNo);
        Assert.Equal("P1", game.State.CardObjects[id].OwnerId); Assert.Null(game.State.CardObjects[id].ControllerId);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["BARON"].Zone);
        Assert.Equal(id, game.State.ObjectLocations["BARON"].BattlefieldObjectId);
        Assert.Contains("BARON", game.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("BARON", game.State.PlayerZones["P1"].Base);
        Assert.Equal(1, game.State.CardObjects["BARON"].ObjectGeneration); Assert.True(game.State.CardObjects["BARON"].IsExhausted);
        Assert.Equal(12, game.State.CardObjects["BARON"].Power); Assert.Equal(mana - 10, game.State.RunePools["P1"].Mana);
        Assert.Equal(3, game.State.BattlefieldStates.Count); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind is "UNIT_MOVED_TO_BATTLEFIELD" or "TRIGGER_QUEUED");
        var entered = Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_PLAYED_TO_BATTLEFIELD");
        Assert.Equal("BATTLEFIELD:" + id, entered.Event.Payload["destination"]);
        Assert.True(game.State.SpellDuelState.IsActive); Assert.Equal(id, game.State.SpellDuelState.BattlefieldObjectId);
        await CloseShowdown(game);
        Assert.Equal("P1", game.State.BattlefieldStates[id].ControllerId); Assert.Equal(1, game.State.PlayerScores["P1"]);
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-147/219", "P1")] [InlineData("UNL-147/219", "P2")] [InlineData("UNL-147/219", null)]
    [InlineData("UNL-238/219", "P1")] [InlineData("UNL-238/219", "P2")] [InlineData("UNL-238/219", null)]
    [InlineData("UNL-147a/219", "P1")] [InlineData("UNL-147a/219", "P2")] [InlineData("UNL-147a/219", null)]
    public async Task AnExistingNestAnywherePreservesTheDeclaredBaseEntryRegardlessOfItsController(string printing, string? controller)
    {
        var game = new Driver(Nest(Position(printing), "P2", controller));
        await game.Act("P1", new PlayCardCommand("BARON", printing, []));
        Assert.Contains("BARON", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "BATTLEFIELD_TOKEN_CREATED");
        Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BaronNestTokenCardNo);
        await game.Replay();
    }

    [Theory]
    [InlineData("UNL-147/219")] [InlineData("UNL-238/219")] [InlineData("UNL-147a/219")]
    public async Task TheOfficialWardenFaqAllowsTheReplacementOfAnOtherwiseLegalBasePlay(string printing)
    {
        var state = RadGearTestPosition.Put(Position(printing), "WARDEN", "OGN·070/298", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("BARON", printing, []));
        var nest = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BaronNestTokenCardNo);
        Assert.Equal(nest.ObjectId, game.State.ObjectLocations["BARON"].BattlefieldObjectId); await game.Replay();
    }

    [Fact]
    public async Task AnUnaffordableOrInventedDestinationCannotCreateAnyBattlefield()
    {
        var state = Position() with { RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("BARON", "UNL-147/219", []));
        await RadGearTestPosition.Reject(Position(), "P1", new PlayCardCommand("BARON", "UNL-147/219", [], Destination: "BATTLEFIELD:INVENTED"));
    }

    [Fact]
    public async Task ReturningAndReplayingTheBaronLeavesTheExistingNestAndUsesBaseTheSecondTime()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("BARON", "UNL-147/219", [])); await CloseShowdown(game);
        var nest = game.State.ObjectLocations["BARON"].BattlefieldObjectId;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["BARON"]));
        await game.Top();
        Assert.DoesNotContain("BARON", nest!, StringComparison.Ordinal);
        await game.Act("P1", new PlayCardCommand("BARON", "UNL-147/219", []));
        Assert.Contains("BARON", game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects.ContainsKey(nest!));
        Assert.Single(game.Events(), entry => entry.Event.Kind == "BATTLEFIELD_TOKEN_CREATED"); await game.Replay();
    }

    [Fact]
    public async Task AnActualConquestGrantedGraveyardPlayUsesTheSameBattlefieldEntryReplacement()
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "BARON", "P1", "GRAVEYARD");
        state = RadGearTestPosition.Put(state, "RITUAL", "SFD·150/221", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["RITUAL"] = state.CardObjects["RITUAL"] with { AttachedToObjectId = "ALLY" } } };
        var game = new Driver(state); await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await CloseShowdown(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["BARON"])); await game.Top();
        Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("BARON", "UNL-147/219", []));
        var nest = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BaronNestTokenCardNo);
        Assert.Equal(nest.ObjectId, game.State.ObjectLocations["BARON"].BattlefieldObjectId); await game.Replay();
    }
}
