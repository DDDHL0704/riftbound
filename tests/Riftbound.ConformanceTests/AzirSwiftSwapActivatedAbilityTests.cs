using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AzirSwiftSwapActivatedAbilityTests
{
    private const string AzirObjectId = "P1-AZIR";
    private const string TargetObjectId = "P1-TARGET";
    private const string BaseTargetObjectId = "P1-BASE-TARGET";
    private const string GreenRuneObjectId = "P1-RUNE-GREEN";
    private const string RedRuneObjectId = "P1-RUNE-RED";
    private const string AzirBattlefieldObjectId = "BF-AZIR";
    private const string TargetBattlefieldObjectId = "BF-TARGET";
    private const string TargetArmamentObjectId = "P1-TARGET-ARMAMENT";
    private const string SecondTargetArmamentObjectId = "P1-TARGET-ARMAMENT-B";
    private const string OtherUnitArmamentObjectId = "P1-OTHER-UNIT-ARMAMENT";
    private const string UnattachedArmamentObjectId = "P1-UNATTACHED-ARMAMENT";
    private const string NonEquipmentAttachedObjectId = "P1-NON-EQUIPMENT-ATTACHED";
    private const string EnemyControlledArmamentObjectId = "P2-ENEMY-CONTROLLED-ARMAMENT";

    [Fact]
    public void CatalogExposesAzirSwiftSwapForBothCollectorNumbers()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId,
            out var ability));

        Assert.Equal(P4ActivatedAbilityCatalog.AzirCardNo, ability.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityEffectKind, ability.EffectKind);
        Assert.Equal(0, ability.ManaCost);
        Assert.Equal(0, ability.PowerCost);
        Assert.Equal(1, ability.RequiredTargetCount);
        Assert.False(ability.ExhaustsSourceAsCost);
        Assert.False(ability.ReactionSpeed);
        var powerCostByTrait = P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(ability);
        Assert.Equal(1, powerCostByTrait[RuneTrait.Green]);
        Assert.True(P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, P4ActivatedAbilityCatalog.AzirCardNo));
        Assert.True(P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, P4ActivatedAbilityCatalog.AzirAltCardNo));
    }

    [Fact]
    public void PromptExposesAzirSwiftSwapRequirementWithGreenCostTargetsAndOnceMetadata()
    {
        var state = BuildAzirState(
            P4ActivatedAbilityCatalog.AzirCardNo,
            new RunePool(0, 0),
            baseObjectIds: [GreenRuneObjectId],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [GreenRuneObjectId] = RuneCard(GreenRuneObjectId, RuneTrait.Green)
            }) with
        {
            TemporaryPaymentResources = [GenericTemporaryResource("MALZAHAR:TEMP-AZIR-PROMPT")]
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.True(activateCandidate.Enabled);
        Assert.Equal([AzirObjectId], (activateCandidate.Sources ?? []).Select(choice => choice.Id).ToArray());
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(entry["abilityId"] as string, P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, StringComparison.Ordinal));

        Assert.Equal(AzirObjectId, requirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.AzirCardNo, requirement["cardNo"]);
        Assert.Equal(0, requirement["manaCost"]);
        Assert.Equal(0, requirement["powerCost"]);
        var powerCostByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(requirement["powerCostByTrait"]);
        Assert.Equal(1, powerCostByTrait[RuneTrait.Green]);
        Assert.Equal(1, requirement["minTargetCount"]);
        Assert.Equal(1, requirement["maxTargetCount"]);
        Assert.False(Assert.IsType<bool>(requirement["exhaustsSource"]));
        Assert.False(Assert.IsType<bool>(requirement["resolvesImmediately"]));
        Assert.True(Assert.IsType<bool>(requirement["swift"]));
        Assert.True(Assert.IsType<bool>(requirement["oncePerTurn"]));
        Assert.Equal("controlled-face-up-unit", requirement["targetScope"]);
        Assert.Equal("ordinary-stack-item-before-swap", requirement["stackPolicy"]);
        Assert.Equal("payment-plan-typed-green", requirement["paymentPolicy"]);
        Assert.Equal(P4ActivatedAbilityCatalog.AzirArmamentReattachPolicy, requirement["armamentReattachPolicy"]);
        Assert.Equal(P4ActivatedAbilityCatalog.AzirArmamentReattachOptionalCostPrefix, requirement["armamentReattachChoicePrefix"]);

        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["targetChoicesByIndex"]);
        Assert.Equal(
            [BaseTargetObjectId, TargetObjectId],
            targetChoicesByIndex["0"].Select(choice => choice.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray());
        var armamentReattachChoicesByTarget = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["armamentReattachChoicesByTargetObjectId"]);
        Assert.Empty(armamentReattachChoicesByTarget[TargetObjectId]);

        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["paymentResourceChoices"]).ToArray();
        Assert.Equal([$"RECYCLE_RUNE:{GreenRuneObjectId}"], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        Assert.DoesNotContain(
            paymentResourceChoices,
            choice => choice.Id.StartsWith(PaymentCostRules.TemporaryPaymentResourceActionPrefix, StringComparison.Ordinal));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            requirement["paymentResourcePowerByChoice"]);
        Assert.Equal(RuneTrait.Green, paymentResourcePowerByChoice[$"RECYCLE_RUNE:{GreenRuneObjectId}"]["trait"]);
        var availablePowerByTraitWithResources = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            requirement["availablePowerByTraitWithPaymentResources"]);
        Assert.Equal(1, availablePowerByTraitWithResources[RuneTrait.Green]);
    }

    [Fact]
    public void PromptExposesImplementedAzirArmamentReattachChoicesByTarget()
    {
        var state = BuildAzirStateWithArmaments();
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var requirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(entry["abilityId"] as string, P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, StringComparison.Ordinal));

        Assert.Equal(P4ActivatedAbilityCatalog.AzirArmamentReattachPolicy, requirement["armamentReattachPolicy"]);
        var choicesByTarget = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["armamentReattachChoicesByTargetObjectId"]);
        Assert.Equal(
            [
                P4ActivatedAbilityCatalog.AzirArmamentReattachOptionalCostId(TargetArmamentObjectId),
                P4ActivatedAbilityCatalog.AzirArmamentReattachOptionalCostId(SecondTargetArmamentObjectId)
            ],
            choicesByTarget[TargetObjectId].Select(choice => choice.Id).ToArray());
        Assert.Equal(
            [P4ActivatedAbilityCatalog.AzirArmamentReattachOptionalCostId(OtherUnitArmamentObjectId)],
            choicesByTarget[BaseTargetObjectId].Select(choice => choice.Id).ToArray());
        Assert.DoesNotContain(
            choicesByTarget[TargetObjectId],
            choice => choice.Id.EndsWith(EnemyControlledArmamentObjectId, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(P4ActivatedAbilityCatalog.AzirCardNo)]
    [InlineData(P4ActivatedAbilityCatalog.AzirAltCardNo)]
    public async Task AzirCommandPaysGreenCreatesStackAndResolutionSwapsPreciseLocations(string cardNo)
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateAzirAsync(
            BuildAzirState(
                cardNo,
                new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Green] = 1
                })),
            engine: engine);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], activated.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), activated.State.RunePools["P1"]);
        Assert.False(activated.State.CardObjects[AzirObjectId].IsExhausted);
        Assert.Contains(
            P4ActivatedAbilityCatalog.AzirSwiftSwapUsedThisTurnEffectId("P1", AzirObjectId),
            activated.State.UntilEndOfTurnEffects);
        var stackItem = Assert.Single(activated.State.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityEffectKind, stackItem.EffectKind);
        Assert.Equal(cardNo, stackItem.CardNo);
        Assert.Equal([TargetObjectId], stackItem.TargetObjectIds);
        var costEvent = Assert.Single(activated.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("ACTIVATE_ABILITY", costEvent.Payload["paymentWindow"]);
        Assert.Equal(0, costEvent.Payload["genericPower"]);
        Assert.Equal(1, costEvent.Payload["totalPowerCost"]);
        var costPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(1, costPowerByTrait[RuneTrait.Green]);

        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-azir-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-azir-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.Contains(AzirObjectId, p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Contains(TargetObjectId, p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(TargetBattlefieldObjectId, p2Pass.State.ObjectLocations[AzirObjectId].BattlefieldObjectId);
        Assert.Equal(AzirBattlefieldObjectId, p2Pass.State.ObjectLocations[TargetObjectId].BattlefieldObjectId);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "ABILITY_RESOLVED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["abilityId"] as string, P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId, StringComparison.Ordinal));
        var swapEvent = Assert.Single(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_LOCATIONS_SWAPPED", StringComparison.Ordinal));
        Assert.Equal(AzirObjectId, swapEvent.Payload["sourceObjectId"]);
        Assert.Equal(TargetObjectId, swapEvent.Payload["targetObjectId"]);
        Assert.False(Assert.IsType<bool>(swapEvent.Payload["armamentReattachApplied"]));
    }

    [Fact]
    public async Task AzirNoReattachChoiceRemainsLegalWhenTargetHasArmament()
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateAzirAsync(BuildAzirStateWithArmaments(), engine: engine);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.Empty(Assert.Single(activated.State.StackItems).OptionalCosts);

        var resolved = await ResolveAzirStackAsync(engine, activated.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Equal(TargetObjectId, resolved.State.CardObjects[TargetArmamentObjectId].AttachedToObjectId);
        Assert.DoesNotContain(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_REATTACHED", StringComparison.Ordinal));
        var swapEvent = Assert.Single(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_LOCATIONS_SWAPPED", StringComparison.Ordinal));
        Assert.False(Assert.IsType<bool>(swapEvent.Payload["armamentReattachApplied"]));
        Assert.Equal(P4ActivatedAbilityCatalog.AzirArmamentReattachPolicy, swapEvent.Payload["armamentReattachPolicy"]);
        Assert.Null(swapEvent.Payload["selectedArmamentObjectId"]);
    }

    [Fact]
    public async Task AzirSelectedLegalArmamentReattachesToAzirOnResolution()
    {
        var engine = new CoreRuleEngine();
        var selectedOptionalCost = P4ActivatedAbilityCatalog.AzirArmamentReattachOptionalCostId(TargetArmamentObjectId);
        var activated = await ActivateAzirAsync(
            BuildAzirStateWithArmaments(),
            optionalCosts: [selectedOptionalCost],
            engine: engine);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.Equal([selectedOptionalCost], Assert.Single(activated.State.StackItems).OptionalCosts);

        var resolved = await ResolveAzirStackAsync(engine, activated.State);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Equal(AzirObjectId, resolved.State.CardObjects[TargetArmamentObjectId].AttachedToObjectId);
        Assert.Equal(TargetObjectId, resolved.State.CardObjects[SecondTargetArmamentObjectId].AttachedToObjectId);
        var swapEvent = Assert.Single(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_LOCATIONS_SWAPPED", StringComparison.Ordinal));
        Assert.True(Assert.IsType<bool>(swapEvent.Payload["armamentReattachApplied"]));
        Assert.Equal(TargetArmamentObjectId, swapEvent.Payload["selectedArmamentObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.AzirArmamentReattachPolicy, swapEvent.Payload["armamentReattachPolicy"]);
        var reattachEvent = Assert.Single(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_REATTACHED", StringComparison.Ordinal));
        Assert.Equal(TargetObjectId, reattachEvent.Payload["previousAttachedToObjectId"]);
        Assert.Equal(AzirObjectId, reattachEvent.Payload["attachedToObjectId"]);
        Assert.Equal(TargetArmamentObjectId, reattachEvent.Payload["equipmentObjectId"]);
    }

    [Fact]
    public async Task AzirCanRecycleGreenRuneForTypedGreenShortfall()
    {
        var paymentResourceAction = $"RECYCLE_RUNE:{GreenRuneObjectId}";
        var state = BuildAzirState(
            P4ActivatedAbilityCatalog.AzirCardNo,
            new RunePool(0, 0),
            baseObjectIds: [GreenRuneObjectId],
            runeDeckObjectIds: ["P1-RUNE-BOTTOM"],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [GreenRuneObjectId] = RuneCard(GreenRuneObjectId, RuneTrait.Green),
                ["P1-RUNE-BOTTOM"] = RuneCard("P1-RUNE-BOTTOM", RuneTrait.Red)
            });

        var result = await ActivateAzirAsync(state, optionalCosts: [paymentResourceAction]);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["RUNE_RECYCLED", "POWER_GAINED", "ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.DoesNotContain(GreenRuneObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM", GreenRuneObjectId], result.State.PlayerZones["P1"].RuneDeck);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([GreenRuneObjectId], Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
    }

    [Fact]
    public async Task AzirOncePerTurnRejectsSecondActivationAndClearsAtTurnEnd()
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateAzirAsync(
            BuildAzirState(
                P4ActivatedAbilityCatalog.AzirCardNo,
                new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Green] = 2
                })),
            engine: engine);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var p1Pass = await engine.ResolveAsync(
            activated.State,
            new PlayerIntent("intent-azir-once-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-azir-once-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);

        await AssertRejectedNoMutationAsync(p2Pass.State, AzirCommand(), ErrorCodes.InvalidTarget);

        var endTurn = await engine.ResolveAsync(
            p2Pass.State,
            new PlayerIntent("intent-azir-end-turn", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);
        Assert.True(endTurn.Accepted, endTurn.ErrorMessage);
        Assert.DoesNotContain(
            P4ActivatedAbilityCatalog.AzirSwiftSwapUsedThisTurnEffectId("P1", AzirObjectId),
            endTurn.State.UntilEndOfTurnEffects);
    }

    [Theory]
    [InlineData("wrong-timing")]
    [InlineData("enemy-unit")]
    [InlineData("equipment")]
    [InlineData("rune")]
    [InlineData("battlefield")]
    [InlineData("hand-target")]
    [InlineData("face-down")]
    [InlineData("stale-target")]
    [InlineData("source-self")]
    [InlineData("dirty-controller-target")]
    [InlineData("wrong-trait-recycle")]
    [InlineData("generic-temporary-resource")]
    [InlineData("duplicate-recycle")]
    [InlineData("invalid-recycle")]
    [InlineData("unnecessary-recycle")]
    [InlineData("unsupported-optional-cost")]
    [InlineData("insufficient-green")]
    public async Task AzirRejectsInvalidCommandsWithoutMutation(string scenario)
    {
        var state = BuildInvalidScenarioState(scenario);
        var command = scenario switch
        {
            "enemy-unit" => AzirCommand(targetObjectIds: ["P2-ENEMY-UNIT"]),
            "equipment" => AzirCommand(targetObjectIds: ["P1-EQUIPMENT"]),
            "rune" => AzirCommand(targetObjectIds: ["P1-RUNE-TARGET"]),
            "battlefield" => AzirCommand(targetObjectIds: ["P1-BATTLEFIELD-CARD"]),
            "hand-target" => AzirCommand(targetObjectIds: ["P1-HAND-UNIT"]),
            "face-down" => AzirCommand(targetObjectIds: ["P1-FACE-DOWN"]),
            "stale-target" => AzirCommand(targetObjectIds: ["P1-STALE"]),
            "source-self" => AzirCommand(targetObjectIds: [AzirObjectId]),
            "dirty-controller-target" => AzirCommand(targetObjectIds: ["P1-DIRTY-P2-CONTROLLED"]),
            "wrong-trait-recycle" => AzirCommand(optionalCosts: [$"RECYCLE_RUNE:{RedRuneObjectId}"]),
            "generic-temporary-resource" => AzirCommand(optionalCosts: [PaymentCostRules.TemporaryPaymentResourceActionId("MALZAHAR:TEMP-AZIR")]),
            "duplicate-recycle" => AzirCommand(optionalCosts: [$"RECYCLE_RUNE:{GreenRuneObjectId}", $"RECYCLE_RUNE:{GreenRuneObjectId}"]),
            "invalid-recycle" => AzirCommand(optionalCosts: ["RECYCLE_RUNE:P1-RUNE-MISSING"]),
            "unnecessary-recycle" => AzirCommand(optionalCosts: [$"RECYCLE_RUNE:{GreenRuneObjectId}"]),
            "unsupported-optional-cost" => AzirCommand(optionalCosts: ["UNSUPPORTED_OPTIONAL_COST"]),
            _ => AzirCommand()
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    [Theory]
    [InlineData("missing-object")]
    [InlineData("non-equipment")]
    [InlineData("unattached")]
    [InlineData("attached-to-different-unit")]
    [InlineData("opponent-controlled")]
    [InlineData("multiple-choices")]
    public async Task AzirRejectsInvalidArmamentReattachChoicesWithoutMutation(string scenario)
    {
        var state = BuildAzirStateWithArmaments();
        var command = scenario switch
        {
            "missing-object" => AzirCommand(optionalCosts: [AzirReattachOptionalCost("P1-MISSING-ARMAMENT")]),
            "non-equipment" => AzirCommand(optionalCosts: [AzirReattachOptionalCost(NonEquipmentAttachedObjectId)]),
            "unattached" => AzirCommand(optionalCosts: [AzirReattachOptionalCost(UnattachedArmamentObjectId)]),
            "attached-to-different-unit" => AzirCommand(optionalCosts: [AzirReattachOptionalCost(OtherUnitArmamentObjectId)]),
            "opponent-controlled" => AzirCommand(optionalCosts: [AzirReattachOptionalCost(EnemyControlledArmamentObjectId)]),
            "multiple-choices" => AzirCommand(optionalCosts:
            [
                AzirReattachOptionalCost(TargetArmamentObjectId),
                AzirReattachOptionalCost(SecondTargetArmamentObjectId)
            ]),
            _ => AzirCommand()
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    [Fact]
    public async Task AzirStaleSelectedArmamentSkipsReattachWithoutFalseEventAndStillSwaps()
    {
        var engine = new CoreRuleEngine();
        var activated = await ActivateAzirAsync(
            BuildAzirStateWithArmaments(),
            optionalCosts: [AzirReattachOptionalCost(TargetArmamentObjectId)],
            engine: engine);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var staleCardObjects = activated.State.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        staleCardObjects[TargetArmamentObjectId] = staleCardObjects[TargetArmamentObjectId] with
        {
            AttachedToObjectId = BaseTargetObjectId
        };
        var staleState = activated.State with
        {
            CardObjects = staleCardObjects
        };

        var resolved = await ResolveAzirStackAsync(engine, staleState);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Equal(TargetBattlefieldObjectId, resolved.State.ObjectLocations[AzirObjectId].BattlefieldObjectId);
        Assert.Equal(AzirBattlefieldObjectId, resolved.State.ObjectLocations[TargetObjectId].BattlefieldObjectId);
        Assert.Equal(BaseTargetObjectId, resolved.State.CardObjects[TargetArmamentObjectId].AttachedToObjectId);
        Assert.DoesNotContain(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "EQUIPMENT_REATTACHED", StringComparison.Ordinal));
        var swapEvent = Assert.Single(resolved.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_LOCATIONS_SWAPPED", StringComparison.Ordinal));
        Assert.False(Assert.IsType<bool>(swapEvent.Payload["armamentReattachApplied"]));
        Assert.Equal(TargetArmamentObjectId, swapEvent.Payload["selectedArmamentObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.AzirArmamentReattachPolicy, swapEvent.Payload["armamentReattachPolicy"]);
    }

    private static async Task<ResolutionResult> ActivateAzirAsync(
        MatchState state,
        IReadOnlyList<string>? optionalCosts = null,
        CoreRuleEngine? engine = null)
    {
        return await (engine ?? new CoreRuleEngine()).ResolveAsync(
            state,
            new PlayerIntent("intent-azir-activate", "P1", CommandTypes.ActivateAbility),
            AzirCommand(optionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static async Task<ResolutionResult> ResolveAzirStackAsync(CoreRuleEngine engine, MatchState state)
    {
        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-azir-resolve-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);

        return await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-azir-resolve-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
    }

    private static string AzirReattachOptionalCost(string equipmentObjectId)
    {
        return P4ActivatedAbilityCatalog.AzirArmamentReattachOptionalCostId(equipmentObjectId);
    }

    private static ActivateAbilityCommand AzirCommand(
        IReadOnlyList<string>? targetObjectIds = null,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            AzirObjectId,
            P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId,
            targetObjectIds ?? [TargetObjectId],
            optionalCosts);
    }

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        ActivateAbilityCommand command,
        string? expectedErrorCode = null)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-azir-invalid", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        if (expectedErrorCode is not null)
        {
            Assert.Equal(expectedErrorCode, result.ErrorCode);
        }

        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    private static MatchState BuildInvalidScenarioState(string scenario)
    {
        var state = scenario switch
        {
            "wrong-trait-recycle" => BuildAzirState(
                P4ActivatedAbilityCatalog.AzirCardNo,
                new RunePool(0, 0),
                baseObjectIds: [RedRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [RedRuneObjectId] = RuneCard(RedRuneObjectId, RuneTrait.Red)
                }),
            "generic-temporary-resource" => BuildAzirState(P4ActivatedAbilityCatalog.AzirCardNo, new RunePool(0, 0)) with
            {
                TemporaryPaymentResources = [GenericTemporaryResource("MALZAHAR:TEMP-AZIR")]
            },
            "duplicate-recycle" => BuildAzirState(
                P4ActivatedAbilityCatalog.AzirCardNo,
                new RunePool(0, 0),
                baseObjectIds: [GreenRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [GreenRuneObjectId] = RuneCard(GreenRuneObjectId, RuneTrait.Green)
                }),
            "unnecessary-recycle" => BuildAzirState(
                P4ActivatedAbilityCatalog.AzirCardNo,
                new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Green] = 1
                }),
                baseObjectIds: [GreenRuneObjectId],
                extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [GreenRuneObjectId] = RuneCard(GreenRuneObjectId, RuneTrait.Green)
                }),
            _ => BuildAzirState(
                P4ActivatedAbilityCatalog.AzirCardNo,
                new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Green] = 1
                }))
        };

        return scenario switch
        {
            "wrong-timing" => state with
            {
                TimingState = TimingStates.NeutralClosed,
                PriorityPlayerId = "P1",
                StackItems =
                [
                    new StackItemState("STACK-PENDING", "P2", "P2-PENDING", "TEST_PENDING", "UNL-001/219")
                ]
            },
            "insufficient-green" => state with
            {
                RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
                {
                    ["P1"] = RunePool.Empty,
                    ["P2"] = RunePool.Empty
                }
            },
            _ => state
        };
    }

    private static MatchState BuildAzirStateWithArmaments()
    {
        return BuildAzirState(
            P4ActivatedAbilityCatalog.AzirCardNo,
            new RunePool(0, 0, new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Green] = 1
            }),
            baseObjectIds:
            [
                TargetArmamentObjectId,
                SecondTargetArmamentObjectId,
                OtherUnitArmamentObjectId,
                UnattachedArmamentObjectId,
                NonEquipmentAttachedObjectId,
                EnemyControlledArmamentObjectId
            ],
            extraCardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [TargetArmamentObjectId] = NonUnit(
                    TargetArmamentObjectId,
                    "SFD·022/221",
                    CardObjectTags.EquipmentCard,
                    attachedToObjectId: TargetObjectId),
                [SecondTargetArmamentObjectId] = NonUnit(
                    SecondTargetArmamentObjectId,
                    "SFD·023/221",
                    CardObjectTags.EquipmentCard,
                    attachedToObjectId: TargetObjectId),
                [OtherUnitArmamentObjectId] = NonUnit(
                    OtherUnitArmamentObjectId,
                    "SFD·024/221",
                    CardObjectTags.EquipmentCard,
                    attachedToObjectId: BaseTargetObjectId),
                [UnattachedArmamentObjectId] = NonUnit(
                    UnattachedArmamentObjectId,
                    "SFD·025/221",
                    CardObjectTags.EquipmentCard),
                [NonEquipmentAttachedObjectId] = new CardObjectState(
                    NonEquipmentAttachedObjectId,
                    cardNo: "SFD·131/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    attachedToObjectId: TargetObjectId,
                    ownerId: "P1",
                    controllerId: "P1"),
                [EnemyControlledArmamentObjectId] = NonUnit(
                    EnemyControlledArmamentObjectId,
                    "SFD·026/221",
                    CardObjectTags.EquipmentCard,
                    attachedToObjectId: TargetObjectId,
                    ownerId: "P2",
                    controllerId: "P2")
            });
    }

    private static MatchState BuildAzirState(
        string cardNo,
        RunePool runePool,
        IReadOnlyList<string>? baseObjectIds = null,
        IReadOnlyList<string>? runeDeckObjectIds = null,
        IReadOnlyDictionary<string, CardObjectState>? extraCardObjects = null)
    {
        var allBaseObjectIds = new[]
        {
            BaseTargetObjectId,
            "P1-EQUIPMENT",
            "P1-RUNE-TARGET",
            "P1-BATTLEFIELD-CARD",
            "P1-FACE-DOWN",
            "P1-DIRTY-P2-CONTROLLED"
        }.Concat(baseObjectIds ?? []).Distinct(StringComparer.Ordinal).ToArray();
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [AzirObjectId] = Unit(AzirObjectId, cardNo, "P1", power: 6),
            [TargetObjectId] = Unit(TargetObjectId, "SFD·125/221", "P1", power: 2),
            [BaseTargetObjectId] = Unit(BaseTargetObjectId, "SFD·126/221", "P1", power: 2),
            ["P1-EQUIPMENT"] = NonUnit("P1-EQUIPMENT", "SFD·139/221", CardObjectTags.EquipmentCard),
            ["P1-RUNE-TARGET"] = NonUnit("P1-RUNE-TARGET", "RUNES·001", CardObjectTags.RuneCard),
            ["P1-BATTLEFIELD-CARD"] = NonUnit("P1-BATTLEFIELD-CARD", "UNL·T01", P6TokenFactoryCatalog.BattlefieldCardTag),
            ["P1-HAND-UNIT"] = Unit("P1-HAND-UNIT", "SFD·127/221", "P1", power: 2),
            ["P1-FACE-DOWN"] = Unit("P1-FACE-DOWN", null, "P1", power: 2, isFaceDown: true, tags: [CardObjectTags.UnitCard, CardObjectTags.Standby]),
            ["P1-STALE"] = Unit("P1-STALE", "SFD·128/221", "P1", power: 2),
            ["P1-DIRTY-P2-CONTROLLED"] = Unit("P1-DIRTY-P2-CONTROLLED", "SFD·129/221", "P1", power: 2, ownerId: "P2", controllerId: "P2"),
            ["P2-ENEMY-UNIT"] = Unit("P2-ENEMY-UNIT", "SFD·130/221", "P2", power: 2)
        };
        foreach (var entry in extraCardObjects ?? new Dictionary<string, CardObjectState>(StringComparer.Ordinal))
        {
            cardObjects[entry.Key] = entry.Value;
        }

        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [AzirObjectId] = new("P1", "BATTLEFIELD", AzirBattlefieldObjectId),
            [TargetObjectId] = new("P1", "BATTLEFIELD", TargetBattlefieldObjectId),
            [BaseTargetObjectId] = new("P1", "BASE"),
            ["P1-EQUIPMENT"] = new("P1", "BASE"),
            ["P1-RUNE-TARGET"] = new("P1", "BASE"),
            ["P1-BATTLEFIELD-CARD"] = new("P1", "BASE"),
            ["P1-HAND-UNIT"] = new("P1", "HAND"),
            ["P1-FACE-DOWN"] = new("P1", "BASE"),
            ["P1-STALE"] = new("P1", "GRAVEYARD"),
            ["P1-DIRTY-P2-CONTROLLED"] = new("P1", "BASE"),
            ["P2-ENEMY-UNIT"] = new("P2", "BATTLEFIELD", "BF-ENEMY")
        };
        foreach (var objectId in baseObjectIds ?? [])
        {
            objectLocations[objectId] = new ObjectLocationState("P1", "BASE");
        }

        foreach (var objectId in runeDeckObjectIds ?? [])
        {
            objectLocations[objectId] = new ObjectLocationState("P1", "RUNE_DECK");
        }

        return new MatchState(
            "room-azir-swift-swap",
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
                ["P1"] = runePool,
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = allBaseObjectIds,
                    Battlefields = [AzirObjectId, TargetObjectId],
                    Hand = ["P1-HAND-UNIT"],
                    RuneDeck = runeDeckObjectIds ?? []
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-ENEMY-UNIT"]
                }
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
    }

    private static CardObjectState Unit(
        string objectId,
        string? cardNo,
        string playerId,
        int power,
        bool isFaceDown = false,
        IReadOnlyList<string>? tags = null,
        string? ownerId = null,
        string? controllerId = null)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            isFaceDown: isFaceDown,
            tags: tags ?? [CardObjectTags.UnitCard],
            ownerId: ownerId ?? playerId,
            controllerId: controllerId ?? playerId);
    }

    private static CardObjectState NonUnit(
        string objectId,
        string cardNo,
        string tag,
        string? attachedToObjectId = null,
        string ownerId = "P1",
        string controllerId = "P1")
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: 0,
            tags: [tag],
            attachedToObjectId: attachedToObjectId,
            ownerId: ownerId,
            controllerId: controllerId);
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
}
