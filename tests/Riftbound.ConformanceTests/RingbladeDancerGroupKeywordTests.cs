using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RingbladeDancerGroupKeywordTests
{
    private const string Dancer = "UNL-071/219";
    private static MatchState Position(bool field)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "DANCER", Dancer, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "HOST", "OGN·219/298", "P1", field ? "BATTLEFIELD" : "BASE", field ? "BF" : null);
        return RadUnitEffectTestFixture.Place(state, "ALLY", "P1", field ? "BASE" : "BATTLEFIELD", field ? null : "BF");
    }
    private static int Steadfast(Driver game, string id)
        => TemporaryCombatKeywordRules.Amount(game.State.CardObjects[id], CardCombatKeywordNames.Steadfast);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualPlayGrantsSteadfastOnlyToOtherCurrentFriendsHereAndItExpires(bool field)
    {
        var game = new Driver(Position(field)); var before = game.State.CardObjects["HOST"].Power;
        await game.Act("P1", new PlayCardCommand("DANCER", Dancer, [], Destination: field ? "BATTLEFIELD:BF" : "BASE"));
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
        Assert.Equal(0, Steadfast(game, "HOST")); await game.Top();
        Assert.Equal(1, Steadfast(game, "HOST")); Assert.Equal(before, game.State.CardObjects["HOST"].Power);
        foreach (var id in new[] { "DANCER", "ALLY", "ENEMY" }) Assert.Equal(0, Steadfast(game, id));
        Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(0, Steadfast(game, "HOST")); await game.Replay();
    }

    [Fact]
    public async Task AnActualAmbushGivesAnExistingDefenderOneMightWithoutBuffingTheDancer()
    {
        var initial = Position(true) with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(initial);
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.CardObjects["HOST"].IsDefending);
        var before = ArmamentMightTests.Might(game.State);
        await game.Act("P2", new PassFocusCommand());
        await game.Act("P1", new PlayCardCommand("DANCER", Dancer, [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal(before, ArmamentMightTests.Might(game.State)); await game.Top();
        Assert.Equal(before + 1, ArmamentMightTests.Might(game.State));
        Assert.Equal(3, CurrentPowerTargetTests.Snapshot(game.State).GetProperty("players")
            .GetProperty("P1").GetProperty("objects").GetProperty("DANCER").GetProperty("effectivePower").GetInt32());
        Assert.Equal(0, Steadfast(game, "DANCER")); await game.Replay();
    }
}
