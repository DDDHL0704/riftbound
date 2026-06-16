using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HostileTakeoverGuardTests
{
    private const string HostileTakeoverObjectId = "P1-SPELL-HOSTILE-TAKEOVER";
    private const string HostileTakeoverCardNo = "SFD·202/221";
    private const string HostileTakeoverTargetObjectId = "P2-BATTLEFIELD-UNIT";
    private const string ControlBattlefieldObjectId = "BF-CONTROL";
    private const string ControlTargetObjectId = "P2-CONTROL-TARGET";
    private const string ControlRemainingOccupantObjectId = "P2-CONTROL-REMAINING";

    [Fact]
    public async Task HostileTakeoverGainsControlReadiesEnemyBattlefieldUnitAndSchedulesReturn()
    {
        var engine = new CoreRuleEngine();
        var state = BuildHostileTakeoverState();

        var played = await PlayHostileTakeoverAsync(engine, state, "P2-BATTLEFIELD-UNIT");
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-hostile-takeover-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hostile-takeover-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains("P2-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Equal("P2", p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].OwnerId);
        Assert.Equal("P1", p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].ControllerId);
        Assert.False(p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.Equal(2, p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(5, p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].Power);
        Assert.Contains(
            "RETURN_CONTROL_TO_OWNER_AT_TURN_END:P2",
            p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].UntilEndOfTurnEffects);
        Assert.Equal(["P1-SPELL-HOSTILE-TAKEOVER"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-HOSTILE-TAKEOVER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("wasExhausted", out var wasExhausted)
            && wasExhausted is true);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONTROL_GAINED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-HOSTILE-TAKEOVER", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["ownerId"] as string, "P2", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["controllerId"] as string, "P1", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["previousControllerId"] as string, "P2", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BATTLEFIELD", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-FRIENDLY-BATTLEFIELD-UNIT")]
    [InlineData("P2-BASE-UNIT")]
    [InlineData("P2-STALE-UNIT")]
    [InlineData("P2-FACE-DOWN-STANDBY")]
    [InlineData("P2-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P2-BATTLEFIELD-SPELL")]
    [InlineData("P2-BATTLEFIELD-RUNE")]
    [InlineData("P2-HAND-UNIT")]
    public async Task HostileTakeoverRejectsInvalidTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildHostileTakeoverState();

        var result = await PlayHostileTakeoverAsync(new CoreRuleEngine(), state, targetObjectId);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HOSTILE-TAKEOVER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FRIENDLY-BATTLEFIELD-UNIT"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-HAND-UNIT"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-BASE-UNIT"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(
            [
                "P2-BATTLEFIELD-UNIT",
                "P2-FACE-DOWN-STANDBY",
                "P2-BATTLEFIELD-EQUIPMENT",
                "P2-BATTLEFIELD-SPELL",
                "P2-BATTLEFIELD-RUNE"
            ],
            result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Equal("P2", result.State.CardObjects["P2-BATTLEFIELD-UNIT"].OwnerId);
        Assert.Equal("P2", result.State.CardObjects["P2-BATTLEFIELD-UNIT"].ControllerId);
        Assert.True(result.State.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.Empty(result.State.CardObjects["P2-BATTLEFIELD-UNIT"].UntilEndOfTurnEffects);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_CONTROL_GAINED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal));
    }

    [Fact]
    public void HostileTakeoverMainActionPlayCardPromptListsOnlyLegalEnemyBattlefieldUnitTarget()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildHostileTakeoverState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, HostileTakeoverObjectId, StringComparison.Ordinal));

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        Assert.True(metadata.ContainsKey("sourceRequirements"));
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, HostileTakeoverObjectId, StringComparison.Ordinal));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var metadataTargetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
            .Select(choice => choice.Id)
            .ToArray();

        Assert.Equal([HostileTakeoverTargetObjectId], metadataTargetChoiceIds);

        var targetIds = (playCandidate.Targets ?? []).Select(target => target.Id).ToArray();
        Assert.Equal([HostileTakeoverTargetObjectId], targetIds);
        foreach (var invalidTargetObjectId in new[]
        {
            "P1-FRIENDLY-BATTLEFIELD-UNIT",
            "P2-BASE-UNIT",
            "P2-STALE-UNIT",
            "P2-FACE-DOWN-STANDBY",
            "P2-BATTLEFIELD-EQUIPMENT",
            "P2-BATTLEFIELD-SPELL",
            "P2-BATTLEFIELD-RUNE",
            "P2-HAND-UNIT"
        })
        {
            Assert.DoesNotContain(invalidTargetObjectId, targetIds);
            Assert.DoesNotContain(invalidTargetObjectId, metadataTargetChoiceIds);
        }
    }

    [Fact]
    public async Task HostileTakeoverPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildHostileTakeoverState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            HostileTakeoverObjectId,
            HostileTakeoverCardNo,
            [HostileTakeoverTargetObjectId]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, HostileTakeoverObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var reorderedStaleRawCommand = ReorderedPromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        Assert.NotEqual(staleRawCommand.GetRawText(), reorderedStaleRawCommand.GetRawText());
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(reorderedStaleRawCommand));
        AssertPromptScopedPlayCardRawCommand(reorderedStaleRawCommand, prompt);
        Assert.False(reorderedStaleRawCommand.TryGetProperty("clientNote", out _));
        const string acceptedClientIntentId = "intent-hostile-takeover-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-hostile-takeover-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertHostileTakeoverStackPriorityState(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var p1PromptAfterAccepted = MatchStateHasher.HashValue(session.PromptFor("P1"));
        var p2PromptAfterAccepted = MatchStateHasher.HashValue(session.PromptFor("P2"));
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PlayCard, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(acceptedJournalEntry.RawCommand.Value));
        AssertPromptScopedPlayCardRawCommand(acceptedJournalEntry.RawCommand.Value, prompt);
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
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        AssertHostileTakeoverStackPriorityState(replay, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

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
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(rejectedJournalEntry.RawCommand.Value));
        AssertPromptScopedPlayCardRawCommand(rejectedJournalEntry.RawCommand.Value, prompt);
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));
        var journalHashAfterReplay = MatchStateHasher.HashValue(journal.Entries);

        var reorderedDuplicateRejected = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            reorderedStaleRawCommand,
            CancellationToken.None);

        Assert.False(reorderedDuplicateRejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, reorderedDuplicateRejected.ErrorCode);
        Assert.Equal(replay.ErrorMessage, reorderedDuplicateRejected.ErrorMessage);
        Assert.Empty(reorderedDuplicateRejected.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(reorderedDuplicateRejected.State));
        Assert.Equal(replay.State.Tick, reorderedDuplicateRejected.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(reorderedDuplicateRejected.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(reorderedDuplicateRejected.Snapshots));
        AssertHostileTakeoverStackPriorityState(reorderedDuplicateRejected, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));

        var duplicateRejected = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
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
        AssertHostileTakeoverStackPriorityState(duplicateRejected, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));

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
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        AssertHostileTakeoverStackPriorityState(conflict, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HostileTakeoverPreservesPreciseBattlefieldAndStartsContestTasksWhenOpponentOccupantRemains()
    {
        var engine = new CoreRuleEngine();
        var state = BuildHostileTakeoverPreciseBattlefieldState(includeRemainingOpponentOccupant: true);

        var played = await PlayHostileTakeoverAsync(engine, state, ControlTargetObjectId);
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-hostile-takeover-precise-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hostile-takeover-precise-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal("P2", p2Pass.State.CardObjects[ControlTargetObjectId].OwnerId);
        Assert.Equal("P1", p2Pass.State.CardObjects[ControlTargetObjectId].ControllerId);
        Assert.Contains(ControlTargetObjectId, p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(ControlTargetObjectId, p2Pass.State.PlayerZones["P2"].Battlefields);

        var targetLocation = p2Pass.State.ObjectLocations[ControlTargetObjectId];
        Assert.Equal("P1", targetLocation.PlayerId);
        Assert.Equal("BATTLEFIELD", targetLocation.Zone);
        Assert.Equal(ControlBattlefieldObjectId, targetLocation.BattlefieldObjectId);

        var battlefield = p2Pass.State.BattlefieldStates[ControlBattlefieldObjectId];
        Assert.True(battlefield.Contested);
        Assert.Equal(["P1", "P2"], battlefield.OccupantControllerIds);
        Assert.Contains(ControlTargetObjectId, battlefield.OccupantObjectIds);
        Assert.Contains(ControlRemainingOccupantObjectId, battlefield.OccupantObjectIds);
        Assert.Equal(TimingStates.SpellDuelOpen, p2Pass.State.TimingState);
        Assert.Equal("P1", p2Pass.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", p2Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{ControlBattlefieldObjectId}", p2Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL", "START_BATTLE"],
            p2Pass.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, ControlBattlefieldObjectId, StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, ControlBattlefieldObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["taskId"] as string, $"task:start-spell-duel:{ControlBattlefieldObjectId}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HostileTakeoverPreservesPreciseBattlefieldAndStartsNonBattleContestWhenNoOpponentOccupantRemains()
    {
        var engine = new CoreRuleEngine();
        var state = BuildHostileTakeoverPreciseBattlefieldState(includeRemainingOpponentOccupant: false);

        var played = await PlayHostileTakeoverAsync(engine, state, ControlTargetObjectId);
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-hostile-takeover-no-contest-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hostile-takeover-no-contest-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        var targetLocation = p2Pass.State.ObjectLocations[ControlTargetObjectId];
        Assert.Equal("P1", targetLocation.PlayerId);
        Assert.Equal("BATTLEFIELD", targetLocation.Zone);
        Assert.Equal(ControlBattlefieldObjectId, targetLocation.BattlefieldObjectId);

        var battlefield = p2Pass.State.BattlefieldStates[ControlBattlefieldObjectId];
        Assert.True(battlefield.Contested);
        Assert.Equal(["P1"], battlefield.OccupantControllerIds);
        Assert.Equal(TimingStates.SpellDuelOpen, p2Pass.State.TimingState);
        Assert.Equal("P1", p2Pass.State.FocusPlayerId);
        Assert.Equal("SPELL_DUEL_TASKS", p2Pass.State.PendingTaskQueue.Phase);
        Assert.Equal($"task:start-spell-duel:{ControlBattlefieldObjectId}", p2Pass.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(
            ["BATTLEFIELD_CONTESTED", "START_SPELL_DUEL"],
            p2Pass.State.PendingTaskQueue.Tasks.Select(task => task.Kind).ToArray());
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLEFIELD_CONTESTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, ControlBattlefieldObjectId, StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "SPELL_DUEL_STARTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["battlefieldObjectId"] as string, ControlBattlefieldObjectId, StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayHostileTakeoverAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-hostile-takeover-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-HOSTILE-TAKEOVER",
                "SFD·202/221",
                [targetObjectId]),
            CancellationToken.None);
    }

    private static JsonElement PromptScopedPlayCardRawCommand(
        PlayCardCommand command,
        ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PlayCard,
            cardObjectId = command.SourceObjectId,
            cardNo = command.CardNo,
            targetObjectIds = command.TargetObjectIds,
            optionalCosts = command.OptionalCosts ?? Array.Empty<string>(),
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
    }

    private static JsonElement ReorderedPromptScopedPlayCardRawCommand(
        PlayCardCommand command,
        ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            snapshotTick = prompt.SnapshotTick,
            promptId = prompt.PromptId,
            optionalCosts = command.OptionalCosts ?? Array.Empty<string>(),
            targetObjectIds = command.TargetObjectIds,
            cardNo = command.CardNo,
            cardObjectId = command.SourceObjectId,
            cmdType = CommandTypes.PlayCard
        });
    }

    private static JsonElement PromptScopedPlayCardRawCommandWithClientNote(
        PlayCardCommand command,
        ActionPromptDto prompt,
        string clientNote)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PlayCard,
            cardObjectId = command.SourceObjectId,
            cardNo = command.CardNo,
            targetObjectIds = command.TargetObjectIds,
            optionalCosts = command.OptionalCosts ?? Array.Empty<string>(),
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote
        });
    }

    private static void AssertPromptScopedPlayCardRawCommand(
        JsonElement rawCommand,
        ActionPromptDto prompt)
    {
        Assert.Equal(CommandTypes.PlayCard, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(HostileTakeoverObjectId, rawCommand.GetProperty("cardObjectId").GetString());
        Assert.Equal(HostileTakeoverCardNo, rawCommand.GetProperty("cardNo").GetString());
        Assert.Equal(
            [HostileTakeoverTargetObjectId],
            rawCommand.GetProperty("targetObjectIds")
                .EnumerateArray()
                .Select(target => target.GetString()!)
                .ToArray());
        Assert.Empty(rawCommand.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static StackItemState AssertHostileTakeoverStackPriorityState(
        ResolutionResult result,
        StackItemState? expectedStackItem = null)
    {
        Assert.Equal(1, result.State.Tick);
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FRIENDLY-BATTLEFIELD-UNIT"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-HAND-UNIT"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-BASE-UNIT"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(
            [
                HostileTakeoverTargetObjectId,
                "P2-FACE-DOWN-STANDBY",
                "P2-BATTLEFIELD-EQUIPMENT",
                "P2-BATTLEFIELD-SPELL",
                "P2-BATTLEFIELD-RUNE"
            ],
            result.State.PlayerZones["P2"].Battlefields);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(2, result.State.CardObjects[HostileTakeoverTargetObjectId].Damage);
        Assert.Equal(5, result.State.CardObjects[HostileTakeoverTargetObjectId].Power);
        Assert.True(result.State.CardObjects[HostileTakeoverTargetObjectId].IsExhausted);
        Assert.Equal("P2", result.State.CardObjects[HostileTakeoverTargetObjectId].OwnerId);
        Assert.Equal("P2", result.State.CardObjects[HostileTakeoverTargetObjectId].ControllerId);
        Assert.Empty(result.State.CardObjects[HostileTakeoverTargetObjectId].UntilEndOfTurnEffects);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(HostileTakeoverObjectId, stackItem.SourceObjectId);
        Assert.Equal(HostileTakeoverCardNo, stackItem.CardNo);
        Assert.Equal([HostileTakeoverTargetObjectId], stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        if (expectedStackItem is not null)
        {
            Assert.Equal(expectedStackItem.StackItemId, stackItem.StackItemId);
            Assert.Equal(expectedStackItem.ControllerId, stackItem.ControllerId);
            Assert.Equal(expectedStackItem.SourceObjectId, stackItem.SourceObjectId);
            Assert.Equal(expectedStackItem.EffectKind, stackItem.EffectKind);
            Assert.Equal(expectedStackItem.CardNo, stackItem.CardNo);
            Assert.Equal(expectedStackItem.TargetObjectIds, stackItem.TargetObjectIds);
            Assert.Equal(expectedStackItem.OptionalCosts, stackItem.OptionalCosts);
            Assert.Equal(expectedStackItem.DamageAmount, stackItem.DamageAmount);
            Assert.Equal(expectedStackItem.EffectRepeatCount, stackItem.EffectRepeatCount);
        }

        return stackItem;
    }

    private static MatchState BuildHostileTakeoverState()
    {
        return new MatchState(
            roomId: "hostile-takeover-guard-test",
            tick: 0,
            turnNumber: 1,
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
                ["P1"] = new(5, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HOSTILE-TAKEOVER"],
                    Battlefields = ["P1-FRIENDLY-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-UNIT"],
                    Base = ["P2-BASE-UNIT"],
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-UNIT",
                        "P2-FACE-DOWN-STANDBY",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HOSTILE-TAKEOVER"] = new(
                    "P1-SPELL-HOSTILE-TAKEOVER",
                    cardNo: "SFD·202/221",
                    manaCost: 5,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FRIENDLY-BATTLEFIELD-UNIT"] = new(
                    "P1-FRIENDLY-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-UNIT"] = new(
                    "P2-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    damage: 2,
                    power: 5,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BASE-UNIT"] = new(
                    "P2-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-STALE-UNIT"] = new(
                    "P2-STALE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-FACE-DOWN-STANDBY"] = new(
                    "P2-FACE-DOWN-STANDBY",
                    isFaceDown: true,
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-EQUIPMENT"] = new(
                    "P2-BATTLEFIELD-EQUIPMENT",
                    cardNo: "SFD·139/221",
                    power: 1,
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-SPELL"] = new(
                    "P2-BATTLEFIELD-SPELL",
                    cardNo: "OGN·169/298",
                    power: 1,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-RUNE"] = new(
                    "P2-BATTLEFIELD-RUNE",
                    cardNo: "RUNES·001",
                    power: 1,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-HAND-UNIT"] = new(
                    "P2-HAND-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });
    }

    private static MatchState BuildHostileTakeoverPreciseBattlefieldState(bool includeRemainingOpponentOccupant)
    {
        var p2Battlefields = includeRemainingOpponentOccupant
            ? new[] { ControlBattlefieldObjectId, ControlTargetObjectId, ControlRemainingOccupantObjectId }
            : [ControlBattlefieldObjectId, ControlTargetObjectId];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-SPELL-HOSTILE-TAKEOVER"] = new(
                "P1-SPELL-HOSTILE-TAKEOVER",
                cardNo: "SFD·202/221",
                manaCost: 5,
                tags: [CardObjectTags.SpellCard],
                ownerId: "P1",
                controllerId: "P1"),
            [ControlBattlefieldObjectId] = new(
                ControlBattlefieldObjectId,
                cardNo: "SFD·214/221",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P2",
                controllerId: "P2"),
            [ControlTargetObjectId] = new(
                ControlTargetObjectId,
                cardNo: "SFD·125/221",
                damage: 2,
                power: 5,
                isExhausted: true,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2")
        };
        if (includeRemainingOpponentOccupant)
        {
            cardObjects[ControlRemainingOccupantObjectId] = new(
                ControlRemainingOccupantObjectId,
                cardNo: "SFD·126/221",
                power: 3,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2");
        }

        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-SPELL-HOSTILE-TAKEOVER"] = new("P1", "HAND"),
            [ControlBattlefieldObjectId] = new("P2", "BATTLEFIELD"),
            [ControlTargetObjectId] = new("P2", "BATTLEFIELD", ControlBattlefieldObjectId)
        };
        if (includeRemainingOpponentOccupant)
        {
            objectLocations[ControlRemainingOccupantObjectId] =
                new ObjectLocationState("P2", "BATTLEFIELD", ControlBattlefieldObjectId);
        }

        return new MatchState(
            roomId: "hostile-takeover-precise-battlefield-test",
            tick: 0,
            turnNumber: 1,
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
                ["P1"] = new(5, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HOSTILE-TAKEOVER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
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
