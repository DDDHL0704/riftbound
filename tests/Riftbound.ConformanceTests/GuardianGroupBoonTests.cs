using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class GuardianGroupBoonTests
{
    private const string Guardian = "OGN·223/298";

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position(Guardian);
        state = RadGearTestPosition.Put(state, "HERE", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "BORROWED", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "ENEMY-HERE", "OGN·219/298", "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "AWAY", "OGN·219/298", "P1", "BATTLEFIELD", "OTHER");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BORROWED"] = state.CardObjects["BORROWED"] with { OwnerId = "P2" },
            ["ENEMY-HERE"] = state.CardObjects["ENEMY-HERE"] with { OwnerId = "P1" } } };
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task OnlyABattlefieldPlayGrantsTheAdditionalBoonToFriendlyUnitsHere(string destination)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Guardian, [], Destination: destination));
        var context = Assert.Single(game.State.StackItems).UnitEvent!;
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", context.SourcePlayDestinationZone);
        Assert.All(new[] { "UNIT", "HERE", "BORROWED" }, id => Assert.Equal(0, game.State.CardObjects[id].BoonCount));
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["UNIT"].BoonCount);
        Assert.Equal(6, game.State.CardObjects["UNIT"].Power);
        foreach (var id in new[] { "HERE", "BORROWED" })
            Assert.Equal(destination == "BASE" ? 0 : 1, game.State.CardObjects[id].BoonCount);
        foreach (var id in new[] { "ALLY", "ENEMY", "ENEMY-HERE", "AWAY", "GEAR", "GRAVE" })
            Assert.Equal(0, game.State.CardObjects[id].BoonCount);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealResponseChangesHereWithoutChangingTheOriginalBattlefieldPlay(bool returned)
    {
        var response = returned ? "OGN·104/298" : "OGS·011/024";
        var game = new Driver(RadGearTestPosition.Put(Position(), "RESPONSE", response, "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("UNIT", Guardian, [], Destination: "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("RESPONSE", response, ["UNIT"]));
        await game.Top();
        Assert.Equal("BATTLEFIELD", Assert.Single(game.State.StackItems).UnitEvent!.SourcePlayDestinationZone);
        await game.Top();
        Assert.Equal(returned ? "HAND" : "BASE", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal(returned ? 0 : 1, game.State.CardObjects["UNIT"].BoonCount);
        Assert.Equal(returned ? 0 : 1, game.State.CardObjects["ALLY"].BoonCount);
        Assert.All(new[] { "HERE", "BORROWED", "ENEMY-HERE", "AWAY" }, id => Assert.Equal(0, game.State.CardObjects[id].BoonCount));
        await game.Replay();
    }

    [Theory]
    [InlineData("missing")] [InlineData("HAND")] [InlineData("MAIN_DECK")] [InlineData("unrelated")]
    public async Task RecoveryRejectsMissingInvalidOrUnrelatedSourcePlayFacts(string mutation)
    {
        var no = mutation == "unrelated" ? "OGN·182/298" : Guardian;
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
        var item = Assert.Single(game.State.StackItems);
        var changed = item.UnitEvent! with { SourcePlayDestinationZone = mutation switch {
            "missing" => null, "unrelated" => "BATTLEFIELD", _ => mutation } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [item with { UnitEvent = changed }] }));
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task ABasePlayCannotBorrowAnotherPermanentsBattlefieldPlayCondition()
    {
        var state = RadGearTestPosition.Put(VenUnitEntryBasicTests.Position(Guardian), "FIRST", Guardian, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "HERE", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("FIRST", Guardian, [], Destination: "BATTLEFIELD:BF"));
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["HERE"].BoonCount);
        Assert.Equal(0, game.State.CardObjects["ALLY"].BoonCount);
        await game.Act("P1", new PlayCardCommand("UNIT", Guardian, []));
        Assert.Equal("BASE", Assert.Single(game.State.StackItems).UnitEvent!.SourcePlayDestinationZone);
        await game.Top();
        Assert.Equal(1, game.State.CardObjects["UNIT"].BoonCount);
        Assert.Equal(0, game.State.CardObjects["ALLY"].BoonCount);
        Assert.Equal(1, game.State.CardObjects["HERE"].BoonCount);
        await game.Replay();
    }
}
