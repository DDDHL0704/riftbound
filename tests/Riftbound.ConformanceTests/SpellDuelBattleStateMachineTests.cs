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
    public async Task PassPriorityByNonPriorityPlayerOrWrongTimingRejectsWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var command = new PassPriorityCommand();
        var stackState = SpellDuelStackState();

        var nonPriorityResult = await engine.ResolveAsync(
            stackState,
            new PlayerIntent("intent-non-priority-pass-priority", "P2", CommandTypes.PassPriority),
            command,
            CancellationToken.None);

        AssertRejectedWithoutMutation(stackState, nonPriorityResult, ErrorCodes.PhaseNotAllowed);
        AssertSpellDuelStackPriorityRejectedWithoutMutationAudit(nonPriorityResult);

        var neutralState = IdleNeutralState();
        var neutralResult = await engine.ResolveAsync(
            neutralState,
            new PlayerIntent("intent-neutral-pass-priority", "P1", CommandTypes.PassPriority),
            command,
            CancellationToken.None);

        AssertRejectedWithoutMutation(neutralState, neutralResult, ErrorCodes.PhaseNotAllowed);
        AssertWrongTimingPassPriorityRejectionAudit(neutralResult);

        var focusState = MultiContestSpellDuelState();
        var focusResult = await engine.ResolveAsync(
            focusState,
            new PlayerIntent("intent-spell-duel-focus-pass-priority", "P1", CommandTypes.PassPriority),
            command,
            CancellationToken.None);

        AssertRejectedWithoutMutation(focusState, focusResult, ErrorCodes.PhaseNotAllowed);
        AssertMultiContestActiveSpellDuelTaskAudit(
            focusResult.State,
            focusResult.Snapshots["P1"],
            focusResult.Prompts["P1"]);
        Assert.DoesNotContain(CommandTypes.PassPriority, focusResult.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, focusResult.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, focusResult.Prompts["P2"].Actions);
    }

    [Fact]
    public async Task PassFocusDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = MultiContestSpellDuelState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.SpellDuelFocus, prompt.View?.Type);
        Assert.Equal("spell-duel:BF-A", prompt.View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, prompt.Actions);

        var command = new PassFocusCommand();
        var rawCommand = PromptScopedRawCommand(CommandTypes.PassFocus, prompt);
        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PassFocus,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        const string clientIntentId = "intent-pass-focus-raw-duplicate";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["FOCUS_PASSED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal("P2", accepted.State.FocusPlayerId);
        Assert.Equal(["P1"], accepted.State.PassedFocusPlayerIds);
        Assert.Equal(PromptTypes.SpellDuelFocus, accepted.Prompts["P2"].View?.Type);
        Assert.Contains(CommandTypes.PassFocus, accepted.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, accepted.Prompts["P1"].Actions);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.PassFocus, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PassFocus, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var duplicate = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Null(duplicate.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicate.State));
        Assert.Equal(accepted.State.Tick, duplicate.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(duplicate.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicate.Prompts));
        Assert.Equal(PromptTypes.SpellDuelFocus, duplicate.Prompts["P2"].View?.Type);
        Assert.Contains(CommandTypes.PassFocus, duplicate.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, duplicate.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Single(journal.Entries);

        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(PromptTypes.SpellDuelFocus, conflict.Prompts["P2"].View?.Type);
        Assert.Contains(CommandTypes.PassFocus, conflict.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, conflict.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PassFocusStalePromptReplayAfterFocusHandoffRecordsRejectedJournalWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = MultiContestSpellDuelState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.SpellDuelFocus, prompt.View?.Type);
        Assert.Equal("BF-A", prompt.View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", prompt.View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, prompt.Actions);

        var command = new PassFocusCommand();
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.PassFocus, prompt);
        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PassFocus,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        const string acceptedIntentId = "intent-pass-focus-before-stale-raw-journal";
        const string staleIntentId = "intent-stale-pass-focus-handoff-raw-journal";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["FOCUS_PASSED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(TimingStates.SpellDuelOpen, accepted.State.TimingState);
        Assert.Equal("P2", accepted.State.FocusPlayerId);
        Assert.Equal(["P1"], accepted.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", accepted.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", accepted.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, accepted.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", accepted.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", accepted.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, accepted.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, accepted.Prompts["P1"].Actions);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PassFocus, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PassFocus, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(TimingStates.SpellDuelOpen, replay.State.TimingState);
        Assert.Equal("P2", replay.State.FocusPlayerId);
        Assert.Equal(["P1"], replay.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", replay.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, replay.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", replay.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", replay.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, replay.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, replay.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PassFocus, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PassFocus, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(accepted.State.Tick, rejectedJournalEntry.StartedTick);
        Assert.Equal(replay.State.Tick, rejectedJournalEntry.CompletedTick);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(TimingStates.SpellDuelOpen, rejectedJournalEntry.AuthoritativeState.TimingState);
        Assert.Equal("P2", rejectedJournalEntry.AuthoritativeState.FocusPlayerId);
        Assert.Equal(["P1"], rejectedJournalEntry.AuthoritativeState.PassedFocusPlayerIds);
        Assert.Equal("BF-A", rejectedJournalEntry.AuthoritativeState.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", rejectedJournalEntry.AuthoritativeState.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.Equal(PromptTypes.SpellDuelFocus, rejectedJournalEntry.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", rejectedJournalEntry.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", rejectedJournalEntry.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, rejectedJournalEntry.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, rejectedJournalEntry.Prompts["P1"].Actions);
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var duplicateRejected = await session.SubmitAsync(
            "P1",
            staleIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateRejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateRejected.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateRejected.ErrorMessage);
        Assert.Empty(duplicateRejected.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(replay.State.Tick, duplicateRejected.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        Assert.Equal(TimingStates.SpellDuelOpen, duplicateRejected.State.TimingState);
        Assert.Equal("P2", duplicateRejected.State.FocusPlayerId);
        Assert.Equal(["P1"], duplicateRejected.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", duplicateRejected.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", duplicateRejected.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, duplicateRejected.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", duplicateRejected.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", duplicateRejected.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, duplicateRejected.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, duplicateRejected.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);

        var conflict = await session.SubmitAsync(
            "P1",
            staleIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(TimingStates.SpellDuelOpen, conflict.State.TimingState);
        Assert.Equal("P2", conflict.State.FocusPlayerId);
        Assert.Equal(["P1"], conflict.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", conflict.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", conflict.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, conflict.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", conflict.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", conflict.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, conflict.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, conflict.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PassFocusSecondPlayerClosingSpellDuelStalePromptReplayRecordsRejectedJournalWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = MultiContestSpellDuelState(lethalFirstDefender: true);
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var p1Prompt = session.PromptFor("P1");
        Assert.True(p1Prompt.Actionable);
        Assert.Equal(PromptTypes.SpellDuelFocus, p1Prompt.View?.Type);
        Assert.Equal("BF-A", p1Prompt.View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", p1Prompt.View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, p1Prompt.Actions);

        var p1Pass = await session.SubmitAsync(
            "P1",
            "intent-pass-focus-to-second-player-before-close",
            new PassFocusCommand(),
            PromptScopedRawCommand(CommandTypes.PassFocus, p1Prompt),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.Null(p1Pass.ErrorCode);
        Assert.Equal(["FOCUS_PASSED"], p1Pass.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(TimingStates.SpellDuelOpen, p1Pass.State.TimingState);
        Assert.Equal("P2", p1Pass.State.FocusPlayerId);
        Assert.Equal(["P1"], p1Pass.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", p1Pass.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal(PromptTypes.SpellDuelFocus, p1Pass.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", p1Pass.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", p1Pass.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, p1Pass.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, p1Pass.Prompts["P1"].Actions);
        Assert.Single(journal.Entries);

        var p2Prompt = session.PromptFor("P2");
        Assert.True(p2Prompt.Actionable);
        Assert.Equal(PromptTypes.SpellDuelFocus, p2Prompt.View?.Type);
        Assert.Equal("BF-A", p2Prompt.View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", p2Prompt.View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, p2Prompt.Actions);

        var command = new PassFocusCommand();
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.PassFocus, p2Prompt);
        var changedRawCommand = PromptScopedRawCommandWithClientNote(
            CommandTypes.PassFocus,
            p2Prompt,
            "changed-payload");
        const string acceptedIntentId = "intent-p2-close-first-spell-duel-before-stale-raw-journal";
        const string staleIntentId = "intent-stale-p2-close-first-spell-duel-raw-journal";

        var accepted = await session.SubmitAsync(
            "P2",
            acceptedIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(
            ["FOCUS_PASSED", "SPELL_DUEL_CLOSED", "UNIT_DESTROYED", "BATTLEFIELD_CONTROL_RESOLVED", "BATTLEFIELD_CONTESTED", "SPELL_DUEL_STARTED"],
            accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Contains("P2-A", accepted.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("P2-A", accepted.State.PlayerZones["P2"].Battlefields);
        Assert.Contains(BattlefieldTaskMarkers.SpellDuelCompleted("BF-A"), accepted.State.UntilEndOfTurnEffects);
        Assert.Equal(TimingStates.SpellDuelOpen, accepted.State.TimingState);
        Assert.Equal("P2", accepted.State.FocusPlayerId);
        Assert.Empty(accepted.State.PassedFocusPlayerIds);
        Assert.Equal("BF-B", accepted.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", accepted.State.PendingTaskQueue.ActiveTaskId);
        Assert.DoesNotContain(
            accepted.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.BattlefieldObjectId, "BF-A", StringComparison.Ordinal));
        Assert.Equal(PromptTypes.SpellDuelFocus, accepted.Prompts["P2"].View?.Type);
        Assert.Equal("BF-B", accepted.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", accepted.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, accepted.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, accepted.Prompts["P1"].Actions);

        var focusPassed = accepted.Events[EventIndex(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "FOCUS_PASSED", StringComparison.Ordinal))];
        Assert.Equal("P2", focusPassed.Payload["playerId"]);
        Assert.Equal("P2", focusPassed.Payload["focusPlayerId"]);
        var spellDuelClosed = accepted.Events[EventIndex(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_CLOSED", StringComparison.Ordinal))];
        Assert.Equal("P1", spellDuelClosed.Payload["turnPlayerId"]);
        Assert.Equal(["BF-A"], StringList(spellDuelClosed.Payload["completedBattlefieldObjectIds"]));
        var destroyed = accepted.Events[EventIndex(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-A", StringComparison.Ordinal))];
        Assert.Equal("SPELL_DUEL_CLEANUP", destroyed.Payload["sourceObjectId"]);
        Assert.Equal("P2", destroyed.Payload["ownerPlayerId"]);
        Assert.Equal("P1", destroyed.Payload["destroyedByPlayerId"]);
        Assert.Equal("GRAVEYARD", destroyed.Payload["destinationZone"]);
        Assert.Equal("LETHAL_DAMAGE", destroyed.Payload["reason"]);
        var contested = accepted.Events[EventIndex(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-B", StringComparison.Ordinal))];
        Assert.Equal("P2", contested.Payload["playerId"]);
        Assert.Equal("P2", contested.Payload["causedByPlayerId"]);
        Assert.Equal(["P1", "P2"], StringList(contested.Payload["participantControllerIds"]));
        Assert.Equal(["P1-B", "P2-B"], StringList(contested.Payload["participantObjectIds"]));
        var started = accepted.Events[EventIndex(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, "BF-B", StringComparison.Ordinal))];
        Assert.Equal("task:start-spell-duel:BF-B", started.Payload["taskId"]);
        Assert.Equal("BATTLEFIELD_CONTESTED", started.Payload["reason"]);
        Assert.Equal("P2", started.Payload["playerId"]);
        Assert.Equal("P2", started.Payload["focusPlayerId"]);
        Assert.Equal("P2", started.Payload["causedByPlayerId"]);
        Assert.Equal(["P1", "P2"], StringList(started.Payload["participantControllerIds"]));
        Assert.Equal(["P1-B", "P2-B"], StringList(started.Payload["participantObjectIds"]));

        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        Assert.Equal(2, journal.Entries.Count);
        var acceptedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P2", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PassFocus, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PassFocus, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(p2Prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(p2Prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));

        var replay = await session.SubmitAsync(
            "P2",
            staleIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(TimingStates.SpellDuelOpen, replay.State.TimingState);
        Assert.Equal("P2", replay.State.FocusPlayerId);
        Assert.Empty(replay.State.PassedFocusPlayerIds);
        Assert.Equal("BF-B", replay.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, replay.Prompts["P2"].View?.Type);
        Assert.Equal("BF-B", replay.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", replay.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, replay.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, replay.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        Assert.Equal(3, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[2];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P2", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PassFocus, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PassFocus, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(p2Prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(p2Prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(accepted.State.Tick, rejectedJournalEntry.StartedTick);
        Assert.Equal(replay.State.Tick, rejectedJournalEntry.CompletedTick);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(TimingStates.SpellDuelOpen, rejectedJournalEntry.AuthoritativeState.TimingState);
        Assert.Equal("P2", rejectedJournalEntry.AuthoritativeState.FocusPlayerId);
        Assert.Empty(rejectedJournalEntry.AuthoritativeState.PassedFocusPlayerIds);
        Assert.Equal("BF-B", rejectedJournalEntry.AuthoritativeState.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", rejectedJournalEntry.AuthoritativeState.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.Equal(PromptTypes.SpellDuelFocus, rejectedJournalEntry.Prompts["P2"].View?.Type);
        Assert.Equal("BF-B", rejectedJournalEntry.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", rejectedJournalEntry.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, rejectedJournalEntry.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, rejectedJournalEntry.Prompts["P1"].Actions);
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

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
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(replay.State.Tick, duplicateRejected.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        Assert.Equal(TimingStates.SpellDuelOpen, duplicateRejected.State.TimingState);
        Assert.Equal("P2", duplicateRejected.State.FocusPlayerId);
        Assert.Empty(duplicateRejected.State.PassedFocusPlayerIds);
        Assert.Equal("BF-B", duplicateRejected.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", duplicateRejected.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, duplicateRejected.Prompts["P2"].View?.Type);
        Assert.Equal("BF-B", duplicateRejected.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", duplicateRejected.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, duplicateRejected.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, duplicateRejected.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(3, journal.Entries.Count);

        var conflict = await session.SubmitAsync(
            "P2",
            staleIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(TimingStates.SpellDuelOpen, conflict.State.TimingState);
        Assert.Equal("P2", conflict.State.FocusPlayerId);
        Assert.Empty(conflict.State.PassedFocusPlayerIds);
        Assert.Equal("BF-B", conflict.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", conflict.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, conflict.Prompts["P2"].View?.Type);
        Assert.Equal("BF-B", conflict.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", conflict.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, conflict.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, conflict.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(3, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PassFocusClosingSpellDuelDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = MultiContestSpellDuelState(lethalFirstDefender: true, passedFocusPlayerIds: ["P2"]);
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.SpellDuelFocus, prompt.View?.Type);
        Assert.Equal("BF-A", prompt.View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", prompt.View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, prompt.Actions);

        var command = new PassFocusCommand();
        var rawCommand = PromptScopedRawCommand(CommandTypes.PassFocus, prompt);
        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PassFocus,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        const string clientIntentId = "intent-pass-focus-close-raw-duplicate";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(
            ["FOCUS_PASSED", "SPELL_DUEL_CLOSED", "UNIT_DESTROYED", "BATTLEFIELD_CONTROL_RESOLVED", "BATTLEFIELD_CONTESTED", "SPELL_DUEL_STARTED"],
            accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Contains("P2-A", accepted.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain("P2-A", accepted.State.PlayerZones["P2"].Battlefields);
        Assert.Contains(BattlefieldTaskMarkers.SpellDuelCompleted("BF-A"), accepted.State.UntilEndOfTurnEffects);
        Assert.Equal(TimingStates.SpellDuelOpen, accepted.State.TimingState);
        Assert.Equal("P1", accepted.State.FocusPlayerId);
        Assert.Equal("BF-B", accepted.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", accepted.State.PendingTaskQueue.ActiveTaskId);
        Assert.DoesNotContain(
            accepted.State.PendingTaskQueue.Tasks,
            task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                && string.Equals(task.BattlefieldObjectId, "BF-A", StringComparison.Ordinal));
        Assert.Equal(PromptTypes.SpellDuelFocus, accepted.Prompts["P1"].View?.Type);
        Assert.Equal("BF-B", accepted.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", accepted.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, accepted.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, accepted.Prompts["P2"].Actions);
        AssertSpellDuelCloseCleanupPromptQueueAudit(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.PassFocus, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PassFocus, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var duplicate = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Null(duplicate.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicate.State));
        Assert.Equal(accepted.State.Tick, duplicate.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(duplicate.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicate.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(duplicate.Snapshots));
        Assert.Equal(TimingStates.SpellDuelOpen, duplicate.State.TimingState);
        Assert.Equal("P1", duplicate.State.FocusPlayerId);
        Assert.Equal("BF-B", duplicate.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", duplicate.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, duplicate.Prompts["P1"].View?.Type);
        Assert.Equal("spell-duel:BF-B", duplicate.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, duplicate.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, duplicate.Prompts["P2"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Single(journal.Entries);

        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(TimingStates.SpellDuelOpen, conflict.State.TimingState);
        Assert.Equal("P1", conflict.State.FocusPlayerId);
        Assert.Equal("BF-B", conflict.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", conflict.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, conflict.Prompts["P1"].View?.Type);
        Assert.Equal("spell-duel:BF-B", conflict.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, conflict.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, conflict.Prompts["P2"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PassPriorityDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = SpellDuelStackState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.StackPriority, prompt.View?.Type);
        Assert.Contains(CommandTypes.PassPriority, prompt.Actions);

        var command = new PassPriorityCommand();
        var rawCommand = PromptScopedRawCommand(CommandTypes.PassPriority, prompt);
        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PassPriority,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        const string clientIntentId = "intent-pass-priority-raw-duplicate";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Empty(accepted.State.StackItems);
        Assert.Equal(TimingStates.SpellDuelOpen, accepted.State.TimingState);
        Assert.Equal("P2", accepted.State.FocusPlayerId);
        Assert.Empty(accepted.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", accepted.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", accepted.State.PendingTaskQueue.ActiveTaskId);
        Assert.Contains(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["stackItemId"] as string, "STACK-SWIFT-A", StringComparison.Ordinal));
        Assert.Equal(PromptTypes.SpellDuelFocus, accepted.Prompts["P2"].View?.Type);
        Assert.Contains(CommandTypes.PassFocus, accepted.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, accepted.Prompts["P1"].Actions);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.PassPriority, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PassPriority, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var duplicate = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Null(duplicate.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicate.State));
        Assert.Equal(accepted.State.Tick, duplicate.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(duplicate.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicate.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(duplicate.Snapshots));
        Assert.Empty(duplicate.State.StackItems);
        Assert.Equal(TimingStates.SpellDuelOpen, duplicate.State.TimingState);
        Assert.Equal("P2", duplicate.State.FocusPlayerId);
        Assert.Equal(PromptTypes.SpellDuelFocus, duplicate.Prompts["P2"].View?.Type);
        Assert.Contains(CommandTypes.PassFocus, duplicate.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, duplicate.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Single(journal.Entries);

        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Empty(conflict.State.StackItems);
        Assert.Equal(TimingStates.SpellDuelOpen, conflict.State.TimingState);
        Assert.Equal("P2", conflict.State.FocusPlayerId);
        Assert.Equal(PromptTypes.SpellDuelFocus, conflict.Prompts["P2"].View?.Type);
        Assert.Contains(CommandTypes.PassFocus, conflict.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, conflict.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PassPriorityStalePromptReplayAfterStackResolvesRecordsRejectedJournalWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = SpellDuelStackState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.StackPriority, prompt.View?.Type);
        Assert.Equal("STACK-SWIFT-A", prompt.View?.RelatedStackItemId);
        Assert.Contains(CommandTypes.PassPriority, prompt.Actions);

        var command = new PassPriorityCommand();
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.PassPriority, prompt);
        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PassPriority,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        const string acceptedIntentId = "intent-pass-priority-before-stale-raw-journal";
        const string staleIntentId = "intent-stale-pass-priority-raw-journal";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Empty(accepted.State.StackItems);
        Assert.Empty(accepted.State.SpellDuelState.StackItemIds);
        Assert.Equal(TimingStates.SpellDuelOpen, accepted.State.TimingState);
        Assert.Equal("P2", accepted.State.FocusPlayerId);
        Assert.Empty(accepted.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", accepted.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", accepted.State.PendingTaskQueue.ActiveTaskId);
        Assert.Contains(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["stackItemId"] as string, "STACK-SWIFT-A", StringComparison.Ordinal));
        Assert.Equal(PromptTypes.SpellDuelFocus, accepted.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", accepted.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", accepted.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, accepted.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, accepted.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, accepted.Prompts["P1"].Actions);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PassPriority, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PassPriority, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));

        var replay = await session.SubmitAsync(
            "P1",
            staleIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Empty(replay.State.StackItems);
        Assert.Empty(replay.State.SpellDuelState.StackItemIds);
        Assert.Equal(TimingStates.SpellDuelOpen, replay.State.TimingState);
        Assert.Equal("P2", replay.State.FocusPlayerId);
        Assert.Empty(replay.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", replay.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, replay.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", replay.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", replay.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, replay.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, replay.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, replay.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PassPriority, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PassPriority, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(accepted.State.Tick, rejectedJournalEntry.StartedTick);
        Assert.Equal(replay.State.Tick, rejectedJournalEntry.CompletedTick);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Empty(rejectedJournalEntry.AuthoritativeState.StackItems);
        Assert.Empty(rejectedJournalEntry.AuthoritativeState.SpellDuelState.StackItemIds);
        Assert.Equal(TimingStates.SpellDuelOpen, rejectedJournalEntry.AuthoritativeState.TimingState);
        Assert.Equal("P2", rejectedJournalEntry.AuthoritativeState.FocusPlayerId);
        Assert.Equal("BF-A", rejectedJournalEntry.AuthoritativeState.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", rejectedJournalEntry.AuthoritativeState.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.Equal(PromptTypes.SpellDuelFocus, rejectedJournalEntry.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", rejectedJournalEntry.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", rejectedJournalEntry.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, rejectedJournalEntry.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, rejectedJournalEntry.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, rejectedJournalEntry.Prompts["P1"].Actions);
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var duplicateRejected = await session.SubmitAsync(
            "P1",
            staleIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateRejected.Accepted);
        Assert.Equal(replay.ErrorCode, duplicateRejected.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateRejected.ErrorMessage);
        Assert.Empty(duplicateRejected.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(replay.State.Tick, duplicateRejected.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        Assert.Empty(duplicateRejected.State.StackItems);
        Assert.Empty(duplicateRejected.State.SpellDuelState.StackItemIds);
        Assert.Equal(TimingStates.SpellDuelOpen, duplicateRejected.State.TimingState);
        Assert.Equal("P2", duplicateRejected.State.FocusPlayerId);
        Assert.Empty(duplicateRejected.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", duplicateRejected.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", duplicateRejected.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, duplicateRejected.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", duplicateRejected.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", duplicateRejected.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, duplicateRejected.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, duplicateRejected.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, duplicateRejected.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);

        var conflict = await session.SubmitAsync(
            "P1",
            staleIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Empty(conflict.State.StackItems);
        Assert.Empty(conflict.State.SpellDuelState.StackItemIds);
        Assert.Equal(TimingStates.SpellDuelOpen, conflict.State.TimingState);
        Assert.Equal("P2", conflict.State.FocusPlayerId);
        Assert.Empty(conflict.State.PassedFocusPlayerIds);
        Assert.Equal("BF-A", conflict.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-A", conflict.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(PromptTypes.SpellDuelFocus, conflict.Prompts["P2"].View?.Type);
        Assert.Equal("BF-A", conflict.Prompts["P2"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", conflict.Prompts["P2"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, conflict.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, conflict.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, conflict.Prompts["P1"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
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
            ["FOCUS_PASSED", "SPELL_DUEL_CLOSED", "UNIT_DESTROYED", "BATTLEFIELD_CONTROL_RESOLVED", "BATTLEFIELD_CONTESTED", "SPELL_DUEL_STARTED"],
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
            ["FOCUS_PASSED", "SPELL_DUEL_CLOSED", "UNIT_DESTROYED", "BATTLEFIELD_CONTROL_RESOLVED", "BATTLEFIELD_CONTESTED", "SPELL_DUEL_STARTED"],
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
    public async Task SpellDuelFocusStalePromptReplayAfterNextContestStartsRecordsRejectedJournalWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = MultiContestSpellDuelState(lethalFirstDefender: true, passedFocusPlayerIds: ["P2"]);
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.SpellDuelFocus, prompt.View?.Type);
        Assert.Equal("BF-A", prompt.View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", prompt.View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, prompt.Actions);

        var command = new PassFocusCommand();
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.PassFocus, prompt);
        var changedRawCommand = PromptScopedRawCommandWithClientNote(
            CommandTypes.PassFocus,
            prompt,
            "changed-payload");
        const string acceptedIntentId = "intent-close-first-spell-duel-before-stale-raw-journal";
        const string staleIntentId = "intent-stale-first-spell-duel-raw-journal";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(
            ["FOCUS_PASSED", "SPELL_DUEL_CLOSED", "UNIT_DESTROYED", "BATTLEFIELD_CONTROL_RESOLVED", "BATTLEFIELD_CONTESTED", "SPELL_DUEL_STARTED"],
            accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(TimingStates.SpellDuelOpen, accepted.State.TimingState);
        Assert.Equal("BF-B", accepted.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", accepted.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal("P1", accepted.State.FocusPlayerId);
        Assert.Equal(PromptTypes.SpellDuelFocus, accepted.Prompts["P1"].View?.Type);
        Assert.Equal("BF-B", accepted.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", accepted.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, accepted.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, accepted.Prompts["P2"].Actions);
        AssertSpellDuelCloseCleanupPromptQueueAudit(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PassFocus, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PassFocus, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));

        var replay = await session.SubmitAsync(
            "P1",
            staleIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(TimingStates.SpellDuelOpen, replay.State.TimingState);
        Assert.Equal("BF-B", replay.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal("P1", replay.State.FocusPlayerId);
        Assert.Equal(PromptTypes.SpellDuelFocus, replay.Prompts["P1"].View?.Type);
        Assert.Equal("BF-B", replay.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", replay.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, replay.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, replay.Prompts["P2"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PassFocus, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PassFocus, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(accepted.State.Tick, rejectedJournalEntry.StartedTick);
        Assert.Equal(replay.State.Tick, rejectedJournalEntry.CompletedTick);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal("BF-B", rejectedJournalEntry.AuthoritativeState.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", rejectedJournalEntry.AuthoritativeState.PendingTaskQueue.ActiveTaskId);
        Assert.Equal("P1", rejectedJournalEntry.AuthoritativeState.FocusPlayerId);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.Equal(PromptTypes.SpellDuelFocus, rejectedJournalEntry.Prompts["P1"].View?.Type);
        Assert.Equal("BF-B", rejectedJournalEntry.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", rejectedJournalEntry.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, rejectedJournalEntry.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, rejectedJournalEntry.Prompts["P2"].Actions);
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var duplicateRejected = await session.SubmitAsync(
            "P1",
            staleIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateRejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateRejected.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateRejected.ErrorMessage);
        Assert.Empty(duplicateRejected.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(replay.State.Tick, duplicateRejected.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        Assert.Equal(TimingStates.SpellDuelOpen, duplicateRejected.State.TimingState);
        Assert.Equal("BF-B", duplicateRejected.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", duplicateRejected.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal("P1", duplicateRejected.State.FocusPlayerId);
        Assert.Equal(PromptTypes.SpellDuelFocus, duplicateRejected.Prompts["P1"].View?.Type);
        Assert.Equal("BF-B", duplicateRejected.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", duplicateRejected.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, duplicateRejected.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, duplicateRejected.Prompts["P2"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);

        var conflict = await session.SubmitAsync(
            "P1",
            staleIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(TimingStates.SpellDuelOpen, conflict.State.TimingState);
        Assert.Equal("BF-B", conflict.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("task:start-spell-duel:BF-B", conflict.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal("P1", conflict.State.FocusPlayerId);
        Assert.Equal(PromptTypes.SpellDuelFocus, conflict.Prompts["P1"].View?.Type);
        Assert.Equal("BF-B", conflict.Prompts["P1"].View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-B", conflict.Prompts["P1"].View?.RelatedSpellDuelId);
        Assert.Contains(CommandTypes.PassFocus, conflict.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, conflict.Prompts["P2"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
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
        var snapshotJson = JsonSerializer.Serialize(snapshot);
        var promptJson = JsonSerializer.Serialize(prompt);
        Assert.Contains("task:start-spell-duel:BF-A", snapshotJson, StringComparison.Ordinal);
        Assert.DoesNotContain("P2-HIDDEN-STANDBY", snapshotJson, StringComparison.Ordinal);
        Assert.DoesNotContain("P2-HIDDEN-STANDBY", promptJson, StringComparison.Ordinal);
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
    public void ReconnectDuringSpellDuelTasksPreservesOwnerHiddenStandbyAndNonFocusPrompt()
    {
        var state = MultiContestSpellDuelState(includeOpponentHiddenStandby: true);
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        session.EnsurePlayer("P1");
        var p2 = session.EnsurePlayer("P2");

        var reconnect = session.ReconnectPlayer("P2", p2.ReconnectToken);
        var snapshot = session.SnapshotFor("P2");
        var prompt = session.PromptFor("P2");
        var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);

        Assert.Equal("P2", reconnect.PlayerId);
        Assert.False(string.IsNullOrWhiteSpace(reconnect.ReconnectToken));
        Assert.Equal("P1", snapshot.Timing["focusPlayerId"]);
        Assert.Equal("SPELL_DUEL_TASKS", Assert.IsType<string>(queue["phase"]));
        Assert.Equal("task:start-spell-duel:BF-A", Assert.IsType<string>(queue["activeTaskId"]));

        var ownerZones = ZoneView(PlayerView(snapshot, "P2"));
        Assert.Contains("P2-HIDDEN-STANDBY", StringList(ownerZones["battlefields"]));
        Assert.Equal(0, Assert.IsType<int>(ownerZones["battlefieldHiddenStandbyCount"]));

        var ownerObjects = ObjectView(PlayerView(snapshot, "P2"));
        var ownerHiddenStandby = Assert.IsType<Dictionary<string, object?>>(ownerObjects["P2-HIDDEN-STANDBY"]);
        Assert.True(Assert.IsType<bool>(ownerHiddenStandby["isFaceDown"]));
        Assert.Equal(1, Assert.IsType<int>(ownerHiddenStandby["power"]));
        Assert.Equal("P2", Assert.IsType<string>(ownerHiddenStandby["ownerId"]));
        Assert.Equal("P2", Assert.IsType<string>(ownerHiddenStandby["controllerId"]));
        Assert.True(ownerHiddenStandby.ContainsKey("cardNo"));
        var ownerHiddenTags = Assert.IsAssignableFrom<IReadOnlyList<string>>(ownerHiddenStandby["tags"]);
        Assert.Contains(CardObjectTags.UnitCard, ownerHiddenTags);
        Assert.Contains(CardObjectTags.Standby, ownerHiddenTags);

        var battlefieldStates = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Lanes["battlefields"]);
        var hiddenBattlefield = Assert.Single(battlefieldStates, battlefield =>
            string.Equals(battlefield["battlefieldObjectId"] as string, "BF-HIDDEN", StringComparison.Ordinal));
        Assert.Contains("P2-HIDDEN-STANDBY", StringList(hiddenBattlefield["standbyObjectIds"]));
        Assert.Equal(0, Assert.IsType<int>(hiddenBattlefield["hiddenStandbyCount"]));
        var standbySlots = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(hiddenBattlefield["standbySlots"]);
        var ownerStandbySlot = Assert.Single(standbySlots, slot =>
            string.Equals(slot["objectId"] as string, "P2-HIDDEN-STANDBY", StringComparison.Ordinal));
        Assert.True(Assert.IsType<bool>(ownerStandbySlot["visible"]));
        Assert.Equal("VISIBLE", Assert.IsType<string>(ownerStandbySlot["state"]));
        Assert.True(Assert.IsType<bool>(ownerStandbySlot["isFaceDown"]));

        var battlefieldTasks = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(snapshot.Timing["battlefieldTasks"]);
        var activeTask = Assert.Single(battlefieldTasks, task =>
            string.Equals(task["kind"] as string, "START_SPELL_DUEL", StringComparison.Ordinal)
            && string.Equals(task["status"] as string, "ACTIVE", StringComparison.Ordinal));
        Assert.Equal("BF-A", Assert.IsType<string>(activeTask["battlefieldObjectId"]));
        Assert.Equal("spell-duel:BF-A", Assert.IsType<string>(activeTask["spellDuelId"]));
        Assert.Equal(["P1", "P2"], Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantControllerIds"]));
        Assert.Equal(["P1-A", "P2-A"], Assert.IsAssignableFrom<IReadOnlyList<string>>(activeTask["participantObjectIds"]));

        Assert.False(prompt.Actionable);
        Assert.Equal(PromptTypes.SpellDuelFocus, prompt.View?.Type);
        Assert.Equal("BF-A", prompt.View?.RelatedBattlefieldId);
        Assert.Equal("spell-duel:BF-A", prompt.View?.RelatedSpellDuelId);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], prompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, prompt.Actions);
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

    private static void AssertSpellDuelStackPriorityRejectedWithoutMutationAudit(ResolutionResult result)
    {
        Assert.Equal(TimingStates.SpellDuelClosed, result.State.TimingState);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Equal(["P2"], result.State.PassedPriorityPlayerIds);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("STACK-SWIFT-A", stackItem.StackItemId);
        Assert.Equal("P1", stackItem.ControllerId);
        Assert.Equal(TimingStates.SpellDuelOpen, stackItem.TimingContext);
        Assert.Equal("BF-A", result.State.SpellDuelState.BattlefieldObjectId);
        Assert.Equal("spell-duel:BF-A", result.State.SpellDuelState.SpellDuelId);
        Assert.Equal(["STACK-SWIFT-A"], result.State.SpellDuelState.StackItemIds);
        Assert.Equal("SPELL_DUEL_TASKS", result.State.PendingTaskQueue.Phase);
        Assert.Equal("task:start-spell-duel:BF-A", result.State.PendingTaskQueue.ActiveTaskId);

        var p1Prompt = result.Prompts["P1"];
        Assert.True(p1Prompt.Actionable);
        Assert.Equal(PromptTypes.StackPriority, p1Prompt.View?.Type);
        Assert.Equal("STACK-SWIFT-A", p1Prompt.View?.RelatedStackItemId);
        Assert.Contains(CommandTypes.PassPriority, p1Prompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, p1Prompt.Actions);

        var p2Prompt = result.Prompts["P2"];
        Assert.False(p2Prompt.Actionable);
        Assert.Equal(PromptTypes.StackPriority, p2Prompt.View?.Type);
        Assert.Equal("STACK-SWIFT-A", p2Prompt.View?.RelatedStackItemId);
        Assert.Equal([PromptTypes.Wait, CommandTypes.Surrender], p2Prompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, p2Prompt.Actions);
        Assert.DoesNotContain(CommandTypes.PassFocus, p2Prompt.Actions);
    }

    private static void AssertWrongTimingPassPriorityRejectionAudit(ResolutionResult result)
    {
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.Empty(result.State.StackItems);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);
        Assert.Empty(result.State.BattlefieldTasks);

        foreach (var prompt in result.Prompts.Values)
        {
            Assert.DoesNotContain(CommandTypes.PassPriority, prompt.Actions);
            Assert.DoesNotContain(CommandTypes.PassFocus, prompt.Actions);
        }
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

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
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
}
