using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class GoldTokenResourceSkillTests
{
    private const string UnlGoldObjectId = "P1-UNL-GOLD";
    private const string SfdGoldObjectId = "P1-SFD-GOLD";
    private const string PendingSpellObjectId = "P2-PENDING-SPELL";
    private const string PendingStackItemId = "STACK-P2-PENDING-SPELL";

    public static IEnumerable<object[]> GoldTokenAbilities()
    {
        yield return new object[]
        {
            UnlGoldObjectId,
            P4ActivatedAbilityCatalog.GoldTokenUnlCardNo,
            P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId,
            P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityEffectKind
        };
        yield return new object[]
        {
            SfdGoldObjectId,
            P4ActivatedAbilityCatalog.GoldTokenSfdCardNo,
            P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId,
            P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityEffectKind
        };
    }

    [Theory]
    [MemberData(nameof(GoldTokenAbilities))]
    public void CatalogExposesGoldTokenResourceSkillDefinitions(
        string _,
        string cardNo,
        string abilityId,
        string effectKind)
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(abilityId, out var ability));

        Assert.Equal(cardNo, ability.SourceCardNo);
        Assert.Equal(effectKind, ability.EffectKind);
        Assert.True(ability.IsResourceSkill);
        Assert.True(ability.ReactionSpeed);
        Assert.True(ability.PaymentOnlyResource);
        Assert.True(ability.ExhaustsSourceAsCost);
        Assert.True(ability.RequiresBaseEquipmentSource);
        Assert.False(ability.RequiresBattlefieldSource);
        Assert.Equal(0, ability.RequiredTargetCount);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, ability.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenPaymentOnlyResourceRestriction, ability.ResourceRestriction);
    }

    [Fact]
    public void GoldTokenDeferredResourceSurfacesAreRemovedButOtherTokenSurfacesRemain()
    {
        var surfaces = P6TokenFactoryCatalog.GetDeferredRuleSurfaces();

        Assert.DoesNotContain(surfaces, surface => string.Equals(
            surface.SurfaceId,
            "TOKEN_DEFERRED_GOLD_REACTION_DESTROY_EXHAUST_GAIN_A_UNL",
            StringComparison.Ordinal));
        Assert.DoesNotContain(surfaces, surface => string.Equals(
            surface.SurfaceId,
            "TOKEN_DEFERRED_GOLD_REACTION_DESTROY_EXHAUST_GAIN_A_SFD",
            StringComparison.Ordinal));
        Assert.DoesNotContain(surfaces, surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.ImageCopySurfaceId,
            StringComparison.Ordinal));
        Assert.DoesNotContain(surfaces, surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.BrushReplacementSurfaceId,
            StringComparison.Ordinal));
        Assert.DoesNotContain(surfaces, surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.BaronNestMoveStaticSurfaceId,
            StringComparison.Ordinal));
        Assert.Contains(P6TokenFactoryCatalog.GetImplementedRuleSurfaces(), surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.BaronNestMoveStaticSurfaceId,
            StringComparison.Ordinal));
        Assert.Contains(P6TokenFactoryCatalog.GetImplementedRuleSurfaces(), surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.ImageCopySurfaceId,
            StringComparison.Ordinal));
        Assert.Contains(P6TokenFactoryCatalog.GetImplementedRuleSurfaces(), surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.BrushReplacementSurfaceId,
            StringComparison.Ordinal));
    }

    [Fact]
    public void GoldTokenReactionPromptExposesServerFilteredDestroyCostResourceSkills()
    {
        var state = BuildGoldPriorityState();
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();

        foreach (var (sourceObjectId, cardNo, abilityId) in new[]
                 {
                     (UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId),
                     (SfdGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenSfdCardNo, P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId)
                 })
        {
            Assert.Contains(activateCandidate.Sources ?? [], choice => string.Equals(choice.Id, sourceObjectId, StringComparison.Ordinal));
            var requirement = Assert.Single(sourceRequirements, entry =>
                string.Equals(entry["abilityId"] as string, abilityId, StringComparison.Ordinal));
            Assert.Equal(sourceObjectId, requirement["sourceObjectId"]);
            Assert.Equal(cardNo, requirement["cardNo"]);
            Assert.Equal(0, requirement["minTargetCount"]);
            Assert.Equal(0, requirement["maxTargetCount"]);
            Assert.True(Assert.IsType<bool>(requirement["resourceSkill"]));
            Assert.True(Assert.IsType<bool>(requirement["reactionSpeed"]));
            Assert.True(Assert.IsType<bool>(requirement["paymentOnly"]));
            Assert.True(Assert.IsType<bool>(requirement["requiresBaseEquipmentSource"]));
            Assert.True(Assert.IsType<bool>(requirement["usesSourceAsDestroyCost"]));
            Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, requirement["generatedPower"]);
            Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, requirement["generatedGenericPower"]);
            Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenPaymentOnlyResourceRestriction, requirement["resourceRestriction"]);
            Assert.Equal("stack-priority-reaction-representative", requirement["timingPolicy"]);
            Assert.Equal("resolves-immediately-without-stack-item", requirement["reactionPolicy"]);
            Assert.Equal("no-ordinary-stack-item", requirement["stackPolicy"]);
            Assert.Equal("temporary-payment-resource-ledger", requirement["resourceLifecycle"]);
            Assert.False(Assert.IsType<bool>(requirement["renataGoldExtraManaAvailable"]));
            Assert.Equal(0, requirement["bonusMana"]);
            Assert.Equal(string.Empty, requirement["bonusTag"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]));
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]));
        }
    }

    [Fact]
    public void GoldTokenReactionPromptExposesRenataBonusMetadataForMarkedGoldSource()
    {
        var state = WithRenataBonusTag(BuildGoldPriorityState(), UnlGoldObjectId);
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();

        var markedRequirement = Assert.Single(sourceRequirements, entry =>
            string.Equals(entry["abilityId"] as string, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, StringComparison.Ordinal));
        Assert.True(Assert.IsType<bool>(markedRequirement["renataGoldExtraManaAvailable"]));
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenRenataBonusMana, markedRequirement["bonusMana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenRenataBonusTag, markedRequirement["bonusTag"]);

        var ordinaryRequirement = Assert.Single(sourceRequirements, entry =>
            string.Equals(entry["abilityId"] as string, P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId, StringComparison.Ordinal));
        Assert.False(Assert.IsType<bool>(ordinaryRequirement["renataGoldExtraManaAvailable"]));
        Assert.Equal(0, ordinaryRequirement["bonusMana"]);
        Assert.Equal(string.Empty, ordinaryRequirement["bonusTag"]);
    }

    [Fact]
    public void GoldTokenReactionPromptDoesNotExposeToNonPriorityPlayer()
    {
        var state = BuildGoldPriorityState();
        var prompt = ResolutionResult.BuildPrompts(state)["P2"];

        Assert.DoesNotContain(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(GoldTokenAbilities))]
    public async Task GoldTokenResourceSkillDestroysSourceAndCreatesGenericTemporaryLedger(
        string sourceObjectId,
        string _,
        string abilityId,
        string effectKind)
    {
        var state = BuildGoldPriorityState();

        var result = await ResolveGoldAsync(state, sourceObjectId, abilityId);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.Tick);
        Assert.DoesNotContain(sourceObjectId, result.State.CardObjects.Keys);
        Assert.DoesNotContain(sourceObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Contains(sourceObjectId, result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal([PendingStackItemId], result.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal(sourceObjectId, temporaryResource.SourceObjectId);
        Assert.Equal(abilityId, temporaryResource.AbilityId);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, temporaryResource.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, temporaryResource.RemainingPower);
        Assert.Empty(temporaryResource.GeneratedPowerByTrait);
        Assert.Empty(temporaryResource.RemainingPowerByTrait);

        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_EXHAUSTED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, sourceObjectId, StringComparison.Ordinal));
        var activatedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal(effectKind, activatedEvent.Payload["effectKind"]);
        Assert.False(Assert.IsType<bool>(activatedEvent.Payload["renataGoldExtraManaApplied"]));
        Assert.Equal(0, activatedEvent.Payload["generatedMana"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, sourceObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "RESOURCE_SKILL_COST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GoldTokenResourceSkillReplayAfterImmediateActivationRejectsWithoutMutation()
    {
        var state = BuildGoldPriorityState();
        var command = Command(UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId);

        var accepted = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-gold-token-resource-replay-accept", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.DoesNotContain(UnlGoldObjectId, accepted.State.CardObjects.Keys);
        Assert.DoesNotContain(UnlGoldObjectId, accepted.State.PlayerZones["P1"].Base);
        Assert.Contains(UnlGoldObjectId, accepted.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", accepted.State.ObjectLocations[UnlGoldObjectId].Zone);
        Assert.Equal(TimingStates.NeutralClosed, accepted.State.TimingState);
        Assert.Equal([PendingStackItemId], accepted.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P1", accepted.State.PriorityPlayerId);
        var temporaryResource = Assert.Single(accepted.State.TemporaryPaymentResources);
        Assert.Equal(UnlGoldObjectId, temporaryResource.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, temporaryResource.AbilityId);
        Assert.Single(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Single(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_EXHAUSTED", StringComparison.Ordinal));
        Assert.Single(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_DESTROYED", StringComparison.Ordinal));
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.Single(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));

        var postActivationHash = MatchStateHasher.Hash(accepted.State);
        var replay = await new CoreRuleEngine().ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-gold-token-resource-replay-stale", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Empty(replay.Events);
        Assert.Equal(postActivationHash, MatchStateHasher.Hash(replay.State));
        Assert.DoesNotContain(UnlGoldObjectId, replay.State.CardObjects.Keys);
        Assert.DoesNotContain(UnlGoldObjectId, replay.State.PlayerZones["P1"].Base);
        Assert.Contains(UnlGoldObjectId, replay.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", replay.State.ObjectLocations[UnlGoldObjectId].Zone);
        Assert.Equal(TimingStates.NeutralClosed, replay.State.TimingState);
        Assert.Equal([PendingStackItemId], replay.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P1", replay.State.PriorityPlayerId);
        Assert.Single(replay.State.TemporaryPaymentResources);
        Assert.DoesNotContain(replay.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.DoesNotContain(replay.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_EXHAUSTED", StringComparison.Ordinal));
        Assert.DoesNotContain(replay.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_DESTROYED", StringComparison.Ordinal));
        Assert.DoesNotContain(replay.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.DoesNotContain(replay.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GoldTokenResourceSkillStalePromptReplayUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildGoldPriorityState();
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
        Assert.Contains(activateCandidate.Sources ?? [], source => string.Equals(source.Id, UnlGoldObjectId, StringComparison.Ordinal));

        var command = Command(UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId);
        var staleRawCommand = PromptScopedActivateAbilityRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedActivateAbilityRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-gold-token-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-gold-token-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        var acceptedTemporaryResourceId = AssertGoldTokenAcceptedEffects(accepted, expectedTemporaryResourceCount: 1);
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
        Assert.Equal(acceptedTemporaryResourceId, AssertGoldTokenAcceptedEffects(replay, expectedTemporaryResourceCount: 1));

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
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(rejectedJournalEntry.RawCommand.Value));
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
        Assert.Equal(acceptedSessionPromptHash, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(acceptedSessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(acceptedTemporaryResourceId, AssertGoldTokenAcceptedEffects(duplicateReplay, expectedTemporaryResourceCount: 1));
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
        Assert.Equal(acceptedSessionPromptHash, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(acceptedSessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(acceptedTemporaryResourceId, AssertGoldTokenAcceptedEffects(conflict, expectedTemporaryResourceCount: 1));
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GoldTemporaryGenericResourcePaysGenericRuneCostAndCleansUp()
    {
        var resourceState = (await ResolveGoldAsync(
            BuildGoldPriorityState(),
            UnlGoldObjectId,
            P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId)).State;
        var temporaryResource = Assert.Single(resourceState.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = new PendingPaymentState(
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
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, paymentResourcePowerByChoice[resourceAction]["power"]);
        Assert.Equal(true, paymentResourcePowerByChoice[resourceAction]["paymentOnly"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(paymentResourcePowerByChoice[resourceAction]["powerByTrait"]));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-gold-pay-generic", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, "SPEND_POWER:any:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.State.PendingPayment);
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Equal(
            ["TEMPORARY_PAYMENT_RESOURCE_SPENT", "TEMPORARY_PAYMENT_RESOURCE_CLEARED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
            result.Events.Select(gameEvent => gameEvent.Kind));

        var spentEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, spentEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, spentEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", spentEvent.Payload["playerId"]);
        Assert.Equal(temporaryResource.ResourceId, spentEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(UnlGoldObjectId, spentEvent.Payload["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, spentEvent.Payload["abilityId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, spentEvent.Payload["consumedPower"]);
        Assert.Equal(0, spentEvent.Payload["remainingPower"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(spentEvent.Payload["allowedPaymentKinds"]));
        Assert.Equal(true, spentEvent.Payload["paymentOnly"]);

        var cleanupEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, cleanupEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, cleanupEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", cleanupEvent.Payload["playerId"]);
        Assert.Equal(temporaryResource.ResourceId, cleanupEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(0, cleanupEvent.Payload["remainingPowerBeforeCleanup"]);
        Assert.Equal(true, cleanupEvent.Payload["paymentOnly"]);

        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, costEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, costEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", costEvent.Payload["playerId"]);
        Assert.Equal([resourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([resourceAction, "SPEND_POWER:any:1"], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Equal(["SPEND_POWER:any:1"], Assert.IsType<string[]>(costEvent.Payload["legalPaymentChoiceIds"]));
        Assert.Equal([temporaryResource.ResourceId], Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, costEvent.Payload["temporaryPaymentResourcePower"]);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, costEvent.Payload["power"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]));
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["remainingPowerByTrait"]));

        var paymentWindowClosedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, paymentWindowClosedEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, paymentWindowClosedEvent.Payload["paymentWindow"]);
    }

    [Fact]
    public async Task GoldTokenTemporaryResourcesFromBothPrintingsCombineForGenericRuneCostAndCleanEachLedger()
    {
        var unlResult = await ResolveGoldAsync(
            BuildGoldPriorityState(),
            UnlGoldObjectId,
            P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId);
        Assert.True(unlResult.Accepted, unlResult.ErrorMessage);
        var sfdResult = await ResolveGoldAsync(
            unlResult.State,
            SfdGoldObjectId,
            P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId);
        Assert.True(sfdResult.Accepted, sfdResult.ErrorMessage);

        var resourceState = sfdResult.State;
        var temporaryResources = resourceState.TemporaryPaymentResources.ToArray();
        Assert.Equal(2, temporaryResources.Length);
        Assert.Equal([UnlGoldObjectId, SfdGoldObjectId], temporaryResources.Select(resource => resource.SourceObjectId).ToArray());
        Assert.Equal(RunePool.Empty, resourceState.RunePools["P1"]);

        var unlTemporaryResource = temporaryResources[0];
        var sfdTemporaryResource = temporaryResources[1];
        var unlResourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(unlTemporaryResource.ResourceId);
        var sfdResourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(sfdTemporaryResource.ResourceId);
        var totalCost = 2 * P4ActivatedAbilityCatalog.GoldTokenGeneratedPower;
        var spendChoice = $"SPEND_POWER:any:{totalCost}";
        var pendingPayment = new PendingPaymentState(
            "PAY-GENERIC-2",
            "TEST_PENDING_PAY_COST",
            "P1",
            powerCost: totalCost,
            legalPaymentChoiceIds: [spendChoice]);
        var state = resourceState with
        {
            PendingPayment = pendingPayment
        };

        Assert.Equal(UnlGoldObjectId, unlTemporaryResource.SourceObjectId);
        Assert.Equal(SfdGoldObjectId, sfdTemporaryResource.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, unlTemporaryResource.AbilityId);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId, sfdTemporaryResource.AbilityId);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, unlTemporaryResource.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, sfdTemporaryResource.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, unlTemporaryResource.RemainingPower);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, sfdTemporaryResource.RemainingPower);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var payCostCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(payCostCandidate.Metadata);
        var paymentResourceChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(
            metadata["paymentResourceChoices"]);
        Assert.Equal([unlResourceAction, sfdResourceAction], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        Assert.Equal(
            [unlResourceAction, sfdResourceAction],
            Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["paymentResourceActionIds"]));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        foreach (var (resourceAction, temporaryResource) in new[]
                 {
                     (unlResourceAction, unlTemporaryResource),
                     (sfdResourceAction, sfdTemporaryResource)
                 })
        {
            Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, paymentResourcePowerByChoice[resourceAction]["power"]);
            Assert.Equal(true, paymentResourcePowerByChoice[resourceAction]["paymentOnly"]);
            Assert.Equal(temporaryResource.ResourceId, paymentResourcePowerByChoice[resourceAction]["temporaryPaymentResourceId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(paymentResourcePowerByChoice[resourceAction]["powerByTrait"]));
        }

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-gold-pay-two-temporary-generic", "P1", CommandTypes.PayCost),
            new PayCostCommand(
                pendingPayment.PaymentId,
                pendingPayment.PaymentWindow,
                [unlResourceAction, sfdResourceAction, spendChoice]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.State.PendingPayment);
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Equal(
            [
                "TEMPORARY_PAYMENT_RESOURCE_SPENT",
                "TEMPORARY_PAYMENT_RESOURCE_CLEARED",
                "TEMPORARY_PAYMENT_RESOURCE_SPENT",
                "TEMPORARY_PAYMENT_RESOURCE_CLEARED",
                "COST_PAID",
                "PAYMENT_WINDOW_CLOSED"
            ],
            result.Events.Select(gameEvent => gameEvent.Kind));

        var spentEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(
            [unlTemporaryResource.ResourceId, sfdTemporaryResource.ResourceId],
            spentEvents.Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["temporaryPaymentResourceId"])).ToArray());
        Assert.Equal(
            [UnlGoldObjectId, SfdGoldObjectId],
            spentEvents.Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["sourceObjectId"])).ToArray());
        Assert.Equal(
            [P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId],
            spentEvents.Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["abilityId"])).ToArray());
        Assert.All(spentEvents, gameEvent =>
        {
            Assert.Equal(pendingPayment.PaymentId, gameEvent.Payload["paymentId"]);
            Assert.Equal(pendingPayment.PaymentWindow, gameEvent.Payload["paymentWindow"]);
            Assert.Equal("P1", gameEvent.Payload["playerId"]);
            Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, gameEvent.Payload["consumedPower"]);
            Assert.Equal(0, gameEvent.Payload["remainingPower"]);
            Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(gameEvent.Payload["allowedPaymentKinds"]));
            Assert.Equal(true, gameEvent.Payload["paymentOnly"]);
        });

        var cleanupEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(
            [unlTemporaryResource.ResourceId, sfdTemporaryResource.ResourceId],
            cleanupEvents.Select(gameEvent => Assert.IsType<string>(gameEvent.Payload["temporaryPaymentResourceId"])).ToArray());
        Assert.All(cleanupEvents, gameEvent =>
        {
            Assert.Equal(pendingPayment.PaymentId, gameEvent.Payload["paymentId"]);
            Assert.Equal(pendingPayment.PaymentWindow, gameEvent.Payload["paymentWindow"]);
            Assert.Equal("P1", gameEvent.Payload["playerId"]);
            Assert.Equal(0, gameEvent.Payload["remainingPowerBeforeCleanup"]);
            Assert.Equal(true, gameEvent.Payload["paymentOnly"]);
        });

        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, costEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, costEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", costEvent.Payload["playerId"]);
        Assert.Equal([unlResourceAction, sfdResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([unlResourceAction, sfdResourceAction, spendChoice], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Equal([spendChoice], Assert.IsType<string[]>(costEvent.Payload["legalPaymentChoiceIds"]));
        Assert.Equal(
            [unlTemporaryResource.ResourceId, sfdTemporaryResource.ResourceId],
            Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
        Assert.Equal(totalCost, costEvent.Payload["temporaryPaymentResourcePower"]);
        Assert.Equal(totalCost, costEvent.Payload["power"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]));
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["remainingPowerByTrait"]));

        var paymentWindowClosedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, paymentWindowClosedEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, paymentWindowClosedEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", paymentWindowClosedEvent.Payload["playerId"]);
    }

    [Theory]
    [InlineData("mana-only")]
    [InlineData("wrong-trait")]
    [InlineData("unnecessary")]
    public async Task GoldTemporaryResourceRejectsNonRuneOrUnnecessaryUseWithoutMutation(string caseName)
    {
        var resourceState = (await ResolveGoldAsync(
            BuildGoldPriorityState(),
            UnlGoldObjectId,
            P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId)).State;
        var temporaryResource = Assert.Single(resourceState.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = caseName switch
        {
            "mana-only" => new PendingPaymentState(
                "PAY-MANA-1",
                "TEST_PENDING_PAY_COST",
                "P1",
                manaCost: 1,
                legalPaymentChoiceIds: ["SPEND_MANA:1"]),
            "wrong-trait" => new PendingPaymentState(
                "PAY-RED-1",
                "TEST_PENDING_PAY_COST",
                "P1",
                powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                },
                legalPaymentChoiceIds: ["SPEND_POWER:red:1"]),
            _ => new PendingPaymentState(
                "PAY-GENERIC-1",
                "TEST_PENDING_PAY_COST",
                "P1",
                powerCost: 1,
                legalPaymentChoiceIds: ["SPEND_POWER:any:1"])
        };
        var state = resourceState with
        {
            PendingPayment = pendingPayment,
            RunePools = caseName == "unnecessary"
                ? new Dictionary<string, RunePool>(StringComparer.Ordinal)
                {
                    ["P1"] = new RunePool(0, 1),
                    ["P2"] = RunePool.Empty
                }
                : resourceState.RunePools
        };
        var initialHash = MatchStateHasher.Hash(state);
        var spendChoice = caseName switch
        {
            "mana-only" => "SPEND_MANA:1",
            "wrong-trait" => "SPEND_POWER:red:1",
            _ => "SPEND_POWER:any:1"
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-gold-reject-{caseName}", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, spendChoice]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task GoldWithRenataBonusManaStillCannotUseTemporaryResourceForManaOnlyCost()
    {
        var resourceState = (await ResolveGoldAsync(
            WithRenataBonusTag(BuildGoldPriorityState(), UnlGoldObjectId),
            UnlGoldObjectId,
            P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId)).State;
        var temporaryResource = Assert.Single(resourceState.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = new PendingPaymentState(
            "PAY-MANA-ONLY",
            "TEST_PENDING_PAY_COST",
            "P1",
            manaCost: 1,
            legalPaymentChoiceIds: ["SPEND_MANA:1"]);
        var state = resourceState with { PendingPayment = pendingPayment };
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-gold-bonus-temp-reject-mana", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, "SPEND_MANA:1"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenRenataBonusMana, result.State.RunePools["P1"].Mana);
    }

    [Theory]
    [InlineData(UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId)]
    [InlineData(SfdGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId)]
    public async Task GoldWithRenataBonusTagAddsManaAndCreatesOnlyOneGenericTemporaryPower(
        string sourceObjectId,
        string abilityId)
    {
        var result = await ResolveGoldAsync(
            WithRenataBonusTag(BuildGoldPriorityState(), sourceObjectId),
            sourceObjectId,
            abilityId);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenRenataBonusMana, result.State.RunePools["P1"].Mana);
        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, temporaryResource.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, temporaryResource.RemainingPower);
        Assert.Empty(temporaryResource.GeneratedPowerByTrait);
        Assert.Empty(temporaryResource.RemainingPowerByTrait);

        var activatedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["renataGoldExtraManaApplied"]));
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenRenataBonusMana, activatedEvent.Payload["generatedMana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenRenataBonusTag, activatedEvent.Payload["bonusTag"]);
        var manaEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.True(Assert.IsType<bool>(manaEvent.Payload["renataGoldExtraManaApplied"]));
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenRenataBonusMana, manaEvent.Payload["generatedMana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenRenataBonusTag, manaEvent.Payload["bonusTag"]);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenRenataBonusMana, manaEvent.Payload["manaAfter"]);
    }

    [Theory]
    [InlineData("wrong-timing")]
    [InlineData("target")]
    [InlineData("optional-cost")]
    [InlineData("temp-resource")]
    [InlineData("recycle-rune")]
    [InlineData("wrong-controller")]
    [InlineData("not-base")]
    [InlineData("face-down")]
    [InlineData("exhausted")]
    [InlineData("non-equipment")]
    [InlineData("missing-gold-tag")]
    [InlineData("wrong-card")]
    [InlineData("missing-source")]
    public async Task GoldTokenResourceSkillRejectsInvalidSourceTimingOrPayloadWithoutMutation(string caseName)
    {
        var state = BuildInvalidState(caseName);
        var command = caseName switch
        {
            "target" => Command(UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, targetObjectIds: ["P2-ANY-TARGET"]),
            "optional-cost" => Command(UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, optionalCosts: ["CONVERT_MANA_TO_GENERIC_POWER:1"]),
            "temp-resource" => Command(UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, optionalCosts: ["TEMP_PAYMENT_RESOURCE:ANY"]),
            "recycle-rune" => Command(UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, optionalCosts: ["RECYCLE_RUNE:P1-RUNE-001"]),
            "missing-source" => Command("P1-MISSING-GOLD", P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId),
            _ => Command(UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId)
        };
        var expectedErrorCode = caseName switch
        {
            "wrong-timing" => ErrorCodes.PhaseNotAllowed,
            "not-base" => ErrorCodes.PhaseNotAllowed,
            "wrong-card" => ErrorCodes.UnsupportedCardBehavior,
            _ => ErrorCodes.InvalidTarget
        };

        await AssertRejectedNoMutationAsync(state, command, expectedErrorCode);
    }

    [Theory]
    [InlineData("wrong-timing")]
    [InlineData("target")]
    [InlineData("optional-cost")]
    [InlineData("wrong-controller")]
    public async Task GoldWithRenataBonusTagRejectsInvalidActivationWithoutAddingMana(string caseName)
    {
        var state = WithRenataBonusTag(BuildInvalidState(caseName), UnlGoldObjectId);
        var command = caseName switch
        {
            "target" => Command(UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, targetObjectIds: ["P2-ANY-TARGET"]),
            "optional-cost" => Command(UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, optionalCosts: ["CONVERT_MANA_TO_GENERIC_POWER:1"]),
            _ => Command(UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId)
        };
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-gold-bonus-reject-{caseName}", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
    }

    private static async Task<ResolutionResult> ResolveGoldAsync(
        MatchState state,
        string sourceObjectId,
        string abilityId)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-gold-token-resource-{abilityId}", "P1", CommandTypes.ActivateAbility),
            Command(sourceObjectId, abilityId),
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
            new PlayerIntent($"intent-gold-token-reject-{expectedErrorCode}", "P1", CommandTypes.ActivateAbility),
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

    private static string AssertGoldTokenAcceptedEffects(
        ResolutionResult result,
        int expectedTemporaryResourceCount)
    {
        Assert.DoesNotContain(UnlGoldObjectId, result.State.CardObjects.Keys);
        Assert.DoesNotContain(UnlGoldObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Contains(UnlGoldObjectId, result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations[UnlGoldObjectId].Zone);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal([PendingStackItemId], result.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);

        Assert.Equal(expectedTemporaryResourceCount, result.State.TemporaryPaymentResources.Count);
        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal(UnlGoldObjectId, temporaryResource.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, temporaryResource.AbilityId);
        Assert.Equal("ACTIVATE_ABILITY", temporaryResource.PaymentWindow);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, temporaryResource.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, temporaryResource.RemainingPower);
        Assert.Empty(temporaryResource.GeneratedPowerByTrait);
        Assert.Empty(temporaryResource.RemainingPowerByTrait);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], temporaryResource.AllowedPaymentKinds);

        if (result.Events.Count > 0)
        {
            Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
            Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

            var powerEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
            Assert.Equal("P1", powerEvent.Payload["playerId"]);
            Assert.Equal(UnlGoldObjectId, powerEvent.Payload["sourceObjectId"]);
            Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId, powerEvent.Payload["abilityId"]);
            Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, powerEvent.Payload["generatedPower"]);
            Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, powerEvent.Payload["power"]);
            Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenGeneratedPower, powerEvent.Payload["remainingPower"]);
            Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenPaymentOnlyResourceRestriction, powerEvent.Payload["resourceRestriction"]);
            Assert.Equal("temporary-payment-resource-ledger", powerEvent.Payload["restrictionLifecycle"]);
            Assert.Equal(temporaryResource.ResourceId, powerEvent.Payload["temporaryPaymentResourceId"]);
            Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(powerEvent.Payload["allowedPaymentKinds"]));
            Assert.True(Assert.IsType<bool>(powerEvent.Payload["paymentOnly"]));

            var activatedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
            Assert.Equal(P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityEffectKind, activatedEvent.Payload["effectKind"]);
            Assert.Equal("no-ordinary-stack-item", activatedEvent.Payload["stackPolicy"]);
            Assert.Equal(temporaryResource.ResourceId, activatedEvent.Payload["temporaryPaymentResourceId"]);
            Assert.False(Assert.IsType<bool>(activatedEvent.Payload["renataGoldExtraManaApplied"]));
            Assert.Equal(0, activatedEvent.Payload["generatedMana"]);

            Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_EXHAUSTED", StringComparison.Ordinal));
            Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_DESTROYED", StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["targetObjectId"] as string, UnlGoldObjectId, StringComparison.Ordinal)
                && string.Equals(gameEvent.Payload["reason"] as string, "RESOURCE_SKILL_COST", StringComparison.Ordinal));
        }

        return temporaryResource.ResourceId;
    }

    private static MatchState BuildInvalidState(string caseName)
    {
        var state = BuildGoldPriorityState();
        return caseName switch
        {
            "wrong-timing" => state with
            {
                TimingState = TimingStates.NeutralOpen,
                PriorityPlayerId = null,
                StackItems = []
            },
            "wrong-controller" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    UnlGoldObjectId,
                    state.CardObjects[UnlGoldObjectId] with { ControllerId = "P2" })
            },
            "not-base" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with
                    {
                        Base = state.PlayerZones["P1"].Base
                            .Where(objectId => !string.Equals(objectId, UnlGoldObjectId, StringComparison.Ordinal))
                            .ToArray(),
                        Battlefields = [UnlGoldObjectId]
                    }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    UnlGoldObjectId,
                    new ObjectLocationState("P1", "BATTLEFIELD", "P1-MAIN"))
            },
            "face-down" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    UnlGoldObjectId,
                    state.CardObjects[UnlGoldObjectId] with { IsFaceDown = true })
            },
            "exhausted" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    UnlGoldObjectId,
                    state.CardObjects[UnlGoldObjectId] with { IsExhausted = true })
            },
            "non-equipment" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    UnlGoldObjectId,
                    state.CardObjects[UnlGoldObjectId] with { Tags = [CardObjectTags.UnitCard, "金币", "反应"] })
            },
            "missing-gold-tag" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    UnlGoldObjectId,
                    state.CardObjects[UnlGoldObjectId] with { Tags = [CardObjectTags.EquipmentCard, "反应"] })
            },
            "wrong-card" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    UnlGoldObjectId,
                    state.CardObjects[UnlGoldObjectId] with { CardNo = "UNL·T06" })
            },
            _ => state
        };
    }

    private static MatchState WithRenataBonusTag(MatchState state, string sourceObjectId)
    {
        if (!state.CardObjects.TryGetValue(sourceObjectId, out var sourceState))
        {
            return state;
        }

        return state with
        {
            CardObjects = ReplaceCardObject(
                state.CardObjects,
                sourceObjectId,
                sourceState with
                {
                    Tags = sourceState.Tags
                        .Append(P4ActivatedAbilityCatalog.GoldTokenRenataBonusTag)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                })
        };
    }

    private static MatchState BuildGoldPriorityState()
    {
        return new MatchState(
            "room-gold-token-resource",
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
                    Base = [UnlGoldObjectId, SfdGoldObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [UnlGoldObjectId] = Gold(UnlGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenUnlCardNo, "P1"),
                [SfdGoldObjectId] = Gold(SfdGoldObjectId, P4ActivatedAbilityCatalog.GoldTokenSfdCardNo, "P1"),
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
                [UnlGoldObjectId] = new("P1", "BASE"),
                [SfdGoldObjectId] = new("P1", "BASE"),
                [PendingSpellObjectId] = new("P2", "STACK")
            });
    }

    private static CardObjectState Gold(
        string objectId,
        string cardNo,
        string playerId)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.EquipmentCard, "金币", "反应"],
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
