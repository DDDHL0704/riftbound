using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CrimsonRoseActivatedAbilityTests
{
    private const string CrimsonRoseObjectId = "P1-CRIMSON-ROSE";
    private const string FriendlyBaseUnitObjectId = "P1-BASE-UNIT";
    private const string FriendlyBattlefieldUnitObjectId = "P1-BATTLEFIELD-UNIT";
    private const string EnemyBaseUnitObjectId = "P2-BASE-UNIT";
    private const string EnemySpellshieldUnitObjectId = "P2-SPELLSHIELD-UNIT";
    private const string FriendlyMaduliObjectId = "P1-MADULI";

    [Fact]
    public void CrimsonRoseOpenMainPromptExposesExperienceReadyUnitRequirement()
    {
        var state = BuildCrimsonRoseState(mana: 1, experience: 3);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Equal([CrimsonRoseObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
                StringComparison.Ordinal));

        Assert.Equal(CrimsonRoseObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseCardNo, requirement["cardNo"]);
        Assert.Equal(0, requirement["manaCost"]);
        Assert.Equal(0, requirement["powerCost"]);
        Assert.Equal(3, requirement["experienceCost"]);
        Assert.Equal(1, requirement["minTargetCount"]);
        Assert.Equal(1, requirement["maxTargetCount"]);
        Assert.True(Assert.IsType<bool>(requirement["exhaustsSource"]));
        Assert.False(Assert.IsType<bool>(requirement["resolvesImmediately"]));
        Assert.Equal("ordinary-stack-item-before-ready", requirement["stackPolicy"]);
        Assert.Equal("payment-plan-experience-and-spellshield-tax", requirement["paymentPolicy"]);
        Assert.True(Assert.IsType<bool>(requirement["requiresBaseEquipmentSource"]));
        Assert.True(Assert.IsType<bool>(requirement["appliesSpellshieldTargetTax"]));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]));

        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        var targetIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(targetChoicesByIndex["0"])
            .Select(choice => choice.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [FriendlyBaseUnitObjectId, FriendlyBattlefieldUnitObjectId, EnemyBaseUnitObjectId, EnemySpellshieldUnitObjectId],
            targetIds);
    }

    [Fact]
    public void CrimsonRoseExperienceOnlyReadyUnitPromptHidesUnrelatedTemporaryPaymentResource()
    {
        var state = BuildCrimsonRoseState(mana: 1, experience: 3) with
        {
            TemporaryPaymentResources = [GenericTemporaryResource("MALZAHAR:TEMP-CRIMSON")]
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.Equal([CrimsonRoseObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
                StringComparison.Ordinal));

        Assert.Equal(CrimsonRoseObjectId, requirement["sourceObjectId"]);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]));
    }

    [Fact]
    public void CrimsonRoseReadyUnitPromptHidesGatekeeperMaduliCannotBecomeActiveTarget()
    {
        var state = AddFriendlyMaduli(BuildCrimsonRoseState(mana: 1, experience: 3));

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
                StringComparison.Ordinal));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);

        Assert.DoesNotContain(
            FriendlyMaduliObjectId,
            targetChoicesByIndex["0"].Select(choice => choice.Id));
    }

    [Theory]
    [InlineData("insufficient-experience")]
    [InlineData("source-exhausted")]
    [InlineData("source-battlefield")]
    [InlineData("wrong-controller")]
    [InlineData("wrong-card")]
    [InlineData("face-down-source")]
    public void CrimsonRosePromptHidesIllegalSourceOrInsufficientExperience(string scenario)
    {
        var state = BuildInvalidScenarioState(scenario);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = (prompt.Candidates ?? [])
            .SingleOrDefault(candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        if (activateCandidate is null)
        {
            return;
        }

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var abilityIds = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"])
            .Select(entry => entry["abilityId"] as string)
            .ToArray();
        Assert.DoesNotContain(P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId, abilityIds);
    }

    [Fact]
    public async Task CrimsonRoseFriendlySpellshieldTargetPaysExperienceNoTaxAndCreatesStack()
    {
        var state = BuildCrimsonRoseState(
            mana: 0,
            experience: 3,
            friendlyBaseUnitTags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield]);

        var result = await ActivateCrimsonRoseAsync(state, FriendlyBaseUnitObjectId);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "EQUIPMENT_EXHAUSTED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.True(result.State.CardObjects[CrimsonRoseObjectId].IsExhausted);
        Assert.True(result.State.CardObjects[FriendlyBaseUnitObjectId].IsExhausted);
        Assert.Equal([CrimsonRoseObjectId, FriendlyBaseUnitObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityEffectKind, stackItem.EffectKind);
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseCardNo, stackItem.CardNo);
        Assert.Equal([FriendlyBaseUnitObjectId], stackItem.TargetObjectIds);

        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId, costEvent.Payload["abilityId"]);
        Assert.Equal("ACTIVATE_ABILITY", costEvent.Payload["paymentWindow"]);
        Assert.Equal(3, costEvent.Payload["experienceCost"]);
        Assert.Equal(0, costEvent.Payload["remainingExperience"]);
        Assert.Equal(0, costEvent.Payload["spellshieldTaxMana"]);
        Assert.Empty(Assert.IsType<string[]>(costEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    [Fact]
    public async Task CrimsonRoseEnemySpellshieldTargetPaysManaTax()
    {
        var state = BuildCrimsonRoseState(mana: 1, experience: 3);

        var result = await ActivateCrimsonRoseAsync(state, EnemySpellshieldUnitObjectId);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.PlayerExperience["P1"]);
        Assert.True(result.State.CardObjects[CrimsonRoseObjectId].IsExhausted);
        Assert.True(result.State.CardObjects[EnemySpellshieldUnitObjectId].IsExhausted);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(1, costEvent.Payload["spellshieldTaxMana"]);
        Assert.Equal(1, costEvent.Payload["baseManaCost"]);
        Assert.Equal(1, costEvent.Payload["totalManaCost"]);
        Assert.Equal([EnemySpellshieldUnitObjectId], Assert.IsType<string[]>(costEvent.Payload["spellshieldTaxTargetObjectIds"]));
    }

    [Fact]
    public async Task CrimsonRoseEnemySpellshieldTargetTaxRejectsSuccessfulCommandReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var state = BuildCrimsonRoseState(mana: 1, experience: 3);
        var command = CrimsonRoseCommand([EnemySpellshieldUnitObjectId]);

        var activated = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-crimson-spellshield-tax-first", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "EQUIPMENT_EXHAUSTED", "COST_PAID", "STACK_ITEM_ADDED"], activated.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(1, CountEvents(activated.Events, "ABILITY_ACTIVATED"));
        Assert.Equal(1, CountEvents(activated.Events, "COST_PAID"));
        Assert.Equal(1, CountEvents(activated.Events, "STACK_ITEM_ADDED"));
        Assert.Equal(0, activated.State.RunePools["P1"].Mana);
        Assert.Equal(0, activated.State.PlayerExperience["P1"]);
        Assert.True(activated.State.CardObjects[CrimsonRoseObjectId].IsExhausted);
        Assert.Equal([CrimsonRoseObjectId, FriendlyBaseUnitObjectId], activated.State.PlayerZones["P1"].Base);
        Assert.Equal(new ObjectLocationState("P1", "BASE"), activated.State.ObjectLocations[CrimsonRoseObjectId]);
        Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "P2-MAIN"), activated.State.ObjectLocations[EnemySpellshieldUnitObjectId]);
        var stackItem = Assert.Single(activated.State.StackItems);
        Assert.Equal(CrimsonRoseObjectId, stackItem.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityEffectKind, stackItem.EffectKind);
        Assert.Equal([EnemySpellshieldUnitObjectId], stackItem.TargetObjectIds);
        var costEvent = Assert.Single(activated.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(1, costEvent.Payload["spellshieldTaxMana"]);
        Assert.Equal([EnemySpellshieldUnitObjectId], Assert.IsType<string[]>(costEvent.Payload["spellshieldTaxTargetObjectIds"]));
        var postActivationHash = MatchStateHasher.Hash(activated.State);

        var replay = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-crimson-spellshield-tax-replay", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Empty(replay.Events);
        Assert.Equal(postActivationHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(activated.State.RunePools["P1"], replay.State.RunePools["P1"]);
        Assert.Equal(activated.State.PlayerExperience["P1"], replay.State.PlayerExperience["P1"]);
        Assert.Equal(activated.State.PlayerZones["P1"].Base, replay.State.PlayerZones["P1"].Base);
        Assert.Equal(activated.State.PlayerZones["P2"].Battlefields, replay.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(activated.State.ObjectLocations[CrimsonRoseObjectId], replay.State.ObjectLocations[CrimsonRoseObjectId]);
        Assert.Equal(activated.State.ObjectLocations[EnemySpellshieldUnitObjectId], replay.State.ObjectLocations[EnemySpellshieldUnitObjectId]);
        Assert.True(replay.State.CardObjects[CrimsonRoseObjectId].IsExhausted);
        var replayStackItem = Assert.Single(replay.State.StackItems);
        Assert.Equal(stackItem, replayStackItem);
    }

    [Fact]
    public async Task CrimsonRoseEnemySpellshieldStalePromptReplayAfterStackPriorityStartsUsesRejectedCacheWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildCrimsonRoseState(mana: 1, experience: 3);
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
        Assert.Equal([CrimsonRoseObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        var command = CrimsonRoseCommand([EnemySpellshieldUnitObjectId]);
        var staleRawCommand = PromptScopedActivateAbilityRawCommand(command, prompt);
        var changedStaleRawCommand = PromptScopedActivateAbilityRawCommandWithClientNote(command, prompt, "changed-payload");
        const string acceptedClientIntentId = "intent-crimson-rose-before-stale-prompt-replay";
        const string staleClientIntentId = "intent-crimson-rose-stale-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "EQUIPMENT_EXHAUSTED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(0, accepted.State.RunePools["P1"].Mana);
        Assert.Equal(0, accepted.State.PlayerExperience["P1"]);
        Assert.True(accepted.State.CardObjects[CrimsonRoseObjectId].IsExhausted);
        Assert.True(accepted.State.CardObjects[EnemySpellshieldUnitObjectId].IsExhausted);
        Assert.Equal(new ObjectLocationState("P1", "BASE"), accepted.State.ObjectLocations[CrimsonRoseObjectId]);
        Assert.Equal(new ObjectLocationState("P2", "BATTLEFIELD", "P2-MAIN"), accepted.State.ObjectLocations[EnemySpellshieldUnitObjectId]);
        Assert.Equal(TimingStates.NeutralClosed, accepted.State.TimingState);
        Assert.Equal("P1", accepted.State.PriorityPlayerId);
        Assert.Contains(CommandTypes.PassPriority, accepted.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, accepted.Prompts["P1"].Actions);
        var acceptedStackItem = Assert.Single(accepted.State.StackItems);
        Assert.Equal(CrimsonRoseObjectId, acceptedStackItem.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityEffectKind, acceptedStackItem.EffectKind);
        Assert.Equal([EnemySpellshieldUnitObjectId], acceptedStackItem.TargetObjectIds);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedClientIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ActivateAbility, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.ActivateAbility, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(CrimsonRoseObjectId, acceptedJournalEntry.RawCommand.Value.GetProperty("sourceObjectId").GetString());
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId, acceptedJournalEntry.RawCommand.Value.GetProperty("abilityId").GetString());
        Assert.Equal(
            [EnemySpellshieldUnitObjectId],
            acceptedJournalEntry.RawCommand.Value.GetProperty("targetObjectIds").EnumerateArray().Select(target => target.GetString()!).ToArray());
        Assert.Empty(acceptedJournalEntry.RawCommand.Value.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
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
        Assert.Equal(0, replay.State.RunePools["P1"].Mana);
        Assert.Equal(0, replay.State.PlayerExperience["P1"]);
        Assert.True(replay.State.CardObjects[CrimsonRoseObjectId].IsExhausted);
        Assert.True(replay.State.CardObjects[EnemySpellshieldUnitObjectId].IsExhausted);
        Assert.Equal(accepted.State.ObjectLocations[CrimsonRoseObjectId], replay.State.ObjectLocations[CrimsonRoseObjectId]);
        Assert.Equal(accepted.State.ObjectLocations[EnemySpellshieldUnitObjectId], replay.State.ObjectLocations[EnemySpellshieldUnitObjectId]);
        Assert.Equal(TimingStates.NeutralClosed, replay.State.TimingState);
        Assert.Equal("P1", replay.State.PriorityPlayerId);
        Assert.Contains(CommandTypes.PassPriority, replay.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, replay.Prompts["P1"].Actions);
        var replayStackItem = Assert.Single(replay.State.StackItems);
        Assert.Equal(acceptedStackItem.StackItemId, replayStackItem.StackItemId);
        Assert.Equal(CrimsonRoseObjectId, replayStackItem.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityEffectKind, replayStackItem.EffectKind);
        Assert.Equal([EnemySpellshieldUnitObjectId], replayStackItem.TargetObjectIds);

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
        Assert.Equal(CommandTypes.ActivateAbility, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(CrimsonRoseObjectId, rejectedJournalEntry.RawCommand.Value.GetProperty("sourceObjectId").GetString());
        Assert.Equal(P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId, rejectedJournalEntry.RawCommand.Value.GetProperty("abilityId").GetString());
        Assert.Equal(
            [EnemySpellshieldUnitObjectId],
            rejectedJournalEntry.RawCommand.Value.GetProperty("targetObjectIds").EnumerateArray().Select(target => target.GetString()!).ToArray());
        Assert.Empty(rejectedJournalEntry.RawCommand.Value.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
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
        Assert.Equal(0, duplicateReplay.State.RunePools["P1"].Mana);
        Assert.Equal(0, duplicateReplay.State.PlayerExperience["P1"]);
        Assert.True(duplicateReplay.State.CardObjects[CrimsonRoseObjectId].IsExhausted);
        Assert.True(duplicateReplay.State.CardObjects[EnemySpellshieldUnitObjectId].IsExhausted);
        Assert.Equal(accepted.State.ObjectLocations[CrimsonRoseObjectId], duplicateReplay.State.ObjectLocations[CrimsonRoseObjectId]);
        Assert.Equal(accepted.State.ObjectLocations[EnemySpellshieldUnitObjectId], duplicateReplay.State.ObjectLocations[EnemySpellshieldUnitObjectId]);
        Assert.Equal(TimingStates.NeutralClosed, duplicateReplay.State.TimingState);
        Assert.Equal("P1", duplicateReplay.State.PriorityPlayerId);
        Assert.Equal(accepted.State.StackItems, duplicateReplay.State.StackItems);
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
        Assert.Equal(0, conflict.State.RunePools["P1"].Mana);
        Assert.Equal(0, conflict.State.PlayerExperience["P1"]);
        Assert.True(conflict.State.CardObjects[CrimsonRoseObjectId].IsExhausted);
        Assert.True(conflict.State.CardObjects[EnemySpellshieldUnitObjectId].IsExhausted);
        Assert.Equal(accepted.State.ObjectLocations[CrimsonRoseObjectId], conflict.State.ObjectLocations[CrimsonRoseObjectId]);
        Assert.Equal(accepted.State.ObjectLocations[EnemySpellshieldUnitObjectId], conflict.State.ObjectLocations[EnemySpellshieldUnitObjectId]);
        Assert.Equal(TimingStates.NeutralClosed, conflict.State.TimingState);
        Assert.Equal("P1", conflict.State.PriorityPlayerId);
        Assert.Equal(accepted.State.StackItems, conflict.State.StackItems);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CrimsonRoseStackPassPassReadiesTargetAndKeepsSourceInBaseExhausted()
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateCrimsonRoseAsync(
            BuildCrimsonRoseState(mana: 0, experience: 3),
            FriendlyBaseUnitObjectId,
            engine);
        Assert.True(activated.Accepted, activated.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-crimson-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-crimson-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal([CrimsonRoseObjectId, FriendlyBaseUnitObjectId], p2Pass.State.PlayerZones["P1"].Base);
        Assert.True(p2Pass.State.CardObjects[CrimsonRoseObjectId].IsExhausted);
        Assert.False(p2Pass.State.CardObjects[FriendlyBaseUnitObjectId].IsExhausted);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId, StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, FriendlyBaseUnitObjectId, StringComparison.Ordinal)
            && Equals(gameEvent.Payload["wasExhausted"], true)
            && Equals(gameEvent.Payload["isExhausted"], false));
    }

    [Fact]
    public async Task CrimsonRoseRejectsHandWrittenGatekeeperMaduliReadyTargetWithoutMutation()
    {
        var state = AddFriendlyMaduli(BuildCrimsonRoseState(mana: 1, experience: 3));

        await AssertRejectedNoMutationAsync(state, CrimsonRoseCommand([FriendlyMaduliObjectId]));
    }

    [Fact]
    public async Task CrimsonRoseStaleStackItemSkipsGatekeeperMaduliCannotBecomeActiveTarget()
    {
        var engine = new CoreRuleEngine();
        var baseState = AddFriendlyMaduli(BuildCrimsonRoseState(mana: 1, experience: 3));
        var state = baseState with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            CardObjects = ReplaceCardObject(
                baseState.CardObjects,
                CrimsonRoseObjectId,
                baseState.CardObjects[CrimsonRoseObjectId] with
                {
                    IsExhausted = true
                }),
            StackItems =
            [
                new StackItemState(
                    "STACK-STALE-CRIMSON-MADULI",
                    "P1",
                    CrimsonRoseObjectId,
                    P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityEffectKind,
                    P4ActivatedAbilityCatalog.CrimsonRoseCardNo,
                    [FriendlyMaduliObjectId],
                    0,
                    1,
                    [])
            ]
        };

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-crimson-maduli-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-crimson-maduli-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.True(p2Pass.State.CardObjects[FriendlyMaduliObjectId].IsExhausted);
        Assert.DoesNotContain(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, FriendlyMaduliObjectId, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("wrong-timing")]
    [InlineData("non-active-player")]
    [InlineData("missing-target")]
    [InlineData("too-many-targets")]
    [InlineData("invalid-target")]
    [InlineData("face-down-target")]
    [InlineData("standby-target")]
    [InlineData("insufficient-experience")]
    [InlineData("insufficient-tax-mana")]
    [InlineData("unsupported-optional-cost")]
    [InlineData("recycle-rune")]
    [InlineData("temporary-resource")]
    [InlineData("source-exhausted")]
    [InlineData("source-battlefield")]
    [InlineData("wrong-controller")]
    [InlineData("wrong-card")]
    [InlineData("face-down-source")]
    public async Task CrimsonRoseRejectsInvalidCommandsWithoutMutation(string scenario)
    {
        var state = BuildInvalidScenarioState(scenario);
        var command = scenario switch
        {
            "missing-target" => CrimsonRoseCommand([]),
            "too-many-targets" => CrimsonRoseCommand([FriendlyBaseUnitObjectId, EnemyBaseUnitObjectId]),
            "invalid-target" => CrimsonRoseCommand(["P1-NON-UNIT-EQUIPMENT"]),
            "face-down-target" => CrimsonRoseCommand(["P1-FACE-DOWN-UNIT"]),
            "standby-target" => CrimsonRoseCommand(["P1-STANDBY-UNIT"]),
            "insufficient-tax-mana" => CrimsonRoseCommand([EnemySpellshieldUnitObjectId]),
            "unsupported-optional-cost" => CrimsonRoseCommand([FriendlyBaseUnitObjectId], ["SPEND_EXPERIENCE:3"]),
            "recycle-rune" => CrimsonRoseCommand([FriendlyBaseUnitObjectId], ["RECYCLE_RUNE:P1-RUNE-BLUE"]),
            "temporary-resource" => CrimsonRoseCommand([FriendlyBaseUnitObjectId], [PaymentCostRules.TemporaryPaymentResourceActionId("MALZAHAR:TEMP-CRIMSON")]),
            _ => CrimsonRoseCommand([FriendlyBaseUnitObjectId])
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    private static async Task<ResolutionResult> ActivateCrimsonRoseAsync(
        MatchState state,
        string targetObjectId,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-crimson-rose", "P1", CommandTypes.ActivateAbility),
            CrimsonRoseCommand([targetObjectId]),
            CancellationToken.None);
    }

    private static ActivateAbilityCommand CrimsonRoseCommand(
        IReadOnlyList<string> targetObjectIds,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            CrimsonRoseObjectId,
            P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
            targetObjectIds,
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

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        ActivateAbilityCommand command)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-crimson-invalid", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    private static int CountEvents(IReadOnlyList<GameEvent> events, string kind)
    {
        return events.Count(gameEvent => string.Equals(gameEvent.Kind, kind, StringComparison.Ordinal));
    }

    private static MatchState BuildInvalidScenarioState(string scenario)
    {
        var extraCardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-NON-UNIT-EQUIPMENT"] = Equipment("P1-NON-UNIT-EQUIPMENT", "SFD·022/221", "P1"),
            ["P1-FACE-DOWN-UNIT"] = Unit("P1-FACE-DOWN-UNIT", "UNL-101/219", "P1", isExhausted: true, isFaceDown: true),
            ["P1-STANDBY-UNIT"] = Unit("P1-STANDBY-UNIT", "UNL-102/219", "P1", isExhausted: true, extraTags: [CardObjectTags.Standby]),
            ["P1-RUNE-BLUE"] = RuneCard("P1-RUNE-BLUE", RuneTrait.Blue)
        };
        var state = scenario switch
        {
            "insufficient-experience" => BuildCrimsonRoseState(mana: 1, experience: 2, extraCardObjects: extraCardObjects),
            "insufficient-tax-mana" => BuildCrimsonRoseState(mana: 0, experience: 3, extraCardObjects: extraCardObjects),
            _ => BuildCrimsonRoseState(mana: 1, experience: 3, extraCardObjects: extraCardObjects)
        };

        state = state with
        {
            PlayerZones = ReplacePlayerZones(
                state.PlayerZones,
                "P1",
                state.PlayerZones["P1"] with
                {
                    Base = state.PlayerZones["P1"].Base
                        .Concat(["P1-NON-UNIT-EQUIPMENT", "P1-FACE-DOWN-UNIT", "P1-STANDBY-UNIT", "P1-RUNE-BLUE"])
                        .ToArray()
                }),
            ObjectLocations = ReplaceObjectLocations(
                state.ObjectLocations,
                new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
                {
                    ["P1-NON-UNIT-EQUIPMENT"] = new("P1", "BASE"),
                    ["P1-FACE-DOWN-UNIT"] = new("P1", "BASE"),
                    ["P1-STANDBY-UNIT"] = new("P1", "BASE"),
                    ["P1-RUNE-BLUE"] = new("P1", "BASE")
                })
        };

        return scenario switch
        {
            "wrong-timing" => state with
            {
                TimingState = TimingStates.NeutralClosed,
                PriorityPlayerId = "P1",
                StackItems =
                [
                    new StackItemState(
                        "STACK-PENDING",
                        "P2",
                        "P2-PENDING-SPELL",
                        "TEST_PENDING",
                        "UNL-001/219")
                ]
            },
            "non-active-player" => state with
            {
                ActivePlayerId = "P2",
                TurnPlayerId = "P2"
            },
            "source-exhausted" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    CrimsonRoseObjectId,
                    state.CardObjects[CrimsonRoseObjectId] with { IsExhausted = true })
            },
            "source-battlefield" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with
                    {
                        Base = state.PlayerZones["P1"].Base
                            .Where(objectId => !string.Equals(objectId, CrimsonRoseObjectId, StringComparison.Ordinal))
                            .ToArray(),
                        Battlefields = [CrimsonRoseObjectId, FriendlyBattlefieldUnitObjectId]
                    }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    CrimsonRoseObjectId,
                    new ObjectLocationState("P1", "BATTLEFIELD", "P1-MAIN"))
            },
            "wrong-controller" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    CrimsonRoseObjectId,
                    state.CardObjects[CrimsonRoseObjectId] with { ControllerId = "P2" })
            },
            "wrong-card" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    CrimsonRoseObjectId,
                    state.CardObjects[CrimsonRoseObjectId] with { CardNo = "UNL-110/219" })
            },
            "face-down-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    CrimsonRoseObjectId,
                    state.CardObjects[CrimsonRoseObjectId] with { IsFaceDown = true })
            },
            _ => state
        };
    }

    private static MatchState BuildCrimsonRoseState(
        int mana,
        int experience,
        IReadOnlyList<string>? friendlyBaseUnitTags = null,
        IReadOnlyDictionary<string, CardObjectState>? extraCardObjects = null)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [CrimsonRoseObjectId] = Equipment(CrimsonRoseObjectId, P4ActivatedAbilityCatalog.CrimsonRoseCardNo, "P1"),
            [FriendlyBaseUnitObjectId] = Unit(FriendlyBaseUnitObjectId, "UNL-101/219", "P1", isExhausted: true, tags: friendlyBaseUnitTags),
            [FriendlyBattlefieldUnitObjectId] = Unit(FriendlyBattlefieldUnitObjectId, "UNL-102/219", "P1", isExhausted: false),
            [EnemyBaseUnitObjectId] = Unit(EnemyBaseUnitObjectId, "UNL-103/219", "P2", isExhausted: true),
            [EnemySpellshieldUnitObjectId] = Unit(EnemySpellshieldUnitObjectId, "UNL-104/219", "P2", isExhausted: true, extraTags: [CardObjectTags.Spellshield])
        };
        foreach (var entry in extraCardObjects ?? new Dictionary<string, CardObjectState>(StringComparer.Ordinal))
        {
            cardObjects[entry.Key] = entry.Value;
        }

        return new MatchState(
            "room-crimson-rose",
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
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = experience,
                ["P2"] = 0
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [CrimsonRoseObjectId, FriendlyBaseUnitObjectId],
                    Battlefields = [FriendlyBattlefieldUnitObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = [EnemyBaseUnitObjectId],
                    Battlefields = [EnemySpellshieldUnitObjectId]
                }
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: cardObjects,
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [CrimsonRoseObjectId] = new("P1", "BASE"),
                [FriendlyBaseUnitObjectId] = new("P1", "BASE"),
                [FriendlyBattlefieldUnitObjectId] = new("P1", "BATTLEFIELD", "P1-MAIN"),
                [EnemyBaseUnitObjectId] = new("P2", "BASE"),
                [EnemySpellshieldUnitObjectId] = new("P2", "BATTLEFIELD", "P2-MAIN")
            });
    }

    private static MatchState AddFriendlyMaduli(MatchState state)
    {
        return state with
        {
            PlayerZones = ReplacePlayerZones(
                state.PlayerZones,
                "P1",
                state.PlayerZones["P1"] with
                {
                    Base = state.PlayerZones["P1"].Base.Concat([FriendlyMaduliObjectId]).ToArray()
                }),
            CardObjects = ReplaceCardObject(
                state.CardObjects,
                FriendlyMaduliObjectId,
                Unit(FriendlyMaduliObjectId, P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo, "P1", isExhausted: true)),
            ObjectLocations = ReplaceObjectLocation(
                state.ObjectLocations,
                FriendlyMaduliObjectId,
                new ObjectLocationState("P1", "BASE"))
        };
    }

    private static CardObjectState Equipment(string objectId, string cardNo, string playerId)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.EquipmentCard],
            cardNo: cardNo,
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState Unit(
        string objectId,
        string cardNo,
        string playerId,
        bool isExhausted,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? extraTags = null,
        bool isFaceDown = false)
    {
        var resolvedTags = tags ?? new[] { CardObjectTags.UnitCard }
            .Concat(extraTags ?? Array.Empty<string>())
            .ToArray();
        return new CardObjectState(
            objectId,
            power: 2,
            isExhausted: isExhausted,
            isFaceDown: isFaceDown,
            tags: resolvedTags,
            cardNo: cardNo,
            ownerId: playerId,
            controllerId: playerId);
    }

    private static CardObjectState RuneCard(string objectId, string trait)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.RuneCard, $"COLOR:{trait}"],
            cardNo: $"RUNE-{trait}",
            ownerId: "P1",
            controllerId: "P1");
    }

    private static TemporaryPaymentResourceState GenericTemporaryResource(string resourceId)
    {
        return new TemporaryPaymentResourceState(
            resourceId,
            "P1",
            "P1-MALZAHAR",
            P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
            "ACTIVATE_ABILITY",
            2,
            2,
            [PaymentCostRules.RuneCostPaymentKind],
            1);
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
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        string objectId,
        ObjectLocationState replacement)
    {
        var next = objectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        next[objectId] = replacement;
        return next;
    }

    private static IReadOnlyDictionary<string, ObjectLocationState> ReplaceObjectLocations(
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        IReadOnlyDictionary<string, ObjectLocationState> replacements)
    {
        var next = objectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var entry in replacements)
        {
            next[entry.Key] = entry.Value;
        }

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
