using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AgileEquipmentDirectPlayAttachTests
{
    [Theory]
    [InlineData("SFD·022/221", "P1-EQUIPMENT-LONG-SWORD", 2)]
    [InlineData("SFD·056/221", "P1-EQUIPMENT-STERAKS", 3)]
    [InlineData("SFD·064/221", "P1-EQUIPMENT-CLOTH-ARMOR", 1)]
    [InlineData("SFD·186/221", "P1-EQUIPMENT-SPINNING-AXE", 2)]
    public async Task AgileEquipmentDirectPlayAttachesToControlledUnit(
        string cardNo,
        string sourceObjectId,
        int manaCost)
    {
        var engine = new CoreRuleEngine();
        var state = BuildAgileEquipmentState(cardNo, sourceObjectId, manaCost);

        var played = await PlayAgileEquipmentAsync(engine, state, cardNo, sourceObjectId, "P1-BASE-UNIT");

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(3 - manaCost, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Equal([sourceObjectId], played.State.ObjectLocations
            .Where(entry => string.Equals(entry.Value.Zone, "STACK", StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .ToArray());
        Assert.Equal(["P1-BASE-UNIT"], stackItem.TargetObjectIds);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-agile-equipment-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-agile-equipment-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains(sourceObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal("P1-BASE-UNIT", p2Pass.State.CardObjects[sourceObjectId].AttachedToObjectId);
        Assert.Contains(CardObjectTags.EquipmentCard, p2Pass.State.CardObjects[sourceObjectId].Tags);
        Assert.Contains("灵便", p2Pass.State.CardObjects[sourceObjectId].Tags);
        Assert.Equal("P1", p2Pass.State.ObjectLocations[sourceObjectId].PlayerId);
        Assert.Equal("BASE", p2Pass.State.ObjectLocations[sourceObjectId].Zone);

        var attachedEvent = Assert.Single(
            p2Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal(sourceObjectId, attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal("P1-BASE-UNIT", attachedEvent.Payload["unitObjectId"]);
        Assert.Equal("P1-BASE-UNIT", attachedEvent.Payload["attachedToObjectId"]);
        Assert.Equal(cardNo, attachedEvent.Payload["equipmentCardNo"]);
        Assert.Equal("AGILE_DIRECT_PLAY_ATTACH", attachedEvent.Payload["reason"]);
    }

    [Fact]
    public void AgileEquipmentPromptExposesControlledUnitTarget()
    {
        var state = BuildAgileEquipmentState("SFD·022/221", "P1-EQUIPMENT-LONG-SWORD", 2);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        Assert.Contains(playCandidate.Sources ?? [], choice => string.Equals(choice.Id, "P1-EQUIPMENT-LONG-SWORD", StringComparison.Ordinal));
        Assert.Contains(playCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P1-BASE-UNIT", StringComparison.Ordinal));
        Assert.Contains(playCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P1-BATTLEFIELD-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(playCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P2-BASE-UNIT", StringComparison.Ordinal));
        Assert.DoesNotContain(playCandidate.Targets ?? [], choice => string.Equals(choice.Id, "P1-EQUIPMENT-TARGET", StringComparison.Ordinal));

        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]);
        var requirement = Assert.Single(
            sourceRequirements,
            entry => string.Equals(entry["sourceObjectId"] as string, "P1-EQUIPMENT-LONG-SWORD", StringComparison.Ordinal));
        Assert.Equal(1, Assert.IsType<int>(requirement["minTargetCount"]));
        Assert.Equal(1, Assert.IsType<int>(requirement["maxTargetCount"]));
        Assert.Equal(CardTargetScopes.FriendlyUnit, Assert.IsType<string>(requirement["targetScope"]));
        Assert.Equal("友方单位", Assert.IsType<string>(requirement["targetScopeLabel"]));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(requirement["targetChoicesByIndex"]);
        var firstChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"]);
        Assert.Equal(
            ["P1-BASE-UNIT", "P1-BATTLEFIELD-UNIT"],
            firstChoices.Select(choice => choice.Id).ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("P2-BASE-UNIT")]
    [InlineData("P1-EQUIPMENT-TARGET")]
    [InlineData("P1-SPELL-TARGET")]
    [InlineData("P1-RUNE-TARGET")]
    [InlineData("P1-FACE-DOWN-UNIT")]
    [InlineData("P1-STALE-UNIT")]
    [InlineData("P1-NONCONTROLLED-UNIT")]
    public async Task AgileEquipmentDirectPlayRejectsInvalidTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildAgileEquipmentState("SFD·022/221", "P1-EQUIPMENT-LONG-SWORD", 2);
        var targetObjectIds = string.IsNullOrWhiteSpace(targetObjectId) ? Array.Empty<string>() : [targetObjectId];

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-agile-equipment-invalid", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "SFD·022/221",
                targetObjectIds),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-LONG-SWORD"], result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("P1-EQUIPMENT-LONG-SWORD", result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AgileEquipmentDirectPlayStalePromptReplayUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildAgileEquipmentState("SFD·022/221", "P1-EQUIPMENT-LONG-SWORD", 2);
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            "P1-EQUIPMENT-LONG-SWORD",
            "SFD·022/221",
            ["P1-BASE-UNIT"]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, "P1-EQUIPMENT-LONG-SWORD", StringComparison.Ordinal));
        Assert.Contains(playCandidate.Targets ?? [], target => string.Equals(target.Id, "P1-BASE-UNIT", StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-agile-equipment-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-agile-equipment-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertAgileEquipmentStackPriorityState(accepted);
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
        AssertAgileEquipmentStackPriorityState(replay, acceptedStackItem);
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
        var journalHashAfterReplay = MatchStateHasher.HashValue(journal.Entries);

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
        AssertAgileEquipmentStackPriorityState(duplicateReplay, acceptedStackItem);
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
        AssertAgileEquipmentStackPriorityState(conflict, acceptedStackItem);
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

    private static async Task<ResolutionResult> PlayAgileEquipmentAsync(
        CoreRuleEngine engine,
        MatchState state,
        string cardNo,
        string sourceObjectId,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-agile-equipment-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(sourceObjectId, cardNo, [targetObjectId]),
            CancellationToken.None);
    }

    private static StackItemState AssertAgileEquipmentStackPriorityState(
        ResolutionResult result,
        StackItemState? expectedStackItem = null)
    {
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("P1-EQUIPMENT-LONG-SWORD", result.State.PlayerZones["P1"].Base);
        Assert.Equal("STACK", result.State.ObjectLocations["P1-EQUIPMENT-LONG-SWORD"].Zone);
        Assert.Null(result.State.CardObjects["P1-EQUIPMENT-LONG-SWORD"].AttachedToObjectId);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1-EQUIPMENT-LONG-SWORD", stackItem.SourceObjectId);
        Assert.Equal(["P1-BASE-UNIT"], stackItem.TargetObjectIds);
        Assert.Equal(expectedStackItem ?? stackItem, stackItem);

        return stackItem;
    }

    private static JsonElement PromptScopedPlayCardRawCommand(
        PlayCardCommand command,
        ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PlayCard,
            sourceObjectId = command.SourceObjectId,
            cardNo = command.CardNo,
            targetObjectIds = command.TargetObjectIds,
            optionalCosts = command.OptionalCosts ?? Array.Empty<string>(),
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
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
            sourceObjectId = command.SourceObjectId,
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
        ActionPromptDto prompt)
    {
        Assert.Equal(CommandTypes.PlayCard, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(command.SourceObjectId, rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal(command.CardNo, rawCommand.GetProperty("cardNo").GetString());
        Assert.Equal(
            command.TargetObjectIds,
            rawCommand.GetProperty("targetObjectIds")
                .EnumerateArray()
                .Select(target => target.GetString()!)
                .ToArray());
        Assert.Equal(
            command.OptionalCosts ?? Array.Empty<string>(),
            rawCommand.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => choice.GetString()!)
                .ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static MatchState BuildAgileEquipmentState(
        string cardNo,
        string sourceObjectId,
        int manaCost)
    {
        return new MatchState(
            roomId: "agile-equipment-direct-play-test",
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
                ["P1"] = new(3, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [sourceObjectId],
                    Base =
                    [
                        "P1-BASE-UNIT",
                        "P1-EQUIPMENT-TARGET",
                        "P1-SPELL-TARGET",
                        "P1-RUNE-TARGET",
                        "P1-FACE-DOWN-UNIT",
                        "P1-NONCONTROLLED-UNIT"
                    ],
                    Battlefields = ["P1-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [sourceObjectId] = Equipment(sourceObjectId, cardNo, manaCost),
                ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT", "P1", "P1"),
                ["P1-BATTLEFIELD-UNIT"] = Unit("P1-BATTLEFIELD-UNIT", "P1", "P1"),
                ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", "P2", "P2"),
                ["P1-NONCONTROLLED-UNIT"] = Unit("P1-NONCONTROLLED-UNIT", "P1", "P2"),
                ["P1-STALE-UNIT"] = Unit("P1-STALE-UNIT", "P1", "P1"),
                ["P1-FACE-DOWN-UNIT"] = Unit("P1-FACE-DOWN-UNIT", "P1", "P1", isFaceDown: true),
                ["P1-EQUIPMENT-TARGET"] = NonUnit("P1-EQUIPMENT-TARGET", "SFD·139/221", CardObjectTags.EquipmentCard),
                ["P1-SPELL-TARGET"] = NonUnit("P1-SPELL-TARGET", "SFD·006/221", CardObjectTags.SpellCard),
                ["P1-RUNE-TARGET"] = NonUnit("P1-RUNE-TARGET", "FND·001/298", CardObjectTags.RuneCard)
            });
    }

    private static CardObjectState Equipment(string objectId, string cardNo, int manaCost)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            manaCost: manaCost,
            tags: [CardObjectTags.EquipmentCard],
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState Unit(
        string objectId,
        string ownerId,
        string controllerId,
        bool isFaceDown = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "SFD·125/221",
            power: 3,
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState NonUnit(string objectId, string cardNo, string tag)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            tags: [tag],
            ownerId: "P1",
            controllerId: "P1");
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
