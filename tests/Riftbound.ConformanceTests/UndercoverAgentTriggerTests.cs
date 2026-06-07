using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class UndercoverAgentTriggerTests
{
    [Fact]
    public async Task UndercoverAgentLastBreathOpensHandChoiceAndDiscardsChosenThenDrawsTwo()
    {
        var engine = new CoreRuleEngine();
        var pending = await ResolveUndercoverAgentTriggerAsync(
            engine,
            BuildUndercoverAgentDestroyedState(["P1-HAND-001", "P1-HAND-002", "P1-HAND-003"]));

        Assert.NotNull(pending.State.PendingHandChoice);
        Assert.Equal(PromptTypes.HandChoice, pending.Prompts["P1"].View?.Type);
        Assert.Equal(PromptTypes.HandChoice, pending.Prompts["P2"].View?.Type);
        Assert.True(pending.Prompts["P1"].Actionable);
        Assert.False(pending.Prompts["P2"].Actionable);
        Assert.Contains(pending.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "HAND_CHOICE_REQUESTED", StringComparison.Ordinal));
        Assert.DoesNotContain(pending.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal));

        var ownCandidate = Assert.Single(
            pending.Prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ChooseHandCards, StringComparison.Ordinal));
        var ownMetadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(ownCandidate.Metadata);
        var ownHandChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(ownMetadata["handChoices"]).ToArray();
        Assert.Equal(["P1-HAND-001", "P1-HAND-002", "P1-HAND-003"], ownHandChoices.Select(choice => choice.Id).ToArray());
        Assert.Equal("UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT", ownMetadata["effectKind"]);

        var opponentViewMetadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(pending.Prompts["P2"].View?.Metadata);
        Assert.False(opponentViewMetadata.ContainsKey("handChoices"));
        Assert.False(opponentViewMetadata.ContainsKey("legalObjectIds"));
        Assert.Equal("WAITING", opponentViewMetadata["serverHandChoiceState"]);

        var choice = pending.State.PendingHandChoice!;
        var accepted = await engine.ResolveAsync(
            pending.State,
            new PlayerIntent("intent-undercover-hand-choice-submit", "P1", CommandTypes.ChooseHandCards),
            new ChooseHandCardsCommand(
                choice.ChoiceId,
                choice.ChoiceWindow,
                ["P1-HAND-001", "P1-HAND-002"]),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.State.PendingHandChoice);
        Assert.Equal(["P1-HAND-003", "P1-DRAW-001", "P1-DRAW-002"], accepted.State.PlayerZones["P1"].Hand);
        Assert.Contains("P1-HAND-001", accepted.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-HAND-002", accepted.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal)));
        Assert.Equal(2, DrawnCardCount(accepted.Events));
        Assert.Contains(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "HAND_CHOICE_RESOLVED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UndercoverAgentHandChoiceRejectsAcceptedCommandReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var pending = await ResolveUndercoverAgentTriggerAsync(
            engine,
            BuildUndercoverAgentDestroyedState(["P1-HAND-001", "P1-HAND-002", "P1-HAND-003"]));
        var choice = pending.State.PendingHandChoice!;
        var command = new ChooseHandCardsCommand(
            choice.ChoiceId,
            choice.ChoiceWindow,
            ["P1-HAND-001", "P1-HAND-002"]);

        var accepted = await engine.ResolveAsync(
            pending.State,
            new PlayerIntent("intent-undercover-hand-choice-before-replay", "P1", CommandTypes.ChooseHandCards),
            command,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.State.PendingHandChoice);
        Assert.Equal(["P1-HAND-003", "P1-DRAW-001", "P1-DRAW-002"], accepted.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal)));
        Assert.Equal(2, DrawnCardCount(accepted.Events));
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await engine.ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-undercover-hand-choice-stale-replay", "P1", CommandTypes.ChooseHandCards),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        AssertNoMutation(accepted.State, replay.State);
        Assert.NotEqual(PromptTypes.HandChoice, replay.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, replay.Prompts["P2"].View?.Type);
    }

    [Fact]
    public async Task UndercoverAgentHandChoiceStalePromptReplayAfterWindowClosesRejectsWithoutMutation()
    {
        const string acceptedClientIntentId = "intent-undercover-hand-choice-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-undercover-hand-choice-stale-prompt-replay";
        var pending = await ResolveUndercoverAgentTriggerAsync(
            new CoreRuleEngine(),
            BuildUndercoverAgentDestroyedState(["P1-HAND-001", "P1-HAND-002", "P1-HAND-003"]));
        var choice = pending.State.PendingHandChoice!;
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(pending.State, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.HandChoice, prompt.View?.Type);
        Assert.Contains(CommandTypes.ChooseHandCards, prompt.Actions);

        var command = new ChooseHandCardsCommand(
            choice.ChoiceId,
            choice.ChoiceWindow,
            ["P1-HAND-001", "P1-HAND-002"]);
        var staleRawCommand = PromptScopedChooseHandCardsRawCommand(command, prompt);
        var changedStaleRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.ChooseHandCards,
            choiceId = command.ChoiceId,
            choiceWindow = command.ChoiceWindow,
            chosenObjectIds = command.ChosenObjectIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.State.PendingHandChoice);
        Assert.Equal(["P1-HAND-003", "P1-DRAW-001", "P1-DRAW-002"], accepted.State.PlayerZones["P1"].Hand);
        Assert.Contains("P1-HAND-001", accepted.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-HAND-002", accepted.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal)));
        Assert.Equal(2, DrawnCardCount(accepted.Events));
        Assert.Contains(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "HAND_CHOICE_RESOLVED", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.HandChoice, accepted.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, accepted.Prompts["P2"].View?.Type);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedResultPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedResultSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var postAcceptedPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var postAcceptedSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));
        var acceptedHand = accepted.State.PlayerZones["P1"].Hand.ToArray();
        var acceptedGraveyard = accepted.State.PlayerZones["P1"].Graveyard.ToArray();
        var acceptedMainDeck = accepted.State.PlayerZones["P1"].MainDeck.ToArray();

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(pending.State.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ChooseHandCards, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.Equal(accepted.Events.Select(gameEvent => gameEvent.Kind), acceptedJournalEntry.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedResultPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedResultSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(staleRawCommand.GetRawText(), acceptedJournalEntry.RawCommand.Value.GetRawText());
        Assert.Equal(CommandTypes.ChooseHandCards, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(choice.ChoiceId, acceptedJournalEntry.RawCommand.Value.GetProperty("choiceId").GetString());
        Assert.Equal(choice.ChoiceWindow, acceptedJournalEntry.RawCommand.Value.GetProperty("choiceWindow").GetString());
        Assert.Equal(["P1-HAND-001", "P1-HAND-002"], acceptedJournalEntry.RawCommand.Value.GetProperty("chosenObjectIds")
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(acceptedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

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
        Assert.Equal(postAcceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(postAcceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        AssertNoMutation(accepted.State, replay.State);
        Assert.Null(replay.State.PendingHandChoice);
        Assert.Equal(acceptedHand, replay.State.PlayerZones["P1"].Hand);
        Assert.Equal(acceptedGraveyard, replay.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(acceptedMainDeck, replay.State.PlayerZones["P1"].MainDeck);
        Assert.NotEqual(PromptTypes.HandChoice, replay.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, replay.Prompts["P2"].View?.Type);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, replay.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, replay.Prompts["P2"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = Assert.Single(journal.Entries, entry => !entry.Accepted);
        Assert.Equal(pending.State.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ChooseHandCards, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(postAcceptedPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(postAcceptedSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(staleRawCommand.GetRawText(), rejectedJournalEntry.RawCommand.Value.GetRawText());
        Assert.Equal(CommandTypes.ChooseHandCards, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(choice.ChoiceId, rejectedJournalEntry.RawCommand.Value.GetProperty("choiceId").GetString());
        Assert.Equal(choice.ChoiceWindow, rejectedJournalEntry.RawCommand.Value.GetProperty("choiceWindow").GetString());
        Assert.Equal(["P1-HAND-001", "P1-HAND-002"], rejectedJournalEntry.RawCommand.Value.GetProperty("chosenObjectIds")
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray());
        Assert.Equal(prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var duplicateReplay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateReplay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateReplay.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateReplay.ErrorMessage);
        Assert.Empty(duplicateReplay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(duplicateReplay.State));
        Assert.Equal(accepted.State.Tick, duplicateReplay.State.Tick);
        Assert.Equal(postAcceptedPromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(postAcceptedSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        AssertNoMutation(accepted.State, duplicateReplay.State);
        Assert.Null(duplicateReplay.State.PendingHandChoice);
        Assert.Equal(acceptedHand, duplicateReplay.State.PlayerZones["P1"].Hand);
        Assert.Equal(acceptedGraveyard, duplicateReplay.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(acceptedMainDeck, duplicateReplay.State.PlayerZones["P1"].MainDeck);
        Assert.NotEqual(PromptTypes.HandChoice, duplicateReplay.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, duplicateReplay.Prompts["P2"].View?.Type);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, duplicateReplay.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, duplicateReplay.Prompts["P2"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);

        var conflict = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            changedStaleRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(postAcceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(postAcceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        AssertNoMutation(accepted.State, conflict.State);
        Assert.Null(conflict.State.PendingHandChoice);
        Assert.Equal(acceptedHand, conflict.State.PlayerZones["P1"].Hand);
        Assert.Equal(acceptedGraveyard, conflict.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(acceptedMainDeck, conflict.State.PlayerZones["P1"].MainDeck);
        Assert.NotEqual(PromptTypes.HandChoice, conflict.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, conflict.Prompts["P2"].View?.Type);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, conflict.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, conflict.Prompts["P2"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UndercoverAgentHandChoiceStaleRawPromptAfterWindowClosesRecordsRejectedJournalWithoutMutation()
    {
        const string acceptedClientIntentId = "intent-undercover-hand-choice-stale-raw-first";
        const string replayClientIntentId = "intent-undercover-hand-choice-stale-raw-replay";
        var pending = await ResolveUndercoverAgentTriggerAsync(
            new CoreRuleEngine(),
            BuildUndercoverAgentDestroyedState(["P1-HAND-001", "P1-HAND-002", "P1-HAND-003"]));
        var choice = pending.State.PendingHandChoice!;
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(pending.State, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.HandChoice, prompt.View?.Type);
        Assert.Contains(CommandTypes.ChooseHandCards, prompt.Actions);

        var command = new ChooseHandCardsCommand(
            choice.ChoiceId,
            choice.ChoiceWindow,
            ["P1-HAND-001", "P1-HAND-002"]);
        var rawCommand = PromptScopedChooseHandCardsRawCommand(command, prompt);

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            GameCommandJsonMapper.Map(rawCommand),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Null(accepted.State.PendingHandChoice);
        Assert.Equal(["P1-HAND-003", "P1-DRAW-001", "P1-DRAW-002"], accepted.State.PlayerZones["P1"].Hand);
        Assert.Contains("P1-HAND-001", accepted.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-HAND-002", accepted.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal)));
        Assert.Equal(2, DrawnCardCount(accepted.Events));
        Assert.Contains(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "HAND_CHOICE_RESOLVED", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.HandChoice, accepted.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, accepted.Prompts["P2"].View?.Type);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, accepted.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, accepted.Prompts["P2"].Actions);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedResultPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedResultSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var postAcceptedPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var postAcceptedSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));
        var acceptedHand = accepted.State.PlayerZones["P1"].Hand.ToArray();
        var acceptedGraveyard = accepted.State.PlayerZones["P1"].Graveyard.ToArray();
        var acceptedMainDeck = accepted.State.PlayerZones["P1"].MainDeck.ToArray();

        var replay = await session.SubmitAsync(
            "P1",
            replayClientIntentId,
            GameCommandJsonMapper.Map(rawCommand),
            rawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(postAcceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(postAcceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        AssertNoMutation(accepted.State, replay.State);
        Assert.Null(replay.State.PendingHandChoice);
        Assert.Equal(acceptedHand, replay.State.PlayerZones["P1"].Hand);
        Assert.Equal(acceptedGraveyard, replay.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(acceptedMainDeck, replay.State.PlayerZones["P1"].MainDeck);
        Assert.NotEqual(PromptTypes.HandChoice, replay.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, replay.Prompts["P2"].View?.Type);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, replay.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, replay.Prompts["P2"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        var handChoiceEntries = journal.Entries
            .Where(entry => string.Equals(entry.CommandType, CommandTypes.ChooseHandCards, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, handChoiceEntries.Length);
        var acceptedEntry = Assert.Single(handChoiceEntries, entry => entry.Accepted);
        var rejectedEntry = Assert.Single(handChoiceEntries, entry => !entry.Accepted);

        Assert.Equal(pending.State.RoomId, acceptedEntry.RoomId);
        Assert.Equal("P1", acceptedEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ChooseHandCards, acceptedEntry.CommandType);
        Assert.True(acceptedEntry.Accepted);
        Assert.Null(acceptedEntry.ErrorMessage);
        Assert.Equal(accepted.Events.Select(gameEvent => gameEvent.Kind), acceptedEntry.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedEntry.AuthoritativeState));
        Assert.Equal(acceptedResultPromptsHash, MatchStateHasher.HashValue(acceptedEntry.Prompts));
        Assert.Equal(acceptedResultSnapshotsHash, MatchStateHasher.HashValue(acceptedEntry.Snapshots));
        Assert.True(acceptedEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.ChooseHandCards, acceptedEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(choice.ChoiceId, acceptedEntry.RawCommand.Value.GetProperty("choiceId").GetString());
        Assert.Equal(choice.ChoiceWindow, acceptedEntry.RawCommand.Value.GetProperty("choiceWindow").GetString());
        Assert.Equal(["P1-HAND-001", "P1-HAND-002"], acceptedEntry.RawCommand.Value.GetProperty("chosenObjectIds")
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray());
        Assert.Equal(prompt.PromptId, acceptedEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());

        Assert.Equal(pending.State.RoomId, rejectedEntry.RoomId);
        Assert.Equal("P1", rejectedEntry.PlayerId);
        Assert.Equal(replayClientIntentId, rejectedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ChooseHandCards, rejectedEntry.CommandType);
        Assert.False(rejectedEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedEntry.ErrorMessage);
        Assert.Empty(rejectedEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedEntry.AuthoritativeState));
        Assert.Equal(postAcceptedPromptsHash, MatchStateHasher.HashValue(rejectedEntry.Prompts));
        Assert.Equal(postAcceptedSnapshotsHash, MatchStateHasher.HashValue(rejectedEntry.Snapshots));
        Assert.True(rejectedEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.ChooseHandCards, rejectedEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(choice.ChoiceId, rejectedEntry.RawCommand.Value.GetProperty("choiceId").GetString());
        Assert.Equal(choice.ChoiceWindow, rejectedEntry.RawCommand.Value.GetProperty("choiceWindow").GetString());
        Assert.Equal(["P1-HAND-001", "P1-HAND-002"], rejectedEntry.RawCommand.Value.GetProperty("chosenObjectIds")
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray());
        Assert.Equal(prompt.PromptId, rejectedEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());

        var cachedReplay = await session.SubmitAsync(
            "P1",
            replayClientIntentId,
            GameCommandJsonMapper.Map(rawCommand),
            rawCommand,
            CancellationToken.None);

        Assert.False(cachedReplay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, cachedReplay.ErrorCode);
        Assert.Empty(cachedReplay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(cachedReplay.State));
        Assert.Equal(accepted.State.Tick, cachedReplay.State.Tick);
        Assert.Equal(postAcceptedPromptsHash, MatchStateHasher.HashValue(cachedReplay.Prompts));
        Assert.Equal(postAcceptedSnapshotsHash, MatchStateHasher.HashValue(cachedReplay.Snapshots));
        AssertNoMutation(accepted.State, cachedReplay.State);
        Assert.Null(cachedReplay.State.PendingHandChoice);
        Assert.Equal(acceptedHand, cachedReplay.State.PlayerZones["P1"].Hand);
        Assert.Equal(acceptedGraveyard, cachedReplay.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(acceptedMainDeck, cachedReplay.State.PlayerZones["P1"].MainDeck);
        Assert.NotEqual(PromptTypes.HandChoice, cachedReplay.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, cachedReplay.Prompts["P2"].View?.Type);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, cachedReplay.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, cachedReplay.Prompts["P2"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.ChooseHandCards,
            choiceId = command.ChoiceId,
            choiceWindow = command.ChoiceWindow,
            chosenObjectIds = command.ChosenObjectIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });

        var conflict = await session.SubmitAsync(
            "P1",
            replayClientIntentId,
            GameCommandJsonMapper.Map(changedRawCommand),
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(postAcceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(postAcceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        AssertNoMutation(accepted.State, conflict.State);
        Assert.Null(conflict.State.PendingHandChoice);
        Assert.Equal(acceptedHand, conflict.State.PlayerZones["P1"].Hand);
        Assert.Equal(acceptedGraveyard, conflict.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(acceptedMainDeck, conflict.State.PlayerZones["P1"].MainDeck);
        Assert.NotEqual(PromptTypes.HandChoice, conflict.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, conflict.Prompts["P2"].View?.Type);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, conflict.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.ChooseHandCards, conflict.Prompts["P2"].Actions);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UndercoverAgentHandChoiceDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        var pending = await ResolveUndercoverAgentTriggerAsync(
            new CoreRuleEngine(),
            BuildUndercoverAgentDestroyedState(["P1-HAND-001", "P1-HAND-002", "P1-HAND-003"]));
        var choice = pending.State.PendingHandChoice!;
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(pending.State, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.HandChoice, prompt.View?.Type);
        Assert.Contains(CommandTypes.ChooseHandCards, prompt.Actions);

        var command = new ChooseHandCardsCommand(
            choice.ChoiceId,
            choice.ChoiceWindow,
            ["P1-HAND-001", "P1-HAND-002"]);
        var rawCommand = PromptScopedChooseHandCardsRawCommand(command, prompt);
        var reorderedRawCommand = JsonSerializer.SerializeToElement(new
        {
            snapshotTick = prompt.SnapshotTick,
            promptId = prompt.PromptId,
            chosenObjectIds = command.ChosenObjectIds,
            choiceWindow = command.ChoiceWindow,
            choiceId = command.ChoiceId,
            cmdType = CommandTypes.ChooseHandCards
        });
        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.ChooseHandCards,
            choiceId = command.ChoiceId,
            choiceWindow = command.ChoiceWindow,
            chosenObjectIds = command.ChosenObjectIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        const string clientIntentId = "intent-undercover-hand-choice-raw-duplicate";

        Assert.Equal(CommandTypes.ChooseHandCards, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(choice.ChoiceId, rawCommand.GetProperty("choiceId").GetString());
        Assert.Equal(choice.ChoiceWindow, rawCommand.GetProperty("choiceWindow").GetString());
        Assert.Equal(["P1-HAND-001", "P1-HAND-002"], rawCommand.GetProperty("chosenObjectIds")
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rawCommand.GetProperty("snapshotTick").GetInt64());
        Assert.NotEqual(rawCommand.GetRawText(), reorderedRawCommand.GetRawText());
        Assert.Equal(CommandTypes.ChooseHandCards, reorderedRawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(choice.ChoiceId, reorderedRawCommand.GetProperty("choiceId").GetString());
        Assert.Equal(choice.ChoiceWindow, reorderedRawCommand.GetProperty("choiceWindow").GetString());
        Assert.Equal(["P1-HAND-001", "P1-HAND-002"], reorderedRawCommand.GetProperty("chosenObjectIds")
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray());
        Assert.Equal(prompt.PromptId, reorderedRawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, reorderedRawCommand.GetProperty("snapshotTick").GetInt64());

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            GameCommandJsonMapper.Map(rawCommand),
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Null(accepted.State.PendingHandChoice);
        Assert.Equal(["P1-HAND-003", "P1-DRAW-001", "P1-DRAW-002"], accepted.State.PlayerZones["P1"].Hand);
        Assert.Contains("P1-HAND-001", accepted.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-HAND-002", accepted.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, accepted.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal)));
        Assert.Equal(2, DrawnCardCount(accepted.Events));
        Assert.Contains(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "HAND_CHOICE_RESOLVED", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.HandChoice, accepted.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, accepted.Prompts["P2"].View?.Type);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var promptsAfterAcceptedHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));
        var acceptedHand = accepted.State.PlayerZones["P1"].Hand.ToArray();
        var acceptedGraveyard = accepted.State.PlayerZones["P1"].Graveyard.ToArray();
        var acceptedMainDeck = accepted.State.PlayerZones["P1"].MainDeck.ToArray();
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.ChooseHandCards, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.ChooseHandCards, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(choice.ChoiceId, journalEntry.RawCommand.Value.GetProperty("choiceId").GetString());
        Assert.Equal(choice.ChoiceWindow, journalEntry.RawCommand.Value.GetProperty("choiceWindow").GetString());
        Assert.Equal(["P1-HAND-001", "P1-HAND-002"], journalEntry.RawCommand.Value.GetProperty("chosenObjectIds")
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            GameCommandJsonMapper.Map(rawCommand),
            rawCommand,
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Null(replay.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        AssertNoMutation(accepted.State, replay.State);
        Assert.Null(replay.State.PendingHandChoice);
        Assert.Equal(acceptedHand, replay.State.PlayerZones["P1"].Hand);
        Assert.Equal(acceptedGraveyard, replay.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(acceptedMainDeck, replay.State.PlayerZones["P1"].MainDeck);
        Assert.NotEqual(PromptTypes.HandChoice, replay.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, replay.Prompts["P2"].View?.Type);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Single(journal.Entries);

        var reorderedReplay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            GameCommandJsonMapper.Map(reorderedRawCommand),
            reorderedRawCommand,
            CancellationToken.None);

        Assert.True(reorderedReplay.Accepted, reorderedReplay.ErrorMessage);
        Assert.Null(reorderedReplay.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(reorderedReplay.State));
        Assert.Equal(accepted.State.Tick, reorderedReplay.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(reorderedReplay.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(reorderedReplay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(reorderedReplay.Snapshots));
        AssertNoMutation(accepted.State, reorderedReplay.State);
        Assert.Null(reorderedReplay.State.PendingHandChoice);
        Assert.Equal(acceptedHand, reorderedReplay.State.PlayerZones["P1"].Hand);
        Assert.Equal(acceptedGraveyard, reorderedReplay.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(acceptedMainDeck, reorderedReplay.State.PlayerZones["P1"].MainDeck);
        Assert.NotEqual(PromptTypes.HandChoice, reorderedReplay.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, reorderedReplay.Prompts["P2"].View?.Type);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Single(journal.Entries);

        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            GameCommandJsonMapper.Map(changedRawCommand),
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(promptsAfterAcceptedHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        AssertNoMutation(accepted.State, conflict.State);
        Assert.Null(conflict.State.PendingHandChoice);
        Assert.Equal(acceptedHand, conflict.State.PlayerZones["P1"].Hand);
        Assert.Equal(acceptedGraveyard, conflict.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(acceptedMainDeck, conflict.State.PlayerZones["P1"].MainDeck);
        Assert.NotEqual(PromptTypes.HandChoice, conflict.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, conflict.Prompts["P2"].View?.Type);
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StateBasedCleanupUndercoverAgentQueuesAndOpensHandChoiceThroughStack()
    {
        var engine = new CoreRuleEngine();
        var pending = await ResolveUndercoverAgentTriggerAsync(
            engine,
            BuildUndercoverAgentCleanupDestroyedState(["P1-HAND-001", "P1-HAND-002"]));

        Assert.NotNull(pending.State.PendingHandChoice);
        Assert.Equal(PromptTypes.HandChoice, pending.Prompts["P1"].View?.Type);
        Assert.Contains(pending.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT", StringComparison.Ordinal));
        Assert.Empty(pending.State.StackItems);
        Assert.Empty(pending.State.TriggerQueue);
    }

    [Fact]
    public async Task UndercoverAgentLastBreathWithOneHandAutoDiscardsAndDrawsTwo()
    {
        var engine = new CoreRuleEngine();
        var resolved = await ResolveUndercoverAgentTriggerAsync(
            engine,
            BuildUndercoverAgentDestroyedState(["P1-HAND-001"]));

        Assert.Null(resolved.State.PendingHandChoice);
        Assert.NotEqual(PromptTypes.HandChoice, resolved.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], resolved.State.PlayerZones["P1"].Hand);
        Assert.Contains("P1-HAND-001", resolved.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(1, resolved.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal)));
        Assert.Equal(2, DrawnCardCount(resolved.Events));
    }

    [Fact]
    public async Task UndercoverAgentLastBreathWithNoHandDrawsTwoWithoutPrompt()
    {
        var engine = new CoreRuleEngine();
        var resolved = await ResolveUndercoverAgentTriggerAsync(
            engine,
            BuildUndercoverAgentDestroyedState([]));

        Assert.Null(resolved.State.PendingHandChoice);
        Assert.NotEqual(PromptTypes.HandChoice, resolved.Prompts["P1"].View?.Type);
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], resolved.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal));
        Assert.Equal(2, DrawnCardCount(resolved.Events));
        Assert.Contains(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "HAND_CHOICE_SKIPPED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UndercoverAgentHandChoiceRejectsInvalidCommandsWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var pending = await ResolveUndercoverAgentTriggerAsync(
            engine,
            BuildUndercoverAgentDestroyedState(["P1-HAND-001", "P1-HAND-002", "P1-HAND-003"]));
        var state = pending.State;
        var choice = state.PendingHandChoice!;

        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P2",
            new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-HAND-001", "P1-HAND-002"]),
            ErrorCodes.PhaseNotAllowed);
        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            new ChooseHandCardsCommand($"stale-{choice.ChoiceId}", choice.ChoiceWindow, ["P1-HAND-001", "P1-HAND-002"]),
            ErrorCodes.PromptExpired);

        const string staleSnapshotClientIntentId = "intent-undercover-stale-snapshot";
        using var staleSnapshot = JsonDocument.Parse(
            $$"""
              {
                "cmdType": "CHOOSE_HAND_CARDS",
                "choiceId": "{{choice.ChoiceId}}",
                "choiceWindow": "{{choice.ChoiceWindow}}",
                "chosenObjectIds": ["P1-HAND-001", "P1-HAND-002"],
                "snapshotTick": {{state.Tick - 1}}
              }
              """);
        var staleSnapshotJournal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), staleSnapshotJournal);
        var initialStateHash = MatchStateHasher.Hash(state);
        var initialPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(state));
        var initialSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(state));
        var initialP1PromptHash = MatchStateHasher.HashValue(session.PromptFor("P1"));
        var initialP2PromptHash = MatchStateHasher.HashValue(session.PromptFor("P2"));
        var initialP1SnapshotHash = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var initialP2SnapshotHash = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var staleSnapshotResult = await session.SubmitAsync(
            "P1",
            staleSnapshotClientIntentId,
            GameCommandJsonMapper.Map(staleSnapshot.RootElement),
            staleSnapshot.RootElement,
            CancellationToken.None);

        Assert.False(staleSnapshotResult.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, staleSnapshotResult.ErrorCode);
        AssertNoMutation(state, staleSnapshotResult.State);
        Assert.Empty(staleSnapshotResult.Events);
        Assert.Equal(initialStateHash, MatchStateHasher.Hash(staleSnapshotResult.State));
        Assert.Equal(state.Tick, staleSnapshotResult.State.Tick);
        Assert.Equal(initialPromptsHash, MatchStateHasher.HashValue(staleSnapshotResult.Prompts));
        Assert.Equal(initialSnapshotsHash, MatchStateHasher.HashValue(staleSnapshotResult.Snapshots));
        Assert.Equal(initialP1PromptHash, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(initialP2PromptHash, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(initialP1SnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(initialP2SnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        var rejectedStaleSnapshotEntry = Assert.Single(staleSnapshotJournal.Entries);
        Assert.Equal(state.RoomId, rejectedStaleSnapshotEntry.RoomId);
        Assert.Equal("P1", rejectedStaleSnapshotEntry.PlayerId);
        Assert.Equal(staleSnapshotClientIntentId, rejectedStaleSnapshotEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ChooseHandCards, rejectedStaleSnapshotEntry.CommandType);
        Assert.False(rejectedStaleSnapshotEntry.Accepted);
        Assert.Equal(staleSnapshotResult.ErrorMessage, rejectedStaleSnapshotEntry.ErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(rejectedStaleSnapshotEntry.ErrorMessage));
        Assert.Empty(rejectedStaleSnapshotEntry.Events);
        Assert.Equal(initialStateHash, MatchStateHasher.Hash(rejectedStaleSnapshotEntry.AuthoritativeState));
        Assert.Equal(initialPromptsHash, MatchStateHasher.HashValue(rejectedStaleSnapshotEntry.Prompts));
        Assert.Equal(initialSnapshotsHash, MatchStateHasher.HashValue(rejectedStaleSnapshotEntry.Snapshots));
        Assert.True(rejectedStaleSnapshotEntry.RawCommand.HasValue);
        Assert.Equal(staleSnapshot.RootElement.GetRawText(), rejectedStaleSnapshotEntry.RawCommand.Value.GetRawText());
        Assert.Equal(CommandTypes.ChooseHandCards, rejectedStaleSnapshotEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(choice.ChoiceId, rejectedStaleSnapshotEntry.RawCommand.Value.GetProperty("choiceId").GetString());
        Assert.Equal(choice.ChoiceWindow, rejectedStaleSnapshotEntry.RawCommand.Value.GetProperty("choiceWindow").GetString());
        Assert.Equal(["P1-HAND-001", "P1-HAND-002"], rejectedStaleSnapshotEntry.RawCommand.Value.GetProperty("chosenObjectIds")
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray());
        Assert.Equal(state.Tick - 1, rejectedStaleSnapshotEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(rejectedStaleSnapshotEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var cachedStaleSnapshotResult = await session.SubmitAsync(
            "P1",
            staleSnapshotClientIntentId,
            GameCommandJsonMapper.Map(staleSnapshot.RootElement),
            staleSnapshot.RootElement,
            CancellationToken.None);

        Assert.False(cachedStaleSnapshotResult.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, cachedStaleSnapshotResult.ErrorCode);
        Assert.Equal(staleSnapshotResult.ErrorMessage, cachedStaleSnapshotResult.ErrorMessage);
        AssertNoMutation(state, cachedStaleSnapshotResult.State);
        Assert.Empty(cachedStaleSnapshotResult.Events);
        Assert.Equal(initialStateHash, MatchStateHasher.Hash(cachedStaleSnapshotResult.State));
        Assert.Equal(state.Tick, cachedStaleSnapshotResult.State.Tick);
        Assert.Equal(initialPromptsHash, MatchStateHasher.HashValue(cachedStaleSnapshotResult.Prompts));
        Assert.Equal(initialSnapshotsHash, MatchStateHasher.HashValue(cachedStaleSnapshotResult.Snapshots));
        Assert.Equal(initialP1PromptHash, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(initialP2PromptHash, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(initialP1SnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(initialP2SnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Same(rejectedStaleSnapshotEntry, Assert.Single(staleSnapshotJournal.Entries));

        var changedStaleSnapshotRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.ChooseHandCards,
            choiceId = choice.ChoiceId,
            choiceWindow = choice.ChoiceWindow,
            chosenObjectIds = new[] { "P1-HAND-002", "P1-HAND-001" },
            snapshotTick = state.Tick - 1,
            clientNote = "changed-payload"
        });

        var conflict = await session.SubmitAsync(
            "P1",
            staleSnapshotClientIntentId,
            GameCommandJsonMapper.Map(changedStaleSnapshotRawCommand),
            changedStaleSnapshotRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        AssertNoMutation(state, conflict.State);
        Assert.Empty(conflict.Events);
        Assert.Equal(initialStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(state.Tick, conflict.State.Tick);
        Assert.Equal(initialPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(initialSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(initialP1PromptHash, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(initialP2PromptHash, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(initialP1SnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(initialP2SnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Same(rejectedStaleSnapshotEntry, Assert.Single(staleSnapshotJournal.Entries));
        Assert.DoesNotContain(staleSnapshotJournal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));

        var postConflictCachedStaleSnapshotResult = await session.SubmitAsync(
            "P1",
            staleSnapshotClientIntentId,
            GameCommandJsonMapper.Map(staleSnapshot.RootElement),
            staleSnapshot.RootElement,
            CancellationToken.None);

        Assert.False(postConflictCachedStaleSnapshotResult.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, postConflictCachedStaleSnapshotResult.ErrorCode);
        Assert.Equal(staleSnapshotResult.ErrorMessage, postConflictCachedStaleSnapshotResult.ErrorMessage);
        AssertNoMutation(state, postConflictCachedStaleSnapshotResult.State);
        Assert.Empty(postConflictCachedStaleSnapshotResult.Events);
        Assert.Equal(initialStateHash, MatchStateHasher.Hash(postConflictCachedStaleSnapshotResult.State));
        Assert.Equal(state.Tick, postConflictCachedStaleSnapshotResult.State.Tick);
        Assert.Equal(initialPromptsHash, MatchStateHasher.HashValue(postConflictCachedStaleSnapshotResult.Prompts));
        Assert.Equal(initialSnapshotsHash, MatchStateHasher.HashValue(postConflictCachedStaleSnapshotResult.Snapshots));
        Assert.Equal(initialP1PromptHash, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(initialP2PromptHash, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(initialP1SnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(initialP2SnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Same(rejectedStaleSnapshotEntry, Assert.Single(staleSnapshotJournal.Entries));

        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-HAND-001", "P1-HAND-001"]),
            ErrorCodes.InvalidPayload);
        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-HAND-001"]),
            ErrorCodes.InvalidPayload);
        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-HAND-001", "P1-UNKNOWN"]),
            ErrorCodes.InvalidTarget);
        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-HAND-001", "P1-BASE-OTHER"]),
            ErrorCodes.InvalidTarget);
        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, null),
            ErrorCodes.InvalidPayload);

        using var malformed = JsonDocument.Parse(
            $$"""
              {
                "cmdType": "CHOOSE_HAND_CARDS",
                "choiceId": "{{choice.ChoiceId}}",
                "choiceWindow": "{{choice.ChoiceWindow}}",
                "chosenObjectIds": "P1-HAND-001"
              }
              """);
        await AssertRejectedWithoutMutationAsync(
            engine,
            state,
            "P1",
            GameCommandJsonMapper.Map(malformed.RootElement),
            ErrorCodes.InvalidPayload);
    }

    [Fact]
    public async Task HiddenAndStandbyUndercoverAgentsDoNotTriggerOrLeakHandChoice()
    {
        var engine = new CoreRuleEngine();
        var p1Pass = await engine.ResolveAsync(
            BuildHiddenUndercoverAgentsDestroyedState(),
            new PlayerIntent("intent-hidden-undercover-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hidden-undercover-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Null(p2Pass.State.PendingHandChoice);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "HAND_CHOICE_REQUESTED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.NotEqual(PromptTypes.HandChoice, p2Pass.Prompts["P1"].View?.Type);
        Assert.NotEqual(PromptTypes.HandChoice, p2Pass.Prompts["P2"].View?.Type);
    }

    private static async Task AssertRejectedWithoutMutationAsync(
        CoreRuleEngine engine,
        MatchState state,
        string playerId,
        GameCommand command,
        string expectedErrorCode)
    {
        var rejected = await engine.ResolveAsync(
            state,
            new PlayerIntent($"intent-undercover-reject-{Guid.NewGuid():N}", playerId, command.CmdType),
            command,
            CancellationToken.None);

        Assert.False(rejected.Accepted);
        Assert.Equal(expectedErrorCode, rejected.ErrorCode);
        AssertNoMutation(state, rejected.State);
        Assert.Empty(rejected.Events);
    }

    private static void AssertNoMutation(MatchState expected, MatchState actual)
    {
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.PendingHandChoice, actual.PendingHandChoice);
        Assert.Equal(expected.PlayerZones["P1"].Hand, actual.PlayerZones["P1"].Hand);
        Assert.Equal(expected.PlayerZones["P1"].Graveyard, actual.PlayerZones["P1"].Graveyard);
        Assert.Equal(expected.PlayerZones["P1"].MainDeck, actual.PlayerZones["P1"].MainDeck);
    }

    private static int DrawnCardCount(IEnumerable<GameEvent> events)
    {
        return events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal))
            .Sum(gameEvent => gameEvent.Payload.TryGetValue("count", out var count)
                && count is int typedCount
                    ? typedCount
                    : 0);
    }

    private static JsonElement PromptScopedChooseHandCardsRawCommand(
        ChooseHandCardsCommand command,
        ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.ChooseHandCards,
            choiceId = command.ChoiceId,
            choiceWindow = command.ChoiceWindow,
            chosenObjectIds = command.ChosenObjectIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
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

    private static async Task<ResolutionResult> ResolveUndercoverAgentTriggerAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-undercover-spirit-fire-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-undercover-spirit-fire-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.TriggerQueue);
        Assert.Single(p2Pass.State.StackItems);
        Assert.Equal("UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT", p2Pass.State.StackItems[0].EffectKind);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGERS_MOVED_TO_STACK", StringComparison.Ordinal));

        var triggerPass1 = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-undercover-trigger-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var triggerPass2 = await engine.ResolveAsync(
            triggerPass1.State,
            new PlayerIntent("intent-undercover-trigger-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(triggerPass2.Accepted, triggerPass2.ErrorMessage);
        Assert.Empty(triggerPass2.State.StackItems);
        Assert.Empty(triggerPass2.State.TriggerQueue);
        Assert.Contains(triggerPass2.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT", StringComparison.Ordinal));
        return triggerPass2;
    }

    private static MatchState BuildUndercoverAgentDestroyedState(IReadOnlyList<string> handObjectIds)
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                MainDeck = ["P1-DRAW-001", "P1-DRAW-002", "P1-DRAW-003"],
                Hand = handObjectIds,
                Base = ["P1-UNDERCOVER-AGENT", "P1-BASE-OTHER"]
            },
            ["P2"] = PlayerZones.Empty
        };
        var cardObjects = BaseCardObjects(handObjectIds);
        cardObjects["P1-UNDERCOVER-AGENT"] = new(
            "P1-UNDERCOVER-AGENT",
            cardNo: "OGN·178/298",
            power: 2,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P1-BASE-OTHER"] = new(
            "P1-BASE-OTHER",
            cardNo: "OGN·033/298",
            power: 1,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P1-SPELL-SPIRIT-FIRE"] = new(
            "P1-SPELL-SPIRIT-FIRE",
            cardNo: "OGN·256/298",
            ownerId: "P1",
            controllerId: "P1");

        return BuildState(
            "undercover-agent-trigger-room",
            playerZones,
            cardObjects,
            ["P1-UNDERCOVER-AGENT"]);
    }

    private static MatchState BuildUndercoverAgentCleanupDestroyedState(IReadOnlyList<string> handObjectIds)
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                MainDeck = ["P1-DRAW-001", "P1-DRAW-002", "P1-DRAW-003"],
                Hand = handObjectIds,
                Base = ["P1-UNDERCOVER-AGENT"]
            },
            ["P2"] = PlayerZones.Empty with
            {
                Base = ["P2-CLEANUP-DUMMY"]
            }
        };
        var cardObjects = BaseCardObjects(handObjectIds);
        cardObjects["P1-UNDERCOVER-AGENT"] = new(
            "P1-UNDERCOVER-AGENT",
            cardNo: "OGN·178/298",
            power: 3,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P1-SPELL-STARFALL"] = new(
            "P1-SPELL-STARFALL",
            cardNo: "OGN·029/298",
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P2-CLEANUP-DUMMY"] = new(
            "P2-CLEANUP-DUMMY",
            cardNo: "OGN·033/298",
            power: 3,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P2",
            controllerId: "P2");

        return BuildState(
            "undercover-agent-cleanup-trigger-room",
            playerZones,
            cardObjects,
            ["P1-UNDERCOVER-AGENT", "P2-CLEANUP-DUMMY"],
            "STACK-STARFALL-UNDERCOVER-AGENT",
            "P1-SPELL-STARFALL",
            "STARFALL_DAMAGE_3_TWICE",
            "OGN·029/298");
    }

    private static MatchState BuildHiddenUndercoverAgentsDestroyedState()
    {
        var playerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
        {
            ["P1"] = PlayerZones.Empty with
            {
                MainDeck = ["P1-DRAW-001", "P1-DRAW-002"],
                Hand = ["P1-HAND-001", "P1-HAND-002"],
                Base = ["P1-HIDDEN-UNDERCOVER-AGENT"]
            },
            ["P2"] = PlayerZones.Empty with
            {
                Base = ["P2-STANDBY-UNDERCOVER-AGENT"]
            }
        };
        var cardObjects = BaseCardObjects(["P1-HAND-001", "P1-HAND-002"]);
        cardObjects["P1-HIDDEN-UNDERCOVER-AGENT"] = new(
            "P1-HIDDEN-UNDERCOVER-AGENT",
            cardNo: "OGN·178/298",
            power: 2,
            isFaceDown: true,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P2-STANDBY-UNDERCOVER-AGENT"] = new(
            "P2-STANDBY-UNDERCOVER-AGENT",
            cardNo: "OGN·178/298",
            power: 2,
            tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
            ownerId: "P2",
            controllerId: "P2");
        cardObjects["P1-SPELL-SPIRIT-FIRE"] = new(
            "P1-SPELL-SPIRIT-FIRE",
            cardNo: "OGN·256/298",
            ownerId: "P1",
            controllerId: "P1");

        return BuildState(
            "hidden-undercover-agent-trigger-room",
            playerZones,
            cardObjects,
            ["P1-HIDDEN-UNDERCOVER-AGENT", "P2-STANDBY-UNDERCOVER-AGENT"]);
    }

    private static Dictionary<string, CardObjectState> BaseCardObjects(IReadOnlyList<string> handObjectIds)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-DRAW-001"] = new("P1-DRAW-001", cardNo: "OGN·033/298", ownerId: "P1", controllerId: "P1"),
            ["P1-DRAW-002"] = new("P1-DRAW-002", cardNo: "OGN·033/298", ownerId: "P1", controllerId: "P1"),
            ["P1-DRAW-003"] = new("P1-DRAW-003", cardNo: "OGN·033/298", ownerId: "P1", controllerId: "P1")
        };

        foreach (var handObjectId in handObjectIds)
        {
            cardObjects[handObjectId] = new(
                handObjectId,
                cardNo: "OGN·033/298",
                ownerId: "P1",
                controllerId: "P1");
        }

        return cardObjects;
    }

    private static MatchState BuildState(
        string roomId,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyList<string> targetObjectIds,
        string stackItemId = "STACK-SPIRIT-FIRE-UNDERCOVER-AGENT",
        string sourceObjectId = "P1-SPELL-SPIRIT-FIRE",
        string effectKind = "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
        string cardNo = "OGN·256/298")
    {
        return new MatchState(
            roomId,
            11,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: playerZones,
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    stackItemId,
                    "P1",
                    sourceObjectId,
                    effectKind,
                    cardNo,
                    targetObjectIds)
            ],
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
    }
}
