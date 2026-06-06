using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ReksaiHasteReadyRedPaymentTests
{
    private const string ReksaiCardNo = "SFD·029/221";
    private const string ReksaiAltACardNo = "SFD·029a/221";
    private const string ReksaiObjectId = "P1-REKSAI";
    private const string RedRuneObjectId = "P1-RUNE-RED";
    private const string GreenRuneObjectId = "P1-RUNE-GREEN";
    private const string RuneDeckObjectId = "P1-RUNE-BOTTOM";

    [Theory]
    [InlineData(ReksaiCardNo)]
    [InlineData(ReksaiAltACardNo)]
    public void ProfileExposesHasteReadyExtraManaAndTypedRedPowerForBothPrintings(string cardNo)
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition));

        var profile = CardPermissionKeywordRules.BuildProfile(definition);

        Assert.True(profile.HasHaste);
        Assert.Equal(HasteOptionalReadyBranchStatuses.ImplementedRepresentative, profile.HasteOptionalReadyBranchStatus);
        Assert.Equal(1, profile.HasteReadyManaCost);
        Assert.Equal(1, profile.HasteReadyPowerCost);
        Assert.Equal(RuneTrait.Red, profile.HasteReadyPowerTrait);
    }

    [Theory]
    [InlineData(ReksaiCardNo)]
    [InlineData(ReksaiAltACardNo)]
    public void PromptExposesHasteReadyRedCostAndOnlyLegalRedRecyclePaymentResource(string cardNo)
    {
        var state = BuildReksaiState(
            cardNo,
            new RunePool(4, 0),
            baseObjectIds: [RedRuneObjectId, GreenRuneObjectId],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [RedRuneObjectId] = RuneCard(RedRuneObjectId, RuneTrait.Red),
                [GreenRuneObjectId] = RuneCard(GreenRuneObjectId, RuneTrait.Green)
            });

        var sourceRequirement = PlayCardSourceRequirement(state, cardNo);

        Assert.Equal(ReksaiObjectId, sourceRequirement["sourceObjectId"]);
        Assert.Equal(cardNo, sourceRequirement["cardNo"]);
        Assert.Equal(3, sourceRequirement["manaCost"]);
        Assert.Equal(3, sourceRequirement["minimumManaCost"]);
        Assert.Equal(0, sourceRequirement["minTargetCount"]);
        Assert.Equal(0, sourceRequirement["maxTargetCount"]);
        Assert.Equal(1, sourceRequirement["hasteReadyPowerCost"]);
        Assert.Equal(RuneTrait.Red, sourceRequirement["hasteReadyPowerTrait"]);

        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .ToArray();
        Assert.Single(optionalCostChoices, choice => string.Equals(choice.Id, HasteOptionalCostNames.HasteReady, StringComparison.Ordinal));

        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"])
            .ToArray();
        Assert.Equal([$"RECYCLE_RUNE:{RedRuneObjectId}"], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        Assert.DoesNotContain(
            paymentResourceChoices,
            choice => choice.Id.StartsWith(PaymentCostRules.TemporaryPaymentResourceActionPrefix, StringComparison.Ordinal));

        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);
        Assert.Equal(RuneTrait.Red, paymentResourcePowerByChoice[$"RECYCLE_RUNE:{RedRuneObjectId}"]["trait"]);

        var availablePowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            sourceRequirement["availablePowerByTrait"]);
        Assert.Empty(availablePowerByTrait);
        var availablePowerByTraitWithPaymentResources = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            sourceRequirement["availablePowerByTraitWithPaymentResources"]);
        Assert.Equal(1, availablePowerByTraitWithPaymentResources[RuneTrait.Red]);
    }

    [Fact]
    public void PromptDoesNotOfferHasteReadyOrGreenRecycleForWrongTraitShortfall()
    {
        var state = BuildReksaiState(
            ReksaiCardNo,
            new RunePool(4, 0),
            baseObjectIds: [GreenRuneObjectId],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [GreenRuneObjectId] = RuneCard(GreenRuneObjectId, RuneTrait.Green)
            });

        var sourceRequirement = PlayCardSourceRequirement(state, ReksaiCardNo);

        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .ToArray();
        Assert.DoesNotContain(optionalCostChoices, choice => string.Equals(choice.Id, HasteOptionalCostNames.HasteReady, StringComparison.Ordinal));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"])
            .ToArray();
        Assert.Empty(paymentResourceChoices);
    }

    [Theory]
    [InlineData(ReksaiCardNo)]
    [InlineData(ReksaiAltACardNo)]
    public async Task ExistingRedPowerPaysHasteReadyAndResolvesActiveToBaseForBothPrintings(string cardNo)
    {
        var engine = new CoreRuleEngine();
        var played = await PlayReksaiAsync(
            BuildReksaiState(
                cardNo,
                new RunePool(4, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                })),
            cardNo,
            engine: engine);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], played.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Empty(played.State.RunePools["P1"].PowerByTrait);
        AssertCostPaid(played, expectedPaymentResourceActions: []);

        var resolved = await ResolveTopOfStackAsync(played.State, engine);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Empty(resolved.State.PlayerZones["P1"].Hand);
        Assert.Contains(ReksaiObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Equal("BASE", resolved.State.ObjectLocations[ReksaiObjectId].Zone);
        Assert.False(resolved.State.CardObjects[ReksaiObjectId].IsExhausted);
        var unitEvent = Assert.Single(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
        Assert.Equal(false, unitEvent.Payload["isExhausted"]);
        Assert.Equal(true, unitEvent.Payload["hasteReadyOptionalCostPaid"]);
    }

    [Fact]
    public async Task RecycleRedRunePaysRequiredTypedRedPowerAndAuditsResourceAction()
    {
        var paymentResourceAction = $"RECYCLE_RUNE:{RedRuneObjectId}";
        var state = BuildReksaiState(
            ReksaiCardNo,
            new RunePool(4, 0),
            baseObjectIds: [RedRuneObjectId],
            runeDeckObjectIds: [RuneDeckObjectId],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [RedRuneObjectId] = RuneCard(RedRuneObjectId, RuneTrait.Red),
                [RuneDeckObjectId] = RuneCard(RuneDeckObjectId, RuneTrait.Green)
            });

        var played = await PlayReksaiAsync(state, ReksaiCardNo, optionalCosts: [HasteOptionalCostNames.HasteReady, paymentResourceAction]);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(["CARD_PLAYED", "RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "STACK_ITEM_ADDED"], played.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.DoesNotContain(RedRuneObjectId, played.State.PlayerZones["P1"].Base);
        Assert.Equal([RuneDeckObjectId, RedRuneObjectId], played.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal("RUNE_DECK", played.State.ObjectLocations[RedRuneObjectId].Zone);
        Assert.Empty(played.State.RunePools["P1"].PowerByTrait);

        var recycleEvent = Assert.Single(played.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        var powerGainedEvent = Assert.Single(played.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        var costEvent = AssertCostPaid(played, expectedPaymentResourceActions: [paymentResourceAction]);
        Assert.Equal(costEvent.Payload["paymentId"], recycleEvent.Payload["paymentId"]);
        Assert.Equal(costEvent.Payload["paymentId"], powerGainedEvent.Payload["paymentId"]);
        Assert.Equal([RedRuneObjectId], Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
    }

    [Fact]
    public async Task ReplayingAcceptedRecycleRedRuneHasteReadyPlayCardRejectsWithoutMutation()
    {
        var paymentResourceAction = $"RECYCLE_RUNE:{RedRuneObjectId}";
        var engine = new CoreRuleEngine();
        var state = BuildReksaiState(
            ReksaiCardNo,
            new RunePool(4, 0),
            baseObjectIds: [RedRuneObjectId],
            runeDeckObjectIds: [RuneDeckObjectId],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [RedRuneObjectId] = RuneCard(RedRuneObjectId, RuneTrait.Red),
                [RuneDeckObjectId] = RuneCard(RuneDeckObjectId, RuneTrait.Green)
            });
        var command = ReksaiCommand(
            ReksaiCardNo,
            optionalCosts: [HasteOptionalCostNames.HasteReady, paymentResourceAction]);

        var played = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-reksai-play-once", "P1", CommandTypes.PlayCard),
            command,
            CancellationToken.None);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(["CARD_PLAYED", "RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "STACK_ITEM_ADDED"], played.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var postAcceptanceHash = MatchStateHasher.Hash(played.State);
        var postAcceptanceHand = played.State.PlayerZones["P1"].Hand.ToArray();
        var postAcceptanceBase = played.State.PlayerZones["P1"].Base.ToArray();
        var postAcceptanceRuneDeck = played.State.PlayerZones["P1"].RuneDeck.ToArray();
        var postAcceptanceStackIds = played.State.StackItems.Select(stackItem => stackItem.SourceObjectId).ToArray();
        var postAcceptanceRunePool = played.State.RunePools["P1"];
        var postAcceptanceLocation = played.State.ObjectLocations[ReksaiObjectId];

        var replay = await engine.ResolveAsync(
            played.State,
            new PlayerIntent("intent-reksai-play-replay", "P1", CommandTypes.PlayCard),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Empty(replay.Events);
        Assert.DoesNotContain(replay.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "CARD_PLAYED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal)
            || string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.Equal(postAcceptanceHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(postAcceptanceHand, replay.State.PlayerZones["P1"].Hand);
        Assert.Equal(postAcceptanceBase, replay.State.PlayerZones["P1"].Base);
        Assert.Equal(postAcceptanceRuneDeck, replay.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(postAcceptanceStackIds, replay.State.StackItems.Select(stackItem => stackItem.SourceObjectId).ToArray());
        Assert.Equal(postAcceptanceRunePool, replay.State.RunePools["P1"]);
        Assert.Equal(postAcceptanceLocation, replay.State.ObjectLocations[ReksaiObjectId]);
        Assert.Single(replay.State.StackItems);
    }

    [Fact]
    public async Task RecycleRedRuneHasteReadyPlayCardStalePromptReplayUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var paymentResourceAction = $"RECYCLE_RUNE:{RedRuneObjectId}";
        var state = BuildReksaiState(
            ReksaiCardNo,
            new RunePool(4, 0),
            baseObjectIds: [RedRuneObjectId],
            runeDeckObjectIds: [RuneDeckObjectId],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [RedRuneObjectId] = RuneCard(RedRuneObjectId, RuneTrait.Red),
                [RuneDeckObjectId] = RuneCard(RuneDeckObjectId, RuneTrait.Green)
            });
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        var optionalCosts = new[] { HasteOptionalCostNames.HasteReady, paymentResourceAction };
        var command = ReksaiCommand(ReksaiCardNo, optionalCosts: optionalCosts);

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, ReksaiObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-reksai-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-reksai-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["CARD_PLAYED", "RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        AssertCostPaid(accepted, expectedPaymentResourceActions: [paymentResourceAction]);
        var acceptedStackItem = AssertReksaiRecycleRedRuneHasteReadyAcceptedState(accepted);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedHand = accepted.State.PlayerZones["P1"].Hand.ToArray();
        var acceptedBase = accepted.State.PlayerZones["P1"].Base.ToArray();
        var acceptedRuneDeck = accepted.State.PlayerZones["P1"].RuneDeck.ToArray();
        var acceptedRunePool = accepted.State.RunePools["P1"];
        var acceptedStack = accepted.State.StackItems.ToArray();
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
        Assert.Equal(acceptedHand, replay.State.PlayerZones["P1"].Hand);
        Assert.Equal(acceptedBase, replay.State.PlayerZones["P1"].Base);
        Assert.Equal(acceptedRuneDeck, replay.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(acceptedRunePool, replay.State.RunePools["P1"]);
        Assert.Equal(acceptedStack, replay.State.StackItems);
        AssertReksaiRecycleRedRuneHasteReadyAcceptedState(replay, acceptedStackItem);
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
        Assert.Equal(acceptedHand, duplicateRejected.State.PlayerZones["P1"].Hand);
        Assert.Equal(acceptedBase, duplicateRejected.State.PlayerZones["P1"].Base);
        Assert.Equal(acceptedRuneDeck, duplicateRejected.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(acceptedRunePool, duplicateRejected.State.RunePools["P1"]);
        Assert.Equal(acceptedStack, duplicateRejected.State.StackItems);
        AssertReksaiRecycleRedRuneHasteReadyAcceptedState(duplicateRejected, acceptedStackItem);
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
        Assert.Equal(acceptedHand, conflict.State.PlayerZones["P1"].Hand);
        Assert.Equal(acceptedBase, conflict.State.PlayerZones["P1"].Base);
        Assert.Equal(acceptedRuneDeck, conflict.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(acceptedRunePool, conflict.State.RunePools["P1"]);
        Assert.Equal(acceptedStack, conflict.State.StackItems);
        AssertReksaiRecycleRedRuneHasteReadyAcceptedState(conflict, acceptedStackItem);
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
    [InlineData("wrong-trait-power")]
    [InlineData("generic-temporary-resource")]
    [InlineData("insufficient-red")]
    [InlineData("wrong-trait-recycle")]
    [InlineData("duplicate-recycle")]
    [InlineData("invalid-recycle")]
    [InlineData("unnecessary-recycle")]
    [InlineData("unsupported-optional-cost")]
    [InlineData("submitted-target")]
    public async Task InvalidHasteReadyPaymentCommandsRejectWithoutMutation(string scenario)
    {
        var state = InvalidScenarioState(scenario);
        var command = scenario switch
        {
            "generic-temporary-resource" => ReksaiCommand(
                ReksaiCardNo,
                optionalCosts:
                [
                    HasteOptionalCostNames.HasteReady,
                    PaymentCostRules.TemporaryPaymentResourceActionId("MALZAHAR:TEMP-REKSAI")
                ]),
            "wrong-trait-recycle" => ReksaiCommand(
                ReksaiCardNo,
                optionalCosts: [HasteOptionalCostNames.HasteReady, $"RECYCLE_RUNE:{GreenRuneObjectId}"]),
            "duplicate-recycle" => ReksaiCommand(
                ReksaiCardNo,
                optionalCosts: [HasteOptionalCostNames.HasteReady, $"RECYCLE_RUNE:{RedRuneObjectId}", $"RECYCLE_RUNE:{RedRuneObjectId}"]),
            "invalid-recycle" => ReksaiCommand(
                ReksaiCardNo,
                optionalCosts: [HasteOptionalCostNames.HasteReady, "RECYCLE_RUNE:P1-RUNE-MISSING"]),
            "unnecessary-recycle" => ReksaiCommand(
                ReksaiCardNo,
                optionalCosts: [HasteOptionalCostNames.HasteReady, $"RECYCLE_RUNE:{RedRuneObjectId}"]),
            "unsupported-optional-cost" => ReksaiCommand(
                ReksaiCardNo,
                optionalCosts: ["UNSUPPORTED_OPTIONAL_COST"]),
            "submitted-target" => ReksaiCommand(
                ReksaiCardNo,
                targetObjectIds: [RedRuneObjectId],
                optionalCosts: [HasteOptionalCostNames.HasteReady]),
            _ => ReksaiCommand(ReksaiCardNo)
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    private static GameEvent AssertCostPaid(
        ResolutionResult result,
        IReadOnlyList<string> expectedPaymentResourceActions)
    {
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.StartsWith("PLAY_CARD:", Assert.IsType<string>(costEvent.Payload["paymentId"]), StringComparison.Ordinal);
        Assert.Equal("PLAY_CARD", costEvent.Payload["paymentWindow"]);
        Assert.Equal(ReksaiObjectId, costEvent.Payload["sourceObjectId"]);
        Assert.Equal(3, costEvent.Payload["baseManaCost"]);
        Assert.Equal(4, costEvent.Payload["totalManaCost"]);
        Assert.Equal(0, costEvent.Payload["genericPower"]);
        Assert.Equal(1, costEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(1, powerByTrait[RuneTrait.Red]);
        Assert.Equal([HasteOptionalCostNames.HasteReady], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Equal(expectedPaymentResourceActions.ToArray(), Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        return costEvent;
    }

    private static IReadOnlyDictionary<string, object?> PlayCardSourceRequirement(MatchState state, string cardNo)
    {
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        return Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            requirement => string.Equals(requirement["cardNo"] as string, cardNo, StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> PlayReksaiAsync(
        MatchState state,
        string cardNo,
        IReadOnlyList<string>? optionalCosts = null,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-reksai-play", "P1", CommandTypes.PlayCard),
            ReksaiCommand(cardNo, optionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static PlayCardCommand ReksaiCommand(
        string cardNo,
        IReadOnlyList<string>? targetObjectIds = null,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new PlayCardCommand(
            ReksaiObjectId,
            cardNo,
            targetObjectIds ?? [],
            OptionalCosts: optionalCosts ?? [HasteOptionalCostNames.HasteReady]);
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
        ActionPromptDto prompt,
        IReadOnlyList<string> optionalCosts)
    {
        Assert.Equal(CommandTypes.PlayCard, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(ReksaiObjectId, rawCommand.GetProperty("cardObjectId").GetString());
        Assert.Equal(ReksaiCardNo, rawCommand.GetProperty("cardNo").GetString());
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

    private static StackItemState AssertReksaiRecycleRedRuneHasteReadyAcceptedState(
        ResolutionResult result,
        StackItemState? expectedStackItem = null)
    {
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(RedRuneObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal([RuneDeckObjectId, RedRuneObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal("RUNE_DECK", result.State.ObjectLocations[RedRuneObjectId].Zone);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(ReksaiObjectId, stackItem.SourceObjectId);
        Assert.Equal(ReksaiCardNo, stackItem.CardNo);
        Assert.Empty(stackItem.TargetObjectIds);
        if (expectedStackItem is not null)
        {
            Assert.Equal(expectedStackItem.StackItemId, stackItem.StackItemId);
            Assert.Equal(expectedStackItem.ControllerId, stackItem.ControllerId);
            Assert.Equal(expectedStackItem.SourceObjectId, stackItem.SourceObjectId);
            Assert.Equal(expectedStackItem.CardNo, stackItem.CardNo);
            Assert.Equal(expectedStackItem.TargetObjectIds, stackItem.TargetObjectIds);
            Assert.Equal(expectedStackItem.OptionalCosts, stackItem.OptionalCosts);
            Assert.Equal(expectedStackItem.Destination, stackItem.Destination);
            Assert.Equal(expectedStackItem.TimingContext, stackItem.TimingContext);
        }

        return stackItem;
    }

    private static async Task<ResolutionResult> ResolveTopOfStackAsync(
        MatchState state,
        CoreRuleEngine engine)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-reksai-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-reksai-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static async Task AssertRejectedNoMutationAsync(MatchState state, PlayCardCommand command)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-reksai-invalid", "P1", CommandTypes.PlayCard),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    private static MatchState InvalidScenarioState(string scenario)
    {
        return scenario switch
        {
            "wrong-trait-power" => BuildReksaiState(
                ReksaiCardNo,
                new RunePool(4, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Green] = 1
                })),
            "generic-temporary-resource" => BuildReksaiState(ReksaiCardNo, new RunePool(4, 0)) with
            {
                TemporaryPaymentResources =
                [
                    new TemporaryPaymentResourceState(
                        "MALZAHAR:TEMP-REKSAI",
                        "P1",
                        "P1-MALZAHAR",
                        "TEST_GENERIC_TEMP",
                        "PLAY_CARD",
                        generatedPower: 1,
                        remainingPower: 1,
                        allowedPaymentKinds: [PaymentCostRules.RuneCostPaymentKind])
                ]
            },
            "wrong-trait-recycle" => BuildReksaiState(
                ReksaiCardNo,
                new RunePool(4, 0),
                baseObjectIds: [GreenRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [GreenRuneObjectId] = RuneCard(GreenRuneObjectId, RuneTrait.Green)
                }),
            "duplicate-recycle" or "invalid-recycle" => BuildReksaiState(
                ReksaiCardNo,
                new RunePool(4, 0),
                baseObjectIds: [RedRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [RedRuneObjectId] = RuneCard(RedRuneObjectId, RuneTrait.Red)
                }),
            "unnecessary-recycle" => BuildReksaiState(
                ReksaiCardNo,
                new RunePool(4, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
                baseObjectIds: [RedRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [RedRuneObjectId] = RuneCard(RedRuneObjectId, RuneTrait.Red)
                }),
            "submitted-target" => BuildReksaiState(
                ReksaiCardNo,
                new RunePool(4, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
                baseObjectIds: [RedRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [RedRuneObjectId] = RuneCard(RedRuneObjectId, RuneTrait.Red)
                }),
            _ => BuildReksaiState(ReksaiCardNo, new RunePool(4, 0))
        };
    }

    private static MatchState BuildReksaiState(
        string cardNo,
        RunePool p1RunePool,
        IReadOnlyList<string>? baseObjectIds = null,
        IReadOnlyList<string>? runeDeckObjectIds = null,
        IReadOnlyDictionary<string, CardObjectState>? extraCardObjects = null)
    {
        var p1Base = baseObjectIds ?? [];
        var p1RuneDeck = runeDeckObjectIds ?? [];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [ReksaiObjectId] = new(
                ReksaiObjectId,
                cardNo: cardNo,
                ownerId: "P1",
                controllerId: "P1")
        };
        foreach (var entry in extraCardObjects ?? new Dictionary<string, CardObjectState>(StringComparer.Ordinal))
        {
            cardObjects[entry.Key] = entry.Value;
        }

        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [ReksaiObjectId] = new("P1", "HAND")
        };
        foreach (var objectId in p1Base)
        {
            objectLocations[objectId] = new ObjectLocationState("P1", "BASE");
        }

        foreach (var objectId in p1RuneDeck)
        {
            objectLocations[objectId] = new ObjectLocationState("P1", "RUNE_DECK");
        }

        return new MatchState(
            "reksai-haste-ready-red",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "s1",
                ["P2"] = "s2"
            }) with
        {
            TurnPlayerId = "P1",
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = p1RunePool,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [ReksaiObjectId],
                    Base = p1Base.ToArray(),
                    RuneDeck = p1RuneDeck.ToArray()
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };
    }

    private static CardObjectState RuneCard(string objectId, string trait)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.RuneCard, $"COLOR:{trait}"],
            cardNo: $"TEST-RUNE-{trait.ToUpperInvariant()}",
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
