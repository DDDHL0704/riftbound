using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Explicit canonical midgame QA. All combatants and equipment are actually
// played; source expectations are CN 468/473/474/808.1.d, not replay agreement.
public sealed class GuardianCombatContinuationTests
{
    [Theory]
    [InlineData("OGN·210/298", false, false, "ordinary")]
    [InlineData("OGN·210/298", true, false, "ordinary")]
    [InlineData("OGN·210/298", true, false, "final-point")]
    [InlineData("OGN·210/298", true, true, "final-point")]
    [InlineData("SFD·148/221", true, false, "ordinary-legend")]
    [InlineData("SFD·148a/221", true, false, "ordinary-legend")]
    [InlineData("OGN·210/298", true, false, "result-burnout")]
    public async Task ActualDefenderReplacementFinishesBeforeResultTriggersControlAndNormalVictory(
        string hostNo, bool twoGuardians, bool otherHeld, string mode)
    {
        var initial = Position(hostNo, otherHeld, mode); var run = new Driver(initial);
        var beforeBattleDraws = 0;
        try {
            await run.Act("P1", new PlayCardCommand("HOST", hostNo, [], Destination: "BATTLEFIELD:HOME"));
            foreach (var gear in twoGuardians ? new[] { "ANGEL", "ANGEL-2" } : new[] { "ANGEL" }) {
                await run.Act("P1", new PlayCardCommand(gear, "SFD·051/221", [], Destination: "BASE"));
                await run.Act("P1", new AssembleEquipmentCommand(gear, "HOST", ["ASSEMBLE_GREEN"])); await run.Top();
            }
            await run.Act("P1", new EndTurnCommand());
            Assert.Equal(mode == "final-point" ? 7 : 0, run.State.PlayerScores["P2"]);
            var runes = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray();
            await run.Act("P2", new TapRuneCommand(runes[0], runes));
            await run.Act("P2", new PlayCardCommand("ATTACKER", "OGN·142/298", [], Destination: "BASE"));
            await run.Act("P2", new PlayCardCommand("READY", "OGN·146/298", ["ATTACKER"])); await run.Top();
            Assert.Equal(10, run.State.CardObjects["ATTACKER"].Power); Assert.False(run.State.CardObjects["ATTACKER"].IsExhausted);
            await run.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:HOME", []));
            for (var i = 0; run.State.FocusPlayerId is not null; i++) {
                Assert.InRange(i, 0, 3); await run.Act(run.State.FocusPlayerId, new PassFocusCommand());
            }
            Assert.True(run.State.BattleState.IsActive); Assert.Equal("P1", run.State.CardObjects["HOME"].ControllerId);
            beforeBattleDraws = run.Events().Count(e => e.Event.Kind == "CARD_DRAWN");
            var beforeDamage = run.State;
            await run.Act("P2", new DeclareBattleCommand("HOME", ["ATTACKER"], ["HOST"], ["COMBAT_ASSIGNMENT"]));
            if (twoGuardians) {
                var pending = Assert.IsType<RuleChoiceContinuation>(run.State.PendingRuleChoice);
                Assert.Equal("P1", pending.Request.PlayerId); Assert.Equal(2, pending.Request.Options.Count);
                Assert.Equal(MatchStateHasher.Hash(beforeDamage), MatchStateHasher.Hash(pending.Origin));
                Assert.Equal(0, run.State.CardObjects["HOST"].Damage); Assert.Equal("P1", run.State.CardObjects["HOME"].ControllerId);
                Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
                await run.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", ["ATTACHED_GEAR:HOST:ANGEL-2"]));
            }
            Assert.Contains("HOST", run.State.PlayerZones["P1"].Base);
            Assert.Contains(twoGuardians ? "ANGEL-2" : "ANGEL", run.State.PlayerZones["P1"].Graveyard);
            Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "UNIT_DESTROYED" && Target(e.Event) == "HOST");
            Assert.Equal(0, run.State.DeathLedger.Counts.GetValueOrDefault("P1"));
            if (mode is "ordinary-legend" or "result-burnout") {
                Assert.NotNull(run.State.PendingBattleResult); Assert.Equal("P1", run.State.CardObjects["HOME"].ControllerId);
                Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
            }
            for (var i = 0; run.State.Status == MatchStatuses.InProgress && (run.State.StackItems.Count > 0 || run.State.PendingBattleResult is not null); i++) {
                Assert.InRange(i, 0, 7); Assert.NotNull(run.State.PriorityPlayerId);
                await run.Act(run.State.PriorityPlayerId, new PassPriorityCommand());
            }
            var events = run.Events(); var recalled = Assert.Single(events, e => e.Event.Kind == "UNIT_RECALLED_TO_BASE" && Target(e.Event) == "HOST");
            var combatCleanup = Assert.Single(events, e => e.Event.Kind == "DAMAGE_REMOVED");
            Assert.Equal("HOST", recalled.Event.Payload["sourceObjectId"]); Assert.Null(run.State.PendingRuleChoice);
            Assert.True(run.State.CardObjects["HOST"].IsExhausted); Assert.Equal(0, run.State.CardObjects["HOST"].Damage);
            Assert.Equal(1, run.State.CardObjects["HOST"].ObjectGeneration);
            Assert.DoesNotContain(events, e => e.Event.Kind == "SCORE_GAINED" && e.Event.Payload.GetValueOrDefault("sourceObjectId") as string == "HOST");
            if (mode == "result-burnout") {
                Assert.True(run.State.BattleState.IsActive); Assert.NotNull(run.State.PendingBattleResult);
                Assert.Equal("P1", run.State.WinnerPlayerId); Assert.Equal(8, run.State.PlayerScores["P1"]);
                Assert.Equal(0, run.State.PlayerScores["P2"]); Assert.Equal("P1", run.State.CardObjects["HOME"].ControllerId);
                Assert.DoesNotContain(events, e => e.Event.Kind is "BATTLEFIELD_CONQUERED" or "BATTLE_CLOSED");
                Assert.Single(events, e => e.Event.Kind == "BURNOUT_APPLIED");
                Assert.True(recalled.Sequence < events.Single(e => e.Event.Kind == "MATCH_WON").Sequence);
            }
            else {
                var conquest = Assert.Single(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED"); Assert.True(recalled.Sequence < conquest.Sequence);
                Assert.True(combatCleanup.Sequence < conquest.Sequence,
                    $"CN 468.1 precedes 468.5: combat cleanup={combatCleanup.Sequence}, conquest={conquest.Sequence}");
                Assert.Equal("P2", run.State.CardObjects["HOME"].ControllerId);
                var win = mode == "final-point" && otherHeld;
                Assert.Equal(win ? "P2" : null, run.State.WinnerPlayerId);
                Assert.Equal(mode == "final-point" ? win ? 8 : 7 : 1, run.State.PlayerScores["P2"]);
                Assert.Equal(0, run.State.PlayerScores["P1"]);
                var extraDraws = mode == "ordinary-legend" || mode == "final-point" && !otherHeld ? 1 : 0;
                Assert.Equal(beforeBattleDraws + extraDraws, events.Count(e => e.Event.Kind == "CARD_DRAWN"));
                if (mode == "ordinary-legend") Assert.True(events.Last(e => e.Event.Kind == "CARD_DRAWN").Sequence < conquest.Sequence);
                if (win) Assert.DoesNotContain(events, e => e.Event.Kind == "BATTLE_CLOSED");
                else {
                    Assert.False(run.State.BattleState.IsActive); Assert.Null(run.State.PendingBattleResult);
                    Assert.True(conquest.Sequence < events.Single(e => e.Event.Kind == "BATTLE_CLOSED").Sequence);
                }
            }
            await run.Replay();
        }
        finally { Export(initial, run, hostNo, twoGuardians, otherHeld, mode, beforeBattleDraws); }
    }

