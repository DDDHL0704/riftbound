using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleWinnerPhaseBoundaryTests
{
    public BattleWinnerPhaseBoundaryTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        var engine = typeof(CoreRuleEngine).Assembly;
        output.WriteLine($"Engine={engine.Location}; SHA256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(engine.Location))).ToLowerInvariant()}; MVID={engine.ManifestModule.ModuleVersionId}; RIF={RuleInputIdentity.Current.IdentitySha256}");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PlainPlayedCombatantsUseCleanupResultControlAndCloseForEitherWinner(bool defenderWins)
    {
        var (initial, run) = await Open(defenderWins ? "defender" : "plain");
        await PassFocus(run); await Declare(run);
        var winner = defenderWins ? "P1" : "P2"; var survivor = defenderWins ? "HOST" : "ATTACKER";
        var dead = defenderWins ? "ATTACKER" : "HOST";
        Assert.Contains(survivor, run.State.PlayerZones[winner].Battlefields);
        Assert.Contains(dead, run.State.PlayerZones[defenderWins ? "P2" : "P1"].Graveyard);
        Assert.Equal(0, run.State.CardObjects[survivor].Damage); Assert.Null(run.State.PendingBattleResult);
        var events = run.Events(); var result = Assert.Single(events, e => e.Event.Kind == "BATTLE_RESULT_DETERMINED");
        var cleanup = Assert.Single(events, e => e.Event.Kind == "DAMAGE_REMOVED"); var closed = Assert.Single(events, e => e.Event.Kind == "BATTLE_CLOSED");
        Assert.True(cleanup.Sequence < result.Sequence && result.Sequence < closed.Sequence);
        Assert.DoesNotContain(events, e => e.Event.Kind == "TRIGGER_QUEUED");
        Assert.Equal(defenderWins ? 0 : 1, run.State.PlayerScores["P2"]); Assert.Equal(0, run.State.PlayerScores["P1"]);
        Assert.Equal(winner, run.State.CardObjects["HOME"].ControllerId);
        if (defenderWins) Assert.DoesNotContain(events, e => e.Event.Kind is "BATTLEFIELD_CONQUERED" or "BATTLEFIELD_HELD");
        else Assert.True(result.Sequence < Assert.Single(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED").Sequence);
        await run.Replay(); Save("plain-defender-" + defenderWins, initial, run);
    }

    [Fact]
    public async Task AnActualRayRemovingTheLastDefenderSkipsDamageAndStillUsesTheSameResultPhases()
    {
        var (initial, run) = await Open("skipped-damage");
        await run.Act("P2", new PlayCardCommand("RAY", "OGN·009/298", ["HOST"])); await run.Top(); await PassFocus(run);
        Assert.Contains("HOST", run.State.PlayerZones["P1"].Graveyard); Assert.Equal("P2", run.State.CardObjects["HOME"].ControllerId);
        Assert.Equal(1, run.State.PlayerScores["P2"]); Assert.Null(run.State.PendingBattleResult);
        Assert.DoesNotContain(run.Commands(), c => c.CommandType is CommandTypes.DeclareBattle or CommandTypes.AssignCombatDamage);
        var events = run.Events(); Assert.DoesNotContain(events, e => e.Event.Kind == "COMBAT_DAMAGE_ASSIGNED");
        var result = Assert.Single(events, e => e.Event.Kind == "BATTLE_RESULT_DETERMINED");
        var conquest = Assert.Single(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        Assert.True(result.Sequence < conquest.Sequence && conquest.Sequence < events.Single(e => e.Event.Kind == "BATTLE_CLOSED").Sequence);
        await run.Replay(); Save("actual-ray-skipped-damage", initial, run);
    }

    [Fact]
    public async Task ActualResultDrawAndZaunDiscardDrawFinishTheirSeparatePhasesExactlyOnce()
    {
        var (initial, run) = await Open("control-observer"); await PassFocus(run); await Declare(run);
        await ChooseGuardian(run);
        Assert.Equal("RESULT_TRIGGERS", run.State.PendingBattleResult!.Stage); Assert.Equal("P1", run.State.CardObjects["HOME"].ControllerId);
        Assert.Null(run.State.PendingHandChoice); await run.Top();
        Assert.Equal("CONTROL_TRIGGERS", run.State.PendingBattleResult!.Stage);
        Assert.Null(run.State.PendingHandChoice);
        Assert.Single(run.State.StackItems, item => item.EffectKind == TriggerKinds.BattlefieldConquerDiscardDraw);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind is "BATTLE_CLOSED" or "CARD_DISCARDED");
        await run.Top();
        var choice = Assert.IsType<PendingHandChoiceState>(run.State.PendingHandChoice);
        Assert.Equal("P2", run.State.CardObjects["HOME"].ControllerId); Assert.Equal(1, choice.RequiredCount);
        Assert.Contains("P2-DRAW-1", choice.LegalObjectIds); Assert.DoesNotContain(run.Events(), e => e.Event.Kind is "BATTLE_CLOSED" or "CARD_DISCARDED");
        await run.Act("P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2-DRAW-1"]));
        var events = run.Events(); Assert.Null(run.State.PendingBattleResult); Assert.Null(run.State.PendingHandChoice);
        Assert.Single(events, e => e.Event.Kind == "BATTLE_RESULT_DETERMINED"); Assert.Single(events, e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        Assert.Single(events, e => e.Event.Kind == "BATTLE_CLOSED"); Assert.Single(events, e => e.Event.Kind == "CARD_DISCARDED");
        var legend = Assert.Single(events, e => e.Event.Kind == "LEGEND_TRIGGER_RESOLVED" && Text(e.Event, "trigger") == "BATTLE_WON_DRAW_ONE");
        var conquest = events.Single(e => e.Event.Kind == "BATTLEFIELD_CONQUERED"); var discarded = events.Single(e => e.Event.Kind == "CARD_DISCARDED");
        Assert.True(legend.Sequence < conquest.Sequence && conquest.Sequence < discarded.Sequence
            && discarded.Sequence < events.Single(e => e.Event.Kind == "BATTLE_CLOSED").Sequence);
        Assert.Equal(3, events.Count(e => e.Event.Kind == "CARD_DRAWN")); Assert.Equal(1, run.State.PlayerScores["P2"]);
        await run.Replay(); Save("result-and-control-observers", initial, run);
    }

    [Fact]
    public async Task MutualOrdinaryDeathsKeepTheExistingNoWinnerBranch()
    {
        var initial = BattleWinnerPhaseScenarioTests.Build("plain");
        initial = ReplaceHand(initial, "HOST", "OGN·219/298", "P1"); initial = ReplaceHand(initial, "ATTACKER", "OGN·219/298", "P2");
        var (_, run) = await Open("plain", initial, "OGN·219/298", "OGN·219/298"); await PassFocus(run); await Declare(run);
        Assert.All(new[] { "P1", "P2" }, p => Assert.Equal(0, run.State.PlayerScores[p]));
        Assert.Contains("HOST", run.State.PlayerZones["P1"].Graveyard); Assert.Contains("ATTACKER", run.State.PlayerZones["P2"].Graveyard);
        Assert.Null(run.State.CardObjects["HOME"].ControllerId); Assert.Null(run.State.PendingBattleResult);
        Assert.Single(run.Events(), e => e.Event.Kind == "BATTLE_NO_RESULT"); Assert.Single(run.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
        // CN468.3 also determines a no-result outcome; it earns no victory or conquest.
        var outcome = Assert.Single(run.Events(), e => e.Event.Kind == "BATTLE_RESULT_DETERMINED");
        Assert.Null(Assert.IsType<BattleResultContinuation>(outcome.Event.Payload["continuation"]).WinnerPlayerId);
        Assert.DoesNotContain(run.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        await run.Replay(); Save("existing-no-winner", initial, run);
    }

    internal static async Task<(MatchState Initial, Driver Run)> Open(string mode, MatchState? position = null,
        string? hostNo = null, string? attackerNo = null)
    {
        var initial = position ?? BattleWinnerPhaseScenarioTests.Build(mode); var run = new Driver(initial);
        hostNo ??= mode == "defender" ? "OGN·142/298" : "OGN·210/298";
        attackerNo ??= mode == "defender" ? "OGN·210/298" : "OGN·142/298";
        await run.Act("P1", new PlayCardCommand("HOST", hostNo, [], Destination: "BATTLEFIELD:HOME"));
        if (mode is "guardians" or "final-conquest" or "result-burnout" or "control-observer") foreach (var gear in new[] { "ANGEL", "ANGEL-2" }) {
            await run.Act("P1", new PlayCardCommand(gear, "SFD·051/221", [], Destination: "BASE"));
            await run.Act("P1", new AssembleEquipmentCommand(gear, "HOST", ["ASSEMBLE_GREEN"])); await run.Top();
        }
        await run.Act("P1", new EndTurnCommand());
        var funding = Enumerable.Range(1, 12).Select(i => "P2-FUND-" + i).ToArray(); await run.Act("P2", new TapRuneCommand(funding[0], funding));
        await run.Act("P2", new PlayCardCommand("ATTACKER", attackerNo, [], Destination: "BASE"));
        await run.Act("P2", new PlayCardCommand("READY", "OGN·146/298", ["ATTACKER"])); await run.Top();
        if (mode == "skipped-damage") await run.Act("P2", new RecycleRuneCommand("P2-RED"));
        await run.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:HOME", [])); return (initial, run);
    }
    internal static async Task PassFocus(Driver run)
    { for (var i = 0; run.State.FocusPlayerId is not null; i++) { Assert.InRange(i, 0, 3); await run.Act(run.State.FocusPlayerId, new PassFocusCommand()); } }
    internal static Task Declare(Driver run) => run.Act("P2", new DeclareBattleCommand("HOME", ["ATTACKER"], ["HOST"], ["COMBAT_ASSIGNMENT"]));
    internal static Task ChooseGuardian(Driver run) => run.Act("P1", new PayCostCommand(run.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", ["ATTACHED_GEAR:HOST:ANGEL-2"]));
    private static MatchState ReplaceHand(MatchState s, string id, string no, string owner)
        => s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) } };
    private static string? Text(GameEvent e, string key) => e.Payload.GetValueOrDefault(key) switch {
        string text => text, JsonElement { ValueKind: JsonValueKind.String } value => value.GetString(), _ => null };
    internal static void Save(string key, MatchState initial, Driver run)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_C10_GUARDIAN_COMBAT_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, key + ".json"), JsonSerializer.Serialize(new { initial,
            final = run.State, commands = run.Commands(), events = run.Events(), scope = "Actual canonical QA command boundary; explicit initial, not natural opening, SQL/API or whole-card proof." }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
