using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AnyUnitTargetScopeGuardTests
{
    [Theory]
    [InlineData("P1-BASE-UNIT")]
    [InlineData("P2-BATTLEFIELD-UNIT")]
    public async Task FirstMateReadiesOnlyPublicFieldUnitTargets(string targetObjectId)
    {
        var engine = new CoreRuleEngine();
        var state = BuildFirstMateState();

        var played = await PlayFirstMateAsync(engine, state, targetObjectId);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Equal([targetObjectId], stackItem.TargetObjectIds);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "FIRST_MATE_PLAY_UNIT_READY_ANOTHER_UNIT", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-first-mate-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-first-mate-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains("P1-UNIT-FIRST-MATE", p2Pass.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("P1-UNIT-FIRST-MATE", p2Pass.State.PlayerZones["P1"].Hand);
        Assert.False(p2Pass.State.CardObjects[targetObjectId].IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, targetObjectId, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P1-BATTLEFIELD-SPELL")]
    [InlineData("P1-BATTLEFIELD-RUNE")]
    [InlineData("P1-FACE-DOWN-STANDBY")]
    [InlineData("P1-FACE-UP-STANDBY")]
    [InlineData("P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT")]
    [InlineData("P1-HAND-UNIT")]
    [InlineData("P1-STALE-UNIT")]
    public async Task FirstMateRejectsNonPublicFieldUnitTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildFirstMateState();

        var result = await PlayFirstMateAsync(new CoreRuleEngine(), state, targetObjectId);

        AssertRejectedWithoutMutation(state, result);
        Assert.True(result.State.CardObjects["P1-BASE-UNIT"].IsExhausted);
        Assert.True(result.State.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);
    }

    [Fact]
    public async Task AnyUnitScopeRejectsNonUnitWhenBehaviorDoesNotRequireUnitTag()
    {
        var state = BuildCurtainRisesState();

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-curtain-rises-equipment-target", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-CURTAIN-RISES",
                "UNL-009/219",
                ["P1-BATTLEFIELD-EQUIPMENT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-CURTAIN-RISES"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
    }

    [Fact]
    public async Task FirstMateAnyUnitTargetScopeStalePromptReplayUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildFirstMateState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            "P1-UNIT-FIRST-MATE",
            "OGN·132/298",
            ["P1-BASE-UNIT"]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, "P1-UNIT-FIRST-MATE", StringComparison.Ordinal));
        Assert.Contains(playCandidate.Targets ?? [], target => string.Equals(target.Id, "P1-BASE-UNIT", StringComparison.Ordinal));

        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-first-mate-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-first-mate-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertFirstMateAcceptedState(accepted);
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
        AssertFirstMateAcceptedState(replay, acceptedStackItem);
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
        AssertFirstMateAcceptedState(duplicateReplay, acceptedStackItem);
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
        AssertFirstMateAcceptedState(conflict, acceptedStackItem);
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

    private static async Task<ResolutionResult> PlayFirstMateAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-first-mate-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-UNIT-FIRST-MATE",
                "OGN·132/298",
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
        Assert.Equal("P1-UNIT-FIRST-MATE", rawCommand.GetProperty("cardObjectId").GetString());
        Assert.Equal("OGN·132/298", rawCommand.GetProperty("cardNo").GetString());
        Assert.Equal(
            ["P1-BASE-UNIT"],
            rawCommand.GetProperty("targetObjectIds")
                .EnumerateArray()
                .Select(target => target.GetString()!)
                .ToArray());
        Assert.Empty(rawCommand.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static void AssertRejectedWithoutMutation(MatchState initialState, ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(initialState), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-FIRST-MATE", "P1-HAND-UNIT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal));
    }

    private static StackItemState AssertFirstMateAcceptedState(
        ResolutionResult result,
        StackItemState? expectedStackItem = null)
    {
        Assert.Equal(1, result.State.Tick);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-UNIT"], result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("P1-UNIT-FIRST-MATE", result.State.PlayerZones["P1"].Base);
        Assert.Equal("STACK", result.State.ObjectLocations["P1-UNIT-FIRST-MATE"].Zone);
        Assert.Null(result.State.PendingPayment);
        Assert.True(result.State.CardObjects["P1-BASE-UNIT"].IsExhausted);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1-UNIT-FIRST-MATE", stackItem.SourceObjectId);
        Assert.Equal("OGN·132/298", stackItem.CardNo);
        Assert.Equal(["P1-BASE-UNIT"], stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        Assert.Equal("FIRST_MATE_PLAY_UNIT_READY_ANOTHER_UNIT", stackItem.EffectKind);
        if (expectedStackItem is not null)
        {
            Assert.Equal(expectedStackItem.StackItemId, stackItem.StackItemId);
            Assert.Equal(expectedStackItem.ControllerId, stackItem.ControllerId);
            Assert.Equal(expectedStackItem.SourceObjectId, stackItem.SourceObjectId);
            Assert.Equal(expectedStackItem.EffectKind, stackItem.EffectKind);
            Assert.Equal(expectedStackItem.CardNo, stackItem.CardNo);
            Assert.Equal(expectedStackItem.TargetObjectIds, stackItem.TargetObjectIds);
            Assert.Equal(expectedStackItem.DamageAmount, stackItem.DamageAmount);
            Assert.Equal(expectedStackItem.EffectRepeatCount, stackItem.EffectRepeatCount);
            Assert.Equal(expectedStackItem.OptionalCosts, stackItem.OptionalCosts);
            Assert.Equal(expectedStackItem.PlayedAfterAnotherCardThisTurn, stackItem.PlayedAfterAnotherCardThisTurn);
            Assert.Equal(expectedStackItem.Destination, stackItem.Destination);
            Assert.Equal(expectedStackItem.TimingContext, stackItem.TimingContext);
        }

        return stackItem;
    }

    private static MatchState BuildFirstMateState()
    {
        return new MatchState(
            roomId: "any-unit-target-scope-guard-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: Seats(),
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
                    Hand = ["P1-UNIT-FIRST-MATE", "P1-HAND-UNIT"],
                    Base = ["P1-BASE-UNIT"],
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-EQUIPMENT",
                        "P1-BATTLEFIELD-SPELL",
                        "P1-BATTLEFIELD-RUNE",
                        "P1-FACE-DOWN-STANDBY",
                        "P1-FACE-UP-STANDBY",
                        "P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-FIRST-MATE"] = Unit(
                    "P1-UNIT-FIRST-MATE",
                    cardNo: "OGN·132/298",
                    power: 3,
                    isExhausted: false),
                ["P1-HAND-UNIT"] = Unit("P1-HAND-UNIT"),
                ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT"),
                ["P2-BATTLEFIELD-UNIT"] = Unit("P2-BATTLEFIELD-UNIT", ownerId: "P2", controllerId: "P2"),
                ["P1-BATTLEFIELD-EQUIPMENT"] = NonUnit("P1-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P1"),
                ["P1-BATTLEFIELD-SPELL"] = NonUnit("P1-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P1"),
                ["P1-BATTLEFIELD-RUNE"] = NonUnit("P1-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P1"),
                ["P1-FACE-DOWN-STANDBY"] = Unit(
                    "P1-FACE-DOWN-STANDBY",
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P1-FACE-UP-STANDBY"] = Unit(
                    "P1-FACE-UP-STANDBY",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"] = Unit(
                    "P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-STALE-UNIT"] = Unit("P1-STALE-UNIT")
            });
    }

    private static MatchState BuildCurtainRisesState()
    {
        return new MatchState(
            roomId: "any-unit-no-required-tag-scope-guard-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: Seats(),
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
                    Hand = ["P1-SPELL-CURTAIN-RISES"],
                    Battlefields = ["P1-BATTLEFIELD-EQUIPMENT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-CURTAIN-RISES"] = NonUnit("P1-SPELL-CURTAIN-RISES", "UNL-009/219", CardObjectTags.SpellCard, "P1", manaCost: 2),
                ["P1-BATTLEFIELD-EQUIPMENT"] = NonUnit("P1-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P1")
            });
    }

    private static Dictionary<string, string> Seats()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P1"] = "connection-1",
            ["P2"] = "connection-2"
        };
    }

    private static CardObjectState Unit(
        string objectId,
        string? cardNo = "SFD·125/221",
        int power = 2,
        bool isFaceDown = false,
        bool isExhausted = true,
        IReadOnlyList<string>? tags = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            isFaceDown: isFaceDown,
            isExhausted: isExhausted,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState NonUnit(
        string objectId,
        string cardNo,
        string tag,
        string playerId,
        int manaCost = 0)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            manaCost: manaCost,
            power: 2,
            isExhausted: true,
            tags: [tag],
            ownerId: playerId,
            controllerId: playerId);
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
