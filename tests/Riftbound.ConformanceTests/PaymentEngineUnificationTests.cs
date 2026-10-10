using System.Text.Json;
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
    public void PlayCardTypedOptionalPowerPromptDoesNotQuoteWrongTraitResources()
    {
        const string redRuneObjectId = "P1-RUNE-RED-WRONG-TINY-GUARDIAN";
        var redRecycleAction = $"RECYCLE_RUNE:{redRuneObjectId}";
        var redTemporaryAction = "TEMP_PAYMENT_RESOURCE:retired-wrong-trait";
        var state = TinyGuardianState(new RunePool(2, 0), baseObjectIds: [redRuneObjectId]) with
        {
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-TINY-GUARDIAN"] = TinyGuardianCard(),
                [redRuneObjectId] = RuneCard(redRuneObjectId, RuneTrait.Red)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-TINY-GUARDIAN"] = new("P1", "HAND"),
                [redRuneObjectId] = new("P1", "BASE")
            }
        };

        var sourceRequirement = AssertSinglePlayCardSourceRequirement(state);
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.DoesNotContain("SPEND_POWER:green:1", optionalCostChoices);
        Assert.DoesNotContain(redRecycleAction, optionalCostChoices);
        Assert.DoesNotContain(redTemporaryAction, optionalCostChoices);
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["paymentResourceChoices"])
            .Select(choice => choice.Id)
            .ToArray();
        Assert.Empty(paymentResourceChoices);
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["availablePower"]));
        Assert.Equal(0, Assert.IsType<int>(sourceRequirement["availablePowerWithPaymentResources"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            sourceRequirement["availablePowerByTraitWithPaymentResources"]));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>(
            sourceRequirement["paymentResourcePowerByChoice"]));
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
    public async Task PendingPayCostRejectsStaleRunePaymentReplayWithoutMutation()
    {
        var paymentResourceAction = "RECYCLE_RUNE:P1-PAY-RUNE";
        var state = PendingGenericPayCostRuneState();
        var command = new PayCostCommand(
            "PENDING-PAY-COST-GENERIC-1",
            "TEST_PENDING_PAY_COST",
            [paymentResourceAction, "SPEND_POWER:1"]);

        var paid = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-pending-pay-cost-stale-temporary-first", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(paid.State);
        Assert.Empty(paid.State.StackItems);
        var afterSpendHash = MatchStateHasher.Hash(paid.State);

        var replay = await new CoreRuleEngine().ResolveAsync(
            paid.State,
            new PlayerIntent("intent-pending-pay-cost-stale-temporary-replay", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Empty(replay.Events);
        Assert.Equal(afterSpendHash, MatchStateHasher.Hash(replay.State));
        Assert.Null(replay.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(replay.State);
        Assert.Empty(replay.State.StackItems);
    }

    [Theory]
    [InlineData("mana", "PENDING-PAY-COST-MANA-1", "SPEND_MANA:1")]
    [InlineData("generic-power", "PENDING-PAY-COST-GENERIC-POOL-1", "SPEND_POWER:1")]
    [InlineData("typed-power", "PENDING-PAY-COST-RED-POOL-1", "SPEND_POWER:red:1")]
    public async Task PendingPayCostRejectsStaleOrdinaryReplayAfterWindowClosesWithoutMutation(
        string costShape,
        string paymentId,
        string paymentChoiceId)
    {
        var state = PendingOrdinaryPayCostState(costShape, paymentId, paymentChoiceId);
        var command = new PayCostCommand(
            paymentId,
            "TEST_PENDING_PAY_COST",
            [paymentChoiceId]);

        var paid = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-pending-pay-cost-{costShape}-ordinary-first", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Equal(["COST_PAID", "PAYMENT_WINDOW_CLOSED"], paid.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Null(paid.State.PendingPayment);
        Assert.Equal(RunePool.Empty, paid.State.RunePools["P1"]);
        AssertNoPayCostPrompt(paid.State);
        var costEvent = Assert.Single(paid.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(paymentId, costEvent.Payload["paymentId"]);
        Assert.Equal("TEST_PENDING_PAY_COST", costEvent.Payload["paymentWindow"]);
        Assert.Equal([paymentChoiceId], Assert.IsType<string[]>(costEvent.Payload["paymentChoiceIds"]));
        Assert.Equal([paymentChoiceId], Assert.IsType<string[]>(costEvent.Payload["legalPaymentChoiceIds"]));
        var afterSpendHash = MatchStateHasher.Hash(paid.State);

        var replay = await new CoreRuleEngine().ResolveAsync(
            paid.State,
            new PlayerIntent($"intent-pending-pay-cost-{costShape}-ordinary-replay", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Empty(replay.Events);
        Assert.Equal(afterSpendHash, MatchStateHasher.Hash(replay.State));
        Assert.Null(replay.State.PendingPayment);
        AssertNoPayCostPrompt(replay.State);
        Assert.Equal(RunePool.Empty, replay.State.RunePools["P1"]);
        Assert.Empty(replay.State.StackItems);
    }

    [Theory]
    [InlineData("mana", "PENDING-PAY-COST-MANA-1", "SPEND_MANA:1")]
    [InlineData("generic-power", "PENDING-PAY-COST-GENERIC-POOL-1", "SPEND_POWER:1")]
    [InlineData("typed-power", "PENDING-PAY-COST-RED-POOL-1", "SPEND_POWER:red:1")]
    public async Task PendingPayCostPromptScopedOrdinaryReplayAfterWindowClosesRejectsWithoutMutation(
        string costShape,
        string paymentId,
        string paymentChoiceId)
    {
        var firstClientIntentId = $"intent-pending-pay-cost-{costShape}-prompt-scoped-stale-raw-first";
        var staleClientIntentId = $"intent-pending-pay-cost-{costShape}-prompt-scoped-stale-raw-replay";
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(
            PendingOrdinaryPayCostState(costShape, paymentId, paymentChoiceId),
            new CoreRuleEngine(),
            journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.Contains(CommandTypes.PayCost, prompt.Actions);
        var command = new PayCostCommand(
            paymentId,
            "TEST_PENDING_PAY_COST",
            [paymentChoiceId]);
        var staleRawCommand = PromptScopedPayCostRawCommand(command, prompt);
        var changedStaleRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = command.PaymentId,
            paymentWindow = command.PaymentWindow,
            paymentChoiceIds = command.PaymentChoiceIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });

        var paid = await session.SubmitAsync(
            "P1",
            firstClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.ErrorCode);
        Assert.Equal(["COST_PAID", "PAYMENT_WINDOW_CLOSED"], paid.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Null(paid.State.PendingPayment);
        AssertNoPayCostPrompt(paid.State);
        Assert.Equal(RunePool.Empty, paid.State.RunePools["P1"]);
        Assert.Empty(paid.State.StackItems);
        var postPaymentStateHash = MatchStateHasher.Hash(paid.State);
        var paidPromptsHash = MatchStateHasher.HashValue(paid.Prompts);
        var paidSnapshotsHash = MatchStateHasher.HashValue(paid.Snapshots);
        var postPaymentAuthoritativePromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(paid.State));
        var postPaymentAuthoritativeSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(paid.State));
        var postPaymentP1SessionSnapshotHash = MatchStateHasher.HashValue(session.SnapshotFor("P1"));
        var postPaymentP2SessionSnapshotHash = MatchStateHasher.HashValue(session.SnapshotFor("P2"));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Equal(postPaymentP1SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(postPaymentP2SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Null(replay.State.PendingPayment);
        AssertNoPayCostPrompt(replay.State);
        Assert.Equal(RunePool.Empty, replay.State.RunePools["P1"]);
        Assert.Empty(replay.State.StackItems);

        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, journal.Entries.Count(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal)));
        var acceptedEntry = Assert.Single(
            journal.Entries,
            entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal) && entry.Accepted);
        var rejectedEntry = Assert.Single(
            journal.Entries,
            entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal) && !entry.Accepted);

        Assert.Equal("payment-engine-pending-pay-cost-ordinary-room", acceptedEntry.RoomId);
        Assert.Equal("P1", acceptedEntry.PlayerId);
        Assert.Equal(firstClientIntentId, acceptedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PayCost, acceptedEntry.CommandType);
        Assert.True(acceptedEntry.Accepted);
        Assert.Null(acceptedEntry.ErrorMessage);
        Assert.Equal(["COST_PAID", "PAYMENT_WINDOW_CLOSED"], acceptedEntry.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(acceptedEntry.AuthoritativeState));
        Assert.Equal(paidPromptsHash, MatchStateHasher.HashValue(acceptedEntry.Prompts));
        Assert.Equal(paidSnapshotsHash, MatchStateHasher.HashValue(acceptedEntry.Snapshots));

        Assert.Equal("payment-engine-pending-pay-cost-ordinary-room", rejectedEntry.RoomId);
        Assert.Equal("P1", rejectedEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PayCost, rejectedEntry.CommandType);
        Assert.False(rejectedEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedEntry.ErrorMessage);
        Assert.Empty(rejectedEntry.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(rejectedEntry.AuthoritativeState));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(rejectedEntry.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(rejectedEntry.Snapshots));
        Assert.True(rejectedEntry.RawCommand.HasValue);
        Assert.Equal(MatchStateHasher.HashValue(staleRawCommand), MatchStateHasher.HashValue(rejectedEntry.RawCommand.Value));
        Assert.Equal(CommandTypes.PayCost, rejectedEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(paymentId, rejectedEntry.RawCommand.Value.GetProperty("paymentId").GetString());
        Assert.Equal("TEST_PENDING_PAY_COST", rejectedEntry.RawCommand.Value.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            [paymentChoiceId],
            rejectedEntry.RawCommand.Value.GetProperty("paymentChoiceIds").EnumerateArray().Select(choice => choice.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, rejectedEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(rejectedEntry.RawCommand.Value.TryGetProperty("clientNote", out _));
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
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(duplicateReplay.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        Assert.Equal(postPaymentP1SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(postPaymentP2SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Null(duplicateReplay.State.PendingPayment);
        AssertNoPayCostPrompt(duplicateReplay.State);
        Assert.Equal(RunePool.Empty, duplicateReplay.State.RunePools["P1"]);
        Assert.Empty(duplicateReplay.State.StackItems);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, journal.Entries.Count(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal)));
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
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Equal(postPaymentP1SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P1")));
        Assert.Equal(postPaymentP2SessionSnapshotHash, MatchStateHasher.HashValue(session.SnapshotFor("P2")));
        Assert.Null(conflict.State.PendingPayment);
        AssertNoPayCostPrompt(conflict.State);
        Assert.Equal(RunePool.Empty, conflict.State.RunePools["P1"]);
        Assert.Empty(conflict.State.StackItems);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, journal.Entries.Count(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal)));
        Assert.Equal(journalHashAfterReplay, MatchStateHasher.HashValue(journal.Entries));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PendingPayCostPromptScopedOrdinaryReplayAfterWindowClosesRecordsRejectedJournalWithoutMutation()
    {
        const string paymentId = "PENDING-PAY-COST-MANA-1";
        const string paymentChoiceId = "SPEND_MANA:1";
        const string firstClientIntentId = "intent-pending-pay-cost-prompt-scoped-stale-raw-first";
        const string staleClientIntentId = "intent-pending-pay-cost-prompt-scoped-stale-raw-replay";
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(
            PendingOrdinaryPayCostState("mana", paymentId, paymentChoiceId),
            new CoreRuleEngine(),
            journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.Contains(CommandTypes.PayCost, prompt.Actions);
        var command = new PayCostCommand(
            paymentId,
            "TEST_PENDING_PAY_COST",
            [paymentChoiceId]);
        var staleRawCommand = PromptScopedPayCostRawCommand(command, prompt);
        var changedStaleRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = command.PaymentId,
            paymentWindow = command.PaymentWindow,
            paymentChoiceIds = command.PaymentChoiceIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });

        var paid = await session.SubmitAsync(
            "P1",
            firstClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.ErrorCode);
        Assert.Equal(["COST_PAID", "PAYMENT_WINDOW_CLOSED"], paid.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Null(paid.State.PendingPayment);
        AssertNoPayCostPrompt(paid.State);
        Assert.Equal(RunePool.Empty, paid.State.RunePools["P1"]);
        Assert.Empty(paid.State.StackItems);
        var postPaymentStateHash = MatchStateHasher.Hash(paid.State);
        var paidPromptsHash = MatchStateHasher.HashValue(paid.Prompts);
        var paidSnapshotsHash = MatchStateHasher.HashValue(paid.Snapshots);
        var postPaymentAuthoritativePromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(paid.State));
        var postPaymentAuthoritativeSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(paid.State));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Null(replay.State.PendingPayment);
        AssertNoPayCostPrompt(replay.State);
        Assert.Equal(RunePool.Empty, replay.State.RunePools["P1"]);
        Assert.Empty(replay.State.StackItems);

        var payCostEntries = journal.Entries
            .Where(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, payCostEntries.Length);
        var acceptedEntry = Assert.Single(payCostEntries, entry => entry.Accepted);
        var rejectedEntry = Assert.Single(payCostEntries, entry => !entry.Accepted);

        Assert.Equal("payment-engine-pending-pay-cost-ordinary-room", acceptedEntry.RoomId);
        Assert.Equal("P1", acceptedEntry.PlayerId);
        Assert.Equal(firstClientIntentId, acceptedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PayCost, acceptedEntry.CommandType);
        Assert.Null(acceptedEntry.ErrorMessage);
        Assert.Equal(["COST_PAID", "PAYMENT_WINDOW_CLOSED"], acceptedEntry.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(acceptedEntry.AuthoritativeState));
        Assert.Equal(paidPromptsHash, MatchStateHasher.HashValue(acceptedEntry.Prompts));
        Assert.Equal(paidSnapshotsHash, MatchStateHasher.HashValue(acceptedEntry.Snapshots));

        Assert.Equal("payment-engine-pending-pay-cost-ordinary-room", rejectedEntry.RoomId);
        Assert.Equal("P1", rejectedEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PayCost, rejectedEntry.CommandType);
        Assert.False(rejectedEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedEntry.ErrorMessage);
        Assert.Empty(rejectedEntry.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(rejectedEntry.AuthoritativeState));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(rejectedEntry.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(rejectedEntry.Snapshots));
        Assert.True(rejectedEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PayCost, rejectedEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(paymentId, rejectedEntry.RawCommand.Value.GetProperty("paymentId").GetString());
        Assert.Equal("TEST_PENDING_PAY_COST", rejectedEntry.RawCommand.Value.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            [paymentChoiceId],
            rejectedEntry.RawCommand.Value.GetProperty("paymentChoiceIds").EnumerateArray().Select(choice => choice.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, rejectedEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(rejectedEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

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
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(duplicateReplay.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        Assert.Null(duplicateReplay.State.PendingPayment);
        AssertNoPayCostPrompt(duplicateReplay.State);
        Assert.Equal(RunePool.Empty, duplicateReplay.State.RunePools["P1"]);
        Assert.Empty(duplicateReplay.State.StackItems);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, journal.Entries.Count(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal)));

        var conflict = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            changedStaleRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Null(conflict.State.PendingPayment);
        AssertNoPayCostPrompt(conflict.State);
        Assert.Equal(RunePool.Empty, conflict.State.RunePools["P1"]);
        Assert.Empty(conflict.State.StackItems);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, journal.Entries.Count(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal)));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PendingPayCostPromptScopedRuneResourceReplayAfterWindowClosesRejectsWithoutMutation()
    {
        var paymentResourceAction = "RECYCLE_RUNE:P1-PAY-RUNE";
        var session = new MatchSession(
            PendingGenericPayCostRuneState(),
            new CoreRuleEngine(),
            NoopMatchJournal.Instance);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.Contains(CommandTypes.PayCost, prompt.Actions);
        var command = new PayCostCommand(
            "PENDING-PAY-COST-GENERIC-1",
            "TEST_PENDING_PAY_COST",
            [paymentResourceAction, "SPEND_POWER:1"]);
        var staleRawCommand = PromptScopedPayCostRawCommand(command, prompt);

        var paid = await session.SubmitAsync(
            "P1",
            "intent-pending-pay-cost-temporary-prompt-scoped-first",
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Equal(
            ["RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
            paid.Events.Select(evt => evt.Kind));
        Assert.Null(paid.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(paid.State);
        AssertNoPayCostPrompt(paid.State);
        var postPaymentHash = MatchStateHasher.Hash(paid.State);

        var replay = await session.SubmitAsync(
            "P1",
            "intent-pending-pay-cost-temporary-prompt-scoped-replay",
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Empty(replay.Events);
        Assert.Equal(postPaymentHash, MatchStateHasher.Hash(replay.State));
        Assert.Null(replay.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(replay.State);
        AssertNoPayCostPrompt(replay.State);
        Assert.Empty(replay.State.StackItems);
    }

    [Fact]
    public async Task PendingPayCostPromptScopedRuneResourceReplayAfterWindowClosesRecordsRejectedJournalWithoutMutation()
    {
        const string paymentId = "PENDING-PAY-COST-GENERIC-1";
        const string paymentChoiceId = "SPEND_POWER:1";
        const string firstClientIntentId = "intent-pending-pay-cost-temporary-prompt-scoped-stale-raw-first";
        const string staleClientIntentId = "intent-pending-pay-cost-temporary-prompt-scoped-stale-raw-replay";
        var paymentResourceAction = "RECYCLE_RUNE:P1-PAY-RUNE";
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(
            PendingGenericPayCostRuneState(),
            new CoreRuleEngine(),
            journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.Contains(CommandTypes.PayCost, prompt.Actions);
        var command = new PayCostCommand(
            paymentId,
            "TEST_PENDING_PAY_COST",
            [paymentResourceAction, paymentChoiceId]);
        var staleRawCommand = PromptScopedPayCostRawCommand(command, prompt);
        var changedStaleRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = command.PaymentId,
            paymentWindow = command.PaymentWindow,
            paymentChoiceIds = command.PaymentChoiceIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });

        var paid = await session.SubmitAsync(
            "P1",
            firstClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.ErrorCode);
        Assert.Equal(
            ["RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
            paid.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Null(paid.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(paid.State);
        AssertNoPayCostPrompt(paid.State);
        Assert.Equal(RunePool.Empty, paid.State.RunePools["P1"]);
        Assert.Empty(paid.State.StackItems);
        var postPaymentStateHash = MatchStateHasher.Hash(paid.State);
        var paidPromptsHash = MatchStateHasher.HashValue(paid.Prompts);
        var paidSnapshotsHash = MatchStateHasher.HashValue(paid.Snapshots);
        var postPaymentAuthoritativePromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(paid.State));
        var postPaymentAuthoritativeSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(paid.State));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Null(replay.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(replay.State);
        AssertNoPayCostPrompt(replay.State);
        Assert.Equal(RunePool.Empty, replay.State.RunePools["P1"]);
        Assert.Empty(replay.State.StackItems);

        var payCostEntries = journal.Entries
            .Where(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, payCostEntries.Length);
        var acceptedEntry = Assert.Single(payCostEntries, entry => entry.Accepted);
        var rejectedEntry = Assert.Single(payCostEntries, entry => !entry.Accepted);

        Assert.Equal("payment-engine-pending-pay-cost-ordinary-room", acceptedEntry.RoomId);
        Assert.Equal("P1", acceptedEntry.PlayerId);
        Assert.Equal(firstClientIntentId, acceptedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PayCost, acceptedEntry.CommandType);
        Assert.True(acceptedEntry.Accepted);
        Assert.Null(acceptedEntry.ErrorMessage);
        Assert.Equal(
            ["RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
            acceptedEntry.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(acceptedEntry.AuthoritativeState));
        Assert.Equal(paidPromptsHash, MatchStateHasher.HashValue(acceptedEntry.Prompts));
        Assert.Equal(paidSnapshotsHash, MatchStateHasher.HashValue(acceptedEntry.Snapshots));

        Assert.Equal("payment-engine-pending-pay-cost-ordinary-room", rejectedEntry.RoomId);
        Assert.Equal("P1", rejectedEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PayCost, rejectedEntry.CommandType);
        Assert.False(rejectedEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedEntry.ErrorMessage);
        Assert.Empty(rejectedEntry.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(rejectedEntry.AuthoritativeState));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(rejectedEntry.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(rejectedEntry.Snapshots));
        Assert.True(rejectedEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PayCost, rejectedEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(paymentId, rejectedEntry.RawCommand.Value.GetProperty("paymentId").GetString());
        Assert.Equal("TEST_PENDING_PAY_COST", rejectedEntry.RawCommand.Value.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            [paymentResourceAction, paymentChoiceId],
            rejectedEntry.RawCommand.Value.GetProperty("paymentChoiceIds").EnumerateArray().Select(choice => choice.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, rejectedEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(rejectedEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

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
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(duplicateReplay.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        Assert.Null(duplicateReplay.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(duplicateReplay.State);
        AssertNoPayCostPrompt(duplicateReplay.State);
        Assert.Equal(RunePool.Empty, duplicateReplay.State.RunePools["P1"]);
        Assert.Empty(duplicateReplay.State.StackItems);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, journal.Entries.Count(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal)));

        var conflict = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            changedStaleRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Null(conflict.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(conflict.State);
        AssertNoPayCostPrompt(conflict.State);
        Assert.Equal(RunePool.Empty, conflict.State.RunePools["P1"]);
        Assert.Empty(conflict.State.StackItems);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, journal.Entries.Count(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal)));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PendingPayCostPromptScopedTypedRuneResourceReplayAfterWindowClosesRecordsRejectedJournalWithoutMutation()
    {
        const string paymentId = "PENDING-PAY-COST-GREEN-1";
        const string paymentChoiceId = "SPEND_POWER:green:1";
        const string firstClientIntentId = "intent-pending-pay-cost-typed-temporary-prompt-scoped-stale-raw-first";
        const string staleClientIntentId = "intent-pending-pay-cost-typed-temporary-prompt-scoped-stale-raw-replay";
        var paymentResourceAction = "RECYCLE_RUNE:P1-PAY-RUNE";
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(
            PendingTypedPayCostRuneState(RuneTrait.Green),
            new CoreRuleEngine(),
            journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.Contains(CommandTypes.PayCost, prompt.Actions);
        var command = new PayCostCommand(
            paymentId,
            "TEST_PENDING_PAY_COST",
            [paymentResourceAction, paymentChoiceId]);
        var staleRawCommand = PromptScopedPayCostRawCommand(command, prompt);
        var changedStaleRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = command.PaymentId,
            paymentWindow = command.PaymentWindow,
            paymentChoiceIds = command.PaymentChoiceIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });

        var paid = await session.SubmitAsync(
            "P1",
            firstClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Null(paid.ErrorCode);
        Assert.Equal(
            ["RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
            paid.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Null(paid.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(paid.State);
        AssertNoPayCostPrompt(paid.State);
        Assert.Equal(RunePool.Empty, paid.State.RunePools["P1"]);
        Assert.Empty(paid.State.StackItems);
        var postPaymentStateHash = MatchStateHasher.Hash(paid.State);
        var paidPromptsHash = MatchStateHasher.HashValue(paid.Prompts);
        var paidSnapshotsHash = MatchStateHasher.HashValue(paid.Snapshots);
        var postPaymentAuthoritativePromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(paid.State));
        var postPaymentAuthoritativeSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(paid.State));

        var replay = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            staleRawCommand,
            CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Equal(ErrorCodes.PromptExpired, replay.ErrorCode);
        Assert.Equal("行动窗口已过期，请按最新提示重新提交。", replay.ErrorMessage);
        Assert.Empty(replay.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Null(replay.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(replay.State);
        AssertNoPayCostPrompt(replay.State);
        Assert.Equal(RunePool.Empty, replay.State.RunePools["P1"]);
        Assert.Empty(replay.State.StackItems);

        var payCostEntries = journal.Entries
            .Where(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, payCostEntries.Length);
        var acceptedEntry = Assert.Single(payCostEntries, entry => entry.Accepted);
        var rejectedEntry = Assert.Single(payCostEntries, entry => !entry.Accepted);

        Assert.Equal("payment-engine-pending-pay-cost-ordinary-room", acceptedEntry.RoomId);
        Assert.Equal("P1", acceptedEntry.PlayerId);
        Assert.Equal(firstClientIntentId, acceptedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PayCost, acceptedEntry.CommandType);
        Assert.True(acceptedEntry.Accepted);
        Assert.Null(acceptedEntry.ErrorMessage);
        Assert.Equal(
            ["RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
            acceptedEntry.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(acceptedEntry.AuthoritativeState));
        Assert.Equal(paidPromptsHash, MatchStateHasher.HashValue(acceptedEntry.Prompts));
        Assert.Equal(paidSnapshotsHash, MatchStateHasher.HashValue(acceptedEntry.Snapshots));

        Assert.Equal("payment-engine-pending-pay-cost-ordinary-room", rejectedEntry.RoomId);
        Assert.Equal("P1", rejectedEntry.PlayerId);
        Assert.Equal(staleClientIntentId, rejectedEntry.ClientIntentId);
        Assert.Equal(CommandTypes.PayCost, rejectedEntry.CommandType);
        Assert.False(rejectedEntry.Accepted);
        Assert.Equal(replay.ErrorMessage, rejectedEntry.ErrorMessage);
        Assert.Empty(rejectedEntry.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(rejectedEntry.AuthoritativeState));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(rejectedEntry.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(rejectedEntry.Snapshots));
        Assert.True(rejectedEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PayCost, rejectedEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(paymentId, rejectedEntry.RawCommand.Value.GetProperty("paymentId").GetString());
        Assert.Equal("TEST_PENDING_PAY_COST", rejectedEntry.RawCommand.Value.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            [paymentResourceAction, paymentChoiceId],
            rejectedEntry.RawCommand.Value.GetProperty("paymentChoiceIds").EnumerateArray().Select(choice => choice.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, rejectedEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rejectedEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(rejectedEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

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
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(duplicateReplay.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(duplicateReplay.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(duplicateReplay.Snapshots));
        Assert.Null(duplicateReplay.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(duplicateReplay.State);
        AssertNoPayCostPrompt(duplicateReplay.State);
        Assert.Equal(RunePool.Empty, duplicateReplay.State.RunePools["P1"]);
        Assert.Empty(duplicateReplay.State.StackItems);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, journal.Entries.Count(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal)));

        var conflict = await session.SubmitAsync(
            "P1",
            staleClientIntentId,
            command,
            changedStaleRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(postPaymentStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(postPaymentAuthoritativePromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(postPaymentAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Null(conflict.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(conflict.State);
        AssertNoPayCostPrompt(conflict.State);
        Assert.Equal(RunePool.Empty, conflict.State.RunePools["P1"]);
        Assert.Empty(conflict.State.StackItems);
        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(2, journal.Entries.Count(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal)));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("mana", "PENDING-PAY-COST-MANA-1", "SPEND_MANA:1")]
    [InlineData("generic-power", "PENDING-PAY-COST-GENERIC-POOL-1", "SPEND_POWER:1")]
    [InlineData("typed-power", "PENDING-PAY-COST-RED-POOL-1", "SPEND_POWER:red:1")]
    public async Task PendingPayCostDuplicateClientIntentAfterWindowClosesReturnsCachedOrdinaryResultWithoutMutation(
        string costShape,
        string paymentId,
        string paymentChoiceId)
    {
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(
            PendingOrdinaryPayCostState(costShape, paymentId, paymentChoiceId),
            new CoreRuleEngine(),
            journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var command = new PayCostCommand(
            paymentId,
            "TEST_PENDING_PAY_COST",
            [paymentChoiceId]);
        var rawCommand = PromptScopedPayCostRawCommand(command, prompt);
        var clientIntentId = $"intent-pending-pay-cost-{costShape}-duplicate";

        var paid = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);
        var postPaymentHash = MatchStateHasher.Hash(paid.State);

        var duplicate = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);
        var gameplayEntries = journal.Entries
            .Where(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal))
            .ToArray();

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Equal(["COST_PAID", "PAYMENT_WINDOW_CLOSED"], paid.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Equal(paid.Events, duplicate.Events);
        Assert.Equal(postPaymentHash, MatchStateHasher.Hash(duplicate.State));
        Assert.Null(duplicate.State.PendingPayment);
        AssertNoPayCostPrompt(duplicate.State);
        Assert.Equal(RunePool.Empty, duplicate.State.RunePools["P1"]);
        Assert.Empty(duplicate.State.StackItems);
        Assert.Single(gameplayEntries);
    }

    [Fact]
    public async Task PendingPayCostDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string paymentId = "PENDING-PAY-COST-MANA-1";
        const string paymentChoiceId = "SPEND_MANA:1";
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(
            PendingOrdinaryPayCostState("mana", paymentId, paymentChoiceId),
            new CoreRuleEngine(),
            journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var command = new PayCostCommand(
            paymentId,
            "TEST_PENDING_PAY_COST",
            [paymentChoiceId]);
        var rawCommand = PromptScopedPayCostRawCommand(command, prompt);
        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = command.PaymentId,
            paymentWindow = command.PaymentWindow,
            paymentChoiceIds = command.PaymentChoiceIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        const string clientIntentId = "intent-pending-pay-cost-raw-duplicate";

        Assert.Equal(CommandTypes.PayCost, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(paymentId, rawCommand.GetProperty("paymentId").GetString());
        Assert.Equal("TEST_PENDING_PAY_COST", rawCommand.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            [paymentChoiceId],
            rawCommand.GetProperty("paymentChoiceIds").EnumerateArray().Select(choice => choice.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rawCommand.GetProperty("snapshotTick").GetInt64());

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(["COST_PAID", "PAYMENT_WINDOW_CLOSED"], accepted.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Null(accepted.State.PendingPayment);
        AssertNoPayCostPrompt(accepted.State);
        Assert.Equal(RunePool.Empty, accepted.State.RunePools["P1"]);
        Assert.Empty(accepted.State.StackItems);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedAuthoritativePromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var acceptedAuthoritativeSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        Assert.Single(journal.Entries);
        var payCostEntry = Assert.Single(
            journal.Entries,
            entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal));
        Assert.Equal(clientIntentId, payCostEntry.ClientIntentId);
        Assert.Equal("P1", payCostEntry.PlayerId);
        Assert.True(payCostEntry.Accepted);
        Assert.True(payCostEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PayCost, payCostEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(paymentId, payCostEntry.RawCommand.Value.GetProperty("paymentId").GetString());
        Assert.Equal("TEST_PENDING_PAY_COST", payCostEntry.RawCommand.Value.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            [paymentChoiceId],
            payCostEntry.RawCommand.Value.GetProperty("paymentChoiceIds").EnumerateArray().Select(choice => choice.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, payCostEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, payCostEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(payCostEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Null(replay.ErrorCode);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Null(replay.State.PendingPayment);
        AssertNoPayCostPrompt(replay.State);
        Assert.Equal(RunePool.Empty, replay.State.RunePools["P1"]);
        Assert.Empty(replay.State.StackItems);
        Assert.Single(journal.Entries);
        Assert.Single(
            journal.Entries,
            entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal));

        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(acceptedAuthoritativePromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Null(conflict.State.PendingPayment);
        AssertNoPayCostPrompt(conflict.State);
        Assert.Equal(RunePool.Empty, conflict.State.RunePools["P1"]);
        Assert.Empty(conflict.State.StackItems);
        Assert.Single(journal.Entries);
        Assert.Single(
            journal.Entries,
            entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PendingPayCostDuplicateClientIntentAfterWindowClosesReturnsCachedRuneResourceResultWithoutMutation()
    {
        var paymentResourceAction = "RECYCLE_RUNE:P1-PAY-RUNE";
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(
            PendingGenericPayCostRuneState(),
            new CoreRuleEngine(),
            journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var command = new PayCostCommand(
            "PENDING-PAY-COST-GENERIC-1",
            "TEST_PENDING_PAY_COST",
            [paymentResourceAction, "SPEND_POWER:1"]);
        var rawCommand = PromptScopedPayCostRawCommand(command, prompt);
        const string clientIntentId = "intent-pending-pay-cost-temporary-duplicate";

        var paid = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);
        var postPaymentHash = MatchStateHasher.Hash(paid.State);

        var duplicate = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);
        var gameplayEntries = journal.Entries
            .Where(entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal))
            .ToArray();

        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Equal(
            ["RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
            paid.Events.Select(evt => evt.Kind));
        Assert.Equal(paid.Events, duplicate.Events);
        Assert.Equal(postPaymentHash, MatchStateHasher.Hash(duplicate.State));
        Assert.Null(duplicate.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(duplicate.State);
        AssertNoPayCostPrompt(duplicate.State);
        Assert.Empty(duplicate.State.StackItems);
        Assert.Single(gameplayEntries);
    }

    [Fact]
    public async Task PendingPayCostRuneResourceDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        var paymentResourceAction = "RECYCLE_RUNE:P1-PAY-RUNE";
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(
            PendingGenericPayCostRuneState(),
            new CoreRuleEngine(),
            journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var command = new PayCostCommand(
            "PENDING-PAY-COST-GENERIC-1",
            "TEST_PENDING_PAY_COST",
            [paymentResourceAction, "SPEND_POWER:1"]);
        var rawCommand = PromptScopedPayCostRawCommand(command, prompt);
        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = command.PaymentId,
            paymentWindow = command.PaymentWindow,
            paymentChoiceIds = command.PaymentChoiceIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        const string clientIntentId = "intent-pending-pay-cost-temporary-raw-duplicate";

        Assert.Equal(CommandTypes.PayCost, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal("PENDING-PAY-COST-GENERIC-1", rawCommand.GetProperty("paymentId").GetString());
        Assert.Equal("TEST_PENDING_PAY_COST", rawCommand.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            [paymentResourceAction, "SPEND_POWER:1"],
            rawCommand.GetProperty("paymentChoiceIds").EnumerateArray().Select(choice => choice.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rawCommand.GetProperty("snapshotTick").GetInt64());

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(
            ["RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
            accepted.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Null(accepted.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(accepted.State);
        AssertNoPayCostPrompt(accepted.State);
        Assert.Equal(RunePool.Empty, accepted.State.RunePools["P1"]);
        Assert.Empty(accepted.State.StackItems);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedAuthoritativePromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var acceptedAuthoritativeSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        Assert.Single(journal.Entries);
        var payCostEntry = Assert.Single(
            journal.Entries,
            entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal));
        Assert.Equal(clientIntentId, payCostEntry.ClientIntentId);
        Assert.Equal("P1", payCostEntry.PlayerId);
        Assert.True(payCostEntry.Accepted);
        Assert.True(payCostEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PayCost, payCostEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal("PENDING-PAY-COST-GENERIC-1", payCostEntry.RawCommand.Value.GetProperty("paymentId").GetString());
        Assert.Equal("TEST_PENDING_PAY_COST", payCostEntry.RawCommand.Value.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            [paymentResourceAction, "SPEND_POWER:1"],
            payCostEntry.RawCommand.Value.GetProperty("paymentChoiceIds").EnumerateArray().Select(choice => choice.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, payCostEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, payCostEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(payCostEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Null(replay.ErrorCode);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Null(replay.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(replay.State);
        AssertNoPayCostPrompt(replay.State);
        Assert.Equal(RunePool.Empty, replay.State.RunePools["P1"]);
        Assert.Empty(replay.State.StackItems);
        Assert.Single(journal.Entries);
        Assert.Single(
            journal.Entries,
            entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal));

        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(acceptedAuthoritativePromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Null(conflict.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(conflict.State);
        AssertNoPayCostPrompt(conflict.State);
        Assert.Equal(RunePool.Empty, conflict.State.RunePools["P1"]);
        Assert.Empty(conflict.State.StackItems);
        Assert.Single(journal.Entries);
        Assert.Single(
            journal.Entries,
            entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PendingPayCostTypedRuneResourceDuplicateClientIntentRawPayloadReplaysButChangedRawConflictsWithoutMutation()
    {
        const string paymentId = "PENDING-PAY-COST-GREEN-1";
        const string paymentChoiceId = "SPEND_POWER:green:1";
        var paymentResourceAction = "RECYCLE_RUNE:P1-PAY-RUNE";
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(
            PendingTypedPayCostRuneState(RuneTrait.Green),
            new CoreRuleEngine(),
            journal);
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        var command = new PayCostCommand(
            paymentId,
            "TEST_PENDING_PAY_COST",
            [paymentResourceAction, paymentChoiceId]);
        var rawCommand = PromptScopedPayCostRawCommand(command, prompt);
        var changedRawCommand = JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = command.PaymentId,
            paymentWindow = command.PaymentWindow,
            paymentChoiceIds = command.PaymentChoiceIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick,
            clientNote = "changed-payload"
        });
        const string clientIntentId = "intent-pending-pay-cost-typed-temporary-raw-duplicate";

        Assert.Equal(CommandTypes.PayCost, rawCommand.GetProperty("cmdType").GetString());
        Assert.Equal(paymentId, rawCommand.GetProperty("paymentId").GetString());
        Assert.Equal("TEST_PENDING_PAY_COST", rawCommand.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            [paymentResourceAction, paymentChoiceId],
            rawCommand.GetProperty("paymentChoiceIds").EnumerateArray().Select(choice => choice.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, rawCommand.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, rawCommand.GetProperty("snapshotTick").GetInt64());

        var accepted = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Null(accepted.ErrorCode);
        Assert.Equal(
            ["RUNE_RECYCLED", "POWER_GAINED", "COST_PAID", "PAYMENT_WINDOW_CLOSED"],
            accepted.Events.Select(gameEvent => gameEvent.Kind));
        Assert.Null(accepted.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(accepted.State);
        AssertNoPayCostPrompt(accepted.State);
        Assert.Equal(new RunePool(0, 0), accepted.State.RunePools["P1"]);
        Assert.Empty(accepted.State.StackItems);
        var acceptedStateHash = MatchStateHasher.Hash(accepted.State);
        var acceptedEventsHash = MatchStateHasher.HashValue(accepted.Events);
        var acceptedPromptsHash = MatchStateHasher.HashValue(accepted.Prompts);
        var acceptedSnapshotsHash = MatchStateHasher.HashValue(accepted.Snapshots);
        var acceptedAuthoritativePromptsHash = MatchStateHasher.HashValue(ResolutionResult.BuildPrompts(accepted.State));
        var acceptedAuthoritativeSnapshotsHash = MatchStateHasher.HashValue(ResolutionResult.BuildSnapshots(accepted.State));
        Assert.Single(journal.Entries);
        var payCostEntry = Assert.Single(
            journal.Entries,
            entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal));
        Assert.Equal(clientIntentId, payCostEntry.ClientIntentId);
        Assert.Equal("P1", payCostEntry.PlayerId);
        Assert.True(payCostEntry.Accepted);
        Assert.True(payCostEntry.RawCommand.HasValue);
        Assert.Equal(CommandTypes.PayCost, payCostEntry.RawCommand.Value.GetProperty("cmdType").GetString());
        Assert.Equal(paymentId, payCostEntry.RawCommand.Value.GetProperty("paymentId").GetString());
        Assert.Equal("TEST_PENDING_PAY_COST", payCostEntry.RawCommand.Value.GetProperty("paymentWindow").GetString());
        Assert.Equal(
            [paymentResourceAction, paymentChoiceId],
            payCostEntry.RawCommand.Value.GetProperty("paymentChoiceIds").EnumerateArray().Select(choice => choice.GetString()!).ToArray());
        Assert.Equal(prompt.PromptId, payCostEntry.RawCommand.Value.GetProperty("promptId").GetString());
        Assert.Equal(prompt.SnapshotTick, payCostEntry.RawCommand.Value.GetProperty("snapshotTick").GetInt64());
        Assert.False(payCostEntry.RawCommand.Value.TryGetProperty("clientNote", out _));

        var replay = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            rawCommand,
            CancellationToken.None);

        Assert.True(replay.Accepted, replay.ErrorMessage);
        Assert.Null(replay.ErrorCode);
        Assert.Equal(acceptedEventsHash, MatchStateHasher.HashValue(replay.Events));
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(replay.State));
        Assert.Equal(acceptedPromptsHash, MatchStateHasher.HashValue(replay.Prompts));
        Assert.Equal(acceptedSnapshotsHash, MatchStateHasher.HashValue(replay.Snapshots));
        Assert.Null(replay.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(replay.State);
        AssertNoPayCostPrompt(replay.State);
        Assert.Equal(new RunePool(0, 0), replay.State.RunePools["P1"]);
        Assert.Empty(replay.State.StackItems);
        Assert.Single(journal.Entries);
        Assert.Single(
            journal.Entries,
            entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal));

        var conflict = await session.SubmitAsync(
            "P1",
            clientIntentId,
            command,
            changedRawCommand,
            CancellationToken.None);

        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(acceptedStateHash, MatchStateHasher.Hash(conflict.State));
        Assert.Equal(acceptedAuthoritativePromptsHash, MatchStateHasher.HashValue(conflict.Prompts));
        Assert.Equal(acceptedAuthoritativeSnapshotsHash, MatchStateHasher.HashValue(conflict.Snapshots));
        Assert.Null(conflict.State.PendingPayment);
        RetiredPaymentLedgerTests.AssertNoLedger(conflict.State);
        AssertNoPayCostPrompt(conflict.State);
        Assert.Equal(new RunePool(0, 0), conflict.State.RunePools["P1"]);
        Assert.Empty(conflict.State.StackItems);
        Assert.Single(journal.Entries);
        Assert.Single(
            journal.Entries,
            entry => string.Equals(entry.CommandType, CommandTypes.PayCost, StringComparison.Ordinal));
        Assert.DoesNotContain(journal.Entries, entry =>
            entry.RawCommand is { } entryRaw
            && entryRaw.TryGetProperty("clientNote", out var clientNote)
            && string.Equals(clientNote.GetString(), "changed-payload", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("mana", "PENDING-PAY-COST-MANA-1", "SPEND_MANA:1", "wrong-player")]
    [InlineData("mana", "PENDING-PAY-COST-MANA-1", "SPEND_MANA:1", "wrong-payment-id")]
    [InlineData("mana", "PENDING-PAY-COST-MANA-1", "SPEND_MANA:1", "wrong-payment-window")]
    [InlineData("generic-power", "PENDING-PAY-COST-GENERIC-POOL-1", "SPEND_POWER:1", "wrong-player")]
    [InlineData("generic-power", "PENDING-PAY-COST-GENERIC-POOL-1", "SPEND_POWER:1", "wrong-payment-id")]
    [InlineData("generic-power", "PENDING-PAY-COST-GENERIC-POOL-1", "SPEND_POWER:1", "wrong-payment-window")]
    [InlineData("typed-power", "PENDING-PAY-COST-RED-POOL-1", "SPEND_POWER:red:1", "wrong-player")]
    [InlineData("typed-power", "PENDING-PAY-COST-RED-POOL-1", "SPEND_POWER:red:1", "wrong-payment-id")]
    [InlineData("typed-power", "PENDING-PAY-COST-RED-POOL-1", "SPEND_POWER:red:1", "wrong-payment-window")]
    public async Task PendingPayCostRejectsWrongPlayerPaymentIdOrWindowOrdinaryActiveWindowWithoutMutation(
        string costShape,
        string paymentId,
        string paymentChoiceId,
        string scenario)
    {
        var state = PendingOrdinaryPayCostState(costShape, paymentId, paymentChoiceId);
        var initialHash = MatchStateHasher.Hash(state);
        var command = new PayCostCommand(
            scenario == "wrong-payment-id" ? $"{paymentId}:stale" : paymentId,
            scenario == "wrong-payment-window" ? "OTHER_PAYMENT_WINDOW" : "TEST_PENDING_PAY_COST",
            [paymentChoiceId]);
        var intentPlayerId = scenario == "wrong-player" ? "P2" : "P1";

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-pending-pay-cost-{costShape}-ordinary-active-{scenario}", intentPlayerId, CommandTypes.PayCost),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.NotNull(result.State.PendingPayment);
        var pendingPayment = result.State.PendingPayment;
        Assert.Equal(paymentId, pendingPayment.PaymentId);
        Assert.Equal("TEST_PENDING_PAY_COST", pendingPayment.PaymentWindow);
        Assert.Equal("P1", pendingPayment.PlayerId);
        Assert.Equal([paymentChoiceId], pendingPayment.LegalPaymentChoiceIds);
        Assert.Equal(state.RunePools["P1"], result.State.RunePools["P1"]);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P2"]);
        Assert.Empty(result.State.StackItems);
        AssertAuthoritativePayCostPrompt(result.State, paymentId, paymentChoiceId);
    }

    [Theory]
    [MemberData(nameof(PendingPayCostIllegalOrdinaryChoiceCases))]
    public async Task PendingPayCostRejectsIllegalOrdinaryActiveWindowChoicesWithoutMutation(
        string costShape,
        string paymentId,
        string paymentChoiceId,
        string scenario,
        string[] submittedPaymentChoiceIds)
    {
        var state = PendingOrdinaryPayCostState(costShape, paymentId, paymentChoiceId);
        var initialHash = MatchStateHasher.Hash(state);
        var command = new PayCostCommand(
            paymentId,
            "TEST_PENDING_PAY_COST",
            submittedPaymentChoiceIds);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-pending-pay-cost-{costShape}-ordinary-active-illegal-{scenario}", "P1", CommandTypes.PayCost),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.NotNull(result.State.PendingPayment);
        var pendingPayment = result.State.PendingPayment;
        Assert.Equal(paymentId, pendingPayment.PaymentId);
        Assert.Equal("TEST_PENDING_PAY_COST", pendingPayment.PaymentWindow);
        Assert.Equal("P1", pendingPayment.PlayerId);
        Assert.Equal([paymentChoiceId], pendingPayment.LegalPaymentChoiceIds);
        Assert.Equal(state.RunePools["P1"], result.State.RunePools["P1"]);
        Assert.Equal(RunePool.Empty, result.State.RunePools["P2"]);
        Assert.Empty(result.State.StackItems);
        AssertAuthoritativePayCostPrompt(result.State, paymentId, paymentChoiceId);
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
            new RunePool(0, 1),
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
        Assert.Equal(1, costEvent.Payload["spellshieldTaxPower"]);
        Assert.Equal(0, costEvent.Payload["totalManaCost"]);
        Assert.Equal(2, costEvent.Payload["genericPower"]);
        Assert.Equal(2, costEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costEvent.Payload["remainingMana"]);
        Assert.Equal(0, costEvent.Payload["remainingPower"]);
    }

    [Fact]
    public async Task ActivateAbilityXerathRejectsRecycleRuneWhenSpellshieldTaxPowerIsMissingWithoutMutation()
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

    private static MatchState TinyGuardianState(RunePool runePool, IReadOnlyList<string>? baseObjectIds = null)
    {
        return new MatchState(
            "payment-engine-tiny-guardian-room",
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
                    Hand = ["P1-UNIT-TINY-GUARDIAN"],
                    Base = baseObjectIds ?? []
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
                ["P1-UNIT-TINY-GUARDIAN"] = TinyGuardianCard()
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-TINY-GUARDIAN"] = new("P1", "HAND")
            });
    }

    private static IReadOnlyDictionary<string, object?> AssertSinglePlayCardSourceRequirement(MatchState state)
    {
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        return Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
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

    private static MatchState PendingGenericPayCostRuneState() => PendingRunePayCostState(false);

    private static MatchState PendingOrdinaryPayCostState(
        string costShape,
        string paymentId,
        string paymentChoiceId)
    {
        var runePool = costShape switch
        {
            "mana" => new RunePool(1, 0),
            "generic-power" => new RunePool(0, 1),
            "typed-power" => new RunePool(
                0,
                0,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(costShape), costShape, null)
        };
        var pendingPayment = costShape switch
        {
            "mana" => new PendingPaymentState(
                paymentId,
                "TEST_PENDING_PAY_COST",
                "P1",
                manaCost: 1,
                legalPaymentChoiceIds: [paymentChoiceId],
                reason: "PENDING_PAY_COST_ORDINARY_MANA_REPLAY_TEST"),
            "generic-power" => new PendingPaymentState(
                paymentId,
                "TEST_PENDING_PAY_COST",
                "P1",
                powerCost: 1,
                legalPaymentChoiceIds: [paymentChoiceId],
                reason: "PENDING_PAY_COST_ORDINARY_GENERIC_POWER_REPLAY_TEST"),
            "typed-power" => new PendingPaymentState(
                paymentId,
                "TEST_PENDING_PAY_COST",
                "P1",
                powerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                },
                legalPaymentChoiceIds: [paymentChoiceId],
                reason: "PENDING_PAY_COST_ORDINARY_TYPED_POWER_REPLAY_TEST"),
            _ => throw new ArgumentOutOfRangeException(nameof(costShape), costShape, null)
        };

        return new MatchState(
            "payment-engine-pending-pay-cost-ordinary-room",
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
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal),
            pendingPayment: pendingPayment);
    }

    public static IEnumerable<object[]> PendingPayCostIllegalOrdinaryChoiceCases()
    {
        foreach (var (costShape, paymentId, paymentChoiceId, unsupportedPaymentChoiceId) in new[]
                 {
                     ("mana", "PENDING-PAY-COST-MANA-1", "SPEND_MANA:1", "SPEND_MANA:2"),
                     ("generic-power", "PENDING-PAY-COST-GENERIC-POOL-1", "SPEND_POWER:1", "SPEND_POWER:blue:1"),
                     ("typed-power", "PENDING-PAY-COST-RED-POOL-1", "SPEND_POWER:red:1", "SPEND_POWER:blue:1")
                 })
        {
            yield return
            [
                costShape,
                paymentId,
                paymentChoiceId,
                "unsupported-choice-only",
                new[] { unsupportedPaymentChoiceId }
            ];
            yield return
            [
                costShape,
                paymentId,
                paymentChoiceId,
                "legal-plus-unsupported-choice",
                new[] { paymentChoiceId, unsupportedPaymentChoiceId }
            ];
            yield return
            [
                costShape,
                paymentId,
                paymentChoiceId,
                "duplicate-legal-choice",
                new[] { paymentChoiceId, paymentChoiceId }
            ];
            yield return
            [
                costShape,
                paymentId,
                paymentChoiceId,
                "blank-choice-entry",
                new[] { paymentChoiceId, "  " }
            ];
        }
    }

    private static void AssertNoPayCostPrompt(MatchState state)
    {
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.DoesNotContain(CommandTypes.PayCost, prompt.Actions);
        Assert.DoesNotContain(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
    }

    private static void AssertAuthoritativePayCostPrompt(
        MatchState state,
        string paymentId,
        string paymentChoiceId,
        IReadOnlyList<string>? expectedPaymentResourceChoiceIds = null)
    {
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.PayCost, prompt.View?.Type);
        Assert.Contains(CommandTypes.PayCost, prompt.Actions);
        var candidate = Assert.Single(
            prompt.Candidates ?? [],
            promptCandidate => string.Equals(promptCandidate.Action, CommandTypes.PayCost, StringComparison.Ordinal));
        Assert.True(candidate.Enabled);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(candidate.Metadata);
        Assert.Equal(paymentId, metadata["paymentId"]);
        Assert.Equal("TEST_PENDING_PAY_COST", metadata["paymentWindow"]);
        var paymentChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentChoices"]);
        Assert.Equal([paymentChoiceId], paymentChoices.Select(choice => choice.Id).ToArray());
        var paymentResourceChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(metadata["paymentResourceChoices"]);
        Assert.Equal(
            expectedPaymentResourceChoiceIds ?? [],
            paymentResourceChoices.Select(choice => choice.Id).ToArray());
    }

    private static JsonElement PromptScopedPayCostRawCommand(PayCostCommand command, ActionPromptDto prompt)
    {
        return JsonSerializer.SerializeToElement(new
        {
            cmdType = CommandTypes.PayCost,
            paymentId = command.PaymentId,
            paymentWindow = command.PaymentWindow,
            paymentChoiceIds = command.PaymentChoiceIds,
            promptId = prompt.PromptId,
            snapshotTick = prompt.SnapshotTick
        });
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

    private static MatchState PendingTypedPayCostRuneState(string trait) => PendingRunePayCostState(true, trait);

    private static MatchState PendingRunePayCostState(bool typed, string trait = RuneTrait.Red)
    {
        var paymentId = typed ? "PENDING-PAY-COST-GREEN-1" : "PENDING-PAY-COST-GENERIC-1";
        var spend = typed ? $"SPEND_POWER:{trait}:1" : "SPEND_POWER:1";
        var state = PendingOrdinaryPayCostState("generic-power", paymentId, spend);
        return state with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = PlayerZones.Empty with { Base = ["P1-PAY-RUNE"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["P1-PAY-RUNE"] = RuneCard("P1-PAY-RUNE", trait) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["P1-PAY-RUNE"] = new("P1", "BASE") },
            PendingPayment = state.PendingPayment! with {
                PowerCost = typed ? 0 : 1,
                PowerCostByTrait = typed ? new Dictionary<string, int> { [trait] = 1 } : new Dictionary<string, int>(),
                PaymentResourceActionIds = ["RECYCLE_RUNE:P1-PAY-RUNE"] }
        };
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

    private static CardObjectState TinyGuardianCard()
    {
        return new(
            "P1-UNIT-TINY-GUARDIAN",
            cardNo: "OGN·044/298",
            tags: [CardObjectTags.UnitCard],
            manaCost: 2,
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
}
