using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ResourceConversionEquipmentResourceSkillTests
{
    private const string EnergyChannelObjectId = "P1-ENERGY-CHANNEL";
    private const string AncientSteleObjectId = "P1-ANCIENT-STELE";
    private const string HextechAnomalyObjectId = "P1-HEXTECH-ANOMALY";
    private const string PendingSpellObjectId = "P2-PENDING-SPELL";
    private const string PendingStackItemId = "STACK-P2-PENDING-SPELL";

    [Fact]
    public void CatalogExposesResourceConversionEquipmentReactionSkills()
    {
        AssertResourceConversionDefinition(
            P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId,
            P4ActivatedAbilityCatalog.EnergyChannelCardNo,
            P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityEffectKind,
            generatedMana: P4ActivatedAbilityCatalog.EnergyChannelGeneratedMana,
            paymentOnly: false);
        AssertResourceConversionDefinition(
            P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId,
            P4ActivatedAbilityCatalog.AncientSteleCardNo,
            P4ActivatedAbilityCatalog.AncientSteleResourceAbilityEffectKind,
            generatedMana: 0,
            paymentOnly: true);
        AssertResourceConversionDefinition(
            P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId,
            P4ActivatedAbilityCatalog.HextechAnomalyCardNo,
            P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityEffectKind,
            generatedMana: 0,
            paymentOnly: false);
    }

    [Fact]
    public void ResourceConversionReactionPromptExposesServerDefinedConversionChoices()
    {
        var state = BuildPriorityState(new RunePool(3, 3));
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();

        var energy = Requirement(sourceRequirements, P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId);
        Assert.Equal(EnergyChannelObjectId, energy["sourceObjectId"]);
        Assert.Equal("gain-mana", energy["conversionKind"]);
        Assert.Equal(P4ActivatedAbilityCatalog.EnergyChannelGeneratedMana, energy["generatedMana"]);
        Assert.Equal("rune-pool-mana-reset-at-turn-cleanup", energy["resourceLifecycle"]);
        Assert.Equal("no-ordinary-stack-item", energy["stackPolicy"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(energy["optionalCostChoices"]));

        var ancient = Requirement(sourceRequirements, P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId);
        Assert.Equal(AncientSteleObjectId, ancient["sourceObjectId"]);
        Assert.Equal("mana-to-generic-power", ancient["conversionKind"]);
        Assert.Equal(3, ancient["maxConversionAmount"]);
        Assert.Equal(P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix, ancient["conversionChoicePrefix"]);
        Assert.Equal("temporary-payment-resource-ledger", ancient["resourceLifecycle"]);
        Assert.Equal("no-ordinary-stack-item", ancient["stackPolicy"]);
        Assert.Equal(
            [
                "CONVERT_MANA_TO_GENERIC_POWER:1",
                "CONVERT_MANA_TO_GENERIC_POWER:2",
                "CONVERT_MANA_TO_GENERIC_POWER:3"
            ],
            Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(ancient["optionalCostChoices"])
                .Select(choice => choice.Id)
                .ToArray());
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(ancient["paymentResourceChoices"]));

        var hextech = Requirement(sourceRequirements, P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId);
        Assert.Equal(HextechAnomalyObjectId, hextech["sourceObjectId"]);
        Assert.Equal("generic-power-to-mana", hextech["conversionKind"]);
        Assert.Equal(3, hextech["maxConversionAmount"]);
        Assert.True(Assert.IsType<bool>(hextech["ordinaryGenericPowerOnly"]));
        Assert.Equal(P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix, hextech["conversionChoicePrefix"]);
        Assert.Equal("rune-pool-mana-reset-at-turn-cleanup", hextech["resourceLifecycle"]);
        Assert.Equal("no-ordinary-stack-item", hextech["stackPolicy"]);
        Assert.Equal(
            [
                "CONVERT_GENERIC_POWER_TO_MANA:1",
                "CONVERT_GENERIC_POWER_TO_MANA:2",
                "CONVERT_GENERIC_POWER_TO_MANA:3"
            ],
            Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(hextech["optionalCostChoices"])
                .Select(choice => choice.Id)
                .ToArray());
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(hextech["paymentResourceChoices"]));
    }

    [Fact]
    public async Task EnergyChannelReactionCommandExhaustsSourceGainsManaWithoutStackItem()
    {
        var result = await ResolveAsync(
            BuildPriorityState(RunePool.Empty),
            EnergyChannelObjectId,
            P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects[EnergyChannelObjectId].IsExhausted);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Equal([PendingStackItemId], result.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["conversionKind"] as string, "gain-mana", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnergyChannelGeneratedResourceManaCannotBeSpentTwiceWithoutMutation()
    {
        var gained = await ResolveAsync(
            BuildPriorityState(RunePool.Empty),
            EnergyChannelObjectId,
            P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId);
        Assert.True(gained.Accepted, gained.ErrorMessage);

        var pendingPayment = new PendingPaymentState(
            "PAY-ENERGY-CHANNEL-MANA-1",
            "TEST_PENDING_PAY_COST",
            "P1",
            manaCost: 1,
            legalPaymentChoiceIds: ["SPEND_MANA:1"]);
        var paymentState = gained.State with
        {
            PendingPayment = pendingPayment
        };
        var paid = await new CoreRuleEngine().ResolveAsync(
            paymentState,
            new PlayerIntent("intent-energy-channel-generated-mana-first", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(RunePool.Empty, paid.State.RunePools["P1"]);
        Assert.Equal([PendingStackItemId], paid.State.StackItems.Select(item => item.StackItemId).ToArray());
        var afterSpendHash = MatchStateHasher.Hash(paid.State);

        var duplicate = await new CoreRuleEngine().ResolveAsync(
            paid.State,
            new PlayerIntent("intent-energy-channel-generated-mana-second", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, ["SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.False(duplicate.Accepted);
        Assert.Empty(duplicate.Events);
        Assert.Equal(afterSpendHash, MatchStateHasher.Hash(duplicate.State));
        Assert.Equal(RunePool.Empty, duplicate.State.RunePools["P1"]);
        Assert.Equal([PendingStackItemId], duplicate.State.StackItems.Select(item => item.StackItemId).ToArray());
    }

    [Fact]
    public async Task AncientSteleConvertsManaToGenericTemporaryPaymentResource()
    {
        var result = await ResolveAsync(
            BuildPriorityState(new RunePool(3, 0)),
            AncientSteleObjectId,
            P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId,
            optionalCosts: ["CONVERT_MANA_TO_GENERIC_POWER:2"]);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects[AncientSteleObjectId].IsExhausted);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        Assert.Equal([PendingStackItemId], result.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal(AncientSteleObjectId, temporaryResource.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId, temporaryResource.AbilityId);
        Assert.Equal(2, temporaryResource.GeneratedPower);
        Assert.Equal(2, temporaryResource.RemainingPower);
        Assert.Empty(temporaryResource.GeneratedPowerByTrait);
        Assert.Empty(temporaryResource.RemainingPowerByTrait);

        var powerEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.AncientStelePaymentOnlyResourceRestriction, powerEvent.Payload["resourceRestriction"]);
        Assert.Equal("mana-to-generic-power", powerEvent.Payload["conversionKind"]);
    }

    [Fact]
    public async Task AncientSteleResourceConversionStalePromptReplayUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildPriorityState(new RunePool(3, 0));
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.ActivateAbility, prompt.Actions);
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Contains(activateCandidate.Sources ?? [], source => string.Equals(source.Id, AncientSteleObjectId, StringComparison.Ordinal));

        var command = Command(
            AncientSteleObjectId,
            P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId,
            optionalCosts: ["CONVERT_MANA_TO_GENERIC_POWER:2"]);
        var staleRawCommand = PromptScopedActivateAbilityRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedActivateAbilityRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-ancient-stele-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-ancient-stele-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(
            ["ABILITY_ACTIVATED", "UNIT_EXHAUSTED", "COST_PAID", "POWER_GAINED"],
            accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedTemporaryResourceId = AssertAncientSteleAcceptedEffects(accepted, expectedTemporaryResourceCount: 1);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedSessionPromptHash = MatchStateHasher.HashValue(session.PromptFor("P1"));
        var acceptedSessionSnapshotHash = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        Assert.Equal(acceptedSessionPromptHash, MatchStateHasher.HashValue(accepted.Prompts["P1"]));
        Assert.Equal(acceptedSessionSnapshotHash, MatchStateHasher.HashValue(accepted.Snapshots["P1"]));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ActivateAbility, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        AssertPromptScopedActivateAbilityRawCommand(acceptedJournalEntry.RawCommand.Value, command, prompt);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
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
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(accepted.State.Tick, replay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(acceptedSessionPromptHash, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(acceptedSessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(acceptedTemporaryResourceId, AssertAncientSteleAcceptedEffects(replay, expectedTemporaryResourceCount: 1));

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ActivateAbility, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        AssertPromptScopedActivateAbilityRawCommand(rejectedJournalEntry.RawCommand.Value, command, prompt);
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
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(duplicateReplay.State));
        Assert.Equal(replay.State.Tick, duplicateReplay.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        Assert.Equal(acceptedTemporaryResourceId, AssertAncientSteleAcceptedEffects(duplicateReplay, expectedTemporaryResourceCount: 1));
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
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(acceptedTemporaryResourceId, AssertAncientSteleAcceptedEffects(conflict, expectedTemporaryResourceCount: 1));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AncientSteleTemporaryGenericResourcePaysGenericRuneCostButRejectsManaOnly()
    {
        var resourceState = (await ResolveAsync(
            BuildPriorityState(new RunePool(2, 0)),
            AncientSteleObjectId,
            P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId,
            optionalCosts: ["CONVERT_MANA_TO_GENERIC_POWER:2"])).State;
        var temporaryResource = Assert.Single(resourceState.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var genericPayment = new PendingPaymentState(
            "PAY-GENERIC-2",
            "TEST_PENDING_PAY_COST",
            "P1",
            powerCost: 2,
            legalPaymentChoiceIds: ["SPEND_POWER:any:2"]);
        var genericState = resourceState with
        {
            PendingPayment = genericPayment
        };

        var prompt = ResolutionResult.BuildPrompts(genericState)["P1"];
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var payCostCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(payCostCandidate.Metadata);
        var paymentResourceChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(
            metadata["paymentResourceChoices"]);
        Assert.Equal([resourceAction], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        Assert.Equal([resourceAction], Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["paymentResourceActionIds"]));
        Assert.Equal(2, Assert.IsType<int>(metadata["availablePowerWithPaymentResources"]));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        Assert.Equal(2, paymentResourcePowerByChoice[resourceAction]["power"]);
        Assert.Equal(true, paymentResourcePowerByChoice[resourceAction]["paymentOnly"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(paymentResourcePowerByChoice[resourceAction]["powerByTrait"]));

        var genericResult = await new CoreRuleEngine().ResolveAsync(
            genericState,
            new PlayerIntent("intent-ancient-stele-pay-generic", "P1", CommandTypes.PayCost),
            new PayCostCommand(genericPayment.PaymentId, genericPayment.PaymentWindow, [resourceAction, "SPEND_POWER:any:2"]),
            CancellationToken.None);

        Assert.True(genericResult.Accepted, genericResult.ErrorMessage);
        Assert.Null(genericResult.State.PendingPayment);
        Assert.Empty(genericResult.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, genericResult.State.RunePools["P1"]);
        Assert.Equal(
            ["TEMPORARY_PAYMENT_RESOURCE_SPENT", "TEMPORARY_PAYMENT_RESOURCE_CLEARED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
            genericResult.Events.Select(gameEvent => gameEvent.Kind));

        var spentEvent = Assert.Single(genericResult.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.Equal(genericPayment.PaymentId, spentEvent.Payload["paymentId"]);
        Assert.Equal(genericPayment.PaymentWindow, spentEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", spentEvent.Payload["playerId"]);
        Assert.Equal(temporaryResource.ResourceId, spentEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(AncientSteleObjectId, spentEvent.Payload["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId, spentEvent.Payload["abilityId"]);
        Assert.Equal(2, spentEvent.Payload["consumedPower"]);
        Assert.Equal(0, spentEvent.Payload["remainingPower"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(spentEvent.Payload["allowedPaymentKinds"]));
        Assert.Equal(true, spentEvent.Payload["paymentOnly"]);

        var cleanupEvent = Assert.Single(genericResult.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
        Assert.Equal(genericPayment.PaymentId, cleanupEvent.Payload["paymentId"]);
        Assert.Equal(genericPayment.PaymentWindow, cleanupEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", cleanupEvent.Payload["playerId"]);
        Assert.Equal(temporaryResource.ResourceId, cleanupEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(0, cleanupEvent.Payload["remainingPowerBeforeCleanup"]);
        Assert.Equal(true, cleanupEvent.Payload["paymentOnly"]);

        var costEvent = Assert.Single(genericResult.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(genericPayment.PaymentId, costEvent.Payload["paymentId"]);
        Assert.Equal(genericPayment.PaymentWindow, costEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", costEvent.Payload["playerId"]);
        Assert.Equal([resourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([resourceAction, "SPEND_POWER:any:2"], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Equal(["SPEND_POWER:any:2"], Assert.IsType<string[]>(costEvent.Payload["legalPaymentChoiceIds"]));
        Assert.Equal([temporaryResource.ResourceId], Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
        Assert.Equal(2, costEvent.Payload["temporaryPaymentResourcePower"]);
        Assert.Equal(2, costEvent.Payload["power"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]));
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["remainingPowerByTrait"]));

        var paymentWindowClosedEvent = Assert.Single(genericResult.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.Equal(genericPayment.PaymentId, paymentWindowClosedEvent.Payload["paymentId"]);
        Assert.Equal(genericPayment.PaymentWindow, paymentWindowClosedEvent.Payload["paymentWindow"]);

        resourceState = (await ResolveAsync(
            BuildPriorityState(new RunePool(2, 0)),
            AncientSteleObjectId,
            P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId,
            optionalCosts: ["CONVERT_MANA_TO_GENERIC_POWER:2"])).State;
        temporaryResource = Assert.Single(resourceState.TemporaryPaymentResources);
        resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var manaPayment = new PendingPaymentState(
            "PAY-MANA-1",
            "TEST_PENDING_PAY_COST",
            "P1",
            manaCost: 1,
            legalPaymentChoiceIds: ["SPEND_MANA:1"]);
        var manaState = resourceState with
        {
            PendingPayment = manaPayment
        };
        var initialHash = MatchStateHasher.Hash(manaState);

        var manaResult = await new CoreRuleEngine().ResolveAsync(
            manaState,
            new PlayerIntent("intent-ancient-stele-reject-mana-only", "P1", CommandTypes.PayCost),
            new PayCostCommand(manaPayment.PaymentId, manaPayment.PaymentWindow, [resourceAction, "SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.False(manaResult.Accepted);
        Assert.Equal(initialHash, MatchStateHasher.Hash(manaResult.State));
        Assert.Empty(manaResult.Events);
    }

    [Fact]
    public async Task HextechAnomalyConvertsOrdinaryGenericPowerToManaWithoutStackItem()
    {
        var result = await ResolveAsync(
            BuildPriorityState(new RunePool(0, 3)),
            HextechAnomalyObjectId,
            P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId,
            optionalCosts: ["CONVERT_GENERIC_POWER_TO_MANA:2"]);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects[HextechAnomalyObjectId].IsExhausted);
        Assert.Equal(2, result.State.RunePools["P1"].Mana);
        Assert.Equal(1, result.State.RunePools["P1"].Power);
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Equal([PendingStackItemId], result.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["conversionKind"] as string, "generic-power-to-mana", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ancient-missing")]
    [InlineData("ancient-zero")]
    [InlineData("ancient-negative")]
    [InlineData("ancient-overpay")]
    [InlineData("ancient-wrong-optional")]
    [InlineData("hextech-missing")]
    [InlineData("hextech-overpay")]
    [InlineData("hextech-target")]
    [InlineData("hextech-temporary-resource")]
    [InlineData("hextech-temp-resource-chain")]
    [InlineData("energy-target")]
    [InlineData("energy-optional")]
    [InlineData("wrong-timing")]
    [InlineData("wrong-card")]
    [InlineData("exhausted")]
    public async Task ResourceConversionEquipmentRejectsInvalidTimingSourceOrPayloadWithoutMutation(string caseName)
    {
        var state = BuildInvalidState(caseName);
        var command = caseName switch
        {
            "ancient-missing" => Command(AncientSteleObjectId, P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId),
            "ancient-zero" => Command(AncientSteleObjectId, P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId, optionalCosts: ["CONVERT_MANA_TO_GENERIC_POWER:0"]),
            "ancient-negative" => Command(AncientSteleObjectId, P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId, optionalCosts: ["CONVERT_MANA_TO_GENERIC_POWER:-1"]),
            "ancient-overpay" => Command(AncientSteleObjectId, P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId, optionalCosts: ["CONVERT_MANA_TO_GENERIC_POWER:4"]),
            "ancient-wrong-optional" => Command(AncientSteleObjectId, P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId, optionalCosts: ["SPEND_MANA:1"]),
            "hextech-missing" => Command(HextechAnomalyObjectId, P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId),
            "hextech-overpay" => Command(HextechAnomalyObjectId, P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId, optionalCosts: ["CONVERT_GENERIC_POWER_TO_MANA:4"]),
            "hextech-target" => Command(HextechAnomalyObjectId, P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId, targetObjectIds: ["P2-ANY-TARGET"], optionalCosts: ["CONVERT_GENERIC_POWER_TO_MANA:1"]),
            "hextech-temporary-resource" => Command(HextechAnomalyObjectId, P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId, optionalCosts: ["TEMP_PAYMENT_RESOURCE:ANY"]),
            "hextech-temp-resource-chain" => Command(HextechAnomalyObjectId, P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId, optionalCosts: ["CONVERT_GENERIC_POWER_TO_MANA:1"]),
            "energy-target" => Command(EnergyChannelObjectId, P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId, targetObjectIds: ["P2-ANY-TARGET"]),
            "energy-optional" => Command(EnergyChannelObjectId, P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId, optionalCosts: ["CONVERT_GENERIC_POWER_TO_MANA:1"]),
            "wrong-card" => Command(EnergyChannelObjectId, P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId),
            "exhausted" => Command(EnergyChannelObjectId, P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId),
            _ => Command(EnergyChannelObjectId, P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId)
        };
        var expectedErrorCode = caseName switch
        {
            "wrong-timing" => ErrorCodes.PhaseNotAllowed,
            "wrong-card" => ErrorCodes.UnsupportedCardBehavior,
            "ancient-overpay" or "hextech-overpay" or "hextech-temp-resource-chain" => ErrorCodes.InsufficientCost,
            _ => ErrorCodes.InvalidTarget
        };

        await AssertRejectedNoMutationAsync(state, command, expectedErrorCode);
    }

    private static void AssertResourceConversionDefinition(
        string abilityId,
        string sourceCardNo,
        string effectKind,
        int generatedMana,
        bool paymentOnly)
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(abilityId, out var ability));
        Assert.Equal(sourceCardNo, ability.SourceCardNo);
        Assert.Equal(effectKind, ability.EffectKind);
        Assert.True(ability.IsResourceSkill);
        Assert.True(ability.ReactionSpeed);
        Assert.True(ability.ExhaustsSourceAsCost);
        Assert.True(ability.RequiresBaseEquipmentSource);
        Assert.False(ability.RequiresBattlefieldSource);
        Assert.Equal(0, ability.RequiredTargetCount);
        Assert.Equal(generatedMana, ability.GeneratedMana);
        Assert.Equal(paymentOnly, ability.PaymentOnlyResource);
    }

    private static IReadOnlyDictionary<string, object?> Requirement(
        IReadOnlyDictionary<string, object?>[] sourceRequirements,
        string abilityId)
    {
        return Assert.Single(sourceRequirements, entry =>
            string.Equals(entry["abilityId"] as string, abilityId, StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> ResolveAsync(
        MatchState state,
        string sourceObjectId,
        string abilityId,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-resource-conversion-{abilityId}", "P1", CommandTypes.ActivateAbility),
            Command(sourceObjectId, abilityId, optionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        ActivateAbilityCommand command,
        string expectedErrorCode)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-resource-conversion-reject-{expectedErrorCode}", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    private static ActivateAbilityCommand Command(
        string sourceObjectId,
        string abilityId,
        IReadOnlyList<string>? targetObjectIds = null,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(sourceObjectId, abilityId, targetObjectIds ?? [], optionalCosts);
    }

    private static JsonElement PromptScopedActivateAbilityRawCommand(
        ActivateAbilityCommand command,
        ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = command.CmdType,
            sourceObjectId = command.SourceObjectId,
            abilityId = command.AbilityId,
            targetObjectIds = command.TargetObjectIds,
            optionalCosts = command.OptionalCosts ?? [],
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
    }

    private static JsonElement PromptScopedActivateAbilityRawCommandWithClientNote(
        ActivateAbilityCommand command,
        ActionPromptDto prompt,
        string clientNote)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = command.CmdType,
            sourceObjectId = command.SourceObjectId,
            abilityId = command.AbilityId,
            targetObjectIds = command.TargetObjectIds,
            optionalCosts = command.OptionalCosts ?? [],
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote
        });
    }

    private static void AssertPromptScopedActivateAbilityRawCommand(
        JsonElement rawCommand,
        ActivateAbilityCommand command,
        ActionPromptDto prompt)
    {
        Assert.Equal(CommandTypes.ActivateAbility, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(command.SourceObjectId, rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal(command.AbilityId, rawCommand.GetProperty("abilityId").GetString());
        Assert.Equal(
            command.TargetObjectIds,
            rawCommand.GetProperty("targetObjectIds").EnumerateArray().Select(target => target.GetString()!).ToArray());
        Assert.Equal(
            command.OptionalCosts ?? [],
            rawCommand.GetProperty("optionalCosts").EnumerateArray().Select(optionalCost => optionalCost.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static string AssertAncientSteleAcceptedEffects(
        ResolutionResult result,
        int expectedTemporaryResourceCount)
    {
        Assert.True(result.State.CardObjects[AncientSteleObjectId].IsExhausted);
        Assert.Equal(1, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        Assert.Equal([PendingStackItemId], result.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        Assert.Equal(expectedTemporaryResourceCount, result.State.TemporaryPaymentResources.Count);
        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal(AncientSteleObjectId, temporaryResource.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId, temporaryResource.AbilityId);
        Assert.Equal("ACTIVATE_ABILITY", temporaryResource.PaymentWindow);
        Assert.Equal(2, temporaryResource.GeneratedPower);
        Assert.Equal(2, temporaryResource.RemainingPower);
        Assert.Empty(temporaryResource.GeneratedPowerByTrait);
        Assert.Empty(temporaryResource.RemainingPowerByTrait);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], temporaryResource.AllowedPaymentKinds);

        if (result.Events.Count > 0)
        {
            var powerEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
            Assert.Equal("P1", powerEvent.Payload["playerId"]);
            Assert.Equal(AncientSteleObjectId, powerEvent.Payload["sourceObjectId"]);
            Assert.Equal(P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId, powerEvent.Payload["abilityId"]);
            Assert.Equal(true, powerEvent.Payload["paymentOnly"]);
            Assert.Equal(2, powerEvent.Payload["generatedPower"]);
            Assert.Equal(2, powerEvent.Payload["power"]);
            Assert.Equal(2, powerEvent.Payload["remainingPower"]);
            Assert.Equal(P4ActivatedAbilityCatalog.AncientStelePaymentOnlyResourceRestriction, powerEvent.Payload["resourceRestriction"]);
            Assert.Equal("temporary-payment-resource-ledger", powerEvent.Payload["restrictionLifecycle"]);
            Assert.Equal(temporaryResource.ResourceId, powerEvent.Payload["temporaryPaymentResourceId"]);
            Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(powerEvent.Payload["allowedPaymentKinds"]));
            Assert.Equal("mana-to-generic-power", powerEvent.Payload["conversionKind"]);
        }

        return temporaryResource.ResourceId;
    }

    private static MatchState BuildInvalidState(string caseName)
    {
        var state = BuildPriorityState(new RunePool(3, 3));
        return caseName switch
        {
            "wrong-timing" => state with
            {
                TimingState = TimingStates.NeutralOpen,
                PriorityPlayerId = null,
                StackItems = []
            },
            "wrong-card" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    EnergyChannelObjectId,
                    state.CardObjects[EnergyChannelObjectId] with { CardNo = P4ActivatedAbilityCatalog.AncientSteleCardNo })
            },
            "exhausted" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    EnergyChannelObjectId,
                    state.CardObjects[EnergyChannelObjectId] with { IsExhausted = true })
            },
            "hextech-temp-resource-chain" => state with
            {
                RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
                {
                    ["P1"] = RunePool.Empty,
                    ["P2"] = RunePool.Empty
                },
                TemporaryPaymentResources =
                [
                    new TemporaryPaymentResourceState(
                        "ANCIENT_STELE:TEMP-HEXTECH-CHAIN",
                        "P1",
                        AncientSteleObjectId,
                        P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId,
                        "ACTIVATE_ABILITY",
                        generatedPower: 1,
                        remainingPower: 1,
                        allowedPaymentKinds: [PaymentCostRules.RuneCostPaymentKind],
                        createdTick: 0)
                ]
            },
            _ => state
        };
    }

    private static MatchState BuildPriorityState(RunePool runePool)
    {
        return new MatchState(
            "room-resource-conversion-equipment",
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
                ["P1"] = runePool,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base =
                    [
                        EnergyChannelObjectId,
                        AncientSteleObjectId,
                        HextechAnomalyObjectId
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [EnergyChannelObjectId] = Equipment(
                    EnergyChannelObjectId,
                    P4ActivatedAbilityCatalog.EnergyChannelCardNo,
                    "P1"),
                [AncientSteleObjectId] = Equipment(
                    AncientSteleObjectId,
                    P4ActivatedAbilityCatalog.AncientSteleCardNo,
                    "P1"),
                [HextechAnomalyObjectId] = Equipment(
                    HextechAnomalyObjectId,
                    P4ActivatedAbilityCatalog.HextechAnomalyCardNo,
                    "P1"),
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
                [EnergyChannelObjectId] = new("P1", "BASE"),
                [AncientSteleObjectId] = new("P1", "BASE"),
                [HextechAnomalyObjectId] = new("P1", "BASE"),
                [PendingSpellObjectId] = new("P2", "STACK")
            });
    }

    private static CardObjectState Equipment(
        string objectId,
        string cardNo,
        string playerId)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.EquipmentCard],
            cardNo: cardNo,
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
