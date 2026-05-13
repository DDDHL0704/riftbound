using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SfdSigilResourceSkillTests
{
    private const string PendingSpellObjectId = "P2-PENDING-SPELL";
    private const string PendingStackItemId = "STACK-P2-PENDING-SPELL";

    public static IEnumerable<object[]> RemainingSfdSigilProfiles()
    {
        return P4ActivatedAbilityCatalog.GetSfdSigilTypedResourceProfiles()
            .Where(profile => !string.Equals(profile.AbilityId, P4ActivatedAbilityCatalog.RageSigilResourceAbilityId, StringComparison.Ordinal))
            .Select(profile => new object[] { profile });
    }

    [Theory]
    [MemberData(nameof(RemainingSfdSigilProfiles))]
    public void CatalogExposesRemainingSfdSigilTypedReactionResourceSkills(P4SigilTypedResourceProfile profile)
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(profile.AbilityId, out var ability));

        Assert.Equal(profile.SourceCardNo, ability.SourceCardNo);
        Assert.Equal(profile.EffectKind, ability.EffectKind);
        Assert.True(ability.IsResourceSkill);
        Assert.True(ability.PaymentOnlyResource);
        Assert.True(ability.ReactionSpeed);
        Assert.True(ability.ExhaustsSourceAsCost);
        Assert.True(ability.RequiresBaseEquipmentSource);
        Assert.False(ability.RequiresBattlefieldSource);
        Assert.Equal(0, ability.RequiredTargetCount);
        Assert.Equal(profile.ResourceRestriction, ability.ResourceRestriction);
        var generatedPowerByTrait = P4ActivatedAbilityCatalog.GeneratedPowerByTraitForAbility(ability);
        Assert.Equal(1, generatedPowerByTrait[profile.Trait]);
    }

    [Fact]
    public void SfdSigilReactionPromptExposesBaseEquipmentTypedPaymentOnlyResourceSkills()
    {
        var state = BuildSigilPriorityState(P4ActivatedAbilityCatalog.GetSfdSigilTypedResourceProfiles());
        var prompts = ResolutionResult.BuildPrompts(state);
        var activateCandidate = Assert.Single(
            prompts["P1"].Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(activateCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]).ToArray();

        foreach (var profile in P4ActivatedAbilityCatalog.GetSfdSigilTypedResourceProfiles())
        {
            var sourceObjectId = SourceObjectId(profile);
            Assert.Contains(activateCandidate.Sources ?? [], choice => string.Equals(choice.Id, sourceObjectId, StringComparison.Ordinal));
            var requirement = Assert.Single(sourceRequirements, entry =>
                string.Equals(entry["abilityId"] as string, profile.AbilityId, StringComparison.Ordinal));
            Assert.Equal(sourceObjectId, requirement["sourceObjectId"]);
            Assert.Equal(profile.SourceCardNo, requirement["cardNo"]);
            Assert.Equal(0, requirement["minTargetCount"]);
            Assert.Equal(0, requirement["maxTargetCount"]);
            Assert.True(Assert.IsType<bool>(requirement["resourceSkill"]));
            Assert.True(Assert.IsType<bool>(requirement["reactionSpeed"]));
            Assert.True(Assert.IsType<bool>(requirement["paymentOnly"]));
            Assert.True(Assert.IsType<bool>(requirement["typedPaymentOnlyResource"]));
            Assert.True(Assert.IsType<bool>(requirement["requiresBaseEquipmentSource"]));
            Assert.True(Assert.IsType<bool>(requirement["exhaustsSource"]));
            Assert.True(Assert.IsType<bool>(requirement["resolvesImmediately"]));
            Assert.Equal(profile.ResourceRestriction, requirement["resourceRestriction"]);
            Assert.Equal("stack-priority-reaction-representative", requirement["timingPolicy"]);
            Assert.Equal("resolves-immediately-without-stack-item", requirement["reactionPolicy"]);
            Assert.Equal("no-ordinary-stack-item", requirement["stackPolicy"]);
            var generatedPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(requirement["generatedPowerByTrait"]);
            Assert.Equal(1, generatedPowerByTrait[profile.Trait]);
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]));
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]));
        }
    }

    [Theory]
    [MemberData(nameof(RemainingSfdSigilProfiles))]
    public async Task SfdSigilReactionCommandExhaustsSourceCreatesTypedTemporaryLedgerWithoutStackItem(P4SigilTypedResourceProfile profile)
    {
        var state = BuildSigilPriorityState([profile]);

        var result = await ResolveSigilAsync(state, profile);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.Tick);
        Assert.True(result.State.CardObjects[SourceObjectId(profile)].IsExhausted);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
        Assert.Equal([PendingStackItemId], result.State.StackItems.Select(item => item.StackItemId).ToArray());
        Assert.DoesNotContain(result.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));

        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal(SourceObjectId(profile), temporaryResource.SourceObjectId);
        Assert.Equal(profile.AbilityId, temporaryResource.AbilityId);
        Assert.Equal(0, temporaryResource.GeneratedPower);
        Assert.Equal(0, temporaryResource.RemainingPower);
        Assert.Equal(1, temporaryResource.GeneratedPowerByTrait[profile.Trait]);
        Assert.Equal(1, temporaryResource.RemainingPowerByTrait[profile.Trait]);

        var activatedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "ABILITY_ACTIVATED", StringComparison.Ordinal));
        Assert.Equal(profile.ResourceRestriction, activatedEvent.Payload["resourceRestriction"]);
        Assert.True(Assert.IsType<bool>(activatedEvent.Payload["typedPaymentOnlyResource"]));
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(activatedEvent.Payload["generatedPowerByTrait"])[profile.Trait]);
    }

    [Theory]
    [MemberData(nameof(RemainingSfdSigilProfiles))]
    public async Task SfdSigilTemporaryTypedResourcePaysSameColorAndGenericRuneCosts(P4SigilTypedResourceProfile profile)
    {
        foreach (var caseName in new[] { "typed", "generic" })
        {
            var resourceState = (await ResolveSigilAsync(BuildSigilPriorityState([profile]), profile)).State;
            var temporaryResource = Assert.Single(resourceState.TemporaryPaymentResources);
            var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
            var pendingPayment = string.Equals(caseName, "typed", StringComparison.Ordinal)
                ? new PendingPaymentState(
                    $"PAY-{profile.Trait.ToUpperInvariant()}-1",
                    "TEST_PENDING_PAY_COST",
                    "P1",
                    powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [profile.Trait] = 1
                    },
                    legalPaymentChoiceIds: [$"SPEND_POWER:{profile.Trait}:1"])
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
            var spendChoice = string.Equals(caseName, "typed", StringComparison.Ordinal)
                ? $"SPEND_POWER:{profile.Trait}:1"
                : "SPEND_POWER:any:1";

            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-sfd-sigil-pay-{profile.Trait}-{caseName}", "P1", CommandTypes.PayCost),
                new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, spendChoice]),
                CancellationToken.None);

            Assert.True(result.Accepted, result.ErrorMessage);
            Assert.Null(result.State.PendingPayment);
            Assert.Empty(result.State.TemporaryPaymentResources);
            Assert.Equal(RunePool.Empty, result.State.RunePools["P1"]);
            var spentEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "TEMPORARY_PAYMENT_RESOURCE_SPENT", StringComparison.Ordinal));
            Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(spentEvent.Payload["consumedPowerByTrait"])[profile.Trait]);
        }
    }

    [Theory]
    [MemberData(nameof(RemainingSfdSigilProfiles))]
    public async Task SfdSigilTemporaryTypedResourceRejectsWrongColorAndManaOnlyWithoutMutation(P4SigilTypedResourceProfile profile)
    {
        foreach (var caseName in new[] { "wrong-color", "mana-only" })
        {
            var resourceState = (await ResolveSigilAsync(BuildSigilPriorityState([profile]), profile)).State;
            var temporaryResource = Assert.Single(resourceState.TemporaryPaymentResources);
            var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
            var wrongTrait = string.Equals(profile.Trait, RuneTrait.Blue, StringComparison.Ordinal)
                ? RuneTrait.Red
                : RuneTrait.Blue;
            var pendingPayment = string.Equals(caseName, "wrong-color", StringComparison.Ordinal)
                ? new PendingPaymentState(
                    $"PAY-{wrongTrait.ToUpperInvariant()}-1",
                    "TEST_PENDING_PAY_COST",
                    "P1",
                    powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [wrongTrait] = 1
                    },
                    legalPaymentChoiceIds: [$"SPEND_POWER:{wrongTrait}:1"])
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
            var spendChoice = string.Equals(caseName, "wrong-color", StringComparison.Ordinal)
                ? $"SPEND_POWER:{wrongTrait}:1"
                : "SPEND_MANA:1";

            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent($"intent-sfd-sigil-reject-pay-{profile.Trait}-{caseName}", "P1", CommandTypes.PayCost),
                new PayCostCommand(pendingPayment.PaymentId, pendingPayment.PaymentWindow, [resourceAction, spendChoice]),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
            Assert.Empty(result.Events);
        }
    }

    [Fact]
    public void OgnSigilsRemainDeferredForSfdResourceSkills()
    {
        var profiles = P4ActivatedAbilityCatalog.GetSfdSigilTypedResourceProfiles()
            .Where(profile => !string.Equals(profile.AbilityId, P4ActivatedAbilityCatalog.RageSigilResourceAbilityId, StringComparison.Ordinal))
            .ToArray();
        var state = BuildSigilPriorityState(
            profiles.Select((profile, index) => profile with { SourceCardNo = OgnSigilCardNos[index] }));
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        Assert.DoesNotContain(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.ActivateAbility, StringComparison.Ordinal));
    }

    private static async Task<ResolutionResult> ResolveSigilAsync(
        MatchState state,
        P4SigilTypedResourceProfile profile)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-sfd-sigil-resource-skill-{profile.Trait}", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(SourceObjectId(profile), profile.AbilityId, []),
            CancellationToken.None);
    }

    private static MatchState BuildSigilPriorityState(IEnumerable<P4SigilTypedResourceProfile> profiles)
    {
        var profileArray = profiles.ToArray();
        var baseObjectIds = profileArray
            .Select(SourceObjectId)
            .ToArray();
        var cardObjects = profileArray.ToDictionary(
            SourceObjectId,
            profile => Equipment(SourceObjectId(profile), profile.SourceCardNo, "P1"),
            StringComparer.Ordinal);
        cardObjects[PendingSpellObjectId] = new CardObjectState(
            PendingSpellObjectId,
            tags: [CardObjectTags.SpellCard],
            cardNo: "UNL-001/219",
            ownerId: "P2",
            controllerId: "P2");
        var objectLocations = baseObjectIds
            .ToDictionary(
                objectId => objectId,
                _ => new ObjectLocationState("P1", "BASE"),
                StringComparer.Ordinal);
        objectLocations[PendingSpellObjectId] = new ObjectLocationState("P2", "STACK");

        return new MatchState(
            "room-sfd-sigil-resource",
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
                    Base = baseObjectIds
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: cardObjects,
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
            objectLocations: objectLocations);
    }

    private static string SourceObjectId(P4SigilTypedResourceProfile profile)
    {
        return $"P1-{profile.ResourceIdPrefix}";
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

    private static readonly string[] OgnSigilCardNos =
    [
        "OGN·081/298",
        "OGN·120/298",
        "OGN·163/298",
        "OGN·204/298",
        "OGN·245/298"
    ];
}
