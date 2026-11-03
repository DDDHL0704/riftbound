using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState BorrowedDuelPosition(string donor, string player = "P1", bool empowered = false)
    {
        var state = DeathAndDuelLifecycleTests.State();
        state = HandDiscardObserverTests.Put(state, "HOST", "OGN·111/298", player, "BASE");
        state = OfficialHoldSequenceTests.AddLegend(state, donor);
        var zones = state.PlayerZones.ToDictionary();
        zones["P1"] = zones["P1"] with { LegendZone = [] };
        zones[player] = zones[player] with { LegendZone = ["LEGEND"] };
        state = state with { PlayerZones = zones,
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new(player, "LEGEND") },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", donor, player) with { IsExhausted = true, IsEmpowered = !empowered },
                ["HOST"] = state.CardObjects["HOST"] with { IsEmpowered = empowered },
                ["D"] = state.CardObjects["D"] with { CardNo = "OGN·001/298", Power = 8, IsExhausted = true } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20), ["P2"] = new(20, 20) } };
        state = AttackDamageDistributionTests.Spell(state, "STATUS", "VEN·035", player);
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", player);
        return AttackDamageDistributionTests.Spell(state, "FLASH", "OGS·011/024", player);
    }

    private static string BorrowedDuelAbilityId(MatchState state, string player = "P1")
        => (ResolutionResult.BuildPrompts(state)[player].Candidates ?? [])
            .Where(c => c.Action == CommandTypes.ActivateAbility
                && c.Metadata?.GetValueOrDefault("sourceRequirements") is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Where(r => Equals(r.GetValueOrDefault("sourceObjectId"), "HOST")
                && ((string)r["abilityLabel"]!).Contains("法术对决中的", StringComparison.Ordinal))
            .Select(r => (string)r["abilityId"]!).Single();

    [Theory]
    [InlineData("VEN·139", false)]
    [InlineData("VEN·139", true)]
    [InlineData("VEN·189", false)]
    [InlineData("VEN·189", true)]
    [InlineData("VEN·189*", false)]
    [InlineData("VEN·189*", true)]
    public async Task BorrowedDuelMovementUsesTheBorrowersEmpowermentAndCapturesItsDuel(string donor, bool empowered)
    {
        var initial = BorrowedDuelPosition(donor, empowered: empowered);
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        var id = BorrowedDuelAbilityId(game.State);
        foreach (var targets in new[] { Array.Empty<string>(), ["D"], ["HOST"], ["BF"], ["A", "A"] })
            await RadGearTestPosition.Reject(game.State, "P1", new ActivateAbilityCommand("HOST", id, targets));
        await RadGearTestPosition.Reject(initial, "P1", new ActivateAbilityCommand("HOST", id, ["A"]));
        await RadGearTestPosition.Reject(game.State, "P2", new ActivateAbilityCommand("HOST", id, ["A"]));
        var pool = game.State.RunePools["P1"];
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, ["A"]));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(pool, game.State.RunePools["P1"]);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(new ObjectBinding("BF", game.State.CardObjects["BF"].ObjectGeneration), item.BorrowedExhaust!.DuelBattlefield);
        foreach (var field in new ObjectBinding?[] { null, new("BF", -1), new("MISSING", 0) })
            Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
                StackItems = [item with { BorrowedExhaust = item.BorrowedExhaust with { DuelBattlefield = field } }] }));
        await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["A"].Zone);
        Assert.Equal(!empowered, game.State.CardObjects["A"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, "toggle")]
    [InlineData(true, "toggle")]
    [InlineData(false, "source")]
    [InlineData(true, "source")]
    [InlineData(true, "target")]
    [InlineData(true, "flash")]
    [InlineData(true, "prohibited")]
    public async Task BorrowedDuelMovementRechecksResponsesAndKeepsTheSeparateReadyInstruction(bool empowered, string response)
    {
        var initial = BorrowedDuelPosition("VEN·139", empowered: empowered);
        if (response == "prohibited") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = initial.CardObjects["BF"] with { CardNo = "OGN·295/298" } } };
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new ActivateAbilityCommand("HOST", BorrowedDuelAbilityId(game.State), ["A"]));
        if (response == "toggle")
        {
            await game.Act("P1", new PlayCardCommand("STATUS", "VEN·035", ["HOST"], Mode: empowered ? "REMOVE_THEN_EMPOWER" : "EMPOWER_THEN_REMOVE"));
            await game.Top();
            empowered = !empowered;
        }
        if (response is "source" or "target")
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [response == "source" ? "HOST" : "A"]));
            await game.Top();
        }
        if (response == "flash")
        {
            await game.Act("P1", new PlayCardCommand("FLASH", "OGS·011/024", ["A"]));
            await game.Top();
        }
        var prefix = game.Events().Length;
        await game.Top();
        Assert.Equal(response == "target" ? "HAND" : response == "prohibited" ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["A"].Zone);
        if (response != "target") Assert.Equal(response == "flash" || !empowered, game.State.CardObjects["A"].IsExhausted);
        if (response is "target" or "flash") Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind == "UNIT_READIED");
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedDuelMovementOnTheOpponentsTurnPaysItsCostWithoutMovingOrReadying()
    {
        var game = new VenBasicFlowSpellTests.Driver(BorrowedDuelPosition("VEN·189*", "P2", true));
        await game.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand());
        Assert.Equal("P2", game.State.FocusPlayerId);
        await game.Act("P2", new ActivateAbilityCommand("HOST", BorrowedDuelAbilityId(game.State, "P2"), ["D"]));
        await game.Top();
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["D"].Zone);
        Assert.True(game.State.CardObjects["D"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedDuelMovementMayTargetTheBorrowerAfterARealReadyResponse()
    {
        var initial = AttackDamageDistributionTests.Spell(BorrowedDuelPosition("VEN·139", empowered: true), "READY", "OGN·146/298", "P1");
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["HOST"]));
        await game.Top();
        if (game.State.FocusPlayerId == "P2") await game.Act("P2", new PassFocusCommand());
        await game.Act("P1", new ActivateAbilityCommand("HOST", BorrowedDuelAbilityId(game.State), ["HOST"]));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["HOST"].Zone);
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        await game.Replay();
    }
}
