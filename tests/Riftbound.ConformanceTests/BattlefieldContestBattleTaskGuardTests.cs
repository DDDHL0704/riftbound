using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldContestBattleTaskGuardTests
{
    [Fact]
    public void ActiveStartBattlePromptOnlyExposesCurrentBattlefieldUnitsForActivePlayer()
    {
        var state = BuildActiveStartBattleGuardState();

        Assert.Equal("BATTLE_TASKS", state.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-1", state.PendingTaskQueue.ActiveTaskId);

        var prompts = ResolutionResult.BuildPrompts(state);
        Assert.True(prompts["P1"].Actionable);
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], prompts["P1"].Actions);
        Assert.False(prompts["P2"].Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], prompts["P2"].Actions);
        var snapshot = ResolutionResult.BuildSnapshots(state)["P1"];
        AssertActiveStartBattlePromptMetadataAudit(state, snapshot, prompts["P1"], prompts["P2"]);

        var declareBattleCandidate = Assert.Single(
            prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.DeclareBattle, StringComparison.Ordinal));
        Assert.True(declareBattleCandidate.Enabled);
        var candidateSources = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(declareBattleCandidate.Sources);
        var candidateTargets = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(declareBattleCandidate.Targets);
        var candidateDestinations = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(declareBattleCandidate.Destinations);
        var candidateOptionalCosts = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(declareBattleCandidate.OptionalCosts);
        Assert.Equal(["P1-ATTACKER-VALID"], candidateSources.Select(choice => choice.Id).ToArray());
        Assert.Equal(["P2-DEFENDER-VALID"], candidateTargets.Select(choice => choice.Id).ToArray());
        Assert.Equal(["BF-1"], candidateDestinations.Select(choice => choice.Id).ToArray());
        Assert.Equal(["COMBAT_ASSIGNMENT"], candidateOptionalCosts.Select(choice => choice.Id).ToArray());
        var metadata = Assert.IsType<Dictionary<string, object?>>(declareBattleCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(sourceRequirements);

        Assert.Equal("P1-ATTACKER-VALID", sourceRequirement["sourceObjectId"]);

        var attackerChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["attackerChoicesByIndex"]);
        var defenderChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["targetChoicesByIndex"]);
        var battlefieldChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["battlefieldChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        var attackerChoiceIds = attackerChoicesByIndex
            .SelectMany(pair => pair.Value)
            .Select(choice => choice.Id)
            .ToArray();
        var defenderChoiceIds = defenderChoicesByIndex
            .SelectMany(pair => pair.Value)
            .Select(choice => choice.Id)
            .ToArray();

        Assert.Equal(["BF-1"], battlefieldChoices);
        Assert.Equal(["P1-ATTACKER-VALID"], attackerChoiceIds);
        Assert.Equal(["P2-DEFENDER-VALID"], defenderChoiceIds);

        Assert.DoesNotContain("P1-OTHER-BATTLEFIELD-UNIT", attackerChoiceIds);
        Assert.DoesNotContain("P1-BASE-UNIT", attackerChoiceIds);
        Assert.DoesNotContain("P1-STALE-UNIT", attackerChoiceIds);
        Assert.DoesNotContain("P1-FACE-DOWN-STANDBY", attackerChoiceIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-EQUIPMENT", attackerChoiceIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-SPELL", attackerChoiceIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-RUNE", attackerChoiceIds);
        Assert.DoesNotContain("P2-OTHER-BATTLEFIELD-UNIT", defenderChoiceIds);
        Assert.DoesNotContain("P2-BASE-UNIT", defenderChoiceIds);
        Assert.DoesNotContain("P2-STALE-UNIT", defenderChoiceIds);
        Assert.DoesNotContain("P2-FACE-DOWN-STANDBY", defenderChoiceIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-EQUIPMENT", defenderChoiceIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-SPELL", defenderChoiceIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-RUNE", defenderChoiceIds);
    }

    [Theory]
    [InlineData("BF-2", "P1-ATTACKER-VALID", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-OTHER-BATTLEFIELD-UNIT", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-BASE-UNIT", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-STALE-UNIT", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-FACE-DOWN-STANDBY", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-BATTLEFIELD-EQUIPMENT", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-BATTLEFIELD-SPELL", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-BATTLEFIELD-RUNE", "P2-DEFENDER-VALID")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-OTHER-BATTLEFIELD-UNIT")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-BASE-UNIT")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-STALE-UNIT")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-FACE-DOWN-STANDBY")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-BATTLEFIELD-EQUIPMENT")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-BATTLEFIELD-SPELL")]
    [InlineData("BF-1", "P1-ATTACKER-VALID", "P2-BATTLEFIELD-RUNE")]
    public async Task ActiveStartBattleRejectsInvalidDeclareBattleWithoutMutation(
        string battlefieldId,
        string attackerObjectId,
        string defenderObjectId)
    {
        var state = BuildActiveStartBattleGuardState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-invalid-start-battle-{attackerObjectId}-{defenderObjectId}", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                battlefieldId,
                [attackerObjectId],
                [defenderObjectId],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        AssertRejectedWithoutMutation(state, result);
    }

    [Fact]
    public async Task ActiveStartBattleDeclareBattleClearsTaskAndPreservesRepresentativeEvents()
    {
        var state = BuildActiveStartBattleGuardState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-valid-start-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "BF-1",
                ["P1-ATTACKER-VALID"],
                ["P2-DEFENDER-VALID"],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(10, result.State.Tick);
        Assert.NotEqual("BATTLE_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.False(result.State.BattleState.IsActive);
        Assert.False(result.State.CardObjects["P1-ATTACKER-VALID"].IsAttacking);
        Assert.Equal("P1", result.State.CardObjects["BF-1"].ControllerId);
        Assert.DoesNotContain("P2-DEFENDER-VALID", result.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("P2-DEFENDER-VALID", result.State.PlayerZones["P2"].Graveyard);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_DECLARED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-DEFENDER-VALID", StringComparison.Ordinal));
        AssertBattleClosedDeclareBattlePromptQueueAudit(result);
    }

    [Fact]
    public async Task ActiveStartBattleDeclareBattleRejectsAcceptedCommandReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var state = BuildActiveStartBattleGuardState();
        var command = new DeclareBattleCommand(
            "BF-1",
            ["P1-ATTACKER-VALID"],
            ["P2-DEFENDER-VALID"],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);

        var accepted = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-valid-start-battle-before-replay", "P1", CommandTypes.DeclareBattle),
            command,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.NotEqual("BATTLE_TASKS", accepted.State.PendingTaskQueue.Phase);
        Assert.False(accepted.State.BattleState.IsActive);
        Assert.Contains("P2-DEFENDER-VALID", accepted.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("P2-DEFENDER-VALID", accepted.State.PlayerZones["P2"].Battlefields);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await engine.ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-valid-start-battle-stale-replay", "P1", CommandTypes.DeclareBattle),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.NotEqual("BATTLE_TASKS", replay.State.PendingTaskQueue.Phase);
        Assert.False(replay.State.BattleState.IsActive);
        Assert.Contains("P2-DEFENDER-VALID", replay.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("P2-DEFENDER-VALID", replay.State.PlayerZones["P2"].Battlefields);
        Assert.False(replay.State.CardObjects["P1-ATTACKER-VALID"].IsAttacking);
        Assert.Equal("P1", replay.State.CardObjects["BF-1"].ControllerId);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, replay.Prompts["P1"].Actions);
        AssertBattleClosedDeclareBattlePromptQueueAudit(replay);
    }

    [Fact]
    public async Task DeclareBattleStalePromptReplayAfterNextSpellDuelStartsRejectsWithoutMutation()
    {
        var state = BuildImmediateBattleNextContestState(includeCleanupBlocker: false);
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new DeclareBattleCommand(
            "BF-1",
            ["P1-ATTACKER-VALID"],
            ["P2-DEFENDER-VALID"],
            OptionalCosts: ["COMBAT_ASSIGNMENT"]);

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.BattleDeclaration, prompt.View?.Type);
        Assert.Equal("BF-1", prompt.View?.RelatedBattlefieldId);
        Assert.Contains(CommandTypes.DeclareBattle, prompt.Actions);
        var staleRawCommand = PromptScopedDeclareBattleRawCommand(command, prompt);

        var accepted = await session.SubmitAsync(
            "P1",
            "intent-valid-start-battle-before-stale-prompt-replay",
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.False(accepted.State.BattleState.IsActive);
        Assert.DoesNotContain(
            accepted.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-1", StringComparison.Ordinal));
        Assert.Equal(TimingStates.SpellDuelOpen, accepted.State.TimingState);
        Assert.Equal("P1", accepted.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", accepted.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-2", accepted.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, accepted.Prompts["P1"].View?.Type);
        Assert.Equal("BF-2", accepted.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Contains(CommandTypes.PassFocus, accepted.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, accepted.Prompts["P1"].Actions);
        AssertImmediateDeclareBattleNextSpellDuelPromptQueueAudit(accepted);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await session.SubmitAsync(
            "P1",
            "intent-valid-start-battle-stale-battle-prompt-replay",
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.False(replay.State.BattleState.IsActive);
        Assert.Equal(TimingStates.SpellDuelOpen, replay.State.TimingState);
        Assert.Equal("P1", replay.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", replay.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-2", replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, replay.Prompts["P1"].View?.Type);
        Assert.Equal("BF-2", replay.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Contains(CommandTypes.PassFocus, replay.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, replay.Prompts["P1"].Actions);
        Assert.Contains("P2-DEFENDER-VALID", replay.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("P2-DEFENDER-VALID", replay.State.PlayerZones["P2"].Battlefields);
        Assert.Equal("P1", replay.State.CardObjects["BF-1"].ControllerId);
        AssertImmediateDeclareBattleNextSpellDuelPromptQueueAudit(replay);
    }

    [Fact]
    public async Task ImmediateDeclareBattleAdvancesNextContestedBattlefieldTaskAfterCurrentBattleCloses()
    {
        var state = BuildImmediateBattleNextContestState(includeCleanupBlocker: false);
        Assert.Equal("BATTLE_TASKS", state.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-1", state.PendingTaskQueue.ActiveTaskId);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-immediate-battle-advances-next-contest", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "BF-1",
                ["P1-ATTACKER-VALID"],
                ["P2-DEFENDER-VALID"],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.False(result.State.BattleState.IsActive);
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-1", StringComparison.Ordinal));
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-2", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, result.Prompts["P1"].View?.Type);
        Assert.Equal("BF-2", result.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.BattleDeclaration, result.Prompts["P1"].View?.Type);

        AssertImmediateDeclareBattleNextContestAudit(result.Events);
        AssertImmediateDeclareBattleNextSpellDuelPromptQueueAudit(result);
    }

    [Fact]
    public async Task ImmediateDeclareBattleDoesNotAdvanceNextContestedBattlefieldWhenCleanupBlocks()
    {
        var state = BuildImmediateBattleNextContestState(includeCleanupBlocker: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-immediate-battle-cleanup-blocks-next-contest", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                "BF-1",
                ["P1-ATTACKER-VALID"],
                ["P2-DEFENDER-VALID"],
                OptionalCosts: ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal("STATE_BASED_CLEANUP", result.State.PendingTaskQueue.Phase);
        Assert.Contains(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "RECALL_UNATTACHED_EQUIPMENT", StringComparison.Ordinal)
                && string.Equals(task.ObjectId, "P2-BATTLEFIELD-EQUIPMENT", StringComparison.Ordinal));

        AssertImmediateDeclareBattleCleanupBlockAudit(result);
    }

    private static void AssertRejectedWithoutMutation(MatchState state, ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(state.Tick, result.State.Tick);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Equal("BATTLE_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-1", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("P1-ATTACKER-VALID", result.State.PlayerZones["P1"].Battlefields);
        Assert.Contains("P2-DEFENDER-VALID", result.State.PlayerZones["P2"].Battlefields);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].IsFaceDown);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].CardNo);
        Assert.True(result.State.CardObjects["P2-FACE-DOWN-STANDBY"].IsFaceDown);
        Assert.Null(result.State.CardObjects["P2-FACE-DOWN-STANDBY"].CardNo);
        AssertActiveStartBattlePromptMetadataAudit(
            result.State,
            result.Snapshots["P1"],
            result.Prompts["P1"],
            result.Prompts["P2"]);
    }

    private static void AssertActiveStartBattlePromptMetadataAudit(
        MatchState state,
        SnapshotDto snapshot,
        ActionPromptDto p1Prompt,
        ActionPromptDto p2Prompt)
    {
        Assert.True(state.PendingTaskQueue.HasTasks);
        Assert.True(state.PendingTaskQueue.IsBlocking);
        Assert.Equal("BATTLE_TASKS", state.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-1", state.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            state.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Equal(
            ["cleanup:battlefield-contested:BF-1", "task:start-spell-duel:BF-1", "task:start-battle:BF-1"],
            state.PendingTaskQueue.Tasks.Select(task => task.TaskId).ToArray());
        Assert.Equal(
            ["BF-1", "BF-1", "BF-1"],
            state.PendingTaskQueue.Tasks.Select(task => task.BattlefieldObjectId!).ToArray());

        var completedSpellDuelTask = Assert.Single(
            state.BattlefieldTasks,
            task => string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal));
        Assert.Equal("COMPLETED", completedSpellDuelTask.Status);
        Assert.Equal("BATTLEFIELD_CONTESTED", completedSpellDuelTask.Reason);
        Assert.Equal("BF-1", completedSpellDuelTask.BattlefieldObjectId);

        var startBattleTask = Assert.Single(
            state.BattlefieldTasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.Equal("PENDING", startBattleTask.Status);
        Assert.Equal("SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", startBattleTask.Reason);
        Assert.Equal("BF-1", startBattleTask.BattlefieldObjectId);
        Assert.Equal(["P1", "P2"], startBattleTask.ParticipantControllerIds);
        Assert.Equal(["P1-ATTACKER-VALID", "P2-DEFENDER-VALID"], startBattleTask.ParticipantObjectIds);
        Assert.Null(startBattleTask.ActingPlayerId);
        Assert.Empty(startBattleTask.StackItemIds);

        var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
        Assert.True(Assert.IsType<bool>(queue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(queue["isBlocking"]));
        Assert.Equal("BATTLE_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-battle:BF-1", Assert.IsType<string>(queue["activeTaskId"]));
        var queueTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            queueTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["cleanup:battlefield-contested:BF-1", "task:start-spell-duel:BF-1", "task:start-battle:BF-1"],
            queueTasks.Select(task => Assert.IsType<string>(task["taskId"])).ToArray());
        Assert.Equal(
            ["BF-1", "BF-1", "BF-1"],
            queueTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());
        var queueMetadata = Assert.IsType<Dictionary<string, object?>>(queue["metadata"]);
        Assert.Equal(3, Assert.IsType<int>(queueMetadata["taskCount"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(queueMetadata["stateBasedTaskKinds"]));

        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]);
        Assert.Equal(
            ["START_SPELL_DUEL", "START_BATTLE"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["COMPLETED", "PENDING"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["status"])).ToArray());
        Assert.Equal(
            ["BF-1", "BF-1"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());
        var snapshotStartBattleTask = Assert.Single(
            battlefieldTasks,
            task => string.Equals(task["kind"] as string, "START_BATTLE", StringComparison.Ordinal));
        Assert.Equal("battle:BF-1", Assert.IsType<string>(snapshotStartBattleTask["battleId"]));
        Assert.Equal("SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", Assert.IsType<string>(snapshotStartBattleTask["reason"]));
        Assert.Equal(["P1", "P2"], Assert.IsAssignableFrom<IReadOnlyList<string>>(snapshotStartBattleTask["participantControllerIds"]));
        Assert.Equal(["P1-ATTACKER-VALID", "P2-DEFENDER-VALID"], Assert.IsAssignableFrom<IReadOnlyList<string>>(snapshotStartBattleTask["participantObjectIds"]));

        Assert.Equal("P1", p1Prompt.PlayerId);
        Assert.True(p1Prompt.Actionable);
        Assert.Equal(state.Tick, p1Prompt.SnapshotTick);
        Assert.Equal(PromptTypes.BattleDeclaration, p1Prompt.View?.Type);
        Assert.Equal("BF-1", p1Prompt.View?.RelatedBattlefieldId);
        Assert.Equal("battle:BF-1", p1Prompt.View?.RelatedBattleId);
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], p1Prompt.Actions);

        Assert.Equal("P2", p2Prompt.PlayerId);
        Assert.False(p2Prompt.Actionable);
        Assert.Equal(state.Tick, p2Prompt.SnapshotTick);
        Assert.Equal(PromptTypes.BattleDeclaration, p2Prompt.View?.Type);
        Assert.Equal("BF-1", p2Prompt.View?.RelatedBattlefieldId);
        Assert.Equal("battle:BF-1", p2Prompt.View?.RelatedBattleId);
        Assert.Equal(["WAIT", "SURRENDER"], p2Prompt.Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, p2Prompt.Actions);

        var promptJson = JsonSerializer.Serialize(p1Prompt);
        Assert.DoesNotContain("P1-FACE-DOWN-STANDBY", promptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("P2-FACE-DOWN-STANDBY", promptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("P2-BASE-UNIT", promptJson, StringComparison.Ordinal);
    }

    private static void AssertBattleClosedDeclareBattlePromptQueueAudit(ResolutionResult result)
    {
        Assert.NotEqual("BATTLE_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.State.BattlefieldTasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-1", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.State.BattlefieldTasks,
            task => string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-1", StringComparison.Ordinal));

        var p1SnapshotQueue = Assert.IsType<Dictionary<string, object?>>(result.Snapshots["P1"].Timing["pendingTaskQueue"]);
        Assert.NotEqual("BATTLE_TASKS", Assert.IsType<string>(p1SnapshotQueue["phase"]));
        var p1SnapshotQueueTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(p1SnapshotQueue["tasks"]);
        Assert.DoesNotContain(p1SnapshotQueueTasks, task => string.Equals(task["kind"] as string, "START_BATTLE", StringComparison.Ordinal));
        Assert.DoesNotContain(p1SnapshotQueueTasks, task => string.Equals(task["kind"] as string, "START_SPELL_DUEL", StringComparison.Ordinal));

        var p1BattlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(result.Snapshots["P1"].Timing["battlefieldTasks"]);
        Assert.DoesNotContain(p1BattlefieldTasks, task => string.Equals(task["battlefieldObjectId"] as string, "BF-1", StringComparison.Ordinal));

        Assert.False(result.State.BattleState.IsActive);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, result.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, result.Prompts["P2"].Actions);
        Assert.NotEqual(PromptTypes.BattleDeclaration, result.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, result.Prompts["P2"].View?.Type);

        var p1PromptJson = JsonSerializer.Serialize(result.Prompts["P1"]);
        Assert.DoesNotContain("P2-DEFENDER-VALID", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("task:start-battle:BF-1", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("battle:BF-1", p1PromptJson, StringComparison.Ordinal);
    }

    private static MatchState BuildActiveStartBattleGuardState()
    {
        return new MatchState(
            roomId: "battlefield-contest-battle-task-guard-room",
            tick: 9,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BASE-UNIT"],
                    Battlefields =
                    [
                        "BF-1",
                        "BF-2",
                        "P1-ATTACKER-VALID",
                        "P1-OTHER-BATTLEFIELD-UNIT",
                        "P1-FACE-DOWN-STANDBY",
                        "P1-BATTLEFIELD-EQUIPMENT",
                        "P1-BATTLEFIELD-SPELL",
                        "P1-BATTLEFIELD-RUNE"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT"],
                    Battlefields =
                    [
                        "P2-DEFENDER-VALID",
                        "BF-3",
                        "P2-OTHER-BATTLEFIELD-UNIT",
                        "P2-FACE-DOWN-STANDBY",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE"
                    ]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: BuildCardObjects(),
            untilEndOfTurnEffects: [BattlefieldTaskMarkers.SpellDuelCompleted("BF-1")],
            objectLocations: BuildObjectLocations());
    }

    private static MatchState BuildImmediateBattleNextContestState(bool includeCleanupBlocker)
    {
        var state = BuildActiveStartBattleGuardState();
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal);
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal);
        var objectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
        {
            ["P2-OTHER-BATTLEFIELD-UNIT"] = new("P2", "BATTLEFIELD", "BF-2")
        };
        if (!includeCleanupBlocker)
        {
            playerZones["P2"] = state.PlayerZones["P2"] with
            {
                Battlefields = state.PlayerZones["P2"].Battlefields
                    .Where(objectId => !string.Equals(objectId, "P2-BATTLEFIELD-EQUIPMENT", StringComparison.Ordinal))
                    .ToArray()
            };
            cardObjects.Remove("P2-BATTLEFIELD-EQUIPMENT");
            objectLocations.Remove("P2-BATTLEFIELD-EQUIPMENT");
        }

        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };
    }

    private static Dictionary<string, CardObjectState> BuildCardObjects()
    {
        return new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["BF-1"] = Battlefield("BF-1", "P1"),
            ["BF-2"] = Battlefield("BF-2", "P1"),
            ["BF-3"] = Battlefield("BF-3", "P2"),
            ["P1-ATTACKER-VALID"] = Unit("P1-ATTACKER-VALID", "P1", power: 4),
            ["P1-OTHER-BATTLEFIELD-UNIT"] = Unit("P1-OTHER-BATTLEFIELD-UNIT", "P1"),
            ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT", "P1"),
            ["P1-FACE-DOWN-STANDBY"] = HiddenStandbyUnit("P1-FACE-DOWN-STANDBY", "P1"),
            ["P1-BATTLEFIELD-EQUIPMENT"] = NonUnit(
                "P1-BATTLEFIELD-EQUIPMENT",
                "SFD·139/221",
                CardObjectTags.EquipmentCard,
                "P1",
                attachedToObjectId: "P1-ATTACKER-VALID"),
            ["P1-BATTLEFIELD-SPELL"] = NonUnit("P1-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P1"),
            ["P1-BATTLEFIELD-RUNE"] = NonUnit("P1-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P1"),
            ["P2-DEFENDER-VALID"] = Unit("P2-DEFENDER-VALID", "P2", power: 2),
            ["P2-OTHER-BATTLEFIELD-UNIT"] = Unit("P2-OTHER-BATTLEFIELD-UNIT", "P2"),
            ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", "P2"),
            ["P2-FACE-DOWN-STANDBY"] = HiddenStandbyUnit("P2-FACE-DOWN-STANDBY", "P2"),
            ["P2-BATTLEFIELD-EQUIPMENT"] = NonUnit(
                "P2-BATTLEFIELD-EQUIPMENT",
                "SFD·139/221",
                CardObjectTags.EquipmentCard,
                "P2",
                attachedToObjectId: "P2-DEFENDER-VALID"),
            ["P2-BATTLEFIELD-SPELL"] = NonUnit("P2-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P2"),
            ["P2-BATTLEFIELD-RUNE"] = NonUnit("P2-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P2")
        };
    }

    private static Dictionary<string, ObjectLocationState> BuildObjectLocations()
    {
        return new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["BF-2"] = new("P1", "BATTLEFIELD", "BF-2"),
            ["BF-3"] = new("P2", "BATTLEFIELD", "BF-3"),
            ["P1-ATTACKER-VALID"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["P1-OTHER-BATTLEFIELD-UNIT"] = new("P1", "BATTLEFIELD", "BF-2"),
            ["P1-BASE-UNIT"] = new("P1", "BASE"),
            ["P1-FACE-DOWN-STANDBY"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["P1-BATTLEFIELD-EQUIPMENT"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["P1-BATTLEFIELD-SPELL"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["P1-BATTLEFIELD-RUNE"] = new("P1", "BATTLEFIELD", "BF-1"),
            ["P2-DEFENDER-VALID"] = new("P2", "BATTLEFIELD", "BF-1"),
            ["P2-OTHER-BATTLEFIELD-UNIT"] = new("P2", "BATTLEFIELD", "BF-3"),
            ["P2-BASE-UNIT"] = new("P2", "BASE"),
            ["P2-FACE-DOWN-STANDBY"] = new("P2", "BATTLEFIELD", "BF-3"),
            ["P2-BATTLEFIELD-EQUIPMENT"] = new("P2", "BATTLEFIELD", "BF-1"),
            ["P2-BATTLEFIELD-SPELL"] = new("P2", "BATTLEFIELD", "BF-1"),
            ["P2-BATTLEFIELD-RUNE"] = new("P2", "BATTLEFIELD", "BF-1")
        };
    }

    private static CardObjectState Battlefield(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·275/298",
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int power = 2)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
            power: power,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState HiddenStandbyUnit(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: null,
            isFaceDown: true,
            tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState NonUnit(
        string objectId,
        string cardNo,
        string tag,
        string playerId,
        string attachedToObjectId = "")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: 1,
            tags: [tag],
            ownerId: playerId,
            controllerId: playerId,
            attachedToObjectId: attachedToObjectId);
    }

    private static int EventIndex(IReadOnlyList<GameEvent> events, Predicate<GameEvent> predicate)
    {
        for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            if (predicate(events[eventIndex]))
            {
                return eventIndex;
            }
        }

        Assert.Fail("Expected event was not emitted.");
        return -1;
    }

    private static void AssertImmediateDeclareBattleNextContestAudit(IReadOnlyList<GameEvent> events)
    {
        var battleClosedIndex = EventIndex(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, "BF-1", StringComparison.Ordinal));
        var battleClosed = events[battleClosedIndex];
        Assert.Equal(["P1-ATTACKER-VALID", "P2-DEFENDER-VALID"], StringList(battleClosed.Payload["participantObjectIds"]));
        Assert.Equal(["P1-ATTACKER-VALID"], StringList(battleClosed.Payload["clearedObjectIds"]));
        Assert.Equal(["P2-DEFENDER-VALID"], StringList(battleClosed.Payload["removedObjectIds"]));

        var controlIndex = EventIndex(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-1", StringComparison.Ordinal));
        var controlResolved = events[controlIndex];
        Assert.Equal("P1", controlResolved.Payload["playerId"]);
        Assert.Equal("BF-1", controlResolved.Payload["battlefieldId"]);
        Assert.Equal("P1", controlResolved.Payload["previousControllerId"]);
        Assert.Equal("P1", controlResolved.Payload["controllerId"]);
        Assert.Equal(false, controlResolved.Payload["changed"]);
        Assert.Equal("CONTROL_CONFIRMED", controlResolved.Payload["resolution"]);
        Assert.Equal("P1", controlResolved.Payload["battleWinnerPlayerId"]);
        Assert.Equal(["P1"], StringList(controlResolved.Payload["occupantControllerIds"]));

        var nextContestIndex = EventIndex(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-2", StringComparison.Ordinal));
        var nextContest = events[nextContestIndex];
        Assert.Equal("P1", nextContest.Payload["playerId"]);
        Assert.Equal("P1", nextContest.Payload["causedByPlayerId"]);
        Assert.Equal(["P1", "P2"], StringList(nextContest.Payload["participantControllerIds"]));
        Assert.Equal(["P1-OTHER-BATTLEFIELD-UNIT", "P2-OTHER-BATTLEFIELD-UNIT"], StringList(nextContest.Payload["participantObjectIds"]));

        var nextSpellDuelIndex = EventIndex(events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-2", StringComparison.Ordinal));
        var nextSpellDuel = events[nextSpellDuelIndex];
        Assert.Equal("task:start-spell-duel:BF-2", nextSpellDuel.Payload["taskId"]);
        Assert.Equal("BATTLEFIELD_CONTESTED", nextSpellDuel.Payload["reason"]);
        Assert.Equal("P1", nextSpellDuel.Payload["playerId"]);
        Assert.Equal("P1", nextSpellDuel.Payload["focusPlayerId"]);
        Assert.Equal("P1", nextSpellDuel.Payload["causedByPlayerId"]);
        Assert.Equal(["P1", "P2"], StringList(nextSpellDuel.Payload["participantControllerIds"]));
        Assert.Equal(["P1-OTHER-BATTLEFIELD-UNIT", "P2-OTHER-BATTLEFIELD-UNIT"], StringList(nextSpellDuel.Payload["participantObjectIds"]));

        Assert.True(battleClosedIndex < controlIndex);
        Assert.True(controlIndex < nextContestIndex);
        Assert.True(nextContestIndex < nextSpellDuelIndex);
    }

    private static void AssertImmediateDeclareBattleNextSpellDuelPromptQueueAudit(ResolutionResult result)
    {
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-2", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Equal(
            ["BF-2", "BF-2", "BF-2"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.BattlefieldObjectId!).ToArray());
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.BattlefieldObjectId, "BF-1", StringComparison.Ordinal));

        var activeSpellDuelTask = Assert.Single(
            result.State.BattlefieldTasks,
            task => string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-2", StringComparison.Ordinal));
        Assert.Equal("ACTIVE", activeSpellDuelTask.Status);
        Assert.Equal("BATTLEFIELD_CONTESTED", activeSpellDuelTask.Reason);
        Assert.Equal("P1", activeSpellDuelTask.ActingPlayerId);
        Assert.Equal(["P1", "P2"], activeSpellDuelTask.ParticipantControllerIds);
        Assert.Equal(["P1-OTHER-BATTLEFIELD-UNIT", "P2-OTHER-BATTLEFIELD-UNIT"], activeSpellDuelTask.ParticipantObjectIds);

        var waitingBattleTask = Assert.Single(
            result.State.BattlefieldTasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-2", StringComparison.Ordinal));
        Assert.Equal("WAITING_FOR_SPELL_DUEL", waitingBattleTask.Status);
        Assert.Equal("SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", waitingBattleTask.Reason);
        Assert.DoesNotContain(
            result.State.BattlefieldTasks,
            task => string.Equals(task.BattlefieldObjectId, "BF-1", StringComparison.Ordinal));

        var queue = Assert.IsType<Dictionary<string, object?>>(result.Snapshots["P1"].Timing["pendingTaskQueue"]);
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.True(Assert.IsType<bool>(queue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(queue["isBlocking"]));
        Assert.Equal("task:start-spell-duel:BF-2", Assert.IsType<string>(queue["activeTaskId"]));
        var queueTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            queueTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["BF-2", "BF-2", "BF-2"],
            queueTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());

        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(result.Snapshots["P1"].Timing["battlefieldTasks"]);
        Assert.Equal(
            ["START_SPELL_DUEL", "START_BATTLE"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["ACTIVE", "WAITING_FOR_SPELL_DUEL"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["status"])).ToArray());
        Assert.Equal(
            ["BF-2", "BF-2"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());

        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(PromptTypes.SpellDuelFocus, result.Prompts["P1"].View?.Type);
        Assert.Equal("BF-2", result.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-2", result.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, result.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, result.Prompts["P1"].Actions);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, result.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P2"].Actions);

        var p1PromptJson = JsonSerializer.Serialize(result.Prompts["P1"]);
        Assert.DoesNotContain("task:start-battle:BF-1", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("battle:BF-1", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("P2-DEFENDER-VALID", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, p1PromptJson, StringComparison.Ordinal);
    }

    private static void AssertImmediateDeclareBattleCleanupBlockAudit(ResolutionResult result)
    {
        var events = result.Events;
        var destroyedIndex = EventIndex(events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-DEFENDER-VALID", StringComparison.Ordinal));
        var destroyed = events[destroyedIndex];
        Assert.Equal("P2", destroyed.Payload["ownerPlayerId"]);
        Assert.Equal("P1", destroyed.Payload["destroyedByPlayerId"]);
        Assert.Equal("GRAVEYARD", destroyed.Payload["destinationZone"]);
        Assert.Equal(["P2-BATTLEFIELD-EQUIPMENT"], StringList(destroyed.Payload["detachedEquipmentObjectIds"]));

        var battleClosedIndex = EventIndex(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldId"] as string, "BF-1", StringComparison.Ordinal));
        var battleClosed = events[battleClosedIndex];
        Assert.Equal(["P1-ATTACKER-VALID", "P2-DEFENDER-VALID"], StringList(battleClosed.Payload["participantObjectIds"]));
        Assert.Equal(["P1-ATTACKER-VALID"], StringList(battleClosed.Payload["clearedObjectIds"]));
        Assert.Equal(["P2-DEFENDER-VALID"], StringList(battleClosed.Payload["removedObjectIds"]));

        var controlIndex = EventIndex(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTROL_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-1", StringComparison.Ordinal));
        var controlResolved = events[controlIndex];
        Assert.Equal("P1", controlResolved.Payload["playerId"]);
        Assert.Equal("BF-1", controlResolved.Payload["battlefieldId"]);
        Assert.Equal("P1", controlResolved.Payload["previousControllerId"]);
        Assert.Equal("P1", controlResolved.Payload["controllerId"]);
        Assert.Equal(false, controlResolved.Payload["changed"]);
        Assert.Equal("CONTROL_CONFIRMED", controlResolved.Payload["resolution"]);
        Assert.Equal("P1", controlResolved.Payload["battleWinnerPlayerId"]);
        Assert.Equal(["P1"], StringList(controlResolved.Payload["occupantControllerIds"]));

        Assert.True(destroyedIndex < battleClosedIndex);
        Assert.True(battleClosedIndex < controlIndex);
        Assert.DoesNotContain(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-2", StringComparison.Ordinal));
        Assert.DoesNotContain(events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-2", StringComparison.Ordinal));

        Assert.False(result.State.BattleState.IsActive);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.True(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("STATE_BASED_CLEANUP", result.State.PendingTaskQueue.Phase);
        Assert.Equal("cleanup:unattached-equipment:BF-1:P2-BATTLEFIELD-EQUIPMENT", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["RECALL_UNATTACHED_EQUIPMENT", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Equal(
            [
                "cleanup:unattached-equipment:BF-1:P2-BATTLEFIELD-EQUIPMENT",
                "cleanup:battlefield-contested:BF-2",
                "task:start-spell-duel:BF-2",
                "task:start-battle:BF-2"
            ],
            result.State.PendingTaskQueue.Tasks.Select(task => task.TaskId).ToArray());

        var cleanupTask = Assert.Single(result.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "RECALL_UNATTACHED_EQUIPMENT", StringComparison.Ordinal)
            && string.Equals(task.ObjectId, "P2-BATTLEFIELD-EQUIPMENT", StringComparison.Ordinal));
        Assert.Equal("UNATTACHED_EQUIPMENT_CLEANUP", cleanupTask.Reason);
        Assert.Equal("P2", cleanupTask.PlayerId);
        Assert.Equal("BF-1", cleanupTask.BattlefieldObjectId);
        Assert.Null(result.State.CardObjects["P2-BATTLEFIELD-EQUIPMENT"].AttachedToObjectId);
        Assert.Contains("P2-BATTLEFIELD-EQUIPMENT", result.State.PlayerZones["P2"].Battlefields);

        var nextSpellDuelTask = Assert.Single(result.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
            && string.Equals(task.BattlefieldObjectId, "BF-2", StringComparison.Ordinal));
        Assert.Equal("task:start-spell-duel:BF-2", nextSpellDuelTask.TaskId);
        Assert.Equal("BATTLEFIELD_CONTESTED", nextSpellDuelTask.Reason);
        Assert.DoesNotContain(result.State.BattlefieldTasks, task =>
            string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
            && string.Equals(task.BattlefieldObjectId, "BF-2", StringComparison.Ordinal)
            && string.Equals(task.Status, "ACTIVE", StringComparison.Ordinal));
        Assert.DoesNotContain(result.State.BattlefieldTasks, task =>
            string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
            && string.Equals(task.BattlefieldObjectId, "BF-2", StringComparison.Ordinal)
            && string.Equals(task.Status, "ACTIVE", StringComparison.Ordinal));

        var queue = Assert.IsType<Dictionary<string, object?>>(result.Snapshots["P1"].Timing["pendingTaskQueue"]);
        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(queue["phase"]));
        Assert.True(Assert.IsType<bool>(queue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(queue["isBlocking"]));
        Assert.Equal("cleanup:unattached-equipment:BF-1:P2-BATTLEFIELD-EQUIPMENT", Assert.IsType<string>(queue["activeTaskId"]));
        var queueTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]);
        Assert.Equal(
            ["RECALL_UNATTACHED_EQUIPMENT", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            queueTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            [
                "cleanup:unattached-equipment:BF-1:P2-BATTLEFIELD-EQUIPMENT",
                "cleanup:battlefield-contested:BF-2",
                "task:start-spell-duel:BF-2",
                "task:start-battle:BF-2"
            ],
            queueTasks.Select(task => Assert.IsType<string>(task["taskId"])).ToArray());
        Assert.Equal(
            ["BF-1", "BF-2", "BF-2", "BF-2"],
            queueTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());
        var queueMetadata = Assert.IsType<Dictionary<string, object?>>(queue["metadata"]);
        Assert.Equal(4, Assert.IsType<int>(queueMetadata["taskCount"]));
        Assert.Equal(
            ["RECALL_UNATTACHED_EQUIPMENT"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(queueMetadata["stateBasedTaskKinds"]));

        Assert.False(result.Prompts["P1"].Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], result.Prompts["P1"].Actions);
        Assert.Equal(PromptTypes.TaskQueue, result.Prompts["P1"].View?.Type);
        Assert.Equal("BF-1", result.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.NotEqual(PromptTypes.SpellDuelFocus, result.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.BattleDeclaration, result.Prompts["P1"].View?.Type);
        Assert.Contains("装备清理", result.Prompts["P1"].Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, result.Prompts["P1"].Actions);

        Assert.False(result.Prompts["P2"].Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], result.Prompts["P2"].Actions);
        Assert.Equal(PromptTypes.TaskQueue, result.Prompts["P2"].View?.Type);
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, result.Prompts["P2"].Actions);

        var p1PromptJson = JsonSerializer.Serialize(result.Prompts["P1"]);
        Assert.DoesNotContain("RECALL_UNATTACHED_EQUIPMENT", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("UNATTACHED_EQUIPMENT_CLEANUP", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:unattached-equipment", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("P2-BATTLEFIELD-EQUIPMENT", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("spell-duel:BF-2", p1PromptJson, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> StringList(object? value)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(value);
    }

    private static JsonElement PromptScopedDeclareBattleRawCommand(
        DeclareBattleCommand command,
        ActionPromptDto prompt)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            cmdType = CommandTypes.DeclareBattle,
            battlefieldId = command.BattlefieldId,
            attackerObjectIds = command.AttackerObjectIds,
            defenderObjectIds = command.DefenderObjectIds,
            optionalCosts = command.OptionalCosts,
            battlefieldTargetObjectIds = command.BattlefieldTargetObjectIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        }));
        return document.RootElement.Clone();
    }
}
