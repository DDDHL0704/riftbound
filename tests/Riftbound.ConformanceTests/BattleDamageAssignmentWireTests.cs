using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.GodotClient;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    private static readonly JsonSerializerOptions JournalWireJson = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NaturalCombatJournalReplaysWithActualJournalSerialization(bool throughJournalJson)
    {
        var (initial, final, commands, events) = await ActualWireCombatJournal();
        var recovered = throughJournalJson ? JournalRoundTrip(events) : events;
        Assert.Contains(recovered, e => e.Event.Kind == "COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED");
        Assert.Contains(recovered, e => e.Event.Kind == "COMBAT_DAMAGE_ASSIGNED");

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initial, commands, final, new CoreRuleEngine(), default, recovered);

        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Equal(MatchStateHasher.Hash(final), replay.ReplayedStateHash);
    }

    [Theory]
    [InlineData("COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED", "damage")]
    [InlineData("COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED", "source-case")]
    [InlineData("COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED", "target")]
    [InlineData("COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED", "member-case")]
    [InlineData("COMBAT_DAMAGE_ASSIGNMENT_SUBMITTED", "missing")]
    [InlineData("COMBAT_DAMAGE_ASSIGNED", "damage")]
    [InlineData("COMBAT_DAMAGE_ASSIGNED", "target")]
    [InlineData("COMBAT_DAMAGE_ASSIGNED", "array-order")]
    [InlineData("COMBAT_DAMAGE_ASSIGNED", "array-duplicate")]
    [InlineData("COMBAT_DAMAGE_ASSIGNED", "member-case")]
    public async Task JournalWireComparisonStillRejectsChangedAssignmentEvidence(string eventKind, string mutation)
    {
        var (initial, final, commands, events) = await ActualWireCombatJournal();
        var recovered = JournalRoundTrip(events).ToArray();
        var control = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initial, commands, final, new CoreRuleEngine(), default, recovered);
        Assert.True(control.IsMatch, string.Join("; ", control.Errors));

        var index = Array.FindIndex(recovered, e => e.Event.Kind == eventKind);
        Assert.True(index >= 0);
        var node = JsonSerializer.SerializeToNode(recovered[index].Event, JournalWireJson)!.AsObject();
        var payload = node["payload"]!.AsObject();
        var assignments = payload["assignments"]!.AsArray();
        var first = assignments[0]!.AsObject();
        switch (mutation)
        {
            case "damage": first["damage"] = first["damage"]!.GetValue<int>() + 1; break;
            case "source-case": first["sourceObjectId"] = AttackerObjectId.ToLowerInvariant(); break;
            case "target": first["targetObjectId"] = BackRowDefenderObjectId; break;
            case "member-case":
                first["Damage"] = first["damage"]!.DeepClone();
                first.Remove("damage");
                break;
            case "missing": payload.Remove("assignments"); break;
            case "array-order":
                Assert.True(assignments.Count > 1);
                payload["assignments"] = new JsonArray(assignments.Reverse().Select(n => n!.DeepClone()).ToArray());
                break;
            case "array-duplicate": assignments.Add(first.DeepClone()); break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        recovered[index] = recovered[index] with { Event = node.Deserialize<GameEvent>(JournalWireJson)! };

        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(
            initial, commands, final, new CoreRuleEngine(), default, recovered);

        Assert.False(replay.IsMatch);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
        Assert.Contains(replay.Errors, error => error.Contains("payload hash", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task SingleDefenderChoosesWhichOfMultipleAttackersReceivesLethalDamage(bool reverseAttackers, bool chooseSecond)
    {
        var state = SingleDefenderWireState(includeSecondAttacker: true);
        var attackers = reverseAttackers
            ? new[] { SecondAttackerObjectId, AttackerObjectId }
            : new[] { AttackerObjectId, SecondAttackerObjectId };
        var engine = new CoreRuleEngine();
        var opened = await engine.ResolveAsync(state, new PlayerIntent("single-defender-declare", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(BattlefieldObjectId, attackers, [BulwarkDefenderObjectId], ["COMBAT_ASSIGNMENT"]), default);

        Assert.True(opened.Accepted, opened.ErrorMessage);
        Assert.True(opened.State.BattleState.IsActive);
        Assert.Equal(PromptTypes.AssignCombatDamage, opened.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(opened.Events, e => e.Kind == "DAMAGE_APPLIED");
        var attackerStep = await engine.ResolveAsync(opened.State, new PlayerIntent("single-defender-attacker", "P1", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
                [new(AttackerObjectId, BulwarkDefenderObjectId, 2), new(SecondAttackerObjectId, BulwarkDefenderObjectId, 2)]), default);
        Assert.True(attackerStep.Accepted, attackerStep.ErrorMessage);
        Assert.True(attackerStep.Prompts["P2"].Actionable);
        Assert.False(attackerStep.Prompts["P1"].Actionable);
        Assert.DoesNotContain(attackerStep.Events, e => e.Kind == "DAMAGE_APPLIED");
        var chosen = chooseSecond ? SecondAttackerObjectId : AttackerObjectId;
        var other = chooseSecond ? AttackerObjectId : SecondAttackerObjectId;
        var defenderStep = await engine.ResolveAsync(attackerStep.State, new PlayerIntent("single-defender-choice", "P2", CommandTypes.AssignCombatDamage),
            new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId,
                [new(BulwarkDefenderObjectId, chosen, 2)]), default);

        Assert.True(defenderStep.Accepted, defenderStep.ErrorMessage);
        Assert.Contains(chosen, defenderStep.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(other, defenderStep.State.PlayerZones["P1"].Battlefields);
        Assert.Contains(BulwarkDefenderObjectId, defenderStep.State.PlayerZones["P2"].Graveyard);
        Assert.Contains(defenderStep.Events, e => e.Kind == "BATTLE_CLOSED");
    }

    [Fact]
    public async Task SingleRecipientOnBothSidesRetainsNaturalAutomaticResolution()
    {
        var result = await new CoreRuleEngine().ResolveAsync(SingleDefenderWireState(includeSecondAttacker: false),
            new PlayerIntent("single-recipient-declare", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(BattlefieldObjectId, [AttackerObjectId], [BulwarkDefenderObjectId], ["COMBAT_ASSIGNMENT"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.False(result.State.BattleState.IsActive);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLE_DAMAGE_ASSIGNMENT_OPENED");
        Assert.Contains(result.Events, e => e.Kind == "BATTLE_CLOSED");
    }

    [Theory]
    [InlineData("multiple-defenders")]
    [InlineData("choose-first-attacker")]
    [InlineData("choose-second-attacker")]
    public async Task NativeCombatControlsColdRecoverDeduplicateAndReplayActualJournalWire(string branch)
    {
        var multipleDefenders = branch == "multiple-defenders";
        var initial = (multipleDefenders ? BuildNaturalStartBattleState() : SingleDefenderWireState(true)) with {
            Tick = 0, Seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" } };
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_COMBAT_ASSIGNMENT_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial, root is null ? null : Path.Combine(root, branch));
        driver.Export("explicit-fixture-initial.json", initial);
        await driver.Act("P1", new DeclareBattleCommand(BattlefieldObjectId,
            multipleDefenders ? [AttackerObjectId] : [AttackerObjectId, SecondAttackerObjectId],
            multipleDefenders ? [BulwarkDefenderObjectId, BackRowDefenderObjectId] : [BulwarkDefenderObjectId], ["COMBAT_ASSIGNMENT"]));
        var attackerAssignments = multipleDefenders
            ? new CombatDamageAssignmentDto[] { new(AttackerObjectId, BulwarkDefenderObjectId, 2), new(AttackerObjectId, BackRowDefenderObjectId, 3) }
            : [new(AttackerObjectId, BulwarkDefenderObjectId, 2), new(SecondAttackerObjectId, BulwarkDefenderObjectId, 2)];
        await driver.Act("P1", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, attackerAssignments));
        Assert.True(ResolutionResult.BuildPrompts(driver.State)["P2"].Actionable);
        var chosen = branch == "choose-second-attacker" ? SecondAttackerObjectId : AttackerObjectId;
        var defenderAssignments = multipleDefenders
            ? new CombatDamageAssignmentDto[] { new(BulwarkDefenderObjectId, AttackerObjectId, 2), new(BackRowDefenderObjectId, AttackerObjectId, 1) }
            : [new(BulwarkDefenderObjectId, chosen, 2)];
        await driver.Act("P2", new AssignCombatDamageCommand($"battle:{BattlefieldObjectId}", BattlefieldObjectId, defenderAssignments));
        Assert.False(driver.State.BattleState.IsActive);
        if (!multipleDefenders)
        {
            Assert.Contains(chosen, driver.State.PlayerZones["P1"].Graveyard);
            Assert.Contains(chosen == AttackerObjectId ? SecondAttackerObjectId : AttackerObjectId,
                driver.State.PlayerZones["P1"].Battlefields);
        }
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, JournalRoundTrip(driver.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        Assert.True(driver.NativeCommands is 0 or 2, "Only the two actual allocation controls count as native commands; fixture declaration is separate.");
        driver.Export("steps.json", new[] { "defend-step-1", "defend-step-2" });
        driver.Export("receipt.json", new { branch, nativeCommands = driver.NativeCommands, commands = driver.Commands().Length,
            events = driver.Events().Length, coldRecoveries = driver.Commands().Length, replay, finalHash = MatchStateHasher.Hash(driver.State),
            scope = "Explicit natural battle fixture, two production allocation controls, strict wire, cold duplicates and journal JSON replay; not visible paired play or a normal opening." });
    }

    private static MatchState SingleDefenderWireState(bool includeSecondAttacker)
    {
        var state = BuildNaturalStartBattleState(includeSecondAttacker: includeSecondAttacker,
            defenderObjectIds: [BulwarkDefenderObjectId]);
        var cards = state.CardObjects.Where(e => e.Key != BackRowDefenderObjectId)
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        foreach (var id in new[] { AttackerObjectId, SecondAttackerObjectId, BulwarkDefenderObjectId }.Where(cards.ContainsKey))
            cards[id] = cards[id] with { Power = 2, Tags = [CardObjectTags.UnitCard] };
        return state with { CardObjects = cards,
            ObjectLocations = state.ObjectLocations.Where(e => e.Key != BackRowDefenderObjectId)
                .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal) };
    }

    private static async Task<(MatchState Initial, MatchState Final, RecoveredCommand[] Commands, RecoveredEvent[] Events)> ActualWireCombatJournal()
    {
        var initial = BuildNaturalStartBattleState();
        var journal = new WireCombatJournal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        await SubmitWire(new Dictionary<string, object?> { ["cmdType"] = CommandTypes.DeclareBattle,
            ["battlefieldId"] = BattlefieldObjectId, ["attackerObjectIds"] = new[] { AttackerObjectId },
            ["defenderObjectIds"] = new[] { BulwarkDefenderObjectId, BackRowDefenderObjectId },
            ["optionalCosts"] = new[] { "COMBAT_ASSIGNMENT" } }, "P1", "wire-declare");
        await SubmitWire(new Dictionary<string, object?> { ["cmdType"] = CommandTypes.AssignCombatDamage,
            ["battleId"] = $"battle:{BattlefieldObjectId}", ["battlefieldId"] = BattlefieldObjectId,
            ["assignments"] = new CombatDamageAssignmentDto[] { new(AttackerObjectId, BulwarkDefenderObjectId, 2), new(AttackerObjectId, BackRowDefenderObjectId, 3) } }, "P1", "wire-attacker");
        await SubmitWire(new Dictionary<string, object?> { ["cmdType"] = CommandTypes.AssignCombatDamage,
            ["battleId"] = $"battle:{BattlefieldObjectId}", ["battlefieldId"] = BattlefieldObjectId,
            ["assignments"] = new CombatDamageAssignmentDto[] { new(BulwarkDefenderObjectId, AttackerObjectId, 2), new(BackRowDefenderObjectId, AttackerObjectId, 1) } }, "P2", "wire-defender");
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((v, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
        return (initial, journal.Entries[^1].AuthoritativeState, commands, events);

        async Task SubmitWire(Dictionary<string, object?> payload, string player, string id)
        {
            var wire = ClientCommandSerializer.Serialize(payload);
            var result = await session.SubmitAsync(player, id, GameCommandJsonMapper.Map(wire), wire, default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
    }

    private static RecoveredEvent[] JournalRoundTrip(IReadOnlyList<RecoveredEvent> events) => events
        .Select(e => e with { Event = JsonSerializer.Deserialize<GameEvent>(JsonSerializer.Serialize(e.Event, JournalWireJson), JournalWireJson)! }).ToArray();

    private sealed class WireCombatJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }
}