    private static string? Target(GameEvent e) => e.Payload.GetValueOrDefault("targetObjectId") as string;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static MatchState Position(string hostNo, bool otherHeld, string mode)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner); locations[id] = new(owner, zone, field);
        }
        Put("HOME", "OGN·278/298", "P1", "BATTLEFIELD", "HOME"); cards["HOME"] = cards["HOME"] with { ControllerId = "P1" };
        Put("OTHER", "OGN·295/298", otherHeld ? "P2" : "P1", "BATTLEFIELD", "OTHER");
        cards["OTHER"] = cards["OTHER"] with { ControllerId = otherHeld ? "P2" : "P1" };
        if (otherHeld) Put("OTHER-KEEPER", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        Put("HOST", hostNo, "P1", "HAND"); Put("ANGEL", "SFD·051/221", "P1", "HAND"); Put("ANGEL-2", "SFD·051/221", "P1", "HAND");
        Put("ATTACKER", "OGN·142/298", "P2", "HAND"); Put("READY", "OGN·146/298", "P2", "HAND");
        var legend = mode is "ordinary-legend" or "result-burnout";
        if (legend) { Put("LEGEND", "SFD·185/221", "P2", "LEGEND"); cards["LEGEND"] = cards["LEGEND"] with { ControllerId = "P2" }; }
        var runes = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray(); foreach (var id in runes) Put(id, "UNL-R06", "P2", "BASE");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { "P1", "P2" }) {
            var deck = Enumerable.Range(1, player == "P2" && mode == "result-burnout" ? 1 : 6).Select(i => player + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { MainDeck = deck, Hand = player == "P1" ? ["HOST", "ANGEL", "ANGEL-2"] : ["ATTACKER", "READY"],
                Base = player == "P1" ? [] : runes, LegendZone = player == "P2" && legend ? ["LEGEND"] : [],
                Battlefields = player == "P1" ? otherHeld ? ["HOME"] : ["HOME", "OTHER"] : otherHeld ? ["OTHER", "OTHER-KEEPER"] : [] };
        }
        var pool = new RunePool(40, 0, new Dictionary<string, int> { [RuneTrait.Green] = 10, [RuneTrait.Purple] = 10 });
        return new MatchState("C10-GUARDIAN-COMBAT", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards, objectLocations: locations,
            playerScores: new Dictionary<string, int> { ["P1"] = mode == "result-burnout" ? 7 : 0,
                ["P2"] = mode == "final-point" ? otherHeld ? 6 : 7 : 0 },
            runePools: new Dictionary<string, RunePool> { ["P1"] = pool, ["P2"] = RunePool.Empty }, seed: 424242) with { CardPlays = new(5, [], []) };
    }
    private static void Export(MatchState initial, Driver run, string no, bool two, bool held, string mode, int beforeBattleDraws)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_C10_GUARDIAN_COMBAT_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, no.Replace('/', '_') + "-" + two + "-" + held + "-" + mode + ".json"), JsonSerializer.Serialize(new {
            initial, final = run.State, commands = run.Commands(), events = run.Events(), beforeBattleDraws,
            runtime = new { path = typeof(CoreRuleEngine).Assembly.Location, mvid = typeof(CoreRuleEngine).Assembly.ManifestModule.ModuleVersionId,
                sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(CoreRuleEngine).Assembly.Location))).ToLowerInvariant() },
            scope = "Actual played combatants and Guardians, actual assembly/readiness/movement/duel/declare/choice/result responses; explicit canonical midgame QA, not natural full game or SQL/API proof."
        }, Json));
    }
}
