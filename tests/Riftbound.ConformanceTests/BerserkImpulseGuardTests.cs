using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BerserkImpulseGuardTests
{
    [Fact]
    public async Task BerserkImpulsePlaysOpponentTopMainDeckUnitToControllerBaseAndResetsState()
    {
        var engine = new CoreRuleEngine();
        var state = BuildBerserkImpulseState("P2-TOP-UNIT");

        var played = await PlayBerserkImpulseAsync(engine, state, "P2-TOP-UNIT");
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Empty(played.State.PlayerZones["P1"].Hand);
        Assert.Single(played.State.StackItems);

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-berserk-impulse-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-berserk-impulse-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(["P1-BASE-UNIT", "P2-TOP-UNIT"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-SECOND-UNIT"], p2Pass.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(["P1-SPELL-BERSERK-IMPULSE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-TOP-UNIT"].Damage);
        Assert.Equal(6, p2Pass.State.CardObjects["P2-TOP-UNIT"].Power);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-TOP-UNIT"].UntilEndOfTurnPowerModifier);
        Assert.Empty(p2Pass.State.CardObjects["P2-TOP-UNIT"].UntilEndOfTurnEffects);
        Assert.False(p2Pass.State.CardObjects["P2-TOP-UNIT"].IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, "P1-SPELL-BERSERK-IMPULSE", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, "P2-TOP-UNIT", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["ownerPlayerId"] as string, "P2", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["playedByPlayerId"] as string, "P1", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceZone"] as string, "MAIN_DECK", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["destinationZone"] as string, "BASE", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P1-TOP-UNIT", "P2-TOP-UNIT")]
    [InlineData("P2-SECOND-UNIT", "P2-TOP-UNIT")]
    [InlineData("P2-TOP-SPELL", "P2-TOP-SPELL")]
    [InlineData("P2-TOP-EQUIPMENT", "P2-TOP-EQUIPMENT")]
    [InlineData("P2-TOP-RUNE", "P2-TOP-RUNE")]
    [InlineData("P2-TOP-FACE-DOWN-UNIT", "P2-TOP-FACE-DOWN-UNIT")]
    [InlineData("P2-HAND-UNIT", "P2-TOP-UNIT")]
    [InlineData("P2-BASE-UNIT", "P2-TOP-UNIT")]
    [InlineData("P2-BATTLEFIELD-UNIT", "P2-TOP-UNIT")]
    public async Task BerserkImpulseRejectsInvalidTargetsWithoutMutation(
        string targetObjectId,
        string opponentTopObjectId)
    {
        var state = BuildBerserkImpulseState(opponentTopObjectId);
        var initialP1Hand = state.PlayerZones["P1"].Hand;
        var initialP1MainDeck = state.PlayerZones["P1"].MainDeck;
        var initialP1Base = state.PlayerZones["P1"].Base;
        var initialP2Hand = state.PlayerZones["P2"].Hand;
        var initialP2MainDeck = state.PlayerZones["P2"].MainDeck;
        var initialP2Base = state.PlayerZones["P2"].Base;
        var initialP2Battlefields = state.PlayerZones["P2"].Battlefields;

        var result = await PlayBerserkImpulseAsync(new CoreRuleEngine(), state, targetObjectId);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(initialP1Hand, result.State.PlayerZones["P1"].Hand);
        Assert.Equal(initialP1MainDeck, result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(initialP1Base, result.State.PlayerZones["P1"].Base);
        Assert.Equal(initialP2Hand, result.State.PlayerZones["P2"].Hand);
        Assert.Equal(initialP2MainDeck, result.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(initialP2Base, result.State.PlayerZones["P2"].Base);
        Assert.Equal(initialP2Battlefields, result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P2-OTHER-TOP-UNIT", "P2-DIRTY-TARGET-UNIT")]
    [InlineData("P2-DIRTY-TARGET-UNIT", "P2-DIRTY-TARGET-UNIT-P1-CONTROLLED")]
    [InlineData("P2-DIRTY-TARGET-SPELL", "P2-DIRTY-TARGET-SPELL")]
    [InlineData("P2-DIRTY-FACE-DOWN-UNIT", "P2-DIRTY-FACE-DOWN-UNIT")]
    public async Task BerserkImpulseDirtyResolutionDoesNotMoveInvalidTopDeckTarget(
        string opponentTopObjectId,
        string targetObjectId)
    {
        var engine = new CoreRuleEngine();
        var state = BuildDirtyResolutionState(opponentTopObjectId, targetObjectId);
        var initialP2MainDeck = state.PlayerZones["P2"].MainDeck;

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-berserk-impulse-dirty-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-berserk-impulse-dirty-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(initialP2MainDeck, p2Pass.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(["P1-SPELL-BERSERK-IMPULSE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BerserkImpulsePlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildBerserkImpulseState("P2-TOP-UNIT");
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            "P1-SPELL-BERSERK-IMPULSE",
            "OGN·025/298",
            ["P2-TOP-UNIT"]);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.False(playCandidate.Enabled);
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-berserk-impulse-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-berserk-impulse-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Contains(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT", StringComparison.Ordinal));
        var acceptedStackItem = AssertBerserkImpulseStackPriorityState(accepted);
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
        AssertBerserkImpulseStackPriorityState(replay, acceptedStackItem);
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
        AssertBerserkImpulseStackPriorityState(duplicateRejected, acceptedStackItem);
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
        AssertBerserkImpulseStackPriorityState(conflict, acceptedStackItem);
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

    private static async Task<ResolutionResult> PlayBerserkImpulseAsync(
        CoreRuleEngine engine,
        MatchState state,
        string targetObjectId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-berserk-impulse-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-BERSERK-IMPULSE",
                "OGN·025/298",
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
        PlayCardCommand command,
        ActionPromptDto prompt)
    {
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

    private static StackItemState AssertBerserkImpulseStackPriorityState(
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
        Assert.Equal(["P1-TOP-UNIT"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-UNIT"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-HAND-UNIT"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-TOP-UNIT", "P2-SECOND-UNIT"], result.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(["P2-BASE-UNIT"], result.State.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BATTLEFIELD-UNIT"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Null(result.State.PendingPayment);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1-SPELL-BERSERK-IMPULSE", stackItem.SourceObjectId);
        Assert.Equal("OGN·025/298", stackItem.CardNo);
        Assert.Equal(["P2-TOP-UNIT"], stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        Assert.Equal("BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT", stackItem.EffectKind);
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

    private static MatchState BuildBerserkImpulseState(string opponentTopObjectId)
    {
        return new MatchState(
            roomId: "berserk-impulse-guard-test",
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
                ["P1"] = new(4, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-TOP-UNIT"],
                    Hand = ["P1-SPELL-BERSERK-IMPULSE"],
                    Base = ["P1-BASE-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-UNIT"],
                    MainDeck = [opponentTopObjectId, "P2-SECOND-UNIT"],
                    Base = ["P2-BASE-UNIT"],
                    Battlefields = ["P2-BATTLEFIELD-UNIT"]
                }
            },
            cardObjects: BaseCardObjects());
    }

    private static MatchState BuildDirtyResolutionState(
        string opponentTopObjectId,
        string targetObjectId)
    {
        var cardObjects = BaseCardObjects();
        cardObjects["P2-OTHER-TOP-UNIT"] = Unit("P2-OTHER-TOP-UNIT", power: 2);
        cardObjects["P2-DIRTY-TARGET-UNIT"] = Unit("P2-DIRTY-TARGET-UNIT", power: 3);
        cardObjects["P2-DIRTY-TARGET-UNIT-P1-CONTROLLED"] = new(
            "P2-DIRTY-TARGET-UNIT-P1-CONTROLLED",
            cardNo: "SFD·125/221",
            power: 3,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P2-DIRTY-TARGET-SPELL"] = Spell("P2-DIRTY-TARGET-SPELL");
        cardObjects["P2-DIRTY-FACE-DOWN-UNIT"] = Unit(
            "P2-DIRTY-FACE-DOWN-UNIT",
            power: 3,
            isFaceDown: true);

        return new MatchState(
            roomId: "berserk-impulse-dirty-resolution-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = [opponentTopObjectId, "P2-DECK-KEEP"]
                }
            },
            cardObjects: cardObjects,
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    "STACK-BERSERK-IMPULSE-DIRTY",
                    "P1",
                    "P1-SPELL-BERSERK-IMPULSE",
                    "BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT",
                    "OGN·025/298",
                    [targetObjectId])
            ]);
    }

    private static Dictionary<string, string> Seats()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P1"] = "connection-1",
            ["P2"] = "connection-2"
        };
    }

    private static Dictionary<string, CardObjectState> BaseCardObjects()
    {
        return new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-SPELL-BERSERK-IMPULSE"] = new(
                "P1-SPELL-BERSERK-IMPULSE",
                cardNo: "OGN·025/298",
                manaCost: 4,
                tags: [CardObjectTags.SpellCard],
                ownerId: "P1",
                controllerId: "P1"),
            ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT", power: 2),
            ["P1-TOP-UNIT"] = Unit("P1-TOP-UNIT", power: 2),
            ["P2-TOP-UNIT"] = new(
                "P2-TOP-UNIT",
                damage: 1,
                untilEndOfTurnEffects: ["STUNNED"],
                power: 8,
                untilEndOfTurnPowerModifier: 2,
                isExhausted: true,
                tags: [CardObjectTags.UnitCard],
                manaCost: 6),
            ["P2-SECOND-UNIT"] = Unit("P2-SECOND-UNIT", power: 3),
            ["P2-TOP-SPELL"] = Spell("P2-TOP-SPELL"),
            ["P2-TOP-EQUIPMENT"] = Equipment("P2-TOP-EQUIPMENT"),
            ["P2-TOP-RUNE"] = Rune("P2-TOP-RUNE"),
            ["P2-TOP-FACE-DOWN-UNIT"] = Unit("P2-TOP-FACE-DOWN-UNIT", power: 3, isFaceDown: true),
            ["P2-HAND-UNIT"] = Unit("P2-HAND-UNIT", power: 3),
            ["P2-BASE-UNIT"] = Unit("P2-BASE-UNIT", power: 3),
            ["P2-BATTLEFIELD-UNIT"] = Unit("P2-BATTLEFIELD-UNIT", power: 3),
            ["P2-DECK-KEEP"] = Spell("P2-DECK-KEEP")
        };
    }

    private static CardObjectState Unit(string objectId, int power, bool isFaceDown = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: isFaceDown ? null : "SFD·125/221",
            power: power,
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.UnitCard]);
    }

    private static CardObjectState Spell(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "OGN·169/298",
            manaCost: 1,
            tags: [CardObjectTags.SpellCard]);
    }

    private static CardObjectState Equipment(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "SFD·139/221",
            manaCost: 1,
            tags: [CardObjectTags.EquipmentCard]);
    }

    private static CardObjectState Rune(string objectId)
    {
        return new CardObjectState(
            objectId,
            cardNo: "RUNES·001",
            tags: [CardObjectTags.RuneCard]);
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
