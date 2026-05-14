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
        Assert.Contains(surfaces, surface => string.Equals(
            surface.SurfaceId,
            "TOKEN_DEFERRED_IMAGE_COPY_SOURCE_REQUIRED",
            StringComparison.Ordinal));
        Assert.Contains(surfaces, surface => string.Equals(
            surface.SurfaceId,
            "TOKEN_DEFERRED_BRUSH_BATTLEFIELD_REPLACEMENT",
            StringComparison.Ordinal));
        Assert.DoesNotContain(surfaces, surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.BaronNestMoveStaticSurfaceId,
            StringComparison.Ordinal));
        Assert.Contains(P6TokenFactoryCatalog.GetImplementedRuleSurfaces(), surface => string.Equals(
            surface.SurfaceId,
            P6TokenFactoryCatalog.BaronNestMoveStaticSurfaceId,
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

        var result = await new CoreRuleEngine().ResolveAsync(
            resourceState with { PendingPayment = pendingPayment },
            new PlayerIntent("intent-gold-pay-generic", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, "SPEND_POWER:any:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Null(result.State.PendingPayment);
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        var spentEvent = Assert.Single(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.Equal(1, spentEvent.Payload["consumedPower"]);
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));
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
}
