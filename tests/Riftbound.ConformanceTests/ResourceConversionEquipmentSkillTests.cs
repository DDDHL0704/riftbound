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

        var genericResult = await new CoreRuleEngine().ResolveAsync(
            resourceState with { PendingPayment = genericPayment },
            new PlayerIntent("intent-ancient-stele-pay-generic", "P1", CommandTypes.PayCost),
            new PayCostCommand(genericPayment.PaymentId, genericPayment.PaymentWindow, [resourceAction, "SPEND_POWER:any:2"]),
            CancellationToken.None);

        Assert.True(genericResult.Accepted, genericResult.ErrorMessage);
        Assert.Empty(genericResult.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, genericResult.State.RunePools["P1"]);
        Assert.Contains(genericResult.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));

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
}
