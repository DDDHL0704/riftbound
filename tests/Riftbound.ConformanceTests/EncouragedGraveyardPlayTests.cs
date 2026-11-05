using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EncouragedGraveyardPlayTests
{
    [Theory]
    [InlineData("BASE", true)]
    [InlineData("BATTLEFIELD:BF", true)]
    [InlineData("BASE", false)]
    [InlineData("BATTLEFIELD:BF", false)]
    public async Task AnotherConfirmedCardEnablesThePrintedGraveyardCost(string destination, bool resolves)
    {
        var game = new Driver(Position(graveyard: true, red: true));
        await Reject(game.State, new PlayCardCommand("UNIT", "UNL-025/219", [], Destination: destination));
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        if (!resolves)
        {
            var first = Assert.Single(game.State.StackItems).StackItemId;
            await game.Act("P1", new PassPriorityCommand());
            await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [first]));
        }
        await game.Top();
        Assert.Empty(game.State.StackItems);
        var command = new PlayCardCommand("UNIT", "UNL-025/219", [], Destination: destination);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait.GetValueOrDefault("red"));
        var beforeMana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", command);
        Assert.Equal(beforeMana - 3, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault("red"));
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.True(Assert.Single(game.State.CardPlays.Finalized, play => play.Source.ObjectId == "UNIT").OriginOutsideHands);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task GraveyardPermissionDoesNotRemoveTheRedPowerCost()
    {
        var game = new Driver(Position(graveyard: true, red: false));
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        await game.Top();
        await Reject(game.State, new PlayCardCommand("UNIT", "UNL-025/219", [], Destination: "BASE"));
        await game.Replay();
    }

    [Fact]
    public async Task HandPlayKeepsItsOrdinaryCostWithoutInspireOrRedPower()
    {
        var game = new Driver(Position(graveyard: false, red: false));
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("UNIT", "UNL-025/219", [], Destination: "BASE"));
        Assert.Equal(mana - 3, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    private static MatchState Position(bool graveyard, bool red)
    {
        var state = VenUnitEntryBasicTests.Position("UNL-025/219");
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = ["GEAR"], Battlefields = ["BF", "ALLY"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["ALLY"] = new("P1", "BATTLEFIELD", "BF") } };
        if (graveyard) state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], Graveyard = ["GRAVE", "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } };
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0, red ? new Dictionary<string, int> { ["red"] = 1 } : new Dictionary<string, int>()) } };
        state = AttackDamageDistributionTests.Spell(state, "FIRST", "OGN·048/298", "P1");
        return AttackDamageDistributionTests.Spell(state, "COUNTER", "OGN·045/298", "P2");
    }

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-graveyard-play", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
