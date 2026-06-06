using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TreasureHunterMoveTriggerTests
{
    private const string TreasureHunterTrigger = "TREASURE_HUNTER_MOVE_CREATE_GOLD";

    [Fact]
    public async Task TreasureHunterMoveCreatesDormantGoldToken()
    {
        var result = await MoveTreasureHunterAsync(BuildTreasureHunterMoveState());

        Assert.True(result.Accepted, result.ErrorMessage);
        var tokenObjectId = Assert.Single(GoldTokenIds(result.State));
        Assert.Equal([tokenObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-TREASURE-HUNTER"], result.State.PlayerZones["P1"].Battlefields);

        var tokenState = result.State.CardObjects[tokenObjectId];
        Assert.True(tokenState.IsExhausted);
        Assert.Equal("P1", tokenState.OwnerId);
        Assert.Equal("P1", tokenState.ControllerId);
        Assert.Contains(CardObjectTags.EquipmentCard, tokenState.Tags);
        Assert.Contains("金币", tokenState.Tags);
        Assert.Contains("反应", tokenState.Tags);

        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, TreasureHunterTrigger, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-TREASURE-HUNTER", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, TreasureHunterTrigger, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["tokenObjectId"] as string, tokenObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TreasureHunterMoveUnitStalePromptReplayUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildTreasureHunterMoveState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new MoveUnitCommand("P1-TREASURE-HUNTER", "BASE", "BATTLEFIELD", []);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.MoveUnit, prompt.Actions);
        Assert.Contains(prompt.Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.MoveUnit, StringComparison.Ordinal)
            && candidate.Enabled
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, command.SourceObjectId, StringComparison.Ordinal))
            && (candidate.Destinations ?? []).Any(destination => string.Equals(destination.Id, command.Destination, StringComparison.Ordinal)));
        var staleRawCommand = PromptScopedMoveUnitRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedMoveUnitRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-treasure-hunter-move-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-treasure-hunter-move-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Contains(accepted.Events, IsTreasureHunterTriggerEvent);
        Assert.Contains(accepted.Events, IsTreasureHunterGoldTokenEvent);
        var acceptedGoldTokenIds = GoldTokenIds(accepted.State);
        var acceptedGoldTokenId = Assert.Single(acceptedGoldTokenIds);
        Assert.Equal([acceptedGoldTokenId], accepted.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-TREASURE-HUNTER"], accepted.State.PlayerZones["P1"].Battlefields);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedBaseHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P1"].Base);
        var acceptedBattlefieldsHash = MatchStateHasher.HashValue(accepted.State.PlayerZones["P1"].Battlefields);
        var acceptedObjectLocationsHash = MatchStateHasher.HashValue(accepted.State.ObjectLocations);
        var acceptedGoldTokenIdsHash = MatchStateHasher.HashValue(acceptedGoldTokenIds);
        var p1PromptAfterAccepted = MatchStateHasher.HashValue(session.PromptFor("P1"));
        var p2PromptAfterAccepted = MatchStateHasher.HashValue(session.PromptFor("P2"));
        var p1SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var p2SnapshotAfterAccepted = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.MoveUnit, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(acceptedJournalEntry.RawCommand.Value));
        AssertPromptScopedMoveUnitRawCommand(acceptedJournalEntry.RawCommand.Value, command, prompt);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        AssertTreasureHunterRejectedReplayDidNotMutate(
            replay,
            acceptedStateHash,
            acceptedBaseHash,
            acceptedBattlefieldsHash,
            acceptedObjectLocationsHash,
            acceptedGoldTokenIdsHash);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(p1PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(p2PromptAfterAccepted, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(p1SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(p2SnapshotAfterAccepted, MatchStateHasher.HashValue(session.SnapshotFor("P2")));

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.MoveUnit, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(MatchStateHasher.HashValue(replay.Prompts), MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(MatchStateHasher.HashValue(replay.Snapshots), MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(rejectedJournalEntry.RawCommand.Value));
        AssertPromptScopedMoveUnitRawCommand(rejectedJournalEntry.RawCommand.Value, command, prompt);
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));
        Assert.Single(journal.Entries, entry => !entry.Accepted);
        var journalHashAfterReplay = MatchStateHasher.HashValue(journal.Entries);

        var duplicateReplay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        AssertTreasureHunterRejectedReplayDidNotMutate(
            duplicateReplay,
            acceptedStateHash,
            acceptedBaseHash,
            acceptedBattlefieldsHash,
            acceptedObjectLocationsHash,
            acceptedGoldTokenIdsHash);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateReplay.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateReplay.ErrorMessage);
        Assert.Equal(replay.State.Tick, duplicateReplay.State.Tick);
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

        AssertTreasureHunterRejectedReplayDidNotMutate(
            conflict,
            acceptedStateHash,
            acceptedBaseHash,
            acceptedBattlefieldsHash,
            acceptedObjectLocationsHash,
            acceptedGoldTokenIdsHash);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
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

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task TreasureHunterHiddenStandbyOrOpponentControlledDoesNotTrigger(
        bool faceDown,
        bool standby,
        bool opponentControlled)
    {
        var state = BuildTreasureHunterMoveState(
            faceDown: faceDown,
            standby: standby,
            opponentControlled: opponentControlled);

        var result = await MoveTreasureHunterAsync(state);

        Assert.Empty(GoldTokenIds(result.State));
        Assert.DoesNotContain(result.Events, IsTreasureHunterTriggerEvent);
        Assert.DoesNotContain(result.Events, IsTreasureHunterGoldTokenEvent);
        if (!result.Accepted)
        {
            Assert.Equal(state.PlayerZones["P1"].Base, result.State.PlayerZones["P1"].Base);
            Assert.Equal(state.PlayerZones["P1"].Battlefields, result.State.PlayerZones["P1"].Battlefields);
        }
    }

    [Fact]
    public async Task NonTreasureHunterMoveDoesNotTrigger()
    {
        var result = await MoveTreasureHunterAsync(BuildTreasureHunterMoveState(cardNo: "SFD·001/221"));

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(GoldTokenIds(result.State));
        Assert.DoesNotContain(result.Events, IsTreasureHunterTriggerEvent);
        Assert.DoesNotContain(result.Events, IsTreasureHunterGoldTokenEvent);
    }

    [Fact]
    public async Task FailedTreasureHunterMoveDoesNotCreateGold()
    {
        var state = BuildTreasureHunterMoveState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-treasure-hunter-invalid-move", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("P1-TREASURE-HUNTER", "BATTLEFIELD", "BASE", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(GoldTokenIds(result.State));
        Assert.DoesNotContain(result.Events, IsTreasureHunterTriggerEvent);
        Assert.DoesNotContain(result.Events, IsTreasureHunterGoldTokenEvent);
        Assert.Equal(state.PlayerZones["P1"].Base, result.State.PlayerZones["P1"].Base);
        Assert.Equal(state.PlayerZones["P1"].Battlefields, result.State.PlayerZones["P1"].Battlefields);
    }

    [Fact]
    public async Task TreasureHunterPreciseRoamMoveCreatesDormantGoldToken()
    {
        var result = await PreciseRoamTreasureHunterAsync(BuildTreasureHunterPreciseRoamState());

        Assert.True(result.Accepted, result.ErrorMessage);
        var tokenObjectId = Assert.Single(GoldTokenIds(result.State));
        Assert.Equal([tokenObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-TREASURE-HUNTER"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("P1-BATTLEFIELD-B", result.State.ObjectLocations["P1-TREASURE-HUNTER"].BattlefieldObjectId);

        var tokenState = result.State.CardObjects[tokenObjectId];
        Assert.True(tokenState.IsExhausted);
        Assert.Equal("P1", tokenState.OwnerId);
        Assert.Equal("P1", tokenState.ControllerId);
        Assert.Contains(CardObjectTags.EquipmentCard, tokenState.Tags);
        Assert.Contains("金币", tokenState.Tags);

        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, TreasureHunterTrigger, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["originZone"] as string, "BATTLEFIELD:P1-BATTLEFIELD-A", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BATTLEFIELD:P1-BATTLEFIELD-B", StringComparison.Ordinal));
        Assert.Contains(result.Events, IsTreasureHunterGoldTokenEvent);
    }

    [Fact]
    public async Task TreasureHunterPreciseRoamNoOpDoesNotCreateGold()
    {
        var state = BuildTreasureHunterPreciseRoamState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-treasure-hunter-roam-noop", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand(
                "P1-TREASURE-HUNTER",
                "BATTLEFIELD:P1-BATTLEFIELD-A",
                "BATTLEFIELD:P1-BATTLEFIELD-A",
                ["ROAM"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(GoldTokenIds(result.State));
        Assert.DoesNotContain(result.Events, IsTreasureHunterTriggerEvent);
        Assert.DoesNotContain(result.Events, IsTreasureHunterGoldTokenEvent);
        Assert.Equal(state.PlayerZones["P1"].Base, result.State.PlayerZones["P1"].Base);
        Assert.Equal(state.PlayerZones["P1"].Battlefields, result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("P1-BATTLEFIELD-A", result.State.ObjectLocations["P1-TREASURE-HUNTER"].BattlefieldObjectId);
    }

    private static async Task<ResolutionResult> MoveTreasureHunterAsync(MatchState state)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-treasure-hunter-move", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("P1-TREASURE-HUNTER", "BASE", "BATTLEFIELD", []),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> PreciseRoamTreasureHunterAsync(MatchState state)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-treasure-hunter-roam", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand(
                "P1-TREASURE-HUNTER",
                "BATTLEFIELD:P1-BATTLEFIELD-A",
                "BATTLEFIELD:P1-BATTLEFIELD-B",
                ["ROAM"]),
            CancellationToken.None);
    }

    private static bool IsTreasureHunterTriggerEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Payload.TryGetValue("trigger", out var trigger) ? trigger as string : null, TreasureHunterTrigger, StringComparison.Ordinal);
    }

    private static bool IsTreasureHunterGoldTokenEvent(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "EQUIPMENT_TOKEN_CREATED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload.TryGetValue("abilityId", out var abilityId) ? abilityId as string : null, TreasureHunterTrigger, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> GoldTokenIds(MatchState state)
    {
        return state.PlayerZones["P1"].Base
            .Concat(state.PlayerZones["P1"].Battlefields)
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains("金币", StringComparer.Ordinal)
                && cardObject.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertTreasureHunterRejectedReplayDidNotMutate(
        ResolutionResult result,
        string acceptedStateHash,
        string acceptedBaseHash,
        string acceptedBattlefieldsHash,
        string acceptedObjectLocationsHash,
        string acceptedGoldTokenIdsHash)
    {
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(result.State));
        Assert.Equal(acceptedBaseHash, MatchStateHasher.HashValue(result.State.PlayerZones["P1"].Base));
        Assert.Equal(acceptedBattlefieldsHash, MatchStateHasher.HashValue(result.State.PlayerZones["P1"].Battlefields));
        Assert.Equal(acceptedObjectLocationsHash, MatchStateHasher.HashValue(result.State.ObjectLocations));
        Assert.Equal(acceptedGoldTokenIdsHash, MatchStateHasher.HashValue(GoldTokenIds(result.State)));
    }

    private static JsonElement PromptScopedMoveUnitRawCommand(
        MoveUnitCommand command,
        ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.MoveUnit,
            sourceObjectId = command.SourceObjectId,
            origin = command.Origin,
            destination = command.Destination,
            optionalCosts = command.OptionalCosts ?? Array.Empty<string>(),
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
    }

    private static JsonElement PromptScopedMoveUnitRawCommandWithClientNote(
        MoveUnitCommand command,
        ActionPromptDto prompt,
        string clientNote)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.MoveUnit,
            sourceObjectId = command.SourceObjectId,
            origin = command.Origin,
            destination = command.Destination,
            optionalCosts = command.OptionalCosts ?? Array.Empty<string>(),
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote
        });
    }

    private static void AssertPromptScopedMoveUnitRawCommand(
        JsonElement rawCommand,
        MoveUnitCommand command,
        ActionPromptDto prompt)
    {
        Assert.Equal(
            ["cmdType", "sourceObjectId", "origin", "destination", "optionalCosts", "promptId", "snapshotTick"],
            rawCommand.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(CommandTypes.MoveUnit, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(command.SourceObjectId, rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal(command.Origin, rawCommand.GetProperty("origin").GetString());
        Assert.Equal(command.Destination, rawCommand.GetProperty("destination").GetString());
        Assert.Equal(
            command.OptionalCosts ?? [],
            rawCommand.GetProperty("optionalCosts").EnumerateArray().Select(value => value.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static MatchState BuildTreasureHunterMoveState(
        string cardNo = "SFD·130/221",
        bool faceDown = false,
        bool standby = false,
        bool opponentControlled = false)
    {
        var treasureHunterTags = standby
            ? new[] { CardObjectTags.UnitCard, CardObjectTags.Standby }
            : [CardObjectTags.UnitCard];
        return new MatchState(
            roomId: "treasure-hunter-move-trigger-test",
            tick: 27,
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
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-TREASURE-HUNTER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-TREASURE-HUNTER"] = new(
                    "P1-TREASURE-HUNTER",
                    isFaceDown: faceDown,
                    cardNo: cardNo,
                    power: 1,
                    tags: treasureHunterTags,
                    ownerId: "P1",
                    controllerId: opponentControlled ? "P2" : "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-TREASURE-HUNTER"] = new("P1", "BASE")
            });
    }

    private static MatchState BuildTreasureHunterPreciseRoamState()
    {
        return new MatchState(
            roomId: "treasure-hunter-roam-trigger-test",
            tick: 27,
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
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-TREASURE-HUNTER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-TREASURE-HUNTER"] = new(
                    "P1-TREASURE-HUNTER",
                    cardNo: "SFD·130/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "游走"],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-TREASURE-HUNTER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-A")
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
