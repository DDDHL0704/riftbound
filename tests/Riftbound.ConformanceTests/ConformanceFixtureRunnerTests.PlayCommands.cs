using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task CoreRuleEnginePlaysPunishmentThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-punishment-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAcceptedPlayCardReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 2);
        state = PrintedCostFixture.Add(state, "P1", "UNL-007/219");
        var command = new PlayCardCommand(
            "P1-SPELL-PUNISHMENT",
            "UNL-007/219",
            ["P2-UNIT-001"]);

        var accepted = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-punishment-play-before-replay", "P1", CommandTypes.PlayCard),
            command,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Empty(accepted.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, accepted.State.RunePools["P1"].Mana);
        var stackItem = Assert.Single(accepted.State.StackItems);
        Assert.Equal("P1-SPELL-PUNISHMENT", stackItem.SourceObjectId);
        Assert.Equal(["P2-UNIT-001"], stackItem.TargetObjectIds);
        Assert.Equal("STACK", accepted.State.ObjectLocations["P1-SPELL-PUNISHMENT"].Zone);
        Assert.Equal(TimingStates.NeutralClosed, accepted.State.TimingState);
        Assert.Equal("P1", accepted.State.PriorityPlayerId);
        Assert.DoesNotContain(CommandTypes.PlayCard, accepted.Prompts["P1"].EnabledActions());
        AssertPlayCardStackPriorityPromptQueueAudit(accepted);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await engine.ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-punishment-play-stale-replay", "P1", CommandTypes.PlayCard),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Empty(replay.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, replay.State.RunePools["P1"].Mana);
        var replayStackItem = Assert.Single(replay.State.StackItems);
        Assert.Equal(stackItem.StackItemId, replayStackItem.StackItemId);
        Assert.Equal("P1-SPELL-PUNISHMENT", replayStackItem.SourceObjectId);
        Assert.Equal(["P2-UNIT-001"], replayStackItem.TargetObjectIds);
        Assert.Equal("STACK", replay.State.ObjectLocations["P1-SPELL-PUNISHMENT"].Zone);
        Assert.Equal(TimingStates.NeutralClosed, replay.State.TimingState);
        Assert.Equal("P1", replay.State.PriorityPlayerId);
        Assert.DoesNotContain(CommandTypes.PlayCard, replay.Prompts["P1"].EnabledActions());
        AssertPlayCardStackPriorityPromptQueueAudit(replay);
    }

    [Fact]
    public async Task PlayCardStalePromptReplayAfterStackPriorityStartsRejectsWithoutMutation()
    {
        var state = PunishmentState(mana: 2);
        state = PrintedCostFixture.Add(state, "P1", "UNL-007/219");
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            "P1-SPELL-PUNISHMENT",
            "UNL-007/219",
            ["P2-UNIT-001"]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.EnabledActions());
        var missingTargetsRawCommand = PromptScopedRawCommand(CommandTypes.PlayCard, prompt);
        var malformed = await session.SubmitAsync("P1", "intent-punishment-stale-missing-targets", command,
            missingTargetsRawCommand, CancellationToken.None);
        Assert.False(malformed.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, malformed.ErrorCode);
        Assert.Same(state, malformed.State);
        Assert.Empty(malformed.Events);
        Assert.Empty(journal.Entries);
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        const string acceptedClientIntentId = "intent-punishment-play-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-punishment-play-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Empty(accepted.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, accepted.State.RunePools["P1"].Mana);
        var stackItem = Assert.Single(accepted.State.StackItems);
        Assert.Equal("P1-SPELL-PUNISHMENT", stackItem.SourceObjectId);
        Assert.Equal(["P2-UNIT-001"], stackItem.TargetObjectIds);
        Assert.Equal("STACK", accepted.State.ObjectLocations["P1-SPELL-PUNISHMENT"].Zone);
        Assert.Equal(TimingStates.NeutralClosed, accepted.State.TimingState);
        Assert.Equal("P1", accepted.State.PriorityPlayerId);
        Assert.Equal(PromptTypes.StackPriority, accepted.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.PlayCard, accepted.Prompts["P1"].EnabledActions());
        AssertPlayCardStackPriorityPromptQueueAudit(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PlayCard, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PlayCard, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Empty(replay.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, replay.State.RunePools["P1"].Mana);
        var replayStackItem = Assert.Single(replay.State.StackItems);
        Assert.Equal(stackItem.StackItemId, replayStackItem.StackItemId);
        Assert.Equal("P1-SPELL-PUNISHMENT", replayStackItem.SourceObjectId);
        Assert.Equal(["P2-UNIT-001"], replayStackItem.TargetObjectIds);
        Assert.Equal("STACK", replay.State.ObjectLocations["P1-SPELL-PUNISHMENT"].Zone);
        Assert.Equal(TimingStates.NeutralClosed, replay.State.TimingState);
        Assert.Equal("P1", replay.State.PriorityPlayerId);
        Assert.Equal(PromptTypes.StackPriority, replay.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.PlayCard, replay.Prompts["P1"].EnabledActions());
        AssertPlayCardStackPriorityPromptQueueAudit(replay);

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PlayCard, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PlayCard, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var rejectedEventsHash = MatchStateHasher.HashValue(replay.Events);
        var duplicateRejected = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateRejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateRejected.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateRejected.ErrorMessage);
        Assert.Equal(rejectedEventsHash, MatchStateHasher.HashValue(duplicateRejected.Events));
        Assert.Empty(duplicateRejected.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(replay.State.Tick, duplicateRejected.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        Assert.Empty(duplicateRejected.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, duplicateRejected.State.RunePools["P1"].Mana);
        var duplicateRejectedStackItem = Assert.Single(duplicateRejected.State.StackItems);
        Assert.Equal(stackItem.StackItemId, duplicateRejectedStackItem.StackItemId);
        Assert.Equal("P1-SPELL-PUNISHMENT", duplicateRejectedStackItem.SourceObjectId);
        Assert.Equal(["P2-UNIT-001"], duplicateRejectedStackItem.TargetObjectIds);
        Assert.Equal("STACK", duplicateRejected.State.ObjectLocations["P1-SPELL-PUNISHMENT"].Zone);
        Assert.Equal(TimingStates.NeutralClosed, duplicateRejected.State.TimingState);
        Assert.Equal("P1", duplicateRejected.State.PriorityPlayerId);
        Assert.Equal(PromptTypes.StackPriority, duplicateRejected.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.PlayCard, duplicateRejected.Prompts["P1"].EnabledActions());
        AssertPlayCardStackPriorityPromptQueueAudit(duplicateRejected);
        Assert.Equal(2, journal.Entries.Count);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PlayCard,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Empty(conflict.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, conflict.State.RunePools["P1"].Mana);
        var conflictStackItem = Assert.Single(conflict.State.StackItems);
        Assert.Equal(stackItem.StackItemId, conflictStackItem.StackItemId);
        Assert.Equal("P1-SPELL-PUNISHMENT", conflictStackItem.SourceObjectId);
        Assert.Equal(["P2-UNIT-001"], conflictStackItem.TargetObjectIds);
        Assert.Equal("STACK", conflict.State.ObjectLocations["P1-SPELL-PUNISHMENT"].Zone);
        Assert.Equal(TimingStates.NeutralClosed, conflict.State.TimingState);
        Assert.Equal("P1", conflict.State.PriorityPlayerId);
        Assert.Equal(PromptTypes.StackPriority, conflict.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.PlayCard, conflict.Prompts["P1"].EnabledActions());
        AssertPlayCardStackPriorityPromptQueueAudit(conflict);
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlayCardDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        var state = PunishmentState(mana: 2);
        state = PrintedCostFixture.Add(state, "P1", "UNL-007/219");
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            "P1-SPELL-PUNISHMENT",
            "UNL-007/219",
            ["P2-UNIT-001"]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.EnabledActions());
        var missingTargetsRawCommand = PromptScopedRawCommand(CommandTypes.PlayCard, prompt);
        var malformed = await session.SubmitAsync("P1", "intent-punishment-idempotency-missing-targets", command,
            missingTargetsRawCommand, CancellationToken.None);
        Assert.False(malformed.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, malformed.ErrorCode);
        Assert.Same(state, malformed.State);
        Assert.Empty(malformed.Events);
        Assert.Empty(journal.Entries);
        var rawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        const string clientIntentId = "intent-punishment-play-raw-idempotency";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Empty(accepted.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, accepted.State.RunePools["P1"].Mana);
        var stackItem = Assert.Single(accepted.State.StackItems);
        Assert.Equal("P1-SPELL-PUNISHMENT", stackItem.SourceObjectId);
        Assert.Equal(["P2-UNIT-001"], stackItem.TargetObjectIds);
        Assert.Equal("STACK", accepted.State.ObjectLocations["P1-SPELL-PUNISHMENT"].Zone);
        Assert.Equal(TimingStates.NeutralClosed, accepted.State.TimingState);
        Assert.Equal("P1", accepted.State.PriorityPlayerId);
        Assert.Equal(PromptTypes.StackPriority, accepted.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.PlayCard, accepted.Prompts["P1"].EnabledActions());
        AssertPlayCardStackPriorityPromptQueueAudit(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.PlayCard, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PlayCard, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Null(replay.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], replay.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Empty(replay.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, replay.State.RunePools["P1"].Mana);
        var replayStackItem = Assert.Single(replay.State.StackItems);
        Assert.Equal(stackItem.StackItemId, replayStackItem.StackItemId);
        Assert.Equal("P1-SPELL-PUNISHMENT", replayStackItem.SourceObjectId);
        Assert.Equal(["P2-UNIT-001"], replayStackItem.TargetObjectIds);
        Assert.Equal("STACK", replay.State.ObjectLocations["P1-SPELL-PUNISHMENT"].Zone);
        Assert.Equal(TimingStates.NeutralClosed, replay.State.TimingState);
        Assert.Equal("P1", replay.State.PriorityPlayerId);
        Assert.Equal(PromptTypes.StackPriority, replay.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.PlayCard, replay.Prompts["P1"].EnabledActions());
        AssertPlayCardStackPriorityPromptQueueAudit(replay);
        Assert.Single(journal.Entries);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PlayCard,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
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
        Assert.Empty(conflict.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, conflict.State.RunePools["P1"].Mana);
        var conflictStackItem = Assert.Single(conflict.State.StackItems);
        Assert.Equal(stackItem.StackItemId, conflictStackItem.StackItemId);
        Assert.Equal("P1-SPELL-PUNISHMENT", conflictStackItem.SourceObjectId);
        Assert.Equal(["P2-UNIT-001"], conflictStackItem.TargetObjectIds);
        Assert.Equal("STACK", conflict.State.ObjectLocations["P1-SPELL-PUNISHMENT"].Zone);
        Assert.Equal(TimingStates.NeutralClosed, conflict.State.TimingState);
        Assert.Equal("P1", conflict.State.PriorityPlayerId);
        Assert.Equal(PromptTypes.StackPriority, conflict.Prompts["P1"].View?.Type);
        Assert.DoesNotContain(CommandTypes.PlayCard, conflict.Prompts["P1"].EnabledActions());
        AssertPlayCardStackPriorityPromptQueueAudit(conflict);
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    private static void AssertPlayCardStackPriorityPromptQueueAudit(ResolutionResult result)
    {
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1", stackItem.ControllerId);
        Assert.Equal("P1-SPELL-PUNISHMENT", stackItem.SourceObjectId);
        Assert.Equal("UNL-007/219", stackItem.CardNo);
        Assert.Equal(["P2-UNIT-001"], stackItem.TargetObjectIds);
        Assert.Equal("STACK", result.State.ObjectLocations["P1-SPELL-PUNISHMENT"].Zone);

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal("P1", snapshot.ActivePlayerId);
            Assert.Equal("P1", Assert.IsType<string>(snapshot.Timing["turnPlayerId"]));
            Assert.Equal(MatchPhases.Main, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.NeutralClosed, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Equal("P1", Assert.IsType<string>(snapshot.Timing["priorityPlayerId"]));
            Assert.Null(snapshot.Timing["focusPlayerId"]);

            var snapshotStackItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            Assert.Equal(stackItem.StackItemId, Assert.IsType<string>(snapshotStackItem["stackItemId"]));
            Assert.Equal("P1", Assert.IsType<string>(snapshotStackItem["controllerId"]));
            Assert.Equal("P1-SPELL-PUNISHMENT", Assert.IsType<string>(snapshotStackItem["sourceObjectId"]));
            Assert.Equal("UNL-007/219", Assert.IsType<string>(snapshotStackItem["cardNo"]));

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }

        Assert.Equal("P1", result.Prompts["P1"].PlayerId);
        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(PromptTypes.StackPriority, result.Prompts["P1"].View?.Type);
        Assert.Equal(stackItem.StackItemId, result.Prompts["P1"].View?.RelatedStackItemId);
        Assert.Contains(CommandTypes.PassPriority, result.Prompts["P1"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.PlayCard, result.Prompts["P1"].EnabledActions());
        Assert.Equal(result.State.Tick, result.Prompts["P1"].SnapshotTick);

        Assert.Equal("P2", result.Prompts["P2"].PlayerId);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.PlayCard, result.Prompts["P2"].EnabledActions());
        Assert.DoesNotContain(CommandTypes.PassPriority, result.Prompts["P2"].EnabledActions());
        Assert.Equal(result.State.Tick, result.Prompts["P2"].SnapshotTick);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPunishmentAgainstBaseUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PUNISHMENT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-UNIT-001"] = new("P2-BASE-UNIT-001")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-punishment-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PUNISHMENT", "UNL-007/219", ["P2-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(["P1-SPELL-PUNISHMENT"], result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task P4PlayCardCommandRejectsSourceWithoutCardNo()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNKNOWN-PLAY-SOURCE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNKNOWN-PLAY-SOURCE"] = new(
                    "P1-HAND-UNKNOWN-PLAY-SOURCE",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-play-card-unknown-cardno-source", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-HAND-UNKNOWN-PLAY-SOURCE", "OGN·009/298", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("出牌需要服务端已确认的卡牌信息。", result.ErrorMessage);
        Assert.DoesNotContain("PLAY_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-HAND-UNKNOWN-PLAY-SOURCE"], result.State.PlayerZones["P1"].Hand);
        Assert.Null(result.State.CardObjects["P1-HAND-UNKNOWN-PLAY-SOURCE"].CardNo);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4PlayCardCommandRejectsSourceCardNoMismatch()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HEXTECH-RAY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HEXTECH-RAY"] = new(
                    "P1-SPELL-HEXTECH-RAY",
                    cardNo: "OGN·009/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-play-card-source-cardno-mismatch", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-HEXTECH-RAY", "SFD·125/221", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("出牌的卡牌信息与提交的牌不匹配。", result.ErrorMessage);
        Assert.DoesNotContain("PLAY_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-HEXTECH-RAY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal("OGN·009/298", result.State.CardObjects["P1-SPELL-HEXTECH-RAY"].CardNo);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4PlayCardCommandRejectsOpponentControlledSourceInPlayerHand()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OPPONENT-CONTROLLED-PLAY-SOURCE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OPPONENT-CONTROLLED-PLAY-SOURCE"] = new(
                    "P1-HAND-OPPONENT-CONTROLLED-PLAY-SOURCE",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-play-card-opponent-controlled-source", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-HAND-OPPONENT-CONTROLLED-PLAY-SOURCE", "SFD·125/221", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("出牌只能选择当前玩家控制的合法来源。", result.ErrorMessage);
        Assert.DoesNotContain("PLAY_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-HAND-OPPONENT-CONTROLLED-PLAY-SOURCE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal("P2", result.State.CardObjects["P1-HAND-OPPONENT-CONTROLLED-PLAY-SOURCE"].ControllerId);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4PlayCardCommandRejectsTargetWithoutCardNo()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HEXTECH-RAY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-UNKNOWN-PLAY-TARGET"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HEXTECH-RAY"] = new(
                    "P1-SPELL-HEXTECH-RAY",
                    cardNo: "OGN·009/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-UNIT-UNKNOWN-PLAY-TARGET"] = new(
                    "P2-UNIT-UNKNOWN-PLAY-TARGET",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-play-card-unknown-cardno-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-HEXTECH-RAY", "OGN·009/298", ["P2-UNIT-UNKNOWN-PLAY-TARGET"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-HEXTECH-RAY"], result.State.PlayerZones["P1"].Hand);
        Assert.Null(result.State.CardObjects["P2-UNIT-UNKNOWN-PLAY-TARGET"].CardNo);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4PunishmentBaseUnitTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-punishment-base-unit-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-PUNISHMENT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-UNIT-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Empty(result.FinalState.StackItems);
    }
}
