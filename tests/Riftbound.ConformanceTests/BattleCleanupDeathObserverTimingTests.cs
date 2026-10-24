using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BattleCleanupDeathObserverTimingTests
{
    public BattleCleanupDeathObserverTimingTests(Xunit.Abstractions.ITestOutputHelper output)
        => BattleLimitedTestSupport.Runtime(output);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutualDeathsFinishTheirOrdinaryObserverBeforeControlAndBattleEnd(bool observer)
    {
        // Explicit midgame HAND opening; both combatants and the optional
        // ordinary observer are actually played. No captured death is injected.
        var initial = Opening(observer);
        var run = new Driver(initial);
        if (observer) await run.Act("P1", new PlayCardCommand("OBSERVER", "UNL-068/219", [], Destination: "BASE"));
        await run.Act("P1", new PlayCardCommand("HOST", "OGN·219/298", [], Destination: "BATTLEFIELD:HOME"));
        await run.Act("P1", new EndTurnCommand());
        var funds = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray();
        await run.Act("P2", new TapRuneCommand(funds[0], funds));
        await run.Act("P2", new PlayCardCommand("ATTACKER", "OGN·219/298", [], Destination: "BASE"));
        await run.Act("P2", new PlayCardCommand("READY", "OGN·146/298", ["ATTACKER"]));
        await run.Top();
        await run.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:HOME", []));
        await BattleLimitedTestSupport.PassFocus(run);
        await BattleLimitedTestSupport.Declare(run);
        Save(observer ? "with-observer-before-resolution" : "without-observer", initial, run);

        Assert.Contains("HOST", run.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ATTACKER", run.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(1, run.State.DeathLedger.Counts.GetValueOrDefault("P1"));
        Assert.Equal(1, run.State.DeathLedger.Counts.GetValueOrDefault("P2"));
        Assert.Equal(observer ? 1 : 0, run.Events().Count(e => e.Event.Kind == "TRIGGER_QUEUED"
            && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "OBSERVER")));
        if (observer)
        {
            // CN468.2 precedes outcome determination, control and battle end.
            // CN334-340 permits a response to this actually queued skill.
            Assert.Contains(run.State.StackItems, item => item.SourceObjectId == "OBSERVER");
            Assert.True(run.State.BattleState.IsActive);
            Assert.Equal("P1", run.State.CardObjects["HOME"].ControllerId);
            Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
            await run.Top();
            Assert.Equal(7, run.State.CardObjects["OBSERVER"].Power);
        }

        Assert.False(run.State.BattleState.IsActive);
        Assert.Null(run.State.CardObjects["HOME"].ControllerId);
        Assert.Equal(0, run.State.PlayerScores["P1"]);
        Assert.Equal(0, run.State.PlayerScores["P2"]);
        Assert.Null(run.State.WinnerPlayerId);
        Assert.Single(run.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
        await run.Replay();
        Save(observer ? "with-observer-completed" : "without-observer-completed", initial, run);
    }

    internal static MatchState Opening(bool observer)
    {
        var state = BattleLimitedTestSupport.Position("plain");
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["HOST"] = Printed("HOST", "OGN·219/298", "P1"),
            ["ATTACKER"] = Printed("ATTACKER", "OGN·219/298", "P2")
        };
        var zones = new Dictionary<string, PlayerZones>(state.PlayerZones);
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations);
        if (observer)
        {
            cards["OBSERVER"] = Printed("OBSERVER", "UNL-068/219", "P1");
            locations["OBSERVER"] = new("P1", "HAND");
            zones["P1"] = zones["P1"] with { Hand = [..zones["P1"].Hand, "OBSERVER"] };
        }
        return state with { RoomId = "C12-BATTLE-CLEANUP-" + observer, CardObjects = cards,
            PlayerZones = zones, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool>(state.RunePools)
                { ["P1"] = new(40, 0, new Dictionary<string, int>
                    { [RuneTrait.Green] = 10, [RuneTrait.Purple] = 10, [RuneTrait.Orange] = 10 }) } };
    }

    private static CardObjectState Printed(string id, string no, string player)
        => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);

    internal static void Save(string name, MatchState initial, Driver run)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_C12_EVIDENCE") is not { } root) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new
            { initial, commands = run.Commands(), events = run.Events(), final = run.State },
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
