using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class FirestormEnemyBattlefieldDamageGuardTests
{
    private const string FirestormObjectId = "P1-SPELL-FIRESTORM";
    private const string FirestormCardNo = "OGS·002/024";

    [Fact]
    public async Task FirestormDamagesOnlyEnemyPublicBattlefieldUnits()
    {
        var engine = new CoreRuleEngine();
        var state = BuildFirestormState();

        var played = await PlayFirestormAsync(engine, state, []);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "FIRESTORM_DAMAGE_ALL_ENEMY_BATTLEFIELD_UNITS_3", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-firestorm-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-firestorm-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-FIRESTORM"], p2Pass.State.PlayerZones["P1"].Graveyard);

        Assert.Equal(0, p2Pass.State.CardObjects["P1-FRIENDLY-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BASE-UNIT"].Damage);
        Assert.Equal(3, p2Pass.State.CardObjects["P2-FIRESTORM-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Equal(3, p2Pass.State.CardObjects["P2-FIRESTORM-BATTLEFIELD-UNIT-002"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BATTLEFIELD-EQUIPMENT"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BATTLEFIELD-SPELL"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-BATTLEFIELD-RUNE"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-FACE-DOWN-STANDBY"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"].Damage);

        var damageTargetIds = p2Pass.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal))
            .Select(gameEvent => gameEvent.Payload["targetObjectId"] as string)
            .ToArray();
        Assert.Equal(2, damageTargetIds.Length);
        Assert.Contains("P2-FIRESTORM-BATTLEFIELD-UNIT-001", damageTargetIds);
        Assert.Contains("P2-FIRESTORM-BATTLEFIELD-UNIT-002", damageTargetIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-EQUIPMENT", damageTargetIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-SPELL", damageTargetIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-RUNE", damageTargetIds);
        Assert.DoesNotContain("P2-FACE-DOWN-STANDBY", damageTargetIds);
        Assert.DoesNotContain("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT", damageTargetIds);
    }

    [Fact]
    public void FirestormPlayCardPromptDoesNotExposeExplicitTargets()
    {
        var state = BuildFirestormState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal)
                && candidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, FirestormObjectId, StringComparison.Ordinal));
        var targetCandidates = playCandidate.Targets ?? [];
        var targetIds = targetCandidates.Select(target => target.Id).ToArray();
        Assert.Empty(targetCandidates);
        Assert.DoesNotContain("P2-FIRESTORM-BATTLEFIELD-UNIT-001", targetIds);
        Assert.DoesNotContain("P2-FIRESTORM-BATTLEFIELD-UNIT-002", targetIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-EQUIPMENT", targetIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-SPELL", targetIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-RUNE", targetIds);
        Assert.DoesNotContain("P2-FACE-DOWN-STANDBY", targetIds);
        Assert.DoesNotContain("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT", targetIds);
        Assert.DoesNotContain("P2-BASE-UNIT", targetIds);
        Assert.DoesNotContain("P1-FRIENDLY-BATTLEFIELD-UNIT", targetIds);

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        Assert.DoesNotContain("targetChoicesByIndex", metadata.Keys);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var firestormRequirement = Assert.Single(
            sourceRequirements,
            requirement => requirement.TryGetValue("sourceObjectId", out var sourceObjectId)
                && string.Equals(sourceObjectId as string, FirestormObjectId, StringComparison.Ordinal));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            firestormRequirement["targetChoicesByIndex"]);
        Assert.Empty(targetChoicesByIndex);
        if (firestormRequirement.TryGetValue("legalTargetSelections", out var rawLegalTargetSelections))
        {
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyList<string>>>(rawLegalTargetSelections));
        }

        if (firestormRequirement.TryGetValue("minTargetCount", out var rawMinTargetCount))
        {
            Assert.Equal(0, Assert.IsType<int>(rawMinTargetCount));
        }

        if (firestormRequirement.TryGetValue("maxTargetCount", out var rawMaxTargetCount))
        {
            Assert.Equal(0, Assert.IsType<int>(rawMaxTargetCount));
        }
    }

    [Theory]
    [InlineData("P2-FIRESTORM-BATTLEFIELD-UNIT-001")]
    [InlineData("P2-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P2-BATTLEFIELD-SPELL")]
    [InlineData("P2-BATTLEFIELD-RUNE")]
    [InlineData("P2-FACE-DOWN-STANDBY")]
    [InlineData("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT")]
    [InlineData("P2-STALE-UNIT")]
    [InlineData("P2-BASE-UNIT")]
    [InlineData("P1-FRIENDLY-BATTLEFIELD-UNIT")]
    public async Task FirestormRejectsExplicitTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildFirestormState();

        var result = await PlayFirestormAsync(new CoreRuleEngine(), state, [targetObjectId]);

        AssertRejectedWithoutMutation(state, result);
    }

    [Fact]
    public async Task FirestormPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildFirestormState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            FirestormObjectId,
            FirestormCardNo,
            []);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, FirestormObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var reorderedStaleRawCommand = ReorderedPromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        Assert.NotEqual(staleRawCommand.GetRawText(), reorderedStaleRawCommand.GetRawText());
        AssertPromptScopedPlayCardRawCommand(reorderedStaleRawCommand, prompt);
        Assert.False(reorderedStaleRawCommand.TryGetProperty("clientNote", out _));
        const string acceptedClientIntentId = "intent-firestorm-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-firestorm-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertFirestormStackPriorityState(accepted);
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
        AssertFirestormStackPriorityState(replay, acceptedStackItem);
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

        var reorderedReplay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            reorderedStaleRawCommand,
            CancellationToken.None);

        Assert.False(reorderedReplay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, reorderedReplay.ErrorCode);
        Assert.Equal(replay.ErrorMessage, reorderedReplay.ErrorMessage);
        Assert.Empty(reorderedReplay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(reorderedReplay.State));
        Assert.Equal(replay.State.Tick, reorderedReplay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(reorderedReplay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(reorderedReplay.Snapshots));
        AssertFirestormStackPriorityState(reorderedReplay, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));

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
        Assert.Equal(replay.State.Tick, duplicateReplay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        AssertFirestormStackPriorityState(duplicateReplay, acceptedStackItem);
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
        AssertFirestormStackPriorityState(conflict, acceptedStackItem);
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

    private static async Task<ResolutionResult> PlayFirestormAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-firestorm-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-FIRESTORM",
                "OGS·002/024",
                targetObjectIds),
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
        Assert.Equal(FirestormObjectId, rawCommand.GetProperty("cardObjectId").GetString());
        Assert.Equal(FirestormCardNo, rawCommand.GetProperty("cardNo").GetString());
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Empty(rawCommand.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static StackItemState AssertFirestormStackPriorityState(
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
        Assert.Equal(["P2-BASE-UNIT"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(
            [
                "P2-FIRESTORM-BATTLEFIELD-UNIT-001",
                "P2-FIRESTORM-BATTLEFIELD-UNIT-002",
                "P2-BATTLEFIELD-EQUIPMENT",
                "P2-BATTLEFIELD-SPELL",
                "P2-BATTLEFIELD-RUNE",
                "P2-FACE-DOWN-STANDBY",
                "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"
            ],
            result.State.PlayerZones["P2"].Battlefields);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal("STACK", result.State.ObjectLocations[FirestormObjectId].Zone);
        Assert.Equal(0, result.State.CardObjects["P1-FRIENDLY-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(0, result.State.CardObjects["P2-BASE-UNIT"].Damage);
        Assert.Equal(0, result.State.CardObjects["P2-FIRESTORM-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Equal(0, result.State.CardObjects["P2-FIRESTORM-BATTLEFIELD-UNIT-002"].Damage);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-EQUIPMENT"].Damage);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-SPELL"].Damage);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-RUNE"].Damage);
        Assert.Equal(0, result.State.CardObjects["P2-FACE-DOWN-STANDBY"].Damage);
        Assert.Equal(0, result.State.CardObjects["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(FirestormCardNo, result.State.CardObjects[FirestormObjectId].CardNo);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(FirestormObjectId, stackItem.SourceObjectId);
        Assert.Equal(FirestormCardNo, stackItem.CardNo);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        Assert.Equal("FIRESTORM_DAMAGE_ALL_ENEMY_BATTLEFIELD_UNITS_3", stackItem.EffectKind);
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

    private static void AssertRejectedWithoutMutation(MatchState initialState, ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(initialState), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(6, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-FIRESTORM"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
    }

    private static MatchState BuildFirestormState()
    {
        return new MatchState(
            roomId: "firestorm-enemy-battlefield-damage-guard-test",
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
                ["P1"] = new(6, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FIRESTORM"],
                    Battlefields = ["P1-FRIENDLY-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT"],
                    Battlefields =
                    [
                        "P2-FIRESTORM-BATTLEFIELD-UNIT-001",
                        "P2-FIRESTORM-BATTLEFIELD-UNIT-002",
                        "P2-BATTLEFIELD-EQUIPMENT",
                        "P2-BATTLEFIELD-SPELL",
                        "P2-BATTLEFIELD-RUNE",
                        "P2-FACE-DOWN-STANDBY",
                        "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"
                    ]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-FIRESTORM"] = Firestorm(),
                ["P1-FRIENDLY-BATTLEFIELD-UNIT"] = Unit("P1-FRIENDLY-BATTLEFIELD-UNIT", power: 8),
                ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", power: 8, ownerId: "P2", controllerId: "P2"),
                ["P2-FIRESTORM-BATTLEFIELD-UNIT-001"] = Unit(
                    "P2-FIRESTORM-BATTLEFIELD-UNIT-001",
                    power: 8,
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-FIRESTORM-BATTLEFIELD-UNIT-002"] = Unit(
                    "P2-FIRESTORM-BATTLEFIELD-UNIT-002",
                    power: 8,
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-EQUIPMENT"] = NonUnit("P2-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P2"),
                ["P2-BATTLEFIELD-SPELL"] = NonUnit("P2-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P2"),
                ["P2-BATTLEFIELD-RUNE"] = NonUnit("P2-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P2"),
                ["P2-FACE-DOWN-STANDBY"] = Unit(
                    "P2-FACE-DOWN-STANDBY",
                    cardNo: null,
                    power: 8,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"] = Unit(
                    "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT",
                    power: 8),
                ["P2-STALE-UNIT"] = Unit("P2-STALE-UNIT", power: 8, ownerId: "P2", controllerId: "P2")
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

    private static CardObjectState Firestorm()
    {
        return new CardObjectState(
            "P1-SPELL-FIRESTORM",
            cardNo: "OGS·002/024",
            manaCost: 6,
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Unit(
        string objectId,
        string? cardNo = "SFD·125/221",
        int power = 2,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            isFaceDown: isFaceDown,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState NonUnit(
        string objectId,
        string cardNo,
        string tag,
        string playerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: 8,
            tags: [tag],
            ownerId: playerId,
            controllerId: playerId);
    }
}
