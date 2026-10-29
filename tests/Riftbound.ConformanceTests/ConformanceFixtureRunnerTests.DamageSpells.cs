using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task CoreRuleEngineBanishesUnitWhenPunishmentDamageWouldDestroyIt()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-punishment-lethal-damage-banishes-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Banished);
    }

    [Fact]
    public async Task CoreRuleEngineBanishesPunishmentTargetDestroyedLaterThisTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-punishment-banishes-if-destroyed-later.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Banished);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineDrawsWhenShatteredFireDestroysTarget()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-shattered-fire-draws-after-lethal-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-MAIN-DRAWN-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineDoesNotDrawWhenShatteredFireTargetSurvives()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-shattered-fire-does-not-draw-without-destroy.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("CARD_DRAWN", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysStarfallAgainstTwoUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-starfall-damages-two-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BASE-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineAllowsStarfallToDamageSameUnitTwice()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-starfall-can-damage-same-unit-twice.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDuelMutualPowerDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-duel-mutual-power-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-DUEL-001"].Damage);
        Assert.DoesNotContain("P2-UNIT-DUEL-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysGentlemanDuelPowerThenMutualDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-gentleman-duel-power-then-mutual-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-GENTLEMAN-001"].Damage);
        Assert.Equal(5, result.FinalState.CardObjects["P1-UNIT-GENTLEMAN-001"].Power);
        Assert.DoesNotContain("P2-UNIT-GENTLEMAN-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMarchingOrdersEchoMutualPowerDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-marching-orders-echo-mutual-power-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Equal(6, result.FinalState.CardObjects["P1-BASE-MARCHING-001"].Damage);
        Assert.DoesNotContain("P2-BATTLEFIELD-MARCHING-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMarchingOrdersAgainstEnemyBaseUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-MARCHING-ORDERS"],
                    Battlefields = ["P1-UNIT-MARCHING-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-MARCHING-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-MARCHING-001"] = new("P1-UNIT-MARCHING-001", power: 4),
                ["P2-BASE-MARCHING-001"] = new("P2-BASE-MARCHING-001", power: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-marching-orders-enemy-base", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-MARCHING-ORDERS",
                "SFD·114/221",
                ["P1-UNIT-MARCHING-001", "P2-BASE-MARCHING-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-MARCHING-ORDERS"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4MarchingOrdersEnemyBaseTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-marching-orders-enemy-base-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-MARCHING-ORDERS"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-MARCHING-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BASE-MARCHING-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-MARCHING-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-MARCHING-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BASE-MARCHING-001"].Damage);
        Assert.Equal(3, result.FinalState.CardObjects["P2-BASE-MARCHING-001"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDuelWhenTargetsAreReversed()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-DUEL"],
                    Battlefields = ["P1-UNIT-DUEL-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-DUEL-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-DUEL-001"] = new("P1-UNIT-DUEL-001", power: 4),
                ["P2-UNIT-DUEL-001"] = new("P2-UNIT-DUEL-001", power: 2)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-duel-reversed-targets", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-DUEL",
                "OGN·128/298",
                ["P2-UNIT-DUEL-001", "P1-UNIT-DUEL-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-DUEL"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4DuelTargetOrderRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-duel-target-order-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-DUEL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-DUEL-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-UNIT-DUEL-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-DUEL-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-DUEL-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-DUEL-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-DUEL-001"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysClashOfGiantsMutualPowerDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-clash-of-giants-mutual-power-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.Equal(3, result.FinalState.CardObjects["P1-BASE-GIANT-001"].Damage);
        Assert.DoesNotContain("P2-BATTLEFIELD-GIANT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysIcathianRainAgainstSameUnitSixTimes()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-icathian-rain-can-hit-same-unit-six-times.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(6, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBladeWhirlwindAgainstAllBattlefieldUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blade-whirlwind-damage-all-battlefield-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTibbersDamageAllBattlefieldUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-tibbers-damage-all-battlefield-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-TIBBERS"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(7, result.FinalState.CardObjects["P1-UNIT-TIBBERS"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-TIBBERS-FRIENDLY-UNIT"].Damage);
        Assert.Equal(3, result.FinalState.CardObjects["P2-TIBBERS-ENEMY-UNIT"].Damage);
        Assert.Equal(2, result.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineTibbersResolutionSkipsOpponentControlledBattlefieldObject()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-TIBBERS-DIRTY-BATTLEFIELD-DAMAGE",
                    "P1",
                    "P1-UNIT-TIBBERS",
                    "TIBBERS_PLAY_UNIT_DAMAGE_ALL_BATTLEFIELD_UNITS_3",
                    "OGS·018/024",
                    [])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P2-TIBBERS-VALID-BATTLEFIELD-UNIT",
                        "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-TIBBERS-VALID-BATTLEFIELD-UNIT"] = new(
                    "P2-TIBBERS-VALID-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"] = new(
                    "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-tibbers-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-tibbers-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(3, p2Pass.State.CardObjects["P2-TIBBERS-VALID-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(["P1-UNIT-TIBBERS"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(
            1,
            p2Pass.Events.Count(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            p2Pass.Events,
            gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
                && string.Equals(
                    gameEvent.Payload["targetObjectId"] as string,
                    "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT",
                    StringComparison.Ordinal));
    }

    [Fact]
    public Task CoreRuleEngineRejectsTibbersWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            8,
            "P1-UNIT-TIBBERS",
            "OGS·018/024",
            "P1-TIBBERS-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEnginePlaysDaisyNoTraitDiscountActiveUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-daisy-no-trait-discount-active-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-DAISY"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(8, result.FinalState.CardObjects["P1-UNIT-DAISY"].Power);
        Assert.False(result.FinalState.CardObjects["P1-UNIT-DAISY"].IsExhausted);
    }

    [Fact]
    public Task CoreRuleEngineRejectsDaisyWhenTargetsAreProvided() =>
        AssertSourceUnitWithTargetRejectedAsync(
            9,
            "P1-UNIT-DAISY",
            "UNL-196/219",
            "P1-DAISY-BASE-UNIT-001");

    [Fact]
    public async Task CoreRuleEngineDestroysUnitsAfterLethalBladeWhirlwindDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-blade-whirlwind-lethal-damage-destroys-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysStayAwayStunAndDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-stay-away-stun-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["STUNNED"], result.FinalState.CardObjects["P2-UNIT-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDisposalOrderDrawMode()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-disposal-order-draw-mode.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-MAIN-DRAWN-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysAssembleTheRanksDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-assemble-the-ranks-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-ASSEMBLE-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCallToActionDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-call-to-action-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-CALL-ACTION-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysVoidRushDrawNoFreePlay()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-void-rush-draw-no-free-play.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-VOID-RUSH-DRAW-001", "P1-VOID-RUSH-DRAW-002"],
            result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysReflectionsSwapDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-reflections-swap-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-REFLECTIONS-BATTLEFIELD-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-REFLECTIONS-BASE-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-REFLECTIONS-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineReflectionsResolutionSkipsOpponentControlledFriendlyZoneTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-REFLECTIONS-DIRTY-SWAP",
                    "P1",
                    "P1-SPELL-REFLECTIONS",
                    "REFLECTIONS_SWAP_FRIENDLY_UNITS_DRAW_1",
                    "UNL-083/219",
                    [
                        "P1-DIRTY-P2-CONTROLLED-BASE-UNIT",
                        "P1-REFLECTIONS-VALID-BATTLEFIELD-UNIT"
                    ])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"],
                    Battlefields = ["P1-REFLECTIONS-VALID-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"] = new(
                    "P1-DIRTY-P2-CONTROLLED-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-REFLECTIONS-VALID-BATTLEFIELD-UNIT"] = new(
                    "P1-REFLECTIONS-VALID-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-reflections-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-reflections-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-REFLECTIONS-VALID-BATTLEFIELD-UNIT"], p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-SPELL-REFLECTIONS"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_LOCATIONS_SWAPPED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsReflectionsWithoutEphemeralTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-REFLECTIONS"],
                    Base = ["P1-REFLECTIONS-BASE-001"],
                    Battlefields = ["P1-REFLECTIONS-BATTLEFIELD-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-REFLECTIONS-BASE-001"] = new("P1-REFLECTIONS-BASE-001", tags: [CardObjectTags.UnitCard]),
                ["P1-REFLECTIONS-BATTLEFIELD-001"] = new("P1-REFLECTIONS-BATTLEFIELD-001", tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-reflections-no-ephemeral", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-REFLECTIONS",
                "UNL-083/219",
                ["P1-REFLECTIONS-BASE-001", "P1-REFLECTIONS-BATTLEFIELD-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-REFLECTIONS"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-REFLECTIONS-BASE-001"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-REFLECTIONS-BATTLEFIELD-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysThunderingDropBasePowerDamageMove()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-thundering-drop-base-power-damage-move.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.True(result.FinalState.BattleState.IsActive);
        Assert.Equal(SelectedBattlefieldFixture.Field, result.FinalState.BattleState.BattlefieldObjectId);
        Assert.Equal("P1", result.FinalState.FocusPlayerId);
        Assert.Equal(SelectedBattlefieldFixture.Field, result.FinalState.ObjectLocations["P1-THUNDERING-DROP-BASE-001"].BattlefieldObjectId);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-THUNDERING-DROP-BASE-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(3, result.FinalState.CardObjects["P2-THUNDERING-DROP-UNIT-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P2-THUNDERING-DROP-UNIT-002"].Damage);
    }

    [Fact]
    public async Task CoreRuleEngineThunderingDropResolutionSkipsOpponentControlledFriendlyBaseTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-THUNDERING-DROP-DIRTY-BASE",
                    "P1",
                    "P1-SPELL-THUNDERING-DROP",
                    "THUNDERING_DROP_BASE_UNIT_POWER_DAMAGE_ENEMY_BATTLEFIELD_MOVE",
                    "OGN·250/298",
                    ["P1-DIRTY-P2-CONTROLLED-BASE-UNIT", SelectedBattlefieldFixture.Field])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P2-THUNDERING-DROP-DIRTY-ENEMY-001",
                        "P2-THUNDERING-DROP-DIRTY-ENEMY-002"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"] = new(
                    "P1-DIRTY-P2-CONTROLLED-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-THUNDERING-DROP-DIRTY-ENEMY-001"] = new(
                    "P2-THUNDERING-DROP-DIRTY-ENEMY-001",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-THUNDERING-DROP-DIRTY-ENEMY-002"] = new(
                    "P2-THUNDERING-DROP-DIRTY-ENEMY-002",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-thundering-drop-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-thundering-drop-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P1-DIRTY-P2-CONTROLLED-BASE-UNIT"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-THUNDERING-DROP-DIRTY-ENEMY-001"].Damage);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-THUNDERING-DROP-DIRTY-ENEMY-002"].Damage);
        Assert.Equal(["P1-SPELL-THUNDERING-DROP"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BATTLEFIELD", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBattleCommandMoveFriendlyAndOpponentUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-battle-command-move-friendly-and-opponent-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.True(ConformanceFixtureRunner.CompareExpected(fixture, result).Count == 0, string.Join("\n", ConformanceFixtureRunner.CompareExpected(fixture, result)));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["BF", "P1-BATTLE-COMMAND-FIELD-KEEPER", "P1-BATTLE-COMMAND-BASE-001"],
            result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BATTLE-COMMAND-BASE-001"], result.FinalState.PlayerZones["P2"].Battlefields);
    }

    [Fact]
    public async Task CoreRuleEngineBattleCommandResolutionSkipsOpponentControlledFriendlyBaseTarget()
    {
        var cast = await OfficialGraveyardRecastTests.Act(CommandedMovementTests.Position(), "P1", CommandedMovementTests.Command());
        // The declared friendly target changes controller before resolution. Its
        // stale physical zone cannot make it a legal friendly target again.
        var state = cast.State with { CardObjects = new Dictionary<string,CardObjectState>(cast.State.CardObjects) {
            ["A"] = cast.State.CardObjects["A"] with { ControllerId = "P2" } } };
        var opened = await OfficialGraveyardRecastTests.Top(state);
        var done = await DestructionOrderTests.Select(opened.State, "MOVE:D");
        Assert.Contains("A", done.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("A", done.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("D", done.State.PlayerZones["P2"].Base);
        Assert.Contains("D", done.State.PlayerZones["P2"].Battlefields);
        Assert.Equal("BF2", done.State.ObjectLocations["D"].BattlefieldObjectId);
        Assert.Contains("AOE", done.State.PlayerZones["P1"].Graveyard);
        var move = Assert.Single(done.Events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Equal("D", move.Payload["targetObjectId"]);
        Assert.Equal("P2", move.Payload["movingPlayerId"]);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBattleCommandWhenTargetsAreReversed()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BATTLE-COMMAND"],
                    Base = ["P1-BATTLE-COMMAND-BASE-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BATTLE-COMMAND-BASE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-COMMAND-BASE-001"] = new("P1-BATTLE-COMMAND-BASE-001", power: 4),
                ["P2-BATTLE-COMMAND-BASE-001"] = new("P2-BATTLE-COMMAND-BASE-001", power: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-battle-command-reversed-targets", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BATTLE-COMMAND",
                "UNL-101/219",
                ["P2-BATTLE-COMMAND-BASE-001", "P1-BATTLE-COMMAND-BASE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BATTLE-COMMAND"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLE-COMMAND-BASE-001"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLE-COMMAND-BASE-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4BattleCommandTargetOrderRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-battle-command-target-order-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BATTLE-COMMAND"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BATTLE-COMMAND-BASE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BATTLE-COMMAND-FIELD-KEEPER"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLE-COMMAND-BASE-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BATTLE-COMMAND-BASE-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BATTLE-COMMAND-BASE-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BATTLE-COMMAND-BASE-001"].Damage);
        Assert.Equal(3, result.FinalState.CardObjects["P2-BATTLE-COMMAND-BASE-001"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysVoidAssaultMoveFriendlyAndEnemyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-void-assault-move-friendly-and-enemy-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["BF", "P1-VOID-ASSAULT-FIELD-KEEPER", "P1-VOID-ASSAULT-FRIENDLY-001"],
            result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-VOID-ASSAULT-ENEMY-001"], result.FinalState.PlayerZones["P2"].Battlefields);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsVoidAssaultWhenTargetsAreReversed()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-VOID-ASSAULT"],
                    Base = ["P1-VOID-ASSAULT-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-VOID-ASSAULT-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-VOID-ASSAULT-FRIENDLY-001"] = new(
                    "P1-VOID-ASSAULT-FRIENDLY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-VOID-ASSAULT-ENEMY-001"] = new(
                    "P2-VOID-ASSAULT-ENEMY-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-void-assault-reversed-targets", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-VOID-ASSAULT",
                "UNL-202/219",
                ["P2-VOID-ASSAULT-ENEMY-001", "P1-VOID-ASSAULT-FRIENDLY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-VOID-ASSAULT"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-VOID-ASSAULT-FRIENDLY-001"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-VOID-ASSAULT-ENEMY-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4VoidAssaultTargetOrderRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-void-assault-target-order-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-VOID-ASSAULT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-VOID-ASSAULT-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-VOID-ASSAULT-FIELD-KEEPER"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-VOID-ASSAULT-ENEMY-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-VOID-ASSAULT-FRIENDLY-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P1-VOID-ASSAULT-FRIENDLY-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-VOID-ASSAULT-ENEMY-001"].Damage);
        Assert.Equal(3, result.FinalState.CardObjects["P2-VOID-ASSAULT-ENEMY-001"].Power);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBulletTimePowerDamageEnemyBattlefield()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-bullet-time-power-damage-enemy-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(3, result.FinalState.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P2-BULLET-TIME-UNIT-002"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BULLET-TIME-BASE-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBulletTimeWhenPowerCostIsInsufficient()
    {
        var state = PunishmentState(mana: 1) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(1, 2),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BULLET-TIME"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BULLET-TIME-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BULLET-TIME-UNIT-001"] = new("P2-BULLET-TIME-UNIT-001", power: 5)
            }
        };
        state = SelectedBattlefieldFixture.WithExplicitFields(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-bullet-time-insufficient-power", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BULLET-TIME",
                "OGN·268/298",
                [SelectedBattlefieldFixture.Field],
                OptionalCosts: ["SPEND_POWER:3"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 2), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BULLET-TIME"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, result.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        Assert.Empty(result.State.StackItems);
        // CN204.3.a: unavailable quantities are rejected at the actual instruction,
        // rather than preventing the spell's otherwise affordable declaration.
        var played = await new CoreRuleEngine().ResolveAsync(state,
            new("bullet-time-declare-before-instruction", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("P1-SPELL-BULLET-TIME", "OGN·268/298", [SelectedBattlefieldFixture.Field]), default);
        Assert.True(played.Accepted, played.ErrorMessage);
        var payment = await InstructionPowerPaymentTimingTests.ToPayment(played.State);
        var unavailable = await InstructionPowerPaymentTimingTests.Pay(payment, "SPEND_POWER:3");
        Assert.False(unavailable.Accepted);
        Assert.Empty(unavailable.Events);
        Assert.Equal(MatchStateHasher.Hash(payment), MatchStateHasher.Hash(unavailable.State));
        Assert.Equal(2, unavailable.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, unavailable.State.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
    }
}
