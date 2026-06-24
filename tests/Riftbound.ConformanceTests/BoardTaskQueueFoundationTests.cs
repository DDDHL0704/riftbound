using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BoardTaskQueueFoundationTests
{
    [Fact]
    public async Task BaseToBattlefieldMoveIntoEmptyBattlefieldKeepsTaskQueueIdle()
    {
        var state = BaseMoveState(occupied: false);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-base-to-empty-battlefield", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("P1-BASE-MOVER", "BASE", "BATTLEFIELD:BF-EMPTY", []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["UNIT_MOVED_TO_BATTLEFIELD"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF-EMPTY"), result.State.ObjectLocations["P1-BASE-MOVER"]);
        Assert.True(result.State.CardObjects["P1-BASE-MOVER"].IsExhausted);
        Assert.False(result.State.BattlefieldStates["BF-EMPTY"].Contested);
        Assert.Equal(["P1"], result.State.BattlefieldStates["BF-EMPTY"].OccupantControllerIds);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        AssertBaseMoveEmptyBattlefieldAudit(result);
    }

    [Fact]
    public async Task ExhaustedMoveUnitSourceIsRejectedAndHiddenFromMainActionPrompt()
    {
        var baseState = BaseMoveState(occupied: false);
        var state = baseState with
        {
            CardObjects = baseState.CardObjects.ToDictionary(
                entry => entry.Key,
                entry => string.Equals(entry.Key, "P1-BASE-MOVER", StringComparison.Ordinal)
                    ? entry.Value with { IsExhausted = true }
                    : entry.Value,
                StringComparer.Ordinal)
        };
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        var moveCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.DoesNotContain(moveCandidate.Sources ?? [], source => string.Equals(source.Id, "P1-BASE-MOVER", StringComparison.Ordinal));
        Assert.DoesNotContain(
            (moveCandidate.Metadata as IReadOnlyDictionary<string, object?>)?["sourceRequirements"] as IEnumerable<IReadOnlyDictionary<string, object?>> ?? [],
            requirement =>
                requirement.TryGetValue("sourceObjectId", out var sourceObjectId)
                && string.Equals(sourceObjectId as string, "P1-BASE-MOVER", StringComparison.Ordinal));
        Assert.DoesNotContain(
            prompt.ObjectContexts ?? [],
            context => string.Equals(context.ObjectId, "P1-BASE-MOVER", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-exhausted-source-move", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("P1-BASE-MOVER", "BASE", "BATTLEFIELD:BF-EMPTY", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(new ObjectLocationState("P1", "BASE"), result.State.ObjectLocations["P1-BASE-MOVER"]);
        Assert.True(result.State.CardObjects["P1-BASE-MOVER"].IsExhausted);
    }

    [Fact]
    public void MainActionPromptProjectsObjectCandidateContextsFromServer()
    {
        var state = BaseMoveState(occupied: false);
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");

        Assert.True(prompt.Actionable);
        var context = Assert.Single(
            prompt.ObjectContexts ?? [],
            candidateContext => string.Equals(candidateContext.ObjectId, "P1-BASE-MOVER", StringComparison.Ordinal));
        Assert.Equal(1, context.EnabledCandidateCount);
        Assert.Equal(0, context.DisabledCandidateCount);
        var moveCandidate = Assert.Single(
            context.Candidates,
            candidate => string.Equals(candidate.Action, CommandTypes.MoveUnit, StringComparison.Ordinal));
        Assert.True(moveCandidate.Enabled);
        Assert.Equal(CommandTypes.MoveUnit, moveCandidate.CommandType);
        Assert.Contains("来源", moveCandidate.Roles);
        Assert.Contains("来源:sourceObjectId*", moveCandidate.RequiredCommandFields ?? []);
        Assert.Contains("服务端字段*", moveCandidate.RequiredCommandFields ?? []);
        Assert.Contains("位置:destination", moveCandidate.CommandFields ?? []);
    }

    [Fact]
    public async Task BaseToBattlefieldMoveIntoOccupiedEnemyBattlefieldStartsSpellDuelAfterCleanupGate()
    {
        var state = BaseMoveState(occupied: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-base-to-occupied-battlefield", "P2", CommandTypes.MoveUnit),
            new MoveUnitCommand("P2-BASE-MOVER", "BASE", "BATTLEFIELD:BF-CONTEST", []),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P2", result.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-CONTEST", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));

        AssertBaseMoveContestSpellDuelAudit(result);
    }

    [Fact]
    public async Task MoveUnitIntoOccupiedEnemyBattlefieldRejectsAcceptedCommandReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var state = BaseMoveState(occupied: true);
        var command = new MoveUnitCommand("P2-BASE-MOVER", "BASE", "BATTLEFIELD:BF-CONTEST", []);

        var accepted = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-move-unit-before-replay", "P2", CommandTypes.MoveUnit),
            command,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(TimingStates.SpellDuelOpen, accepted.State.TimingState);
        Assert.Equal("P2", accepted.State.FocusPlayerId);
        Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "BF-CONTEST"), accepted.State.ObjectLocations["P2-BASE-MOVER"]);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            accepted.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await engine.ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-board-task-move-unit-stale-replay", "P2", CommandTypes.MoveUnit),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(TimingStates.SpellDuelOpen, replay.State.TimingState);
        Assert.Equal("P2", replay.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", replay.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-CONTEST", replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "BF-CONTEST"), replay.State.ObjectLocations["P2-BASE-MOVER"]);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            replay.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.DoesNotContain(CommandTypes.MoveUnit, replay.Prompts["P2"].Actions);

        AssertMoveUnitSpellDuelPromptQueueAudit(accepted);
        AssertMoveUnitSpellDuelPromptQueueAudit(replay);
    }

    [Fact]
    public async Task MoveUnitStalePromptReplayAfterSpellDuelStartsRejectsWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BaseMoveState(occupied: true);
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new MoveUnitCommand("P2-BASE-MOVER", "BASE", "BATTLEFIELD:BF-CONTEST", []);

        var prompt = session.PromptFor("P2");
        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.MoveUnit, prompt.Actions);
        var staleRawCommand = PromptScopedMoveUnitRawCommand(command, prompt);
        var changedRawCommand = PromptScopedMoveUnitRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedIntentId = "intent-board-task-move-unit-before-stale-prompt-replay";
        const string staleIntentId = "intent-board-task-move-unit-stale-action-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P2",
            acceptedIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(TimingStates.SpellDuelOpen, accepted.State.TimingState);
        Assert.Equal("P2", accepted.State.FocusPlayerId);
        Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "BF-CONTEST"), accepted.State.ObjectLocations["P2-BASE-MOVER"]);
        Assert.Equal(
            ["UNIT_MOVED_TO_BATTLEFIELD", "BATTLEFIELD_CONTESTED", "SPELL_DUEL_STARTED"],
            accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            accepted.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Contains(CommandTypes.PassFocus, accepted.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.MoveUnit, accepted.Prompts["P2"].Actions);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        AssertMoveUnitSpellDuelPromptQueueAudit(accepted);

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P2", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.MoveUnit, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.Equal(
            ["UNIT_MOVED_TO_BATTLEFIELD", "BATTLEFIELD_CONTESTED", "SPELL_DUEL_STARTED"],
            acceptedJournalEntry.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        AssertPromptScopedMoveUnitRawCommand(acceptedJournalEntry.RawCommand.Value, command, prompt);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P2",
            staleIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(TimingStates.SpellDuelOpen, replay.State.TimingState);
        Assert.Equal("P2", replay.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", replay.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-CONTEST", replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "BF-CONTEST"), replay.State.ObjectLocations["P2-BASE-MOVER"]);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            replay.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Contains(CommandTypes.PassFocus, replay.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.MoveUnit, replay.Prompts["P2"].Actions);

        AssertMoveUnitSpellDuelPromptQueueAudit(replay);
        var replayHash = MatchStateHasher.Hash(replay.State);
        var replayPromptsHash = MatchStateHasher.HashValue(replay.Prompts);
        var replaySnapshotsHash = MatchStateHasher.HashValue(replay.Snapshots);

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P2", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.MoveUnit, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        AssertPromptScopedMoveUnitRawCommand(rejectedJournalEntry.RawCommand.Value, command, prompt);
        Assert.Equal(replayHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(replayPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(replaySnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));

        var duplicateRejected = await session.SubmitAsync(
            "P2",
            staleIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateRejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateRejected.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateRejected.ErrorMessage);
        Assert.Empty(duplicateRejected.Events);
        Assert.Equal(replayHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(replayPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(replaySnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        Assert.Equal(replay.State.Tick, duplicateRejected.State.Tick);
        AssertMoveUnitSpellDuelPromptQueueAudit(duplicateRejected);
        Assert.Equal(2, journal.Entries.Count);

        var conflict = await session.SubmitAsync(
            "P2",
            staleIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(replayHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replayPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(replaySnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        AssertMoveUnitSpellDuelPromptQueueAudit(conflict);
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StateBasedCleanupBlocksOrdinaryActionsBeforeBattlefieldTasks()
    {
        var state = CleanupPriorityContestState();

        Assert.Equal("STATE_BASED_CLEANUP", state.PendingTaskQueue.Phase);
        Assert.Equal("cleanup:lethal:P1-LETHAL-CONTESTER", state.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["DESTROY_LETHAL_UNIT", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            state.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.False(prompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], prompt.Actions);
        Assert.DoesNotContain("DESTROY_LETHAL_UNIT", prompt.Reason, StringComparison.Ordinal);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-blocked-end-turn", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
        Assert.Equal("STATE_BASED_CLEANUP", result.State.PendingTaskQueue.Phase);

        AssertStateBasedCleanupOrdinaryActionBlockAudit(state, prompt, result);
    }

    [Fact]
    public void StateBasedCleanupWaitingPromptsHideOrdinaryActionsButKeepQueueAuditSnapshots()
    {
        var state = CleanupPriorityContestState();
        var prompts = ResolutionResult.BuildPrompts(state);
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var ordinaryActions = new[]
        {
            CommandTypes.PlayCard,
            CommandTypes.ActivateAbility,
            CommandTypes.AssembleEquipment,
            CommandTypes.MoveUnit,
            CommandTypes.DeclareBattle,
            CommandTypes.HideCard,
            CommandTypes.RevealCard,
            CommandTypes.TapRune,
            CommandTypes.RecycleRune,
            CommandTypes.LegendAct,
            CommandTypes.EndTurn,
            CommandTypes.PassPriority,
            CommandTypes.PassFocus
        };

        foreach (var playerId in new[] { "P1", "P2" })
        {
            var prompt = prompts[playerId];
            Assert.Equal(playerId, prompt.PlayerId);
            Assert.False(prompt.Actionable);
            Assert.Equal(PromptTypes.TaskQueue, prompt.View?.Type);
            Assert.Equal(["WAIT", CommandTypes.Surrender], prompt.Actions);
            Assert.DoesNotContain(prompt.Actions, action => ordinaryActions.Contains(action, StringComparer.Ordinal));
            Assert.NotNull(prompt.Candidates);
            Assert.Equal(prompt.Actions, prompt.Candidates!.Select(candidate => candidate.Action).ToArray());
            Assert.DoesNotContain(
                prompt.Candidates,
                candidate => ordinaryActions.Contains(candidate.Action, StringComparer.Ordinal));

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshots[playerId].Timing["pendingTaskQueue"]);
            Assert.True(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.True(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(queue["phase"]));
            Assert.Equal("cleanup:lethal:P1-LETHAL-CONTESTER", Assert.IsType<string>(queue["activeTaskId"]));
            var queueTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]);
            Assert.Equal(
                [
                    "cleanup:lethal:P1-LETHAL-CONTESTER",
                    "cleanup:battlefield-contested:BF-CONTEST",
                    "task:start-spell-duel:BF-CONTEST",
                    "task:start-battle:BF-CONTEST"
                ],
                queueTasks.Select(task => Assert.IsType<string>(task["taskId"])).ToArray());
            Assert.Equal(
                ["DESTROY_LETHAL_UNIT", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
                queueTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
            var queueMetadata = Assert.IsType<Dictionary<string, object?>>(queue["metadata"]);
            Assert.Equal(4, Assert.IsType<int>(queueMetadata["taskCount"]));
            Assert.Equal(
                ["DESTROY_LETHAL_UNIT"],
                Assert.IsAssignableFrom<IReadOnlyList<string>>(queueMetadata["stateBasedTaskKinds"]));
        }
    }

    [Fact]
    public async Task StackResolutionMoveToBaseCanRemoveContestAndReturnQueueToIdle()
    {
        var state = StackMoveToBaseContestState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-p1-pass-priority", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(
            ["PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "UNIT_MOVED_TO_BASE"],
            result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Empty(result.State.StackItems);
        Assert.Equal(["P2-CONTEST-DEFENDER"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(new ObjectLocationState("P2", "BASE"), result.State.ObjectLocations["P2-CONTEST-DEFENDER"]);
        Assert.False(result.State.BattlefieldStates["BF-CONTEST"].Contested);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
                || string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));

        AssertStackMoveToBaseIdleAudit(result);
    }

    [Fact]
    public async Task PassPriorityRejectsAcceptedCommandReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var state = StackMoveToBaseContestState();
        var command = new PassPriorityCommand();

        var accepted = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-pass-priority-before-replay", "P1", CommandTypes.PassPriority),
            command,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(
            ["PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "UNIT_MOVED_TO_BASE"],
            accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Empty(accepted.State.StackItems);
        Assert.Empty(accepted.State.PassedPriorityPlayerIds);
        Assert.Null(accepted.State.PriorityPlayerId);
        Assert.Equal("IDLE", accepted.State.PendingTaskQueue.Phase);
        Assert.Equal(new ObjectLocationState("P2", "BASE"), accepted.State.ObjectLocations["P2-CONTEST-DEFENDER"]);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await engine.ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-board-task-pass-priority-stale-replay", "P1", CommandTypes.PassPriority),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Empty(replay.State.StackItems);
        Assert.Equal("IDLE", replay.State.PendingTaskQueue.Phase);
        Assert.Equal(new ObjectLocationState("P2", "BASE"), replay.State.ObjectLocations["P2-CONTEST-DEFENDER"]);
        Assert.DoesNotContain(CommandTypes.PassPriority, replay.Prompts["P1"].Actions);

        AssertStackPriorityResolvedIdlePromptQueueAudit(accepted);
        AssertStackPriorityResolvedIdlePromptQueueAudit(replay);
    }

    [Fact]
    public async Task StackPriorityStalePromptReplayAfterNextStackItemStartsRejectsWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = TwoStackPriorityState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.StackPriority, prompt.View?.Type);
        Assert.Equal("STACK-BATTLE-OR-FLIGHT", prompt.View?.RelatedStackItemId);
        Assert.Contains(CommandTypes.PassPriority, prompt.Actions);
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.PassPriority, prompt);
        var changedRawCommand = PromptScopedRawCommandWithClientNote(CommandTypes.PassPriority, prompt, "changed-payload");
        const string acceptedIntentId = "intent-stack-priority-first-item-before-stale-prompt-replay";
        const string staleIntentId = "intent-stack-priority-stale-first-item-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedIntentId,
            new PassPriorityCommand(),
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(
            ["PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "UNIT_MOVED_TO_BASE"],
            accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var remainingStackItem = Assert.Single(accepted.State.StackItems);
        Assert.Equal("STACK-FOLLOWUP-NOOP", remainingStackItem.StackItemId);
        Assert.Equal("P1", accepted.State.PriorityPlayerId);
        Assert.Empty(accepted.State.PassedPriorityPlayerIds);
        Assert.Equal(new ObjectLocationState("P2", "BASE"), accepted.State.ObjectLocations["P2-CONTEST-DEFENDER"]);
        Assert.Equal(PromptTypes.StackPriority, accepted.Prompts["P1"].View?.Type);
        Assert.Equal("STACK-FOLLOWUP-NOOP", accepted.Prompts["P1"].View?.RelatedStackItemId);
        Assert.Contains(CommandTypes.PassPriority, accepted.Prompts["P1"].Actions);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        AssertStackPriorityNextItemPromptQueueAudit(accepted);

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PassPriority, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.Equal(
            ["PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "UNIT_MOVED_TO_BASE"],
            acceptedJournalEntry.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        AssertPromptScopedRawCommand(acceptedJournalEntry.RawCommand.Value, CommandTypes.PassPriority, prompt);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleIntentId,
            new PassPriorityCommand(),
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        remainingStackItem = Assert.Single(replay.State.StackItems);
        Assert.Equal("STACK-FOLLOWUP-NOOP", remainingStackItem.StackItemId);
        Assert.Equal("P1", replay.State.PriorityPlayerId);
        Assert.Empty(replay.State.PassedPriorityPlayerIds);
        Assert.Equal(new ObjectLocationState("P2", "BASE"), replay.State.ObjectLocations["P2-CONTEST-DEFENDER"]);
        Assert.Equal(PromptTypes.StackPriority, replay.Prompts["P1"].View?.Type);
        Assert.Equal("STACK-FOLLOWUP-NOOP", replay.Prompts["P1"].View?.RelatedStackItemId);
        Assert.Contains(CommandTypes.PassPriority, replay.Prompts["P1"].Actions);

        AssertStackPriorityNextItemPromptQueueAudit(replay);
        var replayHash = MatchStateHasher.Hash(replay.State);
        var replayPromptsHash = MatchStateHasher.HashValue(replay.Prompts);
        var replaySnapshotsHash = MatchStateHasher.HashValue(replay.Snapshots);

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PassPriority, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        AssertPromptScopedRawCommand(rejectedJournalEntry.RawCommand.Value, CommandTypes.PassPriority, prompt);
        Assert.Equal(replayHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(replayPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(replaySnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));

        var duplicateRejected = await session.SubmitAsync(
            "P1",
            staleIntentId,
            new PassPriorityCommand(),
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateRejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateRejected.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateRejected.ErrorMessage);
        Assert.Empty(duplicateRejected.Events);
        Assert.Equal(replayHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(replayPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(replaySnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        Assert.Equal(replay.State.Tick, duplicateRejected.State.Tick);
        AssertStackPriorityNextItemPromptQueueAudit(duplicateRejected);
        Assert.Equal(2, journal.Entries.Count);

        var conflict = await session.SubmitAsync(
            "P1",
            staleIntentId,
            new PassPriorityCommand(),
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(replayHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replayPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(replaySnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        AssertStackPriorityNextItemPromptQueueAudit(conflict);
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CleanupLoopRepeatsWhenDestroyedHostCreatesUnattachedEquipmentCandidate()
    {
        var state = CleanupRepeatState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-cleanup-repeat-pass-priority", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(
            ["PRIORITY_PASSED", "STACK_ITEM_RESOLVED", "UNIT_DESTROYED", "EQUIPMENT_RECALLED_TO_BASE"],
            result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Contains("P1-LETHAL-HOST", result.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-ATTACHED-EQUIPMENT", result.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("P1-ATTACHED-EQUIPMENT", result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(new ObjectLocationState("P1", "BASE"), result.State.ObjectLocations["P1-ATTACHED-EQUIPMENT"]);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);

        AssertCleanupRepeatEquipmentRecallAudit(result);
    }

    [Fact]
    public void IllegalStandbyAndUnattachedEquipmentTasksRedactHiddenAndRawPromptDetails()
    {
        var illegalStandbyState = IllegalStandbyState();
        var opponentSnapshot = ResolutionResult.BuildSnapshots(illegalStandbyState)["P2"];
        var opponentQueue = Assert.IsType<Dictionary<string, object?>>(opponentSnapshot.Timing["pendingTaskQueue"]);
        var opponentTask = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(opponentQueue["tasks"]));

        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(opponentQueue["phase"]));
        Assert.True(Assert.IsType<bool>(opponentTask["hiddenObject"]));
        Assert.Equal("BATTLEFIELD_STANDBY", Assert.IsType<string>(opponentTask["hiddenObjectKind"]));
        Assert.DoesNotContain("P1-HIDDEN-STANDBY", Assert.IsType<string>(opponentQueue["activeTaskId"]), StringComparison.Ordinal);
        Assert.DoesNotContain("objectId", opponentTask.Keys);

        var illegalStandbyPrompt = ResolutionResult.BuildPrompts(illegalStandbyState)["P2"];
        Assert.False(illegalStandbyPrompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], illegalStandbyPrompt.Actions);
        Assert.DoesNotContain("REMOVE_ILLEGAL_STANDBY", illegalStandbyPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:", illegalStandbyPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("P1-HIDDEN-STANDBY", illegalStandbyPrompt.Reason, StringComparison.Ordinal);

        var equipmentState = UnattachedEquipmentState();
        var equipmentSnapshot = ResolutionResult.BuildSnapshots(equipmentState)["P1"];
        var equipmentPrompt = ResolutionResult.BuildPrompts(equipmentState)["P1"];
        Assert.False(equipmentPrompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], equipmentPrompt.Actions);
        Assert.Contains("装备清理", equipmentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("RECALL_UNATTACHED_EQUIPMENT", equipmentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("UNATTACHED_EQUIPMENT_CLEANUP", equipmentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:", equipmentPrompt.Reason, StringComparison.Ordinal);

        AssertIllegalStandbyAndEquipmentRedactionAudit(
            illegalStandbyState,
            opponentSnapshot,
            illegalStandbyPrompt,
            equipmentState,
            equipmentSnapshot,
            equipmentPrompt);
    }

    [Fact]
    public async Task PassFocusClosesSpellDuelAndPromotesStartBattleWithParticipantData()
    {
        var state = SpellDuelReadyToCloseState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-pass-focus-promote-battle", "P1", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["FOCUS_PASSED", "SPELL_DUEL_CLOSED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Contains(BattlefieldTaskMarkers.SpellDuelCompleted("BF-CONTEST"), result.State.UntilEndOfTurnEffects);
        Assert.Equal("BATTLE_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-CONTEST", result.State.PendingTaskQueue.ActiveTaskId);

        AssertSpellDuelPromotesBattleDeclarationAudit(result);

        Assert.Collection(
            result.State.BattlefieldTasks,
            task =>
            {
                Assert.Equal("START_SPELL_DUEL", task.Kind);
                Assert.Equal("COMPLETED", task.Status);
                Assert.Equal(["P1", "P2"], task.ParticipantControllerIds);
                Assert.Equal(["P1-CONTEST-ATTACKER", "P2-CONTEST-DEFENDER"], task.ParticipantObjectIds);
            },
            task =>
            {
                Assert.Equal("START_BATTLE", task.Kind);
                Assert.Equal("PENDING", task.Status);
                Assert.Equal(["P1", "P2"], task.ParticipantControllerIds);
                Assert.Equal(["P1-CONTEST-ATTACKER", "P2-CONTEST-DEFENDER"], task.ParticipantObjectIds);
            });
        Assert.Equal(PromptTypes.BattleDeclaration, result.Prompts["P1"].View?.Type);
        Assert.Equal("BF-CONTEST", result.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("battle:BF-CONTEST", result.Prompts["P1"].View?.RelatedBattleId);
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], result.Prompts["P1"].Actions);
        Assert.Equal(["WAIT", "SURRENDER"], result.Prompts["P2"].Actions);
    }

    [Fact]
    public async Task PassFocusClosesSpellDuelAndPromotesBattlefieldOwnerWhenMoverIsTurnPlayer()
    {
        var state = SpellDuelReadyToCloseState() with
        {
            TurnPlayerId = "P2"
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-pass-focus-promote-battle-owner", "P1", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["FOCUS_PASSED", "SPELL_DUEL_CLOSED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("BATTLE_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-CONTEST", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.BattleDeclaration, result.Prompts["P1"].View?.Type);
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], result.Prompts["P1"].Actions);
        Assert.Equal(["WAIT", "SURRENDER"], result.Prompts["P2"].Actions);
    }

    [Fact]
    public async Task PreciseRoamPreservesDestinationCasingAndQueuesOnlyDestinationContestTasks()
    {
        const string originBattlefieldObjectId = "P1-Origin-MiXeD-BF";
        const string destinationBattlefieldObjectId = "P1-Dest-MiXeD-BF";
        const string origin = $"BATTLEFIELD:{originBattlefieldObjectId}";
        const string destination = $"BATTLEFIELD:{destinationBattlefieldObjectId}";
        var state = PreciseRoamContestState(originBattlefieldObjectId, destinationBattlefieldObjectId);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-board-task-precise-roam-contest", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("P1-ROAMER-001", origin, destination, ["ROAM"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var moveEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Equal(origin, Assert.IsType<string>(moveEvent.Payload["origin"]));
        Assert.Equal(destination, Assert.IsType<string>(moveEvent.Payload["destination"]));
        Assert.Equal(
            destinationBattlefieldObjectId,
            result.State.ObjectLocations["P1-ROAMER-001"].BattlefieldObjectId);

        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{destinationBattlefieldObjectId}", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.DoesNotContain(originBattlefieldObjectId, result.State.PendingTaskQueue.ActiveTaskId, StringComparison.Ordinal);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.All(
            result.State.PendingTaskQueue.Tasks,
            task => Assert.Equal(destinationBattlefieldObjectId, task.BattlefieldObjectId));
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.BattlefieldObjectId, originBattlefieldObjectId, StringComparison.Ordinal));
        Assert.All(
            result.State.BattlefieldTasks,
            task => Assert.Equal(destinationBattlefieldObjectId, task.BattlefieldObjectId));
        Assert.Collection(
            result.State.BattlefieldTasks,
            task =>
            {
                Assert.Equal("START_SPELL_DUEL", task.Kind);
                Assert.Equal("ACTIVE", task.Status);
                Assert.Equal(["P1", "P2"], task.ParticipantControllerIds);
                Assert.Equal(["P1-ROAMER-001", "P2-DEST-DEFENDER"], task.ParticipantObjectIds);
            },
            task =>
            {
                Assert.Equal("START_BATTLE", task.Kind);
                Assert.Equal("WAITING_FOR_SPELL_DUEL", task.Status);
                Assert.Equal(["P1", "P2"], task.ParticipantControllerIds);
                Assert.Equal(["P1-ROAMER-001", "P2-DEST-DEFENDER"], task.ParticipantObjectIds);
            });

        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, destinationBattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["focusPlayerId"] as string, "P1", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent =>
            (string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
                || string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal))
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, originBattlefieldObjectId, StringComparison.Ordinal));

        AssertPreciseRoamDestinationContestAudit(result, origin, destination, originBattlefieldObjectId, destinationBattlefieldObjectId);
    }

    [Fact]
    public async Task ReconnectWithPendingCleanupTaskPreservesQueueAndOpponentRedaction()
    {
        var session = new MatchSession("board-task-reconnect-redaction-room", new CoreRuleEngine());
        session.EnsurePlayer("P1");
        var p2Join = session.EnsurePlayer("P2");
        var seed = await session.SeedScenarioAsync(
            "P1",
            "seed-board-task-reconnect-redaction",
            "battlefield-illegal-standby",
            JsonSerializer.SerializeToElement(new
            {
                cmdType = "DEV_SEED_SCENARIO",
                scenarioId = "battlefield-illegal-standby"
            }),
            CancellationToken.None);

        Assert.True(seed.Accepted, seed.ErrorMessage);
        var reconnect = session.ReconnectPlayer("P2", p2Join.ReconnectToken);
        var snapshot = session.SnapshotFor("P2");
        var prompt = session.PromptFor("P2");
        var snapshotJson = JsonSerializer.Serialize(snapshot);
        var promptJson = JsonSerializer.Serialize(prompt);
        var taskQueue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);

        AssertReconnectIllegalStandbyRedactionAudit(reconnect, p2Join.ReconnectToken, snapshot, prompt);
        Assert.DoesNotContain("P1-STANDBY-ILLEGAL-001", snapshotJson, StringComparison.Ordinal);
        Assert.DoesNotContain("P1-STANDBY-ILLEGAL-001", promptJson, StringComparison.Ordinal);
        Assert.Contains("hiddenStandbyCount", snapshotJson, StringComparison.Ordinal);
        Assert.Contains("hiddenObjectKind", snapshotJson, StringComparison.Ordinal);
        Assert.Contains("BATTLEFIELD_STANDBY", snapshotJson, StringComparison.Ordinal);
        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(taskQueue["phase"]));
        Assert.True(Assert.IsType<bool>(taskQueue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(taskQueue["isBlocking"]));
        Assert.DoesNotContain(
            "P1-STANDBY-ILLEGAL-001",
            Assert.IsType<string>(taskQueue["activeTaskId"]),
            StringComparison.Ordinal);
        var task = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(taskQueue["tasks"]));
        Assert.Equal("REMOVE_ILLEGAL_STANDBY", Assert.IsType<string>(task["kind"]));
        Assert.True(Assert.IsType<bool>(task["hiddenObject"]));
        Assert.Equal("BATTLEFIELD_STANDBY", Assert.IsType<string>(task["hiddenObjectKind"]));
        Assert.DoesNotContain("objectId", task.Keys);

        Assert.False(prompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], prompt.Actions);
        Assert.DoesNotContain("REMOVE_ILLEGAL_STANDBY", prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:illegal-standby", prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("P1-STANDBY-ILLEGAL-001", prompt.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReconnectWithPendingCleanupTaskPreservesOwnerVisibilityAndPromptRedaction()
    {
        const string hiddenStandbyObjectId = "P1-STANDBY-ILLEGAL-001";
        var session = new MatchSession("board-task-reconnect-owner-visibility-room", new CoreRuleEngine());
        var p1Join = session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var seed = await session.SeedScenarioAsync(
            "P1",
            "seed-board-task-reconnect-owner-visibility",
            "battlefield-illegal-standby",
            JsonSerializer.SerializeToElement(new
            {
                cmdType = "DEV_SEED_SCENARIO",
                scenarioId = "battlefield-illegal-standby"
            }),
            CancellationToken.None);

        Assert.True(seed.Accepted, seed.ErrorMessage);
        var reconnect = session.ReconnectPlayer("P1", p1Join.ReconnectToken);
        var snapshot = session.SnapshotFor("P1");
        var prompt = session.PromptFor("P1");
        var snapshotJson = JsonSerializer.Serialize(snapshot);
        var promptJson = JsonSerializer.Serialize(prompt);

        Assert.Equal("P1", reconnect.PlayerId);
        Assert.Equal("P1", reconnect.Seat);
        Assert.Equal(p1Join.ReconnectToken, reconnect.ReconnectToken);
        Assert.Contains(hiddenStandbyObjectId, snapshotJson, StringComparison.Ordinal);

        var battlefields = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Lanes["battlefields"]);
        var battlefield = Assert.Single(battlefields);
        Assert.Equal("P2", Assert.IsType<string>(battlefield["controllerId"]));
        var battlefieldObjectId = Assert.IsType<string>(battlefield["battlefieldObjectId"]);
        Assert.Equal([hiddenStandbyObjectId], Assert.IsAssignableFrom<IReadOnlyList<string>>(battlefield["standbyObjectIds"]));
        Assert.Equal(1, Assert.IsType<int>(battlefield["standbySlotCount"]));
        Assert.Equal(1, Assert.IsType<int>(battlefield["faceDownStandbyCount"]));
        Assert.Equal(0, Assert.IsType<int>(battlefield["hiddenStandbyCount"]));
        Assert.Equal(
            ["REMOVE_ILLEGAL_STANDBY"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(battlefield["pendingTaskKinds"]));
        var standbySlot = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(battlefield["standbySlots"]));
        Assert.True(Assert.IsType<bool>(standbySlot["visible"]));
        Assert.Equal("VISIBLE", Assert.IsType<string>(standbySlot["state"]));
        Assert.Equal(hiddenStandbyObjectId, Assert.IsType<string>(standbySlot["objectId"]));

        var taskQueue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(taskQueue["phase"]));
        Assert.True(Assert.IsType<bool>(taskQueue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(taskQueue["isBlocking"]));
        var cleanupTaskId = Assert.IsType<string>(taskQueue["activeTaskId"]);
        Assert.StartsWith("cleanup:illegal-standby:", cleanupTaskId, StringComparison.Ordinal);
        Assert.Contains(hiddenStandbyObjectId, cleanupTaskId, StringComparison.Ordinal);
        var task = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(taskQueue["tasks"]));
        Assert.Equal(cleanupTaskId, Assert.IsType<string>(task["taskId"]));
        Assert.Equal("REMOVE_ILLEGAL_STANDBY", Assert.IsType<string>(task["kind"]));
        Assert.Equal("BATTLEFIELD_CONTROL_CLEANUP", Assert.IsType<string>(task["reason"]));
        Assert.Equal("P1", Assert.IsType<string>(task["playerId"]));
        Assert.Equal(battlefieldObjectId, Assert.IsType<string>(task["battlefieldObjectId"]));
        Assert.Equal(hiddenStandbyObjectId, Assert.IsType<string>(task["objectId"]));
        Assert.DoesNotContain("hiddenObject", task.Keys);
        Assert.DoesNotContain("hiddenObjectKind", task.Keys);

        Assert.False(prompt.Actionable);
        Assert.Equal(PromptTypes.TaskQueue, prompt.View?.Type);
        Assert.Equal(["WAIT", "SURRENDER"], prompt.Actions);
        Assert.NotNull(prompt.Candidates);
        Assert.Equal(prompt.Actions, prompt.Candidates!.Select(candidate => candidate.Action).ToArray());
        Assert.Contains("待命清理", prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("REMOVE_ILLEGAL_STANDBY", promptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("BATTLEFIELD_CONTROL_CLEANUP", promptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:illegal-standby", promptJson, StringComparison.Ordinal);
        Assert.DoesNotContain(hiddenStandbyObjectId, promptJson, StringComparison.Ordinal);
    }

    private static void AssertReconnectIllegalStandbyRedactionAudit(
        PlayerSessionDto reconnect,
        string reconnectToken,
        SnapshotDto snapshot,
        ActionPromptDto prompt)
    {
        Assert.Equal("P2", reconnect.PlayerId);
        Assert.Equal("P2", reconnect.Seat);
        Assert.Equal(reconnectToken, reconnect.ReconnectToken);
        Assert.Empty(snapshot.Stack);

        var battlefields = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Lanes["battlefields"]);
        var battlefield = Assert.Single(battlefields);
        Assert.Equal("P2", Assert.IsType<string>(battlefield["controllerId"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(battlefield["occupantObjectIds"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(battlefield["standbyObjectIds"]));
        Assert.Equal(1, Assert.IsType<int>(battlefield["hiddenStandbyCount"]));
        Assert.Equal(
            ["REMOVE_ILLEGAL_STANDBY"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(battlefield["pendingTaskKinds"]));

        var taskQueue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(taskQueue["phase"]));
        Assert.True(Assert.IsType<bool>(taskQueue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(taskQueue["isBlocking"]));
        Assert.DoesNotContain(
            "P1-STANDBY-ILLEGAL-001",
            Assert.IsType<string>(taskQueue["activeTaskId"]),
            StringComparison.Ordinal);

        var task = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(taskQueue["tasks"]));
        Assert.Equal("REMOVE_ILLEGAL_STANDBY", Assert.IsType<string>(task["kind"]));
        Assert.Equal("BATTLEFIELD_CONTROL_CLEANUP", Assert.IsType<string>(task["reason"]));
        Assert.True(Assert.IsType<bool>(task["hiddenObject"]));
        Assert.Equal("BATTLEFIELD_STANDBY", Assert.IsType<string>(task["hiddenObjectKind"]));
        Assert.DoesNotContain("objectId", task.Keys);
        Assert.DoesNotContain("P1-STANDBY-ILLEGAL-001", Assert.IsType<string>(task["taskId"]), StringComparison.Ordinal);

        Assert.False(prompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], prompt.Actions);
        Assert.DoesNotContain("REMOVE_ILLEGAL_STANDBY", prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:illegal-standby", prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("P1-STANDBY-ILLEGAL-001", prompt.Reason, StringComparison.Ordinal);
    }

    private static void AssertStateBasedCleanupOrdinaryActionBlockAudit(
        MatchState state,
        ActionPromptDto prompt,
        ResolutionResult result)
    {
        Assert.Equal("STATE_BASED_CLEANUP", state.PendingTaskQueue.Phase);
        Assert.True(state.PendingTaskQueue.IsBlocking);
        Assert.Equal("cleanup:lethal:P1-LETHAL-CONTESTER", state.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            [
                "cleanup:lethal:P1-LETHAL-CONTESTER",
                "cleanup:battlefield-contested:BF-CONTEST",
                "task:start-spell-duel:BF-CONTEST",
                "task:start-battle:BF-CONTEST"
            ],
            state.PendingTaskQueue.Tasks.Select(task => task.TaskId).ToArray());
        Assert.Equal(
            ["DESTROY_LETHAL_UNIT", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            state.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());

        Assert.False(prompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], prompt.Actions);
        Assert.Contains("致命伤害清理", prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("DESTROY_LETHAL_UNIT", prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:lethal", prompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("P1-LETHAL-CONTESTER", prompt.Reason, StringComparison.Ordinal);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Equal("STATE_BASED_CLEANUP", result.State.PendingTaskQueue.Phase);
        Assert.True(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal(state.PendingTaskQueue.ActiveTaskId, result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            state.PendingTaskQueue.Tasks.Select(task => task.TaskId).ToArray(),
            result.State.PendingTaskQueue.Tasks.Select(task => task.TaskId).ToArray());
        Assert.Equal(
            state.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray(),
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Contains("致命伤害清理", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("DESTROY_LETHAL_UNIT", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:lethal", result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("P1-LETHAL-CONTESTER", result.ErrorMessage, StringComparison.Ordinal);
    }

    private static void AssertIllegalStandbyAndEquipmentRedactionAudit(
        MatchState illegalStandbyState,
        SnapshotDto opponentSnapshot,
        ActionPromptDto opponentPrompt,
        MatchState equipmentState,
        SnapshotDto equipmentSnapshot,
        ActionPromptDto equipmentPrompt)
    {
        Assert.Equal("STATE_BASED_CLEANUP", illegalStandbyState.PendingTaskQueue.Phase);
        Assert.Equal("cleanup:illegal-standby:BF-1:P1-HIDDEN-STANDBY", illegalStandbyState.PendingTaskQueue.ActiveTaskId);
        var illegalStandbyTask = Assert.Single(illegalStandbyState.PendingTaskQueue.Tasks);
        Assert.Equal("REMOVE_ILLEGAL_STANDBY", illegalStandbyTask.Kind);
        Assert.Equal("BATTLEFIELD_CONTROL_CLEANUP", illegalStandbyTask.Reason);

        var opponentBattlefields = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(opponentSnapshot.Lanes["battlefields"]);
        var opponentBattlefield = Assert.Single(opponentBattlefields);
        Assert.Equal("P2", Assert.IsType<string>(opponentBattlefield["controllerId"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(opponentBattlefield["standbyObjectIds"]));
        Assert.Equal(1, Assert.IsType<int>(opponentBattlefield["hiddenStandbyCount"]));
        Assert.Equal(
            ["REMOVE_ILLEGAL_STANDBY"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(opponentBattlefield["pendingTaskKinds"]));

        var opponentQueue = Assert.IsType<Dictionary<string, object?>>(opponentSnapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(opponentQueue["phase"]));
        Assert.True(Assert.IsType<bool>(opponentQueue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(opponentQueue["isBlocking"]));
        Assert.DoesNotContain("P1-HIDDEN-STANDBY", Assert.IsType<string>(opponentQueue["activeTaskId"]), StringComparison.Ordinal);
        var opponentTask = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(opponentQueue["tasks"]));
        Assert.Equal("REMOVE_ILLEGAL_STANDBY", Assert.IsType<string>(opponentTask["kind"]));
        Assert.Equal("BATTLEFIELD_CONTROL_CLEANUP", Assert.IsType<string>(opponentTask["reason"]));
        Assert.True(Assert.IsType<bool>(opponentTask["hiddenObject"]));
        Assert.Equal("BATTLEFIELD_STANDBY", Assert.IsType<string>(opponentTask["hiddenObjectKind"]));
        Assert.DoesNotContain("objectId", opponentTask.Keys);
        Assert.DoesNotContain("P1-HIDDEN-STANDBY", Assert.IsType<string>(opponentTask["taskId"]), StringComparison.Ordinal);

        Assert.False(opponentPrompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], opponentPrompt.Actions);
        Assert.Contains("待命清理", opponentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("REMOVE_ILLEGAL_STANDBY", opponentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:", opponentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("P1-HIDDEN-STANDBY", opponentPrompt.Reason, StringComparison.Ordinal);

        Assert.Equal("STATE_BASED_CLEANUP", equipmentState.PendingTaskQueue.Phase);
        Assert.Equal("cleanup:unattached-equipment:BF-1:P1-UNATTACHED-EQUIPMENT", equipmentState.PendingTaskQueue.ActiveTaskId);
        var equipmentStateTask = Assert.Single(equipmentState.PendingTaskQueue.Tasks);
        Assert.Equal("RECALL_UNATTACHED_EQUIPMENT", equipmentStateTask.Kind);
        Assert.Equal("UNATTACHED_EQUIPMENT_CLEANUP", equipmentStateTask.Reason);

        var equipmentQueue = Assert.IsType<Dictionary<string, object?>>(equipmentSnapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("STATE_BASED_CLEANUP", Assert.IsType<string>(equipmentQueue["phase"]));
        Assert.True(Assert.IsType<bool>(equipmentQueue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(equipmentQueue["isBlocking"]));
        Assert.Equal(
            "cleanup:unattached-equipment:BF-1:P1-UNATTACHED-EQUIPMENT",
            Assert.IsType<string>(equipmentQueue["activeTaskId"]));
        var equipmentTask = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(equipmentQueue["tasks"]));
        Assert.Equal("RECALL_UNATTACHED_EQUIPMENT", Assert.IsType<string>(equipmentTask["kind"]));
        Assert.Equal("UNATTACHED_EQUIPMENT_CLEANUP", Assert.IsType<string>(equipmentTask["reason"]));
        Assert.Equal("P1-UNATTACHED-EQUIPMENT", Assert.IsType<string>(equipmentTask["objectId"]));

        Assert.False(equipmentPrompt.Actionable);
        Assert.Equal(["WAIT", "SURRENDER"], equipmentPrompt.Actions);
        Assert.Contains("装备清理", equipmentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("RECALL_UNATTACHED_EQUIPMENT", equipmentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("UNATTACHED_EQUIPMENT_CLEANUP", equipmentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("cleanup:", equipmentPrompt.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("P1-UNATTACHED-EQUIPMENT", equipmentPrompt.Reason, StringComparison.Ordinal);
    }

    private static MatchState TwoStackPriorityState()
    {
        var state = StackMoveToBaseContestState();
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Graveyard = playerZones["P1"].Graveyard
                .Concat(["P1-FOLLOWUP-SPELL"])
                .ToArray()
        };

        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-FOLLOWUP-SPELL"] = new CardObjectState(
            "P1-FOLLOWUP-SPELL",
            cardNo: "TEST-001",
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1",
            controllerId: "P1");

        var objectLocations = state.ObjectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        objectLocations["P1-FOLLOWUP-SPELL"] = new ObjectLocationState("P1", "GRAVEYARD");

        return state with
        {
            RoomId = "board-task-stack-priority-stale-prompt-next-stack",
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations,
            StackItems =
            [
                new StackItemState(
                    "STACK-FOLLOWUP-NOOP",
                    "P1",
                    "P1-FOLLOWUP-SPELL",
                    "TEST_RESOLVE",
                    "TEST-001",
                    []),
                state.StackItems.Single()
            ]
        };
    }

    private static MatchState BaseMoveState(bool occupied)
    {
        var p1Battlefields = occupied
            ? new[] { "BF-CONTEST", "P1-CONTEST-DEFENDER" }
            : ["BF-EMPTY"];
        var p2Base = occupied ? new[] { "P2-BASE-MOVER" } : [];
        var p1Base = occupied ? [] : new[] { "P1-BASE-MOVER" };
        var activePlayerId = occupied ? "P2" : "P1";

        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [occupied ? "BF-CONTEST" : "BF-EMPTY"] = Battlefield(occupied ? "BF-CONTEST" : "BF-EMPTY", "P1"),
            ["P1-BASE-MOVER"] = Unit("P1-BASE-MOVER", "P1"),
            ["P2-BASE-MOVER"] = Unit("P2-BASE-MOVER", "P2"),
            ["P1-CONTEST-DEFENDER"] = Unit("P1-CONTEST-DEFENDER", "P1")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [occupied ? "BF-CONTEST" : "BF-EMPTY"] = new("P1", "BATTLEFIELD", occupied ? "BF-CONTEST" : "BF-EMPTY"),
            ["P1-BASE-MOVER"] = new("P1", "BASE"),
            ["P2-BASE-MOVER"] = new("P2", "BASE"),
            ["P1-CONTEST-DEFENDER"] = new("P1", "BATTLEFIELD", "BF-CONTEST")
        };

        return new MatchState(
            roomId: occupied ? "board-task-base-move-contest" : "board-task-base-move-empty",
            tick: 0,
            turnNumber: 1,
            activePlayerId: activePlayerId,
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: activePlayerId,
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = p1Base,
                    Battlefields = p1Battlefields
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = p2Base
                }
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
    }

    private static MatchState CleanupPriorityContestState()
    {
        return new MatchState(
            roomId: "board-task-cleanup-priority-contest",
            tick: 4,
            turnNumber: 2,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-CONTEST", "P1-LETHAL-CONTESTER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-CONTEST-DEFENDER"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-CONTEST"] = Battlefield("BF-CONTEST", "P1"),
                ["P1-LETHAL-CONTESTER"] = Unit("P1-LETHAL-CONTESTER", "P1", damage: 3, power: 3),
                ["P2-CONTEST-DEFENDER"] = Unit("P2-CONTEST-DEFENDER", "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-CONTEST"] = new("P1", "BATTLEFIELD", "BF-CONTEST"),
                ["P1-LETHAL-CONTESTER"] = new("P1", "BATTLEFIELD", "BF-CONTEST"),
                ["P2-CONTEST-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-CONTEST")
            });
    }

    private static MatchState StackMoveToBaseContestState()
    {
        return new MatchState(
            roomId: "board-task-stack-removes-contest",
            tick: 7,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Graveyard = ["P1-SPELL-BATTLE-OR-FLIGHT"],
                    Battlefields = ["BF-CONTEST", "P1-CONTEST-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-CONTEST-DEFENDER"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BATTLE-OR-FLIGHT"] = new(
                    "P1-SPELL-BATTLE-OR-FLIGHT",
                    cardNo: "OGN·168/298",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["BF-CONTEST"] = Battlefield("BF-CONTEST", "P1"),
                ["P1-CONTEST-ATTACKER"] = Unit("P1-CONTEST-ATTACKER", "P1"),
                ["P2-CONTEST-DEFENDER"] = Unit("P2-CONTEST-DEFENDER", "P2")
            },
            priorityPlayerId: "P1",
            passedPriorityPlayerIds: ["P2"],
            stackItems:
            [
                new StackItemState(
                    "STACK-BATTLE-OR-FLIGHT",
                    "P1",
                    "P1-SPELL-BATTLE-OR-FLIGHT",
                    "BATTLE_OR_FLIGHT_MOVE_BATTLEFIELD_UNIT_TO_BASE",
                    "OGN·168/298",
                    ["P2-CONTEST-DEFENDER"])
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BATTLE-OR-FLIGHT"] = new("P1", "GRAVEYARD"),
                ["BF-CONTEST"] = new("P1", "BATTLEFIELD", "BF-CONTEST"),
                ["P1-CONTEST-ATTACKER"] = new("P1", "BATTLEFIELD", "BF-CONTEST"),
                ["P2-CONTEST-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-CONTEST")
            });
    }

    private static MatchState CleanupRepeatState()
    {
        return new MatchState(
            roomId: "board-task-cleanup-repeat",
            tick: 3,
            turnNumber: 2,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P1-LETHAL-HOST", "P1-ATTACHED-EQUIPMENT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P1"),
                ["P1-LETHAL-HOST"] = Unit("P1-LETHAL-HOST", "P1", damage: 3, power: 3),
                ["P1-ATTACHED-EQUIPMENT"] = new(
                    "P1-ATTACHED-EQUIPMENT",
                    cardNo: "SFD·139/221",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P1",
                    controllerId: "P1",
                    attachedToObjectId: "P1-LETHAL-HOST")
            },
            priorityPlayerId: "P1",
            passedPriorityPlayerIds: ["P2"],
            stackItems:
            [
                new StackItemState(
                    "STACK-CLEANUP-REPEAT",
                    "P1",
                    "P1-NOOP-SOURCE",
                    "TEST_RESOLVE",
                    "TEST-000",
                    [])
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-LETHAL-HOST"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-ATTACHED-EQUIPMENT"] = new("P1", "BATTLEFIELD", "BF-1")
            });
    }

    private static MatchState IllegalStandbyState()
    {
        return new MatchState(
            roomId: "board-task-illegal-standby-redaction",
            tick: 5,
            turnNumber: 2,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P1-HIDDEN-STANDBY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P2"),
                ["P1-HIDDEN-STANDBY"] = new(
                    "P1-HIDDEN-STANDBY",
                    cardNo: "OGN·121/298",
                    power: 2,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-HIDDEN-STANDBY"] = new("P1", "BATTLEFIELD", "BF-1")
            });
    }

    private static MatchState UnattachedEquipmentState()
    {
        return new MatchState(
            roomId: "board-task-unattached-equipment-redaction",
            tick: 5,
            turnNumber: 2,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-1", "P1-UNATTACHED-EQUIPMENT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-1"] = Battlefield("BF-1", "P1"),
                ["P1-UNATTACHED-EQUIPMENT"] = new(
                    "P1-UNATTACHED-EQUIPMENT",
                    cardNo: "SFD·139/221",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-1"] = new("P1", "BATTLEFIELD", "BF-1"),
                ["P1-UNATTACHED-EQUIPMENT"] = new("P1", "BATTLEFIELD", "BF-1")
            });
    }

    private static MatchState SpellDuelReadyToCloseState()
    {
        return new MatchState(
            roomId: "board-task-pass-focus-promote-start-battle",
            tick: 8,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.SpellDuelOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["BF-CONTEST", "P1-CONTEST-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-CONTEST-DEFENDER"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF-CONTEST"] = Battlefield("BF-CONTEST", "P1"),
                ["P1-CONTEST-ATTACKER"] = Unit("P1-CONTEST-ATTACKER", "P1"),
                ["P2-CONTEST-DEFENDER"] = Unit("P2-CONTEST-DEFENDER", "P2")
            },
            focusPlayerId: "P1",
            passedFocusPlayerIds: ["P2"],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["BF-CONTEST"] = new("P1", "BATTLEFIELD", "BF-CONTEST"),
                ["P1-CONTEST-ATTACKER"] = new("P1", "BATTLEFIELD", "BF-CONTEST"),
                ["P2-CONTEST-DEFENDER"] = new("P2", "BATTLEFIELD", "BF-CONTEST")
            });
    }

    private static void AssertSpellDuelPromotesBattleDeclarationAudit(ResolutionResult result)
    {
        var focusPassedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "FOCUS_PASSED", StringComparison.Ordinal));
        var focusPassed = result.Events[focusPassedIndex];
        Assert.Equal("P1", focusPassed.Payload["playerId"]);
        Assert.Equal("P1", focusPassed.Payload["focusPlayerId"]);

        var spellDuelClosedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_CLOSED", StringComparison.Ordinal));
        var spellDuelClosed = result.Events[spellDuelClosedIndex];
        Assert.Equal("P1", spellDuelClosed.Payload["turnPlayerId"]);
        Assert.Equal(["BF-CONTEST"], StringList(spellDuelClosed.Payload["completedBattlefieldObjectIds"]));
        Assert.True(focusPassedIndex < spellDuelClosedIndex);

        var spellDuelTask = Assert.Single(result.State.BattlefieldTasks, task =>
            string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal));
        Assert.Equal("task:start-spell-duel:BF-CONTEST", spellDuelTask.TaskId);
        Assert.Equal("COMPLETED", spellDuelTask.Status);
        Assert.Equal("BATTLEFIELD_CONTESTED", spellDuelTask.Reason);
        Assert.Equal("BF-CONTEST", spellDuelTask.BattlefieldObjectId);
        Assert.Equal(["P1", "P2"], spellDuelTask.ParticipantControllerIds);
        Assert.Equal(["P1-CONTEST-ATTACKER", "P2-CONTEST-DEFENDER"], spellDuelTask.ParticipantObjectIds);
        Assert.Null(spellDuelTask.ActingPlayerId);
        Assert.Empty(spellDuelTask.StackItemIds);

        var battleTask = Assert.Single(result.State.BattlefieldTasks, task =>
            string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.Equal("task:start-battle:BF-CONTEST", battleTask.TaskId);
        Assert.Equal("PENDING", battleTask.Status);
        Assert.Equal("SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", battleTask.Reason);
        Assert.Equal("BF-CONTEST", battleTask.BattlefieldObjectId);
        Assert.Equal(["P1", "P2"], battleTask.ParticipantControllerIds);
        Assert.Equal(["P1-CONTEST-ATTACKER", "P2-CONTEST-DEFENDER"], battleTask.ParticipantObjectIds);
        Assert.Null(battleTask.ActingPlayerId);
        Assert.Empty(battleTask.StackItemIds);

        Assert.True(result.State.PendingTaskQueue.HasTasks);
        Assert.True(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("BATTLE_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-battle:BF-CONTEST", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Equal(
            ["cleanup:battlefield-contested:BF-CONTEST", "task:start-spell-duel:BF-CONTEST", "task:start-battle:BF-CONTEST"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.TaskId).ToArray());
        Assert.Equal(
            ["BF-CONTEST", "BF-CONTEST", "BF-CONTEST"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.BattlefieldObjectId!).ToArray());

        var queue = Assert.IsType<Dictionary<string, object?>>(result.Snapshots["P1"].Timing["pendingTaskQueue"]);
        Assert.True(Assert.IsType<bool>(queue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(queue["isBlocking"]));
        Assert.Equal("BATTLE_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-battle:BF-CONTEST", Assert.IsType<string>(queue["activeTaskId"]));
        var queueTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            queueTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["cleanup:battlefield-contested:BF-CONTEST", "task:start-spell-duel:BF-CONTEST", "task:start-battle:BF-CONTEST"],
            queueTasks.Select(task => Assert.IsType<string>(task["taskId"])).ToArray());
        Assert.Equal(
            ["BF-CONTEST", "BF-CONTEST", "BF-CONTEST"],
            queueTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());
        var queueMetadata = Assert.IsType<Dictionary<string, object?>>(queue["metadata"]);
        Assert.Equal(3, Assert.IsType<int>(queueMetadata["taskCount"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(queueMetadata["stateBasedTaskKinds"]));

        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(result.Snapshots["P1"].Timing["battlefieldTasks"]);
        Assert.Equal(
            ["START_SPELL_DUEL", "START_BATTLE"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["COMPLETED", "PENDING"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["status"])).ToArray());
        Assert.Equal(
            ["BF-CONTEST", "BF-CONTEST"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());
        var snapshotStartBattleTask = Assert.Single(
            battlefieldTasks,
            task => string.Equals(task["kind"] as string, "START_BATTLE", StringComparison.Ordinal));
        Assert.Equal("battle:BF-CONTEST", Assert.IsType<string>(snapshotStartBattleTask["battleId"]));
        Assert.Equal("SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", Assert.IsType<string>(snapshotStartBattleTask["reason"]));
        Assert.Equal(["P1", "P2"], Assert.IsAssignableFrom<IReadOnlyList<string>>(snapshotStartBattleTask["participantControllerIds"]));
        Assert.Equal(
            ["P1-CONTEST-ATTACKER", "P2-CONTEST-DEFENDER"],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(snapshotStartBattleTask["participantObjectIds"]));

        Assert.Equal("P1", result.Prompts["P1"].PlayerId);
        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(result.State.Tick, result.Prompts["P1"].SnapshotTick);
        Assert.Equal(PromptTypes.BattleDeclaration, result.Prompts["P1"].View?.Type);
        Assert.Equal("BF-CONTEST", result.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("battle:BF-CONTEST", result.Prompts["P1"].View?.RelatedBattleId);
        Assert.Null(result.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Equal(["DECLARE_BATTLE", "SURRENDER"], result.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P1"].Actions);

        Assert.Equal("P2", result.Prompts["P2"].PlayerId);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.Equal(result.State.Tick, result.Prompts["P2"].SnapshotTick);
        Assert.Equal(PromptTypes.BattleDeclaration, result.Prompts["P2"].View?.Type);
        Assert.Equal("BF-CONTEST", result.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("battle:BF-CONTEST", result.Prompts["P2"].View?.RelatedBattleId);
        Assert.Null(result.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Equal(["WAIT", "SURRENDER"], result.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, result.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P2"].Actions);

        var p1PromptJson = JsonSerializer.Serialize(result.Prompts["P1"]);
        Assert.DoesNotContain("spell-duel:BF-CONTEST", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("task:start-spell-duel:BF-CONTEST", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("PASS_FOCUS", p1PromptJson, StringComparison.Ordinal);
    }

    private static void AssertBaseMoveContestSpellDuelAudit(ResolutionResult result)
    {
        var movedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BASE-MOVER", StringComparison.Ordinal));
        var moved = result.Events[movedIndex];
        Assert.Equal("P2", moved.Payload["playerId"]);
        Assert.Equal("P2-BASE-MOVER", moved.Payload["sourceObjectId"]);
        Assert.Equal("BASE", moved.Payload["originZone"]);
        Assert.Equal("BATTLEFIELD", moved.Payload["destinationZone"]);
        Assert.Equal("BF-CONTEST", moved.Payload["battlefieldObjectId"]);

        var contestedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-CONTEST", StringComparison.Ordinal));
        var contested = result.Events[contestedIndex];
        Assert.Equal("P2", contested.Payload["playerId"]);
        Assert.Equal("P2", contested.Payload["causedByPlayerId"]);
        Assert.Equal(["P1", "P2"], StringList(contested.Payload["participantControllerIds"]));
        Assert.Equal(["P1-CONTEST-DEFENDER", "P2-BASE-MOVER"], StringList(contested.Payload["participantObjectIds"]));

        var spellDuelIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-CONTEST", StringComparison.Ordinal));
        var spellDuel = result.Events[spellDuelIndex];
        Assert.Equal("task:start-spell-duel:BF-CONTEST", spellDuel.Payload["taskId"]);
        Assert.Equal("BATTLEFIELD_CONTESTED", spellDuel.Payload["reason"]);
        Assert.Equal("P2", spellDuel.Payload["playerId"]);
        Assert.Equal("P2", spellDuel.Payload["focusPlayerId"]);
        Assert.Equal("P2", spellDuel.Payload["causedByPlayerId"]);
        Assert.Equal(["P1", "P2"], StringList(spellDuel.Payload["participantControllerIds"]));
        Assert.Equal(["P1-CONTEST-DEFENDER", "P2-BASE-MOVER"], StringList(spellDuel.Payload["participantObjectIds"]));

        Assert.True(movedIndex < contestedIndex);
        Assert.True(contestedIndex < spellDuelIndex);

        var contestTask = Assert.Single(result.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal));
        Assert.Equal("cleanup:battlefield-contested:BF-CONTEST", contestTask.TaskId);
        Assert.Equal("BATTLEFIELD_CONTROL_CHECK", contestTask.Reason);
        Assert.Equal("P1", contestTask.PlayerId);
        Assert.Equal("BF-CONTEST", contestTask.BattlefieldObjectId);

        var spellDuelTask = Assert.Single(result.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal));
        Assert.Equal("task:start-spell-duel:BF-CONTEST", spellDuelTask.TaskId);
        Assert.Equal("BATTLEFIELD_CONTESTED", spellDuelTask.Reason);
        Assert.Equal("P1", spellDuelTask.PlayerId);
        Assert.Equal("BF-CONTEST", spellDuelTask.BattlefieldObjectId);

        var battleTask = Assert.Single(result.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.Equal("task:start-battle:BF-CONTEST", battleTask.TaskId);
        Assert.Equal("SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", battleTask.Reason);
        Assert.Equal("P1", battleTask.PlayerId);
        Assert.Equal("BF-CONTEST", battleTask.BattlefieldObjectId);
    }

    private static void AssertMoveUnitSpellDuelPromptQueueAudit(ResolutionResult result)
    {
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P2", result.State.FocusPlayerId);
        Assert.True(result.State.PendingTaskQueue.HasTasks);
        Assert.True(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-CONTEST", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Equal(
            ["cleanup:battlefield-contested:BF-CONTEST", "task:start-spell-duel:BF-CONTEST", "task:start-battle:BF-CONTEST"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.TaskId).ToArray());
        Assert.Equal(
            ["BF-CONTEST", "BF-CONTEST", "BF-CONTEST"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.BattlefieldObjectId!).ToArray());

        var queue = Assert.IsType<Dictionary<string, object?>>(result.Snapshots["P2"].Timing["pendingTaskQueue"]);
        Assert.True(Assert.IsType<bool>(queue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(queue["isBlocking"]));
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-spell-duel:BF-CONTEST", Assert.IsType<string>(queue["activeTaskId"]));
        var queueTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            queueTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["cleanup:battlefield-contested:BF-CONTEST", "task:start-spell-duel:BF-CONTEST", "task:start-battle:BF-CONTEST"],
            queueTasks.Select(task => Assert.IsType<string>(task["taskId"])).ToArray());
        Assert.Equal(
            ["BF-CONTEST", "BF-CONTEST", "BF-CONTEST"],
            queueTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());
        var queueMetadata = Assert.IsType<Dictionary<string, object?>>(queue["metadata"]);
        Assert.Equal(3, Assert.IsType<int>(queueMetadata["taskCount"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(queueMetadata["stateBasedTaskKinds"]));

        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(result.Snapshots["P2"].Timing["battlefieldTasks"]);
        Assert.Equal(
            ["START_SPELL_DUEL", "START_BATTLE"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["ACTIVE", "WAITING_FOR_SPELL_DUEL"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["status"])).ToArray());
        Assert.Equal(
            ["BF-CONTEST", "BF-CONTEST"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());
        var activeSpellDuelTask = Assert.Single(
            battlefieldTasks,
            task => string.Equals(task["kind"] as string, "START_SPELL_DUEL", StringComparison.Ordinal));
        Assert.Equal("BATTLEFIELD_CONTESTED", Assert.IsType<string>(activeSpellDuelTask["reason"]));
        Assert.Equal("P2", Assert.IsType<string>(activeSpellDuelTask["actingPlayerId"]));
        Assert.Equal("spell-duel:BF-CONTEST", Assert.IsType<string>(activeSpellDuelTask["spellDuelId"]));

        Assert.Equal("P2", result.Prompts["P2"].PlayerId);
        Assert.True(result.Prompts["P2"].Actionable);
        Assert.Equal(PromptTypes.SpellDuelFocus, result.Prompts["P2"].View?.Type);
        Assert.Equal("BF-CONTEST", result.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-CONTEST", result.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Equal([CommandTypes.PassFocus, CommandTypes.Surrender], result.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.MoveUnit, result.Prompts["P2"].Actions);

        Assert.Equal("P1", result.Prompts["P1"].PlayerId);
        Assert.False(result.Prompts["P1"].Actionable);
        Assert.Equal(PromptTypes.SpellDuelFocus, result.Prompts["P1"].View?.Type);
        Assert.Equal("BF-CONTEST", result.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-CONTEST", result.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Equal(["WAIT", CommandTypes.Surrender], result.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.MoveUnit, result.Prompts["P1"].Actions);

        var p2PromptJson = JsonSerializer.Serialize(result.Prompts["P2"]);
        Assert.DoesNotContain(CommandTypes.MoveUnit, p2PromptJson, StringComparison.Ordinal);
    }

    private static void AssertBaseMoveEmptyBattlefieldAudit(ResolutionResult result)
    {
        var moved = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
        Assert.Equal("P1", moved.Payload["playerId"]);
        Assert.Equal("P1-BASE-MOVER", moved.Payload["sourceObjectId"]);
        Assert.Equal("P1-BASE-MOVER", moved.Payload["targetObjectId"]);
        Assert.Equal("BASE", moved.Payload["originZone"]);
        Assert.Equal("BATTLEFIELD", moved.Payload["destinationZone"]);
        Assert.Equal("BATTLEFIELD:BF-EMPTY", moved.Payload["destination"]);
        Assert.Equal("BF-EMPTY", moved.Payload["battlefieldObjectId"]);
        Assert.Empty(StringList(moved.Payload["optionalCosts"]));

        var battlefield = result.State.BattlefieldStates["BF-EMPTY"];
        Assert.Equal("BF-EMPTY", battlefield.BattlefieldObjectId);
        Assert.Equal("P1", battlefield.ZonePlayerId);
        Assert.Equal("P1", battlefield.ControllerId);
        Assert.Equal("CONTROLLED", battlefield.Status);
        Assert.False(battlefield.Contested);
        Assert.Equal(["P1-BASE-MOVER"], battlefield.OccupantObjectIds);
        Assert.Equal(["P1"], battlefield.OccupantControllerIds);
        Assert.Empty(battlefield.StandbyObjectIds);
        Assert.Equal(0, battlefield.FaceDownStandbyCount);

        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);
        Assert.Empty(result.State.BattlefieldTasks);

        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
    }

    private static void AssertStackMoveToBaseIdleAudit(ResolutionResult result)
    {
        var priorityPassedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PRIORITY_PASSED", StringComparison.Ordinal));
        var priorityPassed = result.Events[priorityPassedIndex];
        Assert.Equal("P1", priorityPassed.Payload["playerId"]);
        Assert.Equal("P1", priorityPassed.Payload["priorityPlayerId"]);

        var stackResolvedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        var stackResolved = result.Events[stackResolvedIndex];
        Assert.Equal("STACK-BATTLE-OR-FLIGHT", stackResolved.Payload["stackItemId"]);
        Assert.Equal("P1", stackResolved.Payload["controllerId"]);
        Assert.Equal("P1-SPELL-BATTLE-OR-FLIGHT", stackResolved.Payload["sourceObjectId"]);
        Assert.Equal("BATTLE_OR_FLIGHT_MOVE_BATTLEFIELD_UNIT_TO_BASE", stackResolved.Payload["effectKind"]);

        var movedToBaseIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal));
        var movedToBase = result.Events[movedToBaseIndex];
        Assert.Equal("P1-SPELL-BATTLE-OR-FLIGHT", movedToBase.Payload["sourceObjectId"]);
        Assert.Equal("P2-CONTEST-DEFENDER", movedToBase.Payload["targetObjectId"]);
        Assert.Equal("P2", movedToBase.Payload["ownerPlayerId"]);

        Assert.True(priorityPassedIndex < stackResolvedIndex);
        Assert.True(stackResolvedIndex < movedToBaseIndex);

        var battlefield = result.State.BattlefieldStates["BF-CONTEST"];
        Assert.Equal("BF-CONTEST", battlefield.BattlefieldObjectId);
        Assert.Equal("P1", battlefield.ZonePlayerId);
        Assert.Equal("P1", battlefield.ControllerId);
        Assert.Equal("CONTROLLED", battlefield.Status);
        Assert.False(battlefield.Contested);
        Assert.Equal(["P1-CONTEST-ATTACKER"], battlefield.OccupantObjectIds);
        Assert.Equal(["P1"], battlefield.OccupantControllerIds);

        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);

        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
    }

    private static void AssertStackPriorityResolvedIdlePromptQueueAudit(ResolutionResult result)
    {
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.Equal(new ObjectLocationState("P2", "BASE"), result.State.ObjectLocations["P2-CONTEST-DEFENDER"]);

        Assert.Empty(result.Snapshots["P1"].Stack);
        Assert.Empty(result.Snapshots["P2"].Stack);

        var p1Queue = Assert.IsType<Dictionary<string, object?>>(result.Snapshots["P1"].Timing["pendingTaskQueue"]);
        Assert.False(Assert.IsType<bool>(p1Queue["hasTasks"]));
        Assert.False(Assert.IsType<bool>(p1Queue["isBlocking"]));
        Assert.Equal("IDLE", Assert.IsType<string>(p1Queue["phase"]));
        Assert.Null(p1Queue["activeTaskId"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(p1Queue["tasks"]));

        Assert.DoesNotContain(CommandTypes.PassPriority, result.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, result.Prompts["P2"].Actions);
        Assert.NotEqual(PromptTypes.StackPriority, result.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.StackPriority, result.Prompts["P2"].View?.Type);

        var p1PromptJson = JsonSerializer.Serialize(result.Prompts["P1"]);
        Assert.DoesNotContain("STACK-BATTLE-OR-FLIGHT", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("STACK-FOLLOWUP-NOOP", p1PromptJson, StringComparison.Ordinal);
    }

    private static void AssertStackPriorityNextItemPromptQueueAudit(ResolutionResult result)
    {
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("STACK-FOLLOWUP-NOOP", stackItem.StackItemId);
        Assert.Equal("P1", stackItem.ControllerId);
        Assert.Equal("P1-FOLLOWUP-SPELL", stackItem.SourceObjectId);
        Assert.Equal("TEST_RESOLVE", stackItem.EffectKind);
        Assert.Equal("TEST-001", stackItem.CardNo);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.Equal(new ObjectLocationState("P2", "BASE"), result.State.ObjectLocations["P2-CONTEST-DEFENDER"]);

        var p1StackItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(result.Snapshots["P1"].Stack));
        Assert.Equal("STACK-FOLLOWUP-NOOP", Assert.IsType<string>(p1StackItem["stackItemId"]));
        Assert.Equal("P1", Assert.IsType<string>(p1StackItem["controllerId"]));
        Assert.Equal("P1-FOLLOWUP-SPELL", Assert.IsType<string>(p1StackItem["sourceObjectId"]));
        Assert.Equal("TEST_RESOLVE", Assert.IsType<string>(p1StackItem["effectKind"]));
        Assert.Equal("TEST-001", Assert.IsType<string>(p1StackItem["cardNo"]));

        var p1Queue = Assert.IsType<Dictionary<string, object?>>(result.Snapshots["P1"].Timing["pendingTaskQueue"]);
        Assert.False(Assert.IsType<bool>(p1Queue["hasTasks"]));
        Assert.False(Assert.IsType<bool>(p1Queue["isBlocking"]));
        Assert.Equal("IDLE", Assert.IsType<string>(p1Queue["phase"]));
        Assert.Null(p1Queue["activeTaskId"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(p1Queue["tasks"]));

        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(PromptTypes.StackPriority, result.Prompts["P1"].View?.Type);
        Assert.Equal("STACK-FOLLOWUP-NOOP", result.Prompts["P1"].View?.RelatedStackItemId);
        Assert.Contains(CommandTypes.PassPriority, result.Prompts["P1"].Actions);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.PassPriority, result.Prompts["P2"].Actions);

        var p1PromptJson = JsonSerializer.Serialize(result.Prompts["P1"]);
        Assert.DoesNotContain("STACK-BATTLE-OR-FLIGHT", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("P2-CONTEST-DEFENDER", p1PromptJson, StringComparison.Ordinal);
    }

    private static void AssertCleanupRepeatEquipmentRecallAudit(ResolutionResult result)
    {
        var priorityPassedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PRIORITY_PASSED", StringComparison.Ordinal));
        var priorityPassed = result.Events[priorityPassedIndex];
        Assert.Equal("P1", priorityPassed.Payload["playerId"]);
        Assert.Equal("P1", priorityPassed.Payload["priorityPlayerId"]);

        var stackResolvedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        var stackResolved = result.Events[stackResolvedIndex];
        Assert.Equal("STACK-CLEANUP-REPEAT", stackResolved.Payload["stackItemId"]);
        Assert.Equal("P1", stackResolved.Payload["controllerId"]);
        Assert.Equal("P1-NOOP-SOURCE", stackResolved.Payload["sourceObjectId"]);
        Assert.Equal("TEST_RESOLVE", stackResolved.Payload["effectKind"]);

        var destroyedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-LETHAL-HOST", StringComparison.Ordinal));
        var destroyed = result.Events[destroyedIndex];
        Assert.Equal("P1-NOOP-SOURCE", destroyed.Payload["sourceObjectId"]);
        Assert.Equal("P1", destroyed.Payload["ownerPlayerId"]);
        Assert.Equal("P1", destroyed.Payload["destroyedByPlayerId"]);
        Assert.Equal("GRAVEYARD", destroyed.Payload["destinationZone"]);
        Assert.Equal("LETHAL_DAMAGE", destroyed.Payload["reason"]);
        Assert.Equal(["P1-ATTACHED-EQUIPMENT"], StringList(destroyed.Payload["detachedEquipmentObjectIds"]));

        var recalledIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_RECALLED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentObjectId"] as string, "P1-ATTACHED-EQUIPMENT", StringComparison.Ordinal));
        var recalled = result.Events[recalledIndex];
        Assert.Equal("P1-ATTACHED-EQUIPMENT", recalled.Payload["targetObjectId"]);
        Assert.Equal("P1", recalled.Payload["ownerPlayerId"]);
        Assert.Equal("P1", recalled.Payload["controllerId"]);
        Assert.Equal("BF-1", recalled.Payload["battlefieldObjectId"]);
        Assert.Equal("BATTLEFIELD", recalled.Payload["originZone"]);
        Assert.Equal("BASE", recalled.Payload["destinationZone"]);
        Assert.Equal("UNATTACHED_EQUIPMENT_CLEANUP", recalled.Payload["reason"]);

        Assert.True(priorityPassedIndex < stackResolvedIndex);
        Assert.True(stackResolvedIndex < destroyedIndex);
        Assert.True(destroyedIndex < recalledIndex);

        Assert.DoesNotContain("P1-LETHAL-HOST", result.State.CardObjects.Keys);
        Assert.Contains("P1-LETHAL-HOST", result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(new ObjectLocationState("P1", "BASE"), result.State.ObjectLocations["P1-ATTACHED-EQUIPMENT"]);
        Assert.Null(result.State.CardObjects["P1-ATTACHED-EQUIPMENT"].AttachedToObjectId);

        var battlefield = result.State.BattlefieldStates["BF-1"];
        Assert.Equal("BF-1", battlefield.BattlefieldObjectId);
        Assert.Equal("P1", battlefield.ZonePlayerId);
        Assert.Equal("P1", battlefield.ControllerId);
        Assert.Equal("CONTROLLED", battlefield.Status);
        Assert.False(battlefield.Contested);
        Assert.Empty(battlefield.OccupantObjectIds);
        Assert.Empty(battlefield.OccupantControllerIds);

        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);
        Assert.Empty(result.State.PendingCleanupTasks);
        Assert.Empty(result.State.BattlefieldTasks);
    }

    private static void AssertPreciseRoamDestinationContestAudit(
        ResolutionResult result,
        string origin,
        string destination,
        string originBattlefieldObjectId,
        string destinationBattlefieldObjectId)
    {
        var movedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-ROAMER-001", StringComparison.Ordinal));
        var moved = result.Events[movedIndex];
        Assert.Equal("P1", moved.Payload["playerId"]);
        Assert.Equal("P1-ROAMER-001", moved.Payload["sourceObjectId"]);
        Assert.Equal("BATTLEFIELD", moved.Payload["originZone"]);
        Assert.Equal("BATTLEFIELD", moved.Payload["destinationZone"]);
        Assert.Equal(origin, moved.Payload["origin"]);
        Assert.Equal(destination, moved.Payload["destination"]);
        Assert.Equal("游走", moved.Payload["movementKeyword"]);
        Assert.Equal(["ROAM"], StringList(moved.Payload["optionalCosts"]));
        Assert.False(moved.Payload.ContainsKey("battlefieldObjectId"));

        var contestedIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, destinationBattlefieldObjectId, StringComparison.Ordinal));
        var contested = result.Events[contestedIndex];
        Assert.Equal("P1", contested.Payload["playerId"]);
        Assert.Equal("P1", contested.Payload["causedByPlayerId"]);
        Assert.Equal(["P1", "P2"], StringList(contested.Payload["participantControllerIds"]));
        Assert.Equal(["P1-ROAMER-001", "P2-DEST-DEFENDER"], StringList(contested.Payload["participantObjectIds"]));

        var spellDuelIndex = EventIndex(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, destinationBattlefieldObjectId, StringComparison.Ordinal));
        var spellDuel = result.Events[spellDuelIndex];
        Assert.Equal($"task:start-spell-duel:{destinationBattlefieldObjectId}", spellDuel.Payload["taskId"]);
        Assert.Equal("BATTLEFIELD_CONTESTED", spellDuel.Payload["reason"]);
        Assert.Equal("P1", spellDuel.Payload["playerId"]);
        Assert.Equal("P1", spellDuel.Payload["focusPlayerId"]);
        Assert.Equal("P1", spellDuel.Payload["causedByPlayerId"]);
        Assert.Equal(["P1", "P2"], StringList(spellDuel.Payload["participantControllerIds"]));
        Assert.Equal(["P1-ROAMER-001", "P2-DEST-DEFENDER"], StringList(spellDuel.Payload["participantObjectIds"]));

        Assert.True(movedIndex < contestedIndex);
        Assert.True(contestedIndex < spellDuelIndex);

        var originBattlefield = result.State.BattlefieldStates[originBattlefieldObjectId];
        Assert.Equal(originBattlefieldObjectId, originBattlefield.BattlefieldObjectId);
        Assert.Equal("P1", originBattlefield.ControllerId);
        Assert.False(originBattlefield.Contested);
        Assert.Empty(originBattlefield.OccupantObjectIds);
        Assert.Empty(originBattlefield.OccupantControllerIds);

        var destinationBattlefield = result.State.BattlefieldStates[destinationBattlefieldObjectId];
        Assert.Equal(destinationBattlefieldObjectId, destinationBattlefield.BattlefieldObjectId);
        Assert.Equal("P2", destinationBattlefield.ControllerId);
        Assert.True(destinationBattlefield.Contested);
        Assert.Equal(["P1-ROAMER-001", "P2-DEST-DEFENDER"], destinationBattlefield.OccupantObjectIds);
        Assert.Equal(["P1", "P2"], destinationBattlefield.OccupantControllerIds);

        var contestTask = Assert.Single(result.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal));
        Assert.Equal($"cleanup:battlefield-contested:{destinationBattlefieldObjectId}", contestTask.TaskId);
        Assert.Equal("BATTLEFIELD_CONTROL_CHECK", contestTask.Reason);
        Assert.Equal("P1", contestTask.PlayerId);
        Assert.Equal(destinationBattlefieldObjectId, contestTask.BattlefieldObjectId);

        var spellDuelTask = Assert.Single(result.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal));
        Assert.Equal($"task:start-spell-duel:{destinationBattlefieldObjectId}", spellDuelTask.TaskId);
        Assert.Equal("BATTLEFIELD_CONTESTED", spellDuelTask.Reason);
        Assert.Equal("P1", spellDuelTask.PlayerId);
        Assert.Equal(destinationBattlefieldObjectId, spellDuelTask.BattlefieldObjectId);

        var battleTask = Assert.Single(result.State.PendingTaskQueue.Tasks, task =>
            string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal));
        Assert.Equal($"task:start-battle:{destinationBattlefieldObjectId}", battleTask.TaskId);
        Assert.Equal("SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", battleTask.Reason);
        Assert.Equal("P1", battleTask.PlayerId);
        Assert.Equal(destinationBattlefieldObjectId, battleTask.BattlefieldObjectId);
    }

    private static int EventIndex(IReadOnlyList<GameEvent> events, Func<GameEvent, bool> predicate)
    {
        for (var index = 0; index < events.Count; index++)
        {
            if (predicate(events[index]))
            {
                return index;
            }
        }

        throw new Xunit.Sdk.XunitException("Expected event was not found.");
    }

    private static IReadOnlyList<string> StringList(object? value)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(value);
    }

    private static MatchState PreciseRoamContestState(
        string originBattlefieldObjectId,
        string destinationBattlefieldObjectId)
    {
        return new MatchState(
            roomId: "board-task-precise-roam-contest",
            tick: 6,
            turnNumber: 3,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [originBattlefieldObjectId, destinationBattlefieldObjectId, "P1-ROAMER-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-DEST-DEFENDER"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [originBattlefieldObjectId] = Battlefield(originBattlefieldObjectId, "P1"),
                [destinationBattlefieldObjectId] = Battlefield(destinationBattlefieldObjectId, "P2"),
                ["P1-ROAMER-001"] = RoamUnit("P1-ROAMER-001", "P1"),
                ["P2-DEST-DEFENDER"] = Unit("P2-DEST-DEFENDER", "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [originBattlefieldObjectId] = new("P1", "BATTLEFIELD", originBattlefieldObjectId),
                [destinationBattlefieldObjectId] = new("P1", "BATTLEFIELD", destinationBattlefieldObjectId),
                ["P1-ROAMER-001"] = new("P1", "BATTLEFIELD", originBattlefieldObjectId),
                ["P2-DEST-DEFENDER"] = new("P2", "BATTLEFIELD", destinationBattlefieldObjectId)
            });
    }

    private static Dictionary<string, string> Seats()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P1"] = "connection-1",
            ["P2"] = "connection-2"
        };
    }

    private static Dictionary<string, RunePool> EmptyPools()
    {
        return new Dictionary<string, RunePool>(StringComparer.Ordinal)
        {
            ["P1"] = RunePool.Empty,
            ["P2"] = RunePool.Empty
        };
    }

    private static CardObjectState Battlefield(string objectId, string controllerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·275/298",
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: controllerId,
            controllerId: controllerId);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int damage = 0,
        int power = 3,
        bool isExhausted = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
            damage: damage,
            power: power,
            isExhausted: isExhausted,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState RoamUnit(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·096/221",
            power: 3,
            tags: [CardObjectTags.UnitCard, "游走"],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static JsonElement PromptScopedRawCommand(string cmdType, ActionPromptDto prompt)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["cmdType"] = cmdType,
            ["promptId"] = prompt.PromptId,
            ["snapshotTick"] = prompt.SnapshotTick
        }));
        return document.RootElement.Clone();
    }

    private static JsonElement PromptScopedRawCommandWithClientNote(
        string cmdType,
        ActionPromptDto prompt,
        string clientNote)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["cmdType"] = cmdType,
            ["promptId"] = prompt.PromptId,
            ["snapshotTick"] = prompt.SnapshotTick,
            ["clientNote"] = clientNote
        }));
        return document.RootElement.Clone();
    }

    private static void AssertPromptScopedRawCommand(
        JsonElement rawCommand,
        string cmdType,
        ActionPromptDto prompt)
    {
        Assert.Equal(cmdType, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rawCommand.GetProperty("snapshotTick").GetInt64());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));
    }

    private static JsonElement PromptScopedMoveUnitRawCommand(
        MoveUnitCommand command,
        ActionPromptDto prompt)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            cmdType = CommandTypes.MoveUnit,
            sourceObjectId = command.SourceObjectId,
            origin = command.Origin,
            destination = command.Destination,
            optionalCosts = command.OptionalCosts,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        }));
        return document.RootElement.Clone();
    }

    private static JsonElement PromptScopedMoveUnitRawCommandWithClientNote(
        MoveUnitCommand command,
        ActionPromptDto prompt,
        string clientNote)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            cmdType = CommandTypes.MoveUnit,
            sourceObjectId = command.SourceObjectId,
            origin = command.Origin,
            destination = command.Destination,
            optionalCosts = command.OptionalCosts,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote
        }));
        return document.RootElement.Clone();
    }

    private static void AssertPromptScopedMoveUnitRawCommand(
        JsonElement rawCommand,
        MoveUnitCommand command,
        ActionPromptDto prompt)
    {
        Assert.Equal(CommandTypes.MoveUnit, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(command.SourceObjectId, rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal(command.Origin, rawCommand.GetProperty("origin").GetString());
        Assert.Equal(command.Destination, rawCommand.GetProperty("destination").GetString());
        Assert.Equal(command.OptionalCosts, rawCommand.GetProperty("optionalCosts")
            .EnumerateArray()
            .Select(optionalCost => optionalCost.GetString()!)
            .ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rawCommand.GetProperty("snapshotTick").GetInt64());
        Assert.False(rawCommand.TryGetProperty("clientNote", out _));
    }

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }
}
