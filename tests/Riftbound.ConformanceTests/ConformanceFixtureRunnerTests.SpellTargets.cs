using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Fact]
    public async Task CoreRuleEnginePlaysQuickCounterEnemySpellTargetingFriendlyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-quick-counter-enemy-spell-targeting-friendly-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-QUICK-COUNTER-001"].Damage);
        Assert.Equal(["P1-SPELL-INCINERATE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-SPELL-QUICK-COUNTER"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRepelCounterEnemySpellTargetingChosenFriendlyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-repel-counter-enemy-spell-targeting-chosen-friendly-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-REPEL-001"].Damage);
        Assert.Equal(["P1-SPELL-INCINERATE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-SPELL-REPEL"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysLaurentDuelistCounterSpellPowerBySpellCost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-laurent-duelist-counter-spell-power-by-cost.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-LAURENT-DUELIST-001"].Damage);
        Assert.Equal(5, result.FinalState.CardObjects["P2-UNIT-LAURENT-DUELIST-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-LAURENT-DUELIST-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(["P1-SPELL-INCINERATE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-SPELL-LAURENT-DUELIST"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTakeUpAttachWeaponDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-take-up-attach-weapon-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal("P2-UNIT-TAKE-UP-001", result.FinalState.CardObjects["P2-EQUIPMENT-TAKE-UP-WEAPON"].AttachedToObjectId);
        Assert.Equal("P2", result.FinalState.CardObjects["P2-EQUIPMENT-TAKE-UP-WEAPON"].OwnerId);
        Assert.Equal("P2", result.FinalState.CardObjects["P2-EQUIPMENT-TAKE-UP-WEAPON"].ControllerId);
        Assert.Equal(["P2-DRAW-TAKE-UP-001"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-SPELL-TAKE-UP"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTakeUpDetachWeaponDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-take-up-detach-weapon-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Null(result.FinalState.CardObjects["P2-EQUIPMENT-TAKE-UP-WEAPON"].AttachedToObjectId);
        Assert.Equal("P2", result.FinalState.CardObjects["P2-EQUIPMENT-TAKE-UP-WEAPON"].OwnerId);
        Assert.Equal("P2", result.FinalState.CardObjects["P2-EQUIPMENT-TAKE-UP-WEAPON"].ControllerId);
        Assert.Equal(["P2-DRAW-TAKE-UP-DETACH-001"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-SPELL-TAKE-UP-DETACH"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsTakeUpInvalidWeaponTargets()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-TAKE-UP"],
                    Base =
                    [
                        "P1-UNIT-TAKE-UP-001",
                        "P1-UNIT-TAKE-UP-002",
                        "P1-EQUIPMENT-NON-WEAPON",
                        "P1-EQUIPMENT-WEAPON-ATTACHED-OTHER"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-EQUIPMENT-WEAPON"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-TAKE-UP-001"] = new("P1-UNIT-TAKE-UP-001", tags: [CardObjectTags.UnitCard]),
                ["P1-UNIT-TAKE-UP-002"] = new("P1-UNIT-TAKE-UP-002", tags: [CardObjectTags.UnitCard]),
                ["P1-EQUIPMENT-NON-WEAPON"] = new("P1-EQUIPMENT-NON-WEAPON", tags: [CardObjectTags.EquipmentCard]),
                ["P1-EQUIPMENT-WEAPON-ATTACHED-OTHER"] = new(
                    "P1-EQUIPMENT-WEAPON-ATTACHED-OTHER",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    attachedToObjectId: "P1-UNIT-TAKE-UP-002"),
                ["P2-EQUIPMENT-WEAPON"] = new("P2-EQUIPMENT-WEAPON", tags: [CardObjectTags.EquipmentCard, "武装"])
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P1-UNIT-TAKE-UP-001", "P1-EQUIPMENT-NON-WEAPON"],
            ["P1-UNIT-TAKE-UP-001", "P2-EQUIPMENT-WEAPON"],
            ["P1-UNIT-TAKE-UP-001", "P1-EQUIPMENT-WEAPON-ATTACHED-OTHER"],
            ["P1-EQUIPMENT-NON-WEAPON", "P1-EQUIPMENT-WEAPON-ATTACHED-OTHER"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-take-up-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-TAKE-UP",
                    "SFD·011/221",
                    targetObjectIds),
                CancellationToken.None);

            // DOC-04 434.1.f: this former rejection is a legal reattachment.
            // Keep the original pair and verify its actual detach/attach result.
            if (targetObjectIds[0] == "P1-UNIT-TAKE-UP-001" && targetObjectIds[1] == "P1-EQUIPMENT-WEAPON-ATTACHED-OTHER")
            {
                Assert.True(result.Accepted, result.ErrorMessage);
                var resolved = await OfficialGraveyardRecastTests.Top(result.State);
                Assert.Equal("P1-UNIT-TAKE-UP-001", resolved.State.CardObjects[targetObjectIds[1]].AttachedToObjectId);
                Assert.Single(resolved.Events, e => e.Kind == "EQUIPMENT_DETACHED");
                Assert.Single(resolved.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
                continue;
            }
            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Empty(result.State.StackItems);
            Assert.Equal(["P1-SPELL-TAKE-UP"], result.State.PlayerZones["P1"].Hand);
        }
    }

    [Fact]
    public async Task CoreRuleEnginePlaysReversalGainStackSpellControl()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-reversal-gain-stack-spell-control.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        var remainingStackItem = Assert.Single(result.FinalState.StackItems);
        Assert.Equal("STACK-1-P1-SPELL-INCINERATE", remainingStackItem.StackItemId);
        Assert.Equal("P2", remainingStackItem.ControllerId);
        Assert.Equal("P2", result.FinalState.PriorityPlayerId);
        Assert.Equal(["P2-SPELL-REVERSAL"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsReversalAgainstUnitStackItem()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(4, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-REVERSAL"]
                }
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-UNIT-SCUTTLE",
                    "P1",
                    "P1-UNIT-SCUTTLE",
                    "SCUTTLE_CRAB_PLAY_UNIT_DRAW_1",
                    "UNL-053/219",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-reversal-unit-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-REVERSAL",
                "OGN·080/298",
                ["STACK-1-P1-UNIT-SCUTTLE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-REVERSAL"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-UNIT-SCUTTLE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBerserkImpulseOpponentTopUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-berserk-impulse-play-opponent-top-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        var differences = ConformanceFixtureRunner.CompareExpected(fixture, result);
        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
        Assert.Equal(
            ["P1-BASE-UNIT-001", "P2-BERSERK-IMPULSE-TOP-UNIT"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BERSERK-IMPULSE-DECK-KEEP"], result.FinalState.PlayerZones["P2"].MainDeck);
        Assert.Equal(["P1-SPELL-BERSERK-IMPULSE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BERSERK-IMPULSE-TOP-UNIT"].Damage);
        Assert.Empty(result.FinalState.CardObjects["P2-BERSERK-IMPULSE-TOP-UNIT"].UntilEndOfTurnEffects);
        Assert.True(result.FinalState.CardObjects["P2-BERSERK-IMPULSE-TOP-UNIT"].IsExhausted); // CN 143.4.
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBerserkImpulseInvalidTopDeckTargets()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BERSERK-IMPULSE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck =
                    [
                        "P2-BERSERK-IMPULSE-TOP-SPELL",
                        "P2-BERSERK-IMPULSE-SECOND-UNIT"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BERSERK-IMPULSE-TOP-SPELL"] = new(
                    "P2-BERSERK-IMPULSE-TOP-SPELL",
                    tags: [CardObjectTags.SpellCard]),
                ["P2-BERSERK-IMPULSE-SECOND-UNIT"] = new(
                    "P2-BERSERK-IMPULSE-SECOND-UNIT",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P2-BERSERK-IMPULSE-TOP-SPELL"],
            ["P2-BERSERK-IMPULSE-SECOND-UNIT"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-berserk-impulse-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-BERSERK-IMPULSE",
                    "OGN·025/298",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
            Assert.Equal(["P1-SPELL-BERSERK-IMPULSE"], result.State.PlayerZones["P1"].Hand);
            Assert.Equal(
                ["P2-BERSERK-IMPULSE-TOP-SPELL", "P2-BERSERK-IMPULSE-SECOND-UNIT"],
                result.State.PlayerZones["P2"].MainDeck);
        }
    }

    [Fact]
    public async Task CoreRuleEngineBerserkImpulseResolutionSkipsOpponentControlledTopDeckTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-BERSERK-IMPULSE-DIRTY-MAIN-DECK",
                    "P1",
                    "P1-SPELL-BERSERK-IMPULSE",
                    "BLIND_FURY_REVEAL_BANISH_EFFECT_PLAY",
                    "OGN·025/298",
                    ["P2-DIRTY-P1-CONTROLLED-TOP-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck =
                    [
                        "P2-DIRTY-P1-CONTROLLED-TOP-UNIT",
                        "P2-DECK-KEEP-001"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-DIRTY-P1-CONTROLLED-TOP-UNIT"] = new(
                    "P2-DIRTY-P1-CONTROLLED-TOP-UNIT",
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
            new PlayerIntent("intent-berserk-impulse-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-berserk-impulse-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P2-DIRTY-P1-CONTROLLED-TOP-UNIT", "P2-DECK-KEEP-001"], p2Pass.State.PlayerZones["P2"].MainDeck);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-SPELL-BERSERK-IMPULSE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_PLAYED_TO_BASE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBrightFutureEachPlayerTopFiveUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-bright-future-play-each-player-top-five-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        var differences = ConformanceFixtureRunner.CompareExpected(fixture, result);
        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
        Assert.Equal(
            ["P1-BASE-UNIT-001", "P1-BRIGHT-FUTURE-CHOSEN-UNIT"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P2-BASE-UNIT-001", "P2-BRIGHT-FUTURE-CHOSEN-UNIT"],
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P1-SPELL-BRIGHT-FUTURE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("P1-BRIGHT-FUTURE-CHOSEN-UNIT", result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain("P2-BRIGHT-FUTURE-CHOSEN-UNIT", result.FinalState.PlayerZones["P2"].MainDeck);
        Assert.Contains("P1-BRIGHT-FUTURE-UNSELECTED-001", result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Contains("P2-BRIGHT-FUTURE-UNSELECTED-004", result.FinalState.PlayerZones["P2"].MainDeck);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BRIGHT-FUTURE-CHOSEN-UNIT"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BRIGHT-FUTURE-CHOSEN-UNIT"].Damage);
        Assert.Empty(result.FinalState.CardObjects["P1-BRIGHT-FUTURE-CHOSEN-UNIT"].UntilEndOfTurnEffects);
        Assert.Empty(result.FinalState.CardObjects["P2-BRIGHT-FUTURE-CHOSEN-UNIT"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBrightFutureInvalidTopFiveSelections()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BRIGHT-FUTURE"],
                    MainDeck =
                    [
                        "P1-BRIGHT-FUTURE-TOP-UNIT-001",
                        "P1-BRIGHT-FUTURE-TOP-UNIT-002",
                        "P1-BRIGHT-FUTURE-TOP-FILLER-003",
                        "P1-BRIGHT-FUTURE-TOP-FILLER-004",
                        "P1-BRIGHT-FUTURE-TOP-FILLER-005",
                        "P1-BRIGHT-FUTURE-DECK-KEEP"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck =
                    [
                        "P2-BRIGHT-FUTURE-TOP-SPELL",
                        "P2-BRIGHT-FUTURE-TOP-FILLER-002",
                        "P2-BRIGHT-FUTURE-TOP-FILLER-003",
                        "P2-BRIGHT-FUTURE-TOP-FILLER-004",
                        "P2-BRIGHT-FUTURE-TOP-FILLER-005",
                        "P2-BRIGHT-FUTURE-SIXTH-UNIT"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BRIGHT-FUTURE-TOP-UNIT-001"] = new(
                    "P1-BRIGHT-FUTURE-TOP-UNIT-001",
                    tags: [CardObjectTags.UnitCard]),
                ["P1-BRIGHT-FUTURE-TOP-UNIT-002"] = new(
                    "P1-BRIGHT-FUTURE-TOP-UNIT-002",
                    tags: [CardObjectTags.UnitCard]),
                ["P2-BRIGHT-FUTURE-TOP-SPELL"] = new(
                    "P2-BRIGHT-FUTURE-TOP-SPELL",
                    tags: [CardObjectTags.SpellCard]),
                ["P2-BRIGHT-FUTURE-SIXTH-UNIT"] = new(
                    "P2-BRIGHT-FUTURE-SIXTH-UNIT",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        IReadOnlyList<string>[] invalidTargets =
        [
            ["P1-BRIGHT-FUTURE-TOP-UNIT-001", "P1-BRIGHT-FUTURE-TOP-UNIT-002"],
            ["P1-BRIGHT-FUTURE-TOP-UNIT-001", "P2-BRIGHT-FUTURE-SIXTH-UNIT"],
            ["P1-BRIGHT-FUTURE-TOP-UNIT-001", "P2-BRIGHT-FUTURE-TOP-SPELL"]
        ];

        foreach (var targetObjectIds in invalidTargets)
        {
            var result = await new CoreRuleEngine().ResolveAsync(
                state,
                new PlayerIntent("intent-bright-future-invalid-target", "P1", "PLAY_CARD"),
                new PlayCardCommand(
                    "P1-SPELL-BRIGHT-FUTURE",
                    "OGN·115/298",
                    targetObjectIds),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Empty(result.Events);
            Assert.Equal(0, result.State.Tick);
            Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
            Assert.Equal(["P1-SPELL-BRIGHT-FUTURE"], result.State.PlayerZones["P1"].Hand);
            Assert.Equal("P1-BRIGHT-FUTURE-TOP-UNIT-001", result.State.PlayerZones["P1"].MainDeck[0]);
            Assert.Equal("P2-BRIGHT-FUTURE-TOP-SPELL", result.State.PlayerZones["P2"].MainDeck[0]);
        }
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBrightFutureWhenTopFiveTargetIsNotControlledByDeckPlayer()
    {
        var state = PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BRIGHT-FUTURE"],
                    MainDeck =
                    [
                        "P1-BRIGHT-FUTURE-TOP-UNIT-001",
                        "P1-BRIGHT-FUTURE-TOP-FILLER-002",
                        "P1-BRIGHT-FUTURE-TOP-FILLER-003",
                        "P1-BRIGHT-FUTURE-TOP-FILLER-004",
                        "P1-BRIGHT-FUTURE-TOP-FILLER-005"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck =
                    [
                        "P2-DIRTY-P1-OWNED-TOP-UNIT",
                        "P2-BRIGHT-FUTURE-TOP-FILLER-002",
                        "P2-BRIGHT-FUTURE-TOP-FILLER-003",
                        "P2-BRIGHT-FUTURE-TOP-FILLER-004",
                        "P2-BRIGHT-FUTURE-TOP-FILLER-005"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BRIGHT-FUTURE"] = new(
                    "P1-SPELL-BRIGHT-FUTURE",
                    cardNo: "OGN·115/298",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BRIGHT-FUTURE-TOP-UNIT-001"] = new(
                    "P1-BRIGHT-FUTURE-TOP-UNIT-001",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-DIRTY-P1-OWNED-TOP-UNIT"] = new(
                    "P2-DIRTY-P1-OWNED-TOP-UNIT",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-bright-future-dirty-top-five-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BRIGHT-FUTURE",
                "OGN·115/298",
                ["P1-BRIGHT-FUTURE-TOP-UNIT-001", "P2-DIRTY-P1-OWNED-TOP-UNIT"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(5, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BRIGHT-FUTURE"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal("P1-BRIGHT-FUTURE-TOP-UNIT-001", result.State.PlayerZones["P1"].MainDeck[0]);
        Assert.Equal("P2-DIRTY-P1-OWNED-TOP-UNIT", result.State.PlayerZones["P2"].MainDeck[0]);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysJudgmentDayRecycleUnkept()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-judgment-day-recycle-unkept.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-HAND-KEEP-001", "P1-HAND-KEEP-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-HAND-KEEP-001", "P2-HAND-KEEP-002"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P1-SPELL-JUDGMENT"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-UNIT-RECYCLE-001", result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Contains("P2-HAND-RECYCLE-001", result.FinalState.PlayerZones["P2"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsJudgmentDayInvalidKeepSelection()
    {
        var invalidTargets = JudgmentDayKeepTargets().ToArray();
        invalidTargets[3] = "P1-UNIT-RECYCLE-001";

        var result = await new CoreRuleEngine().ResolveAsync(
            JudgmentDayState(),
            new PlayerIntent("intent-judgment-day-invalid-keep-selection", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-JUDGMENT",
                "OGN·244/298",
                invalidTargets),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(7, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            ["P1-SPELL-JUDGMENT", "P1-HAND-KEEP-001", "P1-HAND-KEEP-002", "P1-HAND-RECYCLE-001"],
            result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsJudgmentDayWhenKeepTargetIsNotControlledByZonePlayer()
    {
        var baseState = JudgmentDayState();
        var playerZones = baseState.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P2"] = playerZones["P2"] with
        {
            Hand = ["P2-HAND-KEEP-001", "P2-HAND-DIRTY-P1-OWNED", "P2-HAND-RECYCLE-001"]
        };
        var cardObjects = baseState.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P2-HAND-DIRTY-P1-OWNED"] = new(
            "P2-HAND-DIRTY-P1-OWNED",
            cardNo: "SFD·125/221",
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1");
        var state = baseState with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
        var invalidTargets = JudgmentDayKeepTargets().ToArray();
        invalidTargets[^1] = "P2-HAND-DIRTY-P1-OWNED";

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-judgment-day-dirty-keep-selection", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-JUDGMENT",
                "OGN·244/298",
                invalidTargets),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(7, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            ["P2-HAND-KEEP-001", "P2-HAND-DIRTY-P1-OWNED", "P2-HAND-RECYCLE-001"],
            result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsWindWallAgainstUnitStackItem()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(3, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-WIND-WALL"]
                }
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-UNIT-SCUTTLE",
                    "P1",
                    "P1-UNIT-SCUTTLE",
                    "SCUTTLE_CRAB_PLAY_UNIT_DRAW_1",
                    "UNL-053/219",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-wind-wall-unit-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-WIND-WALL",
                "OGN·064/298",
                ["STACK-1-P1-UNIT-SCUTTLE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-WIND-WALL"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-UNIT-SCUTTLE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsAbandonAgainstUnitStackItem()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-ABANDON"]
                }
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-UNIT-SCUTTLE",
                    "P1",
                    "P1-UNIT-SCUTTLE",
                    "SCUTTLE_CRAB_PLAY_UNIT_DRAW_1",
                    "UNL-053/219",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-abandon-unit-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-ABANDON",
                "UNL-131/219",
                ["STACK-1-P1-UNIT-SCUTTLE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-ABANDON"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-UNIT-SCUTTLE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFeatherstormCounterAgainstUnitStackItem()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(4, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-FEATHERSTORM"]
                }
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-UNIT-SCUTTLE",
                    "P1",
                    "P1-UNIT-SCUTTLE",
                    "SCUTTLE_CRAB_PLAY_UNIT_DRAW_1",
                    "UNL-053/219",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-featherstorm-counter-unit-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-FEATHERSTORM",
                "UNL-044/219",
                ["STACK-1-P1-UNIT-SCUTTLE"],
                Mode: "COUNTER_SPELL"),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-FEATHERSTORM"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-UNIT-SCUTTLE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsNightfallLullabyAgainstUnitStackItem()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-NIGHTFALL-LULLABY"]
                }
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-UNIT-SCUTTLE",
                    "P1",
                    "P1-UNIT-SCUTTLE",
                    "SCUTTLE_CRAB_PLAY_UNIT_DRAW_1",
                    "UNL-053/219",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-nightfall-unit-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-NIGHTFALL-LULLABY",
                "UNL-190/219",
                ["STACK-1-P1-UNIT-SCUTTLE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-NIGHTFALL-LULLABY"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-UNIT-SCUTTLE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHardBargainAgainstUnitStackItem()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-HARD-BARGAIN"]
                }
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-UNIT-SCUTTLE",
                    "P1",
                    "P1-UNIT-SCUTTLE",
                    "SCUTTLE_CRAB_PLAY_UNIT_DRAW_1",
                    "UNL-053/219",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-hard-bargain-unit-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-HARD-BARGAIN",
                "SFD·136/221",
                ["STACK-1-P1-UNIT-SCUTTLE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-HARD-BARGAIN"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-UNIT-SCUTTLE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDefianceAgainstUnitStackItem()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 2)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-DEFIANCE"]
                }
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-UNIT-SCUTTLE",
                    "P1",
                    "P1-UNIT-SCUTTLE",
                    "SCUTTLE_CRAB_PLAY_UNIT_DRAW_1",
                    "UNL-053/219",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-defiance-unit-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-DEFIANCE",
                "OGN·045/298",
                ["STACK-1-P1-UNIT-SCUTTLE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 2), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-DEFIANCE"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-UNIT-SCUTTLE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsDefianceWhenStackSpellCostExceedsFour()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 5)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-DEFIANCE"]
                }
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-SPELL-COMET",
                    "P1",
                    "P1-SPELL-COMET",
                    "COMET_STRIKE_DAMAGE_6",
                    "OGN·085/298",
                    ["P2-UNIT-DEFIANCE-001"])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-defiance-expensive-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-DEFIANCE",
                "OGN·045/298",
                ["STACK-1-P1-SPELL-COMET"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 5), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-DEFIANCE"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-SPELL-COMET"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineAcceptsDefianceWhenTargetManaExceedsRemainingPower()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 1)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-DEFIANCE"]
                }
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-SPELL-INCINERATE",
                    "P1",
                    "P1-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    ["P2-UNIT-DEFIANCE-001"])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-defiance-low-power-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-DEFIANCE",
                "OGN·045/298",
                ["STACK-1-P1-SPELL-INCINERATE"]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P2"]);
        Assert.Empty(result.State.PlayerZones["P2"].Hand);
        Assert.Equal(2, result.State.StackItems.Count);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsQuickCounterAgainstUnitStackItem()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-QUICK-COUNTER"]
                }
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-UNIT-SCUTTLE",
                    "P1",
                    "P1-UNIT-SCUTTLE",
                    "SCUTTLE_CRAB_PLAY_UNIT_DRAW_1",
                    "UNL-053/219",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-quick-counter-unit-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-QUICK-COUNTER",
                "SFD·045/221",
                ["STACK-1-P1-UNIT-SCUTTLE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-QUICK-COUNTER"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-UNIT-SCUTTLE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsQuickCounterAgainstFriendlyControlledStackItem()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-QUICK-COUNTER"],
                    Battlefields = ["P2-UNIT-QUICK-COUNTER-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-QUICK-COUNTER-001"] = new(
                    "P2-UNIT-QUICK-COUNTER-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P2-SPELL-INCINERATE",
                    "P2",
                    "P2-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    ["P2-UNIT-QUICK-COUNTER-001"])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-quick-counter-friendly-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-QUICK-COUNTER",
                "SFD·045/221",
                ["STACK-1-P2-SPELL-INCINERATE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-QUICK-COUNTER"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P2-SPELL-INCINERATE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsQuickCounterWhenEnemySpellTargetsEnemyObject()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-QUICK-COUNTER-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-QUICK-COUNTER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-QUICK-COUNTER-001"] = new(
                    "P1-UNIT-QUICK-COUNTER-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-SPELL-INCINERATE",
                    "P1",
                    "P1-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    ["P1-UNIT-QUICK-COUNTER-001"])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-quick-counter-enemy-spell-enemy-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-QUICK-COUNTER",
                "SFD·045/221",
                ["STACK-1-P1-SPELL-INCINERATE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-QUICK-COUNTER"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-SPELL-INCINERATE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsRepelAgainstUnitStackItem()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-REPEL"],
                    Battlefields = ["P2-UNIT-REPEL-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-REPEL-001"] = new(
                    "P2-UNIT-REPEL-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-UNIT-SCUTTLE",
                    "P1",
                    "P1-UNIT-SCUTTLE",
                    "SCUTTLE_CRAB_PLAY_UNIT_DRAW_1",
                    "UNL-053/219",
                    ["P2-UNIT-REPEL-001"])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-repel-unit-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-REPEL",
                "UNL-106/219",
                ["P2-UNIT-REPEL-001", "STACK-1-P1-UNIT-SCUTTLE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-REPEL"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-UNIT-SCUTTLE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsRepelAgainstFriendlyControlledStackItem()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-REPEL"],
                    Battlefields = ["P2-UNIT-REPEL-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-REPEL-001"] = new(
                    "P2-UNIT-REPEL-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P2-SPELL-INCINERATE",
                    "P2",
                    "P2-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    ["P2-UNIT-REPEL-001"])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-repel-friendly-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-REPEL",
                "UNL-106/219",
                ["P2-UNIT-REPEL-001", "STACK-1-P2-SPELL-INCINERATE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-REPEL"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P2-SPELL-INCINERATE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsRepelWhenEnemySpellDoesNotTargetChosenUnit()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-REPEL"],
                    Battlefields = ["P2-UNIT-REPEL-001", "P2-UNIT-REPEL-002"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-REPEL-001"] = new(
                    "P2-UNIT-REPEL-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-UNIT-REPEL-002"] = new(
                    "P2-UNIT-REPEL-002",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-SPELL-INCINERATE",
                    "P1",
                    "P1-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    ["P2-UNIT-REPEL-002"])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-repel-spell-misses-chosen-unit", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-REPEL",
                "UNL-106/219",
                ["P2-UNIT-REPEL-001", "STACK-1-P1-SPELL-INCINERATE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-REPEL"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-SPELL-INCINERATE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsRepelWhenEnemySpellTargetsAnotherFriendlyUnitToo()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(1, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-REPEL"],
                    Battlefields = ["P2-UNIT-REPEL-001", "P2-UNIT-REPEL-002"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-REPEL-001"] = new(
                    "P2-UNIT-REPEL-001",
                    power: 5,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-UNIT-REPEL-002"] = new(
                    "P2-UNIT-REPEL-002",
                    power: 4,
                    tags: [CardObjectTags.UnitCard])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-SPELL-STELLAR-CONVERGENCE",
                    "P1",
                    "P1-SPELL-STELLAR-CONVERGENCE",
                    "STELLAR_CONVERGENCE_DAMAGE_6_UP_TO_2",
                    "OGN·105/298",
                    ["P2-UNIT-REPEL-001", "P2-UNIT-REPEL-002"])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-repel-spell-targets-other-friendly-unit", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-REPEL",
                "UNL-106/219",
                ["P2-UNIT-REPEL-001", "STACK-1-P1-SPELL-STELLAR-CONVERGENCE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-REPEL"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-SPELL-STELLAR-CONVERGENCE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsLaurentDuelistAgainstUnitStackItem()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-LAURENT-DUELIST"],
                    Battlefields = ["P2-UNIT-LAURENT-DUELIST-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-LAURENT-DUELIST-001"] = new(
                    "P2-UNIT-LAURENT-DUELIST-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-UNIT-SCUTTLE",
                    "P1",
                    "P1-UNIT-SCUTTLE",
                    "SCUTTLE_CRAB_PLAY_UNIT_DRAW_1",
                    "UNL-053/219",
                    [])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-laurent-duelist-unit-stack-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-LAURENT-DUELIST",
                "SFD·206/221",
                ["P2-UNIT-LAURENT-DUELIST-001", "STACK-1-P1-UNIT-SCUTTLE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-LAURENT-DUELIST"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-UNIT-SCUTTLE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsLaurentDuelistWhenFirstTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 0) with
        {
            ActivePlayerId = "P2",
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-UNIT-LAURENT-DUELIST-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-SPELL-LAURENT-DUELIST"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-LAURENT-DUELIST-001"] = new(
                    "P1-UNIT-LAURENT-DUELIST-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            },
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-SPELL-INCINERATE",
                    "P1",
                    "P1-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    ["P1-UNIT-LAURENT-DUELIST-001"])
            ]
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-laurent-duelist-enemy-unit-first-target", "P2", "PLAY_CARD"),
            new PlayCardCommand(
                "P2-SPELL-LAURENT-DUELIST",
                "SFD·206/221",
                ["P1-UNIT-LAURENT-DUELIST-001", "STACK-1-P1-SPELL-INCINERATE"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P2"]);
        Assert.Equal(["P2-SPELL-LAURENT-DUELIST"], result.State.PlayerZones["P2"].Hand);
        Assert.Equal(["STACK-1-P1-SPELL-INCINERATE"], result.State.StackItems.Select(item => item.StackItemId));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHextechRayThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hextech-ray-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineBoostsThunderingDropAgainstAttackingUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-thundering-drop-attacking-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPiercingLightAgainstTwoBattlefieldUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-piercing-light-two-target-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-002"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBellowsBreathAgainstUpToThreeUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-bellows-breath-up-to-three-units-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(1, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-002"].Damage);
        Assert.Equal(
            3,
            result.EventKinds.Count(kind => string.Equals(kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFirestormAgainstEnemyBattlefieldUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-firestorm-damage-enemy-battlefield-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.CardObjects["P1-FRIENDLY-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-ENEMY-BASE-UNIT"].Damage);
        Assert.Equal(3, result.FinalState.CardObjects["P2-ENEMY-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Equal(3, result.FinalState.CardObjects["P2-ENEMY-BATTLEFIELD-UNIT-002"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCrescentStrikeTargetPlusSplash()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-crescent-strike-target-plus-splash.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.CardObjects["P1-CRESCENT-FRIENDLY-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-CRESCENT-BASE-001"].Damage);
        Assert.Equal(4, result.FinalState.CardObjects["P2-CRESCENT-PRIMARY-001"].Damage);
        Assert.Equal(1, result.FinalState.CardObjects["P2-CRESCENT-SPLASH-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSwitcherooSwappingBattlefieldUnitPowers()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-switcheroo-swap-battlefield-unit-powers.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P1-SWITCHEROO-UNIT-001"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P2-SWITCHEROO-UNIT-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P2-SWITCHEROO-BASE-UNIT"].Power);
    }

    [Fact]
    public async Task CoreRuleEngineReducesThunderingSkyByHighestControlledUnitPower()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-thundering-sky-cost-reduced-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEngineClearsHextechRayDamageAtEndTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-hextech-ray-damage-clears-end-turn.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCometStrikeThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-comet-strike-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFinalSparkThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-final-spark-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSuperMegaDeathRocketThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-super-mega-death-rocket-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCenterStageDrawThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-center-stage-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCenterStageEchoDrawThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-center-stage-echo-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        var costPaidEvent = Assert.Single(result.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.StartsWith("PLAY_CARD:", Assert.IsType<string>(costPaidEvent.Payload["paymentId"]), StringComparison.Ordinal);
        Assert.Equal("PLAY_CARD", costPaidEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-SPELL-CENTER-STAGE", costPaidEvent.Payload["sourceObjectId"]);
        Assert.Equal("CENTER_STAGE_DRAW_1", costPaidEvent.Payload["reason"]);
        Assert.Equal(2, costPaidEvent.Payload["baseManaCost"]);
        Assert.Equal(4, costPaidEvent.Payload["totalManaCost"]);
        Assert.Equal(0, costPaidEvent.Payload["genericPower"]);
        Assert.Equal(0, costPaidEvent.Payload["totalPowerCost"]);
        Assert.Equal(0, costPaidEvent.Payload["remainingMana"]);
        Assert.Equal(["ECHO"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysProphetsOmenThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-prophets-omen-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMightMakesRightDrawPowerfulUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-might-makes-right-draw-powerful-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysEvolutionDayThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-evolution-day-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMobilizeCallRune()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-mobilize-call-rune.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-002"], result.FinalState.PlayerZones["P1"].RuneDeck);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMobilizeDrawsIfRuneCallFails()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-mobilize-draws-if-rune-call-fails.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].RuneDeck);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCatalystOfAeonsCallTwoRunes()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-catalyst-of-aeons-call-two-runes.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-RUNE-001", "P1-RUNE-002"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-RUNE-003"], result.FinalState.PlayerZones["P1"].RuneDeck);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-002"].IsExhausted);
        Assert.DoesNotContain("CARD_DRAWN", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCatalystOfAeonsDrawsIfRuneCallIsShort()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-catalyst-of-aeons-draws-if-rune-call-short.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].RuneDeck);
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMindAndBalanceReducedDrawThenCallRune()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-mind-and-balance-reduced-draw-then-call-rune.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        var eventKinds = result.EventKinds.ToArray();
        Assert.True(Array.IndexOf(eventKinds, "CARD_DRAWN") < Array.IndexOf(eventKinds, "RUNES_CALLED"));
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysVengeanceThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-vengeance-destroy-unit-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEngineQueuesWatchfulSentinelLastBreathDrawWhenDestroyed()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p5-last-breath-watchful-sentinel-draw-on-destroy.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        CardZoneTestAssertions.RetainedOutsidePlay(result.FinalState, "P2-WATCHFUL-SENTINEL-001", "GRAVEYARD");
        Assert.Equal(["P2-LAST-BREATH-DRAW-001"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Contains(result.Events, ev => string.Equals(ev.Kind, "TRIGGER_QUEUED", StringComparison.Ordinal));
        Assert.Contains(result.Events, ev => string.Equals(ev.Kind, "TRIGGER_RESOLVED", StringComparison.Ordinal));
        Assert.Empty(result.FinalState.TriggerQueue);
    }

    [Fact]
    public async Task CoreRuleEngineDetachesEquipmentWhenHostUnitIsDestroyed()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p5-equipment-detaches-when-host-destroyed.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        CardZoneTestAssertions.RetainedOutsidePlay(result.FinalState, "P2-HOST-LEAVE-UNIT", "GRAVEYARD");
        Assert.Contains("P2-HOST-LEAVE-LONG-SWORD", result.FinalState.CardObjects.Keys);
        Assert.Null(result.FinalState.CardObjects["P2-HOST-LEAVE-LONG-SWORD"].AttachedToObjectId);
        Assert.DoesNotContain("P2-HOST-LEAVE-LONG-SWORD", result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Contains("P2-HOST-LEAVE-LONG-SWORD", result.FinalState.PlayerZones["P2"].Base);
        Assert.Contains(result.Events, ev => string.Equals(ev.Kind, "EQUIPMENT_RECALLED_TO_BASE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysWellspringOfHatredDestroyBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-wellspring-of-hatred-destroy-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDetonationThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-detonation-destroy-battlefield-unit-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHuntTheWeakAgainstSmallBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hunt-the-weak-destroy-small-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDarkinBladeAndTargetControllerDraws()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-darkin-blade-destroy-target-controller-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2-DRAW-001", "P2-DRAW-002"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRuinationAndDestroysAllUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-ruination-destroy-all-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-BASE-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P1-BATTLEFIELD-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BASE-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHousecleaningDestroyEachPlayerUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-housecleaning-destroy-each-player-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-HOUSECLEANING-FRIENDLY-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-HOUSECLEANING-ENEMY-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-HOUSECLEANING-KEEPER-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-HOUSECLEANING-KEEPER-001"], result.FinalState.PlayerZones["P2"].Base);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysKingsEdictDestroyingEnemyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-kings-edict-destroy-enemy-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Contains("P1-KINGS-EDICT-FRIENDLY-001", result.FinalState.CardObjects.Keys);
        Assert.Contains("P2-KINGS-EDICT-KEEPER-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-KINGS-EDICT-CHOSEN-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-KINGS-EDICT-CHOSEN-001"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSpiritFireDestroyingBattlefieldUnitsTotalPowerFour()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-spirit-fire-destroy-total-power-four.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-SPIRIT-FIRE-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-SPIRIT-FIRE-UNIT-002", result.FinalState.CardObjects.Keys);
        Assert.Contains("P2-SPIRIT-FIRE-KEEPER-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-SPIRIT-FIRE-UNIT-001", "P2-SPIRIT-FIRE-UNIT-002"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysQuicksandPitThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-quicksand-pit-destroy-battlefield-unit-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysReprimandAndReturnsBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-reprimand-return-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-HAND-001", "P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysGustAndReturnsSmallBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-gust-return-small-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHappenstanceAndReturnsFriendlyThenEnemy()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-happenstance-return-friendly-and-enemy.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-UNIT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-UNIT-001"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHurricaneSweepEachPlayerReturnUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hurricane-sweep-each-player-return-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-HURRICANE-FRIENDLY-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-HURRICANE-ENEMY-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-HURRICANE-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-HURRICANE-ENEMY-001"], result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineHurricaneSweepResolutionSkipsOpponentControlledBattlefieldTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-HURRICANE-SWEEP-DIRTY-BATTLEFIELD",
                    "P1",
                    "P1-SPELL-HURRICANE-SWEEP",
                    "HURRICANE_SWEEP_EACH_PLAYER_MAY_RETURN_UNIT",
                    "OGN·187/298",
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
            new PlayerIntent("intent-hurricane-sweep-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-hurricane-sweep-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"], p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Hand);
        Assert.Equal(["P1-SPELL-HURRICANE-SWEEP"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT", p2Pass.State.CardObjects.Keys);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_RETURNED_TO_HAND", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCustodianJudgmentUnitToDeckTop()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-custodian-judgment-unit-to-deck-top.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-CUSTODIAN-TARGET-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(
            ["P2-CUSTODIAN-TARGET-001", "P2-MAIN-001", "P2-MAIN-002"],
            result.FinalState.PlayerZones["P2"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCustodianJudgmentUnitToDeckBottom()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-custodian-judgment-unit-to-deck-bottom.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-CUSTODIAN-TARGET-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(
            ["P2-MAIN-001", "P2-MAIN-002", "P2-CUSTODIAN-TARGET-001"],
            result.FinalState.PlayerZones["P2"].MainDeck);
    }

    [Fact]
    public async Task CoreRuleEngineCustodianJudgmentResolutionSkipsOpponentControlledBattlefieldTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-CUSTODIAN-JUDGMENT-DIRTY-BATTLEFIELD",
                    "P1",
                    "P1-SPELL-CUSTODIAN-JUDGMENT",
                    "CUSTODIAN_JUDGMENT_ENEMY_BATTLEFIELD_UNIT_TO_TOP",
                    "UNL-204/219",
                    ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"],
                    MainDeck = ["P2-MAIN-001"]
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
            new PlayerIntent("intent-custodian-judgment-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-custodian-judgment-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"], p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-MAIN-001"], p2Pass.State.PlayerZones["P2"].MainDeck);
        Assert.Equal(["P1-SPELL-CUSTODIAN-JUDGMENT"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT", p2Pass.State.CardObjects.Keys);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_RETURNED_TO_DECK", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysReconsiderAndCallsRuneForReturnedOwner()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-reconsider-return-friendly-call-rune.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P1-UNIT-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-RUNE-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.True(result.FinalState.CardObjects["P1-RUNE-001"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysUndertowAndReturnsAllUnitsAndEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-undertow-return-all-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-BASE-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P1-BATTLEFIELD-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P1-BASE-EQUIPMENT-UNDERTOW-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-UNDERTOW-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(
            ["P1-BASE-UNIT-001", "P1-BASE-EQUIPMENT-UNDERTOW-001", "P1-BATTLEFIELD-UNIT-001"],
            result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P2-BASE-EQUIPMENT-UNDERTOW-001", "P2-BATTLEFIELD-UNIT-001"],
            result.FinalState.PlayerZones["P2"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBrokenBladesRematchDestroyEachPlayerEquipment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-broken-blades-rematch-destroy-each-player-equipment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P1-BASE-EQUIPMENT-BROKEN-BLADES-001", result.FinalState.CardObjects.Keys);
        Assert.DoesNotContain("P2-BASE-EQUIPMENT-BROKEN-BLADES-001", result.FinalState.CardObjects.Keys);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsBrokenBladesRematchAgainstUnitTarget()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-BROKEN-BLADES-REMATCH"],
                    Base = ["P1-BASE-EQUIPMENT-BROKEN-BLADES-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-BROKEN-BLADES-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-EQUIPMENT-BROKEN-BLADES-001"] = new("P1-BASE-EQUIPMENT-BROKEN-BLADES-001", tags: [CardObjectTags.EquipmentCard]),
                ["P2-BATTLEFIELD-UNIT-BROKEN-BLADES-001"] = new("P2-BATTLEFIELD-UNIT-BROKEN-BLADES-001", power: 2, tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-broken-blades-unit-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-BROKEN-BLADES-REMATCH",
                "OGN·179/298",
                ["P1-BASE-EQUIPMENT-BROKEN-BLADES-001", "P2-BATTLEFIELD-UNIT-BROKEN-BLADES-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-BROKEN-BLADES-REMATCH"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-EQUIPMENT-BROKEN-BLADES-001"], result.State.PlayerZones["P1"].Base);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-BROKEN-BLADES-001"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBattleOrFlightAndMovesBattlefieldUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-battle-or-flight-move-battlefield-unit-to-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P2-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"],
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.True(result.FinalState.CardObjects.ContainsKey("P2-BATTLEFIELD-UNIT-001"));
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFaceOffAndMovesWeakerEnemyBattlefieldUnitsToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-face-off-move-weaker-enemy-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["BF2", "P1-FACE-OFF-FRIENDLY-001"], result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(
            ["P2-BASE-UNIT-001", "P2-FACE-OFF-ENEMY-001", "P2-FACE-OFF-ENEMY-002"],
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["BF", "P2-FACE-OFF-ENEMY-003"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(1, result.FinalState.PlayerExperience["P1"]);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsFaceOffWhenEnemyTargetIsNotWeakerThanFirstTarget()
    {
        // Preserve the old malformed three-unit command regression. Official Face Off
        // targets a friendly unit and a battlefield; equal-power affected units are legal
        // and stay in place, covered by FaceOffTests.
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-FACE-OFF"],
                    Battlefields = ["P1-FACE-OFF-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-FACE-OFF-ENEMY-001", "P2-FACE-OFF-ENEMY-002"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-FACE-OFF-FRIENDLY-001"] = new(
                    "P1-FACE-OFF-FRIENDLY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-FACE-OFF-ENEMY-001"] = new(
                    "P2-FACE-OFF-ENEMY-001",
                    power: 4,
                    tags: [CardObjectTags.UnitCard]),
                ["P2-FACE-OFF-ENEMY-002"] = new(
                    "P2-FACE-OFF-ENEMY-002",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-face-off-reject-equal-power", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-FACE-OFF",
                "UNL-107/219",
                ["P1-FACE-OFF-FRIENDLY-001", "P2-FACE-OFF-ENEMY-001", "P2-FACE-OFF-ENEMY-002"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-FACE-OFF"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-FACE-OFF-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-FACE-OFF-ENEMY-001", "P2-FACE-OFF-ENEMY-002"], result.State.PlayerZones["P2"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRideTheWindAndMovesFriendlyBattlefieldUnitToBaseReady()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-ride-the-wind-move-friendly-battlefield-unit-to-base-ready.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_READIED", StringComparison.Ordinal)));
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRuthlessPursuitMoveFriendlyUnitRecallMark()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-ruthless-pursuit-move-friendly-unit-recall-mark.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        var currentComparison = ConformanceFixtureRunner.CompareExpected(fixture, result);
        Assert.True(currentComparison.Count == 0, string.Join("\n", currentComparison));
        Assert.Empty(currentComparison);
        Assert.Equal(["P1-RUTHLESS-PURSUIT-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Contains(result.FinalState.CardObjects["P1-RUTHLESS-PURSUIT-UNIT-001"].UntilEndOfTurnEffects,
            effect => effect.StartsWith("GRANTED_CONQUEST_RETURN_BASE|", StringComparison.Ordinal));
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysIsolateAndMovesEnemyBattlefieldUnitToBaseWithoutDrawing()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-isolate-move-enemy-battlefield-unit-to-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P2-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"],
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.DoesNotContain("CARD_DRAWN", result.EventKinds);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCharmAndMovesEnemyBattlefieldUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-charm-move-enemy-battlefield-unit-to-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P2-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"],
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysRisingDragonKickAndMovesEnemyBattlefieldUnitToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-rising-dragon-kick-move-enemy-battlefield-unit-to-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P2-BASE-UNIT-001", "P2-BATTLEFIELD-UNIT-001"],
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Damage);
        Assert.DoesNotContain("STATUS_EFFECT_APPLIED", result.EventKinds);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFlashAndMovesTwoFriendlyBattlefieldUnitsToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-flash-move-two-friendly-battlefield-units-to-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-BASE-UNIT-001", "P1-BATTLEFIELD-UNIT-001", "P1-BATTLEFIELD-UNIT-002"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(1, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Damage);
        Assert.Contains(
            "STUNNED",
            result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-002"].UntilEndOfTurnEffects);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineFlashResolutionSkipsOpponentControlledFriendlyBattlefieldTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-FLASH-DIRTY-BATTLEFIELD",
                    "P1",
                    "P1-SPELL-FLASH",
                    "FLASH_MOVE_UP_TO_2_FRIENDLY_BATTLEFIELD_UNITS_TO_BASE",
                    "OGS·011/024",
                    ["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"] = new(
                    "P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    damage: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-flash-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-flash-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"], p2Pass.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(["P1-SPELL-FLASH"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(1, p2Pass.State.CardObjects["P1-DIRTY-P2-CONTROLLED-BATTLEFIELD-UNIT"].Damage);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysShieldWallAndMovesSelectedFriendlyBattlefieldUnitsToBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-shield-wall-move-any-friendly-battlefield-units-to-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-BASE-UNIT-001",
                "P1-SHIELD-WALL-UNIT-001",
                "P1-SHIELD-WALL-UNIT-002",
                "P1-SHIELD-WALL-UNIT-003"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(
            3,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysPlayfulTentaclesAndMovesEnemyBattlefieldUnitsTotalPowerEight()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-playful-tentacles-move-total-power-eight.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P2-BASE-UNIT-001",
                "P2-PLAYFUL-TENTACLES-UNIT-001",
                "P2-PLAYFUL-TENTACLES-UNIT-002"
            ],
            result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["BF", "P2-PLAYFUL-TENTACLES-KEEPER-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Equal(1, result.FinalState.CardObjects["P2-PLAYFUL-TENTACLES-UNIT-001"].Damage);
        Assert.Contains(
            "STUNNED",
            result.FinalState.CardObjects["P2-PLAYFUL-TENTACLES-UNIT-002"].UntilEndOfTurnEffects);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_BASE", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBaitAndMovesEnemyUnitToAnotherEnemyUnitLocation()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-bait-move-enemy-unit-to-another-location.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2-BAIT-DESTINATION-001", "P2-BAIT-MOVED-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BAIT-KEEPER-001"], result.FinalState.PlayerZones["P2"].Battlefields);
        Assert.Contains(
            "STUNNED",
            result.FinalState.CardObjects["P2-BAIT-MOVED-001"].UntilEndOfTurnEffects);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_MOVED_TO_UNIT_LOCATION", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEngineBaitResolutionSkipsAlreadyControlledEnemyZoneTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-BAIT-DIRTY-LOCATION",
                    "P1",
                    "P1-SPELL-BAIT",
                    "BAIT_MOVE_ENEMY_UNIT_TO_ANOTHER_ENEMY_UNIT_LOCATION_NO_ECHO",
                    "SFD·129/221",
                    [
                        "P2-DIRTY-P1-CONTROLLED-BASE-UNIT",
                        "P2-BAIT-VALID-DESTINATION-UNIT"
                    ])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-DIRTY-P1-CONTROLLED-BASE-UNIT"],
                    Battlefields = ["P2-BAIT-VALID-DESTINATION-UNIT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-DIRTY-P1-CONTROLLED-BASE-UNIT"] = new(
                    "P2-DIRTY-P1-CONTROLLED-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BAIT-VALID-DESTINATION-UNIT"] = new(
                    "P2-BAIT-VALID-DESTINATION-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-bait-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-bait-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P2-DIRTY-P1-CONTROLLED-BASE-UNIT"], p2Pass.State.PlayerZones["P2"].Base);
        Assert.Equal(["P2-BAIT-VALID-DESTINATION-UNIT"], p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P1-SPELL-BAIT"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_MOVED_TO_UNIT_LOCATION", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDragonsRageMoveThenMutualDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dragons-rage-move-then-mutual-damage.fixture.json"), CancellationToken.None);
        var result = await ConformanceFixtureRunner.RunAsync(fixture, new CoreRuleEngine(), CancellationToken.None);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_REFLEXIVE_DUEL_EVIDENCE") is { } root)
        { Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root,"fixture-intermediate.json"), JsonSerializer.Serialize(result)); }
        var differences=ConformanceFixtureRunner.CompareExpected(fixture,result);Assert.True(differences.Count==0,string.Join("; ",differences));
        var choice=Assert.IsType<PendingCardChoiceState>(result.FinalState.PendingCardChoice);
        var selected=await OfficialGraveyardRecastTests.Act(result.FinalState,"P1",new ChooseCardsCommand(choice.ChoiceId,choice.ChoiceWindow,["P2-DRAGONS-RAGE-DESTINATION-001"]));
        Assert.Equal(0,selected.State.CardObjects["P2-DRAGONS-RAGE-MOVED-001"].Damage);
        var done=await OfficialGraveyardRecastTests.Top(selected.State);
        Assert.Empty(done.State.PlayerZones["P2"].Base);
        Assert.Equal(["BF-RAGE","P2-DRAGONS-RAGE-MOVED-001"],done.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(["P2-DRAGONS-RAGE-DESTINATION-001"],done.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(3,done.State.CardObjects["P2-DRAGONS-RAGE-MOVED-001"].Damage);
        Assert.Equal(1,result.EventKinds.Count(kind=>kind=="UNIT_MOVED_TO_BATTLEFIELD"));
        OfficialGraveyardRecastTests.Restore(done.State);
    }

    [Fact]
    public async Task CoreRuleEngineDragonsRageResolutionSkipsAlreadyControlledEnemyZoneTarget()
    {
        var state=ReflexiveMovementDuelTests.Position();
        var played=await OfficialGraveyardRecastTests.Act(state,"P1",ReflexiveMovementDuelTests.Command());
        state=played.State with {CardObjects=new Dictionary<string,CardObjectState>(played.State.CardObjects) {
            ["D"]=played.State.CardObjects["D"] with {ControllerId="P1"} }};
        var done=await OfficialGraveyardRecastTests.Top(state);
        Assert.Equal(state.PlayerZones["P2"].Base,done.State.PlayerZones["P2"].Base);
        Assert.Equal(state.PlayerZones["P2"].Battlefields,done.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("AOE",done.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(0,done.State.CardObjects["D"].Damage);Assert.Equal(0,done.State.CardObjects["E"].Damage);
        Assert.DoesNotContain(done.Events,e=>e.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD" or "DAMAGE_APPLIED");
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTheCurtainRisesEchoAndReadiesUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-the-curtain-rises-echo-ready-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_READIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTheCurtainRisesBaseReadyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-the-curtain-rises-ready-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.False(result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.Single(result.EventKinds, kind => string.Equals(kind, "UNIT_READIED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBeatdownAndReadiesUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-beatdown-ready-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.False(result.FinalState.CardObjects["P1-BASE-UNIT-001"].IsExhausted);
        Assert.Single(result.EventKinds, kind => string.Equals(kind, "UNIT_READIED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHuntAndReadiesAllFriendlyUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hunt-ready-all-friendly-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.False(result.FinalState.CardObjects["P1-BASE-UNIT-001"].IsExhausted);
        Assert.False(result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P2-BASE-UNIT-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_READIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysOverchargedEnergyExhaustsFriendlyUnitsAndDamagesBattlefields()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-overcharged-energy-exhaust-friendly-damage-all-battlefield.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.True(result.FinalState.CardObjects["P1-BASE-UNIT-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.Equal(12, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Damage);
        Assert.DoesNotContain("P2-BATTLEFIELD-UNIT-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_EXHAUSTED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCannonBarrageDamagesOnlyEnemyCombatUnits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-cannon-barrage-damage-enemy-combat-units.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.CardObjects["P1-FRIENDLY-DEFENDER"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P2-ENEMY-ATTACKER"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P2-ENEMY-DEFENDER"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P2-ENEMY-IDLE"].Damage);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "DAMAGE_APPLIED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysProductionSurgeCreatesRobotAndDraws()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-production-surge-create-robot-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-SPELL-PRODUCTION-SURGE-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(3, result.FinalState.CardObjects["P1-SPELL-PRODUCTION-SURGE-TOKEN-001"].Power);
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Contains("UNIT_TOKEN_CREATED", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEngineReducesProductionSurgeWhenControllerHasMechanicalUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-production-surge-reduced-by-mechanical.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MECHANICAL-UNIT-001", "P1-SPELL-PRODUCTION-SURGE-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["机械"], result.FinalState.CardObjects["P1-MECHANICAL-UNIT-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCommonCauseCreatesFourMinionsInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-common-cause-create-four-minions-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-SPELL-COMMON-CAUSE-TOKEN-001",
                "P1-SPELL-COMMON-CAUSE-TOKEN-002",
                "P1-SPELL-COMMON-CAUSE-TOKEN-003",
                "P1-SPELL-COMMON-CAUSE-TOKEN-004"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            4,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
        Assert.All(result.FinalState.PlayerZones["P1"].Base, objectId =>
        {
            Assert.Equal(1, result.FinalState.CardObjects[objectId].Power);
            Assert.Equal(
                [CardObjectTags.UnitCard, CardObjectTags.MinionTokenFamily, "随从"],
                result.FinalState.CardObjects[objectId].Tags);
        });
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFeatherstormCreatesFourWarhawksWithSpellshield()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-featherstorm-create-warhawks.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-SPELL-FEATHERSTORM-TOKEN-001",
                "P1-SPELL-FEATHERSTORM-TOKEN-002",
                "P1-SPELL-FEATHERSTORM-TOKEN-003",
                "P1-SPELL-FEATHERSTORM-TOKEN-004"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            4,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
        Assert.All(result.FinalState.PlayerZones["P1"].Base, objectId =>
        {
            Assert.Equal(1, result.FinalState.CardObjects[objectId].Power);
            Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Spellshield, "鸟类"], result.FinalState.CardObjects[objectId].Tags);
        });
    }

    [Fact]
    public async Task CoreRuleEngineRecallsHighlanderBloodlineTargetWhenDestroyed()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-highlander-bloodline-recall-if-destroyed.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.True(result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.DoesNotContain("P1-BATTLEFIELD-UNIT-001", result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Contains("UNIT_RECALLED_TO_BASE", result.EventKinds);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRecallsTacticalRetreatTargetWhenDestroyed()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-tactical-retreat-recall-if-destroyed.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-BATTLEFIELD-UNIT-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Damage);
        Assert.True(result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].IsExhausted);
        Assert.DoesNotContain("P1-BATTLEFIELD-UNIT-001", result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Contains("UNIT_RECALLED_TO_BASE", result.EventKinds);
        Assert.Empty(result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysLastStandFriendlyPowerPlus3()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-last-stand-friendly-power-plus-3.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P1-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Contains("POWER_MODIFIED_UNTIL_END_OF_TURN", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysAnimalFriendsPowerPerControlledTag()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-animal-friends-power-per-controlled-tag.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].Power);
        Assert.Equal(3, result.FinalState.CardObjects["P2-BATTLEFIELD-UNIT-001"].UntilEndOfTurnPowerModifier);
        Assert.Contains("POWER_MODIFIED_UNTIL_END_OF_TURN", result.EventKinds);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysStandDefiantPowerPerEnemyBattlefieldUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-stand-defiant-power-per-enemy-battlefield-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(6, result.FinalState.CardObjects["P1-BATTLEFIELD-STAND-001"].Power);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BATTLEFIELD-STAND-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSandcraftWithEchoCreatesTwoSandSoldiersInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sandcraft-echo-create-two-sand-soldiers-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-SPELL-SANDCRAFT-TOKEN-001",
                "P1-SPELL-SANDCRAFT-TOKEN-002"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
        Assert.All(result.FinalState.PlayerZones["P1"].Base, objectId =>
            Assert.Equal(2, result.FinalState.CardObjects[objectId].Power));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSandcraftWithoutEchoCreatesOneSandSoldierInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sandcraft-create-one-sand-soldier-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-SPELL-SANDCRAFT-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(2, result.FinalState.CardObjects["P1-SPELL-SANDCRAFT-TOKEN-001"].Power);
        Assert.Single(result.EventKinds, kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysProtectTheEmperorCreatesSandSoldierInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-protect-the-emperor-create-sand-soldier.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-SPELL-PROTECT-EMPEROR-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(2, result.FinalState.CardObjects["P1-SPELL-PROTECT-EMPEROR-TOKEN-001"].Power);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSandSoldiersRiseReadiesTwoSandSoldiers()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sand-soldiers-rise-ready-two.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        // Retain the historical test identity; current errata excludes preexisting soldiers.
        Assert.Equal(
            0,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_READIED", StringComparison.Ordinal)));
        Assert.True(result.FinalState.CardObjects["P1-SAND-SOLDIER-001"].IsExhausted);
        Assert.True(result.FinalState.CardObjects["P1-SAND-SOLDIER-002"].IsExhausted);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSpriteSummonCreatesSpriteInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sprite-summon-create-sprite-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-SPELL-SPRITE-SUMMON-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(3, result.FinalState.CardObjects["P1-SPELL-SPRITE-SUMMON-TOKEN-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard, "仙灵", CardObjectTags.Ephemeral], result.FinalState.CardObjects["P1-SPELL-SPRITE-SUMMON-TOKEN-001"].Tags);
        Assert.Equal(
            1,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSpriteBurstCreatesTwoSpritesInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-sprite-burst-create-two-sprites-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            [
                "P1-SPELL-SPRITE-BURST-TOKEN-001",
                "P1-SPELL-SPRITE-BURST-TOKEN-002"
            ],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(
            2,
            result.EventKinds.Count(kind => string.Equals(kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal)));
        Assert.All(result.FinalState.PlayerZones["P1"].Base, objectId =>
        {
            Assert.Equal(3, result.FinalState.CardObjects[objectId].Power);
            Assert.Equal([CardObjectTags.UnitCard, "仙灵", CardObjectTags.Ephemeral], result.FinalState.CardObjects[objectId].Tags);
        });
    }

    [Fact]
    public async Task CoreRuleEnginePlaysMirrorImageCreatesEphemeralCopyInBase()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-mirror-image-copy-ephemeral-base.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-SPELL-MIRROR-IMAGE-TOKEN-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(3, result.FinalState.CardObjects["P1-SPELL-MIRROR-IMAGE-TOKEN-001"].Power);
        Assert.Equal("SFD·068/221", result.FinalState.CardObjects["P1-SPELL-MIRROR-IMAGE-TOKEN-001"].CardNo);
        Assert.False(result.FinalState.CardObjects["P1-SPELL-MIRROR-IMAGE-TOKEN-001"].IsExhausted);
        Assert.Contains(CardObjectTags.Ephemeral, result.FinalState.CardObjects["P1-SPELL-MIRROR-IMAGE-TOKEN-001"].Tags);
        Assert.DoesNotContain("机械", result.FinalState.CardObjects["P1-SPELL-MIRROR-IMAGE-TOKEN-001"].Tags);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, result.FinalState.CardObjects["P1-SPELL-MIRROR-IMAGE-TOKEN-001"].TokenFactoryCardNo);
        var tokenEvent = Assert.Single(result.Events, gameEvent => string.Equals(gameEvent.Kind, "UNIT_TOKEN_CREATED", StringComparison.Ordinal));
        Assert.Equal("P2-MIRROR-TARGET-001", tokenEvent.Payload["copiedTargetObjectId"]);
        Assert.Equal("SFD·068/221", tokenEvent.Payload["copiedCardNo"]);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, tokenEvent.Payload["tokenFactoryCardNo"]);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, tokenEvent.Payload["tokenCardNo"]);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsMirrorImageAgainstHandCard()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-MIRROR-IMAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HAND-MIRROR-IMAGE-TARGET"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-HAND-MIRROR-IMAGE-TARGET"] = new("P2-HAND-MIRROR-IMAGE-TARGET", power: 4)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-mirror-image-bad-target", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-MIRROR-IMAGE", "UNL-200/219", ["P2-HAND-MIRROR-IMAGE-TARGET"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Empty(result.State.StackItems);
    }

    [Theory]
    [InlineData("missing-card-no")]
    [InlineData("non-unit")]
    [InlineData("dirty-control")]
    [InlineData("face-down")]
    public async Task CoreRuleEngineRejectsMirrorImageInvalidCopySourceWithoutMutation(string caseName)
    {
        var baseTarget = new CardObjectState(
            "P2-MIRROR-TARGET-001",
            cardNo: "SFD·068/221",
            power: 4,
            tags: [CardObjectTags.UnitCard, "机械"],
            ownerId: "P2",
            controllerId: "P2");
        var target = caseName switch
        {
            "missing-card-no" => baseTarget with { CardNo = null },
            "non-unit" => baseTarget with { Tags = [CardObjectTags.EquipmentCard, "武装"] },
            "dirty-control" => baseTarget with { OwnerId = "P1", ControllerId = string.Empty },
            "face-down" => baseTarget with { IsFaceDown = true },
            _ => baseTarget
        };
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-MIRROR-IMAGE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-MIRROR-TARGET-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-MIRROR-IMAGE"] = new(
                    "P1-SPELL-MIRROR-IMAGE",
                    cardNo: "UNL-200/219",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 3,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-MIRROR-TARGET-001"] = target
            }
        };
        var initialHash = MatchStateHasher.Hash(state);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent($"intent-mirror-image-invalid-copy-{caseName}", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-SPELL-MIRROR-IMAGE", "UNL-200/219", ["P2-MIRROR-TARGET-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }
}
