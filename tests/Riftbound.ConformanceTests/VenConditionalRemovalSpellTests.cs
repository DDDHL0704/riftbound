using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenConditionalRemovalSpellTests
{
    [Theory]
    [InlineData("RETURN_FRIENDLY", "ALLY")]
    [InlineData("REDUCE_ENEMY", "ENEMY")]
    public async Task MindTwistPaysItsPrintedCostAndResolvesOnlyTheSelectedMode(string mode, string target)
    {
        var state = Position("VEN·052");
        var command = new PlayCardCommand("UNIT", "VEN·052", [target], Mode: mode);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(1, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Blue]);
        var game = new Driver(state);
        await game.Act("P1", command);
        Assert.Equal(4, game.State.CardObjects[target].Power);
        await game.Top();
        Assert.Equal(19, game.State.RunePools["P1"].Mana);
        if (mode == "RETURN_FRIENDLY") Assert.Contains(target, game.State.PlayerZones["P1"].Hand);
        else {
            Assert.Equal(2, game.State.CardObjects[target].Power);
            await game.Act("P1", new EndTurnCommand());
            Assert.Equal(4, game.State.CardObjects[target].Power);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(0, "BANISHED")] [InlineData(3, "BANISHED")] [InlineData(4, "HAND")]
    public async Task SpiritShiftUsesTheTargetsCurrentMightAtResolution(int might, string destination)
    {
        var state = Position("VEN·106");
        state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Power = might } } };
        var game = new Driver(state);
        var command = new PlayCardCommand("UNIT", "VEN·106", ["ENEMY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.Equal(3, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Purple]);
        await game.Act("P1", command);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["ENEMY"].Zone);
        await game.Top();
        Assert.Equal(destination, game.State.ObjectLocations["ENEMY"].Zone);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
        await game.Replay();
    }

    [Fact]
    public async Task AReactionCanChangeWhichSpiritShiftRemovalInstructionWillRun()
    {
        var state = Position("VEN·106");
        state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = AttackDamageDistributionTests.Spell(state, "REDUCE", "VEN·052", "P1");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·106", ["ENEMY"]));
        await game.Act("P1", new PlayCardCommand("REDUCE", "VEN·052", ["ENEMY"], Mode: "REDUCE_ENEMY"));
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["ENEMY"].Power);
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Banished);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task BloodlettingRemovesEmpowermentBeforeTestingMightAndSupportsItsPrintedFlowCost(bool flow, bool growsWhenEmpowered)
    {
        var state = Position("VEN·127");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", growsWhenEmpowered ? "VEN·124" : "OGN·219/298", "P2")
                with { IsEmpowered = true } } };
        if (flow) state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = [], Graveyard = [..state.PlayerZones["P1"].Graveyard, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } };
        var command = new PlayCardCommand("UNIT", "VEN·127", ["ENEMY"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(flow ? 4 : 2, quote.Cost!.Mana);
        Assert.Equal(flow ? 2 : 1, quote.Cost.PowerByTrait[RuneTrait.Yellow]);
        var game = new Driver(state);
        await game.Act("P1", command);
        Assert.True(game.State.CardObjects["ENEMY"].IsEmpowered);
        await game.Top();
        Assert.False(game.State.CardObjects["ENEMY"].IsEmpowered);
        Assert.Equal(growsWhenEmpowered ? "GRAVEYARD" : "BASE", game.State.ObjectLocations["ENEMY"].Zone);
        Assert.Contains("UNIT", flow ? game.State.PlayerZones["P1"].Banished : game.State.PlayerZones["P1"].Graveyard);
        Assert.Single(game.Events(), e => e.Event.Kind == "OBJECT_EMPOWER_REMOVED");
        await game.Replay();
    }

    [Fact]
    public async Task BloodlettingStillDestroysAnUnempoweredUnitAtExactlyThreeMight()
    {
        var state = Position("VEN·127");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "VEN·124", "P2") } };
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·127", ["ENEMY"]));
        await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "OBJECT_EMPOWER_REMOVED");
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·052", "RETURN_FRIENDLY", "ENEMY")]
    [InlineData("VEN·052", "REDUCE_ENEMY", "ALLY")]
    [InlineData("VEN·052", "FORGED", "ENEMY")]
    [InlineData("VEN·052", "", "ALLY")]
    [InlineData("VEN·106", "", "ENEMY")]
    [InlineData("VEN·106", "", "GEAR")]
    [InlineData("VEN·127", "", "GEAR")]
    [InlineData("VEN·127", "", "GRAVE")]
    public async Task IllegalModesAndTargetsAreRejectedWithoutPayment(string no, string mode, string target)
        => await Reject(Position(no), new("UNIT", no, [target], Mode: mode));

    [Theory]
    [InlineData("VEN·052", "REDUCE_ENEMY")] [InlineData("VEN·106", "")] [InlineData("VEN·127", "")]
    public async Task UnpayableSpellsLeaveAllStateAndEventsUnchanged(string no, string mode)
    {
        var state = Position(no);
        state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 0) } };
        await Reject(state, new("UNIT", no, ["ENEMY"], Mode: mode));
    }

    private static MatchState Position(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 3, [RuneTrait.Purple] = 3, [RuneTrait.Yellow] = 3 }) } };
    }

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-removal", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
