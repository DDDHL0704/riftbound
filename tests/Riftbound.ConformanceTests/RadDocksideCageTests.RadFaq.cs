using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed partial class RadDocksideCageTests
{
    // DOC-17 p16 Q1/Q2: an enemy-owned, enemy-controlled unit is played by us.
    [Theory]
    [InlineData("BASE")]
    [InlineData("BATTLEFIELD:BF")]
    public async Task HoldingTheCageLetsUsPlayTheActualEnemyUnitAtOurPrintedDestinations(string destination)
    {
        var state = RadGearTestPosition.Put(Position(), "PRISONER", "OGN·219/298", "P2", "BATTLEFIELD", "BF");
        Assert.Equal("P2", state.CardObjects["PRISONER"].ControllerId);
        var game = new Driver(state);
        var generation = game.State.CardObjects["PRISONER"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("CARD", Cage, [], Destination: "BATTLEFIELD:BF"));
        await Choose(game, ["PRISONER"]); await game.Top();
        Assert.Contains("PRISONER", game.State.PlayerZones["P2"].Banished);
        Assert.Equal(generation + 1, game.State.CardObjects["PRISONER"].ObjectGeneration);
        await game.ColdCheckpoint();
        if (game.State.SpellDuelState.IsActive)
        {
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        }
        Assert.False(game.State.SpellDuelState.IsActive);
        Assert.False(game.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralOpen, game.State.TimingState);
        await Hold(game); await Choose(game, ["CARD"]); await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal("P1", pending.PlayerId); Assert.Equal("P2", pending.SourcePlayerId);
        await game.ColdCheckpoint();
        var pool = game.State.RunePools["P1"];
        await Reject(game.State, "P2", new PlayCardCommand("PRISONER", "OGN·219/298", [], Destination: destination));
        await Reject(game.State, "P1", new PlayCardCommand("PRISONER", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER"));
        await game.Act("P1", new PlayCardCommand("PRISONER", "OGN·219/298", [], Destination: destination));
        Assert.Equal(pool, game.State.RunePools["P1"]);
        Assert.Equal("P2", game.State.CardObjects["PRISONER"].OwnerId);
        Assert.Equal("P1", game.State.CardObjects["PRISONER"].ControllerId);
        Assert.Equal(generation + 2, game.State.CardObjects["PRISONER"].ObjectGeneration);
        Assert.Contains("PRISONER", destination == "BASE" ? game.State.PlayerZones["P1"].Base : game.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("PRISONER", game.State.PlayerZones["P2"].Banished);
        Assert.Null(game.State.PendingEffectPlay); Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint(); await game.Replay();
    }
}
