using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RideTheWindMoveGuardTests
{
    private const string RideTheWindObjectId = "P1-SPELL-RIDE-THE-WIND";
    private const string RideTheWindCardNo = "OGN·173/298";
    private const string RideTheWindTargetObjectId = "P1-BATTLEFIELD-UNIT";
    private const string RideTheWindEffectKind = "RIDE_THE_WIND_MOVE_FRIENDLY_BATTLEFIELD_UNIT_TO_BASE_READY";

    [Fact]
    public async Task RideTheWindReadiesAndMovesPublicFriendlyBattlefieldUnitToOwnerBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildRideTheWindState();

        var played = await PlayRideTheWindAsync(engine, state, "P1-BATTLEFIELD-UNIT");
        Assert.True(played.Accepted, played.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-ride-the-wind-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-ride-the-wind-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains("P1-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("P1-BATTLEFIELD-UNIT", p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(2, p2Pass.State.CardObjects["P1-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(4, p2Pass.State.CardObjects["P1-BATTLEFIELD-UNIT"].Power);
        Assert.False(p2Pass.State.CardObjects["P1-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && gameEvent.Payload.TryGetValue("wasExhausted", out var wasExhausted)
            && wasExhausted is true);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["ownerPlayerId"] as string, "P1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RideTheWindPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildRideTheWindState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            RideTheWindObjectId,
            RideTheWindCardNo,
            [RideTheWindTargetObjectId]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, RideTheWindObjectId, StringComparison.Ordinal));
        Assert.Contains(playCandidate.Targets ?? [], target => string.Equals(target.Id, RideTheWindTargetObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var reorderedStaleRawCommand = ReorderedPromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        Assert.NotEqual(staleRawCommand.GetRawText(), reorderedStaleRawCommand.GetRawText());
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(reorderedStaleRawCommand));
        AssertPromptScopedPlayCardRawCommand(reorderedStaleRawCommand, command, prompt, assertPropertyOrder: false);
        Assert.False(reorderedStaleRawCommand.TryGetProperty("clientNote", out _));
        Assert.Equal(8, changedStaleRawCommand.EnumerateObject().Count());
        Assert.Equal("changed-payload", changedStaleRawCommand.GetProperty("clientNote").GetString());
        const string acceptedClientIntentId = "intent-ride-the-wind-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-ride-the-wind-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertRideTheWindStackPriorityState(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedStackHash = MatchStateHasher.HashValue(accepted.State.StackItems);
        var acceptedZonesHash = MatchStateHasher.HashValue(accepted.State.PlayerZones);
        var acceptedObjectLocationsHash = MatchStateHasher.HashValue(accepted.State.ObjectLocations);
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
        AssertPromptScopedPlayCardRawCommand(acceptedJournalEntry.RawCommand.Value, command, prompt);
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
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(replay.State.StackItems));
        Assert.Equal(acceptedZonesHash, MatchStateHasher.HashValue(replay.State.PlayerZones));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(replay.State.ObjectLocations));
        AssertRideTheWindStackPriorityState(replay, acceptedStackItem);
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
        AssertPromptScopedPlayCardRawCommand(rejectedJournalEntry.RawCommand.Value, command, prompt);
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));
        Assert.Single(journal.Entries, entry => !entry.Accepted);
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
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(reorderedReplay.State.StackItems));
        Assert.Equal(acceptedZonesHash, MatchStateHasher.HashValue(reorderedReplay.State.PlayerZones));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(reorderedReplay.State.ObjectLocations));
        AssertRideTheWindStackPriorityState(reorderedReplay, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Single(journal.Entries, entry => !entry.Accepted);
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
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(duplicateReplay.State.StackItems));
        Assert.Equal(acceptedZonesHash, MatchStateHasher.HashValue(duplicateReplay.State.PlayerZones));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(duplicateReplay.State.ObjectLocations));
        AssertRideTheWindStackPriorityState(duplicateReplay, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Single(journal.Entries, entry => !entry.Accepted);
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
        Assert.Equal(acceptedStackHash, MatchStateHasher.HashValue(conflict.State.StackItems));
        Assert.Equal(acceptedZonesHash, MatchStateHasher.HashValue(conflict.State.PlayerZones));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(conflict.State.ObjectLocations));
        AssertRideTheWindStackPriorityState(conflict, acceptedStackItem);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Single(journal.Entries, entry => !entry.Accepted);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public void RideTheWindMainActionPlayCardPromptListsOnlyLegalFriendlyPublicBattlefieldUnitTarget()
    {
        var state = BuildRideTheWindState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var invalidTargetObjectIds = new[]
        {
            "P2-ENEMY-BATTLEFIELD-UNIT",
            "P1-BASE-UNIT",
            "P1-STALE-UNIT",
            "P1-FACE-DOWN-STANDBY",
            "P1-BATTLEFIELD-EQUIPMENT",
            "P1-BATTLEFIELD-SPELL",
            "P1-BATTLEFIELD-RUNE"
        };

        var prompt = session.PromptFor("P1");

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, RideTheWindObjectId, StringComparison.Ordinal));

        var targetIds = (playCandidate.Targets ?? []).Select(target => target.Id).ToArray();
        Assert.Equal([RideTheWindTargetObjectId], targetIds);
        foreach (var invalidTargetObjectId in invalidTargetObjectIds)
        {
            Assert.DoesNotContain(invalidTargetObjectId, targetIds);
        }

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        Assert.Contains("sourceRequirements", metadata.Keys);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(sourceRequirements);
        Assert.Equal(RideTheWindObjectId, sourceRequirement["sourceObjectId"]);
        Assert.Contains("targetChoicesByIndex", sourceRequirement.Keys);
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        Assert.Equal(["0"], choicesByIndex.Keys.ToArray());
        var firstTargetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
            .Select(choice => choice.Id)
            .ToArray();

        Assert.Equal([RideTheWindTargetObjectId], firstTargetChoiceIds);
        foreach (var invalidTargetObjectId in invalidTargetObjectIds)
        {
            Assert.DoesNotContain(invalidTargetObjectId, firstTargetChoiceIds);
        }
    }

    [Theory]
    [InlineData("P2-ENEMY-BATTLEFIELD-UNIT")]
    [InlineData("P1-BASE-UNIT")]
    [InlineData("P1-STALE-UNIT")]
    [InlineData("P1-FACE-DOWN-STANDBY")]
    [InlineData("P1-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P1-BATTLEFIELD-SPELL")]
    [InlineData("P1-BATTLEFIELD-RUNE")]
    public async Task RideTheWindRejectsInvalidTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildRideTheWindState();

        var result = await PlayRideTheWindAsync(new CoreRuleEngine(), state, targetObjectId);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-RIDE-THE-WIND"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            [
                "P1-BATTLEFIELD-UNIT",
                "P1-FACE-DOWN-STANDBY",
                "P1-BATTLEFIELD-EQUIPMENT",
                "P1-BATTLEFIELD-SPELL",
                "P1-BATTLEFIELD-RUNE"
            ],
            result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-ENEMY-BATTLEFIELD-UNIT"], result.State.PlayerZones["P2"].Battlefields);
        Assert.True(result.State.CardObjects["P1-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.Equal(2, result.State.CardObjects["P1-BATTLEFIELD-UNIT"].Damage);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayRideTheWindAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-ride-the-wind-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-RIDE-THE-WIND",
                "OGN·173/298",
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
        PlayCardCommand command,
        ActionPromptDto prompt,
        bool assertPropertyOrder = true)
    {
        if (assertPropertyOrder)
        {
            Assert.Equal(
                ["cmdType", "cardObjectId", "cardNo", "targetObjectIds", "optionalCosts", "promptId", "snapshotTick"],
                rawCommand.EnumerateObject().Select(property => property.Name).ToArray());
        }

        Assert.Equal(CommandTypes.PlayCard, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(command.SourceObjectId, rawCommand.GetProperty("cardObjectId").GetString());
        Assert.Equal(command.CardNo, rawCommand.GetProperty("cardNo").GetString());
        Assert.Equal(
            command.TargetObjectIds,
            rawCommand.GetProperty("targetObjectIds")
                .EnumerateArray()
                .Select(target => target.GetString()!)
                .ToArray());
        Assert.Empty(rawCommand.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static StackItemState AssertRideTheWindStackPriorityState(
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
        Assert.Equal(RunePool.Empty, result.State.RunePools["P2"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            [
                RideTheWindTargetObjectId,
                "P1-FACE-DOWN-STANDBY",
                "P1-BATTLEFIELD-EQUIPMENT",
                "P1-BATTLEFIELD-SPELL",
                "P1-BATTLEFIELD-RUNE"
            ],
            result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-ENEMY-BATTLEFIELD-UNIT"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal("STACK", result.State.ObjectLocations[RideTheWindObjectId].Zone);
        Assert.Equal("BATTLEFIELD", result.State.ObjectLocations[RideTheWindTargetObjectId].Zone);

        var targetUnit = result.State.CardObjects[RideTheWindTargetObjectId];
        Assert.Equal(2, targetUnit.Damage);
        Assert.Equal(4, targetUnit.Power);
        Assert.True(targetUnit.IsExhausted);
        Assert.Equal("P1", targetUnit.OwnerId);
        Assert.Equal("P1", targetUnit.ControllerId);
        Assert.Contains(CardObjectTags.UnitCard, targetUnit.Tags);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(RideTheWindObjectId, stackItem.SourceObjectId);
        Assert.Equal(RideTheWindCardNo, stackItem.CardNo);
        Assert.Equal([RideTheWindTargetObjectId], stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        Assert.Equal(RideTheWindEffectKind, stackItem.EffectKind);
        Assert.Equal(0, stackItem.DamageAmount);
        Assert.Equal(1, stackItem.EffectRepeatCount);
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

    private static MatchState BuildRideTheWindState()
    {
        return new MatchState(
            roomId: "ride-the-wind-move-guard-test",
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
                ["P1"] = new(2, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-RIDE-THE-WIND"],
                    Base = ["P1-BASE-UNIT"],
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-UNIT",
                        "P1-FACE-DOWN-STANDBY",
                        "P1-BATTLEFIELD-EQUIPMENT",
                        "P1-BATTLEFIELD-SPELL",
                        "P1-BATTLEFIELD-RUNE"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-ENEMY-BATTLEFIELD-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-RIDE-THE-WIND"] = new(
                    "P1-SPELL-RIDE-THE-WIND",
                    cardNo: "OGN·173/298",
                    manaCost: 2,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-UNIT"] = new(
                    "P1-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    damage: 2,
                    power: 4,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BASE-UNIT"] = new(
                    "P1-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-STALE-UNIT"] = new(
                    "P1-STALE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-FACE-DOWN-STANDBY"] = new(
                    "P1-FACE-DOWN-STANDBY",
                    isFaceDown: true,
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-EQUIPMENT"] = new(
                    "P1-BATTLEFIELD-EQUIPMENT",
                    cardNo: "SFD·139/221",
                    power: 1,
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-SPELL"] = new(
                    "P1-BATTLEFIELD-SPELL",
                    cardNo: "OGN·169/298",
                    power: 1,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-RUNE"] = new(
                    "P1-BATTLEFIELD-RUNE",
                    cardNo: "RUNES·001",
                    power: 1,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-ENEMY-BATTLEFIELD-UNIT"] = new(
                    "P2-ENEMY-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
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
}
