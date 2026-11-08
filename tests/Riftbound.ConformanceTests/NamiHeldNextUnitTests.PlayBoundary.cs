using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class NamiHeldNextUnitTests
{
    [Fact]
    public async Task MerelyPlayingNamiDoesNotCreateHerHoldGrant()
    {
        var initial = RadGearTestPosition.Put(Position(), "HOST", Nami, "P1", "HAND") with {
            ActivePlayerId = "P1", TurnPlayerId = "P1", RunePools = new Dictionary<string, RunePool> {
                ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        var game = new Driver(initial);
        await Fund(game);
        await game.Act("P1", new PlayCardCommand("HOST", Nami, []));
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Empty(CoreRuleEngine.UnitEventTurnPlayGrants(game.State));
        Assert.DoesNotContain(game.Events(), record => record.Event.Kind == "UNIT_EVENT_TURN_PLAY_GRANT_CREATED");
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["FIRST"]));
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }
}
