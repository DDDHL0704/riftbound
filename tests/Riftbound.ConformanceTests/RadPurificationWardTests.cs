using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadPurificationWardTests
{
    private const string Ward = "RAD·039/167";

    [Fact]
    public async Task OfficialPrintingGivesAllFriendlyPublicUnitsTwoPowerImmediately()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Ward);
        Assert.Equal(39532, card.Id); Assert.Equal(4, card.Energy); Assert.Equal(2, card.ReturnEnergy);
        var state = Put(Create(), "WARD", Ward, "P1", "HAND");
        state = Put(state, "HERE", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("WARD", Ward, [], Destination: "BASE"));
        Assert.Empty(game.State.StackItems);
        foreach (var id in new[] { "ALLY", "HERE" }) Assert.Equal(state.CardObjects[id].Power + 2, Power(game.State, id));
        Assert.Equal(state.CardObjects["ENEMY"].Power, Power(game.State, "ENEMY"));
        await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task APlayedUnitKeepsTheAuraUntilTheCapturedSelfDestructionResolves(string owner)
    {
        var state = WithWard(owner: owner);
        state = Put(state, "NEW", "OGN·219/298", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("NEW", "OGN·219/298", [], Destination: "BASE"));
        Assert.Single(game.State.StackItems, i => i.SourceObjectId == "WARD");
        Assert.Contains("WARD", game.State.PlayerZones["P1"].Base);
        Assert.Equal(state.CardObjects["NEW"].Power + 2, Power(game.State, "NEW"));
        await game.Top();
        Assert.Contains("WARD", game.State.PlayerZones[owner].Graveyard);
        foreach (var id in new[] { "ALLY", "NEW" }) Assert.Equal(state.CardObjects[id].Power, Power(game.State, id));
        Assert.DoesNotContain(game.State.ContinuousEffects, e => e.SourceObjectId == "WARD");
        await game.Replay();
    }

    [Fact]
    public async Task MultipleWardsStackAndTheControllerOrdersTheirIndependentDestructions()
    {
        var state = Put(WithWard(), "WARD-2", Ward, "P1", "BASE");
        state = Put(state, "NEW", "OGN·219/298", "P1", "HAND");
        var game = new Driver(state);
        Assert.Equal(state.CardObjects["ALLY"].Power + 4, Power(game.State, "ALLY"));
        await game.Act("P1", new PlayCardCommand("NEW", "OGN·219/298", [], Destination: "BASE"));
        Assert.Equal(2, game.State.TriggerQueue.Count);
        var ids = game.State.TriggerQueue.Select(t => t.TriggerId).ToArray();
        await Reject(game.State, "P2", new OrderTriggersCommand(OrderedTriggerIds: ids));
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        await game.Top();
        Assert.Equal(state.CardObjects["ALLY"].Power + 2, Power(game.State, "ALLY"));
        await game.Top();
        Assert.Equal(state.CardObjects["ALLY"].Power, Power(game.State, "ALLY"));
        Assert.Contains("WARD", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("WARD-2", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task PlayingACreatedUnitTokenAlsoTriggersTheDestruction()
    {
        var state = Put(WithWard(), "FORGE", "OGN·212/298", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("FORGE", "OGN·212/298", [], Destination: "BASE"));
        for (var i = 0; game.State.StackItems.Count > 0; i++) { Assert.InRange(i, 0, 4); await game.Top(); }
        Assert.Contains(game.Events(), e => e.Event.Kind == "UNIT_TOKEN_CREATED");
        Assert.Contains("WARD", game.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(game.State.ContinuousEffects, e => e.SourceObjectId == "WARD");
        await game.Replay();
    }

    [Fact]
    public async Task AnOpponentsUnitDoesNotDestroyOrReceiveTheWard()
    {
        var state = WithWard() with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        state = Put(state, "NEW", "OGN·219/298", "P2", "HAND");
        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("NEW", "OGN·219/298", [], Destination: "BASE"));
        Assert.Empty(game.State.StackItems);
        Assert.Contains("WARD", game.State.PlayerZones["P1"].Base);
        Assert.Equal(state.CardObjects["NEW"].Power, Power(game.State, "NEW"));
        Assert.Equal(state.CardObjects["ALLY"].Power + 2, Power(game.State, "ALLY"));
        await game.Replay();
    }

    [Fact]
    public Task ARejectedUnitPlayCannotTriggerTheDestruction()
    {
        var state = Put(WithWard(), "NEW", "OGN·219/298", "P1", "HAND");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        return Reject(state, "P1", new PlayCardCommand("NEW", "OGN·219/298", [], Destination: "BASE"));
    }

    [Fact]
    public async Task PlayingEquipmentWithoutAUnitDoesNotTriggerTheDestruction()
    {
        var game = new Driver(Put(WithWard(), "HEAP", "OGN·182/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("HEAP", "OGN·182/298", [], Destination: "BASE"));
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Contains("WARD", game.State.PlayerZones["P1"].Base);
        Assert.Equal(game.State.CardObjects["ALLY"].Power + 2, Power(game.State, "ALLY"));
        await game.Replay();
    }

    [Fact]
    public async Task ActualRemovalOfTheEquipmentRemovesItsContinuousPower()
    {
        var game = new Driver(Put(WithWard(), "SALVAGE", "OGN·224/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("SALVAGE", "OGN·224/298", ["WARD"]));
        await game.Top();
        Assert.Contains("WARD", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(game.State.CardObjects["ALLY"].Power, Power(game.State, "ALLY"));
        await game.Replay();
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task CombatUsesTheSameStackedPowerThatTheSnapshotDisplays(int sources)
    {
        var state = WithWard();
        if (sources == 2) state = Put(state, "WARD-2", Ward, "P1", "BASE");
        state = Put(state, "ENEMY", sources == 1 ? "OGN·049/298" : "OGN·192/298", "P2", "BATTLEFIELD", "OTHER");
        Assert.True(state.CardObjects["ALLY"].Power <= state.CardObjects["ENEMY"].Power);
        Assert.True(Power(state, "ALLY") > state.CardObjects["ENEMY"].Power);
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await BattleWinnerPhaseBoundaryTests.PassFocus(game);
        await game.Act("P1", new DeclareBattleCommand("OTHER", ["ALLY"], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Battlefields);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal("P1", game.State.CardObjects["OTHER"].ControllerId);
        Assert.Contains("WARD", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    private static MatchState WithWard(string owner = "P1")
        => RadUnitEffectTestFixture.Place(Put(Create(), "WARD", Ward, owner, "BASE"), "WARD", "P1", "BASE");

    private static int Power(MatchState state, string id)
        => CurrentPowerTargetTests.Snapshot(state).GetProperty("players").GetProperty(state.CardObjects[id].ControllerId!)
            .GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
}
