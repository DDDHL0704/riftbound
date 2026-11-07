using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class GuardianSelfBoonTests
{
    private const string Guardian = "OGN·223/298";

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task ThePrintedSelfBoonWaitsForItsIndependentPlaySkill(string destination)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(Guardian));
        await game.Act("P1", new PlayCardCommand("UNIT", Guardian, [], Destination: destination));
        Assert.Equal(0, game.State.CardObjects["UNIT"].BoonCount);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(Guardian, item.UnitEvent?.CardNo); Assert.Equal("PLAY", item.UnitEvent?.Timing);
        Assert.Empty(item.TargetObjectIds);
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["UNIT"].BoonCount);
        Assert.Equal(6, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealMoveKeepsTheSelfBoonButAReturnCannotBuffTheNewHandObject(bool returned)
    {
        var initial = RadGearTestPosition.Put(VenUnitEntryBasicTests.Position(Guardian), "RESPONSE",
            returned ? "OGN·104/298" : "OGS·011/024", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Guardian, [], Destination: "BATTLEFIELD:BF"));
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RESPONSE", returned ? "OGN·104/298" : "OGS·011/024", ["UNIT"]));
        await game.Top(); await game.Top();
        Assert.Contains("UNIT", returned ? game.State.PlayerZones["P1"].Hand : game.State.PlayerZones["P1"].Base);
        Assert.Equal(returned ? 0 : 1, game.State.CardObjects["UNIT"].BoonCount);
        Assert.Equal(generation + (returned ? 1 : 0), game.State.CardObjects["UNIT"].ObjectGeneration);
        await game.Replay();
    }

    [Fact]
    public async Task TheSelfBoonDoesNotLetTheClientPreselectAnUnrelatedUnit()
        => await RadGearTestPosition.Reject(VenUnitEntryBasicTests.Position(Guardian), "P1",
            new PlayCardCommand("UNIT", Guardian, ["ALLY"]));
}
