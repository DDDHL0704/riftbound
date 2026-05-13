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
        Assert.Equal("PLAY_CARD", costEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-SPELL-BULLET-TIME", costEvent.Payload["sourceObjectId"]);
        Assert.Equal(1, costEvent.Payload["baseManaCost"]);
        Assert.Equal(1, costEvent.Payload["totalManaCost"]);
        Assert.Equal(0, costEvent.Payload["genericPower"]);
        Assert.Equal(2, costEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costEvent.Payload["experienceCost"]);
        Assert.Equal(["SPEND_POWER:red:2"], Assert.IsType<string[]>(costEvent.Payload["optionalCosts"]));
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

    private static CardObjectState RuneCard(string objectId, string trait)
    {
        return new(
            objectId,
            isExhausted: true,
            tags: [CardObjectTags.RuneCard, $"COLOR:{trait}"],
            cardNo: string.Equals(trait, RuneTrait.Blue, StringComparison.Ordinal) ? "UNL-R02" : "UNL-R01",
            ownerId: "P1",
            controllerId: "P1");
    }
}
