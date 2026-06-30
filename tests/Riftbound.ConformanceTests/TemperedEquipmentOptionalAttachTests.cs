using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TemperedEquipmentOptionalAttachTests
{
    private const string SentinelObjectId = "P1-UNIT-SENTINEL-ADEPT";
    private const string SentinelCardNo = "SFD·008/221";
    private const string SpinningAxeObjectId = "P1-EQUIPMENT-SPINNING-AXE";
    private const string SpinningAxeCardNo = "SFD·186/221";
    private const string LongSwordObjectId = "P1-EQUIPMENT-LONG-SWORD";
    private const string LongSwordCardNo = "SFD·022/221";

    [Fact]
    public void PromptExposesOnlyLegalTemperedAttachChoiceForSentinelAdept()
    {
        var state = BuildTemperedState();

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var requirement = Assert.Single(
            sourceRequirements,
            entry => string.Equals(entry["sourceObjectId"] as string, SentinelObjectId, StringComparison.Ordinal));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["optionalCostChoices"]);

        Assert.Equal(
            [TemperedAttachCost(LongSwordObjectId), TemperedAttachCost(SpinningAxeObjectId)],
            optionalCostChoices.Select(choice => choice.Id).ToArray());
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            requirement["targetChoicesByIndex"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["minTargetCount"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["maxTargetCount"]));
    }

    [Fact]
    public void TemperedAttachPromptChoiceIsIsolatedFromOpponentPrompt()
    {
        var state = BuildTemperedState();

        var prompts = ResolutionResult.BuildPrompts(state);
        var p1Prompt = prompts["P1"];
        var p2Prompt = prompts["P2"];

        Assert.True(p1Prompt.Actionable);
        Assert.Contains(CommandTypes.PlayCard, p1Prompt.Actions);
        Assert.Equal(state.Tick, p1Prompt.SnapshotTick);
        var playCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, SentinelObjectId, StringComparison.Ordinal));

        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var requirement = Assert.Single(
            sourceRequirements,
            entry => string.Equals(entry["sourceObjectId"] as string, SentinelObjectId, StringComparison.Ordinal));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["optionalCostChoices"]);
        Assert.Equal(
            [TemperedAttachCost(LongSwordObjectId), TemperedAttachCost(SpinningAxeObjectId)],
            optionalCostChoices.Select(choice => choice.Id).ToArray());

        Assert.False(p2Prompt.Actionable);
        Assert.Equal(state.Tick, p2Prompt.SnapshotTick);
        Assert.DoesNotContain(CommandTypes.PlayCard, p2Prompt.Actions);
        Assert.DoesNotContain(
            p2Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var p2PromptJson = JsonSerializer.Serialize(p2Prompt);
        Assert.DoesNotContain(CommandTypes.PlayCard, p2PromptJson, StringComparison.Ordinal);
        Assert.DoesNotContain("optionalCostChoices", p2PromptJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LegalTemperedOptionalAttachAttachesAfterBothPlayersPass()
    {
        var engine = new CoreRuleEngine();
        var state = BuildTemperedState();
        var optionalCosts = new[] { TemperedAttachCost(SpinningAxeObjectId) };

        var played = await PlaySentinelAdeptAsync(engine, state, optionalCosts);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Equal(optionalCosts, stackItem.OptionalCosts);
        Assert.DoesNotContain(SentinelObjectId, played.State.PlayerZones["P1"].Hand);

        var p1Pass = await PassPriorityAsync(engine, played.State, "P1", "intent-tempered-p1-pass");
        var p2Pass = await PassPriorityAsync(engine, p1Pass.State, "P2", "intent-tempered-p2-pass");

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains(SentinelObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(SentinelObjectId, p2Pass.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.Contains(CardEquipmentKeywordNames.Tempered, p2Pass.State.CardObjects[SentinelObjectId].Tags);

        var attachedEvent = Assert.Single(
            p2Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal(SpinningAxeObjectId, attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal(SentinelObjectId, attachedEvent.Payload["unitObjectId"]);
        Assert.Equal(SentinelObjectId, attachedEvent.Payload["attachedToObjectId"]);
        Assert.Equal(SpinningAxeCardNo, attachedEvent.Payload["equipmentCardNo"]);
        Assert.Equal(SentinelCardNo, attachedEvent.Payload["unitCardNo"]);
        Assert.Equal("TEMPERED_OPTIONAL_ATTACH", attachedEvent.Payload["reason"]);
        Assert.Equal(optionalCosts, Assert.IsType<string[]>(attachedEvent.Payload["optionalCosts"]));
    }

    [Fact]
    public async Task LegalTemperedOptionalAttachAcceptsBehaviorSpecWeaponEquipment()
    {
        var engine = new CoreRuleEngine();
        var state = BuildTemperedState();
        var optionalCosts = new[] { TemperedAttachCost(LongSwordObjectId) };

        var played = await PlaySentinelAdeptAsync(engine, state, optionalCosts);
        var p1Pass = await PassPriorityAsync(engine, played.State, "P1", "intent-tempered-longsword-p1-pass");
        var p2Pass = await PassPriorityAsync(engine, p1Pass.State, "P2", "intent-tempered-longsword-p2-pass");

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Equal(SentinelObjectId, p2Pass.State.CardObjects[LongSwordObjectId].AttachedToObjectId);
        Assert.Null(p2Pass.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);

        var attachedEvent = Assert.Single(
            p2Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal(LongSwordObjectId, attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal(LongSwordCardNo, attachedEvent.Payload["equipmentCardNo"]);
        Assert.Equal(optionalCosts, Assert.IsType<string[]>(attachedEvent.Payload["optionalCosts"]));
    }

    [Fact]
    public async Task TemperedOptionalAttachPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildTemperedState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        IReadOnlyList<string> optionalCosts = [TemperedAttachCost(SpinningAxeObjectId)];
        var command = new PlayCardCommand(
            SentinelObjectId,
            SentinelCardNo,
            [],
            OptionalCosts: optionalCosts);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, SentinelObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var reorderedStaleRawCommand = ReorderedPromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        Assert.NotEqual(staleRawCommand.GetRawText(), reorderedStaleRawCommand.GetRawText());
        AssertPromptScopedPlayCardRawCommand(reorderedStaleRawCommand, prompt, optionalCosts);
        Assert.False(reorderedStaleRawCommand.TryGetProperty("clientNote", out _));
        const string acceptedClientIntentId = "intent-tempered-sentinel-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-tempered-sentinel-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedStackItem = AssertSentinelStackPriorityState(accepted, optionalCosts);
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
        AssertPromptScopedPlayCardRawCommand(acceptedJournalEntry.RawCommand.Value, prompt, optionalCosts);
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
        AssertSentinelStackPriorityState(replay, optionalCosts, acceptedStackItem);
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
        AssertPromptScopedPlayCardRawCommand(rejectedJournalEntry.RawCommand.Value, prompt, optionalCosts);
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
        AssertSentinelStackPriorityState(reorderedDuplicateRejected, optionalCosts, acceptedStackItem);
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
        AssertSentinelStackPriorityState(duplicateRejected, optionalCosts, acceptedStackItem);
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
        AssertSentinelStackPriorityState(conflict, optionalCosts, acceptedStackItem);
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
    public async Task NoTemperedOptionalAttachStillPlaysSentinelAdeptWithoutAttachment()
    {
        var engine = new CoreRuleEngine();
        var state = BuildTemperedState();

        var played = await PlaySentinelAdeptAsync(engine, state);
        var p1Pass = await PassPriorityAsync(engine, played.State, "P1", "intent-tempered-no-optional-p1-pass");
        var p2Pass = await PassPriorityAsync(engine, p1Pass.State, "P2", "intent-tempered-no-optional-p2-pass");

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains(SentinelObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Null(p2Pass.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.DoesNotContain(
            p2Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("P2-EQUIPMENT-SPINNING-AXE")]
    [InlineData("P1-MISSING-SPINNING-AXE")]
    [InlineData("P1-NON-EQUIPMENT-SPINNING-AXE")]
    [InlineData("P1-HAND-SPINNING-AXE")]
    [InlineData("P1-FACE-DOWN-SPINNING-AXE")]
    [InlineData("P1-STALE-SPINNING-AXE")]
    [InlineData("P1-WRONG-CARD-EQUIPMENT")]
    [InlineData("P1-WRONG-CONTROLLER-SPINNING-AXE")]
    public async Task InvalidTemperedOptionalAttachChoiceRejectsWithoutMutation(string equipmentObjectId)
    {
        var state = BuildTemperedState();

        var result = await PlaySentinelAdeptAsync(
            new CoreRuleEngine(),
            state,
            [TemperedAttachCost(equipmentObjectId)]);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Contains(SentinelObjectId, result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(SentinelObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task ResolutionStaleTemperedAttachChoiceNoEffectsWithoutAttachEvent()
    {
        var engine = new CoreRuleEngine();
        var state = BuildTemperedState();
        var optionalCosts = new[] { TemperedAttachCost(SpinningAxeObjectId) };
        var played = await PlaySentinelAdeptAsync(engine, state, optionalCosts);
        var staleState = MoveSpinningAxeToGraveyard(played.State);

        var p1Pass = await PassPriorityAsync(engine, staleState, "P1", "intent-tempered-stale-p1-pass");
        var p2Pass = await PassPriorityAsync(engine, p1Pass.State, "P2", "intent-tempered-stale-p2-pass");

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Contains(SentinelObjectId, p2Pass.State.PlayerZones["P1"].Base);
        Assert.Contains(SpinningAxeObjectId, p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Null(p2Pass.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.DoesNotContain(
            p2Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlaySentinelAdeptAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-tempered-sentinel-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                SentinelObjectId,
                SentinelCardNo,
                [],
                OptionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> PassPriorityAsync(
        CoreRuleEngine engine,
        MatchState state,
        string playerId,
        string intentId)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent(intentId, playerId, CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static MatchState MoveSpinningAxeToGraveyard(MatchState state)
    {
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var p1Zones = playerZones["P1"];
        playerZones["P1"] = p1Zones with
        {
            Base = p1Zones.Base
                .Where(objectId => !string.Equals(objectId, SpinningAxeObjectId, StringComparison.Ordinal))
                .ToArray(),
            Graveyard = p1Zones.Graveyard
                .Concat([SpinningAxeObjectId])
                .ToArray()
        };

        return state with
        {
            PlayerZones = playerZones
        };
    }

    private static MatchState BuildTemperedState()
    {
        return new MatchState(
            roomId: "tempered-equipment-optional-attach-test",
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
                    Hand = [SentinelObjectId, "P1-HAND-SPINNING-AXE"],
                    Base =
                    [
                        SpinningAxeObjectId,
                        LongSwordObjectId,
                        "P1-NON-EQUIPMENT-SPINNING-AXE",
                        "P1-FACE-DOWN-SPINNING-AXE",
                        "P1-WRONG-CARD-EQUIPMENT",
                        "P1-WRONG-CONTROLLER-SPINNING-AXE"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-EQUIPMENT-SPINNING-AXE"]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [SentinelObjectId] = UnitCard(SentinelObjectId, SentinelCardNo, ownerId: "P1", controllerId: "P1"),
                [SpinningAxeObjectId] = SpinningAxe(SpinningAxeObjectId, "P1", "P1"),
                [LongSwordObjectId] = LongSword(LongSwordObjectId, "P1", "P1"),
                ["P1-HAND-SPINNING-AXE"] = SpinningAxe("P1-HAND-SPINNING-AXE", "P1", "P1"),
                ["P1-STALE-SPINNING-AXE"] = SpinningAxe("P1-STALE-SPINNING-AXE", "P1", "P1"),
                ["P1-FACE-DOWN-SPINNING-AXE"] = SpinningAxe("P1-FACE-DOWN-SPINNING-AXE", "P1", "P1", isFaceDown: true),
                ["P1-NON-EQUIPMENT-SPINNING-AXE"] = new(
                    "P1-NON-EQUIPMENT-SPINNING-AXE",
                    cardNo: SpinningAxeCardNo,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-WRONG-CARD-EQUIPMENT"] = new(
                    "P1-WRONG-CARD-EQUIPMENT",
                    cardNo: "SFD·190/221",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-WRONG-CONTROLLER-SPINNING-AXE"] = SpinningAxe(
                    "P1-WRONG-CONTROLLER-SPINNING-AXE",
                    "P1",
                    "P2"),
                ["P2-EQUIPMENT-SPINNING-AXE"] = SpinningAxe("P2-EQUIPMENT-SPINNING-AXE", "P2", "P2")
            });
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
            sourceObjectId = command.SourceObjectId,
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
        ActionPromptDto prompt,
        IReadOnlyList<string> optionalCosts)
    {
        Assert.Equal(CommandTypes.PlayCard, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(SentinelObjectId, rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal(SentinelCardNo, rawCommand.GetProperty("cardNo").GetString());
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Equal(
            optionalCosts,
            rawCommand.GetProperty("optionalCosts")
                .EnumerateArray()
                .Select(choice => choice.GetString()!)
                .ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.True(prompt.SnapshotTick.HasValue);
        Assert.Equal(prompt.SnapshotTick.Value, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static StackItemState AssertSentinelStackPriorityState(
        ResolutionResult result,
        IReadOnlyList<string> optionalCosts,
        StackItemState? expectedStackItem = null)
    {
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-HAND-SPINNING-AXE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            [
                SpinningAxeObjectId,
                LongSwordObjectId,
                "P1-NON-EQUIPMENT-SPINNING-AXE",
                "P1-FACE-DOWN-SPINNING-AXE",
                "P1-WRONG-CARD-EQUIPMENT",
                "P1-WRONG-CONTROLLER-SPINNING-AXE"
            ],
            result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1", stackItem.ControllerId);
        Assert.Equal(SentinelObjectId, stackItem.SourceObjectId);
        Assert.Equal(SentinelCardNo, stackItem.CardNo);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Equal(optionalCosts, stackItem.OptionalCosts);
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

        Assert.Equal("P1", result.Prompts["P1"].PlayerId);
        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(PromptTypes.StackPriority, result.Prompts["P1"].View?.Type);
        Assert.Equal(stackItem.StackItemId, result.Prompts["P1"].View?.RelatedStackItemId);
        Assert.Contains(CommandTypes.PassPriority, result.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.PlayCard, result.Prompts["P1"].Actions);
        Assert.Equal(result.State.Tick, result.Prompts["P1"].SnapshotTick);
        Assert.Equal("P2", result.Prompts["P2"].PlayerId);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.PlayCard, result.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, result.Prompts["P2"].Actions);
        Assert.Equal(result.State.Tick, result.Prompts["P2"].SnapshotTick);

        return stackItem;
    }

    private static CardObjectState UnitCard(
        string objectId,
        string cardNo,
        string ownerId,
        string controllerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            tags: [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState SpinningAxe(
        string objectId,
        string ownerId,
        string controllerId,
        bool isFaceDown = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: SpinningAxeCardNo,
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon, CardEquipmentKeywordNames.Agile],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState LongSword(
        string objectId,
        string ownerId,
        string controllerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: LongSwordCardNo,
            tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon, CardEquipmentKeywordNames.Agile],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static string TemperedAttachCost(string equipmentObjectId)
    {
        return $"TEMPERED_ATTACH:{equipmentObjectId}";
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
