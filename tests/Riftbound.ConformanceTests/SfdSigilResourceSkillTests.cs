using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SfdSigilResourceSkillTests
{
    private const string PendingSpellObjectId = "P2-PENDING-SPELL";
    private const string PendingStackItemId = "STACK-P2-PENDING-SPELL";

    public static IEnumerable<object[]> RemainingSfdSigilProfiles()
    {
        return P4ActivatedAbilityCatalog.GetSfdSigilTypedResourceProfiles()
            .Where(profile => !string.Equals(profile.AbilityId, P4ActivatedAbilityCatalog.RageSigilResourceAbilityId, StringComparison.Ordinal))
            .Select(profile => new object[] { profile });
    }

    [Theory]
    [MemberData(nameof(RemainingSfdSigilProfiles))]
    public void CatalogExposesRemainingSfdSigilTypedReactionResourceSkills(P4SigilTypedResourceProfile profile)
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(profile.AbilityId, out var ability));

        Assert.Equal(profile.SourceCardNo, ability.SourceCardNo);
        Assert.Equal(profile.EffectKind, ability.EffectKind);
        Assert.True(ability.IsResourceSkill);
        Assert.True(ability.PaymentOnlyResource);
        Assert.True(ability.ReactionSpeed);
        Assert.True(ability.ExhaustsSourceAsCost);
        Assert.True(ability.RequiresBaseEquipmentSource);
        Assert.False(ability.RequiresBattlefieldSource);
        Assert.Equal(0, ability.RequiredTargetCount);
        Assert.Equal(profile.ResourceRestriction, ability.ResourceRestriction);
        var generatedPowerByTrait = P4ActivatedAbilityCatalog.GeneratedPowerByTraitForAbility(ability);
        Assert.Equal(1, generatedPowerByTrait[profile.Trait]);
    }

    [Fact]
    public void SfdSigilReactionPromptExposesBaseEquipmentTypedPaymentOnlyResourceSkills()
    {
        var state = BuildSigilPriorityState(P4ActivatedAbilityCatalog.GetSfdSigilTypedResourceProfiles());
        var prompts = ResolutionResult.BuildPrompts(state);
        var activateCandidate = Assert.Single(
            prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();

        foreach (var profile in P4ActivatedAbilityCatalog.GetSfdSigilTypedResourceProfiles())
        {
            var sourceObjectId = SourceObjectId(profile);
            Assert.Contains(activateCandidate.Sources ?? [], choice => string.Equals(choice.Id, sourceObjectId, StringComparison.Ordinal));
            var requirement = Assert.Single(sourceRequirements, entry =>
                string.Equals(entry["abilityId"] as string, profile.AbilityId, StringComparison.Ordinal));
            Assert.Equal(sourceObjectId, requirement["sourceObjectId"]);
            Assert.Equal(profile.SourceCardNo, requirement["cardNo"]);
            Assert.Equal(0, requirement["minTargetCount"]);
            Assert.Equal(0, requirement["maxTargetCount"]);
            Assert.True(Assert.IsType<bool>(requirement["resourceSkill"]));
            Assert.True(Assert.IsType<bool>(requirement["reactionSpeed"]));
            Assert.True(Assert.IsType<bool>(requirement["paymentOnly"]));
            Assert.True(Assert.IsType<bool>(requirement["typedPaymentOnlyResource"]));
            Assert.True(Assert.IsType<bool>(requirement["requiresBaseEquipmentSource"]));
            Assert.True(Assert.IsType<bool>(requirement["exhaustsSource"]));
            Assert.True(Assert.IsType<bool>(requirement["resolvesImmediately"]));
            Assert.Equal(profile.ResourceRestriction, requirement["resourceRestriction"]);
            Assert.Equal("stack-priority-reaction-representative", requirement["timingPolicy"]);
            Assert.Equal("resolves-immediately-without-stack-item", requirement["reactionPolicy"]);
            Assert.Equal("no-ordinary-stack-item", requirement["stackPolicy"]);
            var generatedPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(requirement["generatedPowerByTrait"]);
            Assert.Equal(1, generatedPowerByTrait[profile.Trait]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]));
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]));
        }
    }

    [Theory]
    [MemberData(nameof(RemainingSfdSigilProfiles))]
    public async Task SfdSigilReactionCommandExhaustsSourceCreatesTypedTemporaryLedgerWithoutStackItem(P4SigilTypedResourceProfile profile)
    {
        var state = BuildSigilPriorityState([profile]);

        var result = await ResolveSigilAsync(state, profile);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.Tick);
        Assert.True(result.State.CardObjects[SourceObjectId(profile)].IsExhausted);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Equal([PendingStackItemId], result.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal(SourceObjectId(profile), temporaryResource.SourceObjectId);
        Assert.Equal(profile.AbilityId, temporaryResource.AbilityId);
        Assert.Equal(0, temporaryResource.GeneratedPower);
        Assert.Equal(0, temporaryResource.RemainingPower);
        Assert.Equal(1, temporaryResource.GeneratedPowerByTrait[profile.Trait]);
        Assert.Equal(1, temporaryResource.RemainingPowerByTrait[profile.Trait]);

        var activatedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal(SourceObjectId(profile), activatedEvent.Payload["sourceObjectId"]);
        Assert.Equal(profile.SourceCardNo, activatedEvent.Payload["cardNo"]);
        Assert.Equal(profile.AbilityId, activatedEvent.Payload["abilityId"]);
        Assert.Equal(profile.EffectKind, activatedEvent.Payload["effectKind"]);
        Assert.Equal(profile.ResourceRestriction, activatedEvent.Payload["resourceRestriction"]);
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["typedPaymentOnlyResource"]));
        Assert.Equal("temporary-payment-resource-ledger", activatedEvent.Payload["resourceLifecycle"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(activatedEvent.Payload["allowedPaymentKinds"]));
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(activatedEvent.Payload["generatedPowerByTrait"])[profile.Trait]);

        var powerEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(SourceObjectId(profile), powerEvent.Payload["sourceObjectId"]);
        Assert.Equal(profile.SourceCardNo, powerEvent.Payload["cardNo"]);
        Assert.Equal(profile.AbilityId, powerEvent.Payload["abilityId"]);
        Assert.Equal(profile.EffectKind, powerEvent.Payload["effectKind"]);
        Assert.Equal(profile.ResourceRestriction, powerEvent.Payload["resourceRestriction"]);
        Assert.Equal(temporaryResource.ResourceId, powerEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal("temporary-payment-resource-ledger", powerEvent.Payload["restrictionLifecycle"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(powerEvent.Payload["allowedPaymentKinds"]));
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(powerEvent.Payload["generatedPowerByTrait"])[profile.Trait]);
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(powerEvent.Payload["powerByTrait"])[profile.Trait]);
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(powerEvent.Payload["remainingPowerByTrait"])[profile.Trait]);
    }

    [Theory]
    [MemberData(nameof(RemainingSfdSigilProfiles))]
    public async Task SfdSigilReactionResourceSkillStalePromptReplayAfterTypedTemporaryLedgerUsesRejectedCache(P4SigilTypedResourceProfile profile)
    {
        var journal = new RecordingMatchJournal();
        var state = BuildSigilPriorityState([profile]);
        var command = SigilCommand(profile);
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.ActivateAbility, prompt.Actions);
        AssertSfdSigilPromptExposesProfile(prompt, profile);
        var staleRawCommand = PromptScopedActivateAbilityRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedActivateAbilityRawCommandWithClientNote(
            command,
            prompt,
            "changed-payload");
        var acceptedClientIntentId = $"intent-sfd-sigil-{profile.ResourceIdPrefix}-before-stale-prompt-replay";
        var staleClientIntentId = $"intent-sfd-sigil-{profile.ResourceIdPrefix}-stale-prompt-replay";

        var gained = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(gained.Accepted, gained.ErrorMessage);
        Assert.Equal(1, gained.State.Tick);
        Assert.True(gained.State.CardObjects[SourceObjectId(profile)].IsExhausted);
        Assert.Equal(RunePool.Empty, gained.State.RunePools["P1"]);
        var temporaryResource = Assert.Single(gained.State.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal(SourceObjectId(profile), temporaryResource.SourceObjectId);
        Assert.Equal(profile.AbilityId, temporaryResource.AbilityId);
        Assert.Equal(0, temporaryResource.GeneratedPower);
        Assert.Equal(0, temporaryResource.RemainingPower);
        Assert.Equal(1, temporaryResource.GeneratedPowerByTrait[profile.Trait]);
        Assert.Equal(1, temporaryResource.RemainingPowerByTrait[profile.Trait]);
        Assert.Equal([PendingStackItemId], gained.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Null(gained.State.PendingPayment);
        Assert.Single(gained.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Single(gained.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.DoesNotContain(gained.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        AssertNoSfdSigilPrompt(gained.Prompts["P1"]);
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
        AssertSfdSigilRawCommand(acceptedJournalEntry.RawCommand.Value, command, prompt);
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
        Assert.Equal(gained.State.Tick, replay.State.Tick);
        Assert.Equal(postGainPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(postGainSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(gained.State.RunePools["P1"], replay.State.RunePools["P1"]);
        Assert.True(replay.State.CardObjects[SourceObjectId(profile)].IsExhausted);
        Assert.Single(replay.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.TemporaryPaymentResources, replay.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.StackItems, replay.State.StackItems);
        Assert.Equal(gained.State.PendingTaskQueue.Phase, replay.State.PendingTaskQueue.Phase);
        Assert.Equal(gained.State.PendingTaskQueue.ActiveTaskId, replay.State.PendingTaskQueue.ActiveTaskId);
        Assert.Equal(gained.State.PendingTaskQueue.Tasks, replay.State.PendingTaskQueue.Tasks);
        Assert.Null(replay.State.PendingPayment);
        AssertNoSfdSigilPrompt(replay.Prompts["P1"]);

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ActivateAbility, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(gained.State.Tick, rejectedJournalEntry.StartedTick);
        Assert.Equal(replay.State.Tick, rejectedJournalEntry.CompletedTick);
        Assert.Equal(MatchStateHasher.Hash(replay.State), MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(MatchStateHasher.HashValue(replay.Prompts), MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(MatchStateHasher.HashValue(replay.Snapshots), MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        AssertSfdSigilRawCommand(rejectedJournalEntry.RawCommand.Value, command, prompt);
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

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
        Assert.Equal(postGainHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(replay.State.Tick, duplicateRejected.State.Tick);
        Assert.Equal(postGainPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(postGainSnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        Assert.Equal(gained.State.RunePools["P1"], duplicateRejected.State.RunePools["P1"]);
        Assert.True(duplicateRejected.State.CardObjects[SourceObjectId(profile)].IsExhausted);
        Assert.Single(duplicateRejected.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.TemporaryPaymentResources, duplicateRejected.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.StackItems, duplicateRejected.State.StackItems);
        Assert.Null(duplicateRejected.State.PendingPayment);
        AssertNoSfdSigilPrompt(duplicateRejected.Prompts["P1"]);
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
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(postGainPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(postGainSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(gained.State.RunePools["P1"], conflict.State.RunePools["P1"]);
        Assert.True(conflict.State.CardObjects[SourceObjectId(profile)].IsExhausted);
        Assert.Single(conflict.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.TemporaryPaymentResources, conflict.State.TemporaryPaymentResources);
        Assert.Equal(gained.State.StackItems, conflict.State.StackItems);
        Assert.Null(conflict.State.PendingPayment);
        AssertNoSfdSigilPrompt(conflict.Prompts["P1"]);
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(RemainingSfdSigilProfiles))]
    public async Task SfdSigilTemporaryTypedResourcePaysSameColorAndGenericRuneCosts(P4SigilTypedResourceProfile profile)
    {
        foreach (var caseName in new[] { "typed", "generic" })
        {
            var resourceState = (await ResolveSigilAsync(BuildSigilPriorityState([profile]), profile)).State;
            var temporaryResource = Assert.Single(resourceState.TemporaryPaymentResources);
            var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
            var pendingPayment = string.Equals(caseName, "typed", StringComparison.Ordinal)
                ? new PendingPaymentState(
                    $"PAY-{profile.Trait.ToUpperInvariant()}-1",
                    "TEST_PENDING_PAY_COST",
                    "P1",
                    powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [profile.Trait] = 1
                    },
                    legalPaymentChoiceIds: [$"SPEND_POWER:{profile.Trait}:1"])
                : new PendingPaymentState(
                    "PAY-GENERIC-1",
                    "TEST_PENDING_PAY_COST",
                    "P1",
                    powerCost: 1,
                    legalPaymentChoiceIds: ["SPEND_POWER:any:1"]);
            var state = resourceState with
            {
                PendingPayment = pendingPayment
            };
            var prompt = ResolutionResult.BuildPrompts(state)["P1"];
            var payCostCandidate = Assert.Single(
                prompt.Candidates ?? [],
                candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
            var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(payCostCandidate.Metadata);
            var paymentResourceChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(
                metadata["paymentResourceChoices"]);
            Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, resourceAction, StringComparison.Ordinal));
            Assert.Equal([resourceAction], Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["paymentResourceActionIds"]));
            var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
                metadata["paymentResourcePowerByChoice"]);
            Assert.Equal(0, paymentResourcePowerByChoice[resourceAction]["power"]);
            Assert.Equal(profile.Trait, paymentResourcePowerByChoice[resourceAction]["trait"]);
            Assert.Equal(true, paymentResourcePowerByChoice[resourceAction]["paymentOnly"]);
            var quotedPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
                paymentResourcePowerByChoice[resourceAction]["powerByTrait"]);
            Assert.Equal(1, quotedPowerByTrait[profile.Trait]);

            var spendChoice = string.Equals(caseName, "typed", StringComparison.Ordinal)
                ? $"SPEND_POWER:{profile.Trait}:1"
                : "SPEND_POWER:any:1";

            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-sfd-sigil-pay-{profile.Trait}-{caseName}", "P1", CommandTypes.PayCost),
                new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, spendChoice]),
                CancellationToken.None);

            Assert.True(result.Accepted, result.ErrorMessage);
            Assert.Null(result.State.PendingPayment);
            Assert.Empty(result.State.TemporaryPaymentResources);
            Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
            Assert.Equal(
                ["TEMPORARY_PAYMENT_RESOURCE_SPENT", "TEMPORARY_PAYMENT_RESOURCE_CLEARED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
                result.Events.Select(gameEvent => gameEvent.Kind));

            var spentEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
            Assert.Equal(pendingPayment.PaymentId, spentEvent.Payload["paymentId"]);
            Assert.Equal(pendingPayment.PaymentWindow, spentEvent.Payload["paymentWindow"]);
            Assert.Equal("P1", spentEvent.Payload["playerId"]);
            Assert.Equal(temporaryResource.ResourceId, spentEvent.Payload["temporaryPaymentResourceId"]);
            Assert.Equal(SourceObjectId(profile), spentEvent.Payload["sourceObjectId"]);
            Assert.Equal(profile.AbilityId, spentEvent.Payload["abilityId"]);
            Assert.Equal(0, spentEvent.Payload["consumedPower"]);
            Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(spentEvent.Payload["consumedPowerByTrait"])[profile.Trait]);
            Assert.Equal(0, spentEvent.Payload["remainingPower"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(spentEvent.Payload["remainingPowerByTrait"]));
            Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(spentEvent.Payload["allowedPaymentKinds"]));
            Assert.Equal(true, spentEvent.Payload["paymentOnly"]);

            var cleanupEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
            Assert.Equal(pendingPayment.PaymentId, cleanupEvent.Payload["paymentId"]);
            Assert.Equal(pendingPayment.PaymentWindow, cleanupEvent.Payload["paymentWindow"]);
            Assert.Equal("P1", cleanupEvent.Payload["playerId"]);
            Assert.Equal(temporaryResource.ResourceId, cleanupEvent.Payload["temporaryPaymentResourceId"]);
            Assert.Equal(0, cleanupEvent.Payload["remainingPowerBeforeCleanup"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(cleanupEvent.Payload["remainingPowerByTraitBeforeCleanup"]));
            Assert.Equal(true, cleanupEvent.Payload["paymentOnly"]);

            var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
            Assert.Equal(pendingPayment.PaymentId, costEvent.Payload["paymentId"]);
            Assert.Equal(pendingPayment.PaymentWindow, costEvent.Payload["paymentWindow"]);
            Assert.Equal("P1", costEvent.Payload["playerId"]);
            Assert.Equal([resourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
            Assert.Equal([resourceAction, spendChoice], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
            Assert.Equal([spendChoice], Assert.IsType<string[]>(costEvent.Payload["legalPaymentChoiceIds"]));
            Assert.Equal([temporaryResource.ResourceId], Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
            Assert.Equal(0, costEvent.Payload["temporaryPaymentResourcePower"]);
            Assert.Equal(string.Equals(caseName, "typed", StringComparison.Ordinal) ? 0 : 1, costEvent.Payload["power"]);
            var costPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
            if (string.Equals(caseName, "typed", StringComparison.Ordinal))
            {
                Assert.Equal(1, costPowerByTrait[profile.Trait]);
            }
            else
            {
                Assert.Empty(costPowerByTrait);
            }

            Assert.Equal(0, costEvent.Payload["remainingPower"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["remainingPowerByTrait"]));

            var paymentWindowClosedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
            Assert.Equal(pendingPayment.PaymentId, paymentWindowClosedEvent.Payload["paymentId"]);
            Assert.Equal(pendingPayment.PaymentWindow, paymentWindowClosedEvent.Payload["paymentWindow"]);
        }
    }

    [Fact]
    public async Task SfdSigilTemporaryTypedResourceDoesNotExposeManaOnlyPromptResourceChoices()
    {
        var profile = P4ActivatedAbilityCatalog.GetSfdSigilTypedResourceProfiles()
            .First(profile => !string.Equals(
                profile.AbilityId,
                P4ActivatedAbilityCatalog.RageSigilResourceAbilityId,
                StringComparison.Ordinal));
        var resourceState = (await ResolveSigilAsync(BuildSigilPriorityState([profile]), profile)).State;
        var temporaryResource = Assert.Single(resourceState.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = new PendingPaymentState(
            "PAY-MANA-1",
            "TEST_PENDING_PAY_COST",
            "P1",
            manaCost: 1,
            legalPaymentChoiceIds: ["SPEND_MANA:1"]);
        var state = resourceState with
        {
            PendingPayment = pendingPayment
        };
        var stateHash = MatchStateHasher.Hash(state);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        Assert.Equal(stateHash, MatchStateHasher.Hash(state));
        Assert.Equal(resourceState.TemporaryPaymentResources, state.TemporaryPaymentResources);
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
    }

    [Theory]
    [MemberData(nameof(RemainingSfdSigilProfiles))]
    public async Task SfdSigilTemporaryTypedResourceRejectsWrongColorAndManaOnlyWithoutMutation(P4SigilTypedResourceProfile profile)
    {
        foreach (var caseName in new[] { "wrong-color", "mana-only" })
        {
            var resourceState = (await ResolveSigilAsync(BuildSigilPriorityState([profile]), profile)).State;
            var temporaryResource = Assert.Single(resourceState.TemporaryPaymentResources);
            var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
            var wrongTrait = string.Equals(profile.Trait, RuneTrait.Blue, StringComparison.Ordinal)
                ? RuneTrait.Red
                : RuneTrait.Blue;
            var pendingPayment = string.Equals(caseName, "wrong-color", StringComparison.Ordinal)
                ? new PendingPaymentState(
                    $"PAY-{wrongTrait.ToUpperInvariant()}-1",
                    "TEST_PENDING_PAY_COST",
                    "P1",
                    powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [wrongTrait] = 1
                    },
                    legalPaymentChoiceIds: [$"SPEND_POWER:{wrongTrait}:1"])
                : new PendingPaymentState(
                    "PAY-MANA-1",
                    "TEST_PENDING_PAY_COST",
                    "P1",
                    manaCost: 1,
                    legalPaymentChoiceIds: ["SPEND_MANA:1"]);
            var state = resourceState with
            {
                PendingPayment = pendingPayment
            };
            var initialHash = MatchStateHasher.Hash(state);
            var spendChoice = string.Equals(caseName, "wrong-color", StringComparison.Ordinal)
                ? $"SPEND_POWER:{wrongTrait}:1"
                : "SPEND_MANA:1";

            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-sfd-sigil-reject-pay-{profile.Trait}-{caseName}", "P1", CommandTypes.PayCost),
                new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, spendChoice]),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
            Assert.Empty(result.Events);
        }
    }

    private static async Task<ResolutionResult> ResolveSigilAsync(
        MatchState state,
        P4SigilTypedResourceProfile profile)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-sfd-sigil-resource-skill-{profile.Trait}", "P1", CommandTypes.ActivateAbility),
            SigilCommand(profile),
            CancellationToken.None);
    }

    private static ActivateAbilityCommand SigilCommand(P4SigilTypedResourceProfile profile)
    {
        return new ActivateAbilityCommand(SourceObjectId(profile), profile.AbilityId, []);
    }

    private static void AssertSfdSigilPromptExposesProfile(
        ActionPromptDto prompt,
        P4SigilTypedResourceProfile profile)
    {
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Contains(
            activateCandidate.Sources ?? [],
            source => string.Equals(source.Id, SourceObjectId(profile), StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var requirement = Assert.Single(sourceRequirements, entry =>
            string.Equals(entry["abilityId"] as string, profile.AbilityId, StringComparison.Ordinal));
        Assert.Equal(SourceObjectId(profile), requirement["sourceObjectId"]);
        Assert.True(Assert.IsType<bool>(requirement["typedPaymentOnlyResource"]));
        Assert.True(Assert.IsType<bool>(requirement["paymentOnly"]));
        Assert.True(Assert.IsType<bool>(requirement["reactionSpeed"]));
    }

    private static void AssertNoSfdSigilPrompt(ActionPromptDto prompt)
    {
        Assert.DoesNotContain(CommandTypes.ActivateAbility, prompt.Actions);
        Assert.DoesNotContain(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
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

    private static void AssertSfdSigilRawCommand(
        JsonElement rawCommand,
        ActivateAbilityCommand command,
        ActionPromptDto prompt)
    {
        Assert.Equal(CommandTypes.ActivateAbility, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(command.SourceObjectId, rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal(command.AbilityId, rawCommand.GetProperty("abilityId").GetString());
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Empty(rawCommand.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static MatchState BuildSigilPriorityState(IEnumerable<P4SigilTypedResourceProfile> profiles)
    {
        var profileArray = profiles.ToArray();
        var baseObjectIds = profileArray
            .Select(SourceObjectId)
            .ToArray();
        var cardObjects = profileArray.ToDictionary(
            SourceObjectId,
            profile => Equipment(SourceObjectId(profile), profile.SourceCardNo, "P1"),
            StringComparer.Ordinal);
        cardObjects[PendingSpellObjectId] = new CardObjectState(
            PendingSpellObjectId,
            tags: [CardObjectTags.SpellCard],
            cardNo: "UNL-001/219",
            ownerId: "P2",
            controllerId: "P2");
        var objectLocations = baseObjectIds
            .ToDictionary(
                objectId => objectId,
                _ => new ObjectLocationState("P1", "BASE"),
                StringComparer.Ordinal);
        objectLocations[PendingSpellObjectId] = new ObjectLocationState("P2", "STACK");

        return new MatchState(
            "room-sfd-sigil-resource",
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
                    Base = baseObjectIds
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: cardObjects,
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
            objectLocations: objectLocations);
    }

    private static string SourceObjectId(P4SigilTypedResourceProfile profile)
    {
        return $"P1-{profile.ResourceIdPrefix}";
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
