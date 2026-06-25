using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SfurSongGuardTests
{
    private const string SfurSongObjectId = "P1-EQUIPMENT-SFUR-SONG";
    private const string SfurSongCardNo = "SFD·059/221";
    private const string SfurSongEffectKind = "SFUR_SONG_PLAY_EQUIPMENT";

    [Fact]
    public async Task SfurSongPlayCardWithNoTargetsUsesStackAndResolvesToBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildSfurSongState();

        var played = await PlaySfurSongAsync(engine, state, "P1-EQUIPMENT-SFUR-SONG", []);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-EQUIPMENT-SFUR-SONG", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "SFUR_SONG_PLAY_EQUIPMENT", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-sfur-song-play-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-sfur-song-play-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-SFUR-SONG", "P1-FACE-DOWN-STANDBY-SFUR-SONG", "P1-EQUIPMENT-SFUR-SONG"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.StackItems);

        var equipment = p2Pass.State.CardObjects["P1-EQUIPMENT-SFUR-SONG"];
        Assert.Equal("SFD·059/221", equipment.CardNo);
        Assert.Equal("P1", equipment.OwnerId);
        Assert.Equal("P1", equipment.ControllerId);
        Assert.Equal([CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], equipment.Tags);
        Assert.False(equipment.IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-EQUIPMENT-SFUR-SONG", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentObjectId"] as string, "P1-EQUIPMENT-SFUR-SONG", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentName"] as string, "斯弗尔尚歌", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal));
    }

    [Fact]
    public void SfurSongMainActionPlayCardPromptDoesNotExposeTargetsForEquipmentPlay()
    {
        var state = BuildSfurSongState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
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
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, SfurSongObjectId, StringComparison.Ordinal));
        var targetIds = (playCandidate.Targets ?? []).Select(target => target.Id).ToArray();
        Assert.Empty(targetIds);

        var invalidTargetObjectIds = new[]
        {
            "P1-TARGET-UNIT",
            "P1-BASE-SFUR-SONG",
            "P1-FACE-DOWN-STANDBY-SFUR-SONG",
            "P2-EQUIPMENT-SFUR-SONG"
        };
        foreach (var invalidTargetObjectId in invalidTargetObjectIds)
        {
            Assert.DoesNotContain(invalidTargetObjectId, targetIds);
        }

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, SfurSongObjectId, StringComparison.Ordinal));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        Assert.Empty(targetChoicesByIndex);
        if (sourceRequirement.TryGetValue("minTargetCount", out var rawMinTargetCount))
        {
            Assert.Equal(0, Assert.IsType<int>(rawMinTargetCount));
        }
        if (sourceRequirement.TryGetValue("maxTargetCount", out var rawMaxTargetCount))
        {
            Assert.Equal(0, Assert.IsType<int>(rawMaxTargetCount));
        }

        var metadataTargetIds = targetChoicesByIndex.Values
            .SelectMany(rawTargetChoices => Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(rawTargetChoices))
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Empty(metadataTargetIds);
        foreach (var invalidTargetObjectId in invalidTargetObjectIds)
        {
            Assert.DoesNotContain(invalidTargetObjectId, metadataTargetIds);
        }
    }

    [Fact]
    public async Task SfurSongPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildSfurSongState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            SfurSongObjectId,
            SfurSongCardNo,
            []);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, SfurSongObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var reorderedStaleRawCommand = ReorderedPromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-sfur-song-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-sfur-song-stale-prompt-replay";
        Assert.NotEqual(staleRawCommand.GetRawText(), reorderedStaleRawCommand.GetRawText());
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(reorderedStaleRawCommand));
        AssertPromptScopedPlayCardRawCommand(reorderedStaleRawCommand, prompt);
        Assert.False(reorderedStaleRawCommand.TryGetProperty("clientNote", out _));

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertSfurSongStackPriorityState(accepted);
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
        AssertSfurSongStackPriorityState(replay, acceptedStackItem);
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
        AssertSfurSongStackPriorityState(reorderedReplay, acceptedStackItem);
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
        AssertSfurSongStackPriorityState(duplicateReplay, acceptedStackItem);
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
        AssertSfurSongStackPriorityState(conflict, acceptedStackItem);
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

    [Theory]
    [InlineData("P1-EQUIPMENT-SFUR-SONG", "P1-TARGET-UNIT", 3, ErrorCodes.InvalidTarget)]
    [InlineData("P1-BASE-SFUR-SONG", "", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P2-EQUIPMENT-SFUR-SONG", "", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P1-FACE-DOWN-STANDBY-SFUR-SONG", "", 3, ErrorCodes.CardNotInHand)]
    [InlineData("P1-EQUIPMENT-SFUR-SONG", "", 2, ErrorCodes.InsufficientCost)]
    public async Task SfurSongPlayCardRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        string targetObjectId,
        int mana,
        string expectedErrorCode)
    {
        var state = BuildSfurSongState(mana);
        var targetObjectIds = string.IsNullOrWhiteSpace(targetObjectId) ? Array.Empty<string>() : [targetObjectId];

        var result = await PlaySfurSongAsync(
            new CoreRuleEngine(),
            state,
            sourceObjectId,
            targetObjectIds);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(mana, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SFUR-SONG"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-TARGET-UNIT", "P1-BASE-SFUR-SONG", "P1-FACE-DOWN-STANDBY-SFUR-SONG"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-EQUIPMENT-SFUR-SONG"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.False(result.State.CardObjects["P1-EQUIPMENT-SFUR-SONG"].IsFaceDown);
        Assert.Null(result.State.CardObjects["P1-FACE-DOWN-STANDBY-SFUR-SONG"].CardNo);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY-SFUR-SONG"].IsFaceDown);
        Assert.Equal(
            [CardObjectTags.EquipmentCard, CardObjectTags.Standby],
            result.State.CardObjects["P1-FACE-DOWN-STANDBY-SFUR-SONG"].Tags);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "EQUIPMENT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlaySfurSongAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-sfur-song-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                sourceObjectId,
                "SFD·059/221",
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
        Assert.Equal(SfurSongObjectId, rawCommand.GetProperty("cardObjectId").GetString());
        Assert.Equal(SfurSongCardNo, rawCommand.GetProperty("cardNo").GetString());
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Empty(rawCommand.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static StackItemState AssertSfurSongStackPriorityState(
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
        Assert.Equal(
            ["P1-TARGET-UNIT", "P1-BASE-SFUR-SONG", "P1-FACE-DOWN-STANDBY-SFUR-SONG"],
            result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal("STACK", result.State.ObjectLocations[SfurSongObjectId].Zone);

        var equipment = result.State.CardObjects[SfurSongObjectId];
        Assert.Equal(SfurSongCardNo, equipment.CardNo);
        Assert.Equal("P1", equipment.OwnerId);
        Assert.Equal("P1", equipment.ControllerId);
        Assert.Equal([CardObjectTags.EquipmentCard], equipment.Tags);
        Assert.False(equipment.IsExhausted);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(SfurSongObjectId, stackItem.SourceObjectId);
        Assert.Equal(SfurSongCardNo, stackItem.CardNo);
        Assert.Equal(SfurSongEffectKind, stackItem.EffectKind);
        Assert.Empty(stackItem.TargetObjectIds);
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
            Assert.Equal(expectedStackItem.PlayedAfterAnotherCardThisTurn, stackItem.PlayedAfterAnotherCardThisTurn);
            Assert.Equal(expectedStackItem.Destination, stackItem.Destination);
            Assert.Equal(expectedStackItem.TimingContext, stackItem.TimingContext);
        }

        return stackItem;
    }

    private static MatchState BuildSfurSongState(int mana = 3)
    {
        return new MatchState(
            roomId: "sfur-song-guard-test",
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
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SFUR-SONG"],
                    Base =
                    [
                        "P1-TARGET-UNIT",
                        "P1-BASE-SFUR-SONG",
                        "P1-FACE-DOWN-STANDBY-SFUR-SONG"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-EQUIPMENT-SFUR-SONG"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-SFUR-SONG"] = SfurSong("P1-EQUIPMENT-SFUR-SONG"),
                ["P1-BASE-SFUR-SONG"] = SfurSong("P1-BASE-SFUR-SONG"),
                ["P1-FACE-DOWN-STANDBY-SFUR-SONG"] = SfurSong(
                    "P1-FACE-DOWN-STANDBY-SFUR-SONG",
                    isFaceDown: true,
                    tags: [CardObjectTags.EquipmentCard, CardObjectTags.Standby]),
                ["P2-EQUIPMENT-SFUR-SONG"] = SfurSong(
                    "P2-EQUIPMENT-SFUR-SONG",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-TARGET-UNIT"] = new(
                    "P1-TARGET-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });
    }

    private static CardObjectState SfurSong(
        string objectId,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "SFD·059/221",
            isFaceDown: isFaceDown,
            manaCost: 3,
            tags: tags ?? [CardObjectTags.EquipmentCard],
            ownerId: ownerId,
            controllerId: controllerId);
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
