using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class FluftPoroActivatedAbilityTests
{
    private const string FluftObjectId = "P1-FLUFT-PORO";

    [Fact]
    public void FluftPoroOpenMainPromptExposesWarhawkTokenRequirement()
    {
        var state = BuildFluftPoroState();

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Equal([FluftObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId,
                StringComparison.Ordinal));

        Assert.Equal(FluftObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroCardNo, requirement["cardNo"]);
        Assert.Equal(0, requirement["manaCost"]);
        Assert.Equal(0, requirement["powerCost"]);
        Assert.Equal(0, requirement["experienceCost"]);
        Assert.Equal(0, requirement["minTargetCount"]);
        Assert.Equal(0, requirement["maxTargetCount"]);
        Assert.True(Assert.IsType<bool>(requirement["exhaustsSource"]));
        Assert.True(Assert.IsType<bool>(requirement["requiresBattlefieldSource"]));
        Assert.False(Assert.IsType<bool>(requirement["resolvesImmediately"]));
        Assert.Equal("ordinary-stack-item-before-token-create", requirement["stackPolicy"]);
        Assert.Equal("payment-plan-zero-cost-exhaust-as-cost", requirement["paymentPolicy"]);
        Assert.Equal(P4ActivatedAbilityCatalog.WarhawkTokenCardNo, requirement["tokenCardNo"]);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkTokenCount, requirement["tokenCount"]);
        Assert.Equal(1, requirement["tokenPower"]);
        Assert.Contains(CardObjectTags.UnitCard, Assert.IsType<string[]>(requirement["tokenTags"]));
        Assert.Contains(CardObjectTags.Spellshield, Assert.IsType<string[]>(requirement["tokenTags"]));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]));
    }

    [Theory]
    [InlineData("source-base")]
    [InlineData("source-exhausted")]
    [InlineData("wrong-controller")]
    [InlineData("wrong-card")]
    [InlineData("face-down-source")]
    [InlineData("standby-source")]
    public void FluftPoroPromptHidesIllegalSources(string scenario)
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
        Assert.DoesNotContain(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId, abilityIds);
    }

    [Fact]
    public async Task FluftPoroActivationExhaustsSourceAndCreatesStackWithoutTokens()
    {
        var result = await ActivateFluftPoroAsync(BuildFluftPoroState());

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "UNIT_EXHAUSTED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.True(result.State.CardObjects[FluftObjectId].IsExhausted);
        Assert.Equal([FluftObjectId], result.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(result.State.CardObjects.Values, card => string.Equals(card.CardNo, P4ActivatedAbilityCatalog.WarhawkTokenCardNo, StringComparison.Ordinal));
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityEffectKind, stackItem.EffectKind);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroCardNo, stackItem.CardNo);
        Assert.Empty(stackItem.TargetObjectIds);

        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId, costEvent.Payload["abilityId"]);
        Assert.Equal("ACTIVATE_ABILITY", costEvent.Payload["paymentWindow"]);
        Assert.Equal(0, costEvent.Payload["baseManaCost"]);
        Assert.Equal(0, costEvent.Payload["totalManaCost"]);
        Assert.Equal(0, costEvent.Payload["genericPower"]);
        Assert.Equal(0, costEvent.Payload["totalPowerCost"]);
        Assert.True(Assert.IsType<bool>(costEvent.Payload["exhaustsSource"]));
        Assert.Equal(P4ActivatedAbilityCatalog.WarhawkTokenCardNo, costEvent.Payload["tokenCardNo"]);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkTokenCount, costEvent.Payload["tokenCount"]);
        Assert.Empty(Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
    }

    [Fact]
    public async Task FluftPoroRejectsAcceptedActivationReplayWithoutMutation()
    {
        var engine = new CoreRuleEngine();
        var command = FluftPoroCommand();

        var accepted = await engine.ResolveAsync(
            BuildFluftPoroState(),
            new PlayerIntent("intent-fluft-poro-before-replay", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "UNIT_EXHAUSTED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.True(accepted.State.CardObjects[FluftObjectId].IsExhausted);
        Assert.Equal([FluftObjectId], accepted.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain(accepted.State.CardObjects.Values, card => string.Equals(card.CardNo, P4ActivatedAbilityCatalog.WarhawkTokenCardNo, StringComparison.Ordinal));
        Assert.Equal(TimingStates.NeutralClosed, accepted.State.TimingState);
        Assert.Equal("P1", accepted.State.PriorityPlayerId);
        var stackItem = Assert.Single(accepted.State.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityEffectKind, stackItem.EffectKind);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroCardNo, stackItem.CardNo);
        Assert.Empty(stackItem.TargetObjectIds);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, accepted.Prompts["P1"].Actions);
        AssertFluftPoroStackPriorityPromptQueueAudit(accepted);
        var acceptedHash = MatchStateHasher.Hash(accepted.State);

        var replay = await engine.ResolveAsync(
            accepted.State,
            new PlayerIntent("intent-fluft-poro-stale-replay", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(replay.State));
        Assert.True(replay.State.CardObjects[FluftObjectId].IsExhausted);
        Assert.Equal([FluftObjectId], replay.State.PlayerZones["P1"].Battlefields);
        var replayStackItem = Assert.Single(replay.State.StackItems);
        Assert.Equal(stackItem.StackItemId, replayStackItem.StackItemId);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityEffectKind, replayStackItem.EffectKind);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroCardNo, replayStackItem.CardNo);
        Assert.Empty(replayStackItem.TargetObjectIds);
        Assert.Equal(TimingStates.NeutralClosed, replay.State.TimingState);
        Assert.Equal("P1", replay.State.PriorityPlayerId);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, replay.Prompts["P1"].Actions);
        Assert.DoesNotContain(replay.State.CardObjects.Values, card => string.Equals(card.CardNo, P4ActivatedAbilityCatalog.WarhawkTokenCardNo, StringComparison.Ordinal));
        AssertFluftPoroStackPriorityPromptQueueAudit(replay);
    }

    [Fact]
    public async Task FluftPoroActivationStalePromptReplayAfterStackPriorityStartsRejectsWithoutMutation()
    {
        var journal = new RecordingMatchJournal();
        var state = BuildFluftPoroState();
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.Contains(CommandTypes.ActivateAbility, prompt.Actions);
        var command = FluftPoroCommand();
        var staleRawCommand = PromptScopedActivateAbilityRawCommand(command, prompt);
        var changedRawCommand = PromptScopedActivateAbilityRawCommandWithClientNote(command, prompt, "changed-payload");
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        const string acceptedIntentId = "intent-fluft-poro-before-stale-prompt-replay";
        const string staleIntentId = "intent-fluft-poro-stale-action-prompt-replay";

        var accepted = await session.SubmitAsync(
            "P1",
            acceptedIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "UNIT_EXHAUSTED", "COST_PAID", "STACK_ITEM_ADDED"], accepted.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var acceptedHash = MatchStateHasher.Hash(accepted.State);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var stackItem = Assert.Single(accepted.State.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityEffectKind, stackItem.EffectKind);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroCardNo, stackItem.CardNo);
        Assert.True(accepted.State.CardObjects[FluftObjectId].IsExhausted);
        Assert.Equal(TimingStates.NeutralClosed, accepted.State.TimingState);
        Assert.Equal("P1", accepted.State.PriorityPlayerId);
        Assert.Contains(CommandTypes.PassPriority, accepted.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, accepted.Prompts["P1"].Actions);
        AssertFluftPoroStackPriorityPromptQueueAudit(accepted);

        var acceptedJournalEntry = Assert.Single(journal.Entries);
        Assert.Equal(state.RoomId, acceptedJournalEntry.RoomId);
        Assert.Equal("P1", acceptedJournalEntry.PlayerId);
        Assert.Equal(acceptedIntentId, acceptedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ActivateAbility, acceptedJournalEntry.CommandType);
        Assert.True(acceptedJournalEntry.Accepted);
        Assert.Null(acceptedJournalEntry.ErrorMessage);
        Assert.True(acceptedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.ActivateAbility, acceptedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(FluftObjectId, acceptedJournalEntry.RawCommand.Value.GetProperty("sourceObjectId").GetString());
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId, acceptedJournalEntry.RawCommand.Value.GetProperty("abilityId").GetString());
        Assert.Empty(acceptedJournalEntry.RawCommand.Value.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Empty(acceptedJournalEntry.RawCommand.Value.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, acceptedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, acceptedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(acceptedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(acceptedJournalEntry.Snapshots));

        var replay = await session.SubmitAsync(
            "P1",
            staleIntentId,
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
        Assert.True(replay.State.CardObjects[FluftObjectId].IsExhausted);
        Assert.Equal([FluftObjectId], replay.State.PlayerZones["P1"].Battlefields);
        var replayStackItem = Assert.Single(replay.State.StackItems);
        Assert.Equal(stackItem.StackItemId, replayStackItem.StackItemId);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityEffectKind, replayStackItem.EffectKind);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroCardNo, replayStackItem.CardNo);
        Assert.Empty(replayStackItem.TargetObjectIds);
        Assert.Equal(TimingStates.NeutralClosed, replay.State.TimingState);
        Assert.Equal("P1", replay.State.PriorityPlayerId);
        Assert.Contains(CommandTypes.PassPriority, replay.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, replay.Prompts["P1"].Actions);
        Assert.DoesNotContain(replay.State.CardObjects.Values, card => string.Equals(card.CardNo, P4ActivatedAbilityCatalog.WarhawkTokenCardNo, StringComparison.Ordinal));
        AssertFluftPoroStackPriorityPromptQueueAudit(replay);

        Assert.Equal(2, journal.Entries.Count);
        var rejectedJournalEntry = journal.Entries[1];
        Assert.Equal(state.RoomId, rejectedJournalEntry.RoomId);
        Assert.Equal("P1", rejectedJournalEntry.PlayerId);
        Assert.Equal(staleIntentId, rejectedJournalEntry.ClientIntentId);
        Assert.Equal(CommandTypes.ActivateAbility, rejectedJournalEntry.CommandType);
        Assert.False(rejectedJournalEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedJournalEntry.ErrorMessage);
        Assert.True(rejectedJournalEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.ActivateAbility, rejectedJournalEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(FluftObjectId, rejectedJournalEntry.RawCommand.Value.GetProperty("sourceObjectId").GetString());
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId, rejectedJournalEntry.RawCommand.Value.GetProperty("abilityId").GetString());
        Assert.Empty(rejectedJournalEntry.RawCommand.Value.GetProperty("targetObjectIds").EnumerateArray());
        Assert.Empty(rejectedJournalEntry.RawCommand.Value.GetProperty("optionalCosts").EnumerateArray());
        Assert.Equal(prompt.PromptId, rejectedJournalEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedJournalEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.Equal(accepted.State.Tick, rejectedJournalEntry.StartedTick);
        Assert.Equal(replay.State.Tick, rejectedJournalEntry.CompletedTick);
        Assert.Empty(rejectedJournalEntry.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(rejectedJournalEntry.AuthoritativeState));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(rejectedJournalEntry.Snapshots));
        Assert.False(rejectedJournalEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var duplicateRejected = await session.SubmitAsync(
            "P1",
            staleIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(duplicateRejected.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, duplicateRejected.ErrorCode);
        Assert.Equal(replay.ErrorMessage, duplicateRejected.ErrorMessage);
        Assert.Empty(duplicateRejected.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(duplicateRejected.State));
        Assert.Equal(replay.State.Tick, duplicateRejected.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(duplicateRejected.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(duplicateRejected.Snapshots));
        AssertFluftPoroStackPriorityPromptQueueAudit(duplicateRejected);
        Assert.Equal(2, journal.Entries.Count);

        var conflict = await session.SubmitAsync(
            "P1",
            staleIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(replay.State.Tick, conflict.State.Tick);
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        AssertFluftPoroStackPriorityPromptQueueAudit(conflict);
        Assert.Equal(2, journal.Entries.Count);
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    private static void AssertFluftPoroStackPriorityPromptQueueAudit(ResolutionResult result)
    {
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.Null(result.State.FocusPlayerId);
        Assert.Empty(result.State.PassedFocusPlayerIds);
        Assert.Empty(result.State.BattlefieldTasks);
        Assert.False(result.State.PendingTaskQueue.HasTasks);
        Assert.False(result.State.PendingTaskQueue.IsBlocking);
        Assert.Equal("IDLE", result.State.PendingTaskQueue.Phase);
        Assert.Null(result.State.PendingTaskQueue.ActiveTaskId);
        Assert.Empty(result.State.PendingTaskQueue.Tasks);

        Assert.Equal([FluftObjectId], result.State.PlayerZones["P1"].Battlefields);
        Assert.True(result.State.CardObjects[FluftObjectId].IsExhausted);
        Assert.Equal("P1", result.State.ObjectLocations[FluftObjectId].PlayerId);
        Assert.Equal("BATTLEFIELD", result.State.ObjectLocations[FluftObjectId].Zone);
        Assert.Equal("P1-MAIN", result.State.ObjectLocations[FluftObjectId].BattlefieldObjectId);
        Assert.DoesNotContain(
            result.State.CardObjects.Values,
            card => string.Equals(card.CardNo, P4ActivatedAbilityCatalog.WarhawkTokenCardNo, StringComparison.Ordinal));

        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal("P1", stackItem.ControllerId);
        Assert.Equal(FluftObjectId, stackItem.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroCardNo, stackItem.CardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityEffectKind, stackItem.EffectKind);
        Assert.Empty(stackItem.TargetObjectIds);

        foreach (var snapshot in result.Snapshots.Values)
        {
            Assert.Equal(result.State.Tick, snapshot.Tick);
            Assert.Equal("P1", snapshot.ActivePlayerId);
            Assert.Equal("P1", Assert.IsType<string>(snapshot.Timing["turnPlayerId"]));
            Assert.Equal(MatchPhases.Main, Assert.IsType<string>(snapshot.Timing["phase"]));
            Assert.Equal(TimingStates.NeutralClosed, Assert.IsType<string>(snapshot.Timing["timingState"]));
            Assert.Equal("P1", Assert.IsType<string>(snapshot.Timing["priorityPlayerId"]));
            Assert.Null(snapshot.Timing["focusPlayerId"]);

            var snapshotStackItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            Assert.Equal(stackItem.StackItemId, Assert.IsType<string>(snapshotStackItem["stackItemId"]));
            Assert.Equal("P1", Assert.IsType<string>(snapshotStackItem["controllerId"]));
            Assert.Equal(FluftObjectId, Assert.IsType<string>(snapshotStackItem["sourceObjectId"]));
            Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroCardNo, Assert.IsType<string>(snapshotStackItem["cardNo"]));

            var queue = Assert.IsType<Dictionary<string, object?>>(snapshot.Timing["pendingTaskQueue"]);
            Assert.False(Assert.IsType<bool>(queue["hasTasks"]));
            Assert.False(Assert.IsType<bool>(queue["isBlocking"]));
            Assert.Equal("IDLE", Assert.IsType<string>(queue["phase"]));
            Assert.Null(queue["activeTaskId"]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(queue["tasks"]));
        }

        Assert.Equal("P1", result.Prompts["P1"].PlayerId);
        Assert.True(result.Prompts["P1"].Actionable);
        Assert.Equal(PromptTypes.StackPriority, result.Prompts["P1"].View?.Type);
        Assert.Equal(stackItem.StackItemId, result.Prompts["P1"].View?.RelatedStackItemId);
        Assert.Contains(CommandTypes.PassPriority, result.Prompts["P1"].Actions);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, result.Prompts["P1"].Actions);
        Assert.DoesNotContain(result.Prompts["P1"].Candidates ?? [], candidate =>
            string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal)
            && (candidate.Sources ?? []).Any(source => string.Equals(source.Id, FluftObjectId, StringComparison.Ordinal)));
        Assert.Equal(result.State.Tick, result.Prompts["P1"].SnapshotTick);

        Assert.Equal("P2", result.Prompts["P2"].PlayerId);
        Assert.False(result.Prompts["P2"].Actionable);
        Assert.DoesNotContain(CommandTypes.ActivateAbility, result.Prompts["P2"].Actions);
        Assert.DoesNotContain(CommandTypes.PassPriority, result.Prompts["P2"].Actions);
        Assert.Equal(result.State.Tick, result.Prompts["P2"].SnapshotTick);
    }

    [Fact]
    public async Task FluftPoroStackPassPassCreatesTwoWarhawksInControllerBase()
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateFluftPoroAsync(BuildFluftPoroState(), engine);
        Assert.True(activated.Accepted, activated.ErrorMessage);

        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-fluft-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-fluft-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Equal([FluftObjectId], p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.True(p2Pass.State.CardObjects[FluftObjectId].IsExhausted);
        var tokenObjectIds = p2Pass.State.PlayerZones["P1"].Base
            .Where(objectId => string.Equals(p2Pass.State.CardObjects[objectId].CardNo, P4ActivatedAbilityCatalog.WarhawkTokenCardNo, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, tokenObjectIds.Length);
        foreach (var tokenObjectId in tokenObjectIds)
        {
            var token = p2Pass.State.CardObjects[tokenObjectId];
            Assert.Equal(P4ActivatedAbilityCatalog.WarhawkTokenCardNo, token.CardNo);
            Assert.Equal("P1", token.OwnerId);
            Assert.Equal("P1", token.ControllerId);
            Assert.Equal(1, token.Power);
            Assert.Contains(CardObjectTags.UnitCard, token.Tags);
            Assert.Contains(CardObjectTags.Spellshield, token.Tags);
            Assert.Equal("BASE", p2Pass.State.ObjectLocations[tokenObjectId].Zone);
        }

        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId, StringComparison.Ordinal));
        var tokenEvents = p2Pass.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, tokenEvents.Length);
        foreach (var tokenEvent in tokenEvents)
        {
            Assert.Equal(P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId, tokenEvent.Payload["abilityId"]);
            Assert.Equal(P4ActivatedAbilityCatalog.WarhawkTokenCardNo, tokenEvent.Payload["tokenCardNo"]);
            Assert.Equal("BASE", tokenEvent.Payload["destinationZone"]);
        }
    }

    [Theory]
    [InlineData("wrong-timing")]
    [InlineData("spell-duel")]
    [InlineData("non-active-player")]
    [InlineData("target")]
    [InlineData("unsupported-optional-cost")]
    [InlineData("recycle-rune")]
    [InlineData("temporary-resource")]
    [InlineData("missing-source")]
    [InlineData("source-base")]
    [InlineData("source-exhausted")]
    [InlineData("wrong-controller")]
    [InlineData("wrong-card")]
    [InlineData("face-down-source")]
    [InlineData("standby-source")]
    public async Task FluftPoroRejectsInvalidCommandsWithoutMutation(string scenario)
    {
        var state = BuildInvalidScenarioState(scenario);
        var command = scenario switch
        {
            "target" => FluftPoroCommand(targetObjectIds: ["P2-TARGET"]),
            "unsupported-optional-cost" => FluftPoroCommand(optionalCosts: ["UNSUPPORTED_OPTIONAL_COST"]),
            "recycle-rune" => FluftPoroCommand(optionalCosts: ["RECYCLE_RUNE:P1-RUNE-BLUE"]),
            "temporary-resource" => FluftPoroCommand(optionalCosts: [PaymentCostRules.TemporaryPaymentResourceActionId("MALZAHAR:TEMP-FLUFT")]),
            "missing-source" => new ActivateAbilityCommand(
                "P1-MISSING-FLUFT",
                P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId,
                [],
                null),
            _ => FluftPoroCommand()
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    private static async Task<ResolutionResult> ActivateFluftPoroAsync(
        MatchState state,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-fluft-poro", "P1", CommandTypes.ActivateAbility),
            FluftPoroCommand(),
            CancellationToken.None);
    }

    private static ActivateAbilityCommand FluftPoroCommand(
        IReadOnlyList<string>? targetObjectIds = null,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            FluftObjectId,
            P4ActivatedAbilityCatalog.FluftPoroWarhawkAbilityId,
            targetObjectIds ?? [],
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
            new PlayerIntent("intent-fluft-invalid", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    private static MatchState BuildInvalidScenarioState(string scenario)
    {
        var state = BuildFluftPoroState();

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
            "spell-duel" => state with
            {
                TimingState = TimingStates.SpellDuelOpen,
                FocusPlayerId = "P1"
            },
            "non-active-player" => state with
            {
                ActivePlayerId = "P2",
                TurnPlayerId = "P2"
            },
            "source-base" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with
                    {
                        Base = [FluftObjectId],
                        Battlefields = []
                    }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    FluftObjectId,
                    new ObjectLocationState("P1", "BASE"))
            },
            "source-exhausted" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    FluftObjectId,
                    state.CardObjects[FluftObjectId] with { IsExhausted = true })
            },
            "wrong-controller" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    FluftObjectId,
                    state.CardObjects[FluftObjectId] with { ControllerId = "P2" })
            },
            "wrong-card" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    FluftObjectId,
                    state.CardObjects[FluftObjectId] with { CardNo = "UNL-161/219" })
            },
            "face-down-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    FluftObjectId,
                    state.CardObjects[FluftObjectId] with { IsFaceDown = true })
            },
            "standby-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    FluftObjectId,
                    state.CardObjects[FluftObjectId] with
                    {
                        Tags = state.CardObjects[FluftObjectId].Tags
                            .Concat([CardObjectTags.Standby])
                            .ToArray()
                    })
            },
            _ => state
        };
    }

    private static MatchState BuildFluftPoroState()
    {
        return new MatchState(
            "room-fluft-poro",
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
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [FluftObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            playerScores: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [FluftObjectId] = Unit(FluftObjectId, P4ActivatedAbilityCatalog.FluftPoroCardNo, "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [FluftObjectId] = new("P1", "BATTLEFIELD", "P1-MAIN")
            });
    }

    private static CardObjectState Unit(string objectId, string cardNo, string playerId)
    {
        return new CardObjectState(
            objectId,
            power: 2,
            tags: [CardObjectTags.UnitCard],
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
}
