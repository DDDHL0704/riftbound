using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EdgeOfNightAssembleGuardTests
{
    [Fact]
    public async Task EdgeOfNightAssemblePurpleAttachesToFriendlyPublicUnit()
    {
        var state = BuildEdgeOfNightState();

        var result = await AssembleEdgeOfNightAsync(
            new CoreRuleEngine(),
            state,
            "P1-EQUIPMENT-EDGE-OF-NIGHT",
            "P1-UNIT-ASSEMBLE-TARGET",
            ["ASSEMBLE_PURPLE"]);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId);
        Assert.Contains("P1-EQUIPMENT-EDGE-OF-NIGHT", result.State.PlayerZones["P1"].Base);
        Assert.Contains("P1-UNIT-ASSEMBLE-TARGET", result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(["COST_PAID", "EQUIPMENT_ATTACHED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());

        var costPaidEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("P1-EQUIPMENT-EDGE-OF-NIGHT", costPaidEvent.Payload["sourceObjectId"]);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", costPaidEvent.Payload["targetObjectId"]);
        Assert.Equal(0, costPaidEvent.Payload["mana"]);
        Assert.Equal(1, costPaidEvent.Payload["power"]);
        Assert.Equal(["ASSEMBLE_PURPLE"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));

        var attachedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal("P1-EQUIPMENT-EDGE-OF-NIGHT", attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", attachedEvent.Payload["unitObjectId"]);
        Assert.Equal("P1-UNIT-ASSEMBLE-TARGET", attachedEvent.Payload["attachedToObjectId"]);
        Assert.Equal("SFD·139/221", attachedEvent.Payload["equipmentCardNo"]);
        Assert.Equal(["ASSEMBLE_PURPLE"], Assert.IsType<string[]>(attachedEvent.Payload["optionalCosts"]));
    }

    [Fact]
    public async Task EdgeOfNightAssembleRejectsAcceptedCommandReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var state = BuildEdgeOfNightState();
        var command = new AssembleEquipmentCommand(
            "P1-EQUIPMENT-EDGE-OF-NIGHT",
            "P1-UNIT-ASSEMBLE-TARGET",
            ["ASSEMBLE_PURPLE"]);

        var assembled = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-edge-of-night-assemble-first", "P1", CommandTypes.AssembleEquipment),
            command,
            CancellationToken.None);

        Assert.True(assembled.Accepted, assembled.ErrorMessage);
        Assert.Equal(["COST_PAID", "EQUIPMENT_ATTACHED"], assembled.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(1, CountEvents(assembled.Events, "COST_PAID"));
        Assert.Equal(1, CountEvents(assembled.Events, "EQUIPMENT_ATTACHED"));
        Assert.Equal(new RunePool(0, 0), assembled.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            assembled.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId);
        Assert.Null(assembled.State.PendingPayment);
        Assert.Empty(assembled.State.StackItems);
        Assert.Equal("IDLE", assembled.State.PendingTaskQueue.Phase);
        Assert.Empty(assembled.State.PendingTaskQueue.Tasks);
        Assert.DoesNotContain(
            ResolutionResult.BuildPrompts(assembled.State)["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.AssembleEquipment, StringComparison.Ordinal)
                && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, "P1-EQUIPMENT-EDGE-OF-NIGHT", StringComparison.Ordinal)));
        var postAssembleHash = MatchStateHasher.Hash(assembled.State);

        var replay = await engine.ResolveAsync(
            assembled.State,
            new PlayerIntent("intent-edge-of-night-assemble-replay", "P1", CommandTypes.AssembleEquipment),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Empty(replay.Events);
        Assert.Equal(postAssembleHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(assembled.State.RunePools["P1"], replay.State.RunePools["P1"]);
        Assert.Equal(assembled.State.PlayerZones["P1"].Base, replay.State.PlayerZones["P1"].Base);
        Assert.Equal(assembled.State.PlayerZones["P1"].Hand, replay.State.PlayerZones["P1"].Hand);
        Assert.Equal(assembled.State.PlayerZones["P2"].Base, replay.State.PlayerZones["P2"].Base);
        Assert.Equal(
            assembled.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId,
            replay.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId);
        Assert.True(replay.State.CardObjects["P1-FACE-DOWN-EDGE-OF-NIGHT"].IsFaceDown);
        Assert.Null(replay.State.CardObjects["P1-FACE-DOWN-EDGE-OF-NIGHT"].CardNo);
        Assert.True(replay.State.CardObjects["P1-FACE-DOWN-STANDBY-UNIT"].IsFaceDown);
        Assert.Null(replay.State.CardObjects["P1-FACE-DOWN-STANDBY-UNIT"].CardNo);
        Assert.Null(replay.State.PendingPayment);
        Assert.Empty(replay.State.StackItems);
        Assert.Equal(assembled.State.PendingTaskQueue.Phase, replay.State.PendingTaskQueue.Phase);
        Assert.Equal(assembled.State.PendingTaskQueue.ActiveTaskId, replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(assembled.State.PendingTaskQueue.Tasks, replay.State.PendingTaskQueue.Tasks);
        Assert.DoesNotContain(
            ResolutionResult.BuildPrompts(replay.State)["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.AssembleEquipment, StringComparison.Ordinal)
                && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, "P1-EQUIPMENT-EDGE-OF-NIGHT", StringComparison.Ordinal)));
        Assert.DoesNotContain(replay.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EdgeOfNightAssembleStalePromptReplayAfterEquipmentAttachesRejectsWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildEdgeOfNightState();
        var command = new AssembleEquipmentCommand(
            "P1-EQUIPMENT-EDGE-OF-NIGHT",
            "P1-UNIT-ASSEMBLE-TARGET",
            ["ASSEMBLE_PURPLE"]);
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.AssembleEquipment, prompt.Actions);
        var assembleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.AssembleEquipment, StringComparison.Ordinal));
        Assert.True(assembleCandidate.Enabled);
        Assert.Contains(
            assembleCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-EQUIPMENT-EDGE-OF-NIGHT", StringComparison.Ordinal));
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.AssembleEquipment, prompt);
        var changedStaleRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.AssembleEquipment,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        const string firstClientIntentId = "intent-edge-of-night-assemble-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-edge-of-night-assemble-stale-prompt-replay";

        var assembled = await session.SubmitAsync(
            "P1",
            firstClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(assembled.Accepted, assembled.ErrorMessage);
        Assert.Equal(["COST_PAID", "EQUIPMENT_ATTACHED"], assembled.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), assembled.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            assembled.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId);
        Assert.Null(assembled.State.PendingPayment);
        Assert.Empty(assembled.State.StackItems);
        Assert.Equal("IDLE", assembled.State.PendingTaskQueue.Phase);
        Assert.Empty(assembled.State.PendingTaskQueue.Tasks);
        var postAssembleHash = MatchStateHasher.Hash(assembled.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(assembled.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(assembled.Snapshots);
        var postAssembleAuthoritativePromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(assembled.State));
        var postAssembleAuthoritativeSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(assembled.State));
        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(firstClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.AssembleEquipment, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.Equal(["COST_PAID", "EQUIPMENT_ATTACHED"], acceptedJournalEntry.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(postAssembleHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.AssembleEquipment, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(postAssembleHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(postAssembleAuthoritativePromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(postAssembleAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(assembled.State.RunePools["P1"], replay.State.RunePools["P1"]);
        Assert.Equal(assembled.State.PlayerZones["P1"].Base, replay.State.PlayerZones["P1"].Base);
        Assert.Equal(assembled.State.PlayerZones["P1"].Hand, replay.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            assembled.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId,
            replay.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId);
        Assert.True(replay.State.CardObjects["P1-FACE-DOWN-EDGE-OF-NIGHT"].IsFaceDown);
        Assert.Null(replay.State.CardObjects["P1-FACE-DOWN-EDGE-OF-NIGHT"].CardNo);
        Assert.True(replay.State.CardObjects["P1-FACE-DOWN-STANDBY-UNIT"].IsFaceDown);
        Assert.Null(replay.State.CardObjects["P1-FACE-DOWN-STANDBY-UNIT"].CardNo);
        Assert.Null(replay.State.PendingPayment);
        Assert.Empty(replay.State.StackItems);
        Assert.Equal(assembled.State.PendingTaskQueue.Phase, replay.State.PendingTaskQueue.Phase);
        Assert.Equal(assembled.State.PendingTaskQueue.Tasks, replay.State.PendingTaskQueue.Tasks);
        var replayAssembleCandidate = replay.Prompts["P1"].Candidates?
            .SingleOrDefault(candidate => string.Equals(candidate.Action, CommandTypes.AssembleEquipment, StringComparison.Ordinal));
        if (replayAssembleCandidate is not null)
        {
            Assert.False(replayAssembleCandidate.Enabled);
            Assert.DoesNotContain(
                replayAssembleCandidate.Sources ?? [],
                source => string.Equals(source.Id, "P1-EQUIPMENT-EDGE-OF-NIGHT", StringComparison.Ordinal));
        }

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.AssembleEquipment, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(postAssembleHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(postAssembleAuthoritativePromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(postAssembleAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.AssembleEquipment, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
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
        Assert.Equal(postAssembleHash, MatchStateHasher.Hash(duplicateReplay.State));
        Assert.Equal(postAssembleAuthoritativePromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(postAssembleAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        Assert.Equal(assembled.State.RunePools["P1"], duplicateReplay.State.RunePools["P1"]);
        Assert.Equal(assembled.State.PlayerZones["P1"].Base, duplicateReplay.State.PlayerZones["P1"].Base);
        Assert.Equal(assembled.State.PlayerZones["P1"].Hand, duplicateReplay.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            assembled.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId,
            duplicateReplay.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId);
        Assert.True(duplicateReplay.State.CardObjects["P1-FACE-DOWN-EDGE-OF-NIGHT"].IsFaceDown);
        Assert.Null(duplicateReplay.State.CardObjects["P1-FACE-DOWN-EDGE-OF-NIGHT"].CardNo);
        Assert.True(duplicateReplay.State.CardObjects["P1-FACE-DOWN-STANDBY-UNIT"].IsFaceDown);
        Assert.Null(duplicateReplay.State.CardObjects["P1-FACE-DOWN-STANDBY-UNIT"].CardNo);
        Assert.Null(duplicateReplay.State.PendingPayment);
        Assert.Empty(duplicateReplay.State.StackItems);
        Assert.Equal(assembled.State.PendingTaskQueue.Phase, duplicateReplay.State.PendingTaskQueue.Phase);
        Assert.Equal(assembled.State.PendingTaskQueue.Tasks, duplicateReplay.State.PendingTaskQueue.Tasks);
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
        Assert.Equal(postAssembleHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(postAssembleAuthoritativePromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(postAssembleAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(assembled.State.RunePools["P1"], conflict.State.RunePools["P1"]);
        Assert.Equal(assembled.State.PlayerZones["P1"].Base, conflict.State.PlayerZones["P1"].Base);
        Assert.Equal(assembled.State.PlayerZones["P1"].Hand, conflict.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            assembled.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId,
            conflict.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId);
        Assert.True(conflict.State.CardObjects["P1-FACE-DOWN-EDGE-OF-NIGHT"].IsFaceDown);
        Assert.Null(conflict.State.CardObjects["P1-FACE-DOWN-EDGE-OF-NIGHT"].CardNo);
        Assert.True(conflict.State.CardObjects["P1-FACE-DOWN-STANDBY-UNIT"].IsFaceDown);
        Assert.Null(conflict.State.CardObjects["P1-FACE-DOWN-STANDBY-UNIT"].CardNo);
        Assert.Null(conflict.State.PendingPayment);
        Assert.Empty(conflict.State.StackItems);
        Assert.Equal(assembled.State.PendingTaskQueue.Phase, conflict.State.PendingTaskQueue.Phase);
        Assert.Equal(assembled.State.PendingTaskQueue.Tasks, conflict.State.PendingTaskQueue.Tasks);
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-FACE-DOWN-EDGE-OF-NIGHT", "P1-UNIT-ASSEMBLE-TARGET", "ASSEMBLE_PURPLE", 1)]
    [InlineData("P1-HAND-EDGE-OF-NIGHT", "P1-UNIT-ASSEMBLE-TARGET", "ASSEMBLE_PURPLE", 1)]
    [InlineData("P2-EQUIPMENT-EDGE-OF-NIGHT", "P1-UNIT-ASSEMBLE-TARGET", "ASSEMBLE_PURPLE", 1)]
    [InlineData("P1-ATTACHED-EDGE-OF-NIGHT", "P1-UNIT-ASSEMBLE-TARGET", "ASSEMBLE_PURPLE", 1)]
    [InlineData("P1-UNKNOWN-EDGE-OF-NIGHT", "P1-UNIT-ASSEMBLE-TARGET", "ASSEMBLE_PURPLE", 1)]
    [InlineData("P1-EQUIPMENT-EDGE-OF-NIGHT", "P1-UNKNOWN-TARGET", "ASSEMBLE_PURPLE", 1)]
    [InlineData("P1-EQUIPMENT-EDGE-OF-NIGHT", "P2-UNIT-ASSEMBLE-TARGET", "ASSEMBLE_PURPLE", 1)]
    [InlineData("P1-EQUIPMENT-EDGE-OF-NIGHT", "P1-FACE-DOWN-STANDBY-UNIT", "ASSEMBLE_PURPLE", 1)]
    [InlineData("P1-EQUIPMENT-EDGE-OF-NIGHT", "P1-BATTLEFIELD-EQUIPMENT", "ASSEMBLE_PURPLE", 1)]
    [InlineData("P1-EQUIPMENT-EDGE-OF-NIGHT", "P1-BATTLEFIELD-SPELL", "ASSEMBLE_PURPLE", 1)]
    [InlineData("P1-EQUIPMENT-EDGE-OF-NIGHT", "P1-BATTLEFIELD-RUNE", "ASSEMBLE_PURPLE", 1)]
    [InlineData("P1-EQUIPMENT-EDGE-OF-NIGHT", "P1-UNIT-ASSEMBLE-TARGET", "", 1)]
    [InlineData("P1-EQUIPMENT-EDGE-OF-NIGHT", "P1-UNIT-ASSEMBLE-TARGET", "ASSEMBLE_RED", 1)]
    [InlineData("P1-EQUIPMENT-EDGE-OF-NIGHT", "P1-UNIT-ASSEMBLE-TARGET", "ASSEMBLE_PURPLE", 0)]
    public async Task EdgeOfNightAssembleRejectsInvalidInputsWithoutMutation(
        string sourceObjectId,
        string targetObjectId,
        string optionalCost,
        int purplePower)
    {
        var state = BuildEdgeOfNightState(purplePower);
        var optionalCosts = string.IsNullOrWhiteSpace(optionalCost) ? Array.Empty<string>() : [optionalCost];

        var result = await AssembleEdgeOfNightAsync(
            new CoreRuleEngine(),
            state,
            sourceObjectId,
            targetObjectId,
            optionalCosts);

        Assert.False(result.Accepted);
        Assert.True(result.ErrorCode is ErrorCodes.UnsupportedCommand or ErrorCodes.InsufficientCost);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Null(result.State.PendingPayment);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(state.RunePools["P1"], result.State.RunePools["P1"]);
        Assert.Equal(state.PlayerZones["P1"].Hand, result.State.PlayerZones["P1"].Hand);
        Assert.Equal(state.PlayerZones["P1"].Base, result.State.PlayerZones["P1"].Base);
        Assert.Equal(state.PlayerZones["P1"].Battlefields, result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(state.PlayerZones["P2"].Base, result.State.PlayerZones["P2"].Base);
        Assert.Equal(state.PlayerZones["P2"].Battlefields, result.State.PlayerZones["P2"].Battlefields);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId);
        Assert.Equal(
            "P1-OLD-HOST",
            result.State.CardObjects["P1-ATTACHED-EDGE-OF-NIGHT"].AttachedToObjectId);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY-UNIT"].IsFaceDown);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EdgeOfNightPlayCardWithNoTargetsUsesStackAndResolvesToBase()
    {
        var engine = new CoreRuleEngine();
        var state = BuildEdgeOfNightPlayState();

        var played = await PlayEdgeOfNightAsync(engine, state, []);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "EDGE_OF_NIGHT_PLAY_EQUIPMENT", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-edge-of-night-play-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-edge-of-night-play-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(["P1-UNIT-ASSEMBLE-TARGET", "P1-EQUIPMENT-EDGE-OF-NIGHT"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal([CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], p2Pass.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].Tags);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentObjectId"] as string, "P1-EQUIPMENT-EDGE-OF-NIGHT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["equipmentName"] as string, "夜之锋刃", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal));
    }

    [Fact]
    public void EdgeOfNightDirectPlayMainActionPlayCardPromptExposesNoTargetChoices()
    {
        var state = BuildEdgeOfNightPlayState();
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
                && candidate.Enabled
                && (candidate.Sources ?? []).Any(source =>
                    string.Equals(source.Id, "P1-EQUIPMENT-EDGE-OF-NIGHT", StringComparison.Ordinal)));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-EQUIPMENT-EDGE-OF-NIGHT", StringComparison.Ordinal));

        var invalidTargetObjectIds = new[]
        {
            "P1-UNIT-ASSEMBLE-TARGET",
            "P2-UNIT-ASSEMBLE-TARGET",
            "P1-FACE-DOWN-STANDBY-UNIT",
            "P1-BATTLEFIELD-EQUIPMENT",
            "P1-BATTLEFIELD-SPELL",
            "P1-BATTLEFIELD-RUNE"
        };
        var targetIds = (playCandidate.Targets ?? []).Select(target => target.Id).ToArray();
        Assert.Empty(targetIds);
        foreach (var invalidTargetObjectId in invalidTargetObjectIds)
        {
            Assert.DoesNotContain(invalidTargetObjectId, targetIds);
        }

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        Assert.True(metadata.TryGetValue("sourceRequirements", out var rawSourceRequirements));
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                rawSourceRequirements)
            .ToArray();
        var edgeOfNightRequirement = Assert.Single(
            sourceRequirements,
            sourceRequirement => sourceRequirement.TryGetValue("sourceObjectId", out var sourceObjectId)
                && string.Equals(sourceObjectId as string, "P1-EQUIPMENT-EDGE-OF-NIGHT", StringComparison.Ordinal));
        Assert.True(edgeOfNightRequirement.TryGetValue("targetChoicesByIndex", out var rawTargetChoicesByIndex));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            rawTargetChoicesByIndex);
        Assert.Empty(targetChoicesByIndex);

        if (edgeOfNightRequirement.TryGetValue("minTargetCount", out var rawMinTargetCount))
        {
            Assert.Equal(0, Assert.IsType<int>(rawMinTargetCount));
        }

        if (edgeOfNightRequirement.TryGetValue("maxTargetCount", out var rawMaxTargetCount))
        {
            Assert.Equal(0, Assert.IsType<int>(rawMaxTargetCount));
        }

        var metadataTargetIds = targetChoicesByIndex.Values
            .Where(rawTargetChoices => rawTargetChoices is not null)
            .SelectMany(rawTargetChoices => Assert
                .IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(rawTargetChoices)
                .Select(choice => choice.Id))
            .ToArray();

        Assert.Empty(metadataTargetIds);
        foreach (var invalidTargetObjectId in invalidTargetObjectIds)
        {
            Assert.DoesNotContain(invalidTargetObjectId, metadataTargetIds);
        }
    }

    [Fact]
    public async Task EdgeOfNightPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildEdgeOfNightPlayState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            "P1-EQUIPMENT-EDGE-OF-NIGHT",
            "SFD·139/221",
            []);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, "P1-EQUIPMENT-EDGE-OF-NIGHT", StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var reorderedStaleRawCommand = ReorderedPromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-edge-of-night-play-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-edge-of-night-play-stale-prompt-replay";

        Assert.NotEqual(staleRawCommand.GetRawText(), reorderedStaleRawCommand.GetRawText());
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(reorderedStaleRawCommand));
        AssertPromptScopedPlayCardRawCommand(reorderedStaleRawCommand, command, prompt, assertPropertyOrder: false);
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
        Assert.DoesNotContain(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "EQUIPMENT_PLAYED_TO_BASE", StringComparison.Ordinal));
        var acceptedStackItem = AssertEdgeOfNightPlayStackPriorityState(accepted);
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
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], acceptedJournalEntry.Events.Select(gameEvent => gameEvent.Kind).ToArray());
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
        AssertEdgeOfNightPlayStackPriorityState(replay, acceptedStackItem);
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
        AssertEdgeOfNightPlayStackPriorityState(reorderedReplay, acceptedStackItem);
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
        AssertEdgeOfNightPlayStackPriorityState(duplicateReplay, acceptedStackItem);
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
        AssertEdgeOfNightPlayStackPriorityState(conflict, acceptedStackItem);
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
    public async Task EdgeOfNightPlayCardRejectsExplicitTargetsWithoutMutation()
    {
        var state = BuildEdgeOfNightPlayState();

        var result = await PlayEdgeOfNightAsync(new CoreRuleEngine(), state, ["P1-UNIT-ASSEMBLE-TARGET"]);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-EDGE-OF-NIGHT"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "EQUIPMENT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> AssembleEdgeOfNightAsync(
        CoreRuleEngine engine,
        MatchState state,
        string sourceObjectId,
        string targetObjectId,
        IReadOnlyList<string> optionalCosts)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-edge-of-night-assemble", "P1", CommandTypes.AssembleEquipment),
            new AssembleEquipmentCommand(sourceObjectId, targetObjectId, optionalCosts),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> PlayEdgeOfNightAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-edge-of-night-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-EQUIPMENT-EDGE-OF-NIGHT",
                "SFD·139/221",
                targetObjectIds),
            CancellationToken.None);
    }

    private static int CountEvents(IReadOnlyList<GameEvent> events, string kind)
    {
        return events.Count(gameEvent => string.Equals(gameEvent.Kind, kind, StringComparison.Ordinal));
    }

    private static JsonElement PromptScopedRawCommand(string cmdType, ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
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
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Empty(rawCommand.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static StackItemState AssertEdgeOfNightPlayStackPriorityState(
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
        Assert.Equal(["P1-UNIT-ASSEMBLE-TARGET"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.PlayerZones["P2"].Graveyard);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal("STACK", result.State.ObjectLocations["P1-EQUIPMENT-EDGE-OF-NIGHT"].Zone);
        Assert.Equal("BASE", result.State.ObjectLocations["P1-UNIT-ASSEMBLE-TARGET"].Zone);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"].AttachedToObjectId);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1-EQUIPMENT-EDGE-OF-NIGHT", stackItem.SourceObjectId);
        Assert.Equal("SFD·139/221", stackItem.CardNo);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        Assert.Equal("EDGE_OF_NIGHT_PLAY_EQUIPMENT", stackItem.EffectKind);
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

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }

    private static MatchState BuildEdgeOfNightState(int purplePower = 1)
    {
        return new MatchState(
            roomId: "edge-of-night-assemble-guard-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: RunePools(mana: 0, purplePower: purplePower),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-EDGE-OF-NIGHT"],
                    Base =
                    [
                        "P1-EQUIPMENT-EDGE-OF-NIGHT",
                        "P1-FACE-DOWN-EDGE-OF-NIGHT",
                        "P1-ATTACHED-EDGE-OF-NIGHT",
                        "P1-UNIT-ASSEMBLE-TARGET",
                        "P1-FACE-DOWN-STANDBY-UNIT"
                    ],
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-EQUIPMENT",
                        "P1-BATTLEFIELD-SPELL",
                        "P1-BATTLEFIELD-RUNE"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-EQUIPMENT-EDGE-OF-NIGHT"],
                    Battlefields = ["P2-UNIT-ASSEMBLE-TARGET"]
                }
            },
            cardObjects: CardObjects(includePlaySource: false));
    }

    private static MatchState BuildEdgeOfNightPlayState()
    {
        return new MatchState(
            roomId: "edge-of-night-play-guard-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: RunePools(mana: 3, purplePower: 0),
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-EDGE-OF-NIGHT"],
                    Base = ["P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: CardObjects(includePlaySource: true));
    }

    private static Dictionary<string, string> Seats()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P1"] = "connection-1",
            ["P2"] = "connection-2"
        };
    }

    private static Dictionary<string, RunePool> RunePools(int mana, int purplePower)
    {
        return new Dictionary<string, RunePool>(StringComparer.Ordinal)
        {
            ["P1"] = purplePower > 0
                ? new RunePool(
                    mana,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Purple] = purplePower
                    })
                : new RunePool(mana, 0),
            ["P2"] = RunePool.Empty
        };
    }

    private static Dictionary<string, CardObjectState> CardObjects(bool includePlaySource)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-EQUIPMENT-EDGE-OF-NIGHT"] = EdgeOfNight("P1-EQUIPMENT-EDGE-OF-NIGHT"),
            ["P1-HAND-EDGE-OF-NIGHT"] = EdgeOfNight("P1-HAND-EDGE-OF-NIGHT"),
            ["P1-FACE-DOWN-EDGE-OF-NIGHT"] = EdgeOfNight(
                "P1-FACE-DOWN-EDGE-OF-NIGHT",
                isFaceDown: true,
                tags: [CardObjectTags.EquipmentCard, CardObjectTags.Standby]),
            ["P1-ATTACHED-EDGE-OF-NIGHT"] = EdgeOfNight(
                "P1-ATTACHED-EDGE-OF-NIGHT",
                attachedToObjectId: "P1-OLD-HOST"),
            ["P2-EQUIPMENT-EDGE-OF-NIGHT"] = EdgeOfNight("P2-EQUIPMENT-EDGE-OF-NIGHT", ownerId: "P2", controllerId: "P2"),
            ["P1-UNIT-ASSEMBLE-TARGET"] = Unit("P1-UNIT-ASSEMBLE-TARGET"),
            ["P1-OLD-HOST"] = Unit("P1-OLD-HOST"),
            ["P1-FACE-DOWN-STANDBY-UNIT"] = Unit("P1-FACE-DOWN-STANDBY-UNIT", isFaceDown: true),
            ["P2-UNIT-ASSEMBLE-TARGET"] = Unit("P2-UNIT-ASSEMBLE-TARGET", ownerId: "P2", controllerId: "P2"),
            ["P1-BATTLEFIELD-EQUIPMENT"] = Equipment("P1-BATTLEFIELD-EQUIPMENT"),
            ["P1-BATTLEFIELD-SPELL"] = Spell("P1-BATTLEFIELD-SPELL"),
            ["P1-BATTLEFIELD-RUNE"] = Rune("P1-BATTLEFIELD-RUNE")
        };

        if (includePlaySource)
        {
            cardObjects["P1-EQUIPMENT-EDGE-OF-NIGHT"] = new CardObjectState(
                "P1-EQUIPMENT-EDGE-OF-NIGHT",
                cardNo: "SFD·139/221",
                manaCost: 3,
                tags: [CardObjectTags.EquipmentCard],
                ownerId: "P1",
                controllerId: "P1");
        }

        return cardObjects;
    }

    private static CardObjectState EdgeOfNight(
        string objectId,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string? attachedToObjectId = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            isFaceDown: isFaceDown,
            tags: tags ?? [CardObjectTags.EquipmentCard, "武装"],
            manaCost: 3,
            attachedToObjectId: attachedToObjectId,
            cardNo: isFaceDown ? null : "SFD·139/221",
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState Unit(
        string objectId,
        bool isFaceDown = false,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            isFaceDown: isFaceDown,
            power: 3,
            tags: isFaceDown ? [CardObjectTags.UnitCard, CardObjectTags.Standby] : [CardObjectTags.UnitCard],
            cardNo: isFaceDown ? null : "SFD·125/221",
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState Equipment(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·022/221",
            tags: [CardObjectTags.EquipmentCard],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Spell(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·169/298",
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Rune(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "RUNES·001",
            tags: [CardObjectTags.RuneCard],
            ownerId: "P1",
            controllerId: "P1");
    }
}
