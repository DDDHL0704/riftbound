using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HuntReadyGuardTests
{
    private const string MaduliObjectId = "P1-GATEKEEPER-MADULI";

    [Fact]
    public async Task HuntReadiesOnlyFriendlyPublicFieldUnits()
    {
        var engine = new CoreRuleEngine();
        var state = BuildHuntState();

        var played = await PlayHuntAsync(engine, state, []);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(new RunePool(0, 0), played.State.RunePools["P1"]);
        Assert.Single(played.State.StackItems);
        Assert.Contains(played.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "HUNT_READY_ALL_FRIENDLY_UNITS", StringComparison.Ordinal));

        var p1Pass = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-hunt-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hunt-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal(["P1-SPELL-HUNT"], p2Pass.State.PlayerZones["P1"].Graveyard);

        Assert.False(p2Pass.State.CardObjects["P1-BASE-UNIT"].IsExhausted);
        Assert.False(p2Pass.State.CardObjects["P1-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.True(p2Pass.State.CardObjects[MaduliObjectId].IsExhausted);
        Assert.True(p2Pass.State.CardObjects["P1-BATTLEFIELD-EQUIPMENT"].IsExhausted);
        Assert.True(p2Pass.State.CardObjects["P1-BATTLEFIELD-SPELL"].IsExhausted);
        Assert.True(p2Pass.State.CardObjects["P1-BATTLEFIELD-RUNE"].IsExhausted);
        Assert.True(p2Pass.State.CardObjects["P1-FACE-DOWN-STANDBY"].IsExhausted);
        Assert.True(p2Pass.State.CardObjects["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.True(p2Pass.State.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);

        var readiedTargetIds = p2Pass.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal))
            .Select(gameEvent => gameEvent.Payload["targetObjectId"] as string)
            .ToArray();
        Assert.Equal(2, readiedTargetIds.Length);
        Assert.Contains("P1-BASE-UNIT", readiedTargetIds);
        Assert.Contains("P1-BATTLEFIELD-UNIT", readiedTargetIds);
        Assert.DoesNotContain(MaduliObjectId, readiedTargetIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-EQUIPMENT", readiedTargetIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-SPELL", readiedTargetIds);
        Assert.DoesNotContain("P1-BATTLEFIELD-RUNE", readiedTargetIds);
        Assert.DoesNotContain("P1-FACE-DOWN-STANDBY", readiedTargetIds);
        Assert.DoesNotContain("P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT", readiedTargetIds);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT", readiedTargetIds);
    }

    [Fact]
    public void HuntMainActionPlayCardPromptExposesNoTargetChoices()
    {
        const string huntObjectId = "P1-SPELL-HUNT";
        var state = BuildHuntState();
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
                && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, huntObjectId, StringComparison.Ordinal)));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, huntObjectId, StringComparison.Ordinal));

        var targetIds = (playCandidate.Targets ?? []).Select(target => target.Id).ToArray();
        Assert.Empty(targetIds);
        foreach (var fixtureObjectId in new[]
        {
            "P1-BASE-UNIT",
            "P1-BATTLEFIELD-UNIT",
            "P2-BATTLEFIELD-UNIT",
            "P1-FACE-DOWN-STANDBY",
            "P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT",
            MaduliObjectId,
            huntObjectId
        })
        {
            Assert.DoesNotContain(fixtureObjectId, targetIds);
        }

        if (playCandidate.Metadata is null)
        {
            return;
        }

        if (playCandidate.Metadata.TryGetValue("targetChoicesByIndex", out var candidateTargetChoicesPayload)
            && candidateTargetChoicesPayload is not null)
        {
            AssertEmptyTargetChoicesByIndex(candidateTargetChoicesPayload);
        }

        if (!playCandidate.Metadata.TryGetValue("sourceRequirements", out var sourceRequirementsPayload)
            || sourceRequirementsPayload is null)
        {
            return;
        }

        var huntSourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                sourceRequirementsPayload)
            .Where(requirement =>
                requirement.TryGetValue("sourceObjectId", out var sourceObjectId)
                && string.Equals(sourceObjectId as string, huntObjectId, StringComparison.Ordinal))
            .ToArray();
        foreach (var sourceRequirement in huntSourceRequirements)
        {
            if (!sourceRequirement.TryGetValue("targetChoicesByIndex", out var targetChoicesPayload)
                || targetChoicesPayload is null)
            {
                continue;
            }

            AssertEmptyTargetChoicesByIndex(targetChoicesPayload);
        }
    }

    [Fact]
    public async Task HuntPlayCardStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildHuntState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var command = new PlayCardCommand(
            "P1-SPELL-HUNT",
            "SFD·204/221",
            []);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, command.SourceObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-hunt-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-hunt-stale-prompt-replay";

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
            && string.Equals(gameEvent.Payload["effectKind"] as string, "HUNT_READY_ALL_FRIENDLY_UNITS", StringComparison.Ordinal));
        var acceptedStackItem = AssertHuntStackPriorityState(accepted);
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
        AssertHuntStackPriorityState(replay, acceptedStackItem);
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
        AssertHuntStackPriorityState(duplicateReplay, acceptedStackItem);
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
        AssertHuntStackPriorityState(conflict, acceptedStackItem);
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
    [InlineData("P1-BASE-UNIT")]
    [InlineData(MaduliObjectId)]
    [InlineData("P1-BATTLEFIELD-UNIT")]
    [InlineData("P2-BATTLEFIELD-UNIT")]
    [InlineData("P1-BATTLEFIELD-EQUIPMENT")]
    [InlineData("P1-BATTLEFIELD-SPELL")]
    [InlineData("P1-BATTLEFIELD-RUNE")]
    [InlineData("P1-FACE-DOWN-STANDBY")]
    [InlineData("P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT")]
    public async Task HuntRejectsExplicitTargetsWithoutMutation(string targetObjectId)
    {
        var state = BuildHuntState();

        var result = await PlayHuntAsync(new CoreRuleEngine(), state, [targetObjectId]);

        AssertRejectedWithoutMutation(state, result);
    }

    private static async Task<ResolutionResult> PlayHuntAsync(
        CoreRuleEngine engine,
        MatchState state,
        IReadOnlyList<string> targetObjectIds)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-hunt-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                "P1-SPELL-HUNT",
                "SFD·204/221",
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

    private static StackItemState AssertHuntStackPriorityState(
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
        Assert.Equal(["P1-BASE-UNIT", MaduliObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(
            [
                "P1-BATTLEFIELD-UNIT",
                "P1-BATTLEFIELD-EQUIPMENT",
                "P1-BATTLEFIELD-SPELL",
                "P1-BATTLEFIELD-RUNE",
                "P1-FACE-DOWN-STANDBY",
                "P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"
            ],
            result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-UNIT"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Null(result.State.PendingPayment);
        Assert.True(result.State.CardObjects["P1-BASE-UNIT"].IsExhausted);
        Assert.True(result.State.CardObjects["P1-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.True(result.State.CardObjects[MaduliObjectId].IsExhausted);
        Assert.True(result.State.CardObjects["P1-BATTLEFIELD-EQUIPMENT"].IsExhausted);
        Assert.True(result.State.CardObjects["P1-BATTLEFIELD-SPELL"].IsExhausted);
        Assert.True(result.State.CardObjects["P1-BATTLEFIELD-RUNE"].IsExhausted);
        Assert.True(result.State.CardObjects["P1-FACE-DOWN-STANDBY"].IsExhausted);
        Assert.True(result.State.CardObjects["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"].IsExhausted);
        Assert.True(result.State.CardObjects["P2-BATTLEFIELD-UNIT"].IsExhausted);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1-SPELL-HUNT", stackItem.SourceObjectId);
        Assert.Equal("SFD·204/221", stackItem.CardNo);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Empty(stackItem.OptionalCosts);
        Assert.Equal("HUNT_READY_ALL_FRIENDLY_UNITS", stackItem.EffectKind);
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
        }

        return stackItem;
    }

    private static void AssertEmptyTargetChoicesByIndex(object targetChoicesPayload)
    {
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(targetChoicesPayload);
        Assert.All(targetChoicesByIndex.Values, choices =>
        {
            if (choices is null)
            {
                return;
            }

            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choices));
        });
    }

    private static void AssertRejectedWithoutMutation(MatchState initialState, ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(MatchStateHasher.Hash(initialState), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HUNT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal));
    }

    private static MatchState BuildHuntState()
    {
        return new MatchState(
            roomId: "hunt-ready-guard-test",
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
                ["P1"] = new(1, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HUNT"],
                    Base = ["P1-BASE-UNIT", MaduliObjectId],
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-UNIT",
                        "P1-BATTLEFIELD-EQUIPMENT",
                        "P1-BATTLEFIELD-SPELL",
                        "P1-BATTLEFIELD-RUNE",
                        "P1-FACE-DOWN-STANDBY",
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
                ["P1-SPELL-HUNT"] = Hunt(),
                ["P1-BASE-UNIT"] = Unit("P1-BASE-UNIT"),
                [MaduliObjectId] = Unit(MaduliObjectId, cardNo: P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo),
                ["P1-BATTLEFIELD-UNIT"] = Unit("P1-BATTLEFIELD-UNIT"),
                ["P1-BATTLEFIELD-EQUIPMENT"] = NonUnit("P1-BATTLEFIELD-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard, "P1"),
                ["P1-BATTLEFIELD-SPELL"] = NonUnit("P1-BATTLEFIELD-SPELL", "OGN·169/298", CardObjectTags.SpellCard, "P1"),
                ["P1-BATTLEFIELD-RUNE"] = NonUnit("P1-BATTLEFIELD-RUNE", "RUNES·001", CardObjectTags.RuneCard, "P1"),
                ["P1-FACE-DOWN-STANDBY"] = Unit(
                    "P1-FACE-DOWN-STANDBY",
                    cardNo: null,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
                ["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"] = Unit(
                    "P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-UNIT"] = Unit("P2-BATTLEFIELD-UNIT", ownerId: "P2", controllerId: "P2")
            });
    }

    private static CardObjectState Hunt()
    {
        return new CardObjectState(
            "P1-SPELL-HUNT",
            cardNo: "SFD·204/221",
            manaCost: 1,
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
            isExhausted: true,
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
