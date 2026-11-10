using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class IsolateAloneEnemyDrawTests
{
    private const string Isolate = "UNL-124/219";
    private static MatchState Position(int enemies)
    {
        var state = RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "SPELL", Isolate, "P1", "HAND");
        for (var i = 0; i < enemies; i++) state = RadGearTestPosition.Put(state, i == 0 ? "TARGET" : "FOE" + i,
            "OGN·175/298", "P2", "BATTLEFIELD", "OTHER");
        return state;
    }
    private static async Task Resolve(Driver game, int expectedDraws)
    {
        var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        await game.Act("P1", new PlayCardCommand("SPELL", Isolate, ["TARGET"])); await game.Top();
        Assert.Equal(deck - expectedDraws, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(expectedDraws, game.Events().Count(entry => entry.Event.Kind == "CARD_DRAWN")); await game.Replay();
    }

    [Theory]
    [InlineData(1, 0)] [InlineData(2, 1)] [InlineData(3, 0)]
    public async Task OnlyAnAloneEnemyRemainingAtTheOriginalBattlefieldAfterMovementEarnsTheDraw(int enemies, int draws)
    {
        var game = new Driver(Position(enemies)); await Resolve(game, draws);
        Assert.Contains("TARGET", game.State.PlayerZones["P2"].Base);
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(2, 0)]
    public async Task TheSeparateThenInstructionStillChecksTheFieldWhenTheTargetsOwnRestrictionPreventsMovement(int enemies, int draws)
    {
        var game = new Driver(RadGearTestPosition.Put(Position(enemies), "TARGET", "UNL-111/219", "P2", "BATTLEFIELD", "OTHER"));
        await Resolve(game, draws); Assert.Equal("OTHER", game.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_MOVED_TO_BASE");
    }

    [Fact]
    public async Task AnAloneEnemyAtAnotherBattlefieldDoesNotEarnThisDraw()
    {
        var state = RadGearTestPosition.Put(Position(1), "ELSEWHERE", "OGN·175/298", "P2", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
        await Resolve(new Driver(state), 0);
    }

    [Fact]
    public async Task ARealResponseRemovingTheOtherEnemyChangesTheAfterMoveConditionToFalse()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(2), "RETURN", "OGN·104/298", "P2", "HAND"));
        await game.Act("P1", new PlayCardCommand("SPELL", Isolate, ["TARGET"])); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["FOE1"])); await game.Top(); await game.Top();
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        Assert.Contains("TARGET", game.State.PlayerZones["P2"].Base); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheTargetBeforeResolutionLeavesNoUsableBattlefieldReferenceAndNoDraw()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(2), "RETURN", "OGN·104/298", "P2", "HAND"));
        await game.Act("P1", new PlayCardCommand("SPELL", Isolate, ["TARGET"])); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["TARGET"])); await game.Top(); await game.Top();
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARD_DRAWN");
        Assert.Contains("TARGET", game.State.PlayerZones["P2"].Hand); await game.Replay();
    }
}
