using System.Reflection;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleWinnerPhaseBoundaryTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnActualReturnedOrReplayedRengarCannotRemainTheOldDeclaredConquestSource(bool replay)
    {
        var (initial, run) = await OpenRengarSource();
        try {
            var birth = run.State.CardObjects["ATTACKER-2"].ObjectGeneration;
            Assert.Equal("RESULT_TRIGGERS", run.State.PendingBattleResult!.Stage);
            if (run.State.PriorityPlayerId != "P2") await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
            await run.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ATTACKER-2"])); await run.Top();
            var returned = run.State.CardObjects["ATTACKER-2"].ObjectGeneration;
            Assert.True(returned > birth); Assert.Contains("ATTACKER-2", run.State.PlayerZones["P2"].Hand);
            if (replay) {
                if (run.State.PriorityPlayerId != "P2") await run.Act(run.State.PriorityPlayerId!, new PassPriorityCommand());
                await run.Act("P2", new PlayCardCommand("ATTACKER-2", "SFD·025/221", [], Destination: "BATTLEFIELD:HOME"));
                if (run.State.StackItems[^1].CardNo == "SFD·025/221") await run.Top();
                Assert.Contains("ATTACKER-2", run.State.PlayerZones["P2"].Battlefields);
                Assert.True(run.State.CardObjects["ATTACKER-2"].ObjectGeneration > returned);
            }
            Assert.Equal(birth, run.State.PendingBattleResult!.PublicObjectBindings.Single(b => b.ObjectId == "ATTACKER-2").Generation);
            await run.Top();
            Assert.Equal("ATTACKER", Assert.Single(run.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED").Event.Payload["sourceObjectId"]);
            Assert.Single(run.Events(), e => e.Event.Kind == "BATTLE_RESULT_DETERMINED"); Assert.Single(run.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
            Assert.Equal(1, run.State.PlayerScores["P2"]); await run.Replay();
        }
        finally { Save("actual-declared-source-return-replay-" + replay, initial, run); }
    }

    // Explicit representation-only selector boundaries. These inputs are never
    // installed into a live session or accepted as a recovery/action history.
    [Theory]
    [InlineData("other-battlefield")] [InlineData("current-controller-changed")]
    public async Task RepresentationOnlyForeignBattleOrChangedControllerCannotSupplyThePreferredDeclaration(string mode)
    {
        var (initial, run) = await OpenRengarSource(); var before = MatchStateHasher.Hash(run.State);
        var original = run.State.PendingBattleResult!;
        var cards = new Dictionary<string, CardObjectState>(run.State.CardObjects);
        var actor = mode == "current-controller-changed" ? "P1" : "P2";
        if (mode == "current-controller-changed") {
            foreach (var id in new[] { "ATTACKER", "ATTACKER-2" }) cards[id] = cards[id] with { ControllerId = "P1" };
            cards["HOME"] = cards["HOME"] with { ControllerId = "P2" };
        }
        var hypothetical = run.State with { CardObjects = cards, StackItems = [], TriggerQueue = [],
            PendingBattleResult = original with { Stage = "CONTROL_TRIGGERS", BattlefieldId = mode == "other-battlefield" ? "OTHER" : "HOME" } };
        var method = typeof(CoreRuleEngine).GetMethod("ResolveNonBattlefieldControlAfterSpellDuelClosed", BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = ((MatchState State, IReadOnlyList<GameEvent> Events))method.Invoke(null, [hypothetical, "HOME", true, "P2", 0])!;
        var conquest = Assert.Single(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        Assert.Equal("ATTACKER", conquest.Payload["sourceObjectId"]); Assert.Equal(actor, conquest.Payload["playerId"]);
        Assert.Equal(before, MatchStateHasher.Hash(run.State));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_C10_GUARDIAN_COMBAT_EVIDENCE") is { Length: > 0 } root) {
            Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "representation-only-source-fallback-" + mode + ".json"), JsonSerializer.Serialize(new {
                hypothetical, result, originalLiveStateUnchanged = true,
                scope = "Private selector representation boundary only; not a reachable control-change or foreign-frame recovery claim." }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
        await run.Top(); await run.Replay(); Save("representation-source-live-control-" + mode, initial, run);
    }

    private static async Task<(MatchState Initial, Driver Run)> OpenRengarSource()
    {
        var initial = ReplaceHand(BattleWinnerPhaseScenarioTests.Build("plain"), "ATTACKER", "OGN·210/298", "P2");
        foreach (var (id, no, zone) in new[] { ("ATTACKER-2", "SFD·025/221", "HAND"), ("READY-2", "OGN·146/298", "HAND"),
            ("RETURN", "OGN·104/298", "HAND"), ("P2-RED-1", "UNL-R01", "BASE"), ("P2-RED-2", "UNL-R01", "BASE"),
            ("P2-FUND-13", "UNL-R06", "BASE"), ("P2-FUND-14", "UNL-R06", "BASE"), ("P2-FUND-15", "UNL-R06", "BASE"),
            ("LEGEND", "SFD·185/221", "LEGEND"), ("P2-RETURN-RUNE", "UNL-R03", "RUNE_DECK") }) {
            var card = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P2");
            if (zone == "LEGEND") card = card with { ControllerId = "P2" };
            var zones = initial.PlayerZones["P2"];
            zones = zone switch {
                "HAND" => zones with { Hand = [..zones.Hand, id] }, "BASE" => zones with { Base = [..zones.Base, id] },
                "LEGEND" => zones with { LegendZone = [id] },
                _ => zones with { RuneDeck = [zones.RuneDeck[0], id, ..zones.RuneDeck.Skip(1)] } };
            initial = initial with {
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { [id] = card },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [id] = new("P2", zone) },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = zones } };
        }
        var run = new Driver(initial);
        await run.Act("P1", new PlayCardCommand("HOST", "OGN·210/298", [], Destination: "BATTLEFIELD:HOME")); await run.Act("P1", new EndTurnCommand());
        foreach (var batch in Enumerable.Range(1, 15).Select(i => "P2-FUND-" + i).Chunk(12)) await run.Act("P2", new TapRuneCommand(batch[0], batch));
        await run.Act("P2", new RecycleRuneCommand("P2-RED-1", ["P2-RED-1", "P2-RED-2"]));
        foreach (var (unit, ready, no) in new[] { ("ATTACKER", "READY", "OGN·210/298"), ("ATTACKER-2", "READY-2", "SFD·025/221") }) {
            await run.Act("P2", new PlayCardCommand(unit, no, [], Destination: "BASE"));
            if (run.State.StackItems.Count > 0) await run.Top();
            await run.Act("P2", new PlayCardCommand(ready, "OGN·146/298", [unit])); await run.Top();
        }
        await run.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:HOME", [], ["ATTACKER", "ATTACKER-2"])); await PassFocus(run);
        await run.Act("P2", new DeclareBattleCommand("HOME", ["ATTACKER-2", "ATTACKER"], ["HOST"], ["COMBAT_ASSIGNMENT"]));
        await run.Act("P2", new AssignCombatDamageCommand("battle:HOME", "HOME", [new("ATTACKER", "HOST", 3), new("ATTACKER-2", "HOST", 5)]));
        await run.Act("P1", new AssignCombatDamageCommand("battle:HOME", "HOME", [new("HOST", "ATTACKER", 2)]));
        Assert.Equal("RESULT_TRIGGERS", run.State.PendingBattleResult!.Stage); return (initial, run);
    }
}
