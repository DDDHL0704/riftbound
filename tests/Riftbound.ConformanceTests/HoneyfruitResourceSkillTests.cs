using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HoneyfruitResourceSkillTests
{
    private const string HoneyfruitObjectId = "P1-HONEYFRUIT";
    private const string PendingSpellObjectId = "P2-PENDING-SPELL";
    private const string PendingStackItemId = "STACK-P2-PENDING-SPELL";

    [Fact]
    public void CatalogExposesHoneyfruitEquipmentReactionResourceSkill()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId,
            out var ability));

        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitCardNo, ability.SourceCardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityEffectKind, ability.EffectKind);
        Assert.True(ability.IsResourceSkill);
        Assert.True(ability.ReactionSpeed);
        Assert.True(ability.PaymentOnlyResource);
        Assert.True(ability.ExhaustsSourceAsCost);
        Assert.True(ability.RequiresBaseEquipmentSource);
        Assert.False(ability.RequiresBattlefieldSource);
        Assert.Equal(0, ability.RequiredTargetCount);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, ability.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitPaymentOnlyResourceRestriction, ability.ResourceRestriction);
    }

    [Fact]
    public void HoneyfruitPromptExposesBaseAndLevelSixBranchesOnlyInLegalReactionWindow()
    {
        var basePrompt = ResolutionResult.BuildPrompts(BuildHoneyfruitPriorityState())["P1"];
        var baseRequirement = HoneyfruitRequirement(basePrompt);
        Assert.Equal(HoneyfruitObjectId, baseRequirement["sourceObjectId"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitCardNo, baseRequirement["cardNo"]);
        Assert.True(Assert.IsType<bool>(baseRequirement["resourceSkill"]));
        Assert.True(Assert.IsType<bool>(baseRequirement["reactionSpeed"]));
        Assert.True(Assert.IsType<bool>(baseRequirement["paymentOnly"]));
        Assert.True(Assert.IsType<bool>(baseRequirement["requiresBaseEquipmentSource"]));
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, baseRequirement["generatedPower"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, baseRequirement["generatedGenericPower"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitPaymentOnlyResourceRestriction, baseRequirement["resourceRestriction"]);
        Assert.Equal("stack-priority-reaction-representative", baseRequirement["timingPolicy"]);
        Assert.Equal("no-ordinary-stack-item", baseRequirement["stackPolicy"]);
        Assert.False(Assert.IsType<bool>(baseRequirement["levelSixEligible"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(baseRequirement["optionalCostChoices"]));

        var upgradedPrompt = ResolutionResult.BuildPrompts(WithExperience(BuildHoneyfruitPriorityState(), 6))["P1"];
        var upgradedRequirement = HoneyfruitRequirement(upgradedPrompt);
        Assert.True(Assert.IsType<bool>(upgradedRequirement["levelSixEligible"]));
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitUpgradedGeneratedMana, upgradedRequirement["upgradedGeneratedMana"]);
        var optionalChoice = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(
            upgradedRequirement["optionalCostChoices"]));
        Assert.Equal(LevelSixChoice(), optionalChoice.Id);

        AssertNoHoneyfruitPrompt(BuildHoneyfruitPriorityState() with
        {
            TimingState = TimingStates.NeutralOpen,
            PriorityPlayerId = null,
            StackItems = []
        });
        AssertNoHoneyfruitPrompt(BuildHoneyfruitPriorityState() with
        {
            CardObjects = ReplaceCardObject(
                BuildHoneyfruitPriorityState().CardObjects,
                HoneyfruitObjectId,
                Honeyfruit(HoneyfruitObjectId, "P1") with { IsExhausted = true })
        });
        AssertNoHoneyfruitPrompt(BuildHoneyfruitPriorityState(), "P2");
    }

    [Fact]
    public async Task HoneyfruitBaseBranchExhaustsSourceCreatesPaymentOnlyGenericPowerWithoutStackItem()
    {
        var result = await ResolveHoneyfruitAsync(BuildHoneyfruitPriorityState());

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.True(result.State.CardObjects[HoneyfruitObjectId].IsExhausted);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Equal([PendingStackItemId], result.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));

        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal(HoneyfruitObjectId, temporaryResource.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId, temporaryResource.AbilityId);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, temporaryResource.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, temporaryResource.RemainingPower);
        Assert.Empty(temporaryResource.GeneratedPowerByTrait);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], temporaryResource.AllowedPaymentKinds);

        var snapshotResource = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            result.Snapshots["P1"].Timing["temporaryPaymentResources"]));
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitPaymentOnlyResourceRestriction, snapshotResource["resourceRestriction"]);

        var activatedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.False(Assert.IsType<bool>(activatedEvent.Payload["levelSixBranch"]));
        Assert.Equal(0, activatedEvent.Payload["generatedMana"]);
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["generatedResourceCannotBeTargetedAsResponse"]));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_EXHAUSTED", StringComparison.Ordinal));
        Assert.Contains(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HoneyfruitLevelSixBranchAddsManaAndPaymentOnlyGenericPower()
    {
        var result = await ResolveHoneyfruitAsync(WithExperience(BuildHoneyfruitPriorityState(), 6), [LevelSixChoice()]);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitUpgradedGeneratedMana, result.State.RunePools["P1"].Mana);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        Assert.True(result.State.CardObjects[HoneyfruitObjectId].IsExhausted);
        Assert.Single(result.State.TemporaryPaymentResources);

        var activatedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["levelSixBranch"]));
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitUpgradedGeneratedMana, activatedEvent.Payload["generatedMana"]);
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitGeneratedPower, activatedEvent.Payload["generatedPower"]);
        var manaEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "MANA_GAINED", StringComparison.Ordinal));
        Assert.True(Assert.IsType<bool>(manaEvent.Payload["levelSixBranch"]));
        Assert.Equal(P4ActivatedAbilityCatalog.HoneyfruitUpgradedGeneratedMana, manaEvent.Payload["manaAfter"]);
    }

    [Fact]
    public async Task HoneyfruitGeneratedResourcesPayLaterRuneCostAndClearAtPaymentOrTurnCleanup()
    {
        var activated = await ResolveHoneyfruitAsync(WithExperience(BuildHoneyfruitPriorityState(), 6), [LevelSixChoice()]);
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var temporaryResource = Assert.Single(activated.State.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = new PendingPaymentState(
            "PAY-HONEYFRUIT-GENERATED",
            "TEST_PENDING_PAY_COST",
            "P1",
            manaCost: 1,
            powerCost: 1,
            legalPaymentChoiceIds: ["SPEND_MANA:1", "SPEND_POWER:any:1"]);

        var paid = await new CoreRuleEngine().ResolveAsync(
            activated.State with { PendingPayment = pendingPayment },
            new PlayerIntent("intent-honeyfruit-pay-generated", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, "SPEND_MANA:1", "SPEND_POWER:any:1"]),
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        Assert.Empty(paid.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, paid.State.RunePools["P1"]);
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
        Assert.Contains(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_CLEARED", StringComparison.Ordinal));

        var unused = await ResolveHoneyfruitAsync(BuildHoneyfruitPriorityState());
        var cleanup = await new CoreRuleEngine().ResolveAsync(
            unused.State with
            {
                TimingState = TimingStates.NeutralOpen,
                PriorityPlayerId = null,
                PassedPriorityPlayerIds = [],
                StackItems = []
            },
            new PlayerIntent("intent-honeyfruit-end-turn", "P1", CommandTypes.EndTurn),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(cleanup.Accepted, cleanup.ErrorMessage);
        Assert.Empty(cleanup.State.TemporaryPaymentResources);
        Assert.Equal(RunePool.Empty, cleanup.State.RunePools["P1"]);
    }

    [Theory]
    [InlineData("duplicate-resource")]
    [InlineData("mana-only")]
    public async Task HoneyfruitTemporaryResourceRejectsWrongOrDuplicateSpendWithoutMutation(string caseName)
    {
        var activated = await ResolveHoneyfruitAsync(BuildHoneyfruitPriorityState());
        Assert.True(activated.Accepted, activated.ErrorMessage);
        var temporaryResource = Assert.Single(activated.State.TemporaryPaymentResources);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var pendingPayment = caseName == "mana-only"
            ? new PendingPaymentState(
                "PAY-HONEYFRUIT-MANA-ONLY",
                "TEST_PENDING_PAY_COST",
                "P1",
                manaCost: 1,
                legalPaymentChoiceIds: ["SPEND_MANA:1"])
            : new PendingPaymentState(
                "PAY-HONEYFRUIT-GENERIC-1",
                "TEST_PENDING_PAY_COST",
                "P1",
                powerCost: 1,
                legalPaymentChoiceIds: ["SPEND_POWER:any:1"]);
        var state = activated.State with { PendingPayment = pendingPayment };
        var initialHash = MatchStateHasher.Hash(state);
        var choices = caseName == "mana-only"
            ? new[] { resourceAction, "SPEND_MANA:1" }
            : [resourceAction, resourceAction, "SPEND_POWER:any:1"];

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-honeyfruit-reject-pay-{caseName}", "P1", CommandTypes.PayCost),
            new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, choices),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    [Theory]
    [InlineData("wrong-timing")]
    [InlineData("target")]
    [InlineData("exhausted")]
    [InlineData("stale-source")]
    [InlineData("non-honeyfruit-source")]
    [InlineData("illegal-upgraded-branch")]
    [InlineData("wrong-upgraded-source")]
    [InlineData("unsupported-generated-amount")]
    [InlineData("temp-resource-optional-cost")]
    public async Task HoneyfruitRejectsInvalidSourceTimingOrPayloadWithoutMutation(string caseName)
    {
        var state = BuildInvalidState(caseName);
        var command = caseName switch
        {
            "target" => HoneyfruitCommand(targetObjectIds: ["P2-ANY-TARGET"]),
            "illegal-upgraded-branch" => HoneyfruitCommand(optionalCosts: [LevelSixChoice()]),
            "wrong-upgraded-source" => HoneyfruitCommand(optionalCosts: [$"{P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix}P1-OTHER"]),
            "unsupported-generated-amount" => HoneyfruitCommand(optionalCosts: ["HONEYFRUIT_GAIN:2"]),
            "temp-resource-optional-cost" => HoneyfruitCommand(optionalCosts: ["TEMP_PAYMENT_RESOURCE:HANDWRITTEN"]),
            _ => HoneyfruitCommand()
        };

        await AssertRejectedNoMutationAsync(state, command);
    }

    private static async Task<ResolutionResult> ResolveHoneyfruitAsync(
        MatchState state,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-honeyfruit-resource", "P1", CommandTypes.ActivateAbility),
            HoneyfruitCommand(optionalCosts: optionalCosts),
            CancellationToken.None);
    }

    private static async Task AssertRejectedNoMutationAsync(
        MatchState state,
        ActivateAbilityCommand command)
    {
        var initialHash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-honeyfruit-invalid", "P1", CommandTypes.ActivateAbility),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }

    private static IReadOnlyDictionary<string, object?> HoneyfruitRequirement(ActionPromptDto prompt)
    {
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        Assert.Contains(activateCandidate.Sources ?? [], choice => string.Equals(choice.Id, HoneyfruitObjectId, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        return Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]),
            entry => string.Equals(entry["abilityId"] as string, P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId, StringComparison.Ordinal));
    }

    private static void AssertNoHoneyfruitPrompt(MatchState state, string playerId = "P1")
    {
        var prompt = ResolutionResult.BuildPrompts(state)[playerId];
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
                    P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId,
                    StringComparison.Ordinal));
        }
    }

    private static ActivateAbilityCommand HoneyfruitCommand(
        IReadOnlyList<string>? targetObjectIds = null,
        IReadOnlyList<string>? optionalCosts = null)
    {
        return new ActivateAbilityCommand(
            HoneyfruitObjectId,
            P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId,
            targetObjectIds ?? [],
            optionalCosts);
    }

    private static string LevelSixChoice()
    {
        return $"{P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix}{HoneyfruitObjectId}";
    }

    private static MatchState BuildInvalidState(string caseName)
    {
        var state = BuildHoneyfruitPriorityState();
        return caseName switch
        {
            "wrong-timing" => state with
            {
                TimingState = TimingStates.NeutralOpen,
                PriorityPlayerId = null,
                StackItems = []
            },
            "exhausted" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    HoneyfruitObjectId,
                    state.CardObjects[HoneyfruitObjectId] with { IsExhausted = true })
            },
            "stale-source" => state with
            {
                PlayerZones = ReplacePlayerZones(
                    state.PlayerZones,
                    "P1",
                    state.PlayerZones["P1"] with
                    {
                        Base = [],
                        Battlefields = [HoneyfruitObjectId]
                    }),
                ObjectLocations = ReplaceObjectLocation(
                    state.ObjectLocations,
                    HoneyfruitObjectId,
                    new ObjectLocationState("P1", "BATTLEFIELD", "P1-MAIN"))
            },
            "non-honeyfruit-source" => state with
            {
                CardObjects = ReplaceCardObject(
                    state.CardObjects,
                    HoneyfruitObjectId,
                    state.CardObjects[HoneyfruitObjectId] with { CardNo = "UNL-050/219" })
            },
            _ => state
        };
    }

    private static MatchState WithExperience(MatchState state, int experience)
    {
        return state with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = experience,
                ["P2"] = 0
            }
        };
    }

    private static MatchState BuildHoneyfruitPriorityState()
    {
        return new MatchState(
            "room-honeyfruit-resource",
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
                    Base = [HoneyfruitObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [HoneyfruitObjectId] = Honeyfruit(HoneyfruitObjectId, "P1"),
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
                [HoneyfruitObjectId] = new("P1", "BASE"),
                [PendingSpellObjectId] = new("P2", "STACK")
            });
    }

    private static CardObjectState Honeyfruit(string objectId, string playerId)
    {
        return new CardObjectState(
            objectId,
            tags: [CardObjectTags.EquipmentCard, "蜜糖果实", "反应"],
            cardNo: P4ActivatedAbilityCatalog.HoneyfruitCardNo,
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
