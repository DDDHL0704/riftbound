using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class SfdRoamClauseAuditTests
{
    [Fact]
    public async Task PrintedLaurentBladeguardRoamsBetweenRealBattlefieldsAndReplays()
    {
        var initial = Put(Put(Create(), "ROAMER", "SFD·096/221", "P1", "HAND"),
            "READY", "RAD·010/167", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("ROAMER", "SFD·096/221", [], Destination: "BATTLEFIELD:BF"));
        Assert.Contains("游走", game.State.CardObjects["ROAMER"].Tags);
        await game.Act("P1", new PlayCardCommand("READY", "RAD·010/167", ["ROAMER"]));
        await game.Top();
        var generation = game.State.CardObjects["ROAMER"].ObjectGeneration;
        await game.Act("P1", new MoveUnitCommand("ROAMER", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]));
        Assert.Equal("OTHER", game.State.ObjectLocations["ROAMER"].BattlefieldObjectId);
        Assert.Equal(generation, game.State.CardObjects["ROAMER"].ObjectGeneration);
        Assert.True(game.State.CardObjects["ROAMER"].IsExhausted);
        Assert.Contains("游走", game.State.CardObjects["ROAMER"].Tags);
        Assert.DoesNotContain("ROAMER", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }
}
