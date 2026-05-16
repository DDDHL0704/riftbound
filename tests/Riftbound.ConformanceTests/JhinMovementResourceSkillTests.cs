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

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(entry["abilityId"] as string, P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, StringComparison.Ordinal));
        Assert.Equal(JhinObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinCardNo, requirement["cardNo"]);
        Assert.True(Assert.IsType<bool>(requirement["resourceSkill"]));
        Assert.True(Assert.IsType<bool>(requirement["paymentOnly"]));
        Assert.True(Assert.IsType<bool>(requirement["movementTriggered"]));
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedMana, requirement["generatedMana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.JhinMoveResourceGeneratedPower, requirement["generatedPower"]);
        Assert.Equal("server-captured-movement-trigger-open-main", requirement["timingPolicy"]);
        Assert.Equal("no-ordinary-stack-item", requirement["stackPolicy"]);
        Assert.True(Assert.IsType<bool>(requirement["generatedResourceCannotBeTargetedAsResponse"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]);
        var requiredOptionalCosts = Assert.IsAssignableFrom<string[]>(requirement["requiredOptionalCosts"]);
        var triggerChoice = Assert.Single(optionalCostChoices);
        Assert.Equal($"{P4ActivatedAbilityCatalog.JhinMoveTriggerOptionalCostPrefix}{trigger.TriggerId}", triggerChoice.Id);
        Assert.Equal([triggerChoice.Id], requiredOptionalCosts);

        AssertNoJhinResourceSkill(moved.Prompts["P2"]);
    }

    [Fact]
    public async Task JhinMovementResourceSkillGainsManaAndPaymentOnlyPowerWithoutStackResponse()
    {
        var moved = await MoveJhinAsync(BuildJhinBaseState());
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

        var activatedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal("no-ordinary-stack-item", activatedEvent.Payload["stackPolicy"]);
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["generatedResourceCannotBeTargetedAsResponse"]));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
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

        var result = await new CoreRuleEngine().ResolveAsync(
            activated.State with { PendingPayment = pendingPayment },
            new PlayerIntent("intent-jhin-pay-generated", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, "SPEND_MANA:1", "SPEND_POWER:any:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.State.PendingPayment);
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
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

    private static string JhinTriggerChoice(MatchState state)
    {
        var trigger = Assert.Single(state.TriggerQueue);
        return $"{P4ActivatedAbilityCatalog.JhinMoveTriggerOptionalCostPrefix}{trigger.TriggerId}";
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
}
