using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SpellDuelBattleStateMachineTests
{
    [Fact]
    public void MultipleContestedBattlefieldsExposeOneActiveSpellDuelTaskInDeterministicOrder()
    {
        var state = MultiContestSpellDuelState();
        var snapshot = ResolutionResult.BuildSnapshots(state)["P1"];
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        Assert.Equal("BF-A", state.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("spell-duel:BF-A", state.SpellDuelState.SpellDuelId);
        Assert.Equal("SPELL_DUEL_TASKS", state.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-A", state.PendingTaskQueue.ActiveTaskId);

        var spellDuelTasks = state.BattlefieldTasks
            .Where(task => string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(["BF-A", "BF-B"], spellDuelTasks.Select(task => task.BattlefieldObjectId).ToArray());
        Assert.Single(spellDuelTasks, task => string.Equals(task.Status, "ACTIVE", StringComparison.Ordinal));
        Assert.Equal("ACTIVE", spellDuelTasks[0].Status);
        Assert.Equal("PENDING", spellDuelTasks[1].Status);
        Assert.Equal(["P1-A", "P2-A"], spellDuelTasks[0].ParticipantObjectIds);
        Assert.Equal(["P1", "P2"], spellDuelTasks[0].ParticipantControllerIds);

        var startBattleTasks = state.BattlefieldTasks
            .Where(task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(["BF-A", "BF-B"], startBattleTasks.Select(task => task.BattlefieldObjectId).ToArray());
        Assert.All(startBattleTasks, task => Assert.Equal("WAITING_FOR_SPELL_DUEL", task.Status));

        AssertMultiContestActiveSpellDuelTaskAudit(state, snapshot, prompt);
    }

    [Fact]
    public async Task PassFocusByNonFocusPlayerOrWrongTimingRejectsWithoutMutation()
    {
        var state = MultiContestSpellDuelState();
        var nonFocusResult = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-non-focus-pass-focus", "P2", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);

        AssertRejectedWithoutMutation(state, nonFocusResult, ErrorCodes.PhaseNotAllowed);
        AssertNonFocusPassFocusRejectionAudit(nonFocusResult);

        var neutralState = IdleNeutralState();
        var wrongTimingResult = await new CoreRuleEngine().ResolveAsync(
            neutralState,
            new PlayerIntent("intent-wrong-timing-pass-focus", "P1", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);

        AssertRejectedWithoutMutation(neutralState, wrongTimingResult, ErrorCodes.PhaseNotAllowed);
        AssertWrongTimingPassFocusRejectionAudit(wrongTimingResult);
    }

    [Fact]
    public async Task PassFocusRejectsAcceptedCommandReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var state = MultiContestSpellDuelState();
        var command = new PassFocusCommand();

        var accepted = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-pass-focus-accepted-before-replay", "P1", CommandTypes.PassFocus),
            command,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(["FOCUS_PASSED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal("P2", accepted.State.FocusPlayerId);
        Assert.Equal(["P1"], accepted.State.PassedFocusPlayerIds);
        Assert.Equal(PromptTypes.SpellDuelFocus, accepted.Prompts["P2"].View?.Type);
        Assert.Contains(CommandTypes.PassFocus, accepted.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, accepted.Prompts["P1"].Actions);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await engine.ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-pass-focus-accepted-stale-replay", "P1", CommandTypes.PassFocus),
            command,
            CancellationToken.None);

        AssertRejectedWithoutMutation(accepted.State, replay, ErrorCodes.PhaseNotAllowed);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(PromptTypes.SpellDuelFocus, replay.Prompts["P2"].View?.Type);
        Assert.Contains(CommandTypes.PassFocus, replay.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, replay.Prompts["P1"].Actions);
        AssertAcceptedPassFocusReplayRejectionAudit(replay);
    }

    [Fact]
    public async Task SpellDuelStackResolutionReturnsToSameActiveTaskUntilBothPlayersPassFocus()
    {
        var state = SpellDuelStackState();

        Assert.Equal("BF-A", state.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("spell-duel:BF-A", state.SpellDuelState.SpellDuelId);
        Assert.Equal(["STACK-SWIFT-A"], state.SpellDuelState.StackItemIds);
        Assert.Equal("task:start-spell-duel:BF-A", state.PendingTaskQueue.ActiveTaskId);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-resolve-spell-duel-stack", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P2", result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", result.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.DoesNotContain(BattlefieldTaskMarkers.SpellDuelCompleted("BF-A"), result.State.UntilEndOfTurnEffects);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["stackItemId"] as string, "STACK-SWIFT-A", StringComparison.Ordinal));
        AssertStackResolutionReturnsToActiveSpellDuelTaskAudit(result);
    }

    [Fact]
    public async Task ClosingSpellDuelWithCleanupRemovedParticipantSkipsOnlyMatchingBattleAndAdvancesNextTask()
    {
        var state = MultiContestSpellDuelState(lethalFirstDefender: true, passedFocusPlayerIds: ["P2"]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-close-first-spell-duel-cleanup-next-task", "P1", CommandTypes.PassFocus),
            new PassFocusCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(
            ["FOCUS_PASSED", "SPELL_DUEL_CLOSED", "UNIT_DESTROYED", "BATTLEFIELD_CONTESTED", "SPELL_DUEL_STARTED"],
            result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Contains("P2-A", result.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("P2-A", result.State.PlayerZones["P2"].Battlefields);
        Assert.Contains(BattlefieldTaskMarkers.SpellDuelCompleted("BF-A"), result.State.UntilEndOfTurnEffects);
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.Equal("BF-B", result.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-A", StringComparison.Ordinal));

        AssertSpellDuelCloseCleanupPromptQueueAudit(result);
    }

    [Fact]
    public async Task SpellDuelFocusStalePromptReplayAfterNextContestStartsRejectsWithoutMutation()
    {
        var state = MultiContestSpellDuelState(lethalFirstDefender: true, passedFocusPlayerIds: ["P2"]);
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.SpellDuelFocus, prompt.View?.Type);
        Assert.Equal("spell-duel:BF-A", prompt.View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, prompt.Actions);
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.PassFocus, prompt);

        var accepted = await session.SubmitAsync(
            "P1",
            "intent-close-first-spell-duel-before-stale-prompt-replay",
            new PassFocusCommand(),
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(
            ["FOCUS_PASSED", "SPELL_DUEL_CLOSED", "UNIT_DESTROYED", "BATTLEFIELD_CONTESTED", "SPELL_DUEL_STARTED"],
            accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal("BF-B", accepted.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", accepted.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal("P1", accepted.State.FocusPlayerId);
        Assert.Equal(PromptTypes.SpellDuelFocus, accepted.Prompts["P1"].View?.Type);
        Assert.Equal("spell-duel:BF-B", accepted.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, accepted.Prompts["P1"].Actions);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await session.SubmitAsync(
            "P1",
            "intent-stale-first-spell-duel-prompt-replay",
            new PassFocusCommand(),
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal("BF-B", replay.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal("P1", replay.State.FocusPlayerId);
        Assert.Equal(PromptTypes.SpellDuelFocus, replay.Prompts["P1"].View?.Type);
        Assert.Equal("spell-duel:BF-B", replay.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, replay.Prompts["P1"].Actions);
        AssertStaleFirstSpellDuelPromptReplayAudit(replay);
    }

    [Fact]
    public void ReconnectDuringSpellDuelTasksPreservesTaskMetadataAndHiddenRedaction()
    {
        var state = MultiContestSpellDuelState(includeOpponentHiddenStandby: true);
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var p1 = session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var reconnect = session.ReconnectPlayer("P1", p1.ReconnectToken);
        var snapshot = session.SnapshotFor("P1");
        var prompt = session.PromptFor("P1");
        var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);

        Assert.Equal("P1", reconnect.PlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-spell-duel:BF-A", Assert.IsType<string>(queue["activeTaskId"]));
        Assert.Equal("P1", snapshot.Timing["focusPlayerId"]);
        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]);
        var activeTask = Assert.Single(battlefieldTasks, task =>
            string.Equals(task["kind"] as string, "START_SPELL_DUEL", StringComparison.Ordinal)
            && string.Equals(task["status"] as string, "ACTIVE", StringComparison.Ordinal));
        Assert.Equal("BF-A", Assert.IsType<string>(activeTask["battlefieldObjectId"]));
        Assert.Equal("spell-duel:BF-A", Assert.IsType<string>(activeTask["spellDuelId"]));
        Assert.Equal(["P1", "P2"], Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantControllerIds"]));
        Assert.Equal(["P1-A", "P2-A"], Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantObjectIds"]));
        Assert.Equal(PromptTypes.SpellDuelFocus, prompt.View?.Type);
        Assert.Equal("BF-A", prompt.View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", prompt.View?.RelatedSpellDuelId);
        AssertOpponentHiddenStandbyRedacted(snapshot, "P2-HIDDEN-STANDBY");
        AssertReconnectSpellDuelTaskMetadataAudit(reconnect, snapshot, prompt);
    }

    [Fact]
    public void ReconnectDuringBattleTasksPreservesBattleMetadataAndHiddenRedaction()
    {
        var state = StartBattleTaskState(includeOpponentHiddenStandby: true);
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var p1 = session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var reconnect = session.ReconnectPlayer("P1", p1.ReconnectToken);
        var snapshot = session.SnapshotFor("P1");
        var prompt = session.PromptFor("P1");
        var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);

        Assert.Equal("P1", reconnect.PlayerId);
        Assert.Equal("BATTLE_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-battle:BF-A", Assert.IsType<string>(queue["activeTaskId"]));
        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]);
        var activeTask = Assert.Single(battlefieldTasks, task =>
            string.Equals(task["kind"] as string, "START_BATTLE", StringComparison.Ordinal)
            && string.Equals(task["status"] as string, "PENDING", StringComparison.Ordinal)
            && string.Equals(task["battlefieldObjectId"] as string, "BF-A", StringComparison.Ordinal));
        Assert.Equal("battle:BF-A", Assert.IsType<string>(activeTask["battleId"]));
        Assert.Equal(["P1", "P2"], Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantControllerIds"]));
        Assert.Equal(["P1-A", "P2-A"], Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantObjectIds"]));
        Assert.Equal(PromptTypes.BattleDeclaration, prompt.View?.Type);
        Assert.Equal("BF-A", prompt.View?.RelatedBattlefieldId);
        Assert.Equal("battle:BF-A", prompt.View?.RelatedBattleId);
        AssertOpponentHiddenStandbyRedacted(snapshot, "P2-HIDDEN-STANDBY");
        AssertReconnectBattleTaskMetadataAudit(reconnect, snapshot, prompt);
    }

    private static void AssertRejectedWithoutMutation(
        MatchState state,
        ResolutionResult result,
        string expectedErrorCode)
    {
        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(state.Tick, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private static void AssertNonFocusPassFocusRejectionAudit(ResolutionResult result)
    {
        AssertMultiContestActiveSpellDuelTaskAudit(
            result.State,
            result.Snapshots["P1"],
            result.Prompts["P1"]);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P2"].Actions);
        Assert.Equal(PromptTypes.SpellDuelFocus, result.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", result.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", result.Prompts["P2"].View?.RelatedSpellDuelId);
    }

    private static void AssertWrongTimingPassFocusRejectionAudit(ResolutionResult result)
    {
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P2"].Actions);
    }

    private static void AssertAcceptedPassFocusReplayRejectionAudit(ResolutionResult result)
    {
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P2", result.State.FocusPlayerId);
        Assert.Equal(["P1"], result.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", result.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("spell-duel:BF-A", result.State.SpellDuelState.SpellDuelId);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-A", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_SPELL_DUEL", "START_BATTLE", "START_BATTLE"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Equal(
            ["cleanup:battlefield-contested:BF-A", "cleanup:battlefield-contested:BF-B", "task:start-spell-duel:BF-A", "task:start-spell-duel:BF-B", "task:start-battle:BF-A", "task:start-battle:BF-B"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.TaskId).ToArray());

        var queue = Assert.IsType<Dictionary<string, object?>>(result.Snapshots["P2"].Timing["pendingTaskQueue"]);
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.True(Assert.IsType<bool>(queue["isBlocking"]));
        Assert.Equal("task:start-spell-duel:BF-A", Assert.IsType<string>(queue["activeTaskId"]));
        Assert.True(result.Prompts["P2"].Actionable);
        Assert.Equal("BF-A", result.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", result.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.False(result.Prompts["P1"].Actionable);
        Assert.Equal("BF-A", result.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", result.Prompts["P1"].View?.RelatedSpellDuelId);
    }

    private static void AssertStackResolutionReturnsToActiveSpellDuelTaskAudit(ResolutionResult result)
    {
        var resolved = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Equal("STACK-SWIFT-A", resolved.Payload["stackItemId"]);
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P2", result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Empty(result.State.StackItems);
        Assert.Equal("BF-A", result.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("spell-duel:BF-A", result.State.SpellDuelState.SpellDuelId);
        Assert.Empty(result.State.SpellDuelState.StackItemIds);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-A", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_SPELL_DUEL", "START_BATTLE", "START_BATTLE"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());

        var queue = Assert.IsType<Dictionary<string, object?>>(result.Snapshots["P2"].Timing["pendingTaskQueue"]);
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-spell-duel:BF-A", Assert.IsType<string>(queue["activeTaskId"]));
        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(result.Snapshots["P2"].Timing["battlefieldTasks"]);
        var activeSpellDuelTask = Assert.Single(battlefieldTasks, task =>
            string.Equals(task["kind"] as string, "START_SPELL_DUEL", StringComparison.Ordinal)
            && string.Equals(task["status"] as string, "ACTIVE", StringComparison.Ordinal)
            && string.Equals(task["battlefieldObjectId"] as string, "BF-A", StringComparison.Ordinal));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(activeSpellDuelTask["stackItemIds"]));
        Assert.True(result.Prompts["P2"].Actionable);
        Assert.Contains(CommandTypes.PassFocus, result.Prompts["P2"].Actions);
        Assert.Equal(PromptTypes.SpellDuelFocus, result.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", result.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", result.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.False(result.Prompts["P1"].Actionable);
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P1"].Actions);
    }

    private static void AssertStaleFirstSpellDuelPromptReplayAudit(ResolutionResult result)
    {
        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Equal("BF-B", result.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("spell-duel:BF-B", result.State.SpellDuelState.SpellDuelId);
        Assert.Contains(BattlefieldTaskMarkers.SpellDuelCompleted("BF-A"), result.State.UntilEndOfTurnEffects);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-B", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Equal(
            ["BF-B", "BF-B", "BF-B"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.BattlefieldObjectId!).ToArray());
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.BattlefieldObjectId, "BF-A", StringComparison.Ordinal));

        var queue = Assert.IsType<Dictionary<string, object?>>(result.Snapshots["P1"].Timing["pendingTaskQueue"]);
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-spell-duel:BF-B", Assert.IsType<string>(queue["activeTaskId"]));
        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal("BF-B", result.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", result.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P2"].Actions);
    }

    private static void AssertReconnectSpellDuelTaskMetadataAudit(
        PlayerSessionDto reconnect,
        SnapshotDto snapshot,
        ActionPromptDto prompt)
    {
        Assert.Equal("P1", reconnect.PlayerId);
        Assert.False(string.IsNullOrWhiteSpace(reconnect.ReconnectToken));
        Assert.Equal("P1", snapshot.Timing["focusPlayerId"]);

        var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
        Assert.True(Assert.IsType<bool>(queue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(queue["isBlocking"]));
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-spell-duel:BF-A", Assert.IsType<string>(queue["activeTaskId"]));
        var queueTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_SPELL_DUEL", "START_BATTLE", "START_BATTLE"],
            queueTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.DoesNotContain("P2-HIDDEN-STANDBY", queueTasks.Select(task => task["objectId"] as string));

        var activeTask = Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]),
            task => string.Equals(task["kind"] as string, "START_SPELL_DUEL", StringComparison.Ordinal)
                && string.Equals(task["status"] as string, "ACTIVE", StringComparison.Ordinal)
                && string.Equals(task["battlefieldObjectId"] as string, "BF-A", StringComparison.Ordinal));
        Assert.Equal("BATTLEFIELD_CONTESTED", Assert.IsType<string>(activeTask["reason"]));
        Assert.Equal("P1", Assert.IsType<string>(activeTask["actingPlayerId"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["stackItemIds"]));

        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.PassFocus, prompt.Actions);
        Assert.Contains(CommandTypes.Surrender, prompt.Actions);
        Assert.Equal("BF-A", prompt.View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", prompt.View?.RelatedSpellDuelId);
    }

    private static void AssertReconnectBattleTaskMetadataAudit(
        PlayerSessionDto reconnect,
        SnapshotDto snapshot,
        ActionPromptDto prompt)
    {
        Assert.Equal("P1", reconnect.PlayerId);
        Assert.False(string.IsNullOrWhiteSpace(reconnect.ReconnectToken));
        var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
        Assert.True(Assert.IsType<bool>(queue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(queue["isBlocking"]));
        Assert.Equal("BATTLE_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-battle:BF-A", Assert.IsType<string>(queue["activeTaskId"]));
        var queueTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_SPELL_DUEL", "START_BATTLE", "START_BATTLE"],
            queueTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["BF-A", "BF-B", "BF-A", "BF-B", "BF-A", "BF-B"],
            queueTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());
        Assert.DoesNotContain("P2-HIDDEN-STANDBY", queueTasks.Select(task => task["objectId"] as string));

        var activeTask = Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]),
            task => string.Equals(task["kind"] as string, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task["battlefieldObjectId"] as string, "BF-A", StringComparison.Ordinal));
        Assert.Equal("PENDING", Assert.IsType<string>(activeTask["status"]));
        Assert.Equal("SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", Assert.IsType<string>(activeTask["reason"]));
        Assert.Equal("battle:BF-A", Assert.IsType<string>(activeTask["battleId"]));
        Assert.Null(activeTask["actingPlayerId"]);

        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.DeclareBattle, prompt.Actions);
        Assert.Contains(CommandTypes.Surrender, prompt.Actions);
        Assert.Equal(PromptTypes.BattleDeclaration, prompt.View?.Type);
        Assert.Equal("BF-A", prompt.View?.RelatedBattlefieldId);
        Assert.Equal("battle:BF-A", prompt.View?.RelatedBattleId);
    }

    private static void AssertSpellDuelCloseCleanupPromptQueueAudit(ResolutionResult result)
    {
        var events = result.Events;
        var focusPassedIndex = EventIndex(events, gameEvent =>
            string.Equals(gameEvent.Kind, "FOCUS_PASSED", StringComparison.Ordinal));
        var focusPassed = events[focusPassedIndex];
        Assert.Equal("P1", focusPassed.Payload["playerId"]);
        Assert.Equal("P1", focusPassed.Payload["focusPlayerId"]);

        var spellDuelClosedIndex = EventIndex(events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_CLOSED", StringComparison.Ordinal));
        var spellDuelClosed = events[spellDuelClosedIndex];
        Assert.Equal("P1", spellDuelClosed.Payload["turnPlayerId"]);
        Assert.Equal(["BF-A"], StringList(spellDuelClosed.Payload["completedBattlefieldObjectIds"]));

        var destroyedIndex = EventIndex(events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-A", StringComparison.Ordinal));
        var destroyed = events[destroyedIndex];
        Assert.Equal("SPELL_DUEL_CLEANUP", destroyed.Payload["sourceObjectId"]);
        Assert.Equal("P2", destroyed.Payload["ownerPlayerId"]);
        Assert.Equal("P1", destroyed.Payload["destroyedByPlayerId"]);
        Assert.Equal("GRAVEYARD", destroyed.Payload["destinationZone"]);
        Assert.Equal("LETHAL_DAMAGE", destroyed.Payload["reason"]);

        var contestedIndex = EventIndex(events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-B", StringComparison.Ordinal));
        var contested = events[contestedIndex];
        Assert.Equal("P1", contested.Payload["playerId"]);
        Assert.Equal("P1", contested.Payload["causedByPlayerId"]);
        Assert.Equal(["P1", "P2"], StringList(contested.Payload["participantControllerIds"]));
        Assert.Equal(["P1-B", "P2-B"], StringList(contested.Payload["participantObjectIds"]));

        var startedIndex = EventIndex(events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-B", StringComparison.Ordinal));
        var started = events[startedIndex];
        Assert.Equal("task:start-spell-duel:BF-B", started.Payload["taskId"]);
        Assert.Equal("BATTLEFIELD_CONTESTED", started.Payload["reason"]);
        Assert.Equal("P1", started.Payload["playerId"]);
        Assert.Equal("P1", started.Payload["focusPlayerId"]);
        Assert.Equal("P1", started.Payload["causedByPlayerId"]);
        Assert.Equal(["P1", "P2"], StringList(started.Payload["participantControllerIds"]));
        Assert.Equal(["P1-B", "P2-B"], StringList(started.Payload["participantObjectIds"]));

        Assert.True(focusPassedIndex < spellDuelClosedIndex);
        Assert.True(spellDuelClosedIndex < destroyedIndex);
        Assert.True(destroyedIndex < contestedIndex);
        Assert.True(contestedIndex < startedIndex);

        Assert.Equal(TimingStates.SpellDuelOpen, result.State.TimingState);
        Assert.Equal("P1", result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Equal("BF-B", result.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("spell-duel:BF-B", result.State.SpellDuelState.SpellDuelId);
        Assert.Contains(BattlefieldTaskMarkers.SpellDuelCompleted("BF-A"), result.State.UntilEndOfTurnEffects);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-B", result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Equal(
            ["cleanup:battlefield-contested:BF-B", "task:start-spell-duel:BF-B", "task:start-battle:BF-B"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.TaskId).ToArray());
        Assert.Equal(
            ["BF-B", "BF-B", "BF-B"],
            result.State.PendingTaskQueue.Tasks.Select(task => task.BattlefieldObjectId!).ToArray());
        Assert.DoesNotContain(
            result.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.BattlefieldObjectId, "BF-A", StringComparison.Ordinal));

        var activeSpellDuelTask = Assert.Single(
            result.State.BattlefieldTasks,
            task => string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-B", StringComparison.Ordinal));
        Assert.Equal("ACTIVE", activeSpellDuelTask.Status);
        Assert.Equal("BATTLEFIELD_CONTESTED", activeSpellDuelTask.Reason);
        Assert.Equal("P1", activeSpellDuelTask.ActingPlayerId);
        Assert.Equal(["P1", "P2"], activeSpellDuelTask.ParticipantControllerIds);
        Assert.Equal(["P1-B", "P2-B"], activeSpellDuelTask.ParticipantObjectIds);
        var waitingBattleTask = Assert.Single(
            result.State.BattlefieldTasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-B", StringComparison.Ordinal));
        Assert.Equal("WAITING_FOR_SPELL_DUEL", waitingBattleTask.Status);
        Assert.Equal("SPELL_DUEL_AFTER_BATTLEFIELD_CONTEST", waitingBattleTask.Reason);
        Assert.DoesNotContain(
            result.State.BattlefieldTasks,
            task => string.Equals(task.BattlefieldObjectId, "BF-A", StringComparison.Ordinal));

        var queue = Assert.IsType<Dictionary<string, object?>>(result.Snapshots["P1"].Timing["pendingTaskQueue"]);
        Assert.True(Assert.IsType<bool>(queue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(queue["isBlocking"]));
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-spell-duel:BF-B", Assert.IsType<string>(queue["activeTaskId"]));
        var queueTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            queueTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["cleanup:battlefield-contested:BF-B", "task:start-spell-duel:BF-B", "task:start-battle:BF-B"],
            queueTasks.Select(task => Assert.IsType<string>(task["taskId"])).ToArray());
        Assert.Equal(
            ["BF-B", "BF-B", "BF-B"],
            queueTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());

        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(result.Snapshots["P1"].Timing["battlefieldTasks"]);
        Assert.Equal(
            ["START_SPELL_DUEL", "START_BATTLE"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["ACTIVE", "WAITING_FOR_SPELL_DUEL"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["status"])).ToArray());
        Assert.Equal(
            ["BF-B", "BF-B"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());

        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(PromptTypes.SpellDuelFocus, result.Prompts["P1"].View?.Type);
        Assert.Equal("BF-B", result.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", result.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, result.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, result.Prompts["P1"].Actions);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.PassFocus, result.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.DeclareBattle, result.Prompts["P2"].Actions);

        var p1PromptJson = JsonSerializer.Serialize(result.Prompts["P1"]);
        Assert.DoesNotContain("P2-A", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("task:start-battle:BF-A", p1PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("spell-duel:BF-A", p1PromptJson, StringComparison.Ordinal);
    }

    private static void AssertMultiContestActiveSpellDuelTaskAudit(
        MatchState state,
        SnapshotDto snapshot,
        ActionPromptDto prompt)
    {
        Assert.Equal(TimingStates.SpellDuelOpen, state.TimingState);
        Assert.Equal("P1", state.FocusPlayerId);
        Assert.Empty(state.PassedFocusPlayerIds);
        Assert.Equal("BF-A", state.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("spell-duel:BF-A", state.SpellDuelState.SpellDuelId);
        Assert.Equal("SPELL_DUEL_TASKS", state.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-A", state.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_SPELL_DUEL", "START_BATTLE", "START_BATTLE"],
            state.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Equal(
            ["BF-A", "BF-B", "BF-A", "BF-B", "BF-A", "BF-B"],
            state.PendingTaskQueue.Tasks.Select(task => task.BattlefieldObjectId!).ToArray());
        Assert.Equal(
            [
                "cleanup:battlefield-contested:BF-A",
                "cleanup:battlefield-contested:BF-B",
                "task:start-spell-duel:BF-A",
                "task:start-spell-duel:BF-B",
                "task:start-battle:BF-A",
                "task:start-battle:BF-B"
            ],
            state.PendingTaskQueue.Tasks.Select(task => task.TaskId).ToArray());

        var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.True(Assert.IsType<bool>(queue["hasTasks"]));
        Assert.True(Assert.IsType<bool>(queue["isBlocking"]));
        Assert.Equal("task:start-spell-duel:BF-A", Assert.IsType<string>(queue["activeTaskId"]));
        var queueTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_SPELL_DUEL", "START_BATTLE", "START_BATTLE"],
            queueTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["BF-A", "BF-B", "BF-A", "BF-B", "BF-A", "BF-B"],
            queueTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());
        Assert.Equal(
            [
                "cleanup:battlefield-contested:BF-A",
                "cleanup:battlefield-contested:BF-B",
                "task:start-spell-duel:BF-A",
                "task:start-spell-duel:BF-B",
                "task:start-battle:BF-A",
                "task:start-battle:BF-B"
            ],
            queueTasks.Select(task => Assert.IsType<string>(task["taskId"])).ToArray());

        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]);
        Assert.Equal(
            ["START_SPELL_DUEL", "START_BATTLE", "START_SPELL_DUEL", "START_BATTLE"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["kind"])).ToArray());
        Assert.Equal(
            ["ACTIVE", "WAITING_FOR_SPELL_DUEL", "PENDING", "WAITING_FOR_SPELL_DUEL"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["status"])).ToArray());
        Assert.Equal(
            ["BF-A", "BF-A", "BF-B", "BF-B"],
            battlefieldTasks.Select(task => Assert.IsType<string>(task["battlefieldObjectId"])).ToArray());

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.SpellDuelFocus, prompt.View?.Type);
        Assert.Equal("BF-A", prompt.View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", prompt.View?.RelatedSpellDuelId);
        Assert.Equal(["PASS_FOCUS", "SURRENDER"], prompt.Actions);
        Assert.Contains(CommandTypes.PassFocus, prompt.Actions);
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

    private static void AssertOpponentHiddenStandbyRedacted(SnapshotDto snapshot, string hiddenObjectId)
    {
        var opponentZones = ZoneView(PlayerView(snapshot, "P2"));
        Assert.DoesNotContain(hiddenObjectId, StringList(opponentZones["battlefields"]));
        var opponentObjects = ObjectView(PlayerView(snapshot, "P2"));
        if (opponentObjects.TryGetValue(hiddenObjectId, out var hiddenObject))
        {
            var hiddenView = Assert.IsType<Dictionary<string, object?>>(hiddenObject);
            Assert.True(Assert.IsType<bool>(hiddenView["isFaceDown"]));
            Assert.DoesNotContain("power", hiddenView.Keys);
            Assert.DoesNotContain("tags", hiddenView.Keys);
            Assert.DoesNotContain("cardNo", hiddenView.Keys);
        }
    }

    private static MatchState IdleNeutralState()
    {
        return new MatchState(
            "spell-duel-battle-idle-room",
            1,
            1,
            "P1",
            Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: EmptyPools(),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            });
    }

    private static MatchState SpellDuelStackState()
    {
        var state = MultiContestSpellDuelState();
        return state with
        {
            TimingState = TimingStates.SpellDuelClosed,
            FocusPlayerId = null,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            PassedPriorityPlayerIds = ["P2"],
            StackItems =
            [
                new StackItemState(
                    "STACK-SWIFT-A",
                    "P1",
                    "P1-SWIFT-SOURCE",
                    "UNKNOWN_NOOP_EFFECT",
                    timingContext: TimingStates.SpellDuelOpen)
            ]
        };
    }

    private static MatchState StartBattleTaskState(bool includeOpponentHiddenStandby = false)
    {
        return MultiContestSpellDuelState(includeOpponentHiddenStandby: includeOpponentHiddenStandby) with
        {
            TimingState = TimingStates.NeutralOpen,
            FocusPlayerId = null,
            PassedFocusPlayerIds = [],
            ActivePlayerId = "P1",
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("BF-A")]
        };
    }

    private static MatchState MultiContestSpellDuelState(
        bool lethalFirstDefender = false,
        bool includeOpponentHiddenStandby = false,
        IReadOnlyList<string>? passedFocusPlayerIds = null)
    {
        var p2Battlefields = includeOpponentHiddenStandby
            ? new[] { "P2-A", "P2-B", "BF-HIDDEN", "P2-HIDDEN-STANDBY" }
            : ["P2-A", "P2-B"];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["BF-A"] = Battlefield("BF-A", "P1"),
            ["BF-B"] = Battlefield("BF-B", "P1"),
            ["P1-A"] = Unit("P1-A", "P1", power: 4),
            ["P2-A"] = Unit("P2-A", "P2", power: 3, damage: lethalFirstDefender ? 3 : 0),
            ["P1-B"] = Unit("P1-B", "P1", power: 2),
            ["P2-B"] = Unit("P2-B", "P2", power: 2)
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["BF-A"] = new("P1", "BATTLEFIELD", "BF-A"),
            ["BF-B"] = new("P1", "BATTLEFIELD", "BF-B"),
            ["P1-A"] = new("P1", "BATTLEFIELD", "BF-A"),
            ["P2-A"] = new("P2", "BATTLEFIELD", "BF-A"),
            ["P1-B"] = new("P1", "BATTLEFIELD", "BF-B"),
            ["P2-B"] = new("P2", "BATTLEFIELD", "BF-B")
        };
        if (includeOpponentHiddenStandby)
        {
            cardObjects["BF-HIDDEN"] = Battlefield("BF-HIDDEN", "P2");
            cardObjects["P2-HIDDEN-STANDBY"] = new CardObjectState(
                "P2-HIDDEN-STANDBY",
                isFaceDown: true,
                power: 1,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                ownerId: "P2",
                controllerId: "P2");
            objectLocations["BF-HIDDEN"] = new("P2", "BATTLEFIELD", "BF-HIDDEN");
            objectLocations["P2-HIDDEN-STANDBY"] = new("P2", "BATTLEFIELD", "BF-HIDDEN");
        }

        return new MatchState(
            "spell-duel-battle-state-machine-room",
            10,
            3,
            "P1",
            Seats(),
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
                    Battlefields = ["BF-A", "BF-B", "P1-A", "P1-B"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            cardObjects: cardObjects,
            focusPlayerId: "P1",
            passedFocusPlayerIds: passedFocusPlayerIds ?? [],
            objectLocations: objectLocations);
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
        int power,
        int damage = 0)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·125/221",
            damage: damage,
            power: power,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }

    private static Dictionary<string, object?> PlayerView(SnapshotDto snapshot, string playerId)
    {
        return Assert.IsType<Dictionary<string, object?>>(snapshot.Players[playerId]);
    }

    private static Dictionary<string, object?> ZoneView(Dictionary<string, object?> player)
    {
        return Assert.IsType<Dictionary<string, object?>>(player["zones"]);
    }

    private static Dictionary<string, object?> ObjectView(Dictionary<string, object?> player)
    {
        return Assert.IsType<Dictionary<string, object?>>(player["objects"]);
    }

    private static IReadOnlyList<string> StringList(object? value)
    {
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(value);
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
}
