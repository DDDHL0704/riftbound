using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class JhinMovementResourceSkillTests
{
    private const string JhinObjectId = "P1-JHIN";

    [Fact]
    public void CatalogExposesJhinMovementResourceSkill()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId,
            out var ability));

        Assert.Equal(P4ActivatedAbilityCatalog.JhinCardNo, ability.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityEffectKind, ability.EffectKind);
        Assert.True(ability.IsResourceSkill);
        Assert.True(ability.PaymentOnlyResource);
        Assert.False(ability.ReactionSpeed);
        Assert.False(ability.ExhaustsSourceAsCost);
        Assert.Equal(0, ability.RequiredTargetCount);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedMana, ability.GeneratedMana);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, ability.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceRestriction, ability.ResourceRestriction);
    }

    [Fact]
    public void JhinMovementSourceIdentityUsesAbilitySourceCardGroup()
    {
        var repositoryRoot = RepositoryRoot();
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));
        var matchRecoverySource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Riftbound.Engine",
            "MatchRecovery.cs"));

        Assert.DoesNotContain(
            "sourceState.CardNo, P4ActivatedAbilityCatalog.JhinCardNo",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "sourceCardNo, P4ActivatedAbilityCatalog.JhinCardNo",
            matchRecoverySource,
            StringComparison.Ordinal);
        Assert.Contains("P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId", matchRecoverySource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JhinResourceSkillPromptAppearsOnlyAfterServerCapturedMoveTrigger()
    {
        var beforeMovePrompt = ResolutionResult.BuildPrompts(BuildJhinBaseState())["P1"];
        AssertNoJhinResourceSkill(beforeMovePrompt);

        var moved = await MoveJhinAsync(BuildJhinBaseState());
        Assert.True(moved.Accepted, moved.ErrorMessage);

        var trigger = Assert.Single(moved.State.TriggerQueue);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityEffectKind, trigger.EffectKind);
        Assert.Equal(JhinObjectId, trigger.SourceObjectId);
        Assert.Equal("UNIT_MOVED_TO_BATTLEFIELD", trigger.TriggeredByEventKind);
        Assert.Contains(moved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["triggerId"] as string, trigger.TriggerId, StringComparison.Ordinal));

        var prompt = moved.Prompts["P1"];
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Equal([JhinObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        Assert.Empty(activateCandidate.Targets ?? []);

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(entry["abilityId"] as string, P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, StringComparison.Ordinal));
        Assert.Equal(JhinObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinCardNo, requirement["cardNo"]);
        Assert.True(Assert.IsType<bool>(requirement["resourceSkill"]));
        Assert.True(Assert.IsType<bool>(requirement["paymentOnly"]));
        Assert.True(Assert.IsType<bool>(requirement["movementTriggered"]));
        Assert.Equal(0, requirement["minTargetCount"]);
        Assert.Equal(0, requirement["maxTargetCount"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedMana, requirement["generatedMana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, requirement["generatedPower"]);
        Assert.Equal("server-captured-movement-trigger-open-main", requirement["timingPolicy"]);
        Assert.Equal("no-ordinary-stack-item", requirement["stackPolicy"]);
        Assert.True(Assert.IsType<bool>(requirement["generatedResourceCannotBeTargetedAsResponse"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]);
        var requiredOptionalCosts = Assert.IsAssignableFrom<string[]>(requirement["requiredOptionalCosts"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]));
        var triggerChoice = Assert.Single(optionalCostChoices);
        Assert.Equal($"{P4ActivatedAbilityCatalog.JhinMoveTriggerOptionalCostPrefix}{trigger.TriggerId}", triggerChoice.Id);
        Assert.Equal("server-owned Jhin movement trigger context", triggerChoice.Reason);
        Assert.Equal([triggerChoice.Id], requiredOptionalCosts);

        AssertNoJhinResourceSkill(moved.Prompts["P2"]);
    }

    [Fact]
    public async Task JhinMovementResourceSkillGainsManaAndPaymentOnlyPowerWithoutStackResponse()
    {
        var moved = await MoveJhinAsync(BuildJhinBaseState());
        var trigger = Assert.Single(moved.State.TriggerQueue);
        var triggerChoice = JhinTriggerChoice(moved.State);

        var result = await ActivateJhinAsync(moved.State, optionalCosts: [triggerChoice]);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.TriggerQueue);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedMana, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal(JhinObjectId, temporaryResource.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, temporaryResource.AbilityId);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, temporaryResource.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, temporaryResource.RemainingPower);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], temporaryResource.AllowedPaymentKinds);
        var snapshotResource = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            result.Snapshots["P1"].Timing["temporaryPaymentResources"]));
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceRestriction, snapshotResource["resourceRestriction"]);
        Assert.Equal(
            ["TRIGGER_RESOLVED", "ABILITY_ACTIVATED", "MANA_GAINED", "POWER_GAINED"],
            result.Events.Select(gameEvent => gameEvent.Kind));

        var triggerResolvedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.Equal(trigger.TriggerId, triggerResolvedEvent.Payload["triggerId"]);

        var activatedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        var paymentId = Assert.IsType<string>(activatedEvent.Payload["paymentId"]);
        Assert.Equal("ACTIVATE_ABILITY", activatedEvent.Payload["paymentWindow"]);
        Assert.Equal(trigger.TriggerId, activatedEvent.Payload["movementTriggerId"]);
        Assert.Equal("BASE", activatedEvent.Payload["origin"]);
        Assert.Equal("BATTLEFIELD", activatedEvent.Payload["destination"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedMana, activatedEvent.Payload["generatedMana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, activatedEvent.Payload["generatedPower"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceRestriction, activatedEvent.Payload["resourceRestriction"]);
        Assert.Equal(temporaryResource.ResourceId, activatedEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal($"JHIN:{paymentId}", temporaryResource.ResourceId);
        Assert.Equal("mana-rune-pool-plus-temporary-payment-resource-ledger", activatedEvent.Payload["resourceLifecycle"]);
        Assert.Equal("no-ordinary-stack-item", activatedEvent.Payload["stackPolicy"]);
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["generatedResourceCannotBeTargetedAsResponse"]));

        var manaEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.Equal(JhinObjectId, manaEvent.Payload["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, manaEvent.Payload["abilityId"]);
        Assert.Equal(trigger.TriggerId, manaEvent.Payload["movementTriggerId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedMana, manaEvent.Payload["mana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedMana, manaEvent.Payload["manaAfter"]);
        Assert.Equal("rune-pool-mana-reset-at-turn-cleanup", manaEvent.Payload["resourceLifecycle"]);

        var powerEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(paymentId, powerEvent.Payload["paymentId"]);
        Assert.Equal("ACTIVATE_ABILITY", powerEvent.Payload["paymentWindow"]);
        Assert.Equal(JhinObjectId, powerEvent.Payload["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, powerEvent.Payload["abilityId"]);
        Assert.Equal(trigger.TriggerId, powerEvent.Payload["movementTriggerId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, powerEvent.Payload["generatedPower"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, powerEvent.Payload["power"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, powerEvent.Payload["remainingPower"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceRestriction, powerEvent.Payload["resourceRestriction"]);
        Assert.Equal("temporary-payment-resource-ledger", powerEvent.Payload["restrictionLifecycle"]);
        Assert.Equal(temporaryResource.ResourceId, powerEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(powerEvent.Payload["allowedPaymentKinds"]));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JhinMovementResourceSkillStalePromptReplayUsesRejectedCacheWithoutMutation()
    {
        var moved = await MoveJhinAsync(BuildJhinBaseState());
        Assert.True(moved.Accepted, moved.ErrorMessage);
        var trigger = Assert.Single(moved.State.TriggerQueue);
        var triggerChoice = JhinTriggerChoice(moved.State);
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(moved.State, new CoreRuleEngine(), journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.True(prompt.Actionable);
        Assert.Contains(CommandTypes.ActivateAbility, prompt.Actions);
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Equal([JhinObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        var command = JhinCommand(optionalCosts: [triggerChoice]);
        var staleRawCommand = PromptScopedActivateAbilityRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedActivateAbilityRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-jhin-resource-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-jhin-resource-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Empty(accepted.State.StackItems);
        Assert.Empty(accepted.State.TriggerQueue);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedMana, accepted.State.RunePools["P1"].Mana);
        Assert.Equal(0, accepted.State.RunePools["P1"].Power);
        var acceptedTemporaryResource = AssertSingleJhinTemporaryResource(accepted.State);
        Assert.Equal(
            ["TRIGGER_RESOLVED", "ABILITY_ACTIVATED", "MANA_GAINED", "POWER_GAINED"],
            accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.DoesNotContain(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        var triggerResolvedEvent = Assert.Single(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.Equal(trigger.TriggerId, triggerResolvedEvent.Payload["triggerId"]);
        var activatedEvent = Assert.Single(accepted.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal(trigger.TriggerId, activatedEvent.Payload["movementTriggerId"]);
        Assert.Equal("no-ordinary-stack-item", activatedEvent.Payload["stackPolicy"]);
        Assert.Equal(acceptedTemporaryResource.ResourceId, activatedEvent.Payload["temporaryPaymentResourceId"]);
        var manaEvent = Assert.Single(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.Equal(JhinObjectId, manaEvent.Payload["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, manaEvent.Payload["abilityId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedMana, manaEvent.Payload["manaAfter"]);
        var powerEvent = Assert.Single(accepted.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(JhinObjectId, powerEvent.Payload["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, powerEvent.Payload["abilityId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, powerEvent.Payload["remainingPower"]);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedAuthoritativePromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var acceptedAuthoritativeSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        var acceptedP1SessionPromptHash = MatchStateHasher.HashValue(session.PromptFor("P1"));
        var acceptedP2SessionPromptHash = MatchStateHasher.HashValue(session.PromptFor("P2"));
        var acceptedP1SessionSnapshotHash = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var acceptedP2SessionSnapshotHash = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(moved.State.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ActivateAbility, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        AssertPromptScopedJhinRawCommand(acceptedJournalEntry.RawCommand.Value, prompt, [triggerChoice]);
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
        Assert.Equal(acceptedAuthoritativePromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(acceptedP1SessionPromptHash, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(acceptedP2SessionPromptHash, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(acceptedP1SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(acceptedP2SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(accepted.State.RunePools["P1"], replay.State.RunePools["P1"]);
        Assert.Equal(accepted.State.TemporaryPaymentResources, replay.State.TemporaryPaymentResources);
        AssertSingleJhinTemporaryResource(replay.State);
        Assert.Empty(replay.State.StackItems);
        Assert.Empty(replay.State.TriggerQueue);

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(moved.State.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ActivateAbility, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedAuthoritativePromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(acceptedAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(rejectedJournalEntry.RawCommand.Value));
        AssertPromptScopedJhinRawCommand(rejectedJournalEntry.RawCommand.Value, prompt, [triggerChoice]);
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
        Assert.Equal(acceptedAuthoritativePromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(acceptedAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        Assert.Equal(acceptedP1SessionPromptHash, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(acceptedP2SessionPromptHash, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(acceptedP1SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(acceptedP2SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(accepted.State.RunePools["P1"], duplicateReplay.State.RunePools["P1"]);
        Assert.Equal(accepted.State.TemporaryPaymentResources, duplicateReplay.State.TemporaryPaymentResources);
        AssertSingleJhinTemporaryResource(duplicateReplay.State);
        Assert.Empty(duplicateReplay.State.StackItems);
        Assert.Empty(duplicateReplay.State.TriggerQueue);
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
        Assert.Equal(acceptedAuthoritativePromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(acceptedP1SessionPromptHash, MatchStateHasher.HashValue(session.PromptFor("P1")));
        Assert.Equal(acceptedP2SessionPromptHash, MatchStateHasher.HashValue(session.PromptFor("P2")));
        Assert.Equal(acceptedP1SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(acceptedP2SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Equal(accepted.State.RunePools["P1"], conflict.State.RunePools["P1"]);
        Assert.Equal(accepted.State.TemporaryPaymentResources, conflict.State.TemporaryPaymentResources);
        AssertSingleJhinTemporaryResource(conflict.State);
        Assert.Empty(conflict.State.StackItems);
        Assert.Empty(conflict.State.TriggerQueue);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JhinPreciseRoamMoveQueuesMovementResourceTriggerAndCanResolve()
    {
        var moved = await PreciseRoamJhinAsync(BuildJhinPreciseRoamState());

        Assert.True(moved.Accepted, moved.ErrorMessage);
        Assert.Equal("P1-BATTLEFIELD-B", moved.State.ObjectLocations[JhinObjectId].BattlefieldObjectId);
        var trigger = Assert.Single(moved.State.TriggerQueue);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityEffectKind, trigger.EffectKind);
        Assert.Equal(JhinObjectId, trigger.SourceObjectId);
        Assert.Equal("UNIT_MOVED_TO_BATTLEFIELD", trigger.TriggeredByEventKind);
        Assert.Contains("BATTLEFIELD:P1-BATTLEFIELD-A", trigger.TriggerId, StringComparison.Ordinal);
        Assert.Contains("BATTLEFIELD:P1-BATTLEFIELD-B", trigger.TriggerId, StringComparison.Ordinal);
        Assert.Contains(moved.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["triggerId"] as string, trigger.TriggerId, StringComparison.Ordinal));

        var result = await ActivateJhinAsync(moved.State, optionalCosts: [JhinTriggerChoice(moved.State)]);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.TriggerQueue);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedMana, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, temporaryResource.RemainingPower);
        var snapshotResource = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            result.Snapshots["P1"].Timing["temporaryPaymentResources"]));
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceRestriction, snapshotResource["resourceRestriction"]);
    }

    [Fact]
    public async Task JhinResourceSkillPromptDisappearsWhenMovementContextIsStale()
    {
        var moved = await MoveJhinAsync(BuildJhinBaseState());
        Assert.True(moved.Accepted, moved.ErrorMessage);

        var staleContextState = moved.State with
        {
            ObjectLocations = ReplaceObjectLocation(
                moved.State.ObjectLocations,
                JhinObjectId,
                new ObjectLocationState("P1", "BASE"))
        };

        AssertNoJhinResourceSkill(ResolutionResult.BuildPrompts(staleContextState)["P1"]);
    }

    [Fact]
    public async Task JhinGeneratedManaAndPowerCanPayLaterLegalRuneCostThenClear()
    {
        var moved = await MoveJhinAsync(BuildJhinBaseState());
        var activated = await ActivateJhinAsync(moved.State, optionalCosts: [JhinTriggerChoice(moved.State)]);
        Assert.True(activated.Accepted, activated.ErrorMessage);

        var temporaryResource = Assert.Single(activated.State.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = new PendingPaymentState(
            "PAY-JHIN-GENERATED-MANA-POWER",
            "TEST_PENDING_PAY_COST",
            "P1",
            manaCost: 1,
            powerCost: 1,
            legalPaymentChoiceIds: ["SPEND_MANA:1", "SPEND_POWER:any:1"]);
        var paymentState = activated.State with { PendingPayment = pendingPayment };
        var prompt = ResolutionResult.BuildPrompts(paymentState)["P1"];
        var payCostCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(payCostCandidate.Metadata);
        var resourceActions = Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["paymentResourceActionIds"]);
        Assert.Equal([resourceAction], resourceActions);
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, paymentResourcePowerByChoice[resourceAction]["power"]);
        Assert.Equal(true, paymentResourcePowerByChoice[resourceAction]["paymentOnly"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(paymentResourcePowerByChoice[resourceAction]["powerByTrait"]));

        var result = await new CoreRuleEngine().ResolveAsync(
            paymentState,
            new PlayerIntent("intent-jhin-pay-generated", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, "SPEND_MANA:1", "SPEND_POWER:any:1"]),
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
        Assert.Equal(temporaryResource.ResourceId, spentEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(JhinObjectId, spentEvent.Payload["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, spentEvent.Payload["abilityId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, spentEvent.Payload["consumedPower"]);
        Assert.Equal(0, spentEvent.Payload["remainingPower"]);
        Assert.Equal(true, spentEvent.Payload["paymentOnly"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(spentEvent.Payload["allowedPaymentKinds"]));

        var cleanupEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, cleanupEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, cleanupEvent.Payload["paymentWindow"]);
        Assert.Equal(temporaryResource.ResourceId, cleanupEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(0, cleanupEvent.Payload["remainingPowerBeforeCleanup"]);
        Assert.Equal(true, cleanupEvent.Payload["paymentOnly"]);

        var costEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, costEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, costEvent.Payload["paymentWindow"]);
        Assert.Equal([resourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([resourceAction, "SPEND_MANA:1", "SPEND_POWER:any:1"], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Equal(["SPEND_MANA:1", "SPEND_POWER:any:1"], Assert.IsType<string[]>(costEvent.Payload["legalPaymentChoiceIds"]));
        Assert.Equal([temporaryResource.ResourceId], Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, costEvent.Payload["temporaryPaymentResourcePower"]);
        Assert.Equal(1, costEvent.Payload["mana"]);
        Assert.Equal(1, costEvent.Payload["power"]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);

        var paymentWindowClosedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "PAYMENT_WINDOW_CLOSED", StringComparison.Ordinal));
        Assert.Equal(pendingPayment.PaymentId, paymentWindowClosedEvent.Payload["paymentId"]);
        Assert.Equal(pendingPayment.PaymentWindow, paymentWindowClosedEvent.Payload["paymentWindow"]);
    }

    [Fact]
    public async Task JhinGeneratedResourceDoesNotLeakPromptMetadataForManaOnlyPayment()
    {
        var moved = await MoveJhinAsync(BuildJhinBaseState());
        var activated = await ActivateJhinAsync(moved.State, optionalCosts: [JhinTriggerChoice(moved.State)]);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedMana, activated.State.RunePools["P1"].Mana);

        var temporaryResource = Assert.Single(activated.State.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = new PendingPaymentState(
            "PAY-JHIN-MANA-ONLY",
            "TEST_PENDING_PAY_COST",
            "P1",
            manaCost: 1,
            legalPaymentChoiceIds: ["SPEND_MANA:1"]);
        var paymentState = activated.State with { PendingPayment = pendingPayment };

        var prompt = ResolutionResult.BuildPrompts(paymentState)["P1"];

        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.Contains(CommandTypes.PayCost, prompt.Actions);
        var payCostCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(payCostCandidate.Metadata);
        var paymentChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(metadata["paymentChoices"]);
        Assert.Equal(["SPEND_MANA:1"], paymentChoices.Select(choice => choice.Id).ToArray());
        Assert.DoesNotContain(paymentChoices, choice => string.Equals(choice.Id, resourceAction, StringComparison.Ordinal));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(metadata["paymentResourceActionIds"]));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        Assert.Empty(paymentResourcePowerByChoice);
        Assert.DoesNotContain(resourceAction, paymentResourcePowerByChoice.Keys);
        Assert.Equal(temporaryResource, Assert.Single(paymentState.TemporaryPaymentResources));
        Assert.Equal(pendingPayment, paymentState.PendingPayment);
    }

    [Fact]
    public async Task JhinGeneratedResourcesExpireAtTurnEndWhenUnused()
    {
        var moved = await MoveJhinAsync(BuildJhinBaseState());
        var activated = await ActivateJhinAsync(moved.State, optionalCosts: [JhinTriggerChoice(moved.State)]);
        Assert.True(activated.Accepted, activated.ErrorMessage);

        var result = await new CoreRuleEngine().ResolveAsync(
            activated.State,
            new PlayerIntent("intent-jhin-end-turn", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_POOL_CLEARED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnusedJhinMovementTriggerExpiresAtTurnEnd()
    {
        var moved = await MoveJhinAsync(BuildJhinBaseState());
        Assert.Single(moved.State.TriggerQueue);

        var result = await new CoreRuleEngine().ResolveAsync(
            moved.State,
            new PlayerIntent("intent-jhin-trigger-expire", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.State.TriggerQueue);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TRIGGER_EXPIRED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, P4ActivatedAbilityCatalog.JhinMoveResourceAbilityEffectKind, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("wrong-window")]
    [InlineData("missing-trigger")]
    [InlineData("stale-source")]
    [InlineData("stale-context")]
    [InlineData("wrong-resource-use")]
    [InlineData("handwritten-trigger")]
    public async Task JhinMovementResourceSkillRejectsInvalidCommandsWithoutMutation(string caseName)
    {
        var moved = await MoveJhinAsync(BuildJhinBaseState());
        var state = caseName switch
        {
            "wrong-window" => moved.State with { TimingState = TimingStates.NeutralClosed },
            "missing-trigger" => BuildJhinBaseState(),
            "stale-source" => moved.State with
            {
                CardObjects = ReplaceCardObject(
                    moved.State.CardObjects,
                    JhinObjectId,
                    moved.State.CardObjects[JhinObjectId] with { CardNo = "UNL-023/219" })
            },
            "stale-context" => moved.State with
            {
                ObjectLocations = ReplaceObjectLocation(
                    moved.State.ObjectLocations,
                    JhinObjectId,
                    new ObjectLocationState("P1", "BASE"))
            },
            _ => moved.State
        };
        var command = caseName switch
        {
            "missing-trigger" => JhinCommand(optionalCosts: [$"{P4ActivatedAbilityCatalog.JhinMoveTriggerOptionalCostPrefix}MISSING"]),
            "wrong-resource-use" => JhinCommand(optionalCosts: [PaymentCostRules.TemporaryPaymentResourceActionId("JHIN:HANDWRITTEN")]),
            "handwritten-trigger" => JhinCommand(optionalCosts: [$"{P4ActivatedAbilityCatalog.JhinMoveTriggerOptionalCostPrefix}JHIN_MOVE_RESOURCE::999::{JhinObjectId}::BASE::BATTLEFIELD"]),
            _ => JhinCommand(optionalCosts: [JhinTriggerChoice(moved.State)])
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    private static async Task<ResolutionResult> MoveJhinAsync(MatchState state)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-jhin-move", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand(JhinObjectId, "BASE", "BATTLEFIELD", []),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> PreciseRoamJhinAsync(MatchState state)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-jhin-precise-roam", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand(JhinObjectId, "BATTLEFIELD:P1-BATTLEFIELD-A", "BATTLEFIELD:P1-BATTLEFIELD-B", ["ROAM"]),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ActivateJhinAsync(
        MatchState state,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-jhin-resource", "P1", CommandTypes.ActivateAbility),
            JhinCommand(optionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static ActivateAbilityCommand JhinCommand(IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            JhinObjectId,
            P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId,
            [],
            optionalCosts);
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

    private static string JhinTriggerChoice(MatchState state)
    {
        var trigger = Assert.Single(state.TriggerQueue);
        return $"{P4ActivatedAbilityCatalog.JhinMoveTriggerOptionalCostPrefix}{trigger.TriggerId}";
    }

    private static TemporaryPaymentResourceState AssertSingleJhinTemporaryResource(MatchState state)
    {
        var temporaryResource = Assert.Single(state.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal(JhinObjectId, temporaryResource.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, temporaryResource.AbilityId);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, temporaryResource.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, temporaryResource.RemainingPower);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], temporaryResource.AllowedPaymentKinds);
        return temporaryResource;
    }

    private static void AssertPromptScopedJhinRawCommand(
        JsonElement rawCommand,
        ActionPromptDto prompt,
        IReadOnlyList<string> optionalCosts)
    {
        Assert.Equal(CommandTypes.ActivateAbility, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(JhinObjectId, rawCommand.GetProperty("sourceObjectId").GetString());
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, rawCommand.GetProperty("abilityId").GetString());
        Assert.Empty(rawCommand.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Equal(
            optionalCosts,
            rawCommand.GetProperty("optionalCosts").EnumerateArray().Select(cost => cost.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rawCommand.GetProperty("snapshotTick").GetInt64());
    }

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        ActivateAbilityCommand command)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-jhin-invalid", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    private static void AssertNoJhinResourceSkill(ActionPromptDto prompt)
    {
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
                    P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId,
                    StringComparison.Ordinal));
        }
    }

    private static MatchState BuildJhinBaseState()
    {
        return new MatchState(
            roomId: "jhin-movement-resource-skill-test",
            tick: 12,
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
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [JhinObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [JhinObjectId] = new(
                    JhinObjectId,
                    cardNo: P4ActivatedAbilityCatalog.JhinCardNo,
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "法盾", "游走"],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [JhinObjectId] = new("P1", "BASE")
            });
    }

    private static MatchState BuildJhinPreciseRoamState()
    {
        return BuildJhinBaseState() with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [JhinObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [JhinObjectId] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-A")
            }
        };
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
