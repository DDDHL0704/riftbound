using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HoneyfruitResourceSkillTests
{
    private const string HoneyfruitObjectId = "P1-HONEYFRUIT";
    private const string PendingSpellObjectId = "P2-PENDING-SPELL";
    private const string PendingStackItemId = "STACK-P2-PENDING-SPELL";

    [Fact]
    public void CatalogExposesHoneyfruitEquipmentReactionResourceSkill()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId,
            out var ability));

        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitCardNo, ability.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityEffectKind, ability.EffectKind);
        Assert.True(ability.IsResourceSkill);
        Assert.True(ability.ReactionSpeed);
        Assert.True(ability.PaymentOnlyResource);
        Assert.True(ability.ExhaustsSourceAsCost);
        Assert.True(ability.RequiresBaseEquipmentSource);
        Assert.False(ability.RequiresBattlefieldSource);
        Assert.Equal(0, ability.RequiredTargetCount);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, ability.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitPaymentOnlyResourceRestriction, ability.ResourceRestriction);
    }

    [Fact]
    public void HoneyfruitPromptExposesBaseAndLevelSixBranchesOnlyInLegalReactionWindow()
    {
        var basePrompt = ResolutionResult.BuildPrompts(BuildHoneyfruitPriorityState())["P1"];
        var baseRequirement = HoneyfruitRequirement(basePrompt);
        Assert.Equal(HoneyfruitObjectId, baseRequirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitCardNo, baseRequirement["cardNo"]);
        Assert.True(Assert.IsType<bool>(baseRequirement["resourceSkill"]));
        Assert.True(Assert.IsType<bool>(baseRequirement["reactionSpeed"]));
        Assert.True(Assert.IsType<bool>(baseRequirement["paymentOnly"]));
        Assert.True(Assert.IsType<bool>(baseRequirement["requiresBaseEquipmentSource"]));
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, baseRequirement["generatedPower"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, baseRequirement["generatedGenericPower"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitPaymentOnlyResourceRestriction, baseRequirement["resourceRestriction"]);
        Assert.Equal("stack-priority-reaction-representative", baseRequirement["timingPolicy"]);
        Assert.Equal("no-ordinary-stack-item", baseRequirement["stackPolicy"]);
        Assert.False(Assert.IsType<bool>(baseRequirement["levelSixEligible"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(baseRequirement["optionalCostChoices"]));

        var upgradedPrompt = ResolutionResult.BuildPrompts(WithExperience(BuildHoneyfruitPriorityState(), 6))["P1"];
        var upgradedRequirement = HoneyfruitRequirement(upgradedPrompt);
        Assert.True(Assert.IsType<bool>(upgradedRequirement["levelSixEligible"]));
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitUpgradedGeneratedMana, upgradedRequirement["upgradedGeneratedMana"]);
        var optionalChoice = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(
            upgradedRequirement["optionalCostChoices"]));
        Assert.Equal(LevelSixChoice(), optionalChoice.Id);

        AssertNoHoneyfruitPrompt(BuildHoneyfruitPriorityState() with
        {
            TimingState = TimingStates.NeutralOpen,
            PriorityPlayerId = null,
            StackItems = []
        });
        AssertNoHoneyfruitPrompt(BuildHoneyfruitPriorityState() with
        {
            CardObjects = ReplaceCardObject(
                BuildHoneyfruitPriorityState().CardObjects,
                HoneyfruitObjectId,
                Honeyfruit(HoneyfruitObjectId, "P1") with { IsExhausted = true })
        });
        AssertNoHoneyfruitPrompt(BuildHoneyfruitPriorityState(), "P2");
    }

    [Fact]
    public async Task HoneyfruitBaseBranchExhaustsSourceCreatesPaymentOnlyGenericPowerWithoutStackItem()
    {
        var result = await ResolveHoneyfruitAsync(BuildHoneyfruitPriorityState());

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects[HoneyfruitObjectId].IsExhausted);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Equal([PendingStackItemId], result.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));

        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal(HoneyfruitObjectId, temporaryResource.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId, temporaryResource.AbilityId);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, temporaryResource.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, temporaryResource.RemainingPower);
        Assert.Empty(temporaryResource.GeneratedPowerByTrait);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], temporaryResource.AllowedPaymentKinds);

        var snapshotResource = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            result.Snapshots["P1"].Timing["temporaryPaymentResources"]));
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitPaymentOnlyResourceRestriction, snapshotResource["resourceRestriction"]);

        var activatedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.False(Assert.IsType<bool>(activatedEvent.Payload["levelSixBranch"]));
        Assert.Equal(0, activatedEvent.Payload["generatedMana"]);
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["generatedResourceCannotBeTargetedAsResponse"]));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_EXHAUSTED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HoneyfruitLevelSixBranchAddsManaAndPaymentOnlyGenericPower()
    {
        var result = await ResolveHoneyfruitAsync(WithExperience(BuildHoneyfruitPriorityState(), 6), [LevelSixChoice()]);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitUpgradedGeneratedMana, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        Assert.True(result.State.CardObjects[HoneyfruitObjectId].IsExhausted);
        Assert.Single(result.State.TemporaryPaymentResources);

        var activatedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["levelSixBranch"]));
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitUpgradedGeneratedMana, activatedEvent.Payload["generatedMana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, activatedEvent.Payload["generatedPower"]);
        var manaEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.True(Assert.IsType<bool>(manaEvent.Payload["levelSixBranch"]));
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitUpgradedGeneratedMana, manaEvent.Payload["manaAfter"]);
    }

    [Fact]
    public async Task HoneyfruitLevelSixResourceStalePromptReplayAfterTemporaryLedgerRejectsWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = WithExperience(BuildHoneyfruitPriorityState(), 6);
        var command = HoneyfruitCommand(optionalCosts: [LevelSixChoice()]);
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.ActivateAbility, prompt.Actions);
        var requirement = HoneyfruitRequirement(prompt);
        Assert.True(Assert.IsType<bool>(requirement["levelSixEligible"]));
        Assert.Contains(
            Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]),
            choice => string.Equals(choice.Id, LevelSixChoice(), StringComparison.Ordinal));
        var staleRawCommand = PromptScopedRawCommand(CommandTypes.ActivateAbility, prompt);
        var changedStaleRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.ActivateAbility,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        const string acceptedClientIntentId = "intent-honeyfruit-level-six-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-honeyfruit-level-six-stale-prompt-replay";

        var gained = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(gained.Accepted, gained.ErrorMessage);
        Assert.True(gained.State.CardObjects[HoneyfruitObjectId].IsExhausted);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitUpgradedGeneratedMana, gained.State.RunePools["P1"].Mana);
        Assert.Equal(0, gained.State.RunePools["P1"].Power);
        var temporaryResource = Assert.Single(gained.State.TemporaryPaymentResources);
        Assert.Equal(HoneyfruitObjectId, temporaryResource.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId, temporaryResource.AbilityId);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, temporaryResource.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, temporaryResource.RemainingPower);
        Assert.Equal([PendingStackItemId], gained.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Null(gained.State.PendingPayment);
        Assert.Single(gained.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Single(gained.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_EXHAUSTED", StringComparison.Ordinal));
        Assert.Single(gained.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.Single(gained.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.DoesNotContain(gained.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        AssertNoHoneyfruitPrompt(gained.State);
        var postGainHash = MatchStateHasher.Hash(gained.State);
        var postGainPromptsHash = MatchStateHasher.HashValue(gained.Prompts);
        var postGainSnapshotsHash = MatchStateHasher.HashValue(gained.Snapshots);

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ActivateAbility, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.ActivateAbility, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(postGainHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(postGainPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(postGainSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(postGainHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(postGainPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(postGainSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(gained.State.RunePools["P1"], replay.State.RunePools["P1"]);
        Assert.True(replay.State.CardObjects[HoneyfruitObjectId].IsExhausted);
        Assert.Single(replay.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.TemporaryPaymentResources, replay.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.StackItems, replay.State.StackItems);
        Assert.Equal(gained.State.PendingTaskQueue.Phase, replay.State.PendingTaskQueue.Phase);
        Assert.Equal(gained.State.PendingTaskQueue.ActiveTaskId, replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(gained.State.PendingTaskQueue.Tasks, replay.State.PendingTaskQueue.Tasks);
        Assert.Null(replay.State.PendingPayment);
        AssertNoHoneyfruitPrompt(replay.State);

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ActivateAbility, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(MatchStateHasher.Hash(replay.State), MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(MatchStateHasher.HashValue(replay.Prompts), MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(MatchStateHasher.HashValue(replay.Snapshots), MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.ActivateAbility, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
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
        Assert.Equal(postGainHash, MatchStateHasher.Hash(duplicateReplay.State));
        Assert.Equal(postGainPromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(postGainSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        Assert.Equal(gained.State.RunePools["P1"], duplicateReplay.State.RunePools["P1"]);
        Assert.True(duplicateReplay.State.CardObjects[HoneyfruitObjectId].IsExhausted);
        Assert.Single(duplicateReplay.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.TemporaryPaymentResources, duplicateReplay.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.StackItems, duplicateReplay.State.StackItems);
        Assert.Equal(gained.State.PendingTaskQueue.Phase, duplicateReplay.State.PendingTaskQueue.Phase);
        Assert.Equal(gained.State.PendingTaskQueue.ActiveTaskId, duplicateReplay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(gained.State.PendingTaskQueue.Tasks, duplicateReplay.State.PendingTaskQueue.Tasks);
        Assert.Null(duplicateReplay.State.PendingPayment);
        AssertNoHoneyfruitPrompt(duplicateReplay.State);
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
        Assert.Equal(postGainHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(postGainPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(postGainSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(gained.State.RunePools["P1"], conflict.State.RunePools["P1"]);
        Assert.True(conflict.State.CardObjects[HoneyfruitObjectId].IsExhausted);
        Assert.Single(conflict.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.TemporaryPaymentResources, conflict.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.StackItems, conflict.State.StackItems);
        Assert.Equal(gained.State.PendingTaskQueue.Phase, conflict.State.PendingTaskQueue.Phase);
        Assert.Equal(gained.State.PendingTaskQueue.ActiveTaskId, conflict.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(gained.State.PendingTaskQueue.Tasks, conflict.State.PendingTaskQueue.Tasks);
        Assert.Null(conflict.State.PendingPayment);
        AssertNoHoneyfruitPrompt(conflict.State);
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HoneyfruitGeneratedResourcesPayLaterRuneCostAndClearAtPaymentOrTurnCleanup()
    {
        var activated = await ResolveHoneyfruitAsync(WithExperience(BuildHoneyfruitPriorityState(), 6), [LevelSixChoice()]);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var temporaryResource = Assert.Single(activated.State.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = new PendingPaymentState(
            "PAY-HONEYFRUIT-GENERATED",
            "TEST_PENDING_PAY_COST",
            "P1",
            manaCost: 1,
            powerCost: 1,
            legalPaymentChoiceIds: ["SPEND_MANA:1", "SPEND_POWER:any:1"]);
        var paymentState = activated.State with { PendingPayment = pendingPayment };

        var prompt = ResolutionResult.BuildPrompts(paymentState)["P1"];
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var payCostCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(payCostCandidate.Metadata);
        var paymentResourceChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(
            metadata["paymentResourceChoices"]);
        Assert.Equal([resourceAction], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        Assert.Equal([resourceAction], Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["paymentResourceActionIds"]));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, paymentResourcePowerByChoice[resourceAction]["power"]);
        Assert.Equal(true, paymentResourcePowerByChoice[resourceAction]["paymentOnly"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(paymentResourcePowerByChoice[resourceAction]["powerByTrait"]));

        var paid = await new CoreRuleEngine().ResolveAsync(
            paymentState,
            new PlayerIntent("intent-honeyfruit-pay-generated", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, "SPEND_MANA:1", "SPEND_POWER:any:1"]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Empty(paid.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, paid.State.RunePools["P1"]);
        Assert.Equal(
            ["TEMPORARY_PAYMENT_RESOURCE_SPENT", "TEMPORARY_PAYMENT_RESOURCE_CLEARED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
            paid.Events.Select(gameEvent => gameEvent.Kind));

        var spentEvent = Assert.Single(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, spentEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, spentEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", spentEvent.Payload["playerId"]);
        Assert.Equal(temporaryResource.ResourceId, spentEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(HoneyfruitObjectId, spentEvent.Payload["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId, spentEvent.Payload["abilityId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, spentEvent.Payload["consumedPower"]);
        Assert.Equal(0, spentEvent.Payload["remainingPower"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(spentEvent.Payload["allowedPaymentKinds"]));
        Assert.Equal(true, spentEvent.Payload["paymentOnly"]);

        var cleanupEvent = Assert.Single(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, cleanupEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, cleanupEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", cleanupEvent.Payload["playerId"]);
        Assert.Equal(temporaryResource.ResourceId, cleanupEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(0, cleanupEvent.Payload["remainingPowerBeforeCleanup"]);
        Assert.Equal(true, cleanupEvent.Payload["paymentOnly"]);

        var costEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, costEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, costEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", costEvent.Payload["playerId"]);
        Assert.Equal([resourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([resourceAction, "SPEND_MANA:1", "SPEND_POWER:any:1"], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Equal(["SPEND_MANA:1", "SPEND_POWER:any:1"], Assert.IsType<string[]>(costEvent.Payload["legalPaymentChoiceIds"]));
        Assert.Equal([temporaryResource.ResourceId], Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, costEvent.Payload["temporaryPaymentResourcePower"]);
        Assert.Equal(1, costEvent.Payload["mana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, costEvent.Payload["power"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]));
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["remainingPowerByTrait"]));

        var paymentWindowClosedEvent = Assert.Single(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, paymentWindowClosedEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, paymentWindowClosedEvent.Payload["paymentWindow"]);

        var unused = await ResolveHoneyfruitAsync(BuildHoneyfruitPriorityState());
        var cleanup = await new CoreRuleEngine().ResolveAsync(
            unused.State with
            {
                TimingState = TimingStates.NeutralOpen,
                PriorityPlayerId = null,
                PassedPriorityPlayerIds = [],
                StackItems = []
            },
            new PlayerIntent("intent-honeyfruit-end-turn", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(cleanup.Accepted, cleanup.ErrorMessage);
        Assert.Empty(cleanup.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, cleanup.State.RunePools["P1"]);
    }

    [Fact]
    public async Task HoneyfruitTemporaryResourceDoesNotExposeManaOnlyPromptResourceChoices()
    {
        var activated = await ResolveHoneyfruitAsync(BuildHoneyfruitPriorityState());
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var temporaryResource = Assert.Single(activated.State.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = new PendingPaymentState(
            "PAY-MANA-1",
            "TEST_PENDING_PAY_COST",
            "P1",
            manaCost: 1,
            legalPaymentChoiceIds: ["SPEND_MANA:1"]);
        var state = activated.State with
        {
            PendingPayment = pendingPayment
        };
        var stateHash = MatchStateHasher.Hash(state);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        Assert.Equal(stateHash, MatchStateHasher.Hash(state));
        Assert.Equal(activated.State.TemporaryPaymentResources, state.TemporaryPaymentResources);
        Assert.Equal(temporaryResource, Assert.Single(state.TemporaryPaymentResources));
        Assert.NotNull(state.PendingPayment);
        var actualPendingPayment = state.PendingPayment!;
        Assert.Equal("PAY-MANA-1", actualPendingPayment.PaymentId);
        Assert.Equal("TEST_PENDING_PAY_COST", actualPendingPayment.PaymentWindow);
        Assert.Equal("P1", actualPendingPayment.PlayerId);
        Assert.Equal(1, actualPendingPayment.ManaCost);
        Assert.Equal(0, actualPendingPayment.PowerCost);
        Assert.Empty(actualPendingPayment.PowerCostByTrait);
        Assert.Equal(["SPEND_MANA:1"], actualPendingPayment.LegalPaymentChoiceIds);
        Assert.Empty(actualPendingPayment.PaymentResourceActionIds);

        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.Contains(CommandTypes.PayCost, prompt.Actions);
        var payCostCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(payCostCandidate.Metadata);
        var paymentChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(metadata["paymentChoices"]);
        Assert.Contains(paymentChoices, choice => string.Equals(choice.Id, "SPEND_MANA:1", StringComparison.Ordinal));
        Assert.DoesNotContain(paymentChoices, choice => string.Equals(choice.Id, resourceAction, StringComparison.Ordinal));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["paymentResourceActionIds"]));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        Assert.Empty(paymentResourcePowerByChoice);
        Assert.DoesNotContain(resourceAction, paymentResourcePowerByChoice.Keys);
    }

    [Fact]
    public async Task HoneyfruitLevelSixGeneratedManaAndTemporaryPowerCombineWithRunePoolForLargerMixedCost()
    {
        var activated = await ResolveHoneyfruitAsync(WithExperience(BuildHoneyfruitPriorityState(), 6), [LevelSixChoice()]);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var temporaryResource = Assert.Single(activated.State.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var powerCost = P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower + 1;
        var powerChoice = $"SPEND_POWER:any:{powerCost}";
        var pendingPayment = new PendingPaymentState(
            "PAY-HONEYFRUIT-MIXED-GENERATED-PLUS-RUNE",
            "TEST_PENDING_PAY_COST",
            "P1",
            manaCost: 1,
            powerCost: powerCost,
            legalPaymentChoiceIds: ["SPEND_MANA:1", powerChoice]);
        var runePools = new Dictionary<string, RunePool>(activated.State.RunePools, StringComparer.Ordinal)
        {
            ["P1"] = new RunePool(activated.State.RunePools["P1"].Mana, 1)
        };
        var paymentState = activated.State with
        {
            PendingPayment = pendingPayment,
            RunePools = runePools
        };

        var prompt = ResolutionResult.BuildPrompts(paymentState)["P1"];
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var payCostCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(payCostCandidate.Metadata);
        var paymentResourceChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(
            metadata["paymentResourceChoices"]);
        Assert.Equal([resourceAction], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, paymentResourcePowerByChoice[resourceAction]["power"]);
        Assert.Equal(true, paymentResourcePowerByChoice[resourceAction]["paymentOnly"]);

        var paid = await new CoreRuleEngine().ResolveAsync(
            paymentState,
            new PlayerIntent("intent-honeyfruit-pay-mixed-generated-plus-rune", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, "SPEND_MANA:1", powerChoice]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Empty(paid.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, paid.State.RunePools["P1"]);
        Assert.Equal(
            ["TEMPORARY_PAYMENT_RESOURCE_SPENT", "TEMPORARY_PAYMENT_RESOURCE_CLEARED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
            paid.Events.Select(gameEvent => gameEvent.Kind));

        var spentEvent = Assert.Single(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, spentEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, spentEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", spentEvent.Payload["playerId"]);
        Assert.Equal(temporaryResource.ResourceId, spentEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(HoneyfruitObjectId, spentEvent.Payload["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId, spentEvent.Payload["abilityId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, spentEvent.Payload["consumedPower"]);
        Assert.Equal(0, spentEvent.Payload["remainingPower"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(spentEvent.Payload["allowedPaymentKinds"]));
        Assert.Equal(true, spentEvent.Payload["paymentOnly"]);

        var clearedEvent = Assert.Single(paid.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, clearedEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, clearedEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", clearedEvent.Payload["playerId"]);
        Assert.Equal(temporaryResource.ResourceId, clearedEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(0, clearedEvent.Payload["remainingPowerBeforeCleanup"]);
        Assert.Equal(true, clearedEvent.Payload["paymentOnly"]);

        var costEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, costEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, costEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", costEvent.Payload["playerId"]);
        Assert.Equal([resourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([resourceAction, "SPEND_MANA:1", powerChoice], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Equal(["SPEND_MANA:1", powerChoice], Assert.IsType<string[]>(costEvent.Payload["legalPaymentChoiceIds"]));
        Assert.Equal([temporaryResource.ResourceId], Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
        Assert.Equal(1, costEvent.Payload["mana"]);
        Assert.Equal(powerCost, costEvent.Payload["power"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, costEvent.Payload["temporaryPaymentResourcePower"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]));
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["remainingPowerByTrait"]));
    }

    [Theory]
    [InlineData("duplicate-resource")]
    [InlineData("mana-only")]
    public async Task HoneyfruitTemporaryResourceRejectsWrongOrDuplicateSpendWithoutMutation(string caseName)
    {
        var activated = await ResolveHoneyfruitAsync(BuildHoneyfruitPriorityState());
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var temporaryResource = Assert.Single(activated.State.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = caseName == "mana-only"
            ? new PendingPaymentState(
                "PAY-HONEYFRUIT-MANA-ONLY",
                "TEST_PENDING_PAY_COST",
                "P1",
                manaCost: 1,
                legalPaymentChoiceIds: ["SPEND_MANA:1"])
            : new PendingPaymentState(
                "PAY-HONEYFRUIT-GENERIC-1",
                "TEST_PENDING_PAY_COST",
                "P1",
                powerCost: 1,
                legalPaymentChoiceIds: ["SPEND_POWER:any:1"]);
        var state = activated.State with { PendingPayment = pendingPayment };
        var initialHash = MatchStateHasher.Hash(state);
        var choices = caseName == "mana-only"
            ? new[] { resourceAction, "SPEND_MANA:1" }
            : [resourceAction, resourceAction, "SPEND_POWER:any:1"];

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-honeyfruit-reject-pay-{caseName}", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, choices),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    [Theory]
    [InlineData("wrong-timing")]
    [InlineData("target")]
    [InlineData("exhausted")]
    [InlineData("stale-source")]
    [InlineData("non-honeyfruit-source")]
    [InlineData("illegal-upgraded-branch")]
    [InlineData("wrong-upgraded-source")]
    [InlineData("unsupported-generated-amount")]
    [InlineData("temp-resource-optional-cost")]
    public async Task HoneyfruitRejectsInvalidSourceTimingOrPayloadWithoutMutation(string caseName)
    {
        var state = BuildInvalidState(caseName);
        var command = caseName switch
        {
            "target" => HoneyfruitCommand(targetObjectIds: ["P2-ANY-TARGET"]),
            "illegal-upgraded-branch" => HoneyfruitCommand(optionalCosts: [LevelSixChoice()]),
            "wrong-upgraded-source" => HoneyfruitCommand(optionalCosts: [$"{P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix}P1-OTHER"]),
            "unsupported-generated-amount" => HoneyfruitCommand(optionalCosts: ["HONEYFRUIT_GAIN:2"]),
            "temp-resource-optional-cost" => HoneyfruitCommand(optionalCosts: ["TEMP_PAYMENT_RESOURCE:HANDWRITTEN"]),
            _ => HoneyfruitCommand()
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    private static async Task<ResolutionResult> ResolveHoneyfruitAsync(
        MatchState state,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-honeyfruit-resource", "P1", CommandTypes.ActivateAbility),
            HoneyfruitCommand(optionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        ActivateAbilityCommand command)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-honeyfruit-invalid", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    private static IReadOnlyDictionary<string, object?> HoneyfruitRequirement(ActionPromptDto prompt)
    {
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.Contains(activateCandidate.Sources ?? [], choice => string.Equals(choice.Id, HoneyfruitObjectId, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        return Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(entry["abilityId"] as string, P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId, StringComparison.Ordinal));
    }

    private static void AssertNoHoneyfruitPrompt(MatchState state, string playerId = "P1")
    {
        var prompt = ResolutionResult.BuildPrompts(state)[playerId];
        foreach (var candidate in prompt.Candidates ?? [])
        {
            if (!string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal)
                || candidate.Metadata is not IReadOnlyDictionary<string, object?> metadata
                || !metadata.TryGetValue("sourceRequirements", out var rawRequirements)
                || rawRequirements is not IEnumerable<IReadOnlyDictionary<string, object?>> sourceRequirements)
            {
                continue;
            }

            Assert.DoesNotContain(
                sourceRequirements,
                requirement => string.Equals(
                    requirement["abilityId"] as string,
                    P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId,
                    StringComparison.Ordinal));
        }
    }

    private static ActivateAbilityCommand HoneyfruitCommand(
        IReadOnlyList<string>? targetObjectIds = null,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            HoneyfruitObjectId,
            P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId,
            targetObjectIds ?? [],
            optionalCosts);
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

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];

        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }
    }

    private static string LevelSixChoice()
    {
        return $"{P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix}{HoneyfruitObjectId}";
    }

    private static MatchState BuildInvalidState(string caseName)
    {
        var state = BuildHoneyfruitPriorityState();
        return caseName switch
        {
            "wrong-timing" => state with
            {
                TimingState = TimingStates.NeutralOpen,
                PriorityPlayerId = null,
                StackItems = []
            },
            "exhausted" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    HoneyfruitObjectId,
                    state.CardObjects[HoneyfruitObjectId] with { IsExhausted = true })
            },
            "stale-source" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with
                    {
                        Base = [],
                        Battlefields = [HoneyfruitObjectId]
                    }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    HoneyfruitObjectId,
                    new ObjectLocationState("P1", "BATTLEFIELD", "P1-MAIN"))
            },
            "non-honeyfruit-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    HoneyfruitObjectId,
                    state.CardObjects[HoneyfruitObjectId] with { CardNo = "UNL-050/219" })
            },
            _ => state
        };
    }

    private static MatchState WithExperience(MatchState state, int experience)
    {
        return state with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = experience,
                ["P2"] = 0
            }
        };
    }

    private static MatchState BuildHoneyfruitPriorityState()
    {
        return new MatchState(
            "room-honeyfruit-resource",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "Alice",
                ["P2"] = "Bob"
            },
            status: MatchStatuses.InProgress,
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
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [HoneyfruitObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [HoneyfruitObjectId] = Honeyfruit(HoneyfruitObjectId, "P1"),
                [PendingSpellObjectId] = new(
                    PendingSpellObjectId,
                    tags: [CardObjectTags.SpellCard],
                    cardNo: "UNL-001/219",
                    ownerId: "P2",
                    controllerId: "P2")
            },
            priorityPlayerId: "P1",
            stackItems:
            [
                new StackItemState(
                    PendingStackItemId,
                    "P2",
                    PendingSpellObjectId,
                    "TEST_PENDING_REACTION_SPELL",
                    "UNL-001/219")
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [HoneyfruitObjectId] = new("P1", "BASE"),
                [PendingSpellObjectId] = new("P2", "STACK")
            });
    }

    private static CardObjectState Honeyfruit(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.EquipmentCard, "蜜糖果实", "反应"],
            cardNo: P4ActivatedAbilityCatalog.HoneyfruitCardNo,
            ownerId: playerId,
            controllerId: playerId);
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

    private static IReadOnlyDictionary<string, PlayerZones> ReplacePlayerZones(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string playerId,
        PlayerZones replacement)
    {
        var next = playerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[playerId] = replacement;
        return next;
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> ReplaceObjectLocation(
        IReadOnlyDictionary<string, ObjectLocationState> locations,
        string objectId,
        ObjectLocationState replacement)
    {
        var next = locations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[objectId] = replacement;
        return next;
    }
}
