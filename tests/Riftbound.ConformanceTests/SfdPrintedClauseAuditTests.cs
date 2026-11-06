using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class SfdPrintedClauseAuditTests
{
    [Fact]
    public async Task StandDefiantCanRespondToAnOpponentsSpellAndResolvesBeforeIt()
    {
        var initial = RadUnitEffectTestFixture.Place(Create(), "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = Put(Put(initial, "REACTION", "SFD·001/221", "P1", "HAND"),
            "FIRST", "OGN·048/298", "P2", "HAND") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("FIRST", "OGN·048/298", []));
        if (game.State.PriorityPlayerId != "P1")
            await game.Act("P2", new PassPriorityCommand());
        Assert.Equal("P1", game.State.PriorityPlayerId);
        await game.Act("P1", new PlayCardCommand("REACTION", "SFD·001/221", ["ALLY"]));
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Equal("REACTION", game.State.StackItems.Last().SourceObjectId);
        Assert.DoesNotContain("FIRST", game.State.PlayerZones["P2"].Graveyard);
        await game.Top();
        Assert.Contains("REACTION", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("FIRST", Assert.Single(game.State.StackItems).SourceObjectId);
        Assert.DoesNotContain("FIRST", game.State.PlayerZones["P2"].Graveyard);
        await game.Top();
        Assert.Contains("FIRST", game.State.PlayerZones["P2"].Graveyard);
        await game.Replay();
    }
}
