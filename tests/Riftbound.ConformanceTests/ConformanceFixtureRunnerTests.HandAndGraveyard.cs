using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task CoreRuleEngineRejectsHuntingRhythmAgainstEnemyUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HUNTING-RHYTHM"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-HUNTING-RHYTHM-ENEMY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-HUNTING-RHYTHM-ENEMY-001"] = new(
                    "P2-HUNTING-RHYTHM-ENEMY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hunting-rhythm-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-HUNTING-RHYTHM", "UNL-184/219", ["P2-HUNTING-RHYTHM-ENEMY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HUNTING-RHYTHM"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-HUNTING-RHYTHM-ENEMY-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDisposalOrderRecycleMode()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-disposal-order-recycle-opponent-graveyard.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P2-MAIN-001", "P2-GRAVE-003", "P2-GRAVE-001"],
            result.FinalState.PlayerZones["P2"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRecycleTargetsSkipsOpponentZoneCardNotControlledByThatPlayer()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            PassedPriorityPlayerIds = ["P1"],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-MAIN-001"],
                    Graveyard = ["P2-GRAVE-CONTROLLED", "P2-GRAVE-DIRTY-P1-OWNED"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-GRAVE-CONTROLLED"] = new(
                    "P2-GRAVE-CONTROLLED",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-GRAVE-DIRTY-P1-OWNED"] = new(
                    "P2-GRAVE-DIRTY-P1-OWNED",
                    cardNo: "SFD·126/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1")
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-DISPOSAL-DIRTY-RECYCLE",
                    "P1",
                    "P1-SPELL-DISPOSAL-ORDER",
                    "DISPOSAL_ORDER_RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3",
                    "UNL-103/219",
                    ["P2-GRAVE-CONTROLLED", "P2-GRAVE-DIRTY-P1-OWNED"])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-pass-dirty-recycle-target", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(["P2-MAIN-001", "P2-GRAVE-CONTROLLED"], result.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(["P2-GRAVE-DIRTY-P1-OWNED"], result.State.PlayerZones["P2"].Graveyard);

        var recycleEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal));
        Assert.Equal("P2", recycleEvent.Payload["playerId"]);
        Assert.Equal(1, recycleEvent.Payload["count"]);
        Assert.Equal(["P2-GRAVE-CONTROLLED"], Assert.IsAssignableFrom<IReadOnlyList<string>>(recycleEvent.Payload["cardIds"]));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPortalpaloozaOtherChoosesCards()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-portalpalooza-other-chooses-cards.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-PORTAL-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-PORTAL-DRAW-001"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPortalpaloozaOtherChoosesRunes()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-portalpalooza-other-chooses-runes.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-PORTAL-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-PORTAL-RUNE-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.True(result.FinalState.CardObjects["P1-PORTAL-RUNE-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P2-PORTAL-RUNE-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCovertSabotageRecycleOpponentNonUnitHandCard()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-covert-sabotage-recycle-opponent-non-unit-hand-card.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2-COVERT-SABOTAGE-UNIT-CARD"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(
            ["P2-MAIN-KEEPER", "P2-COVERT-SABOTAGE-SPELL-CARD"],
            result.FinalState.PlayerZones["P2"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCovertSabotageUnitHandCardTarget()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-COVERT-SABOTAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-UNIT-CARD", "P2-SPELL-CARD"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-CARD"] = new("P2-UNIT-CARD", tags: [CardObjectTags.UnitCard]),
                ["P2-SPELL-CARD"] = new("P2-SPELL-CARD", tags: ["CARD_TYPE:SPELL"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-covert-sabotage-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-COVERT-SABOTAGE", "OGN·156/298", ["P2-UNIT-CARD"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-COVERT-SABOTAGE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-CARD", "P2-SPELL-CARD"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBoneSkewerOpponentHandUnitStunned()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-bone-skewer-opponent-hand-unit-stunned.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        var differences=ConformanceFixtureRunner.CompareExpected(fixture,result);
        Assert.True(differences.Count==0,string.Join("\n",differences));
        Assert.Equal(["P2-BONE-SKEWER-HAND-SPELL"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(
            ["P2-BASE-UNIT-001"],
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal("BF",result.FinalState.ObjectLocations["P2-BONE-SKEWER-HAND-UNIT"].BattlefieldObjectId);
        Assert.Contains("STUNNED", result.FinalState.CardObjects["P2-BONE-SKEWER-HAND-UNIT"].UntilEndOfTurnEffects);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_PLAYED_TO_BATTLEFIELD", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBoneSkewerWhenOpponentHandTargetIsNotUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BONE-SKEWER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-BONE-SKEWER-HAND-SPELL"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BONE-SKEWER-HAND-SPELL"] = new(
                    "P2-BONE-SKEWER-HAND-SPELL",
                    tags: [CardObjectTags.SpellCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-bone-skewer-non-unit-hand-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BONE-SKEWER",
                "UNL-139/219",
                ["P2-BONE-SKEWER-HAND-SPELL"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BONE-SKEWER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BONE-SKEWER-HAND-SPELL"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBoneSkewerWhenOpponentHandTargetIsNotControlledByOpponent()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BONE-SKEWER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-DIRTY-P1-OWNED-HAND-UNIT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BONE-SKEWER"] = new(
                    "P1-SPELL-BONE-SKEWER",
                    cardNo: "UNL-139/219",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-DIRTY-P1-OWNED-HAND-UNIT"] = new(
                    "P2-DIRTY-P1-OWNED-HAND-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-bone-skewer-dirty-opponent-hand-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BONE-SKEWER",
                "UNL-139/219",
                ["P2-DIRTY-P1-OWNED-HAND-UNIT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BONE-SKEWER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-DIRTY-P1-OWNED-HAND-UNIT"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineBoneSkewerResolutionSkipsOpponentControlledHandZoneTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-BONE-SKEWER-DIRTY-HAND",
                    "P1",
                    "P1-SPELL-BONE-SKEWER",
                    "BONE_SKEWER_PLAY_OPPONENT_HAND_UNIT_STUNNED",
                    "UNL-139/219",
                    ["P2-DIRTY-P1-CONTROLLED-HAND-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-DIRTY-P1-CONTROLLED-HAND-UNIT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-DIRTY-P1-CONTROLLED-HAND-UNIT"] = new(
                    "P2-DIRTY-P1-CONTROLLED-HAND-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-bone-skewer-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-bone-skewer-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P2-DIRTY-P1-CONTROLLED-HAND-UNIT"], p2Pass.State.PlayerZones["P2"].Hand);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Base);
        Assert.Equal(["P1-SPELL-BONE-SKEWER"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHelpArrivesFriendlyHandUnitForFree()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-help-arrives-friendly-hand-unit-free.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-HELP-ARRIVES-HAND-SPELL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-HELP-FIELD", "P1-HELP-ARRIVES-HAND-UNIT"],
            result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(3, result.FinalState.CardObjects["P1-HELP-ARRIVES-HAND-UNIT"].ManaCost);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_PLAYED_TO_BATTLEFIELD", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHelpArrivesWhenHandUnitCostExceedsReductionWindow()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HELP-ARRIVES", "P1-HELP-ARRIVES-HIGH-COST-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HELP-ARRIVES-HIGH-COST-UNIT"] = new(
                    "P1-HELP-ARRIVES-HIGH-COST-UNIT",
                    manaCost: 4,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-help-arrives-high-cost-hand-unit", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-HELP-ARRIVES",
                "SFD·111/221",
                ["P1-HELP-ARRIVES-HIGH-COST-UNIT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            ["P1-SPELL-HELP-ARRIVES", "P1-HELP-ARRIVES-HIGH-COST-UNIT"],
            result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHelpArrivesWhenFriendlyHandTargetIsOpponentControlled()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand =
                    [
                        "P1-SPELL-HELP-ARRIVES",
                        "P1-DIRTY-OPPONENT-CONTROLLED-HAND-UNIT"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HELP-ARRIVES"] = new(
                    "P1-SPELL-HELP-ARRIVES",
                    cardNo: "SFD·111/221",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-DIRTY-OPPONENT-CONTROLLED-HAND-UNIT"] = new(
                    "P1-DIRTY-OPPONENT-CONTROLLED-HAND-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-help-arrives-opponent-controlled-friendly-hand-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-HELP-ARRIVES",
                "SFD·111/221",
                ["P1-DIRTY-OPPONENT-CONTROLLED-HAND-UNIT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            ["P1-SPELL-HELP-ARRIVES", "P1-DIRTY-OPPONENT-CONTROLLED-HAND-UNIT"],
            result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHostileTakeoverGainControlReadyBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hostile-takeover-gain-control-ready-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2-HOSTILE-TAKEOVER-TARGET"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P2-HOSTILE-TAKEOVER-TARGET"].IsExhausted);
        Assert.Equal("P2", result.FinalState.CardObjects["P2-HOSTILE-TAKEOVER-TARGET"].OwnerId);
        Assert.Equal("P1", result.FinalState.CardObjects["P2-HOSTILE-TAKEOVER-TARGET"].ControllerId);
        Assert.Contains(
            "RETURN_CONTROL_TO_OWNER_AT_TURN_END:P2",
            result.FinalState.CardObjects["P2-HOSTILE-TAKEOVER-TARGET"].UntilEndOfTurnEffects);
        Assert.Contains(
            "UNIT_CONTROL_GAINED",
            result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEngineHostileTakeoverResolutionSkipsAlreadyControlledEnemyZoneTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-HOSTILE-TAKEOVER-DIRTY-BATTLEFIELD",
                    "P1",
                    "P1-SPELL-HOSTILE-TAKEOVER",
                    "HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT",
                    "SFD·202/221",
                    ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"] = new(
                    "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-hostile-takeover-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hostile-takeover-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"], p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-SPELL-HOSTILE-TAKEOVER"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("P1", p2Pass.State.CardObjects["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"].ControllerId);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_CONTROL_GAINED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task P5HostileTakeoverReturnsControlAndRecallsAtEndTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p5-hostile-takeover-end-turn-return-recall.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Contains("P2-HOSTILE-TAKEOVER-TARGET", result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal("P2", result.FinalState.CardObjects["P2-HOSTILE-TAKEOVER-TARGET"].OwnerId);
        Assert.Equal("P2", result.FinalState.CardObjects["P2-HOSTILE-TAKEOVER-TARGET"].ControllerId);
        Assert.Empty(result.FinalState.CardObjects["P2-HOSTILE-TAKEOVER-TARGET"].UntilEndOfTurnEffects);
        Assert.Contains("UNIT_CONTROL_RETURNED", result.EventKinds);
        Assert.Contains("UNIT_RECALLED_TO_OWNER_BASE", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHostileTakeoverWhenTargetIsFriendlyBattlefieldUnit()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HOSTILE-TAKEOVER"],
                    Battlefields = ["P1-HOSTILE-TAKEOVER-FRIENDLY-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HOSTILE-TAKEOVER-FRIENDLY-UNIT"] = new(
                    "P1-HOSTILE-TAKEOVER-FRIENDLY-UNIT",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hostile-takeover-friendly-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-HOSTILE-TAKEOVER",
                "SFD·202/221",
                ["P1-HOSTILE-TAKEOVER-FRIENDLY-UNIT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HOSTILE-TAKEOVER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-HOSTILE-TAKEOVER-FRIENDLY-UNIT"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTimeTwistSchedulesExtraTurnAndBanishesSource()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-time-twist-extra-turn-banish-source.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal("P1", result.FinalState.ExtraTurnPlayerId);
        Assert.Equal(["P1-SPELL-TIME-TWIST"], result.FinalState.PlayerZones["P1"].Banished);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Contains("EXTRA_TURN_SCHEDULED", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEngineUsesTimeTwistExtraTurnOnEndTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-time-twist-extra-turn-banish-source.fixture.json"),
            CancellationToken.None);
        var played = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        var result = await new CoreRuleEngine().ResolveAsync(
            played.FinalState,
            new PlayerIntent("intent-time-twist-end-turn", "P1", "END_TURN"),
            new EndTurnCommand(),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(823, result.State.TurnNumber);
        Assert.Equal("P1", result.State.TurnPlayerId);
        Assert.Equal("P1", result.State.ActivePlayerId);
        Assert.Equal(MatchPhases.Main, result.State.Phase);
        Assert.Equal(TimingStates.NeutralOpen, result.State.TimingState);
        Assert.Null(result.State.ExtraTurnPlayerId);
        Assert.Equal(["P1-MAIN-TIME-TWIST-001"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-RUNE-TIME-TWIST-001", "P1-RUNE-TIME-TWIST-002"],
            result.State.PlayerZones["P1"].Base);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPredictiveOffensiveDrawOneRecycleOther()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-predictive-offensive-draw-one-recycle-other.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-PREDICTIVE-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-MAIN-KEEPER", "P1-PREDICTIVE-RECYCLE-001"],
            result.FinalState.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPredictiveOffensiveTargetOutsideTopTwo()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-TOP-001", "P1-TOP-002", "P1-NOT-VIEWED-001"],
                    Hand = ["P1-SPELL-PREDICTIVE-OFFENSIVE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-predictive-offensive-outside-top-two", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-PREDICTIVE-OFFENSIVE", "SFD·122/221", ["P1-NOT-VIEWED-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-PREDICTIVE-OFFENSIVE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-TOP-001", "P1-TOP-002", "P1-NOT-VIEWED-001"],
            result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPredictiveOffensiveWhenLookWindowCardIsNotControlledByDeckPlayer()
    {
        var state = PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-PREDICTIVE-TOP-CONTROLLED", "P1-PREDICTIVE-DIRTY-P2-OWNED"],
                    Hand = ["P1-SPELL-PREDICTIVE-OFFENSIVE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-PREDICTIVE-OFFENSIVE"] = new(
                    "P1-SPELL-PREDICTIVE-OFFENSIVE",
                    cardNo: "SFD·122/221",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-PREDICTIVE-TOP-CONTROLLED"] = new(
                    "P1-PREDICTIVE-TOP-CONTROLLED",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-PREDICTIVE-DIRTY-P2-OWNED"] = new(
                    "P1-PREDICTIVE-DIRTY-P2-OWNED",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-predictive-offensive-dirty-look-window", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-PREDICTIVE-OFFENSIVE",
                "SFD·122/221",
                ["P1-PREDICTIVE-TOP-CONTROLLED"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-PREDICTIVE-OFFENSIVE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-PREDICTIVE-TOP-CONTROLLED", "P1-PREDICTIVE-DIRTY-P2-OWNED"],
            result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCardTrickDrawOneRecycleRest()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-card-trick-draw-one-recycle-rest.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-CARD-TRICK-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-MAIN-KEEPER", "P1-CARD-TRICK-RECYCLE-A", "P1-CARD-TRICK-RECYCLE-B"],
            result.FinalState.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCardTrickTargetOutsideTopThree()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-TOP-001", "P1-TOP-002", "P1-TOP-003", "P1-NOT-VIEWED-001"],
                    Hand = ["P1-SPELL-CARD-TRICK"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-card-trick-outside-top-three", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-CARD-TRICK", "OGN·183/298", ["P1-NOT-VIEWED-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-CARD-TRICK"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-TOP-001", "P1-TOP-002", "P1-TOP-003", "P1-NOT-VIEWED-001"],
            result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDragonTigerDrawUnitRecycleRest()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dragon-tiger-draw-unit-recycle-rest.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAGON-TIGER-UNIT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-MAIN-KEEPER", "P1-DRAGON-TIGER-RECYCLE-B", "P1-DRAGON-TIGER-RECYCLE-A"],
            result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-DRAGON-TIGER-UNIT-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDragonTigerNoSelectionRecycleAll()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dragon-tiger-no-unit-selection-recycle-all.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-MAIN-KEEPER", "P1-DRAGON-TIGER-RECYCLE-B", "P1-DRAGON-TIGER-RECYCLE-C", "P1-DRAGON-TIGER-RECYCLE-A"],
            result.FinalState.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysReinforcementsNoSelectionRecycleTopFive()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-reinforcements-no-selection-recycle-top-five.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            [
                "P1-MAIN-KEEPER",
                "P1-REINFORCEMENTS-RECYCLE-E",
                "P1-REINFORCEMENTS-RECYCLE-D",
                "P1-REINFORCEMENTS-RECYCLE-A",
                "P1-REINFORCEMENTS-RECYCLE-C",
                "P1-REINFORCEMENTS-RECYCLE-B"
            ],
            result.FinalState.PlayerZones["P1"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDragonTigerNonUnitTopDeckTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-TOP-SPELL-001", "P1-TOP-UNIT-001", "P1-TOP-UNIT-002"],
                    Hand = ["P1-SPELL-DRAGON-TIGER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-TOP-SPELL-001"] = new("P1-TOP-SPELL-001", tags: ["CARD_TYPE:SPELL"]),
                ["P1-TOP-UNIT-001"] = new("P1-TOP-UNIT-001", tags: [CardObjectTags.UnitCard]),
                ["P1-TOP-UNIT-002"] = new("P1-TOP-UNIT-002", tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dragon-tiger-non-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-DRAGON-TIGER", "UNL-032/219", ["P1-TOP-SPELL-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-DRAGON-TIGER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-TOP-SPELL-001", "P1-TOP-UNIT-001", "P1-TOP-UNIT-002"],
            result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDragonTigerTargetOutsideTopThree()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-TOP-UNIT-001", "P1-TOP-UNIT-002", "P1-TOP-UNIT-003", "P1-NOT-VIEWED-UNIT-001"],
                    Hand = ["P1-SPELL-DRAGON-TIGER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-TOP-UNIT-001"] = new("P1-TOP-UNIT-001", tags: [CardObjectTags.UnitCard]),
                ["P1-TOP-UNIT-002"] = new("P1-TOP-UNIT-002", tags: [CardObjectTags.UnitCard]),
                ["P1-TOP-UNIT-003"] = new("P1-TOP-UNIT-003", tags: [CardObjectTags.UnitCard]),
                ["P1-NOT-VIEWED-UNIT-001"] = new("P1-NOT-VIEWED-UNIT-001", tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-dragon-tiger-outside-top-three", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-DRAGON-TIGER", "UNL-032/219", ["P1-NOT-VIEWED-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-SPELL-DRAGON-TIGER"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-TOP-UNIT-001", "P1-TOP-UNIT-002", "P1-TOP-UNIT-003", "P1-NOT-VIEWED-UNIT-001"],
            result.State.PlayerZones["P1"].MainDeck);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMeditationBaseDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-meditation-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-MAIN-DRAWN-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSalvageDrawWithoutDestroyingEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-salvage-draw-no-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Contains("CARD_DRAWN", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSalvageDestroyEquipmentThenDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-salvage-destroy-equipment-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-SALVAGE-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-SALVAGE-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-EQUIPMENT-SALVAGE-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSalvageAgainstUnitTarget()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-SALVAGE-DRAW-001"],
                    Hand = ["P1-SPELL-SALVAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-SALVAGE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-UNIT-SALVAGE-001"] = new(
                    "P2-BATTLEFIELD-UNIT-SALVAGE-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-salvage-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-SALVAGE", "OGN·224/298", ["P2-BATTLEFIELD-UNIT-SALVAGE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SALVAGE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SALVAGE-DRAW-001"], result.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-SALVAGE-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysKingOfTheHillDrawsBaseCardOnly()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-king-of-the-hill-draw-no-controlled-battlefields.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Single(
            result.Events,
            gameEvent => string.Equals(gameEvent.Kind, "CARD_DRAWN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMeditationExhaustFriendlyUnitExtraDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-meditation-exhaust-friendly-extra-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.True(result.FinalState.CardObjects["P1-UNIT-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMeditationWhenOptionalCostTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-MEDITATION"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-001"] = new("P2-UNIT-001", isExhausted: false)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-meditation-enemy-optional-cost-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-MEDITATION",
                "OGN·048/298",
                [],
                OptionalCosts: ["EXHAUST_FRIENDLY_UNIT:P2-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-MEDITATION"], result.State.PlayerZones["P1"].Hand);
        Assert.False(result.State.CardObjects["P2-UNIT-001"].IsExhausted);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSacrificeDestroyFriendlyPowerfulDrawCallRune()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sacrifice-destroy-friendly-powerful-draw-call-rune.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SACRIFICE-001", "P1-SPELL-SACRIFICE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.False(result.FinalState.CardObjects.ContainsKey("P1-UNIT-SACRIFICE-001"));
        Assert.Equal(["P1"], result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineReconcilesObjectLocationsForDestroyedAdditionalCost()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SACRIFICE"],
                    Battlefields = ["P1-UNIT-SACRIFICE-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SACRIFICE"] = new(
                    "P1-SPELL-SACRIFICE",
                    cardNo: "UNL-173/219",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-SACRIFICE-001"] = new(
                    "P1-UNIT-SACRIFICE-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SACRIFICE"] = new("P1", "HAND"),
                ["P1-UNIT-SACRIFICE-001"] = new("P1", "BATTLEFIELD", "P1-MAIN")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sacrifice-additional-cost-location", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SACRIFICE",
                "UNL-173/219",
                [],
                OptionalCosts: ["DESTROY_FRIENDLY_POWERFUL_UNIT:P1-UNIT-SACRIFICE-001"]),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(["P1-UNIT-SACRIFICE-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", result.State.ObjectLocations["P1-UNIT-SACRIFICE-001"].Zone);
        Assert.Equal("STACK", result.State.ObjectLocations["P1-SPELL-SACRIFICE"].Zone);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["reason"] as string, "ADDITIONAL_COST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSacrificeWithoutAdditionalCost()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SACRIFICE"],
                    Battlefields = ["P1-UNIT-SACRIFICE-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-SACRIFICE-001"] = new("P1-UNIT-SACRIFICE-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sacrifice-missing-additional-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-SACRIFICE", "UNL-173/219", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SACRIFICE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-SACRIFICE-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSacrificeWhenAdditionalCostUnitIsNotPowerful()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SACRIFICE"],
                    Battlefields = ["P1-UNIT-SACRIFICE-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-SACRIFICE-001"] = new("P1-UNIT-SACRIFICE-001", power: 4)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sacrifice-weak-additional-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SACRIFICE",
                "UNL-173/219",
                [],
                OptionalCosts: ["DESTROY_FRIENDLY_POWERFUL_UNIT:P1-UNIT-SACRIFICE-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SACRIFICE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-SACRIFICE-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-UNIT-SACRIFICE-001"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSoulStrangleDestroyFriendlyPowerBuffDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-soul-strangle-destroy-friendly-power-buff-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SOUL-002"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P1-UNIT-SOUL-001", "P1-SPELL-SOUL-STRANGLE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(6, result.FinalState.CardObjects["P1-UNIT-SOUL-002"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-SOUL-002"].UntilEndOfTurnPowerModifier);
        Assert.False(result.FinalState.CardObjects.ContainsKey("P1-UNIT-SOUL-001"));
        Assert.Equal(["P1"], result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSoulStrangleWhenSecondTargetIsEnemy()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SOUL-STRANGLE"],
                    Battlefields = ["P1-UNIT-SOUL-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-SOUL-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-SOUL-001"] = new("P1-UNIT-SOUL-001", power: 4),
                ["P2-UNIT-SOUL-001"] = new("P2-UNIT-SOUL-001", power: 2)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-soul-strangle-enemy-second-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SOUL-STRANGLE",
                "SFD·163/221",
                ["P1-UNIT-SOUL-001", "P2-UNIT-SOUL-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SOUL-STRANGLE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-SOUL-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-UNIT-SOUL-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(4, result.State.CardObjects["P1-UNIT-SOUL-001"].Power);
        Assert.Equal(2, result.State.CardObjects["P2-UNIT-SOUL-001"].Power);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCleaveOverwhelmAttackingPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-cleave-overwhelm-attacking-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["OVERWHELM_3"], result.FinalState.CardObjects["P2-UNIT-CLEAVE-001"].UntilEndOfTurnEffects);
        Assert.Equal(5, result.FinalState.CardObjects["P2-UNIT-CLEAVE-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P2-UNIT-CLEAVE-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEngineCleaveDoesNotModifyPowerWhenTargetIsNotAttacking()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CLEAVE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-CLEAVE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-CLEAVE-001"] = new(
                    "P2-UNIT-CLEAVE-001",
                    power: 2,
                    isAttacking: false,
                    tags: [CardObjectTags.UnitCard])
            }
        };
        var engine = new CoreRuleEngine();

        var playResult = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-cleave-non-attacking-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-CLEAVE",
                "OGN·004/298",
                ["P2-UNIT-CLEAVE-001"]),
            CancellationToken.None);
        var p1PassResult = await engine.ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-cleave-non-attacking-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2PassResult = await engine.ResolveAsync(
            p1PassResult.State,
            new PlayerIntent("intent-cleave-non-attacking-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        Assert.True(p1PassResult.Accepted);
        Assert.True(p2PassResult.Accepted);
        Assert.Contains(p2PassResult.Events, gameEvent => gameEvent.Kind == "STATUS_EFFECT_APPLIED");
        Assert.DoesNotContain(p2PassResult.Events, gameEvent => gameEvent.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.Equal(["OVERWHELM_3"], p2PassResult.State.CardObjects["P2-UNIT-CLEAVE-001"].UntilEndOfTurnEffects);
        Assert.Equal(2, p2PassResult.State.CardObjects["P2-UNIT-CLEAVE-001"].Power);
        Assert.Equal(0, p2PassResult.State.CardObjects["P2-UNIT-CLEAVE-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBloodRushEchoOverwhelmAttackingPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blood-rush-echo-overwhelm-attacking-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["OVERWHELM_2"], result.FinalState.CardObjects["P2-UNIT-BLOOD-RUSH-001"].UntilEndOfTurnEffects);
        Assert.Equal(6, result.FinalState.CardObjects["P2-UNIT-BLOOD-RUSH-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P2-UNIT-BLOOD-RUSH-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPowerPunchOverwhelmRoamAttackingPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-power-punch-overwhelm-roam-attacking-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["OVERWHELM_2", "ROAM"], result.FinalState.CardObjects["P2-UNIT-POWER-PUNCH-001"].UntilEndOfTurnEffects);
        Assert.Equal(4, result.FinalState.CardObjects["P2-UNIT-POWER-PUNCH-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-POWER-PUNCH-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePowerPunchDoesNotModifyPowerWhenTargetIsNotAttacking()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-POWER-PUNCH"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-POWER-PUNCH-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-POWER-PUNCH-001"] = new(
                    "P2-UNIT-POWER-PUNCH-001",
                    power: 2,
                    isAttacking: false)
            }
        };
        state = PrintedCostFixture.Add(state, "P1", "UNL-010/219");
        var engine = new CoreRuleEngine();

        var playResult = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-power-punch-non-attacking-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-POWER-PUNCH",
                "UNL-010/219",
                ["P2-UNIT-POWER-PUNCH-001"]),
            CancellationToken.None);
        var p1PassResult = await engine.ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-power-punch-non-attacking-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2PassResult = await engine.ResolveAsync(
            p1PassResult.State,
            new PlayerIntent("intent-power-punch-non-attacking-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        Assert.True(p1PassResult.Accepted);
        Assert.True(p2PassResult.Accepted);
        Assert.Equal(2, p2PassResult.Events.Count(gameEvent => gameEvent.Kind == "STATUS_EFFECT_APPLIED"));
        Assert.DoesNotContain(p2PassResult.Events, gameEvent => gameEvent.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.Equal(["OVERWHELM_2", "ROAM"], p2PassResult.State.CardObjects["P2-UNIT-POWER-PUNCH-001"].UntilEndOfTurnEffects);
        Assert.Equal(2, p2PassResult.State.CardObjects["P2-UNIT-POWER-PUNCH-001"].Power);
        Assert.Equal(0, p2PassResult.State.CardObjects["P2-UNIT-POWER-PUNCH-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysParrySteadfastBarrierDefendingPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-parry-steadfast-barrier-defending-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["COMBAT_KEYWORD:坚守3:1", "COMBAT_KEYWORD:壁垒:1"], result.FinalState.CardObjects["P2-UNIT-PARRY-001"].UntilEndOfTurnEffects);
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-PARRY-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-PARRY-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEngineParryDoesNotModifyPowerWhenTargetIsNotDefending()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PARRY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-PARRY-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-PARRY-001"] = new(
                    "P2-UNIT-PARRY-001",
                    power: 2,
                    isDefending: false)
            }
        };
        var engine = new CoreRuleEngine();

        var playResult = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-parry-non-defending-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-PARRY",
                "OGN·057/298",
                ["P2-UNIT-PARRY-001"]),
            CancellationToken.None);
        var p1PassResult = await engine.ResolveAsync(
            playResult.State,
            new PlayerIntent("intent-parry-non-defending-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2PassResult = await engine.ResolveAsync(
            p1PassResult.State,
            new PlayerIntent("intent-parry-non-defending-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(playResult.Accepted);
        Assert.True(p1PassResult.Accepted);
        Assert.True(p2PassResult.Accepted);
        Assert.Equal(2, p2PassResult.Events.Count(gameEvent => gameEvent.Kind == "STATUS_EFFECT_APPLIED"));
        Assert.DoesNotContain(p2PassResult.Events, gameEvent => gameEvent.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        Assert.Equal(["COMBAT_KEYWORD:坚守3:1", "COMBAT_KEYWORD:壁垒:1"], p2PassResult.State.CardObjects["P2-UNIT-PARRY-001"].UntilEndOfTurnEffects);
        Assert.Equal(2, p2PassResult.State.CardObjects["P2-UNIT-PARRY-001"].Power);
        Assert.Equal(0, p2PassResult.State.CardObjects["P2-UNIT-PARRY-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRoaringReckoningDiscardEchoOverwhelmAttackingPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-roaring-reckoning-discard-echo-overwhelm-attacking-power.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["OVERWHELM_4"], result.FinalState.CardObjects["P2-UNIT-ROARING-RECKONING-001"].UntilEndOfTurnEffects);
        Assert.Equal(10, result.FinalState.CardObjects["P2-UNIT-ROARING-RECKONING-001"].Power);
        Assert.Equal(8, result.FinalState.CardObjects["P2-UNIT-ROARING-RECKONING-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(["P1-DISCARD-001", "P1-SPELL-ROARING-RECKONING"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsRoaringReckoningWhenDiscardOptionalCostTargetsSource()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ROARING-RECKONING"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-ROARING-RECKONING-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-ROARING-RECKONING-001"] = new(
                    "P2-UNIT-ROARING-RECKONING-001",
                    power: 2,
                    isAttacking: true)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-roaring-reckoning-discard-source", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-ROARING-RECKONING",
                "UNL-017/219",
                ["P2-UNIT-ROARING-RECKONING-001"],
                OptionalCosts: ["DISCARD_HAND_CARD:P1-SPELL-ROARING-RECKONING"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(["P1-SPELL-ROARING-RECKONING"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMoonsilverGiftDiscardDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-moonsilver-gift-discard-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-DISCARD-001", "P1-SPELL-MOONSILVER-GIFT"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMoonsilverGiftWhenDiscardTargetIsSource()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-DRAW-001", "P1-DRAW-002"],
                    Hand = ["P1-SPELL-MOONSILVER-GIFT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-moonsilver-gift-source-discard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-MOONSILVER-GIFT",
                "UNL-125/219",
                ["P1-SPELL-MOONSILVER-GIFT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-MOONSILVER-GIFT"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSinfulPleasureDiscardDamageByManaCost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sinful-pleasure-discard-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DISCARD-SINFUL-001", "P1-SPELL-SINFUL-PLEASURE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(4, result.FinalState.CardObjects["P2-BATTLEFIELD-SINFUL-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P1-DISCARD-SINFUL-001"].ManaCost);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSinfulPleasureWhenDiscardTargetIsOpponentHand()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SINFUL-PLEASURE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-DISCARD-SINFUL-001"],
                    Battlefields = ["P2-BATTLEFIELD-SINFUL-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-DISCARD-SINFUL-001"] = new("P2-DISCARD-SINFUL-001", manaCost: 4),
                ["P2-BATTLEFIELD-SINFUL-001"] = new("P2-BATTLEFIELD-SINFUL-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sinful-pleasure-opponent-hand-discard", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SINFUL-PLEASURE",
                "OGN·008/298",
                ["P2-DISCARD-SINFUL-001", "P2-BATTLEFIELD-SINFUL-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SINFUL-PLEASURE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-DISCARD-SINFUL-001"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(0, result.State.CardObjects["P2-BATTLEFIELD-SINFUL-001"].Damage);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRewindTimelineDiscardHandsDrawFour()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rewind-timeline-discard-hands-draw-four.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002", "P1-DRAW-003", "P1-DRAW-004"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-DRAW-001", "P2-DRAW-002", "P2-DRAW-003", "P2-DRAW-004"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P1-HAND-001", "P1-HAND-002", "P1-SPELL-REWIND-TIMELINE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-HAND-001"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRewindTimelineDiscardsOnlyControlledHandCards()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rewind-timeline-discard-hands-draw-four.fixture.json"),
            CancellationToken.None);
        var initial = fixture.InitialState!;
        var players = initial.Players!.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        players["P1"] = players["P1"] with
        {
            Hand = ["P1-SPELL-REWIND-TIMELINE", "P1-HAND-DIRTY-P2-CARD", "P1-HAND-001", "P1-HAND-002"]
        };
        players["P2"] = players["P2"] with
        {
            Hand = ["P2-HAND-DIRTY-P1-CARD", "P2-HAND-001"]
        };
        var cardObjects = (initial.CardObjects ?? new Dictionary<string, ConformanceCardObjectState>(StringComparer.Ordinal))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-HAND-DIRTY-P2-CARD"] = new ConformanceCardObjectState
        {
            OwnerId = "P2",
            ControllerId = "P2"
        };
        cardObjects["P2-HAND-DIRTY-P1-CARD"] = new ConformanceCardObjectState
        {
            OwnerId = "P1",
            ControllerId = "P1"
        };
        var dirtyFixture = fixture with
        {
            InitialState = initial with
            {
                Players = players,
                CardObjects = cardObjects
            }
        };

        var result = await ConformanceFixtureRunner.RunAsync(
            dirtyFixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Equal(
            ["P1-HAND-DIRTY-P2-CARD", "P1-DRAW-001", "P1-DRAW-002", "P1-DRAW-003", "P1-DRAW-004"],
            result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P2-HAND-DIRTY-P1-CARD", "P2-DRAW-001", "P2-DRAW-002", "P2-DRAW-003", "P2-DRAW-004"],
            result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P1-HAND-001", "P1-HAND-002", "P1-SPELL-REWIND-TIMELINE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-HAND-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        var discardEvents = result.Events
            .Where(gameEvent => string.Equals(gameEvent.Kind, "CARDS_DISCARDED", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, discardEvents.Length);
        Assert.Equal(["P1-HAND-001", "P1-HAND-002"], Assert.IsAssignableFrom<IReadOnlyList<string>>(discardEvents[0].Payload["objectIds"]));
        Assert.Equal(["P2-HAND-001"], Assert.IsAssignableFrom<IReadOnlyList<string>>(discardEvents[1].Payload["objectIds"]));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysReviveReturnGraveyardUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-revive-return-graveyard-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-OTHER-001", "P1-SPELL-REVIVE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsReviveWhenTargetIsOpponentGraveyard()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-REVIVE"],
                    Graveyard = ["P1-GRAVE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Graveyard = ["P2-GRAVE-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-revive-opponent-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-REVIVE",
                "OGN·170/298",
                ["P2-GRAVE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-REVIVE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-GRAVE-UNIT-001"], result.State.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineReviveResolutionSkipsOpponentControlledOwnGraveyardTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-REVIVE-DIRTY-GRAVEYARD",
                    "P1",
                    "P1-SPELL-REVIVE",
                    "REVIVE_FROM_GRAVEYARD_TO_HAND",
                    "OGN·170/298",
                    ["P1-GRAVE-DIRTY-P2-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Graveyard = ["P1-GRAVE-UNIT-001", "P1-GRAVE-DIRTY-P2-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-UNIT-001"] = new(
                    "P1-GRAVE-UNIT-001",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GRAVE-DIRTY-P2-UNIT"] = new(
                    "P1-GRAVE-DIRTY-P2-UNIT",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-revive-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-revive-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-UNIT-001", "P1-GRAVE-DIRTY-P2-UNIT", "P1-SPELL-REVIVE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_RETURNED_TO_HAND", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHarrowingPlayGraveyardUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-harrowing-play-graveyard-unit-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-HARROWING-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-OTHER-001", "P1-SPELL-HARROWING"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(4, result.FinalState.CardObjects["P1-HARROWING-GRAVE-UNIT-001"].Power);
        Assert.True(result.FinalState.CardObjects["P1-HARROWING-GRAVE-UNIT-001"].IsExhausted); // CN 143.4.
    }

    [Fact]
    public async Task CoreRuleEnginePlaysGhostMatronOptionalGraveyardUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-ghost-matron-graveyard-unit-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        var differences = ConformanceFixtureRunner.CompareExpected(fixture, result);
        Assert.True(differences.Count == 0, string.Join("\n", differences));
        Assert.Equal(["P1-UNIT-GHOST-MATRON", "P1-GHOST-MATRON-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-OTHER-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(4, result.FinalState.CardObjects["P1-UNIT-GHOST-MATRON"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P1-GHOST-MATRON-GRAVE-UNIT-001"].ManaCost);
        Assert.True(result.FinalState.CardObjects["P1-GHOST-MATRON-GRAVE-UNIT-001"].IsExhausted); // CN 143.4.
        Assert.Equal("BASE", result.FinalState.ObjectLocations["P1-GHOST-MATRON-GRAVE-UNIT-001"].Zone);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTidecallerOptionalLocationSwap()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-tidecaller-swap-target-location.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-TIDE-CALLER-SWAP-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-UNIT-TIDE-CALLER"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal("BATTLEFIELD", result.FinalState.ObjectLocations["P1-UNIT-TIDE-CALLER"].Zone);
        Assert.Equal("BASE", result.FinalState.ObjectLocations["P1-TIDE-CALLER-SWAP-TARGET-001"].Zone);
    }

    [Theory]
    [InlineData(4, 4)]
    public async Task CoreRuleEngineRejectsGhostMatronGraveyardTargetAboveCostOrPowerLimit(
        int targetManaCost,
        int controllerPower)
    {
        var state = PunishmentState(mana: 4) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(4, controllerPower),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-GHOST-MATRON"],
                    Graveyard = ["P1-GHOST-MATRON-GRAVE-UNIT-LIMIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-GHOST-MATRON"] = new(
                    "P1-UNIT-GHOST-MATRON",
                    cardNo: "OGN·226/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GHOST-MATRON-GRAVE-UNIT-LIMIT"] = new(
                    "P1-GHOST-MATRON-GRAVE-UNIT-LIMIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    manaCost: targetManaCost,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-ghost-matron-graveyard-target-limit", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-GHOST-MATRON",
                "OGN·226/298",
                ["P1-GHOST-MATRON-GRAVE-UNIT-LIMIT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(["P1-UNIT-GHOST-MATRON"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GHOST-MATRON-GRAVE-UNIT-LIMIT"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Equal(new RunePool(4, controllerPower), result.State.RunePools["P1"]);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHarrowingWhenTargetIsNonUnitGraveyardCard()
    {
        var state = PunishmentState(mana: 6) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-HARROWING"],
                    Graveyard = ["P1-GRAVE-EQUIPMENT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-EQUIPMENT-001"] = new(
                    "P1-GRAVE-EQUIPMENT-001",
                    power: 0,
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-harrowing-non-unit-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-HARROWING",
                "OGN·198/298",
                ["P1-GRAVE-EQUIPMENT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(6, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-HARROWING"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-EQUIPMENT-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineHarrowingResolutionSkipsOpponentControlledOwnGraveyardTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-HARROWING-DIRTY-GRAVEYARD",
                    "P1",
                    "P1-SPELL-HARROWING",
                    "HARROWING_PLAY_GRAVEYARD_UNIT_TO_BASE",
                    "OGN·198/298",
                    ["P1-GRAVE-DIRTY-P2-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Graveyard = ["P1-GRAVE-UNIT-001", "P1-GRAVE-DIRTY-P2-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-UNIT-001"] = new(
                    "P1-GRAVE-UNIT-001",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GRAVE-DIRTY-P2-UNIT"] = new(
                    "P1-GRAVE-DIRTY-P2-UNIT",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-harrowing-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-harrowing-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-UNIT-001", "P1-GRAVE-DIRTY-P2-UNIT", "P1-SPELL-HARROWING"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSteadfastLoyaltyPlayLowCostGraveyardUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-steadfast-loyalty-graveyard-unit-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        var differences = ConformanceFixtureRunner.CompareExpected(fixture, result);
        Assert.True(differences.Count == 0, string.Join("\n", differences));
        Assert.Equal(["P1-STEADFAST-LOYALTY-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-GRAVE-OTHER-001", "P1-SPELL-STEADFAST-LOYALTY"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, result.FinalState.CardObjects["P1-STEADFAST-LOYALTY-GRAVE-UNIT-001"].ManaCost);
        Assert.True(result.FinalState.CardObjects["P1-STEADFAST-LOYALTY-GRAVE-UNIT-001"].IsExhausted); // CN 143.4.
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCruelRevivalDestroyFriendlyUnitPlayGraveyardUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-cruel-revival-destroy-unit-play-graveyard-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        var differences = ConformanceFixtureRunner.CompareExpected(fixture, result);
        Assert.True(differences.Count == 0, string.Join("\n", differences));
        Assert.Equal(["P1-CRUEL-REVIVAL-GRAVE-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P1-CRUEL-REVIVAL-COST-UNIT-001", "P1-SPELL-CRUEL-REVIVAL", "P1-SPELL-INCINERATE"],
            result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, result.FinalState.CardObjects["P1-CRUEL-REVIVAL-GRAVE-UNIT-001"].ManaCost);
        Assert.True(result.FinalState.CardObjects["P1-CRUEL-REVIVAL-GRAVE-UNIT-001"].IsExhausted); // CN 143.4.
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCruelRevivalWithoutDestroyAdditionalCost()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CRUEL-REVIVAL"],
                    Graveyard = ["P1-CRUEL-REVIVAL-GRAVE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CRUEL-REVIVAL-GRAVE-UNIT-001"] = new(
                    "P1-CRUEL-REVIVAL-GRAVE-UNIT-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 2)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-cruel-revival-missing-additional-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-CRUEL-REVIVAL",
                "UNL-142/219",
                ["P1-CRUEL-REVIVAL-GRAVE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-CRUEL-REVIVAL"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-CRUEL-REVIVAL-GRAVE-UNIT-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCruelRevivalWhenGraveyardUnitCostsMoreThanDestroyedUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CRUEL-REVIVAL"],
                    Base = ["P1-CRUEL-REVIVAL-COST-UNIT-001"],
                    Graveyard = ["P1-CRUEL-REVIVAL-GRAVE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CRUEL-REVIVAL-COST-UNIT-001"] = new(
                    "P1-CRUEL-REVIVAL-COST-UNIT-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 2),
                ["P1-CRUEL-REVIVAL-GRAVE-UNIT-001"] = new(
                    "P1-CRUEL-REVIVAL-GRAVE-UNIT-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-cruel-revival-expensive-graveyard-unit", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-CRUEL-REVIVAL",
                "UNL-142/219",
                ["P1-CRUEL-REVIVAL-GRAVE-UNIT-001"],
                OptionalCosts: ["DESTROY_FRIENDLY_UNIT:P1-CRUEL-REVIVAL-COST-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-CRUEL-REVIVAL"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-CRUEL-REVIVAL-COST-UNIT-001"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-CRUEL-REVIVAL-GRAVE-UNIT-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSteadfastLoyaltyWhenTargetIsNonUnitGraveyardCard()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-STEADFAST-LOYALTY"],
                    Graveyard = ["P1-GRAVE-EQUIPMENT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-EQUIPMENT-001"] = new(
                    "P1-GRAVE-EQUIPMENT-001",
                    power: 0,
                    tags: [CardObjectTags.EquipmentCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-steadfast-loyalty-non-unit-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-STEADFAST-LOYALTY",
                "UNL-168/219",
                ["P1-GRAVE-EQUIPMENT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-STEADFAST-LOYALTY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-EQUIPMENT-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSteadfastLoyaltyWhenTargetCostsTooMuch()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-STEADFAST-LOYALTY"],
                    Graveyard = ["P1-GRAVE-UNIT-EXPENSIVE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-UNIT-EXPENSIVE"] = new(
                    "P1-GRAVE-UNIT-EXPENSIVE",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-steadfast-loyalty-expensive-graveyard-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-STEADFAST-LOYALTY",
                "UNL-168/219",
                ["P1-GRAVE-UNIT-EXPENSIVE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-STEADFAST-LOYALTY"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-UNIT-EXPENSIVE"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysGuerrillaWarfareReturnStandbyGraveyardCards()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-guerrilla-warfare-return-standby-graveyard.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-GRAVE-STANDBY-001", "P1-GRAVE-STANDBY-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-NON-STANDBY-001", "P1-SPELL-GUERRILLA-WARFARE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.Standby], result.FinalState.CardObjects["P1-GRAVE-STANDBY-001"].Tags);
        Assert.Equal([CardObjectTags.Standby], result.FinalState.CardObjects["P1-GRAVE-STANDBY-002"].Tags);
        Assert.Equal(["FREE_STANDBY_HIDE:P1", "PLAYED_SPELL_THIS_TURN:P1"], result.FinalState.UntilEndOfTurnEffects);
        Assert.Single(result.Events, evt => string.Equals(evt.Kind, "STANDBY_HIDE_PERMISSION_GRANTED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsGuerrillaWarfareWhenTargetIsNotStandby()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-GUERRILLA-WARFARE"],
                    Graveyard = ["P1-GRAVE-NON-STANDBY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GRAVE-NON-STANDBY-001"] = new("P1-GRAVE-NON-STANDBY-001", tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-guerrilla-warfare-non-standby-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-GUERRILLA-WARFARE",
                "OGN·264/298",
                ["P1-GRAVE-NON-STANDBY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-GUERRILLA-WARFARE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-NON-STANDBY-001"], result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task P4GuerrillaWarfareNonStandbyTargetRejectionFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-guerrilla-warfare-non-standby-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-GUERRILLA-WARFARE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-GRAVE-NON-STANDBY-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCallOfTheShadowsGiveEphemeralDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-call-of-the-shadows-give-ephemeral-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-SPELL-CALL-OF-THE-SHADOWS"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal([CardObjectTags.Ephemeral], result.FinalState.CardObjects["P1-UNIT-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCallOfTheShadowsWhenTargetAlreadyHasEphemeral()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CALL-OF-THE-SHADOWS"],
                    Base = ["P1-UNIT-EPHEMERAL"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-EPHEMERAL"] = new("P1-UNIT-EPHEMERAL", tags: [CardObjectTags.Ephemeral])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-call-of-the-shadows-ephemeral-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-CALL-OF-THE-SHADOWS",
                "UNL-165/219",
                ["P1-UNIT-EPHEMERAL"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-CALL-OF-THE-SHADOWS"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-EPHEMERAL"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFlowingTimeMirrorBattlefieldUnitEphemeral()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-flowing-time-mirror-battlefield-unit-ephemeral.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal([CardObjectTags.Ephemeral], result.FinalState.CardObjects["P2-BATTLEFIELD-FLOWING-TIME-MIRROR-001"].Tags);
        Assert.Equal(["P1-SPELL-FLOWING-TIME-MIRROR"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFlowingTimeMirrorEquipmentEphemeral()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-flowing-time-mirror-equipment-ephemeral.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [CardObjectTags.EquipmentCard, CardObjectTags.Ephemeral],
            result.FinalState.CardObjects["P2-BASE-EQUIPMENT-FLOWING-TIME-MIRROR-001"].Tags);
        Assert.Equal(["P1-SPELL-FLOWING-TIME-MIRROR"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFlowingTimeMirrorAgainstBaseUnit()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FLOWING-TIME-MIRROR"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-FLOWING-TIME-MIRROR-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-FLOWING-TIME-MIRROR-001"] = new("P2-BASE-FLOWING-TIME-MIRROR-001", power: 4)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-flowing-time-mirror-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-FLOWING-TIME-MIRROR",
                "OGN·180/298",
                ["P2-BASE-FLOWING-TIME-MIRROR-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-FLOWING-TIME-MIRROR"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-FLOWING-TIME-MIRROR-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysAshesToAshesEquipmentEphemeral()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-ashes-to-ashes-equipment-ephemeral.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [CardObjectTags.EquipmentCard, CardObjectTags.Ephemeral],
            result.FinalState.CardObjects["P2-BASE-EQUIPMENT-ASHES-TO-ASHES-001"].Tags);
        Assert.Equal(["P1-SPELL-ASHES-TO-ASHES"], result.FinalState.PlayerZones["P1"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAshesToAshesAgainstUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-ASHES-TO-ASHES"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-ASHES-TO-ASHES-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-ASHES-TO-ASHES-UNIT-001"] = new("P2-BASE-ASHES-TO-ASHES-UNIT-001", power: 2, tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-ashes-to-ashes-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-ASHES-TO-ASHES",
                "UNL-070/219",
                ["P2-BASE-ASHES-TO-ASHES-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-ASHES-TO-ASHES"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-ASHES-TO-ASHES-UNIT-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSigilBurstDestroyEquipmentAndTargetControllerDraws()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sigil-burst-destroy-equipment-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-SIGIL-BURST-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-DRAW-SIGIL-BURST-001", "P2-DRAW-SIGIL-BURST-002"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-BASE-EQUIPMENT-SIGIL-BURST-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSigilBurstAgainstUnit()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SIGIL-BURST"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-BASE-SIGIL-BURST-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BASE-SIGIL-BURST-UNIT-001"] = new("P2-BASE-SIGIL-BURST-UNIT-001", power: 2, tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-sigil-burst-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-SIGIL-BURST",
                "SFD·005/221",
                ["P2-BASE-SIGIL-BURST-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-SIGIL-BURST"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BASE-SIGIL-BURST-UNIT-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBackAgainstWallDoublesFriendlyPowerAndEphemeral()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-back-against-wall-double-power-ephemeral.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(6, result.FinalState.CardObjects["P1-BATTLEFIELD-BACK-AGAINST-WALL-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-BATTLEFIELD-BACK-AGAINST-WALL-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal([CardObjectTags.Ephemeral], result.FinalState.CardObjects["P1-BATTLEFIELD-BACK-AGAINST-WALL-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBackAgainstWallAgainstEnemyUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BACK-AGAINST-WALL"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-BACK-AGAINST-WALL-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-BACK-AGAINST-WALL-001"] = new("P2-BATTLEFIELD-BACK-AGAINST-WALL-001", power: 3)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-back-against-wall-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BACK-AGAINST-WALL",
                "OGN·069/298",
                ["P2-BATTLEFIELD-BACK-AGAINST-WALL-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BACK-AGAINST-WALL"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-BATTLEFIELD-BACK-AGAINST-WALL-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBorrowedHistoryDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-borrowed-history-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
    }
}
