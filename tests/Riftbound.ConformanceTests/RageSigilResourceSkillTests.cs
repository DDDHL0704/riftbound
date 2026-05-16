using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RageSigilResourceSkillTests
{
    private const string RageSigilObjectId = "P1-RAGE-SIGIL";
    private const string PendingSpellObjectId = "P2-PENDING-SPELL";
    private const string PendingStackItemId = "STACK-P2-PENDING-SPELL";

    [Fact]
    public void CatalogExposesRageSigilTypedReactionResourceSkill()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.RageSigilResourceAbilityId,
            out var ability));

        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilCardNo, ability.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilResourceAbilityEffectKind, ability.EffectKind);
        Assert.True(ability.IsResourceSkill);
        Assert.True(ability.PaymentOnlyResource);
        Assert.True(ability.ReactionSpeed);
        Assert.True(ability.ExhaustsSourceAsCost);
        Assert.True(ability.RequiresBaseEquipmentSource);
        Assert.False(ability.RequiresBattlefieldSource);
        Assert.Equal(0, ability.RequiredTargetCount);
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilTypedResourceRestriction, ability.ResourceRestriction);
        var generatedPowerByTrait = P4ActivatedAbilityCatalog.GeneratedPowerByTraitForAbility(ability);
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilGeneratedRedPower, generatedPowerByTrait[RuneTrait.Red]);
    }

    [Fact]
    public void RageSigilReactionPromptExposesBaseEquipmentTypedPaymentOnlyResourceSkill()
    {
        var state = BuildRageSigilPriorityState();
        var prompts = ResolutionResult.BuildPrompts(state);

        var p1Prompt = prompts["P1"];
        Assert.True(p1Prompt.Actionable);
        Assert.Contains(CommandTypes.ActivateAbility, p1Prompt.Actions);
        var activateCandidate = Assert.Single(
            p1Prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Equal([RageSigilObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var requirement = Assert.Single(sourceRequirements, entry =>
            string.Equals(
                entry["abilityId"] as string,
                P4ActivatedAbilityCatalog.RageSigilResourceAbilityId,
                StringComparison.Ordinal));
        Assert.Equal(RageSigilObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilCardNo, requirement["cardNo"]);
        Assert.Equal(0, requirement["minTargetCount"]);
        Assert.Equal(0, requirement["maxTargetCount"]);
        Assert.True(Assert.IsType<bool>(requirement["resourceSkill"]));
        Assert.True(Assert.IsType<bool>(requirement["reactionSpeed"]));
        Assert.True(Assert.IsType<bool>(requirement["paymentOnly"]));
        Assert.True(Assert.IsType<bool>(requirement["typedPaymentOnlyResource"]));
        Assert.True(Assert.IsType<bool>(requirement["requiresBaseEquipmentSource"]));
        Assert.True(Assert.IsType<bool>(requirement["exhaustsSource"]));
        Assert.True(Assert.IsType<bool>(requirement["resolvesImmediately"]));
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilTypedResourceRestriction, requirement["resourceRestriction"]);
        Assert.Equal("stack-priority-reaction-representative", requirement["timingPolicy"]);
        Assert.Equal("resolves-immediately-without-stack-item", requirement["reactionPolicy"]);
        Assert.Equal("temporary-payment-resource-ledger", requirement["resourceLifecycle"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(requirement["allowedPaymentKinds"]));
        var generatedPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(requirement["generatedPowerByTrait"]);
        Assert.Equal(1, generatedPowerByTrait[RuneTrait.Red]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        Assert.Empty(targetChoicesByIndex);

        Assert.DoesNotContain(
            prompts["P2"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RageSigilReactionCommandExhaustsSourceCreatesRedTemporaryLedgerWithoutStackItem()
    {
        var state = BuildRageSigilPriorityState();

        var result = await ResolveRageSigilAsync(state);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.ErrorCode);
        Assert.Equal(1, result.State.Tick);
        Assert.True(result.State.CardObjects[RageSigilObjectId].IsExhausted);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Equal([PendingStackItemId], result.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P1", result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal(RageSigilObjectId, temporaryResource.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilResourceAbilityId, temporaryResource.AbilityId);
        Assert.Equal(0, temporaryResource.GeneratedPower);
        Assert.Equal(0, temporaryResource.RemainingPower);
        Assert.Equal(1, temporaryResource.GeneratedPowerByTrait[RuneTrait.Red]);
        Assert.Equal(1, temporaryResource.RemainingPowerByTrait[RuneTrait.Red]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], temporaryResource.AllowedPaymentKinds);

        var activatedEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal(RageSigilObjectId, activatedEvent.Payload["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilCardNo, activatedEvent.Payload["cardNo"]);
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilResourceAbilityId, activatedEvent.Payload["abilityId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilResourceAbilityEffectKind, activatedEvent.Payload["effectKind"]);
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["resourceSkill"]));
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["reactionSpeed"]));
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["paymentOnly"]));
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["typedPaymentOnlyResource"]));
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilTypedResourceRestriction, activatedEvent.Payload["resourceRestriction"]);
        Assert.Equal("temporary-payment-resource-ledger", activatedEvent.Payload["resourceLifecycle"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(activatedEvent.Payload["allowedPaymentKinds"]));
        var generatedPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(activatedEvent.Payload["generatedPowerByTrait"]);
        Assert.Equal(1, generatedPowerByTrait[RuneTrait.Red]);

        var powerEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal(RageSigilObjectId, powerEvent.Payload["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilCardNo, powerEvent.Payload["cardNo"]);
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilResourceAbilityId, powerEvent.Payload["abilityId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilResourceAbilityEffectKind, powerEvent.Payload["effectKind"]);
        Assert.Equal(temporaryResource.ResourceId, powerEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal("temporary-payment-resource-ledger", powerEvent.Payload["restrictionLifecycle"]);
        Assert.Equal(P4ActivatedAbilityCatalog.RageSigilTypedResourceRestriction, powerEvent.Payload["resourceRestriction"]);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], Assert.IsType<string[]>(powerEvent.Payload["allowedPaymentKinds"]));
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(powerEvent.Payload["powerByTrait"]);
        Assert.Equal(1, powerByTrait[RuneTrait.Red]);
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(powerEvent.Payload["remainingPowerByTrait"])[RuneTrait.Red]);
    }

    [Theory]
    [InlineData("red-typed")]
    [InlineData("generic")]
    public async Task RageSigilTemporaryRedResourcePaysRuneCostsAndCleansUp(string caseName)
    {
        var resourceState = (await ResolveRageSigilAsync(BuildRageSigilPriorityState())).State;
        var temporaryResource = Assert.Single(resourceState.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = caseName == "red-typed"
            ? new PendingPaymentState(
                "PAY-RED-1",
                "TEST_PENDING_PAY_COST",
                "P1",
                powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                },
                legalPaymentChoiceIds: ["SPEND_POWER:red:1"])
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
        Assert.Contains(
            Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(payCostCandidate.Metadata)["paymentResourceChoices"]),
            choice => string.Equals(choice.Id, resourceAction, StringComparison.Ordinal));

        var spendChoice = caseName == "red-typed" ? "SPEND_POWER:red:1" : "SPEND_POWER:any:1";
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-rage-sigil-pay-{caseName}", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, spendChoice]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.State.PendingPayment);
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);

        var spentEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.Equal(temporaryResource.ResourceId, spentEvent.Payload["temporaryPaymentResourceId"]);
        Assert.Equal(0, spentEvent.Payload["consumedPower"]);
        var consumedPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(spentEvent.Payload["consumedPowerByTrait"]);
        Assert.Equal(1, consumedPowerByTrait[RuneTrait.Red]);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([temporaryResource.ResourceId], Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
    }

    [Theory]
    [InlineData("blue-typed")]
    [InlineData("mana-only")]
    public async Task RageSigilTemporaryRedResourceRejectsNonMatchingOrNonRunePaymentWithoutMutation(string caseName)
    {
        var resourceState = (await ResolveRageSigilAsync(BuildRageSigilPriorityState())).State;
        var temporaryResource = Assert.Single(resourceState.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = caseName == "blue-typed"
            ? new PendingPaymentState(
                "PAY-BLUE-1",
                "TEST_PENDING_PAY_COST",
                "P1",
                powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Blue] = 1
                },
                legalPaymentChoiceIds: ["SPEND_POWER:blue:1"])
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
        var spendChoice = caseName == "blue-typed" ? "SPEND_POWER:blue:1" : "SPEND_MANA:1";

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-rage-sigil-reject-pay-{caseName}", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, spendChoice]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    [Theory]
    [InlineData("wrong-priority")]
    [InlineData("open-main")]
    [InlineData("spell-duel-focus")]
    [InlineData("cleanup-blocking")]
    public async Task RageSigilRejectsWrongTimingWithoutMutation(string caseName)
    {
        var state = BuildInvalidTimingState(caseName);

        await AssertRejectedNoMutationAsync(
            state,
            "P1",
            RageSigilCommand(),
            ErrorCodes.PhaseNotAllowed);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("optional-cost")]
    [InlineData("exhausted")]
    [InlineData("face-down")]
    [InlineData("battlefield-source")]
    [InlineData("wrong-controller")]
    [InlineData("wrong-card")]
    public async Task RageSigilRejectsInvalidSourceOrPayloadWithoutMutation(string caseName)
    {
        var state = BuildInvalidCommandState(caseName);
        var command = caseName switch
        {
            "target" => RageSigilCommand(["P2-ANY-TARGET"]),
            "optional-cost" => RageSigilCommand(optionalCosts: [PaymentCostRules.TemporaryPaymentResourceActionId("MALZAHAR:TEMP")]),
            _ => RageSigilCommand()
        };

        await AssertRejectedNoMutationAsync(
            state,
            "P1",
            command,
            caseName switch
            {
                "wrong-card" => ErrorCodes.UnsupportedCardBehavior,
                "battlefield-source" => ErrorCodes.PhaseNotAllowed,
                _ => ErrorCodes.InvalidTarget
            });
    }

    private static async Task<ResolutionResult> ResolveRageSigilAsync(MatchState state)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-rage-sigil-resource-skill", "P1", CommandTypes.ActivateAbility),
            RageSigilCommand(),
            CancellationToken.None);
    }

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        string playerId,
        ActivateAbilityCommand command,
        string expectedErrorCode)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-rage-sigil-rejected-{expectedErrorCode}", playerId, CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    private static ActivateAbilityCommand RageSigilCommand(
        IReadOnlyList<string>? targetObjectIds = null,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            RageSigilObjectId,
            P4ActivatedAbilityCatalog.RageSigilResourceAbilityId,
            targetObjectIds ?? [],
            optionalCosts);
    }

    private static MatchState BuildInvalidTimingState(string caseName)
    {
        var state = BuildRageSigilPriorityState();
        return caseName switch
        {
            "wrong-priority" => state with
            {
                PriorityPlayerId = "P2"
            },
            "open-main" => state with
            {
                TimingState = TimingStates.NeutralOpen,
                PriorityPlayerId = null,
                StackItems = []
            },
            "spell-duel-focus" => state with
            {
                TimingState = TimingStates.SpellDuelOpen,
                PriorityPlayerId = null,
                StackItems = [],
                FocusPlayerId = "P1"
            },
            "cleanup-blocking" => state with
            {
                PendingPayment = new PendingPaymentState(
                    "PAY-BLOCKING",
                    "TEST_PENDING_PAY_COST",
                    "P1",
                    powerCost: 1,
                    legalPaymentChoiceIds: ["SPEND_POWER:any:1"])
            },
            _ => state
        };
    }

    private static MatchState BuildInvalidCommandState(string caseName)
    {
        var state = BuildRageSigilPriorityState();
        return caseName switch
        {
            "exhausted" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    RageSigilObjectId,
                    state.CardObjects[RageSigilObjectId] with { IsExhausted = true })
            },
            "face-down" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    RageSigilObjectId,
                    state.CardObjects[RageSigilObjectId] with { IsFaceDown = true })
            },
            "battlefield-source" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with
                    {
                        Base = [],
                        Battlefields = [RageSigilObjectId]
                    }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    RageSigilObjectId,
                    new ObjectLocationState("P1", "BATTLEFIELD", "P1-MAIN"))
            },
            "wrong-controller" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    RageSigilObjectId,
                    state.CardObjects[RageSigilObjectId] with { ControllerId = "P2" })
            },
            "wrong-card" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    RageSigilObjectId,
                    state.CardObjects[RageSigilObjectId] with { CardNo = "SFD·223/221" })
            },
            _ => state
        };
    }

    private static MatchState BuildRageSigilPriorityState()
    {
        return new MatchState(
            "room-rage-sigil-resource",
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
                    Base = [RageSigilObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [RageSigilObjectId] = Equipment(
                    RageSigilObjectId,
                    P4ActivatedAbilityCatalog.RageSigilCardNo,
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
                [RageSigilObjectId] = new("P1", "BASE"),
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
}
