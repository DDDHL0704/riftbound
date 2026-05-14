using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PaymentEngineUnificationTests
{
    [Fact]
    public void PaymentPlanCommitDebitsManaTypedPowerExperienceAndBuildsAuditPayload()
    {
        var plan = new PaymentCostRules.PaymentPlan(
            "PAYMENT-PLAN-001",
            "PLAY_CARD",
            "P1",
            baseManaCost: 2,
            totalManaCost: 1,
            genericPowerCost: 1,
            totalPowerCost: 3,
            powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Red] = 2
            },
            experienceCost: 2,
            optionalCostIds: ["SPEND_POWER:red:2"],
            paymentResourceActionIds: ["RECYCLE_RUNE:P1-RUNE-RED"],
            legalPaymentChoiceIds: ["SPEND_MANA:1"],
            reason: "PAYMENT_PLAN_TEST",
            sourceObjectId: "P1-SOURCE");
        var runePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
        {
            ["P1"] = new(
                2,
                1,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 2,
                    [RuneTrait.Blue] = 1
                }),
            ["P2"] = RunePool.Empty
        };
        var playerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["P1"] = 3,
            ["P2"] = 0
        };

        var authorization = PaymentCostRules.AuthorizePayment(plan, runePools["P1"], playerExperience["P1"]);
        var commit = PaymentCostRules.TryCommitPayment(plan, runePools, playerExperience);

        Assert.True(authorization.Accepted, authorization.Reason);
        Assert.True(commit.Accepted, commit.ErrorMessage);
        Assert.Equal(1, commit.RunePools["P1"].Mana);
        Assert.Equal(0, commit.RunePools["P1"].Power);
        Assert.False(commit.RunePools["P1"].PowerByTrait.ContainsKey(RuneTrait.Red));
        Assert.Equal(1, commit.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        Assert.Equal(1, commit.PlayerExperience["P1"]);

        var payload = PaymentCostRules.BuildCostPaidPayload(
            plan,
            commit.RunePools,
            commit.PlayerExperience,
            new Dictionary<string, object?>());

        Assert.Equal("PAYMENT-PLAN-001", payload["paymentId"]);
        Assert.Equal("PLAY_CARD", payload["paymentWindow"]);
        Assert.Equal("P1", payload["playerId"]);
        Assert.Equal(2, payload["baseManaCost"]);
        Assert.Equal(1, payload["totalManaCost"]);
        Assert.Equal(1, payload["genericPower"]);
        Assert.Equal(3, payload["totalPowerCost"]);
        Assert.Equal(2, payload["experienceCost"]);
        Assert.Equal("PAYMENT_PLAN_TEST", payload["reason"]);
        Assert.Equal("P1-SOURCE", payload["sourceObjectId"]);
        Assert.Equal(["SPEND_POWER:red:2"], Assert.IsType<string[]>(payload["optionalCosts"]));
        Assert.Equal(["RECYCLE_RUNE:P1-RUNE-RED"], Assert.IsType<string[]>(payload["paymentResourceActions"]));
        Assert.Equal(["SPEND_MANA:1"], Assert.IsType<string[]>(payload["legalPaymentChoiceIds"]));
        Assert.Equal(1, payload["remainingMana"]);
        Assert.Equal(0, payload["remainingPower"]);
        Assert.Equal(1, payload["remainingExperience"]);
    }

    [Fact]
    public void PaymentPlanCommitRejectsWrongTraitWithoutMutation()
    {
        var plan = new PaymentCostRules.PaymentPlan(
            "PAYMENT-PLAN-002",
            "PLAY_CARD",
            "P1",
            genericPowerCost: 0,
            totalPowerCost: 2,
            powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Red] = 2
            });
        var runePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
        {
            ["P1"] = new(
                0,
                0,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Blue] = 2
                })
        };

        var commit = PaymentCostRules.TryCommitPayment(plan, runePools);

        Assert.False(commit.Accepted);
        Assert.Equal("INSUFFICIENT_COST", commit.ErrorCode);
        Assert.Equal(2, commit.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        Assert.False(commit.RunePools["P1"].PowerByTrait.ContainsKey(RuneTrait.Red));
    }

    [Fact]
    public async Task PlayCardRecycleRuneRollbackKeepsStateWhenPostResourceTypedCostFails()
    {
        const string redRuneObjectId = "P1-RUNE-RED-PARTIAL-PAYMENT";
        const string blueRuneObjectId = "P1-RUNE-BLUE-WRONG-PAYMENT";
        var bluePaymentResourceAction = $"RECYCLE_RUNE:{blueRuneObjectId}";
        var state = BulletTimeState(
            new RunePool(
                1,
                0,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
            baseObjectIds: [redRuneObjectId, blueRuneObjectId]) with
        {
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = BulletTimeCard(),
                ["P2-BULLET-TIME-UNIT-001"] = EnemyUnit(),
                [redRuneObjectId] = RuneCard(redRuneObjectId, RuneTrait.Red),
                [blueRuneObjectId] = RuneCard(blueRuneObjectId, RuneTrait.Blue),
                ["P1-RUNE-BOTTOM-001"] = RuneCard("P1-RUNE-BOTTOM-001", RuneTrait.Red)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new("P1", "HAND"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2", "BATTLEFIELD"),
                [redRuneObjectId] = new("P1", "BASE"),
                [blueRuneObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK")
            }
        };
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-rollback-wrong-trait-payment-resource", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [],
                OptionalCosts: [bluePaymentResourceAction, "SPEND_POWER:red:2"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Equal([redRuneObjectId, blueRuneObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001"], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(1, result.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.DoesNotContain(RuneTrait.Blue, result.State.RunePools["P1"].PowerByTrait.Keys);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task PlayCardCostPaidUsesPaymentPlanAuditMetadata()
    {
        var state = BulletTimeState(
            new RunePool(
                1,
                0,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 2,
                    [RuneTrait.Blue] = 1
                }));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-play-card-payment-plan-audit", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [],
                OptionalCosts: ["SPEND_POWER:red:2"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.StartsWith("PLAY_CARD:", Assert.IsType<string>(costEvent.Payload["paymentId"]), StringComparison.Ordinal);
        Assert.Equal("PLAY_CARD", costEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", costEvent.Payload["playerId"]);
        Assert.Equal("P1-SPELL-BULLET-TIME", costEvent.Payload["sourceObjectId"]);
        Assert.Equal("BULLET_TIME_DAMAGE_ENEMY_BATTLEFIELD_UNITS_BY_POWER_SPENT", costEvent.Payload["reason"]);
        Assert.Equal(1, costEvent.Payload["baseManaCost"]);
        Assert.Equal(1, costEvent.Payload["totalManaCost"]);
        Assert.Equal(0, costEvent.Payload["genericPower"]);
        Assert.Equal(2, costEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costEvent.Payload["experienceCost"]);
        Assert.Equal(["SPEND_POWER:red:2"], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Empty(Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(2, powerByTrait[RuneTrait.Red]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
        var remainingPowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["remainingPowerByTrait"]);
        Assert.Equal(1, remainingPowerByTrait[RuneTrait.Blue]);
    }

    [Fact]
    public async Task PlayCardGenericPowerShortfallQuotesAndCommitsTemporaryPaymentResource()
    {
        var temporaryResource = TemporaryResource("MALZAHAR:TEMP-PLAY", remainingPower: 1);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var state = BulletTimeState(new RunePool(1, 0)) with
        {
            TemporaryPaymentResources = [temporaryResource]
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, "SPEND_POWER:1", StringComparison.Ordinal));
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, resourceAction, StringComparison.Ordinal));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["paymentResourceChoices"]);
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, resourceAction, StringComparison.Ordinal));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);
        Assert.Equal(1, paymentResourcePowerByChoice[resourceAction]["power"]);
        Assert.Equal(true, paymentResourcePowerByChoice[resourceAction]["paymentOnly"]);
        Assert.Equal(temporaryResource.ResourceId, paymentResourcePowerByChoice[resourceAction]["temporaryPaymentResourceId"]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-play-card-temporary-payment-resource", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [],
                OptionalCosts: [resourceAction, "SPEND_POWER:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(
            ["CARD_PLAYED", "TEMPORARY_PAYMENT_RESOURCE_SPENT", "TEMPORARY_PAYMENT_RESOURCE_CLEARED", "COST_PAID", "STACK_ITEM_ADDED"],
            result.Events.Select(evt => evt.Kind));
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(1, stackItem.DamageAmount);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["SPEND_POWER:1"], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Equal([resourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([temporaryResource.ResourceId], Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
        Assert.Equal(1, costEvent.Payload["temporaryPaymentResourcePower"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
    }

    [Fact]
    public async Task PlayCardRejectsInsufficientTemporaryPaymentResourceWithoutMutation()
    {
        var temporaryResource = TemporaryResource("MALZAHAR:TEMP-PLAY-INSUFFICIENT", remainingPower: 1);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var state = BulletTimeState(new RunePool(1, 0)) with
        {
            TemporaryPaymentResources = [temporaryResource]
        };
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-play-card-insufficient-temporary-resource", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [],
                OptionalCosts: [resourceAction, "SPEND_POWER:2"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public void PlayCardTemporaryPaymentResourceRaisesGenericOptionalPowerCeiling()
    {
        var temporaryResource = TemporaryResource("MALZAHAR:TEMP-PLAY-CEILING", remainingPower: 1);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var state = BulletTimeState(new RunePool(1, 1)) with
        {
            TemporaryPaymentResources = [temporaryResource]
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, resourceAction, StringComparison.Ordinal));
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, "SPEND_POWER:2", StringComparison.Ordinal));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);
        Assert.Equal(temporaryResource.ResourceId, paymentResourcePowerByChoice[resourceAction]["temporaryPaymentResourceId"]);
    }

    [Fact]
    public async Task AssembleEquipmentCostPaidUsesPaymentPlanAuditMetadata()
    {
        var state = AssembleState(new RunePool(
            0,
            0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Red] = 1
            }));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-assemble-payment-plan-audit", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                ["ASSEMBLE_RED"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("ASSEMBLE_EQUIPMENT", costEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-EQUIPMENT-LONG-SWORD", costEvent.Payload["sourceObjectId"]);
        Assert.Equal(0, costEvent.Payload["baseManaCost"]);
        Assert.Equal(0, costEvent.Payload["totalManaCost"]);
        Assert.Equal(0, costEvent.Payload["genericPower"]);
        Assert.Equal(1, costEvent.Payload["totalPowerCost"]);
        Assert.Equal("ASSEMBLE_EQUIPMENT", costEvent.Payload["reason"]);
        Assert.Equal(["ASSEMBLE_RED"], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
    }

    [Fact]
    public async Task AssembleEquipmentAnyPowerShortfallQuotesAndCommitsTemporaryPaymentResource()
    {
        var temporaryResource = TemporaryResource("MALZAHAR:TEMP-ASSEMBLE", remainingPower: 1);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var state = SpinningAxeAssembleState(new RunePool(0, 0)) with
        {
            TemporaryPaymentResources = [temporaryResource]
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var assembleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(assembleCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);
        Assert.Equal(["ASSEMBLE_ANY_POWER"], optionalCostChoices.Select(choice => choice.Id).ToArray());
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["paymentResourceChoices"]);
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, resourceAction, StringComparison.Ordinal));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);
        Assert.Equal(1, paymentResourcePowerByChoice[resourceAction]["power"]);
        Assert.Equal(true, paymentResourcePowerByChoice[resourceAction]["paymentOnly"]);
        Assert.Equal(temporaryResource.ResourceId, paymentResourcePowerByChoice[resourceAction]["temporaryPaymentResourceId"]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-assemble-temporary-payment-resource", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-SPINNING-AXE",
                "P1-UNIT-ASSEMBLE-TARGET",
                [resourceAction, "ASSEMBLE_ANY_POWER"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(
            ["TEMPORARY_PAYMENT_RESOURCE_SPENT", "TEMPORARY_PAYMENT_RESOURCE_CLEARED", "COST_PAID", "EQUIPMENT_ATTACHED"],
            result.Events.Select(evt => evt.Kind));
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            "P1-UNIT-ASSEMBLE-TARGET",
            result.State.CardObjects["P1-EQUIPMENT-SPINNING-AXE"].AttachedToObjectId);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(["ASSEMBLE_ANY_POWER"], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Equal([resourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([temporaryResource.ResourceId], Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
        Assert.Equal(1, costEvent.Payload["temporaryPaymentResourcePower"]);
    }

    [Fact]
    public async Task AssembleEquipmentRejectsTemporaryPaymentResourceForTypedPowerWithoutMutation()
    {
        var temporaryResource = TemporaryResource("MALZAHAR:TEMP-ASSEMBLE-TYPED", remainingPower: 1);
        var resourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var state = AssembleState(new RunePool(0, 0)) with
        {
            TemporaryPaymentResources = [temporaryResource]
        };
        var initialHash = MatchStateHasher.Hash(state);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var assembleCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ASSEMBLE_EQUIPMENT", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(assembleCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        foreach (var sourceRequirement in sourceRequirements)
        {
            var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"]);
            Assert.DoesNotContain(paymentResourceChoices, choice => string.Equals(choice.Id, resourceAction, StringComparison.Ordinal));
        }

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-assemble-typed-temporary-resource-rejected", "P1", "ASSEMBLE_EQUIPMENT"),
            new AssembleEquipmentCommand(
                "P1-EQUIPMENT-LONG-SWORD",
                "P1-UNIT-ASSEMBLE-TARGET",
                [resourceAction, "ASSEMBLE_RED"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ActivateAbilityViQuotesAndCommitsRecycleRunePaymentResource()
    {
        const string runeObjectId = "P1-RUNE-RED-ACTIVATE-VI";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var state = ViActivateState(
            new RunePool(2, 0),
            baseObjectIds: ["P1-UNIT-VI", runeObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = ViCard(),
                [runeObjectId] = RuneCard(runeObjectId, RuneTrait.Red),
                ["P1-RUNE-BOTTOM-001"] = RuneCard("P1-RUNE-BOTTOM-001", RuneTrait.Blue)
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new("P1", "BASE"),
                [runeObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK")
            },
            runeDeckObjectIds: ["P1-RUNE-BOTTOM-001"]);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
        Assert.Contains(activateCandidate.OptionalCosts ?? [], choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(activateCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["paymentResourceChoices"]);
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);
        Assert.Equal(RuneTrait.Red, paymentResourcePowerByChoice[paymentResourceAction]["trait"]);
        Assert.Equal(1, paymentResourcePowerByChoice[paymentResourceAction]["power"]);
        Assert.Equal(1, sourceRequirement["availablePowerWithPaymentResources"]);
        var availablePowerWithResources = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            sourceRequirement["availablePowerByTraitWithPaymentResources"]);
        Assert.Equal(1, availablePowerWithResources[RuneTrait.Red]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-activate-vi-recycle-rune-payment-resource", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI",
                P4ActivatedAbilityCatalog.ViDoublePowerAbilityId,
                [],
                [paymentResourceAction]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["RUNE_RECYCLED", "POWER_GAINED", "ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"], result.Events.Select(evt => evt.Kind));
        Assert.DoesNotContain(runeObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001", runeObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal("RUNE_DECK", result.State.ObjectLocations[runeObjectId].Zone);
        Assert.False(result.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.ViDoublePowerAbilityEffectKind, stackItem.EffectKind);
        var recycledEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        var powerGainedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("ACTIVATE_ABILITY", recycledEvent.Payload["paymentWindow"]);
        Assert.Equal("ACTIVATE_ABILITY", powerGainedEvent.Payload["paymentWindow"]);
        Assert.Equal("ACTIVATE_ABILITY", costEvent.Payload["paymentWindow"]);
        Assert.Equal(costEvent.Payload["paymentId"], recycledEvent.Payload["paymentId"]);
        Assert.Equal(costEvent.Payload["paymentId"], powerGainedEvent.Payload["paymentId"]);
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([runeObjectId], Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
        Assert.Empty(Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Equal(2, costEvent.Payload["totalManaCost"]);
        Assert.Equal(1, costEvent.Payload["genericPower"]);
        Assert.Equal(1, costEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
    }

    [Fact]
    public async Task ActivateAbilityViQuotesMixedResourcesAndCommitsTemporaryPaymentResource()
    {
        const string runeObjectId = "P1-RUNE-RED-ACTIVATE-VI-MIXED";
        var recycleAction = $"RECYCLE_RUNE:{runeObjectId}";
        var temporaryResource = TemporaryResource("MALZAHAR:TEMP-ACTIVATE", remainingPower: 1);
        var temporaryAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var state = ViActivateState(
            new RunePool(2, 0),
            baseObjectIds: ["P1-UNIT-VI", runeObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = ViCard(),
                [runeObjectId] = RuneCard(runeObjectId, RuneTrait.Red),
                ["P1-RUNE-BOTTOM-001"] = RuneCard("P1-RUNE-BOTTOM-001", RuneTrait.Blue)
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new("P1", "BASE"),
                [runeObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK")
            },
            runeDeckObjectIds: ["P1-RUNE-BOTTOM-001"]) with
        {
            TemporaryPaymentResources = [temporaryResource]
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(activateCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["paymentResourceChoices"]);
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, recycleAction, StringComparison.Ordinal));
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, temporaryAction, StringComparison.Ordinal));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]);
        Assert.Equal(RuneTrait.Red, paymentResourcePowerByChoice[recycleAction]["trait"]);
        Assert.Equal(1, paymentResourcePowerByChoice[temporaryAction]["power"]);
        Assert.Equal(true, paymentResourcePowerByChoice[temporaryAction]["paymentOnly"]);
        Assert.Equal(temporaryResource.ResourceId, paymentResourcePowerByChoice[temporaryAction]["temporaryPaymentResourceId"]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-activate-vi-temporary-payment-resource", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI",
                P4ActivatedAbilityCatalog.ViDoublePowerAbilityId,
                [],
                [temporaryAction]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(
            ["TEMPORARY_PAYMENT_RESOURCE_SPENT", "TEMPORARY_PAYMENT_RESOURCE_CLEARED", "ABILITY_ACTIVATED", "COST_PAID", "STACK_ITEM_ADDED"],
            result.Events.Select(evt => evt.Kind));
        Assert.Empty(result.State.TemporaryPaymentResources);
        Assert.Equal(["P1-UNIT-VI", runeObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001"], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal([temporaryAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([temporaryResource.ResourceId], Assert.IsType<string[]>(costEvent.Payload["temporaryPaymentResourceIds"]));
        Assert.Equal(1, costEvent.Payload["temporaryPaymentResourcePower"]);
        Assert.Empty(Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-owner")]
    [InlineData("zero")]
    [InlineData("wrong-kind")]
    [InlineData("duplicate")]
    [InlineData("unnecessary")]
    public async Task ActivateAbilityRejectsInvalidTemporaryPaymentResourceActionsWithoutMutation(string scenario)
    {
        var temporaryResource = scenario switch
        {
            "missing" => null,
            "wrong-owner" => TemporaryResource("MALZAHAR:TEMP-ACTIVATE-INVALID", ownerPlayerId: "P2"),
            "zero" => TemporaryResource("MALZAHAR:TEMP-ACTIVATE-INVALID", remainingPower: 0),
            "wrong-kind" => TemporaryResource(
                "MALZAHAR:TEMP-ACTIVATE-INVALID",
                allowedPaymentKinds: ["SCORE_COST"]),
            _ => TemporaryResource("MALZAHAR:TEMP-ACTIVATE-INVALID")
        };
        var temporaryAction = PaymentCostRules.TemporaryPaymentResourceActionId(
            temporaryResource?.ResourceId ?? "MALZAHAR:TEMP-ACTIVATE-MISSING");
        var optionalCosts = scenario == "duplicate"
            ? [temporaryAction, temporaryAction]
            : new[] { temporaryAction };
        var state = ViActivateState(
            scenario == "unnecessary" ? new RunePool(2, 1) : new RunePool(2, 0),
            baseObjectIds: ["P1-UNIT-VI"],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = ViCard()
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new("P1", "BASE")
            }) with
        {
            TemporaryPaymentResources = temporaryResource is null ? [] : [temporaryResource]
        };
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-activate-vi-invalid-temporary-resource-{scenario}", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI",
                P4ActivatedAbilityCatalog.ViDoublePowerAbilityId,
                [],
                optionalCosts),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.True(
            string.Equals(result.ErrorCode, ErrorCodes.InvalidTarget, StringComparison.Ordinal)
            || string.Equals(result.ErrorCode, ErrorCodes.InsufficientCost, StringComparison.Ordinal));
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task ActivateAbilityRejectsUnnecessaryRecycleRunePaymentResourceWithoutMutation()
    {
        const string runeObjectId = "P1-RUNE-RED-ACTIVATE-UNNEEDED";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var state = ViActivateState(
            new RunePool(2, 1),
            baseObjectIds: ["P1-UNIT-VI", runeObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = ViCard(),
                [runeObjectId] = RuneCard(runeObjectId, RuneTrait.Red)
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new("P1", "BASE"),
                [runeObjectId] = new("P1", "BASE")
            });
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-activate-vi-unneeded-recycle-rejected", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI",
                P4ActivatedAbilityCatalog.ViDoublePowerAbilityId,
                [],
                [paymentResourceAction]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Equal(["P1-UNIT-VI", runeObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task ActivateAbilityRejectsInvalidRecycleRunePaymentResourceWithoutMutation()
    {
        const string runeObjectId = "P1-RUNE-RED-ACTIVATE-FACEDOWN";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var state = ViActivateState(
            new RunePool(2, 0),
            baseObjectIds: ["P1-UNIT-VI", runeObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = ViCard(),
                [runeObjectId] = RuneCard(runeObjectId, RuneTrait.Red, isFaceDown: true)
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-VI"] = new("P1", "BASE"),
                [runeObjectId] = new("P1", "BASE")
            });
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-activate-vi-invalid-recycle-rejected", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-VI",
                P4ActivatedAbilityCatalog.ViDoublePowerAbilityId,
                [],
                [paymentResourceAction]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.True(result.State.CardObjects[runeObjectId].IsFaceDown);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task PendingPayCostRecyclesRuneThenPaysTypedPowerThroughPaymentPlan()
    {
        const string runeObjectId = "P1-RUNE-RED-PENDING-PAY-COST";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var state = PendingPayCostResourceState(
            new RunePool(0, 0),
            runeObjectId,
            RuneCard(runeObjectId, RuneTrait.Red));

        Assert.Equal([paymentResourceAction], state.PendingPayment?.PaymentResourceActionIds);
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var paymentChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentChoices"]);
        Assert.Equal(["SPEND_POWER:red:1"], paymentChoices.Select(choice => choice.Id).ToArray());
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"]);
        Assert.Equal([paymentResourceAction], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        Assert.Equal(RuneTrait.Red, paymentResourcePowerByChoice[paymentResourceAction]["trait"]);
        Assert.Equal(1, paymentResourcePowerByChoice[paymentResourceAction]["power"]);
        Assert.Equal(1, metadata["availablePowerWithPaymentResources"]);
        var snapshotPendingPayment = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            ResolutionResult.BuildSnapshots(state)["P1"].Timing["pendingPayment"]);
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(snapshotPendingPayment["paymentResourceActions"]));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-pending-pay-cost-recycle-rune", "P1", CommandTypes.PayCost),
            new PayCostCommand(
                "PENDING-PAY-COST-RED-1",
                "TEST_PENDING_PAY_COST",
                [paymentResourceAction, "SPEND_POWER:red:1"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"], result.Events.Select(evt => evt.Kind));
        Assert.Null(result.State.PendingPayment);
        Assert.DoesNotContain(runeObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001", runeObjectId], result.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal("RUNE_DECK", result.State.ObjectLocations[runeObjectId].Zone);
        Assert.False(result.State.CardObjects[runeObjectId].IsExhausted);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        var recycledEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        var powerGainedEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("TEST_PENDING_PAY_COST", recycledEvent.Payload["paymentWindow"]);
        Assert.Equal("TEST_PENDING_PAY_COST", powerGainedEvent.Payload["paymentWindow"]);
        Assert.Equal("TEST_PENDING_PAY_COST", costEvent.Payload["paymentWindow"]);
        Assert.Equal(costEvent.Payload["paymentId"], recycledEvent.Payload["paymentId"]);
        Assert.Equal(costEvent.Payload["paymentId"], powerGainedEvent.Payload["paymentId"]);
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal(["SPEND_POWER:red:1"], Assert.IsType<string[]>(costEvent.Payload["legalPaymentChoiceIds"]));
        Assert.Equal([paymentResourceAction, "SPEND_POWER:red:1"], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Equal([runeObjectId], Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
        Assert.Equal(0, costEvent.Payload["mana"]);
        Assert.Equal(0, costEvent.Payload["power"]);
        var powerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["powerByTrait"]);
        Assert.Equal(1, powerByTrait[RuneTrait.Red]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(costEvent.Payload["remainingPowerByTrait"]));
    }

    [Fact]
    public void PendingPayCostPromptQuotesGenericTemporaryPaymentResourceOnce()
    {
        var temporaryResource = TemporaryResource("MALZAHAR:TEMP-PENDING-PAY-COST-PROMPT");
        var paymentResourceAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var state = PendingGenericPayCostTemporaryResourceState(temporaryResource);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"]);
        Assert.Equal([paymentResourceAction], paymentResourceChoices.Select(choice => choice.Id).ToArray());
        Assert.Equal(1, Assert.IsType<int>(metadata["availablePowerWithPaymentResources"]));
        var paymentResourcePowerByChoice = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            metadata["paymentResourcePowerByChoice"]);
        Assert.Equal(1, paymentResourcePowerByChoice[paymentResourceAction]["power"]);
        var availablePowerByTrait = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            metadata["availablePowerByTraitWithPaymentResources"]);
        Assert.Empty(availablePowerByTrait);
    }

    [Fact]
    public async Task PendingPayCostRejectsUnnecessaryRecycleRuneWithoutMutation()
    {
        const string runeObjectId = "P1-RUNE-RED-PENDING-UNNEEDED";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var state = PendingPayCostResourceState(
            new RunePool(
                0,
                0,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
            runeObjectId,
            RuneCard(runeObjectId, RuneTrait.Red));
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-pending-pay-cost-unneeded-rune", "P1", CommandTypes.PayCost),
            new PayCostCommand(
                "PENDING-PAY-COST-RED-1",
                "TEST_PENDING_PAY_COST",
                [paymentResourceAction, "SPEND_POWER:red:1"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Equal([runeObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001"], result.State.PlayerZones["P1"].RuneDeck);
        Assert.NotNull(result.State.PendingPayment);
    }

    [Theory]
    [InlineData(true, "UNL-R01")]
    [InlineData(false, "")]
    public async Task PendingPayCostRejectsInvalidRecycleRuneWithoutMutation(bool faceDown, string? cardNo)
    {
        const string runeObjectId = "P1-RUNE-RED-PENDING-INVALID";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var state = PendingPayCostResourceState(
            new RunePool(0, 0),
            runeObjectId,
            RuneCard(runeObjectId, RuneTrait.Red, faceDown, cardNo));
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-pending-pay-cost-invalid-rune-{faceDown}", "P1", CommandTypes.PayCost),
            new PayCostCommand(
                "PENDING-PAY-COST-RED-1",
                "TEST_PENDING_PAY_COST",
                [paymentResourceAction, "SPEND_POWER:red:1"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Equal([runeObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001"], result.State.PlayerZones["P1"].RuneDeck);
        Assert.NotNull(result.State.PendingPayment);
    }

    [Fact]
    public async Task ActivateAbilityXerathPaysSpellshieldTaxAndRecyclesRunePaymentResource()
    {
        const string runeObjectId = "P1-RUNE-RED-ACTIVATE-XERATH";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var state = XerathActivateState(
            new RunePool(1, 0),
            p1BaseObjectIds: [runeObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = XerathCard(),
                [runeObjectId] = RuneCard(runeObjectId, RuneTrait.Red),
                ["P1-RUNE-BOTTOM-001"] = RuneCard("P1-RUNE-BOTTOM-001", RuneTrait.Blue),
                ["P2-SPELLSHIELD-UNIT-001"] = EnemyUnit() with
                {
                    ObjectId = "P2-SPELLSHIELD-UNIT-001",
                    CardNo = "SFD·125/221",
                    Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield]
                }
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new("P1", "BATTLEFIELD"),
                [runeObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK"),
                ["P2-SPELLSHIELD-UNIT-001"] = new("P2", "BATTLEFIELD")
            },
            runeDeckObjectIds: ["P1-RUNE-BOTTOM-001"]);

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(activateCandidate.Metadata);
        var sourceRequirement = Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["targetChoicesByIndex"]);
        Assert.Contains(targetChoicesByIndex["0"], choice => string.Equals(choice.Id, "P2-SPELLSHIELD-UNIT-001", StringComparison.Ordinal));
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["paymentResourceChoices"]);
        Assert.Contains(paymentResourceChoices, choice => string.Equals(choice.Id, paymentResourceAction, StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-activate-xerath-recycle-rune-payment-resource", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                P4ActivatedAbilityCatalog.XerathDamageAbilityId,
                ["P2-SPELLSHIELD-UNIT-001"],
                [paymentResourceAction]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["RUNE_RECYCLED", "POWER_GAINED", "ABILITY_ACTIVATED", "COST_PAID", "UNIT_EXHAUSTED", "STACK_ITEM_ADDED"], result.Events.Select(evt => evt.Kind));
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.True(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.DoesNotContain(runeObjectId, result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-BOTTOM-001", runeObjectId], result.State.PlayerZones["P1"].RuneDeck);
        var stackItem = Assert.Single(result.State.StackItems);
        Assert.Equal(P4ActivatedAbilityCatalog.XerathDamageAbilityEffectKind, stackItem.EffectKind);
        Assert.Equal(["P2-SPELLSHIELD-UNIT-001"], stackItem.TargetObjectIds);
        var recycledEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "RUNE_RECYCLED", StringComparison.Ordinal));
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("ACTIVATE_ABILITY", recycledEvent.Payload["paymentWindow"]);
        Assert.Equal(costEvent.Payload["paymentId"], recycledEvent.Payload["paymentId"]);
        Assert.Equal([paymentResourceAction], Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal([runeObjectId], Assert.IsType<string[]>(costEvent.Payload["recycledRuneObjectIds"]));
        Assert.Equal(1, costEvent.Payload["spellshieldTaxMana"]);
        Assert.Equal(1, costEvent.Payload["totalManaCost"]);
        Assert.Equal(1, costEvent.Payload["genericPower"]);
        Assert.Equal(1, costEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
    }

    [Fact]
    public async Task ActivateAbilityXerathRejectsTemporaryPaymentResourceWhenSpellshieldTaxManaIsMissingWithoutMutation()
    {
        var temporaryResource = TemporaryResource("MALZAHAR:TEMP-ACTIVATE-XERATH-TAX", remainingPower: 1);
        var temporaryAction = PaymentCostRules.TemporaryPaymentResourceActionId(temporaryResource.ResourceId);
        var state = XerathActivateState(
            new RunePool(0, 0),
            p1BaseObjectIds: [],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = XerathCard(),
                ["P2-SPELLSHIELD-UNIT-001"] = EnemyUnit() with
                {
                    ObjectId = "P2-SPELLSHIELD-UNIT-001",
                    CardNo = "SFD·125/221",
                    Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield]
                }
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new("P1", "BATTLEFIELD"),
                ["P2-SPELLSHIELD-UNIT-001"] = new("P2", "BATTLEFIELD")
            }) with
        {
            TemporaryPaymentResources = [temporaryResource]
        };
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-activate-xerath-tax-mana-missing-temporary-rejected", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                P4ActivatedAbilityCatalog.XerathDamageAbilityId,
                ["P2-SPELLSHIELD-UNIT-001"],
                [temporaryAction]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.False(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task ActivateAbilityXerathRejectsRecycleRuneWhenSpellshieldTaxManaIsMissingWithoutMutation()
    {
        const string runeObjectId = "P1-RUNE-RED-ACTIVATE-XERATH-MANA-MISSING";
        var paymentResourceAction = $"RECYCLE_RUNE:{runeObjectId}";
        var state = XerathActivateState(
            new RunePool(0, 0),
            p1BaseObjectIds: [runeObjectId],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = XerathCard(),
                [runeObjectId] = RuneCard(runeObjectId, RuneTrait.Red),
                ["P2-SPELLSHIELD-UNIT-001"] = EnemyUnit() with
                {
                    ObjectId = "P2-SPELLSHIELD-UNIT-001",
                    CardNo = "SFD·125/221",
                    Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield]
                }
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH"] = new("P1", "BATTLEFIELD"),
                [runeObjectId] = new("P1", "BASE"),
                ["P2-SPELLSHIELD-UNIT-001"] = new("P2", "BATTLEFIELD")
            });
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-activate-xerath-tax-mana-missing-recycle-rejected", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-XERATH",
                P4ActivatedAbilityCatalog.XerathDamageAbilityId,
                ["P2-SPELLSHIELD-UNIT-001"],
                [paymentResourceAction]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.False(result.State.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Equal([runeObjectId], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public void ActivateAbilityMalzaharPromptExposesDestroyCostTargetsAndPaymentOnlyMetadata()
    {
        var state = MalzaharResourceSkillState(
            new RunePool(0, 0),
            p1BaseObjectIds:
            [
                "P1-UNIT-MALZAHAR",
                "P1-UNIT-MALZAHAR-COST",
                "P1-EQUIPMENT-MALZAHAR-COST",
                "P1-FACEDOWN-MALZAHAR-COST"
            ],
            p2BattlefieldObjectIds: ["P2-UNIT-MALZAHAR-COST"],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-MALZAHAR"] = MalzaharCard(),
                ["P1-UNIT-MALZAHAR-COST"] = FriendlyCostUnit("P1-UNIT-MALZAHAR-COST"),
                ["P1-EQUIPMENT-MALZAHAR-COST"] = FriendlyCostEquipment("P1-EQUIPMENT-MALZAHAR-COST"),
                ["P1-FACEDOWN-MALZAHAR-COST"] = FriendlyCostUnit("P1-FACEDOWN-MALZAHAR-COST", isFaceDown: true),
                ["P2-UNIT-MALZAHAR-COST"] = EnemyUnit() with
                {
                    ObjectId = "P2-UNIT-MALZAHAR-COST"
                }
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-MALZAHAR"] = new("P1", "BASE"),
                ["P1-UNIT-MALZAHAR-COST"] = new("P1", "BASE"),
                ["P1-EQUIPMENT-MALZAHAR-COST"] = new("P1", "BASE"),
                ["P1-FACEDOWN-MALZAHAR-COST"] = new("P1", "BASE"),
                ["P2-UNIT-MALZAHAR-COST"] = new("P2", "BATTLEFIELD")
            });

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var activateCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "ACTIVATE_ABILITY", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(activateCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]);
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(
                requirement["abilityId"] as string,
                P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
                StringComparison.Ordinal));

        Assert.Equal("P1-UNIT-MALZAHAR", sourceRequirement["sourceObjectId"]);
        Assert.True(Assert.IsType<bool>(sourceRequirement["resourceSkill"]));
        Assert.True(Assert.IsType<bool>(sourceRequirement["paymentOnly"]));
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharResourceGeneratedPower, sourceRequirement["generatedPower"]);
        Assert.True(Assert.IsType<bool>(sourceRequirement["usesTargetAsCost"]));
        Assert.True(Assert.IsType<bool>(sourceRequirement["resolvesImmediately"]));
        Assert.Equal(
            P4ActivatedAbilityCatalog.MalzaharPaymentOnlyResourceRestriction,
            sourceRequirement["resourceRestriction"]);

        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            sourceRequirement["targetChoicesByIndex"]);
        Assert.Contains(targetChoicesByIndex["0"], choice => string.Equals(choice.Id, "P1-UNIT-MALZAHAR-COST", StringComparison.Ordinal));
        Assert.Contains(targetChoicesByIndex["0"], choice => string.Equals(choice.Id, "P1-EQUIPMENT-MALZAHAR-COST", StringComparison.Ordinal));
        Assert.DoesNotContain(targetChoicesByIndex["0"], choice => string.Equals(choice.Id, "P1-UNIT-MALZAHAR", StringComparison.Ordinal));
        Assert.DoesNotContain(targetChoicesByIndex["0"], choice => string.Equals(choice.Id, "P1-FACEDOWN-MALZAHAR-COST", StringComparison.Ordinal));
        Assert.DoesNotContain(targetChoicesByIndex["0"], choice => string.Equals(choice.Id, "P2-UNIT-MALZAHAR-COST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActivateAbilityMalzaharDestroysFriendlyCostObjectExhaustsSourceAndGainsPaymentOnlyPower()
    {
        var state = MalzaharResourceSkillState(
            new RunePool(0, 0),
            p1BaseObjectIds: ["P1-UNIT-MALZAHAR", "P1-UNIT-MALZAHAR-COST"],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-MALZAHAR"] = MalzaharCard(),
                ["P1-UNIT-MALZAHAR-COST"] = FriendlyCostUnit("P1-UNIT-MALZAHAR-COST")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-MALZAHAR"] = new("P1", "BASE"),
                ["P1-UNIT-MALZAHAR-COST"] = new("P1", "BASE")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-activate-malzahar-resource-skill", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-MALZAHAR",
                P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
                ["P1-UNIT-MALZAHAR-COST"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["ABILITY_ACTIVATED", "UNIT_EXHAUSTED", "UNIT_DESTROYED", "POWER_GAINED"], result.Events.Select(evt => evt.Kind));
        Assert.True(result.State.CardObjects["P1-UNIT-MALZAHAR"].IsExhausted);
        Assert.False(result.State.CardObjects.ContainsKey("P1-UNIT-MALZAHAR-COST"));
        Assert.Equal(["P1-UNIT-MALZAHAR"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-UNIT-MALZAHAR-COST"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P1-UNIT-MALZAHAR-COST"].Zone);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        var temporaryResource = Assert.Single(result.State.TemporaryPaymentResources);
        Assert.Equal("P1", temporaryResource.OwnerPlayerId);
        Assert.Equal("P1-UNIT-MALZAHAR", temporaryResource.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, temporaryResource.AbilityId);
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharResourceGeneratedPower, temporaryResource.GeneratedPower);
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharResourceGeneratedPower, temporaryResource.RemainingPower);
        Assert.Equal([PaymentCostRules.RuneCostPaymentKind], temporaryResource.AllowedPaymentKinds);
        Assert.Empty(result.State.StackItems);

        var destroyEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal));
        Assert.Equal("RESOURCE_SKILL_COST", destroyEvent.Payload["reason"]);
        Assert.Equal("P1-UNIT-MALZAHAR-COST", destroyEvent.Payload["targetObjectId"]);
        Assert.Equal(true, destroyEvent.Payload["resourceSkill"]);
        Assert.Equal(true, destroyEvent.Payload["paymentOnly"]);
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharResourceGeneratedPower, destroyEvent.Payload["generatedPower"]);

        var powerEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "POWER_GAINED", StringComparison.Ordinal));
        Assert.Equal("ACTIVATE_ABILITY", powerEvent.Payload["paymentWindow"]);
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, powerEvent.Payload["abilityId"]);
        Assert.Equal("P1-UNIT-MALZAHAR-COST", powerEvent.Payload["destroyedCostObjectId"]);
        Assert.Equal(true, powerEvent.Payload["resourceSkill"]);
        Assert.Equal(true, powerEvent.Payload["paymentOnly"]);
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharResourceGeneratedPower, powerEvent.Payload["generatedPower"]);
        Assert.Equal(P4ActivatedAbilityCatalog.MalzaharPaymentOnlyResourceRestriction, powerEvent.Payload["resourceRestriction"]);
        Assert.Equal("temporary-payment-resource-ledger", powerEvent.Payload["restrictionLifecycle"]);
        Assert.Equal(temporaryResource.ResourceId, powerEvent.Payload["temporaryPaymentResourceId"]);
    }

    [Fact]
    public async Task ActivateAbilityMalzaharRejectsEnemyCostTargetWithoutMutation()
    {
        var state = MalzaharResourceSkillState(
            new RunePool(0, 0),
            p1BaseObjectIds: ["P1-UNIT-MALZAHAR"],
            p2BattlefieldObjectIds: ["P2-UNIT-MALZAHAR-COST"],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-MALZAHAR"] = MalzaharCard(),
                ["P2-UNIT-MALZAHAR-COST"] = EnemyUnit() with
                {
                    ObjectId = "P2-UNIT-MALZAHAR-COST"
                }
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-MALZAHAR"] = new("P1", "BASE"),
                ["P2-UNIT-MALZAHAR-COST"] = new("P2", "BATTLEFIELD")
            });
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-activate-malzahar-enemy-cost-rejected", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-MALZAHAR",
                P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
                ["P2-UNIT-MALZAHAR-COST"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.False(result.State.CardObjects["P1-UNIT-MALZAHAR"].IsExhausted);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task ActivateAbilityMalzaharRejectsClosedTimingWithoutOpeningStackOrMutating()
    {
        var state = MalzaharResourceSkillState(
            new RunePool(0, 0),
            p1BaseObjectIds: ["P1-UNIT-MALZAHAR", "P1-UNIT-MALZAHAR-COST"],
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-MALZAHAR"] = MalzaharCard(),
                ["P1-UNIT-MALZAHAR-COST"] = FriendlyCostUnit("P1-UNIT-MALZAHAR-COST")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-MALZAHAR"] = new("P1", "BASE"),
                ["P1-UNIT-MALZAHAR-COST"] = new("P1", "BASE")
            },
            timingState: TimingStates.NeutralClosed);
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-activate-malzahar-closed-timing-rejected", "P1", "ACTIVATE_ABILITY"),
            new ActivateAbilityCommand(
                "P1-UNIT-MALZAHAR",
                P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
                ["P1-UNIT-MALZAHAR-COST"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.False(result.State.CardObjects["P1-UNIT-MALZAHAR"].IsExhausted);
        Assert.Equal(0, result.State.RunePools["P1"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task HideCardStandardStandbyUsesPaymentPlanAuditMetadata()
    {
        var state = HideCardState(new RunePool(1, 0));

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var hideCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "HIDE_CARD", StringComparison.Ordinal));
        Assert.Contains(hideCandidate.OptionalCosts ?? [], choice => string.Equals(choice.Id, "STANDBY_A", StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hide-card-payment-plan-standard", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "STANDBY",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(["COST_PAID", "CARD_HIDDEN"], result.Events.Select(evt => evt.Kind));
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-HAND-OGN-TEEMO"], result.State.PlayerZones["P1"].Base);
        Assert.True(result.State.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.StartsWith("HIDE_CARD:", Assert.IsType<string>(costEvent.Payload["paymentId"]), StringComparison.Ordinal);
        Assert.Equal("HIDE_CARD", costEvent.Payload["paymentWindow"]);
        Assert.Equal("P1", costEvent.Payload["playerId"]);
        Assert.Equal("P1-HAND-OGN-TEEMO", costEvent.Payload["sourceObjectId"]);
        Assert.Equal("STANDBY_HIDE", costEvent.Payload["reason"]);
        Assert.Equal(1, costEvent.Payload["mana"]);
        Assert.Equal(0, costEvent.Payload["power"]);
        Assert.Equal(1, costEvent.Payload["baseManaCost"]);
        Assert.Equal(1, costEvent.Payload["totalManaCost"]);
        Assert.Equal(0, costEvent.Payload["genericPower"]);
        Assert.Equal(0, costEvent.Payload["totalPowerCost"]);
        Assert.Equal(["STANDBY_A"], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Empty(Assert.IsType<string[]>(costEvent.Payload["paymentResourceActions"]));
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
    }

    [Fact]
    public async Task HideCardFreeStandbyUsesZeroCostPaymentPlanAuditMetadata()
    {
        var state = HideCardState(
            new RunePool(0, 0),
            untilEndOfTurnEffects: ["FREE_STANDBY_HIDE:P1"]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hide-card-payment-plan-free", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "STANDBY",
                ["STANDBY_FREE"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.True(result.State.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("HIDE_CARD", costEvent.Payload["paymentWindow"]);
        Assert.Equal(0, costEvent.Payload["mana"]);
        Assert.Equal(0, costEvent.Payload["baseManaCost"]);
        Assert.Equal(0, costEvent.Payload["totalManaCost"]);
        Assert.Equal(["STANDBY_FREE"], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Equal(true, costEvent.Payload["standbyHideCostWaived"]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
    }

    [Fact]
    public async Task HideCardTeemoReplacementUsesPaymentPlanAuditMetadata()
    {
        var state = HideCardState(new RunePool(1, 0), hasTeemoLegend: true);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hide-card-payment-plan-teemo", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "STANDBY",
                ["STANDBY_TEEMO_MANA"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P1"]);
        Assert.True(result.State.CardObjects["P1-HAND-OGN-TEEMO"].IsFaceDown);
        var costEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal("HIDE_CARD", costEvent.Payload["paymentWindow"]);
        Assert.Equal(1, costEvent.Payload["mana"]);
        Assert.Equal(1, costEvent.Payload["baseManaCost"]);
        Assert.Equal(1, costEvent.Payload["totalManaCost"]);
        Assert.Equal(["STANDBY_TEEMO_MANA"], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
        Assert.Equal(true, costEvent.Payload["teemoStandbyHideReplacement"]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
    }

    [Fact]
    public async Task HideCardInsufficientStandbyManaRejectsWithoutMutation()
    {
        var state = HideCardState(new RunePool(0, 0));
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hide-card-payment-plan-insufficient", "P1", "HIDE_CARD"),
            new HideCardCommand(
                "P1-HAND-OGN-TEEMO",
                "OGN·121/298",
                "STANDBY",
                ["STANDBY_A"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
    }

    private static MatchState BulletTimeState(RunePool runePool, IReadOnlyList<string>? baseObjectIds = null)
    {
        return new MatchState(
            "payment-engine-unification-room",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = runePool,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BULLET-TIME"],
                    Base = baseObjectIds ?? [],
                    RuneDeck = baseObjectIds is null ? [] : ["P1-RUNE-BOTTOM-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = BulletTimeCard(),
                ["P2-BULLET-TIME-UNIT-001"] = EnemyUnit()
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new("P1", "HAND"),
                ["P2-BULLET-TIME-UNIT-001"] = new("P2", "BATTLEFIELD")
            });
    }

    private static MatchState AssembleState(RunePool runePool)
    {
        return new MatchState(
            "payment-engine-assemble-room",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = runePool,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-LONG-SWORD", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new("P1", "BASE"),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new("P1", "BASE")
            });
    }

    private static MatchState SpinningAxeAssembleState(RunePool runePool)
    {
        return AssembleState(runePool) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-EQUIPMENT-SPINNING-AXE", "P1-UNIT-ASSEMBLE-TARGET"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-SPINNING-AXE"] = new(
                    "P1-EQUIPMENT-SPINNING-AXE",
                    cardNo: "SFD·186/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便", "瞬息"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-SPINNING-AXE"] = new("P1", "BASE"),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new("P1", "BASE")
            }
        };
    }

    private static MatchState ViActivateState(
        RunePool runePool,
        IReadOnlyList<string> baseObjectIds,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        IReadOnlyList<string>? runeDeckObjectIds = null)
    {
        return new MatchState(
            "payment-engine-activate-vi-room",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = runePool,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = baseObjectIds,
                    RuneDeck = runeDeckObjectIds ?? []
                },
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(cardObjects, StringComparer.Ordinal),
            objectLocations: new Dictionary<string, ObjectLocationState>(objectLocations, StringComparer.Ordinal));
    }

    private static MatchState XerathActivateState(
        RunePool runePool,
        IReadOnlyList<string> p1BaseObjectIds,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        IReadOnlyList<string>? runeDeckObjectIds = null)
    {
        return new MatchState(
            "payment-engine-activate-xerath-room",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = runePool,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = p1BaseObjectIds,
                    Battlefields = ["P1-UNIT-XERATH"],
                    RuneDeck = runeDeckObjectIds ?? []
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-SPELLSHIELD-UNIT-001"]
                }
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(cardObjects, StringComparer.Ordinal),
            objectLocations: new Dictionary<string, ObjectLocationState>(objectLocations, StringComparer.Ordinal));
    }

    private static MatchState MalzaharResourceSkillState(
        RunePool runePool,
        IReadOnlyList<string> p1BaseObjectIds,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        IReadOnlyList<string>? p2BattlefieldObjectIds = null,
        string timingState = TimingStates.NeutralOpen)
    {
        return new MatchState(
            "payment-engine-activate-malzahar-room",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            timingState,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = runePool,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = p1BaseObjectIds
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2BattlefieldObjectIds ?? []
                }
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(cardObjects, StringComparer.Ordinal),
            objectLocations: new Dictionary<string, ObjectLocationState>(objectLocations, StringComparer.Ordinal));
    }

    private static MatchState PendingPayCostResourceState(
        RunePool runePool,
        string runeObjectId,
        CardObjectState runeCard)
    {
        return new MatchState(
            "payment-engine-pending-pay-cost-room",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = runePool,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [runeObjectId],
                    RuneDeck = ["P1-RUNE-BOTTOM-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [runeObjectId] = runeCard,
                ["P1-RUNE-BOTTOM-001"] = RuneCard("P1-RUNE-BOTTOM-001", RuneTrait.Blue)
            },
            pendingPayment: new PendingPaymentState(
                "PENDING-PAY-COST-RED-1",
                "TEST_PENDING_PAY_COST",
                "P1",
                powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                },
                legalPaymentChoiceIds: ["SPEND_POWER:red:1"],
                reason: "PENDING_PAY_COST_RESOURCE_TEST",
                paymentResourceActionIds: [$"RECYCLE_RUNE:{runeObjectId}"]),
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [runeObjectId] = new("P1", "BASE"),
                ["P1-RUNE-BOTTOM-001"] = new("P1", "RUNE_DECK")
            });
    }

    private static MatchState PendingGenericPayCostTemporaryResourceState(TemporaryPaymentResourceState temporaryResource)
    {
        return new MatchState(
            "payment-engine-pending-pay-cost-temporary-room",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal),
            pendingPayment: new PendingPaymentState(
                "PENDING-PAY-COST-GENERIC-1",
                "TEST_PENDING_PAY_COST",
                "P1",
                powerCost: 1,
                legalPaymentChoiceIds: ["SPEND_POWER:1"],
                reason: "PENDING_PAY_COST_TEMPORARY_RESOURCE_TEST"),
            temporaryPaymentResources: [temporaryResource]);
    }

    private static MatchState HideCardState(
        RunePool runePool,
        IReadOnlyList<string>? untilEndOfTurnEffects = null,
        bool hasTeemoLegend = false)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-HAND-OGN-TEEMO"] = new(
                "P1-HAND-OGN-TEEMO",
                cardNo: "OGN·121/298",
                power: 2,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                ownerId: "P1",
                controllerId: "P1")
        };
        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-HAND-OGN-TEEMO"] = new("P1", "HAND")
        };
        if (hasTeemoLegend)
        {
            cardObjects["P1-LEGEND-TEEMO"] = new(
                "P1-LEGEND-TEEMO",
                cardNo: "OGN·263/298",
                ownerId: "P1",
                controllerId: "P1");
            objectLocations["P1-LEGEND-TEEMO"] = new("P1", "LEGEND_ZONE");
        }

        return new MatchState(
            "payment-engine-hide-card-room",
            0,
            1,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P1",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = runePool,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-OGN-TEEMO"],
                    LegendZone = hasTeemoLegend ? ["P1-LEGEND-TEEMO"] : []
                },
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            cardObjects,
            untilEndOfTurnEffects: untilEndOfTurnEffects ?? [],
            objectLocations: objectLocations);
    }

    private static CardObjectState BulletTimeCard()
    {
        return new(
            "P1-SPELL-BULLET-TIME",
            cardNo: "OGN·268/298",
            tags: [CardObjectTags.SpellCard],
            manaCost: 1,
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState EnemyUnit()
    {
        return new(
            "P2-BULLET-TIME-UNIT-001",
            cardNo: "SFD·125/221",
            power: 5,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P2",
            controllerId: "P2");
    }

    private static CardObjectState ViCard()
    {
        return new(
            "P1-UNIT-VI",
            power: 3,
            tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
            cardNo: P4ActivatedAbilityCatalog.ViCardNo,
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState XerathCard()
    {
        return new(
            "P1-UNIT-XERATH",
            power: 5,
            tags: [CardObjectTags.UnitCard],
            cardNo: P4ActivatedAbilityCatalog.XerathCardNo,
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState MalzaharCard(bool isExhausted = false)
    {
        return new(
            "P1-UNIT-MALZAHAR",
            power: 3,
            isExhausted: isExhausted,
            tags: [CardObjectTags.UnitCard],
            cardNo: P4ActivatedAbilityCatalog.MalzaharCardNo,
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState FriendlyCostUnit(string objectId, bool isFaceDown = false)
    {
        return new(
            objectId,
            power: 2,
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.UnitCard],
            cardNo: "SFD·125/221",
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState FriendlyCostEquipment(string objectId)
    {
        return new(
            objectId,
            tags: [CardObjectTags.EquipmentCard],
            cardNo: "SFD·022/221",
            ownerId: "P1",
            controllerId: "P1");
    }

    private static CardObjectState RuneCard(
        string objectId,
        string trait,
        bool isFaceDown = false,
        string? cardNo = null)
    {
        return new(
            objectId,
            isExhausted: true,
            isFaceDown: isFaceDown,
            tags: [CardObjectTags.RuneCard, $"COLOR:{trait}"],
            cardNo: cardNo ?? (string.Equals(trait, RuneTrait.Blue, StringComparison.Ordinal) ? "UNL-R02" : "UNL-R01"),
            ownerId: "P1",
            controllerId: "P1");
    }

    private static TemporaryPaymentResourceState TemporaryResource(
        string resourceId,
        int remainingPower = 1,
        string ownerPlayerId = "P1",
        IReadOnlyList<string>? allowedPaymentKinds = null)
    {
        return new TemporaryPaymentResourceState(
            resourceId,
            ownerPlayerId,
            "P1-UNIT-MALZAHAR",
            P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
            "ACTIVATE_ABILITY",
            generatedPower: Math.Max(remainingPower, 1),
            remainingPower: remainingPower,
            allowedPaymentKinds: allowedPaymentKinds ?? [PaymentCostRules.RuneCostPaymentKind],
            createdTick: 1);
    }
}
