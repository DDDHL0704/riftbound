using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task P4HideCardCommandPlacesStandbyCardFaceDown()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-TEEMO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OGN-TEEMO"] = new(
                    "P1-HAND-OGN-TEEMO",
                    cardNo: "OGN·121/298",
                    manaCost: 2,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            }
        };

        state = StandbyTestDriver.WithControlledBattlefield(state);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 1) } };
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-face-down", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Contains("P1-HAND-OGN-TEEMO", result.State.PlayerZones["P1"].Battlefields);
        var hiddenCard = result.State.CardObjects["P1-HAND-OGN-TEEMO"];
        Assert.True(hiddenCard.IsFaceDown);
        Assert.Equal(2, hiddenCard.Power);
        Assert.Equal(2, hiddenCard.ManaCost);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], hiddenCard.Tags);
        Assert.Empty(result.State.StackItems);

        Assert.Equal(["COST_PAID", "CARD_HIDDEN"], result.Events.Select(evt => evt.Kind));
        var hiddenEvent = Assert.Single(result.Events, evt => string.Equals(evt.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        Assert.DoesNotContain("cardNo", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain("power", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain("tags", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain("manaCost", hiddenEvent.Payload.Keys);

        StandbyTestDriver.AssertHiddenBattlefieldPrivacy(result, "P1-HAND-OGN-TEEMO");
    }

    [Fact]
    public async Task P4HideCardCommandRejectsAcceptedReplayWithoutMutation()
    {
        const string sourceObjectId = "P1-HAND-OGN-TEEMO";
        var engine = new CoreRuleEngine();
        var command = new HideCardCommand(
            sourceObjectId,
            "OGN·121/298",
            "BATTLEFIELD:STANDBY-TEST-FIELD",
            ["STANDBY_A"]);
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: "OGN·121/298",
                    manaCost: 2,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            }
        };

        state = StandbyTestDriver.WithControlledBattlefield(state);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 1) } };
        var accepted = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-before-replay", "P1", CommandTypes.HideCard),
            command,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(["COST_PAID", "CARD_HIDDEN"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), accepted.State.RunePools["P1"]);
        Assert.Empty(accepted.State.PlayerZones["P1"].Hand);
        Assert.Empty(accepted.State.PlayerZones["P1"].Base);
        Assert.Contains(sourceObjectId, accepted.State.PlayerZones["P1"].Battlefields);
        var hiddenCard = accepted.State.CardObjects[sourceObjectId];
        Assert.True(hiddenCard.IsFaceDown);
        Assert.Equal(2, hiddenCard.Power);
        Assert.Equal(2, hiddenCard.ManaCost);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], hiddenCard.Tags);
        Assert.Empty(accepted.State.StackItems);
        var hiddenEvent = Assert.Single(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        Assert.DoesNotContain("cardNo", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain("power", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain("tags", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain("manaCost", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain(CommandTypes.HideCard, accepted.Prompts["P1"].EnabledActions());
        AssertHideCardOrdinaryMainPromptQueueAudit(accepted, sourceObjectId);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await engine.ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-p4-hide-card-stale-replay", "P1", CommandTypes.HideCard),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.CardNotInHand, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(new RunePool(0, 0), replay.State.RunePools["P1"]);
        Assert.Empty(replay.State.PlayerZones["P1"].Hand);
        Assert.Empty(replay.State.PlayerZones["P1"].Base);
        Assert.Contains(sourceObjectId, replay.State.PlayerZones["P1"].Battlefields);
        var replayHiddenCard = replay.State.CardObjects[sourceObjectId];
        Assert.True(replayHiddenCard.IsFaceDown);
        Assert.Equal(2, replayHiddenCard.Power);
        Assert.Equal(2, replayHiddenCard.ManaCost);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], replayHiddenCard.Tags);
        Assert.Empty(replay.State.StackItems);
        Assert.DoesNotContain(CommandTypes.HideCard, replay.Prompts["P1"].EnabledActions());
        AssertHideCardOrdinaryMainPromptQueueAudit(replay, sourceObjectId);
    }

    [Fact]
    public async Task HideCardStalePromptReplayAfterCardMovesToBaseRejectsWithoutMutation()
    {
        const string sourceObjectId = "P1-HAND-OGN-TEEMO-STALE-PROMPT";
        var command = new HideCardCommand(
            sourceObjectId,
            "OGN·121/298",
            "BATTLEFIELD:STANDBY-TEST-FIELD",
            ["STANDBY_A"]);
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: "OGN·121/298",
                    manaCost: 2,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            }
        };
        state = StandbyTestDriver.WithControlledBattlefield(state);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 1) } };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.HideCard, prompt.EnabledActions());
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.HideCard, prompt);
        const string acceptedClientIntentId = "intent-p4-hide-card-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-p4-hide-card-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            NativeCommandWire.Bind(command, ref staleRawCommand),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["COST_PAID", "CARD_HIDDEN"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), accepted.State.RunePools["P1"]);
        Assert.Empty(accepted.State.PlayerZones["P1"].Hand);
        Assert.Empty(accepted.State.PlayerZones["P1"].Base);
        Assert.Contains(sourceObjectId, accepted.State.PlayerZones["P1"].Battlefields);
        var hiddenCard = accepted.State.CardObjects[sourceObjectId];
        Assert.True(hiddenCard.IsFaceDown);
        Assert.Equal(2, hiddenCard.Power);
        Assert.Equal(2, hiddenCard.ManaCost);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], hiddenCard.Tags);
        Assert.Empty(accepted.State.StackItems);
        var hiddenEvent = Assert.Single(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        Assert.DoesNotContain("cardNo", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain("power", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain("tags", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain("manaCost", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain(CommandTypes.HideCard, accepted.Prompts["P1"].EnabledActions());
        AssertHideCardOrdinaryMainPromptQueueAudit(accepted, sourceObjectId);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.HideCard, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.HideCard, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(acceptedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            NativeCommandWire.Bind(command, ref staleRawCommand),
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(new RunePool(0, 0), replay.State.RunePools["P1"]);
        Assert.Empty(replay.State.PlayerZones["P1"].Hand);
        Assert.Empty(replay.State.PlayerZones["P1"].Base);
        Assert.Contains(sourceObjectId, replay.State.PlayerZones["P1"].Battlefields);
        var replayHiddenCard = replay.State.CardObjects[sourceObjectId];
        Assert.True(replayHiddenCard.IsFaceDown);
        Assert.Equal(2, replayHiddenCard.Power);
        Assert.Equal(2, replayHiddenCard.ManaCost);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], replayHiddenCard.Tags);
        Assert.Empty(replay.State.StackItems);
        var replayHideCandidate = Assert.Single(
            replay.Prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.HideCard, StringComparison.Ordinal));
        Assert.False(replayHideCandidate.Enabled);
        Assert.DoesNotContain(replayHideCandidate.Sources ?? [], source => string.Equals(source.Id, sourceObjectId, StringComparison.Ordinal));
        AssertHideCardOrdinaryMainPromptQueueAudit(replay, sourceObjectId);

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.HideCard, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(rejectedJournalEntry.RawCommand.Value));
        Assert.Equal(CommandTypes.HideCard, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var rejectedEventsHash = MatchStateHasher.HashValue(replay.Events);
        var duplicateRejected = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            NativeCommandWire.Bind(command, ref staleRawCommand),
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
        Assert.Equal(new RunePool(0, 0), duplicateRejected.State.RunePools["P1"]);
        Assert.Empty(duplicateRejected.State.PlayerZones["P1"].Hand);
        Assert.Empty(duplicateRejected.State.PlayerZones["P1"].Base);
        Assert.Contains(sourceObjectId, duplicateRejected.State.PlayerZones["P1"].Battlefields);
        var duplicateRejectedHiddenCard = duplicateRejected.State.CardObjects[sourceObjectId];
        Assert.True(duplicateRejectedHiddenCard.IsFaceDown);
        Assert.Equal(2, duplicateRejectedHiddenCard.Power);
        Assert.Equal(2, duplicateRejectedHiddenCard.ManaCost);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], duplicateRejectedHiddenCard.Tags);
        Assert.Empty(duplicateRejected.State.StackItems);
        var duplicateRejectedHideCandidate = Assert.Single(
            duplicateRejected.Prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.HideCard, StringComparison.Ordinal));
        Assert.False(duplicateRejectedHideCandidate.Enabled);
        Assert.DoesNotContain(duplicateRejectedHideCandidate.Sources ?? [], source => string.Equals(source.Id, sourceObjectId, StringComparison.Ordinal));
        AssertHideCardOrdinaryMainPromptQueueAudit(duplicateRejected, sourceObjectId);
        Assert.Equal(2, journal.Entries.Count);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.HideCard,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            NativeCommandWire.Bind(command, ref changedRawCommand),
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(new RunePool(0, 0), conflict.State.RunePools["P1"]);
        Assert.Empty(conflict.State.PlayerZones["P1"].Hand);
        Assert.Empty(conflict.State.PlayerZones["P1"].Base);
        Assert.Contains(sourceObjectId, conflict.State.PlayerZones["P1"].Battlefields);
        var conflictHiddenCard = conflict.State.CardObjects[sourceObjectId];
        Assert.True(conflictHiddenCard.IsFaceDown);
        Assert.Equal(2, conflictHiddenCard.Power);
        Assert.Equal(2, conflictHiddenCard.ManaCost);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], conflictHiddenCard.Tags);
        Assert.Empty(conflict.State.StackItems);
        var conflictHideCandidate = Assert.Single(
            conflict.Prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.HideCard, StringComparison.Ordinal));
        Assert.False(conflictHideCandidate.Enabled);
        Assert.DoesNotContain(conflictHideCandidate.Sources ?? [], source => string.Equals(source.Id, sourceObjectId, StringComparison.Ordinal));
        AssertHideCardOrdinaryMainPromptQueueAudit(conflict, sourceObjectId);
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HideCardDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string sourceObjectId = "P1-HAND-OGN-TEEMO-RAW-DUPLICATE";
        var command = new HideCardCommand(
            sourceObjectId,
            "OGN·121/298",
            "BATTLEFIELD:STANDBY-TEST-FIELD",
            ["STANDBY_A"]);
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: "OGN·121/298",
                    manaCost: 2,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            }
        };
        state = StandbyTestDriver.WithControlledBattlefield(state);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 1) } };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.HideCard, prompt.EnabledActions());
        var rawCommand = PromptScopedRawCommand(CommandTypes.HideCard, prompt);
        const string clientIntentId = "intent-hide-card-raw-idempotency";

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref rawCommand),
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["COST_PAID", "CARD_HIDDEN"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), accepted.State.RunePools["P1"]);
        Assert.Empty(accepted.State.PlayerZones["P1"].Hand);
        Assert.Empty(accepted.State.PlayerZones["P1"].Base);
        Assert.Contains(sourceObjectId, accepted.State.PlayerZones["P1"].Battlefields);
        var hiddenCard = accepted.State.CardObjects[sourceObjectId];
        Assert.True(hiddenCard.IsFaceDown);
        Assert.Equal(2, hiddenCard.Power);
        Assert.Equal(2, hiddenCard.ManaCost);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], hiddenCard.Tags);
        Assert.Empty(accepted.State.StackItems);
        var hiddenEvent = Assert.Single(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        Assert.DoesNotContain("cardNo", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain("power", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain("tags", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain("manaCost", hiddenEvent.Payload.Keys);
        Assert.DoesNotContain(CommandTypes.HideCard, accepted.Prompts["P1"].EnabledActions());
        AssertHideCardOrdinaryMainPromptQueueAudit(accepted, sourceObjectId);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var currentPromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var currentSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var journalEntry = Assert.Single(journal.Entries);
        Assert.Equal(clientIntentId, journalEntry.ClientIntentId);
        Assert.Equal("P1", journalEntry.PlayerId);
        Assert.Equal(CommandTypes.HideCard, journalEntry.CommandType);
        Assert.True(journalEntry.Accepted);
        Assert.True(journalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.HideCard, journalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, journalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, journalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(journalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref rawCommand),
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Null(replay.ErrorCode);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(["COST_PAID", "CARD_HIDDEN"], replay.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), replay.State.RunePools["P1"]);
        Assert.Empty(replay.State.PlayerZones["P1"].Hand);
        Assert.Empty(replay.State.PlayerZones["P1"].Base);
        Assert.Contains(sourceObjectId, replay.State.PlayerZones["P1"].Battlefields);
        var replayHiddenCard = replay.State.CardObjects[sourceObjectId];
        Assert.True(replayHiddenCard.IsFaceDown);
        Assert.Equal(2, replayHiddenCard.Power);
        Assert.Equal(2, replayHiddenCard.ManaCost);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], replayHiddenCard.Tags);
        Assert.Empty(replay.State.StackItems);
        Assert.DoesNotContain(CommandTypes.HideCard, replay.Prompts["P1"].EnabledActions());
        AssertHideCardOrdinaryMainPromptQueueAudit(replay, sourceObjectId);
        Assert.Single(journal.Entries);

        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.HideCard,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            NativeCommandWire.Bind(command, ref changedRawCommand),
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(accepted.State.Tick, conflict.State.Tick);
        Assert.Equal(currentPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(currentSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(new RunePool(0, 0), conflict.State.RunePools["P1"]);
        Assert.Empty(conflict.State.PlayerZones["P1"].Hand);
        Assert.Empty(conflict.State.PlayerZones["P1"].Base);
        Assert.Contains(sourceObjectId, conflict.State.PlayerZones["P1"].Battlefields);
        var conflictHiddenCard = conflict.State.CardObjects[sourceObjectId];
        Assert.True(conflictHiddenCard.IsFaceDown);
        Assert.Equal(2, conflictHiddenCard.Power);
        Assert.Equal(2, conflictHiddenCard.ManaCost);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], conflictHiddenCard.Tags);
        Assert.Empty(conflict.State.StackItems);
        AssertHideCardOrdinaryMainPromptQueueAudit(conflict, sourceObjectId);
        Assert.Single(journal.Entries);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    private static void AssertHideCardOrdinaryMainPromptQueueAudit(ResolutionResult result, string sourceObjectId)
    {
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Contains(sourceObjectId, result.State.PlayerZones["P1"].Battlefields);
        var hiddenCard = result.State.CardObjects[sourceObjectId];
        Assert.True(hiddenCard.IsFaceDown);
        Assert.Equal(2, hiddenCard.Power);
        Assert.Equal(2, hiddenCard.ManaCost);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], hiddenCard.Tags);
        Assert.Equal("P1", result.State.ObjectLocations[sourceObjectId].PlayerId);
        Assert.Equal("BATTLEFIELD", result.State.ObjectLocations[sourceObjectId].Zone);
        Assert.Equal(StandbyTestDriver.BattlefieldId, result.State.ObjectLocations[sourceObjectId].BattlefieldObjectId);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal("P1", snapshot.ActivePlayerId);
            Assert.Equal("P1", Assert.IsType<string>(snapshot.Timing["turnPlayerId"]));
            Assert.Equal(MatchPhases.Main, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.NeutralOpen, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Null(snapshot.Timing["priorityPlayerId"]);
            Assert.Null(snapshot.Timing["focusPlayerId"]);
            Assert.Empty(snapshot.Stack);

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }

        StandbyTestDriver.AssertHiddenBattlefieldPrivacy(result, sourceObjectId);

        Assert.Equal("P1", result.Prompts["P1"].PlayerId);
        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(PromptTypes.MainAction, result.Prompts["P1"].View?.Type);
        Assert.Equal(result.State.Tick, result.Prompts["P1"].SnapshotTick);
        Assert.Contains(CommandTypes.EndTurn, result.Prompts["P1"].EnabledActions());
        var hideCandidate = result.Prompts["P1"].Candidates?
            .SingleOrDefault(candidate => string.Equals(candidate.Action, CommandTypes.HideCard, StringComparison.Ordinal));
        if (hideCandidate is not null)
        {
            Assert.False(hideCandidate.Enabled);
            Assert.DoesNotContain(
                hideCandidate.Sources ?? [],
                source => string.Equals(source.Id, sourceObjectId, StringComparison.Ordinal));
        }

        Assert.Equal("P2", result.Prompts["P2"].PlayerId);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.HideCard, result.Prompts["P2"].EnabledActions());
        Assert.Equal(result.State.Tick, result.Prompts["P2"].SnapshotTick);
    }

    [Theory]
    [InlineData("OGN·278/298")]
    [InlineData("OGN·278a/298")]
    public async Task P79BattlefieldBandleTreeArrangesExtraStandbyCard(string battlefieldCardNo)
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-BANDLE-STANDBY"],
                    Battlefields = ["P1-BATTLEFIELD-BANDLE-TREE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-BANDLE-STANDBY"] = new(
                    "P1-HAND-BANDLE-STANDBY",
                    cardNo: "OGN·121/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-BANDLE-TREE"] = new(
                    "P1-BATTLEFIELD-BANDLE-TREE",
                    cardNo: battlefieldCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 1) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["P1-BATTLEFIELD-BANDLE-TREE"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-BANDLE-TREE") } };
        const string secondSource = "P1-HAND-BANDLE-STANDBY-SECOND";
        state = state with
        {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 2) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
            {
                ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Append(secondSource).ToArray() }
            },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                [secondSource] = state.CardObjects["P1-HAND-BANDLE-STANDBY"] with { ObjectId = secondSource }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations)
            {
                [secondSource] = new("P1", "HAND")
            }
        };
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-bandle-tree-extra-standby", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-BANDLE-STANDBY",
                "OGN·121/298",
                "BATTLEFIELD:P1-BATTLEFIELD-BANDLE-TREE",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(new RunePool(0, 1), result.State.RunePools["P1"]);
        Assert.Equal([secondSource], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLEFIELD-BANDLE-TREE", "P1-HAND-BANDLE-STANDBY"], result.State.PlayerZones["P1"].Battlefields);
        var hiddenCard = result.State.CardObjects["P1-HAND-BANDLE-STANDBY"];
        Assert.True(hiddenCard.IsFaceDown);
        Assert.Equal("OGN·121/298", hiddenCard.CardNo);
        Assert.Contains(CardObjectTags.Standby, hiddenCard.Tags);
        // The tree grants an extra slot; placing a card does not trigger a battlefield ability (811.1.c).
        Assert.Equal(["COST_PAID", "CARD_HIDDEN"], result.Events.Select(evt => evt.Kind));
        var hiddenEvent = Assert.Single(result.Events, evt => string.Equals(evt.Kind, "CARD_HIDDEN", StringComparison.Ordinal));
        Assert.Equal("BATTLEFIELD", hiddenEvent.Payload["destinationZone"]);
        Assert.Equal("P1-BATTLEFIELD-BANDLE-TREE", hiddenEvent.Payload["battlefieldObjectId"]);
        var second = await new CoreRuleEngine().ResolveAsync(result.State,
            new PlayerIntent("bandle-second-hide", "P1", CommandTypes.HideCard),
            new HideCardCommand(secondSource, "OGN·121/298", "BATTLEFIELD:P1-BATTLEFIELD-BANDLE-TREE", ["STANDBY_A"]), default);
        Assert.True(second.Accepted, second.ErrorMessage);
        Assert.Empty(second.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, second.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, second.State.CardObjects.Values.Count(c => c.IsFaceDown));
        Assert.Equal("P1-BATTLEFIELD-BANDLE-TREE", second.State.ObjectLocations[secondSource].BattlefieldObjectId);
        Assert.Empty(second.State.StackItems);
        Assert.Equal(["COST_PAID", "CARD_HIDDEN"], second.Events.Select(e => e.Kind));
    }

    [Theory]
    [InlineData("P1", "P2")]
    [InlineData("P2", "")]
    public async Task P79BattlefieldBandleTreeRejectsExtraStandbyWithoutControlledTree(
        string battlefieldOwnerId,
        string battlefieldControllerId)
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-BANDLE-STANDBY"],
                    Battlefields = ["P1-BATTLEFIELD-BANDLE-TREE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-BANDLE-STANDBY"] = new(
                    "P1-HAND-BANDLE-STANDBY",
                    cardNo: "OGN·121/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-BANDLE-TREE"] = new(
                    "P1-BATTLEFIELD-BANDLE-TREE",
                    cardNo: "OGN·278/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: battlefieldOwnerId,
                    controllerId: battlefieldControllerId)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-bandle-tree-extra-standby-rejected", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-BANDLE-STANDBY",
                "OGN·121/298",
                "BATTLEFIELD:P1-BATTLEFIELD-BANDLE-TREE",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-BANDLE-STANDBY"], result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("P1-HAND-BANDLE-STANDBY", result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsInsufficientStandbyCost()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-TEEMO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OGN-TEEMO"] = new(
                    "P1-HAND-OGN-TEEMO",
                    cardNo: "OGN·121/298",
                    manaCost: 2,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            }
        };

        state = StandbyTestDriver.WithControlledBattlefield(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-insufficient-cost", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsInsufficientStandbyCostFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-hide-card-standby-insufficient-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsSourceOutsideHand()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BASE-OGN-TEEMO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-OGN-TEEMO"] = new(
                    "P1-BASE-OGN-TEEMO",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-source-outside-hand", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-BASE-OGN-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.CardNotInHand, result.ErrorCode);
        Assert.Equal("待命埋伏只能选择自己手牌中的牌。", result.ErrorMessage);
        Assert.DoesNotContain("HIDE_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-OGN-TEEMO"], result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects["P1-BASE-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.State.CardObjects["P1-BASE-OGN-TEEMO"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.State.CardObjects["P1-BASE-OGN-TEEMO"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsOpponentHandSource()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-OGN-TEEMO"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-HAND-OGN-TEEMO"] = new(
                    "P2-HAND-OGN-TEEMO",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    cardNo: "OGN·121/298")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-opponent-hand-source", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P2-HAND-OGN-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.CardNotInHand, result.ErrorCode);
        Assert.Equal("待命埋伏只能选择自己手牌中的牌。", result.ErrorMessage);
        Assert.DoesNotContain("HIDE_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-HAND-OGN-TEEMO"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.PlayerZones["P2"].Base);
        Assert.False(result.State.CardObjects["P2-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.State.CardObjects["P2-HAND-OGN-TEEMO"].Power);
        Assert.Equal("OGN·121/298", result.State.CardObjects["P2-HAND-OGN-TEEMO"].CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.State.CardObjects["P2-HAND-OGN-TEEMO"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsKnownNonStandbyCard()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNL-ARENA-COUNCILOR"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNL-ARENA-COUNCILOR"] = new(
                    "P1-HAND-UNL-ARENA-COUNCILOR",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "约德尔人"],
                    manaCost: 5,
                    cardNo: "UNL-001/219")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-known-non-standby", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-UNL-ARENA-COUNCILOR",
                "UNL-001/219",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("竞技场理事 没有待命关键词，不能进行待命埋伏。", result.ErrorMessage);
        Assert.DoesNotContain("HIDE_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-ARENA-COUNCILOR"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects["P1-HAND-UNL-ARENA-COUNCILOR"].IsFaceDown);
        Assert.Equal(3, result.State.CardObjects["P1-HAND-UNL-ARENA-COUNCILOR"].Power);
        Assert.Equal(5, result.State.CardObjects["P1-HAND-UNL-ARENA-COUNCILOR"].ManaCost);
        Assert.Equal("UNL-001/219", result.State.CardObjects["P1-HAND-UNL-ARENA-COUNCILOR"].CardNo);
        Assert.Equal([CardObjectTags.UnitCard, "约德尔人"], result.State.CardObjects["P1-HAND-UNL-ARENA-COUNCILOR"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandSourceOutsideHandRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-hide-card-standby-source-outside-hand-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects["P1-BASE-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.FinalState.CardObjects["P1-BASE-OGN-TEEMO"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.FinalState.CardObjects["P1-BASE-OGN-TEEMO"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandOpponentHandSourceRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-hide-card-standby-opponent-hand-source-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-HAND-OGN-TEEMO"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Base);
        Assert.False(result.FinalState.CardObjects["P2-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.FinalState.CardObjects["P2-HAND-OGN-TEEMO"].Power);
        Assert.Equal("OGN·121/298", result.FinalState.CardObjects["P2-HAND-OGN-TEEMO"].CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.FinalState.CardObjects["P2-HAND-OGN-TEEMO"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandKnownNonStandbyCardRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-hide-card-known-non-standby-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNL-ARENA-COUNCILOR"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects["P1-HAND-UNL-ARENA-COUNCILOR"].IsFaceDown);
        Assert.Equal(3, result.FinalState.CardObjects["P1-HAND-UNL-ARENA-COUNCILOR"].Power);
        Assert.Equal(5, result.FinalState.CardObjects["P1-HAND-UNL-ARENA-COUNCILOR"].ManaCost);
        Assert.Equal("UNL-001/219", result.FinalState.CardObjects["P1-HAND-UNL-ARENA-COUNCILOR"].CardNo);
        Assert.Equal([CardObjectTags.UnitCard, "约德尔人"], result.FinalState.CardObjects["P1-HAND-UNL-ARENA-COUNCILOR"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsUnsupportedDestination()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-TEEMO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OGN-TEEMO"] = new(
                    "P1-HAND-OGN-TEEMO",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-unsupported-destination", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "BASE",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal("提莫 不能以该去向进行待命埋伏。", result.ErrorMessage);
        Assert.DoesNotContain("HIDE_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.State.CardObjects["P1-HAND-OGN-TEEMO"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.State.CardObjects["P1-HAND-OGN-TEEMO"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandUnsupportedDestinationRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-hide-card-standby-unsupported-destination-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsUnsupportedOptionalCost()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-TEEMO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OGN-TEEMO"] = new(
                    "P1-HAND-OGN-TEEMO",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-unsupported-optional-cost", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["ECHO"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("请选择且仅选择一种当前可用的待命布置费用。", result.ErrorMessage);
        Assert.DoesNotContain("HIDE_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.State.CardObjects["P1-HAND-OGN-TEEMO"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.State.CardObjects["P1-HAND-OGN-TEEMO"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsMissingStandbyCost()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-TEEMO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OGN-TEEMO"] = new(
                    "P1-HAND-OGN-TEEMO",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-missing-standby-cost", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("请选择且仅选择一种当前可用的待命布置费用。", result.ErrorMessage);
        Assert.DoesNotContain("HIDE_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.State.CardObjects["P1-HAND-OGN-TEEMO"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.State.CardObjects["P1-HAND-OGN-TEEMO"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandUnsupportedOptionalCostRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-hide-card-standby-unsupported-optional-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandMissingStandbyCostRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-hide-card-standby-missing-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsOutsideActivePlayerOpenMainWindow()
    {
        var state = PunishmentState(mana: 1) with
        {
            ActivePlayerId = "P2",
            TurnPlayerId = "P2",
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-TEEMO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OGN-TEEMO"] = new(
                    "P1-HAND-OGN-TEEMO",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-window", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("待命埋伏只能在当前玩家的开放主阶段提交。", result.ErrorMessage);
        Assert.DoesNotContain("HIDE_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.State.CardObjects["P1-HAND-OGN-TEEMO"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.State.CardObjects["P1-HAND-OGN-TEEMO"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandWindowRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-hide-card-standby-window-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsPendingStackWindow()
    {
        var state = PunishmentState(mana: 1) with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            PassedPriorityPlayerIds = [],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-TEEMO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OGN-TEEMO"] = new(
                    "P1-HAND-OGN-TEEMO",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-0-P2-SPELL-PROBE",
                    "P2",
                    "P2-SPELL-PROBE",
                    "PENDING_TEST_SPELL",
                    "TEST-000",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-pending-stack", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Equal("待命埋伏只能在当前玩家的开放主阶段提交。", result.ErrorMessage);
        Assert.DoesNotContain("HIDE_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.State.CardObjects["P1-HAND-OGN-TEEMO"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.State.CardObjects["P1-HAND-OGN-TEEMO"].Tags);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("STACK-0-P2-SPELL-PROBE", stackItem.StackItemId);
    }

    [Fact]
    public async Task P4HideCardCommandPendingStackRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-hide-card-standby-pending-stack-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(2, result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.FinalState.CardObjects["P1-HAND-OGN-TEEMO"].Tags);
        var stackItem = Assert.Single(result.FinalState.StackItems);
        Assert.Equal("STACK-0-P2-SPELL-PROBE", stackItem.StackItemId);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsSourceCardNoMismatch()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-TIDE-CALLER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OGN-TIDE-CALLER"] = new(
                    "P1-HAND-OGN-TIDE-CALLER",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    cardNo: "OGN·199/298")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-cardno-mismatch", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TIDE-CALLER",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("待命埋伏的手牌信息与提交的牌不匹配。", result.ErrorMessage);
        Assert.DoesNotContain("HIDE_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TIDE-CALLER"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects["P1-HAND-OGN-TIDE-CALLER"].IsFaceDown);
        Assert.Equal(2, result.State.CardObjects["P1-HAND-OGN-TIDE-CALLER"].Power);
        Assert.Equal("OGN·199/298", result.State.CardObjects["P1-HAND-OGN-TIDE-CALLER"].CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], result.State.CardObjects["P1-HAND-OGN-TIDE-CALLER"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsSourceWithoutCardNo()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNKNOWN-STANDBY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNKNOWN-STANDBY"] = new(
                    "P1-HAND-UNKNOWN-STANDBY",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-unknown-cardno", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-UNKNOWN-STANDBY",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("待命埋伏需要服务端已确认的手牌信息。", result.ErrorMessage);
        Assert.DoesNotContain("HIDE_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNKNOWN-STANDBY"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects["P1-HAND-UNKNOWN-STANDBY"].CardNo);
        Assert.False(result.State.CardObjects["P1-HAND-UNKNOWN-STANDBY"].IsFaceDown);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], result.State.CardObjects["P1-HAND-UNKNOWN-STANDBY"].Tags);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsOpponentControlledSourceInPlayerHand()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OPPONENT-CONTROLLED-STANDBY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OPPONENT-CONTROLLED-STANDBY"] = new(
                    "P1-HAND-OPPONENT-CONTROLLED-STANDBY",
                    cardNo: "OGN·121/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-opponent-controlled-source", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OPPONENT-CONTROLLED-STANDBY",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal("待命埋伏只能选择自己控制的手牌。", result.ErrorMessage);
        Assert.DoesNotContain("HIDE_CARD", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OPPONENT-CONTROLLED-STANDBY"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.False(result.State.CardObjects["P1-HAND-OPPONENT-CONTROLLED-STANDBY"].IsFaceDown);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandSourceCardNoMismatchRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-hide-card-standby-cardno-mismatch-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(1, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TIDE-CALLER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects["P1-HAND-OGN-TIDE-CALLER"].IsFaceDown);
        Assert.Equal(2, result.FinalState.CardObjects["P1-HAND-OGN-TIDE-CALLER"].Power);
        Assert.Equal("OGN·199/298", result.FinalState.CardObjects["P1-HAND-OGN-TIDE-CALLER"].CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], result.FinalState.CardObjects["P1-HAND-OGN-TIDE-CALLER"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4HideCardCommandUsesGuerrillaWarfareFreeStandbyPermission()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-TEEMO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OGN-TEEMO"] = new(
                    "P1-HAND-OGN-TEEMO",
                    cardNo: "OGN·121/298",
                    manaCost: 2,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            },
            UntilEndOfTurnEffects = ["FREE_STANDBY_HIDE:P1"]
        };

        state = StandbyTestDriver.WithControlledBattlefield(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-free-standby", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_FREE"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Null(result.ErrorCode);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["FREE_STANDBY_HIDE:P1"], result.State.UntilEndOfTurnEffects);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Contains("P1-HAND-OGN-TEEMO", result.State.PlayerZones["P1"].Battlefields);
        Assert.True(result.State.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(["COST_PAID", "CARD_HIDDEN"], result.Events.Select(evt => evt.Kind));

        var costEvent = Assert.Single(result.Events, evt => string.Equals(evt.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(0, Assert.IsType<int>(costEvent.Payload["mana"]));
        Assert.True(Assert.IsType<bool>(costEvent.Payload["standbyHideCostWaived"]));
    }

    [Fact]
    public async Task P79EmberMonkGainsPowerWhenFriendlyStandbyCardIsHidden()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-TEEMO"],
                    Base =
                    [
                        "P1-EMBER-MONK",
                        "P1-EMBER-MONK-FACEDOWN",
                        "P1-EMBER-MONK-STANDBY"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-EMBER-MONK"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OGN-TEEMO"] = new(
                    "P1-HAND-OGN-TEEMO",
                    cardNo: "OGN·121/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-EMBER-MONK"] = new(
                    "P1-EMBER-MONK",
                    cardNo: "OGN·167/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-EMBER-MONK-FACEDOWN"] = new(
                    "P1-EMBER-MONK-FACEDOWN",
                    cardNo: "OGN·167/298",
                    isFaceDown: true,
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-EMBER-MONK-STANDBY"] = new(
                    "P1-EMBER-MONK-STANDBY",
                    isFaceDown: true,
                    cardNo: "OGN·167/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-EMBER-MONK"] = new(
                    "P2-EMBER-MONK",
                    cardNo: "OGN·167/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        // Current printed text triggers on playing from face-down, not on placing a card (811.1.c).
        state = StandbyTestDriver.WithControlledBattlefield(state);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 1) } };
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p7-9-ember-monk-standby-hidden", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["COST_PAID", "CARD_HIDDEN"], result.Events.Select(evt => evt.Kind));
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Contains("P1-HAND-OGN-TEEMO", result.State.PlayerZones["P1"].Battlefields);
        Assert.True(result.State.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);

        Assert.DoesNotContain(result.Events, e => e.Kind is "TRIGGER_RESOLVED" or "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.Empty(result.State.TriggerQueue);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(4, result.State.CardObjects["P1-EMBER-MONK"].Power);
        Assert.Equal(0, result.State.CardObjects["P1-EMBER-MONK"].UntilEndOfTurnPowerModifier);
        Assert.Equal(4, result.State.CardObjects["P1-EMBER-MONK-FACEDOWN"].Power);
        Assert.Equal(0, result.State.CardObjects["P1-EMBER-MONK-FACEDOWN"].UntilEndOfTurnPowerModifier);
        Assert.Equal(4, result.State.CardObjects["P1-EMBER-MONK-STANDBY"].Power);
        Assert.Equal(0, result.State.CardObjects["P1-EMBER-MONK-STANDBY"].UntilEndOfTurnPowerModifier);
        Assert.Equal(4, result.State.CardObjects["P2-EMBER-MONK"].Power);
        Assert.Equal(0, result.State.CardObjects["P2-EMBER-MONK"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsFreeStandbyWithoutGuerrillaWarfarePermission()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-TEEMO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-OGN-TEEMO"] = new(
                    "P1-HAND-OGN-TEEMO",
                    cardNo: "OGN·121/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-hide-card-free-standby-rejected", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "BATTLEFIELD:STANDBY-TEST-FIELD",
                ["STANDBY_FREE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task P4HideCardCommandRejectsFreeStandbyWithoutGuerrillaWarfarePermissionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-hide-card-standby-free-without-permission-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4RevealCardCommandRevealsStandbyCardInBase()
    {
        // Preserve the original card/method while following current battlefield placement and independent play ability rules.
        await StandbyTestDriver.AssertUnitRevealAtOriginalBattlefield("STANDBY_REVEAL");
    }

    [Fact]
    public async Task P4RevealCardCommandRevealsStandbyCardAtBattlefield()
    {
        // Preserve the original card/method while following current battlefield placement and independent play ability rules.
        await StandbyTestDriver.AssertUnitRevealAtOriginalBattlefield("STANDBY_REACTION");
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsAcceptedBaseReplayWithoutMutation()
    {
        // Current source is established by real placement and next-turn response; original method identity retained.
        await StandbyTestDriver.AssertDirectRevealReplayRejected("OGN·135/298");
    }

    [Fact]
    public async Task RevealCardBaseStalePromptReplayAfterCardFlipsFaceUpRejectsWithoutMutation()
    {
        // Current source is established by real placement and next-turn response; original method identity retained.
        await StandbyTestDriver.AssertSessionRevealReplay("OGN·135/298", stale: true);
    }

    [Fact]
    public async Task RevealCardBaseDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        // Current source is established by real placement and next-turn response; original method identity retained.
        await StandbyTestDriver.AssertSessionRevealReplay("OGN·135/298", stale: false);
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsSourceCardNoMismatch()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REVEAL", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        command = command with { CardNo = "OGN·135/298" };
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.InvalidTarget, "不匹配");
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsSourceWithoutCardNo()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REVEAL", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["SHADOW"] = state.CardObjects["SHADOW"] with { CardNo = null }
        }};
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.InvalidTarget, "已确认");
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsOpponentControlledSourceInPlayerBase()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REVEAL", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["SHADOW"] = state.CardObjects["SHADOW"] with { ControllerId = "P2" }
        }};
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.InvalidTarget, "自己控制");
    }

    [Fact]
    public async Task P4RevealCardCommandSourceCardNoMismatchRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-cardno-mismatch-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FACEDOWN-OGN-TIDE-CALLER"], result.FinalState.PlayerZones["P1"].Base);
        var source = result.FinalState.CardObjects["P1-FACEDOWN-OGN-TIDE-CALLER"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·199/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsSourceOutsidePlayerBase()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REVEAL", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        command = command with { SourceObjectId = "ABSENT-SOURCE" };
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.InvalidTarget, "已确认");
    }

    [Fact]
    public async Task P4RevealCardCommandSourceOutsidePlayerBaseRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-source-outside-base-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-FACEDOWN-OGN-TEEMO"], result.FinalState.PlayerZones["P2"].Base);
        var source = result.FinalState.CardObjects["P2-FACEDOWN-OGN-TEEMO"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·121/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsSourceNotFaceDown()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REVEAL", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["SHADOW"] = state.CardObjects["SHADOW"] with { IsFaceDown = false }
        }};
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.InvalidTarget, "面朝下");
    }

    [Fact]
    public async Task P4RevealCardCommandSourceNotFaceDownRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-source-not-face-down-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(["P1-REVEALED-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        var source = result.FinalState.CardObjects["P1-REVEALED-OGN-TEEMO"];
        Assert.False(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·121/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsBaseRevealTargetsWithoutSourceBattlefieldContext()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·057/298");
        var command = new RevealCardCommand("SHADOW", "OGN·057/298", ["SHADOW-OUTSIDE"],
            "STANDBY_REVEAL", ["STANDBY_REVEAL_0"], "STACK");
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.InvalidTarget, "target");
    }

    [Fact]
    public async Task P4RevealCardCommandBaseRevealTargetContextGuardFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(["P1-FACEDOWN-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.True(result.FinalState.CardObjects["P1-FACEDOWN-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsUnsupportedOptionalCost()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REVEAL", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        command = command with { OptionalCosts = ["STANDBY_REVEAL_0", "ECHO"] };
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.UnsupportedCardBehavior, "optional cost");
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsMissingRevealCost()
    {
        // Historical method identity retained: zero printed cost is authorized by real provenance, not this transport marker.
        await StandbyTestDriver.AssertRevealWithoutMarkerUsesActualPermission("STANDBY_REVEAL");
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsKnownNonStandbyCard()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REVEAL", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["SHADOW"] = state.CardObjects["SHADOW"] with { CardNo = "OGN·001/298" }
        }};
        command = command with { CardNo = "OGN·001/298" };
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.UnsupportedCardBehavior, "没有待命");
    }

    [Fact]
    public async Task P4RevealCardCommandUnsupportedOptionalCostRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-unsupported-optional-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FACEDOWN-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        var source = result.FinalState.CardObjects["P1-FACEDOWN-OGN-TEEMO"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·121/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4RevealCardCommandMissingRevealCostRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-missing-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FACEDOWN-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        var source = result.FinalState.CardObjects["P1-FACEDOWN-OGN-TEEMO"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·121/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4RevealCardCommandKnownNonStandbyCardRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-known-non-standby-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FACEDOWN-UNL-ARENA-COUNCILOR"], result.FinalState.PlayerZones["P1"].Base);
        var source = result.FinalState.CardObjects["P1-FACEDOWN-UNL-ARENA-COUNCILOR"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(3, source.Power);
        Assert.Equal(5, source.ManaCost);
        Assert.Equal("UNL-001/219", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, "约德尔人"], source.Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsReactionKnownNonStandbyCard()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["SHADOW"] = state.CardObjects["SHADOW"] with { CardNo = "OGN·001/298" }
        }};
        command = command with { CardNo = "OGN·001/298" };
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.UnsupportedCardBehavior, "没有待命");
    }

    [Fact]
    public async Task P4RevealCardCommandReactionKnownNonStandbyCardRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-reaction-known-non-standby-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FACEDOWN-UNL-ARENA-COUNCILOR"], result.FinalState.PlayerZones["P1"].Base);
        var source = result.FinalState.CardObjects["P1-FACEDOWN-UNL-ARENA-COUNCILOR"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(3, source.Power);
        Assert.Equal(5, source.ManaCost);
        Assert.Equal("UNL-001/219", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, "约德尔人"], source.Tags);
        Assert.Equal(["STACK-0-P2-SPELL-PROBE"], result.FinalState.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsReactionMissingRevealCost()
    {
        // Historical method identity retained: zero printed cost is authorized by real provenance, not this transport marker.
        await StandbyTestDriver.AssertRevealWithoutMarkerUsesActualPermission("STANDBY_REACTION");
    }

    [Fact]
    public async Task P4RevealCardCommandReactionMissingRevealCostRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-reaction-missing-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FACEDOWN-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        var source = result.FinalState.CardObjects["P1-FACEDOWN-OGN-TEEMO"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·121/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Equal(["STACK-0-P2-SPELL-PROBE"], result.FinalState.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsReactionUnsupportedOptionalCost()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        command = command with { OptionalCosts = ["STANDBY_REVEAL_0", "ECHO"] };
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.UnsupportedCardBehavior, "optional cost");
    }

    [Fact]
    public async Task P4RevealCardCommandReactionUnsupportedOptionalCostRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-reaction-unsupported-optional-cost-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FACEDOWN-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        var source = result.FinalState.CardObjects["P1-FACEDOWN-OGN-TEEMO"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·121/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Equal(["STACK-0-P2-SPELL-PROBE"], result.FinalState.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsReactionSourceCardNoMismatch()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        command = command with { CardNo = "OGN·135/298" };
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.InvalidTarget, "不匹配");
    }

    [Fact]
    public async Task P4RevealCardCommandReactionSourceCardNoMismatchRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-reaction-cardno-mismatch-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FACEDOWN-OGN-TIDE-CALLER"], result.FinalState.PlayerZones["P1"].Base);
        var source = result.FinalState.CardObjects["P1-FACEDOWN-OGN-TIDE-CALLER"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·199/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Equal(["STACK-0-P2-SPELL-PROBE"], result.FinalState.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsReactionUnsupportedDestination()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        command = command with { Destination = "HAND" };
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.InvalidTarget, "原战场");
    }

    [Fact]
    public async Task P4RevealCardCommandReactionUnsupportedDestinationRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-reaction-unsupported-destination-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FACEDOWN-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        var source = result.FinalState.CardObjects["P1-FACEDOWN-OGN-TEEMO"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·121/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Equal(["STACK-0-P2-SPELL-PROBE"], result.FinalState.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsReactionSourceOutsidePlayerBase()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        command = command with { SourceObjectId = "ABSENT-SOURCE" };
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.InvalidTarget, "已确认");
    }

    [Fact]
    public async Task P4RevealCardCommandReactionSourceOutsidePlayerBaseRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-reaction-source-outside-base-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-FACEDOWN-OGN-TEEMO"], result.FinalState.PlayerZones["P2"].Base);
        var source = result.FinalState.CardObjects["P2-FACEDOWN-OGN-TEEMO"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·121/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Equal(["STACK-0-P2-SPELL-PROBE"], result.FinalState.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsReactionSourceNotFaceDown()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
        {
            ["SHADOW"] = state.CardObjects["SHADOW"] with { IsFaceDown = false }
        }};
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.InvalidTarget, "面朝下");
    }

    [Fact]
    public async Task P4RevealCardCommandReactionSourceNotFaceDownRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-reaction-source-not-face-down-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FACEUP-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        var source = result.FinalState.CardObjects["P1-FACEUP-OGN-TEEMO"];
        Assert.False(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·121/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Equal(["STACK-0-P2-SPELL-PROBE"], result.FinalState.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsUnsupportedDestination()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [],
            "STANDBY_REVEAL", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F");
        command = command with { Destination = "HAND" };
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.InvalidTarget, "原战场");
    }

    [Fact]
    public async Task P4RevealCardCommandUnsupportedDestinationRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-unsupported-destination-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FACEDOWN-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        var source = result.FinalState.CardObjects["P1-FACEDOWN-OGN-TEEMO"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·121/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsBaseReactionTargetWithoutSourceBattlefieldContext()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·057/298");
        var command = new RevealCardCommand("SHADOW", "OGN·057/298", ["SHADOW-OUTSIDE"],
            "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK");
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.InvalidTarget, "target");
    }

    [Fact]
    public async Task P4RevealCardCommandBaseReactionTargetContextGuardFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-reaction-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(["P1-FACEDOWN-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.True(result.FinalState.CardObjects["P1-FACEDOWN-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Equal(["STACK-0-P2-SPELL-PROBE"], result.FinalState.StackItems.Select(item => item.StackItemId));
    }

    [Theory]
    [InlineData(
        "p4-reveal-card-standby-target-rejected.fixture.json",
        "p4-reveal-card-standby-target-rejected",
        "P4.321",
        "")]
    [InlineData(
        "p4-reveal-card-standby-reaction-target-rejected.fixture.json",
        "p4-reveal-card-standby-reaction-target-rejected",
        "P4.324",
        "STACK-0-P2-SPELL-PROBE")]
    public async Task P4StandbyTargetContextGuardsCoverBaseRevealAndBaseReactionBranches(
        string fixtureFileName,
        string expectedFixtureId,
        string expectedAuditMarker,
        string expectedStackItemId)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        Assert.Equal(expectedFixtureId, fixture.FixtureId);
        Assert.Equal("RULE_AUDITED", fixture.AuditStatus);
        Assert.False(fixture.RequiresRuleAudit);
        Assert.Contains(fixture.RulesEvidence ?? [], evidence =>
            string.Equals(evidence.Source, "data/official/card-catalog.zh-CN.json", StringComparison.Ordinal)
            && string.Equals(evidence.Locator, "OGN·121/298 提莫", StringComparison.Ordinal));
        Assert.Contains(fixture.RulesEvidence ?? [], evidence =>
            evidence.Note.Contains(expectedAuditMarker, StringComparison.Ordinal)
            && evidence.Note.Contains("不伤害目标", StringComparison.Ordinal));

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Empty(result.EventKinds);
        Assert.Equal(["P1-FACEDOWN-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        var source = result.FinalState.CardObjects["P1-FACEDOWN-OGN-TEEMO"];
        Assert.True(source.IsFaceDown);
        Assert.Equal(2, source.Power);
        Assert.Equal(2, source.ManaCost);
        Assert.Equal("OGN·121/298", source.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby], source.Tags);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        IReadOnlyList<string> expectedStackItemIds = string.IsNullOrWhiteSpace(expectedStackItemId)
            ? Array.Empty<string>()
            : [expectedStackItemId];
        Assert.Equal(expectedStackItemIds, result.FinalState.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task P4RevealCardCommandPlaysStandbyReactionToStack()
    {
        // Preserve the original card/method while following current battlefield placement and independent play ability rules.
        await StandbyTestDriver.AssertCountedStandbyResponse("STANDBY_REACTION", postEntrySelection: true, rejectOtherField: false);
    }

    [Fact]
    public async Task P4RevealCardCommandPlaysBattlefieldStandbyReactionToStackAndReturnsToBattlefield()
    {
        // Preserve the original card/method while following current battlefield placement and independent play ability rules.
        await StandbyTestDriver.AssertCountedStandbyResponse("STANDBY_REACTION", postEntrySelection: false, rejectOtherField: false);
    }

    [Fact]
    public async Task P4RevealCardCommandBattlefieldStandbyReactionTargetDamageCountsTopFiveStandbyCards()
    {
        // Preserve the original card/method while following current battlefield placement and independent play ability rules.
        await StandbyTestDriver.AssertCountedStandbyResponse("STANDBY_REVEAL", postEntrySelection: true, rejectOtherField: true);
    }

    [Fact]
    public async Task P4RevealCardCommandBaseStandbyReactionTargetDamageUsesSpellDuelBattlefieldContext()
    {
        // Preserve the original card/method while following current battlefield placement and independent play ability rules.
        await StandbyTestDriver.AssertCountedStandbyResponse("STANDBY_REACTION", postEntrySelection: false, rejectOtherField: true);
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsAcceptedReactionReplayWithoutMutation()
    {
        // Current source is established by real placement and next-turn response; original method identity retained.
        await StandbyTestDriver.AssertDirectRevealReplayRejected("OGN·083/298");
    }

    [Fact]
    public async Task RevealCardReactionDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        // Current source is established by real placement and next-turn response; original method identity retained.
        await StandbyTestDriver.AssertSessionRevealReplay("OGN·083/298", stale: false);
    }

    [Fact]
    public async Task RevealCardReactionStalePromptReplayAfterCardMovesToStackRejectsWithoutMutation()
    {
        // Current source is established by real placement and next-turn response; original method identity retained.
        await StandbyTestDriver.AssertSessionRevealReplay("OGN·083/298", stale: true);
    }

    [Fact]
    public async Task P4RevealCardCommandResolvesStandbyReactionTeemoSelfPowerPlusThree()
    {
        // Preserve the original card/method while following current battlefield placement and independent play ability rules.
        await StandbyTestDriver.AssertRevealWithoutMarkerUsesActualPermission("STANDBY_REACTION", marker: true);
    }

    [Fact]
    public async Task P4RevealCardCommandStandbyReactionTeemoSelfPowerFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-reaction-teemo-self-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalTick - result.InitialTick);
        Assert.Equal(TimingStates.NeutralClosed, result.FinalState.TimingState);
        Assert.Equal("P2", result.FinalState.PriorityPlayerId);
        Assert.Equal("REPLY", Assert.Single(result.FinalState.StackItems).SourceObjectId);
        Assert.Contains("SHADOW", result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("SHADOW", result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal("F", result.FinalState.ObjectLocations["SHADOW"].BattlefieldObjectId);
        var teemo = result.FinalState.CardObjects["SHADOW"];
        Assert.False(teemo.IsFaceDown);
        Assert.Equal(4, teemo.Power);
        Assert.Equal(3, teemo.UntilEndOfTurnPowerModifier);
        Assert.Equal("OGN·197/298", teemo.CardNo);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], teemo.Tags);
        OfficialGraveyardRecastTests.Restore(result.FinalState);
        ArmamentMightTests.PublicRecovery(result.FinalState);
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsReactionPlayWithoutPriorityWindow()
    {
        var state = await StandbySourceRulesTests.ReadyShadow("OGN·197/298");
        state = (await OfficialInsightAndSpellLockTests.Act(state, "P1", new PassPriorityCommand())).State;
        Assert.Empty(state.StackItems);
        Assert.Equal("P2", state.TurnPlayerId);
        var command = new RevealCardCommand("SHADOW", "OGN·197/298", [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "STACK");
        await StandbyTestDriver.AssertRevealRejected(state, command, ErrorCodes.PhaseNotAllowed);
    }

    [Fact]
    public async Task P4RevealCardCommandRejectsReactionPlayWithoutPriorityWindowFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-reveal-card-standby-reaction-without-priority-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(["P1-FACEDOWN-OGN-TEEMO"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.True(result.FinalState.CardObjects["P1-FACEDOWN-OGN-TEEMO"].IsFaceDown);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Empty(result.FinalState.StackItems);
    }
}
