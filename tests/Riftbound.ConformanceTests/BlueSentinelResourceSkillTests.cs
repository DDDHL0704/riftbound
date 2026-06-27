using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BlueSentinelResourceSkillTests
{
    private const string BattlefieldObjectId = "BATTLEFIELD:P1-MAIN";
    private const string AttackerObjectId = "P1-BLUE-SENTINEL-ATTACKER";
    private const string BlueSentinelObjectId = "P2-BLUE-SENTINEL";

    [Fact]
    public void CatalogExposesBlueSentinelDelayedResourceSkill()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityId,
            out var ability));

        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelCardNo, ability.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityEffectKind, ability.EffectKind);
        Assert.True(ability.IsResourceSkill);
        Assert.True(ability.PaymentOnlyResource);
        Assert.True(ability.RequiresBattlefieldSource);
        Assert.False(ability.ReactionSpeed);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelGeneratedPower, ability.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelPaymentOnlyResourceRestriction, ability.ResourceRestriction);
    }

    [Fact]
    public void BlueSentinelSourceIdentityUsesAbilitySourceCardGroup()
    {
        var repositoryRoot = RepositoryRoot();
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));
        var matchSessionSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Riftbound.Engine",
            "MatchSession.cs"));
        var matchRecoverySource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Riftbound.Engine",
            "MatchRecovery.cs"));

        Assert.DoesNotContain(
            "sourceState.CardNo, P4ActivatedAbilityCatalog.BlueSentinelCardNo",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "sourceState.CardNo, P4ActivatedAbilityCatalog.BlueSentinelCardNo",
            matchSessionSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "sourceState.CardNo, P4ActivatedAbilityCatalog.BlueSentinelCardNo",
            matchRecoverySource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "sourceCardNo, P4ActivatedAbilityCatalog.BlueSentinelCardNo",
            matchRecoverySource,
            StringComparison.Ordinal);
        Assert.Contains("P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId", matchRecoverySource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BlueSentinelHeldBattlefieldQueuesServerOwnedDelayedTrigger()
    {
        var result = await ResolveHeldBattleAsync();

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "BATTLEFIELD_HELD", StringComparison.Ordinal));
        var trigger = Assert.Single(result.State.TriggerQueue);
        Assert.Equal("P2", trigger.ControllerId);
        Assert.Equal(BlueSentinelObjectId, trigger.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityEffectKind, trigger.EffectKind);
        Assert.Equal("BATTLEFIELD_HELD", trigger.TriggeredByEventKind);
        Assert.Contains(BattlefieldObjectId, trigger.TriggerId, StringComparison.Ordinal);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["triggerId"] as string, trigger.TriggerId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task BlueSentinelDelayedResourceIsPromptedAndConsumedOnlyForNextMainRunePayment()
    {
        var held = await ResolveHeldBattleAsync();
        var trigger = Assert.Single(held.State.TriggerQueue);
        var payment = PendingRunePayment();
        var paymentState = NextMainPaymentState(held.State, payment);
        var prompt = ResolutionResult.BuildPrompts(paymentState)["P2"];
        var candidate = Assert.Single(prompt.Candidates ?? [], candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var resourceActions = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"]);
        var resourceAction = Assert.Single(resourceActions);
        Assert.Equal($"{P4ActivatedAbilityCatalog.BlueSentinelDelayedResourceActionPrefix}{trigger.TriggerId}", resourceAction.Id);
        var resourceActionIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["paymentResourceActionIds"]);
        Assert.Equal([resourceAction.Id], resourceActionIds);
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelGeneratedPower, paymentResourcePowerByChoice[resourceAction.Id]["power"]);
        Assert.Equal(true, paymentResourcePowerByChoice[resourceAction.Id]["paymentOnly"]);
        Assert.Equal(trigger.TriggerId, paymentResourcePowerByChoice[resourceAction.Id]["delayedTriggerId"]);
        Assert.Equal(BlueSentinelObjectId, paymentResourcePowerByChoice[resourceAction.Id]["sourceObjectId"]);
        Assert.Equal(BattlefieldObjectId, paymentResourcePowerByChoice[resourceAction.Id]["battlefieldObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityId, paymentResourcePowerByChoice[resourceAction.Id]["abilityId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelPaymentOnlyResourceRestriction, paymentResourcePowerByChoice[resourceAction.Id]["resourceRestriction"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(paymentResourcePowerByChoice[resourceAction.Id]["allowedPaymentKinds"]));
        Assert.Equal("temporary-payment-resource-ledger", paymentResourcePowerByChoice[resourceAction.Id]["resourceLifecycle"]);
        Assert.Equal(true, paymentResourcePowerByChoice[resourceAction.Id]["generatedResourceCannotBeTargetedAsResponse"]);

        var expectedTemporaryResourceId = $"BLUE_SENTINEL:{payment.PaymentId}:{trigger.TriggerId}";
        var expectedTemporaryResourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(expectedTemporaryResourceId);

        var result = await new CoreRuleEngine().ResolveAsync(
            paymentState,
            new PlayerIntent("intent-blue-sentinel-pay-generated", "P2", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [resourceAction.Id, "SPEND_POWER:any:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.State.PendingPayment);
        Assert.Empty(result.State.TriggerQueue);
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P2"]);
        Assert.Equal(
            [
                "TRIGGER_RESOLVED",
                "ABILITY_ACTIVATED",
                "POWER_GAINED",
                "TEMPORARY_PAYMENT_RESOURCE_SPENT",
                "TEMPORARY_PAYMENT_RESOURCE_CLEARED",
                "COST_PAID",
                "PAYMENT_WINDOW_CLOSED"
            ],
            result.Events.Select(gameEvent => gameEvent.Kind));

        var triggerResolvedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.Equal(trigger.TriggerId, triggerResolvedEvent.Payload["triggerId"]);
        var activatedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityId, activatedEvent.Payload["abilityId"]);
        Assert.Equal(payment.PaymentId, activatedEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, activatedEvent.Payload["paymentWindow"]);
        Assert.Equal(trigger.TriggerId, activatedEvent.Payload["delayedTriggerId"]);
        Assert.Equal(BattlefieldObjectId, activatedEvent.Payload["battlefieldObjectId"]);
        Assert.Equal(expectedTemporaryResourceId, activatedEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelPaymentOnlyResourceRestriction, activatedEvent.Payload["resourceRestriction"]);
        Assert.Equal("no-ordinary-stack-item", activatedEvent.Payload["stackPolicy"]);
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["generatedResourceCannotBeTargetedAsResponse"]));

        var powerGainedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, powerGainedEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, powerGainedEvent.Payload["paymentWindow"]);
        Assert.Equal(trigger.TriggerId, powerGainedEvent.Payload["delayedTriggerId"]);
        Assert.Equal(expectedTemporaryResourceId, powerGainedEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelGeneratedPower, powerGainedEvent.Payload["power"]);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelGeneratedPower, powerGainedEvent.Payload["remainingPower"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(powerGainedEvent.Payload["allowedPaymentKinds"]));

        var spendEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, spendEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, spendEvent.Payload["paymentWindow"]);
        Assert.Equal(expectedTemporaryResourceId, spendEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(BlueSentinelObjectId, spendEvent.Payload["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityId, spendEvent.Payload["abilityId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelGeneratedPower, spendEvent.Payload["consumedPower"]);
        Assert.Equal(0, spendEvent.Payload["remainingPower"]);
        Assert.Equal(true, spendEvent.Payload["paymentOnly"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(spendEvent.Payload["allowedPaymentKinds"]));

        var cleanupEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, cleanupEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, cleanupEvent.Payload["paymentWindow"]);
        Assert.Equal(expectedTemporaryResourceId, cleanupEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(0, cleanupEvent.Payload["remainingPowerBeforeCleanup"]);
        Assert.Equal(true, cleanupEvent.Payload["paymentOnly"]);

        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, costEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, costEvent.Payload["paymentWindow"]);
        Assert.Equal([expectedTemporaryResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([resourceAction.Id, "SPEND_POWER:any:1"], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Equal(["SPEND_POWER:any:1"], Assert.IsType<string[]>(costEvent.Payload["legalPaymentChoiceIds"]));
        Assert.Equal([expectedTemporaryResourceId], Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelGeneratedPower, costEvent.Payload["temporaryPaymentResourcePower"]);
        Assert.Equal(P4ActivatedAbilityCatalog.BlueSentinelGeneratedPower, costEvent.Payload["power"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);

        var paymentWindowClosedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.Equal(payment.PaymentId, paymentWindowClosedEvent.Payload["paymentId"]);
        Assert.Equal(payment.PaymentWindow, paymentWindowClosedEvent.Payload["paymentWindow"]);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BlueSentinelDelayedResourceDoesNotLeakPromptMetadataForManaOnlyPayment()
    {
        var held = await ResolveHeldBattleAsync();
        var trigger = Assert.Single(held.State.TriggerQueue);
        var payment = new PendingPaymentState(
            "PAY-BLUE-SENTINEL-MANA-ONLY",
            "TEST_PENDING_PAY_COST",
            "P2",
            manaCost: 1,
            legalPaymentChoiceIds: ["SPEND_MANA:1"]);
        var paymentState = NextMainPaymentState(held.State, payment);
        var delayedResourceAction = $"{P4ActivatedAbilityCatalog.BlueSentinelDelayedResourceActionPrefix}{trigger.TriggerId}";

        var prompt = ResolutionResult.BuildPrompts(paymentState)["P2"];

        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.Contains(CommandTypes.PayCost, prompt.Actions);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var paymentChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(metadata["paymentChoices"]);
        Assert.Equal(["SPEND_MANA:1"], paymentChoices.Select(choice => choice.Id).ToArray());
        var paymentResourceChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"]);
        Assert.Empty(paymentResourceChoices);
        var paymentResourceActionIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["paymentResourceActionIds"]);
        Assert.Empty(paymentResourceActionIds);
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        Assert.Empty(paymentResourcePowerByChoice);
        Assert.DoesNotContain(delayedResourceAction, paymentResourcePowerByChoice.Keys);
        Assert.Equal(trigger, Assert.Single(paymentState.TriggerQueue));
        Assert.Equal(payment, paymentState.PendingPayment);
    }

    [Fact]
    public async Task BlueSentinelStalePromptScopedPayCostReplayAfterWindowClosesUsesRejectedCache()
    {
        var acceptedClientIntentId = "intent-blue-sentinel-pay-generated-before-stale-replay";
        var staleClientIntentId = "intent-blue-sentinel-pay-generated-stale-replay";
        var journal = new RecordingMatchJournal();
        var held = await ResolveHeldBattleAsync();
        var trigger = Assert.Single(held.State.TriggerQueue);
        var payment = PendingRunePayment();
        var session = new MatchSession(
            NextMainPaymentState(held.State, payment),
            new CoreRuleEngine(),
            journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P2");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.Contains(CommandTypes.PayCost, prompt.Actions);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var resourceActions = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"]);
        var resourceAction = Assert.Single(resourceActions);
        Assert.Equal(
            $"{P4ActivatedAbilityCatalog.BlueSentinelDelayedResourceActionPrefix}{trigger.TriggerId}",
            resourceAction.Id);
        var command = new PayCostCommand(
            payment.PaymentId,
            payment.PaymentWindow,
            [resourceAction.Id, "SPEND_POWER:any:1"]);
        var staleRawCommand = PromptScopedPayCostRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedPayCostRawCommandWithClientNote(
            command,
            prompt,
            "changed-payload");

        var paid = await session.SubmitAsync(
            "P2",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.ErrorCode);
        Assert.Equal(
            [
                "TRIGGER_RESOLVED",
                "ABILITY_ACTIVATED",
                "POWER_GAINED",
                "TEMPORARY_PAYMENT_RESOURCE_SPENT",
                "TEMPORARY_PAYMENT_RESOURCE_CLEARED",
                "COST_PAID",
                "PAYMENT_WINDOW_CLOSED"
            ],
            paid.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Null(paid.State.PendingPayment);
        Assert.Empty(paid.State.TriggerQueue);
        Assert.Empty(paid.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, paid.State.RunePools["P2"]);
        AssertNoPayCostPrompt(paid.Prompts["P2"]);
        var postPaymentStateHash = MatchStateHasher.Hash(paid.State);
        var postPaymentAuthoritativePromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(paid.State));
        var postPaymentAuthoritativeSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(paid.State));
        var postPaymentP1SessionSnapshotHash = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var postPaymentP2SessionSnapshotHash = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var replay = await session.SubmitAsync(
            "P2",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(postPaymentP1SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(postPaymentP2SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Null(replay.State.PendingPayment);
        Assert.Empty(replay.State.TriggerQueue);
        Assert.Empty(replay.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, replay.State.RunePools["P2"]);
        AssertNoPayCostPrompt(replay.Prompts["P2"]);

        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, journal.Entries.Count(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal)));
        var rejectedEntry = Assert.Single(
            journal.Entries,
            entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal) && !entry.Accepted);
        Assert.Equal(held.State.RoomId, rejectedEntry.RoomId);
        Assert.Equal("P2", rejectedEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PayCost, rejectedEntry.CommandType);
        Assert.False(rejectedEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedEntry.ErrorMessage);
        Assert.Empty(rejectedEntry.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(rejectedEntry.AuthoritativeState));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(rejectedEntry.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(rejectedEntry.Snapshots));
        Assert.True(rejectedEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(rejectedEntry.RawCommand.Value));
        AssertPayCostRawCommand(rejectedEntry.RawCommand.Value, command, prompt);
        Assert.False(rejectedEntry.RawCommand.Value.TryGetProperty("clientNote", out _));
        var journalHashAfterReplay = MatchStateHasher.HashValue(journal.Entries);

        var duplicateReplay = await session.SubmitAsync(
            "P2",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateReplay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateReplay.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateReplay.ErrorMessage);
        Assert.Empty(duplicateReplay.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(duplicateReplay.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        Assert.Equal(postPaymentP1SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(postPaymentP2SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Null(duplicateReplay.State.PendingPayment);
        Assert.Empty(duplicateReplay.State.TriggerQueue);
        Assert.Empty(duplicateReplay.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, duplicateReplay.State.RunePools["P2"]);
        AssertNoPayCostPrompt(duplicateReplay.Prompts["P2"]);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));

        var conflict = await session.SubmitAsync(
            "P2",
            staleClientIntentId,
            command,
            changedStaleRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(postPaymentP1SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(postPaymentP2SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Null(conflict.State.PendingPayment);
        Assert.Empty(conflict.State.TriggerQueue);
        Assert.Empty(conflict.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, conflict.State.RunePools["P2"]);
        AssertNoPayCostPrompt(conflict.Prompts["P2"]);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("wrong-main-phase")]
    [InlineData("late-next-main-window")]
    [InlineData("missing-trigger")]
    [InlineData("stale-source")]
    [InlineData("stale-battlefield")]
    [InlineData("unsupported-generated-amount")]
    [InlineData("duplicate-spend")]
    [InlineData("non-rune-payment")]
    [InlineData("ordinary-temp-forged")]
    public async Task BlueSentinelDelayedResourceRejectsInvalidCommandsWithoutMutation(string caseName)
    {
        var held = await ResolveHeldBattleAsync();
        var trigger = Assert.Single(held.State.TriggerQueue);
        var payment = caseName == "non-rune-payment"
            ? new PendingPaymentState(
                "PAY-BLUE-SENTINEL-MANA-ONLY",
                "TEST_PENDING_PAY_COST",
                "P2",
                manaCost: 1,
                legalPaymentChoiceIds: ["SPEND_MANA:1"])
            : PendingRunePayment();
        var state = caseName switch
        {
            "wrong-main-phase" => NextMainPaymentState(held.State, payment) with { Phase = MatchPhases.TurnStart },
            "late-next-main-window" => NextMainPaymentState(held.State, payment) with { TurnNumber = held.State.TurnNumber + 2 },
            "missing-trigger" => NextMainPaymentState(held.State with { TriggerQueue = [] }, payment),
            "stale-source" => NextMainPaymentState(held.State with
            {
                CardObjects = ReplaceCardObject(
                    held.State.CardObjects,
                    BlueSentinelObjectId,
                    held.State.CardObjects[BlueSentinelObjectId] with { CardNo = "UNL-088/219" })
            }, payment),
            "stale-battlefield" => NextMainPaymentState(held.State with
            {
                ObjectLocations = ReplaceObjectLocation(
                    held.State.ObjectLocations,
                    BlueSentinelObjectId,
                    new ObjectLocationState("P2", "BATTLEFIELD", "BATTLEFIELD:P2-OTHER"))
            }, payment),
            _ => NextMainPaymentState(held.State, payment)
        };
        var action = $"{P4ActivatedAbilityCatalog.BlueSentinelDelayedResourceActionPrefix}{trigger.TriggerId}";
        var choices = caseName switch
        {
            "unsupported-generated-amount" => [$"{P4ActivatedAbilityCatalog.BlueSentinelDelayedResourceActionPrefix}{trigger.TriggerId}:2", "SPEND_POWER:any:1"],
            "duplicate-spend" => [action, action, "SPEND_POWER:any:1"],
            "non-rune-payment" => [action, "SPEND_MANA:1"],
            "ordinary-temp-forged" => [PaymentCostRules.TemporaryPaymentResourceActionId("BLUE_SENTINEL:HANDWRITTEN"), "SPEND_POWER:any:1"],
            _ => new[] { action, "SPEND_POWER:any:1" }
        };

        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-blue-sentinel-invalid-{caseName}", "P2", CommandTypes.PayCost),
            new PayCostCommand(payment.PaymentId, payment.PaymentWindow, choices),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    private static async Task<ResolutionResult> ResolveHeldBattleAsync()
    {
        return await new CoreRuleEngine().ResolveAsync(
            BuildHeldBattleState(),
            new PlayerIntent("intent-blue-sentinel-held-battle", "P1", CommandTypes.DeclareBattle),
            new DeclareBattleCommand(
                BattlefieldObjectId,
                [AttackerObjectId],
                [BlueSentinelObjectId],
                ["COMBAT_ASSIGNMENT"]),
            CancellationToken.None);
    }

    private static PendingPaymentState PendingRunePayment()
    {
        return new PendingPaymentState(
            "PAY-BLUE-SENTINEL-GENERATED",
            "TEST_PENDING_PAY_COST",
            "P2",
            powerCost: 1,
            legalPaymentChoiceIds: ["SPEND_POWER:any:1"]);
    }

    private static MatchState NextMainPaymentState(MatchState state, PendingPaymentState payment)
    {
        return state with
        {
            TurnNumber = state.TurnNumber + 1,
            TurnPlayerId = "P2",
            ActivePlayerId = "P2",
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            PendingPayment = payment,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            }
        };
    }

    private static void AssertNoPayCostPrompt(ActionPromptDto prompt)
    {
        Assert.DoesNotContain(CommandTypes.PayCost, prompt.Actions);
        Assert.DoesNotContain(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
    }

    private static JsonElement PromptScopedPayCostRawCommand(PayCostCommand command, ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = command.PaymentId,
            paymentWindow = command.PaymentWindow,
            paymentChoiceIds = command.PaymentChoiceIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
    }

    private static JsonElement PromptScopedPayCostRawCommandWithClientNote(
        PayCostCommand command,
        ActionPromptDto prompt,
        string clientNote)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = command.PaymentId,
            paymentWindow = command.PaymentWindow,
            paymentChoiceIds = command.PaymentChoiceIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote
        });
    }

    private static void AssertPayCostRawCommand(
        JsonElement rawCommand,
        PayCostCommand command,
        ActionPromptDto prompt)
    {
        Assert.Equal(CommandTypes.PayCost, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(command.PaymentId, rawCommand.GetProperty("paymentId").GetString());
        Assert.Equal(command.PaymentWindow, rawCommand.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            command.PaymentChoiceIds,
            rawCommand.GetProperty("paymentChoiceIds").EnumerateArray().Select(choice => choice.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static MatchState BuildHeldBattleState()
    {
        return new MatchState(
            roomId: "blue-sentinel-resource-skill-test",
            tick: 30,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "Alice",
                ["P2"] = "Bob"
            },
            status: MatchStatuses.InProgress,
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
                    Battlefields = [AttackerObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [BlueSentinelObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [AttackerObjectId] = new(
                    AttackerObjectId,
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                [BlueSentinelObjectId] = new(
                    BlueSentinelObjectId,
                    cardNo: P4ActivatedAbilityCatalog.BlueSentinelCardNo,
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "坚守2"],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [AttackerObjectId] = new("P1", "BATTLEFIELD", "P1-OTHER-BATTLEFIELD"),
                [BlueSentinelObjectId] = new("P2", "BATTLEFIELD", BattlefieldObjectId)
            });
    }

    private static IReadOnlyDictionary<string, CardObjectState> ReplaceCardObject(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string objectId,
        CardObjectState replacement)
    {
        var next = cardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[objectId] = replacement;
        return next;
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> ReplaceObjectLocation(
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        string objectId,
        ObjectLocationState replacement)
    {
        var next = objectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[objectId] = replacement;
        return next;
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

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "riftbound-dotnet.sln"))
                || File.Exists(Path.Combine(current.FullName, "Riftbound.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test output directory.");
    }
}
