using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class JaxTemperedOptionalAttachTests
{
    private const string JaxObjectId = "P1-UNIT-JAX";
    private const string JaxCardNo = "SFD·119/221";
    private const string JaxAltCardNo = "SFD·119a/221";
    private const string SpinningAxeObjectId = "P1-EQUIPMENT-SPINNING-AXE";
    private const string SpinningAxeCardNo = "SFD·186/221";
    private const string JaxTrigger = "JAX_WEAPON_ATTACH_PAY_1_DRAW_1";
    private const string TriggerPaymentWindow = "TRIGGER_PAYMENT";
    private const string PayOneMana = "SPEND_MANA:1";
    private const string Decline = "DECLINE";

    [Theory]
    [InlineData(JaxCardNo)]
    [InlineData(JaxAltCardNo)]
    public void PromptExposesLegalTemperedAttachChoiceForJax(string jaxCardNo)
    {
        var state = BuildTemperedJaxState(jaxCardNo: jaxCardNo);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var requirement = Assert.Single(
            sourceRequirements,
            entry => string.Equals(entry["sourceObjectId"] as string, JaxObjectId, StringComparison.Ordinal));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["optionalCostChoices"]);

        Assert.Equal(
            [TemperedAttachCost(SpinningAxeObjectId)],
            optionalCostChoices.Select(choice => choice.Id).ToArray());
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            requirement["targetChoicesByIndex"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["minTargetCount"]));
        Assert.Equal(0, Assert.IsType<int>(requirement["maxTargetCount"]));
    }

    [Theory]
    [InlineData(JaxCardNo)]
    [InlineData(JaxAltCardNo)]
    public async Task LegalTemperedOptionalAttachOpensJaxWeaponPaymentAfterBothPlayersPass(string jaxCardNo)
    {
        var engine = new CoreRuleEngine();
        var state = BuildTemperedJaxState(jaxCardNo: jaxCardNo);
        var optionalCosts = new[] { TemperedAttachCost(SpinningAxeObjectId) };

        var played = await PlayJaxAsync(engine, state, jaxCardNo, optionalCosts);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.RunePools["P1"].Mana);
        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.Equal(optionalCosts, stackItem.OptionalCosts);
        Assert.DoesNotContain(JaxObjectId, played.State.PlayerZones["P1"].Hand);

        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        Assert.Contains(JaxObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Equal(JaxObjectId, resolved.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.Contains(CardEquipmentKeywordNames.Tempered, resolved.State.CardObjects[JaxObjectId].Tags);
        AssertTemperedAttachEvent(resolved, jaxCardNo, optionalCosts);
        AssertJaxPaymentOpen(resolved);
    }

    [Fact]
    public async Task JaxTemperedOptionalAttachPlayCardStalePromptReplayUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildTemperedJaxState();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");
        IReadOnlyList<string> optionalCosts = [TemperedAttachCost(SpinningAxeObjectId)];
        var command = new PlayCardCommand(
            JaxObjectId,
            JaxCardNo,
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
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, JaxObjectId, StringComparison.Ordinal));
        var staleRawCommand = PromptScopedPlayCardRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPlayCardRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-jax-tempered-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-jax-tempered-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        var acceptedStackItem = AssertJaxStackPriorityState(accepted, optionalCosts);
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
        AssertJaxStackPriorityState(replay, optionalCosts, acceptedStackItem);
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
        AssertJaxStackPriorityState(duplicateRejected, optionalCosts, acceptedStackItem);
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
        AssertJaxStackPriorityState(conflict, optionalCosts, acceptedStackItem);
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
    public async Task JaxTemperedWeaponAttachPaymentAcceptedDrawsOneAndClosesWindow()
    {
        var engine = new CoreRuleEngine();
        var opened = await OpenJaxTemperedPaymentAsync(engine);
        var payment = AssertJaxPaymentOpen(opened);

        var paid = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-jax-tempered-pay", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        Assert.Equal(["P1-HAND-SPINNING-AXE", "P1-JAX-DRAW-001"], paid.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-JAX-DRAW-002"], paid.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(JaxObjectId, paid.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Contains(paid.Events, IsJaxTriggerResolved);
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JaxTemperedWeaponAttachPaymentPromptIsIsolatedToController()
    {
        var opened = await OpenJaxTemperedPaymentAsync(new CoreRuleEngine());
        AssertJaxPaymentOpen(opened);

        var p1Prompt = opened.Prompts["P1"];
        Assert.True(p1Prompt.Actionable);
        Assert.Equal(PromptTypes.PayCost, p1Prompt.View?.Type);
        Assert.Contains(CommandTypes.PayCost, p1Prompt.Actions);
        Assert.DoesNotContain(CommandTypes.PlayCard, p1Prompt.Actions);

        var p1PayCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(p1PayCandidate.Metadata);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentChoices"]);
        Assert.Equal([PayOneMana, Decline], choices.Select(choice => choice.Id).ToArray());
        Assert.False(metadata.ContainsKey("sourceRequirements"));
        Assert.False(metadata.ContainsKey("optionalCostChoices"));

        var p2Prompt = opened.Prompts["P2"];
        Assert.False(p2Prompt.Actionable);
        Assert.Equal(opened.State.Tick, p2Prompt.SnapshotTick);
        Assert.DoesNotContain(CommandTypes.PayCost, p2Prompt.Actions);
        Assert.DoesNotContain(CommandTypes.PlayCard, p2Prompt.Actions);
        Assert.DoesNotContain(
            p2Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
    }

    [Fact]
    public async Task JaxTemperedWeaponAttachPaymentDeclineClosesWithoutDraw()
    {
        var engine = new CoreRuleEngine();
        var opened = await OpenJaxTemperedPaymentAsync(engine);
        var payment = AssertJaxPaymentOpen(opened);

        var declined = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-jax-tempered-decline", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [Decline]),
            CancellationToken.None);

        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Null(declined.State.PendingPayment);
        Assert.Equal(1, declined.State.RunePools["P1"].Mana);
        Assert.Equal(["P1-HAND-SPINNING-AXE"], declined.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-JAX-DRAW-001", "P1-JAX-DRAW-002"], declined.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(JaxObjectId, declined.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_PAYMENT_DECLINED", StringComparison.Ordinal));
        Assert.Contains(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.DoesNotContain(declined.Events, IsJaxTriggerResolved);
        Assert.DoesNotContain(declined.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JaxTemperedWeaponAttachInsufficientPaymentRejectsAndKeepsWindow()
    {
        var engine = new CoreRuleEngine();
        var opened = await OpenJaxTemperedPaymentAsync(engine, mana: 4);
        var payment = AssertJaxPaymentOpen(opened);

        var insufficient = await engine.ResolveAsync(
            opened.State,
            new PlayerIntent("intent-jax-tempered-insufficient", "P1", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [PayOneMana]),
            CancellationToken.None);

        Assert.False(insufficient.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, insufficient.ErrorCode);
        Assert.Empty(insufficient.Events);
        Assert.NotNull(insufficient.State.PendingPayment);
        Assert.Equal(["P1-HAND-SPINNING-AXE"], insufficient.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-JAX-DRAW-001", "P1-JAX-DRAW-002"], insufficient.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(JaxObjectId, insufficient.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
    }

    [Fact]
    public async Task NoTemperedOptionalAttachStillPlaysJaxWithoutAttachmentOrPayment()
    {
        var engine = new CoreRuleEngine();
        var state = BuildTemperedJaxState();

        var played = await PlayJaxAsync(engine, state, JaxCardNo);
        var resolved = await ResolveTopStackAsync(engine, played.State);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(JaxObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Null(resolved.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.Null(resolved.State.PendingPayment);
        Assert.DoesNotContain(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.DoesNotContain(resolved.Events, IsJaxPaymentOpened);
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
        var state = BuildTemperedJaxState();

        var result = await PlayJaxAsync(
            new CoreRuleEngine(),
            state,
            JaxCardNo,
            [TemperedAttachCost(equipmentObjectId)]);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(5, result.State.RunePools["P1"].Mana);
        Assert.Contains(JaxObjectId, result.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(JaxObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.Null(result.State.PendingPayment);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task ResolutionStaleTemperedAttachChoicePlaysJaxWithoutAttachOrPayment()
    {
        var engine = new CoreRuleEngine();
        var state = BuildTemperedJaxState();
        var played = await PlayJaxAsync(
            engine,
            state,
            JaxCardNo,
            [TemperedAttachCost(SpinningAxeObjectId)]);
        var staleState = MoveSpinningAxeToGraveyard(played.State);

        var resolved = await ResolveTopStackAsync(engine, staleState);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Contains(JaxObjectId, resolved.State.PlayerZones["P1"].Base);
        Assert.Contains(SpinningAxeObjectId, resolved.State.PlayerZones["P1"].Graveyard);
        Assert.Null(resolved.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.Null(resolved.State.PendingPayment);
        Assert.DoesNotContain(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.DoesNotContain(resolved.Events, IsJaxPaymentOpened);
    }

    private static async Task<ResolutionResult> OpenJaxTemperedPaymentAsync(
        CoreRuleEngine engine,
        int mana = 5)
    {
        var played = await PlayJaxAsync(
            engine,
            BuildTemperedJaxState(mana: mana),
            JaxCardNo,
            [TemperedAttachCost(SpinningAxeObjectId)]);
        Assert.True(played.Accepted, played.ErrorMessage);

        return await ResolveTopStackAsync(engine, played.State);
    }

    private static async Task<ResolutionResult> PlayJaxAsync(
        CoreRuleEngine engine,
        MatchState state,
        string jaxCardNo,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-jax-tempered-play", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                JaxObjectId,
                jaxCardNo,
                [],
                OptionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ResolveTopStackAsync(
        CoreRuleEngine engine,
        MatchState state)
    {
        var p1Pass = await PassPriorityAsync(engine, state, "P1", "intent-jax-tempered-p1-pass");
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await PassPriorityAsync(engine, p1Pass.State, "P2", "intent-jax-tempered-p2-pass");
        return p2Pass;
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

    private static void AssertTemperedAttachEvent(
        ResolutionResult result,
        string jaxCardNo,
        IReadOnlyList<string> optionalCosts)
    {
        var attachedEvent = Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_ATTACHED", StringComparison.Ordinal));
        Assert.Equal(SpinningAxeObjectId, attachedEvent.Payload["equipmentObjectId"]);
        Assert.Equal(JaxObjectId, attachedEvent.Payload["unitObjectId"]);
        Assert.Equal(JaxObjectId, attachedEvent.Payload["attachedToObjectId"]);
        Assert.Equal(SpinningAxeCardNo, attachedEvent.Payload["equipmentCardNo"]);
        Assert.Equal(jaxCardNo, attachedEvent.Payload["unitCardNo"]);
        Assert.Equal("TEMPERED_OPTIONAL_ATTACH", attachedEvent.Payload["reason"]);
        Assert.Equal(optionalCosts, Assert.IsType<string[]>(attachedEvent.Payload["optionalCosts"]));
    }

    private static PendingPaymentState AssertJaxPaymentOpen(ResolutionResult result)
    {
        var payment = result.State.PendingPayment;
        Assert.NotNull(payment);
        Assert.Equal(TriggerPaymentWindow, payment.PaymentWindow);
        Assert.Equal("P1", payment.PlayerId);
        Assert.Equal(1, payment.ManaCost);
        Assert.Contains(PayOneMana, payment.LegalPaymentChoiceIds);
        Assert.Contains(Decline, payment.LegalPaymentChoiceIds);

        var openedEvent = Assert.Single(result.Events, IsJaxPaymentOpened);
        Assert.Equal(payment.PaymentId, openedEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, openedEvent.Payload["paymentWindow"]);
        Assert.Equal(JaxTrigger, openedEvent.Payload["trigger"]);
        Assert.Equal(JaxObjectId, openedEvent.Payload["sourceObjectId"]);
        Assert.Equal(SpinningAxeObjectId, openedEvent.Payload["equipmentObjectId"]);
        Assert.Equal(SpinningAxeCardNo, openedEvent.Payload["equipmentCardNo"]);
        Assert.Equal([PayOneMana, Decline], Assert.IsType<string[]>(openedEvent.Payload["paymentChoices"]));
        Assert.Equal(JaxTrigger, openedEvent.Payload["reason"]);

        var prompt = result.Prompts["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        Assert.Equal(payment.PaymentId, Assert.IsType<string>(metadata["paymentId"]));
        Assert.Equal(TriggerPaymentWindow, Assert.IsType<string>(metadata["paymentWindow"]));
        var cost = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(metadata["cost"]);
        Assert.Equal(1, Assert.IsType<int>(cost["mana"]));
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentChoices"]).ToArray();
        Assert.Contains(choices, choice => string.Equals(choice.Id, PayOneMana, StringComparison.Ordinal));
        Assert.Contains(choices, choice => string.Equals(choice.Id, Decline, StringComparison.Ordinal));
        return payment;
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
        Assert.Equal(JaxObjectId, rawCommand.GetProperty("cardObjectId").GetString());
        Assert.Equal(JaxCardNo, rawCommand.GetProperty("cardNo").GetString());
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

    private static StackItemState AssertJaxStackPriorityState(
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
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Equal(["P1-HAND-SPINNING-AXE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            [
                SpinningAxeObjectId,
                "P1-NON-EQUIPMENT-SPINNING-AXE",
                "P1-FACE-DOWN-SPINNING-AXE",
                "P1-WRONG-CARD-EQUIPMENT",
                "P1-WRONG-CONTROLLER-SPINNING-AXE"
            ],
            result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.CardObjects[SpinningAxeObjectId].AttachedToObjectId);
        Assert.Null(result.State.PendingPayment);

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1", stackItem.ControllerId);
        Assert.Equal(JaxObjectId, stackItem.SourceObjectId);
        Assert.Equal(JaxCardNo, stackItem.CardNo);
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

    private static MatchState BuildTemperedJaxState(
        int mana = 5,
        string jaxCardNo = JaxCardNo)
    {
        return new MatchState(
            roomId: "jax-tempered-equipment-optional-attach-test",
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
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [JaxObjectId, "P1-HAND-SPINNING-AXE"],
                    MainDeck = ["P1-JAX-DRAW-001", "P1-JAX-DRAW-002"],
                    Base =
                    [
                        SpinningAxeObjectId,
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
                [JaxObjectId] = UnitCard(JaxObjectId, jaxCardNo, ownerId: "P1", controllerId: "P1"),
                [SpinningAxeObjectId] = SpinningAxe(SpinningAxeObjectId, "P1", "P1"),
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
                ["P2-EQUIPMENT-SPINNING-AXE"] = SpinningAxe("P2-EQUIPMENT-SPINNING-AXE", "P2", "P2"),
                ["P1-JAX-DRAW-001"] = new(
                    "P1-JAX-DRAW-001",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-JAX-DRAW-002"] = new(
                    "P1-JAX-DRAW-002",
                    ownerId: "P1",
                    controllerId: "P1")
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            });
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

    private static bool IsJaxPaymentOpened(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_OPENED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, JaxTrigger, StringComparison.Ordinal);
    }

    private static bool IsJaxTriggerResolved(GameEvent gameEvent)
    {
        return string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["trigger"] as string, JaxTrigger, StringComparison.Ordinal);
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
