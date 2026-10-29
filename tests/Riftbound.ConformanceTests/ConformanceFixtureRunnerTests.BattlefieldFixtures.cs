using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    // Current midgame initial data, prepared before the tested command/journal.
    private static MatchState P4PhysicalBattlefieldState(MatchState state)
    {
        const string field = "BATTLEFIELD:P1-MAIN";
        var cards = state.CardObjects.ToDictionary();
        var zones = state.PlayerZones.ToDictionary();
        var locations = new Dictionary<string, ObjectLocationState> { [field] = new("P1", "BATTLEFIELD", field) };
        cards[field] = new(field, cardNo: "OGN·275/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: "P1", controllerId: "P2");
        foreach (var (player, playerZones) in state.PlayerZones)
            foreach (var id in playerZones.Battlefields)
            {
                Assert.Contains(CardObjectTags.UnitCard, cards[id].Tags);
                cards[id] = cards[id] with { OwnerId = player, ControllerId = player };
                locations[id] = new(player, "BATTLEFIELD", field);
            }
        zones["P1"] = zones["P1"] with { Battlefields = [field, ..zones["P1"].Battlefields] };
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations,
            UntilEndOfTurnEffects = [..state.UntilEndOfTurnEffects, BattlefieldTaskMarkers.SpellDuelCompleted(field)] };
    }

    private static MatchState PunishmentState(int mana)
    {
        return new MatchState(
            "p2-punishment-room",
            0,
            7,
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
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PUNISHMENT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"],
                    Hand = ["P2-HAND-001"]
                }
            },
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-PUNISHMENT"] = new(
                    "P1-SPELL-PUNISHMENT",
                    cardNo: "UNL-007/219",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 2,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });
    }

    private static MatchState BaronNestMoveState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-ORIGIN",
                        "P1-BARON-NEST",
                        "P1-BARON-MOVER"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ORIGIN"] = new(
                    "P1-BATTLEFIELD-ORIGIN",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BARON-NEST"] = new(
                    "P1-BARON-NEST",
                    cardNo: P6TokenFactoryCatalog.BaronNestTokenCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BARON-MOVER"] = new(
                    "P1-BARON-MOVER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ORIGIN"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-ORIGIN"),
                ["P1-BARON-NEST"] = new("P1", "BATTLEFIELD", "P1-BARON-NEST"),
                ["P1-BARON-MOVER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-ORIGIN")
            }
        };
    }

    private static MatchState PostStackCleanupPowerState(
        string objectId,
        int power,
        int damage)
    {
        return new MatchState(
            "p7-post-stack-power-cleanup-room",
            1,
            1,
            "P2",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [objectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [objectId] = new(
                    objectId,
                    damage: damage,
                    power: power,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P2",
            passedPriorityPlayerIds: ["P1"],
            stackItems:
            [
                new StackItemState(
                    $"STACK-NOOP-{objectId}",
                    "P1",
                    "P1-SPELL-NOOP",
                    "UNKNOWN_NOOP_EFFECT")
            ],
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [objectId] = new("P1", "BASE")
            });
    }


    private static MatchState VexBattlefieldHoldState(
        string sourceCardNo,
        string sourceObjectId,
        bool legendExhausted)
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-VEX-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-VEX-DEFENDER"],
                    LegendZone = [sourceObjectId],
                    MainDeck = ["P2-VEX-DRAW-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-VEX-ATTACKER"] = new(
                    "P1-VEX-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-VEX-DEFENDER"] = new(
                    "P2-VEX-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: sourceCardNo,
                    isExhausted: legendExhausted,
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-VEX-DRAW-001"] = new(
                    "P2-VEX-DRAW-001",
                    cardNo: "UNL-001/219",
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState IreliaPendingFriendlyTargetState(
        string sourceCardNo,
        string sourceObjectId,
        bool targetFriendlyUnit)
    {
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana: 1);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-BATTLEFIELD-UNIT"] = cardObjects["P1-LEGEND-BATTLEFIELD-UNIT"] with
        {
            IsExhausted = true
        };
        cardObjects["P1-PENDING-SOURCE"] = new(
            "P1-PENDING-SOURCE",
            cardNo: "UNL-159/219",
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P2-NON-FRIENDLY-TARGET"] = new(
            "P2-NON-FRIENDLY-TARGET",
            power: 2,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P2",
            controllerId: "P2");
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P2"] = playerZones["P2"] with
        {
            Battlefields = ["P2-NON-FRIENDLY-TARGET"]
        };

        return state with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            PassedPriorityPlayerIds = [],
            StackItems =
            [
                new StackItemState(
                    "STACK-PENDING-IRELIA-TARGET",
                    "P1",
                    "P1-PENDING-SOURCE",
                    "PENDING_IRELIA_FRIENDLY_TARGET_TEST",
                    cardObjects["P1-PENDING-SOURCE"].CardNo,
                    targetFriendlyUnit ? ["P1-LEGEND-BATTLEFIELD-UNIT"] : ["P2-NON-FRIENDLY-TARGET"],
                    0)
            ],
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }


    private static MatchState RenataBattlefieldHoldState(
        string sourceCardNo,
        string sourceObjectId,
        bool legendExhausted,
        int score)
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-RENATA-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-RENATA-DEFENDER"],
                    LegendZone = [sourceObjectId]
                }
            },
            PlayerScores = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = score
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-RENATA-ATTACKER"] = new(
                    "P1-RENATA-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-RENATA-DEFENDER"] = new(
                    "P2-RENATA-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: sourceCardNo,
                    isExhausted: legendExhausted,
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState LeblancBattlefieldConquerState(
        string sourceCardNo,
        string sourceObjectId,
        bool hasDiscard)
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-LEBLANC-ATTACKER"],
                    Hand = hasDiscard ? ["P1-LEBLANC-DISCARD"] : [],
                    LegendZone = [sourceObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-LEBLANC-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEBLANC-ATTACKER"] = new(
                    "P1-LEBLANC-ATTACKER",
                    cardNo: "UNL-021/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt, "潜伏"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEBLANC-DISCARD"] = new(
                    "P1-LEBLANC-DISCARD",
                    cardNo: "UNL-001/219",
                    ownerId: "P1",
                    controllerId: "P1"),
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: sourceCardNo,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-LEBLANC-DEFENDER"] = new(
                    "P2-LEBLANC-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState LeblancBattlefieldHoldState(
        string sourceCardNo,
        string sourceObjectId,
        bool hasDiscard,
        bool legendExhausted)
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-LEBLANC-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-LEBLANC-DEFENDER"],
                    Hand = hasDiscard ? ["P2-LEBLANC-DISCARD"] : [],
                    LegendZone = [sourceObjectId]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEBLANC-ATTACKER"] = new(
                    "P1-LEBLANC-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-LEBLANC-DEFENDER"] = new(
                    "P2-LEBLANC-DEFENDER",
                    cardNo: "SFD·101/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "法盾"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-LEBLANC-DISCARD"] = new(
                    "P2-LEBLANC-DISCARD",
                    cardNo: "UNL-002/219",
                    ownerId: "P2",
                    controllerId: "P2"),
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: sourceCardNo,
                    isExhausted: legendExhausted,
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState IvernBattlefieldResultState(
        string sourceCardNo,
        string sourceObjectId,
        bool controllerHolds,
        bool legendExhausted)
    {
        var legendControllerId = controllerHolds ? "P2" : "P1";
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-IVERN-ATTACKER"],
                    LegendZone = controllerHolds ? [] : [sourceObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-IVERN-DEFENDER"],
                    LegendZone = controllerHolds ? [sourceObjectId] : []
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-IVERN-ATTACKER"] = new(
                    "P1-IVERN-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: controllerHolds ? 1 : 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-IVERN-DEFENDER"] = new(
                    "P2-IVERN-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: controllerHolds ? 4 : 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: sourceCardNo,
                    isExhausted: legendExhausted,
                    ownerId: legendControllerId,
                    controllerId: legendControllerId)
            }
        };
    }

    private static MatchState BattlefieldHeldDrawState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-HELD-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-BATTLEFIELD-DRAW-001"],
                    Battlefields = ["P2-BATTLEFIELD-DREAM-TREE", "P2-BATTLEFIELD-HELD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-HELD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-HELD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-DREAM-TREE"] = new(
                    "P2-BATTLEFIELD-DREAM-TREE",
                    cardNo: "OGN·280/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-HELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-HELD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState DunehornBeastHeldDrawState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-DUNEHORN-HELD-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-DUNEHORN-DRAW-001", "P2-DUNEHORN-DRAW-002"],
                    Battlefields = ["P2-DUNEHORN-BATTLEFIELD", "P2-DUNEHORN-BEAST"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-DUNEHORN-HELD-ATTACKER"] = new(
                    "P1-DUNEHORN-HELD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-DUNEHORN-BATTLEFIELD"] = new(
                    "P2-DUNEHORN-BATTLEFIELD",
                    cardNo: "SFD·209/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DUNEHORN-BEAST"] = new(
                    "P2-DUNEHORN-BEAST",
                    cardNo: "SFD·027/221",
                    power: 7,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DUNEHORN-DRAW-001"] = new(
                    "P2-DUNEHORN-DRAW-001",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DUNEHORN-DRAW-002"] = new(
                    "P2-DUNEHORN-DRAW-002",
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldBoonState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-BOON-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-NAVORI-ARENA", "P2-BATTLEFIELD-BOON-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-BOON-ATTACKER"] = new(
                    "P1-BATTLEFIELD-BOON-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-NAVORI-ARENA"] = new(
                    "P2-BATTLEFIELD-NAVORI-ARENA",
                    cardNo: "OGN·283/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-BOON-DEFENDER"] = new(
                    "P2-BATTLEFIELD-BOON-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldMoveToBaseState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-REHEARSAL-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-REHEARSAL-HALL", "P2-BATTLEFIELD-REHEARSAL-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-REHEARSAL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-REHEARSAL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-REHEARSAL-HALL"] = new(
                    "P2-BATTLEFIELD-REHEARSAL-HALL",
                    cardNo: "UNL-207/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-REHEARSAL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-REHEARSAL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldDefendMoveToBaseState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-PLUNDER-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-PLUNDER-ALLEY", "P2-BATTLEFIELD-PLUNDER-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-PLUNDER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-PLUNDER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-PLUNDER-ALLEY"] = new(
                    "P2-BATTLEFIELD-PLUNDER-ALLEY",
                    cardNo: "OGN·285/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-PLUNDER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-PLUNDER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldConquerBoonDrawState()
    {
        return PunishmentState(mana: 0) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P2-BATTLEFIELD-SHIRANA-MONASTERY")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P2-BATTLEFIELD-SHIRANA-MONASTERY"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-SHIRANA-MONASTERY"),
                ["P1-BATTLEFIELD-SHIRANA-ATTACKER"] = new("P1", "BATTLEFIELD", "P2-BATTLEFIELD-SHIRANA-MONASTERY"),
                ["P2-BATTLEFIELD-SHIRANA-DEFENDER"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-SHIRANA-MONASTERY")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-BATTLEFIELD-BOON-DRAW-CARD"],
                    Battlefields = ["P1-BATTLEFIELD-SHIRANA-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-SHIRANA-MONASTERY", "P2-BATTLEFIELD-SHIRANA-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SHIRANA-ATTACKER"] = new(
                    "P1-BATTLEFIELD-SHIRANA-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Boon, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-BOON-DRAW-CARD"] = new(
                    "P1-BATTLEFIELD-BOON-DRAW-CARD",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-SHIRANA-MONASTERY"] = new(
                    "P2-BATTLEFIELD-SHIRANA-MONASTERY",
                    cardNo: "OGN·282/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-SHIRANA-DEFENDER"] = new(
                    "P2-BATTLEFIELD-SHIRANA-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldConquerWarhawkState()
    {
        return PunishmentState(mana: 0) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P2-BATTLEFIELD-HUNTING-GROUNDS")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P2-BATTLEFIELD-HUNTING-GROUNDS"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-HUNTING-GROUNDS"),
                ["P1-BATTLEFIELD-HUNTING-ATTACKER"] = new("P1", "BATTLEFIELD", "P2-BATTLEFIELD-HUNTING-GROUNDS"),
                ["P2-BATTLEFIELD-HUNTING-DEFENDER"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-HUNTING-GROUNDS")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-HUNTING-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-HUNTING-GROUNDS", "P2-BATTLEFIELD-HUNTING-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-HUNTING-ATTACKER"] = new(
                    "P1-BATTLEFIELD-HUNTING-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-HUNTING-GROUNDS"] = new(
                    "P2-BATTLEFIELD-HUNTING-GROUNDS",
                    cardNo: "UNL-217/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-HUNTING-DEFENDER"] = new(
                    "P2-BATTLEFIELD-HUNTING-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldConquerMillState()
    {
        return PunishmentState(mana: 0) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-SCRAPYARD")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-SCRAPYARD"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SCRAPYARD"),
                ["P1-BATTLEFIELD-CONQUER-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SCRAPYARD"),
                ["P2-BATTLEFIELD-CONQUER-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-SCRAPYARD")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-BATTLEFIELD-MILL-001", "P1-BATTLEFIELD-MILL-002", "P1-BATTLEFIELD-MILL-003"],
                    Battlefields = ["P1-BATTLEFIELD-SCRAPYARD", "P1-BATTLEFIELD-CONQUER-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-CONQUER-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SCRAPYARD"] = new(
                    "P1-BATTLEFIELD-SCRAPYARD",
                    cardNo: "SFD·212/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-CONQUER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-CONQUER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-CONQUER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-CONQUER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldConquerDiscardDrawState()
    {
        return PunishmentState(mana: 0) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-ZAUN-SUMP")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-ZAUN-SUMP"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-ZAUN-SUMP"),
                ["P1-BATTLEFIELD-DISCARD-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-ZAUN-SUMP"),
                ["P2-BATTLEFIELD-DISCARD-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-ZAUN-SUMP")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-BATTLEFIELD-DRAW-001"],
                    Hand = ["P1-BATTLEFIELD-DISCARD-001"],
                    Battlefields = ["P1-BATTLEFIELD-ZAUN-SUMP", "P1-BATTLEFIELD-DISCARD-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-DISCARD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ZAUN-SUMP"] = new(
                    "P1-BATTLEFIELD-ZAUN-SUMP",
                    cardNo: "OGN·298/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-DISCARD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-DISCARD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-DISCARD-001"] = new(
                    "P1-BATTLEFIELD-DISCARD-001",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-DISCARD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DISCARD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    internal static MatchState BattlefieldConquerRecycleRuneState()
    {
        return PunishmentState(mana: 0) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-THUNDER-RUNE")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-THUNDER-RUNE"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-THUNDER-RUNE"),
                ["P1-BATTLEFIELD-RECYCLE-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-THUNDER-RUNE"),
                ["P2-BATTLEFIELD-RECYCLE-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-THUNDER-RUNE")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-MAIN-001"],
                    Base = ["P1-BATTLEFIELD-RECYCLE-RUNE-001"],
                    Battlefields = ["P1-BATTLEFIELD-THUNDER-RUNE", "P1-BATTLEFIELD-RECYCLE-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-RECYCLE-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-THUNDER-RUNE"] = new(
                    "P1-BATTLEFIELD-THUNDER-RUNE",
                    cardNo: "OGN·287/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-RECYCLE-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RECYCLE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-RECYCLE-RUNE-001"] = new(
                    "P1-BATTLEFIELD-RECYCLE-RUNE-001",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-RECYCLE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-RECYCLE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldDefendRevealSpellState(bool topCardIsSpell)
    {
        var topCardObjectId = topCardIsSpell
            ? "P2-BATTLEFIELD-REVEAL-SPELL"
            : "P2-BATTLEFIELD-REVEAL-UNIT";
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-REVEAL-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = [topCardObjectId, "P2-MAIN-001"],
                    Battlefields = ["P2-BATTLEFIELD-RAVENBLOOM", "P2-BATTLEFIELD-REVEAL-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-REVEAL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-REVEAL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-RAVENBLOOM"] = new(
                    "P2-BATTLEFIELD-RAVENBLOOM",
                    cardNo: "SFD·215/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-REVEAL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-REVEAL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-REVEAL-SPELL"] = new(
                    "P2-BATTLEFIELD-REVEAL-SPELL",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-REVEAL-UNIT"] = new(
                    "P2-BATTLEFIELD-REVEAL-UNIT",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldEphemeralSteadfastState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-EPHEMERAL-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-BLACK-FLAME", "P2-BATTLEFIELD-EPHEMERAL-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-EPHEMERAL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-EPHEMERAL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-BLACK-FLAME"] = new(
                    "P2-BATTLEFIELD-BLACK-FLAME",
                    cardNo: "UNL-208/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-EPHEMERAL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-EPHEMERAL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldDefenderSteadfastState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BATTLEFIELD-FORTIFIED-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-FORTIFIED-POSITION", "P2-BATTLEFIELD-FORTIFIED-DEFENDER"]
                }
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState> {
                ["P1-BATTLEFIELD-FORTIFIED-ATTACKER"] = new("P1", "BASE"),
                ["P2-BATTLEFIELD-FORTIFIED-POSITION"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-FORTIFIED-POSITION"),
                ["P2-BATTLEFIELD-FORTIFIED-DEFENDER"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-FORTIFIED-POSITION") },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-FORTIFIED-ATTACKER"] = new(
                    "P1-BATTLEFIELD-FORTIFIED-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-FORTIFIED-POSITION"] = new(
                    "P2-BATTLEFIELD-FORTIFIED-POSITION",
                    cardNo: "OGN·279/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-FORTIFIED-DEFENDER"] = new(
                    "P2-BATTLEFIELD-FORTIFIED-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldIsolatedDefenderState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-ISOLATED-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-FORBIDDEN-WASTELAND", "P2-BATTLEFIELD-ISOLATED-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ISOLATED-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ISOLATED-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-FORBIDDEN-WASTELAND"] = new(
                    "P2-BATTLEFIELD-FORBIDDEN-WASTELAND",
                    cardNo: "UNL-210/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-ISOLATED-DEFENDER"] = new(
                    "P2-BATTLEFIELD-ISOLATED-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldConquerReadyLegendState()
    {
        return PunishmentState(mana: 1) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-LEGEND-HALL")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-LEGEND-HALL"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-LEGEND-HALL"),
                ["P1-BATTLEFIELD-READY-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-LEGEND-HALL"),
                ["P2-BATTLEFIELD-READY-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-LEGEND-HALL")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-LEGEND-HALL", "P1-BATTLEFIELD-READY-ATTACKER"],
                    LegendZone = ["P1-LEGEND-READY-TARGET"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-READY-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-LEGEND-HALL"] = new(
                    "P1-BATTLEFIELD-LEGEND-HALL",
                    cardNo: "SFD·210/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-READY-ATTACKER"] = new(
                    "P1-BATTLEFIELD-READY-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-READY-TARGET"] = new(
                    "P1-LEGEND-READY-TARGET",
                    cardNo: "TEST-LEGEND-READY-TARGET",
                    isExhausted: true,
                    tags: ["CARD_TYPE:LEGEND"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-READY-DEFENDER"] = new(
                    "P2-BATTLEFIELD-READY-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldConquerReadyRunesEndState()
    {
        return PunishmentState(mana: 0) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-MOUNT-TARGON")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-MOUNT-TARGON"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-MOUNT-TARGON"),
                ["P1-BATTLEFIELD-RUNE-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-MOUNT-TARGON"),
                ["P2-BATTLEFIELD-RUNE-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-MOUNT-TARGON")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BATTLEFIELD-READY-RUNE-001", "P1-BATTLEFIELD-READY-RUNE-002"],
                    Battlefields = ["P1-BATTLEFIELD-MOUNT-TARGON", "P1-BATTLEFIELD-RUNE-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-MAIN-001"],
                    RuneDeck = ["P2-RUNE-001", "P2-RUNE-002"],
                    Battlefields = ["P2-BATTLEFIELD-RUNE-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MOUNT-TARGON"] = new(
                    "P1-BATTLEFIELD-MOUNT-TARGON",
                    cardNo: "OGN·289/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-RUNE-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RUNE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-RUNE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-RUNE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-READY-RUNE-001"] = new(
                    "P1-BATTLEFIELD-READY-RUNE-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-READY-RUNE-002"] = new(
                    "P1-BATTLEFIELD-READY-RUNE-002",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-MAIN-001"] = new(
                    "P2-MAIN-001",
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldConquerDrawOtherState()
    {
        return PunishmentState(mana: 0) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-THRONE-OF-POWER")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-THRONE-OF-POWER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-THRONE-OF-POWER"),
                ["P1-BATTLEFIELD-DRAW-OTHER-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-THRONE-OF-POWER"),
                ["P2-BATTLEFIELD-DRAW-OTHER-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-THRONE-OF-POWER")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck =
                    [
                        "P1-BATTLEFIELD-DRAW-OTHER-001",
                        "P1-BATTLEFIELD-DRAW-OTHER-002",
                        "P1-BATTLEFIELD-DRAW-OTHER-003"
                    ],
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-THRONE-OF-POWER",
                        "P1-BATTLEFIELD-OTHER-001",
                        "P1-BATTLEFIELD-OTHER-002",
                        "P1-BATTLEFIELD-DRAW-OTHER-ATTACKER"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-DRAW-OTHER-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-THRONE-OF-POWER"] = new(
                    "P1-BATTLEFIELD-THRONE-OF-POWER",
                    cardNo: "SFD·217/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-OTHER-001"] = new(
                    "P1-BATTLEFIELD-OTHER-001",
                    cardNo: "OGN·280/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-OTHER-002"] = new(
                    "P1-BATTLEFIELD-OTHER-002",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-DRAW-OTHER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-DRAW-OTHER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-DRAW-OTHER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DRAW-OTHER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-DRAW-OTHER-001"] = new(
                    "P1-BATTLEFIELD-DRAW-OTHER-001",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-DRAW-OTHER-002"] = new(
                    "P1-BATTLEFIELD-DRAW-OTHER-002",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-DRAW-OTHER-003"] = new(
                    "P1-BATTLEFIELD-DRAW-OTHER-003",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldConquerPowerfulDrawState()
    {
        return PunishmentState(mana: 1) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-SUNKEN-TEMPLE")],
            ObjectLocations = new Dictionary<string, ObjectLocationState> {
                ["P1-BATTLEFIELD-SUNKEN-TEMPLE"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SUNKEN-TEMPLE"),
                ["P1-BATTLEFIELD-POWERFUL-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SUNKEN-TEMPLE"),
                ["P2-BATTLEFIELD-POWERFUL-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-SUNKEN-TEMPLE")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-BATTLEFIELD-POWERFUL-DRAW-001", "P1-BATTLEFIELD-POWERFUL-DRAW-002"],
                    Battlefields = ["P1-BATTLEFIELD-SUNKEN-TEMPLE", "P1-BATTLEFIELD-POWERFUL-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-POWERFUL-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SUNKEN-TEMPLE"] = new(
                    "P1-BATTLEFIELD-SUNKEN-TEMPLE",
                    cardNo: "SFD·218/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-POWERFUL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-POWERFUL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-POWERFUL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-POWERFUL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-POWERFUL-DRAW-001"] = new(
                    "P1-BATTLEFIELD-POWERFUL-DRAW-001",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-POWERFUL-DRAW-002"] = new(
                    "P1-BATTLEFIELD-POWERFUL-DRAW-002",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldConquerPowerfulDrawMultipleAttackersState()
    {
        return PunishmentState(mana: 1) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-SUNKEN-TEMPLE")],
            ObjectLocations = new Dictionary<string, ObjectLocationState> {
                ["P1-BATTLEFIELD-SUNKEN-TEMPLE"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SUNKEN-TEMPLE"),
                ["P1-BATTLEFIELD-WEAK-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SUNKEN-TEMPLE"),
                ["P1-BATTLEFIELD-SECOND-POWERFUL-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-SUNKEN-TEMPLE"),
                ["P2-BATTLEFIELD-POWERFUL-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-SUNKEN-TEMPLE")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-BATTLEFIELD-POWERFUL-DRAW-001", "P1-BATTLEFIELD-POWERFUL-DRAW-002"],
                    Battlefields =
                    [
                        "P1-BATTLEFIELD-SUNKEN-TEMPLE",
                        "P1-BATTLEFIELD-WEAK-ATTACKER",
                        "P1-BATTLEFIELD-SECOND-POWERFUL-ATTACKER"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-POWERFUL-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SUNKEN-TEMPLE"] = new(
                    "P1-BATTLEFIELD-SUNKEN-TEMPLE",
                    cardNo: "SFD·218/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-WEAK-ATTACKER"] = new(
                    "P1-BATTLEFIELD-WEAK-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-SECOND-POWERFUL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-SECOND-POWERFUL-ATTACKER",
                    cardNo: "SFD·126/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-POWERFUL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-POWERFUL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-POWERFUL-DRAW-001"] = new(
                    "P1-BATTLEFIELD-POWERFUL-DRAW-001",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-POWERFUL-DRAW-002"] = new(
                    "P1-BATTLEFIELD-POWERFUL-DRAW-002",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldConquerGoldState()
    {
        return PunishmentState(mana: 1) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-TREASURE-PILE")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-TREASURE-PILE"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-TREASURE-PILE"),
                ["P1-BATTLEFIELD-GOLD-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-TREASURE-PILE"),
                ["P2-BATTLEFIELD-GOLD-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-TREASURE-PILE")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-TREASURE-PILE", "P1-BATTLEFIELD-GOLD-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-GOLD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-TREASURE-PILE"] = new(
                    "P1-BATTLEFIELD-TREASURE-PILE",
                    cardNo: "SFD·220/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-GOLD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-GOLD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-GOLD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-GOLD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldConquerSandSoldierState(int mana = 1)
    {
        return PunishmentState(mana: mana) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-EMPEROR-SHRINE")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-EMPEROR-SHRINE"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-EMPEROR-SHRINE"),
                ["P1-BATTLEFIELD-SAND-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-EMPEROR-SHRINE"),
                ["P2-BATTLEFIELD-SAND-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-EMPEROR-SHRINE")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-EMPEROR-SHRINE", "P1-BATTLEFIELD-SAND-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-SAND-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-EMPEROR-SHRINE"] = new(
                    "P1-BATTLEFIELD-EMPEROR-SHRINE",
                    cardNo: "SFD·207/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-SAND-ATTACKER"] = new(
                    "P1-BATTLEFIELD-SAND-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-SAND-DEFENDER"] = new(
                    "P2-BATTLEFIELD-SAND-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldConquerReadyEquipmentState()
    {
        return PunishmentState(mana: 0) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-MOONVEIL-ALTAR")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-MOONVEIL-ALTAR"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-MOONVEIL-ALTAR"),
                ["P1-BATTLEFIELD-EQUIPMENT-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-MOONVEIL-ALTAR"),
                ["P2-BATTLEFIELD-EQUIPMENT-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-MOONVEIL-ALTAR")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BATTLEFIELD-EQUIPMENT-HOST", "P1-BATTLEFIELD-ARMAMENT"],
                    Battlefields = ["P1-BATTLEFIELD-MOONVEIL-ALTAR", "P1-BATTLEFIELD-EQUIPMENT-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-EQUIPMENT-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MOONVEIL-ALTAR"] = new(
                    "P1-BATTLEFIELD-MOONVEIL-ALTAR",
                    cardNo: "SFD·221/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-EQUIPMENT-ATTACKER"] = new(
                    "P1-BATTLEFIELD-EQUIPMENT-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-EQUIPMENT-HOST"] = new(
                    "P1-BATTLEFIELD-EQUIPMENT-HOST",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-ARMAMENT"] = new(
                    "P1-BATTLEFIELD-ARMAMENT",
                    cardNo: "SFD·022/221",
                    isExhausted: true,
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    attachedToObjectId: "P1-BATTLEFIELD-EQUIPMENT-HOST",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-EQUIPMENT-DEFENDER"] = new(
                    "P2-BATTLEFIELD-EQUIPMENT-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldMinionState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-MINION-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNITY-SANCTUM", "P2-BATTLEFIELD-MINION-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MINION-ATTACKER"] = new(
                    "P1-BATTLEFIELD-MINION-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-UNITY-SANCTUM"] = new(
                    "P2-BATTLEFIELD-UNITY-SANCTUM",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-MINION-DEFENDER"] = new(
                    "P2-BATTLEFIELD-MINION-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldRunesState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    RuneDeck = ["P1-BATTLEFIELD-RUNE-001"],
                    Battlefields = ["P1-BATTLEFIELD-RUNES-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    RuneDeck = ["P2-BATTLEFIELD-RUNE-001"],
                    Battlefields = ["P2-BATTLEFIELD-CONFETTI-TREE", "P2-BATTLEFIELD-RUNES-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-RUNES-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RUNES-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-CONFETTI-TREE"] = new(
                    "P2-BATTLEFIELD-CONFETTI-TREE",
                    cardNo: "SFD·219/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-RUNES-DEFENDER"] = new(
                    "P2-BATTLEFIELD-RUNES-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldRuneState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    RuneDeck = ["P1-RUNE-001"],
                    Battlefields = ["P1-BATTLEFIELD-SINGLE-RUNE-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    RuneDeck = ["P2-BATTLEFIELD-SINGLE-RUNE-001"],
                    Battlefields = ["P2-BATTLEFIELD-STAR-PEAK", "P2-BATTLEFIELD-SINGLE-RUNE-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SINGLE-RUNE-ATTACKER"] = new(
                    "P1-BATTLEFIELD-SINGLE-RUNE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-STAR-PEAK"] = new(
                    "P2-BATTLEFIELD-STAR-PEAK",
                    cardNo: "OGN·288/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-SINGLE-RUNE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-SINGLE-RUNE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldStaticPowerState(
        bool battlefieldSourceFaceDown = false,
        bool defenderFaceDown = false)
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-POWER-PLUS", "P1-BATTLEFIELD-STATIC-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-STATIC-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-POWER-PLUS"] = new(
                    "P1-BATTLEFIELD-POWER-PLUS",
                    cardNo: "OGN·294/298",
                    isFaceDown: battlefieldSourceFaceDown,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-STATIC-ATTACKER"] = new(
                    "P1-BATTLEFIELD-STATIC-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-STATIC-DEFENDER"] = new(
                    "P2-BATTLEFIELD-STATIC-DEFENDER",
                    cardNo: "SFD·125/221",
                    isFaceDown: defenderFaceDown,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState SameBattlefieldOtherFriendlyStaticPowerState(
        bool sourceStandby = false,
        bool allyStandby = false)
    {
        static IReadOnlyList<string> UnitTags(bool standby)
        {
            return standby
                ? [CardObjectTags.UnitCard, CardObjectTags.Standby]
                : [CardObjectTags.UnitCard];
        }

        static IReadOnlyList<string> EliteUnitTags(bool standby)
        {
            return standby
                ? [CardObjectTags.UnitCard, CardObjectTags.Standby, "精锐"]
                : [CardObjectTags.UnitCard, "精锐"];
        }

        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-GAREN-BATTLEFIELD",
                        "P1-GAREN-SOURCE",
                        "P1-GAREN-ALLY",
                        "P1-GAREN-OTHER-BATTLEFIELD",
                        "P1-GAREN-OTHER-BATTLEFIELD-ALLY"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-GAREN-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-GAREN-BATTLEFIELD"] = new(
                    "P1-GAREN-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GAREN-SOURCE"] = new(
                    "P1-GAREN-SOURCE",
                    cardNo: "OGS·013/024",
                    power: 5,
                    isFaceDown: sourceStandby,
                    tags: EliteUnitTags(sourceStandby),
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GAREN-ALLY"] = new(
                    "P1-GAREN-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    isFaceDown: allyStandby,
                    tags: UnitTags(allyStandby),
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GAREN-OTHER-BATTLEFIELD"] = new(
                    "P1-GAREN-OTHER-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GAREN-OTHER-BATTLEFIELD-ALLY"] = new(
                    "P1-GAREN-OTHER-BATTLEFIELD-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: UnitTags(standby: false),
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-GAREN-DEFENDER"] = new(
                    "P2-GAREN-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: UnitTags(standby: false),
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-GAREN-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-GAREN-BATTLEFIELD"),
                ["P1-GAREN-SOURCE"] = new("P1", "BATTLEFIELD", "P1-GAREN-BATTLEFIELD"),
                ["P1-GAREN-ALLY"] = new("P1", "BATTLEFIELD", "P1-GAREN-BATTLEFIELD"),
                ["P2-GAREN-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-GAREN-BATTLEFIELD"),
                ["P1-GAREN-OTHER-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-GAREN-OTHER-BATTLEFIELD"),
                ["P1-GAREN-OTHER-BATTLEFIELD-ALLY"] = new("P1", "BATTLEFIELD", "P1-GAREN-OTHER-BATTLEFIELD")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-GAREN-BATTLEFIELD")]
        };
    }

    private static MatchState SameBattlefieldOtherFriendlyFilteredStaticPowerState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-LEE-SIN-BATTLEFIELD",
                        "P1-LEE-SIN-SOURCE",
                        "P1-LEE-SIN-BOON-ALLY",
                        "P1-LEE-SIN-NONBOON-ALLY",
                        "P1-LEE-SIN-OTHER-BATTLEFIELD",
                        "P1-LEE-SIN-OTHER-BATTLEFIELD-BOON-ALLY"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-LEE-SIN-DEFENDER", "P2-LEE-SIN-BOON-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEE-SIN-BATTLEFIELD"] = new(
                    "P1-LEE-SIN-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEE-SIN-SOURCE"] = new(
                    "P1-LEE-SIN-SOURCE",
                    cardNo: "OGN·151/298",
                    power: 6,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Boon],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEE-SIN-BOON-ALLY"] = new(
                    "P1-LEE-SIN-BOON-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Boon],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEE-SIN-NONBOON-ALLY"] = new(
                    "P1-LEE-SIN-NONBOON-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEE-SIN-OTHER-BATTLEFIELD"] = new(
                    "P1-LEE-SIN-OTHER-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEE-SIN-OTHER-BATTLEFIELD-BOON-ALLY"] = new(
                    "P1-LEE-SIN-OTHER-BATTLEFIELD-BOON-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Boon],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-LEE-SIN-DEFENDER"] = new(
                    "P2-LEE-SIN-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-LEE-SIN-BOON-DEFENDER"] = new(
                    "P2-LEE-SIN-BOON-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Boon],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-LEE-SIN-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-LEE-SIN-BATTLEFIELD"),
                ["P1-LEE-SIN-SOURCE"] = new("P1", "BATTLEFIELD", "P1-LEE-SIN-BATTLEFIELD"),
                ["P1-LEE-SIN-BOON-ALLY"] = new("P1", "BATTLEFIELD", "P1-LEE-SIN-BATTLEFIELD"),
                ["P1-LEE-SIN-NONBOON-ALLY"] = new("P1", "BATTLEFIELD", "P1-LEE-SIN-BATTLEFIELD"),
                ["P2-LEE-SIN-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-LEE-SIN-BATTLEFIELD"),
                ["P2-LEE-SIN-BOON-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-LEE-SIN-BATTLEFIELD"),
                ["P1-LEE-SIN-OTHER-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-LEE-SIN-OTHER-BATTLEFIELD"),
                ["P1-LEE-SIN-OTHER-BATTLEFIELD-BOON-ALLY"] = new("P1", "BATTLEFIELD", "P1-LEE-SIN-OTHER-BATTLEFIELD")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-LEE-SIN-BATTLEFIELD")]
        };
    }

    private static MatchState BattlefieldFilteredStaticPowerState(
        bool battlefieldSourceFaceDown = false,
        bool catDefenderFaceDown = false)
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-BRUSH-BATTLEFIELD",
                        "P1-BRUSH-BIRD-ATTACKER",
                        "P1-BRUSH-IVERN-ALLY",
                        "P1-BRUSH-NONMATCHING-ALLY",
                        "P1-BRUSH-OTHER-BATTLEFIELD",
                        "P1-BRUSH-OTHER-BATTLEFIELD-PORO"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BRUSH-CAT-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BRUSH-BATTLEFIELD"] = new(
                    "P1-BRUSH-BATTLEFIELD",
                    cardNo: "UNL·T03",
                    isFaceDown: battlefieldSourceFaceDown,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag, "草丛"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BRUSH-BIRD-ATTACKER"] = new(
                    "P1-BRUSH-BIRD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "鸟类"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BRUSH-IVERN-ALLY"] = new(
                    "P1-BRUSH-IVERN-ALLY",
                    cardNo: "UNL-051/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BRUSH-NONMATCHING-ALLY"] = new(
                    "P1-BRUSH-NONMATCHING-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "机械"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BRUSH-OTHER-BATTLEFIELD"] = new(
                    "P1-BRUSH-OTHER-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BRUSH-OTHER-BATTLEFIELD-PORO"] = new(
                    "P1-BRUSH-OTHER-BATTLEFIELD-PORO",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "魄罗"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BRUSH-CAT-DEFENDER"] = new(
                    "P2-BRUSH-CAT-DEFENDER",
                    cardNo: "SFD·125/221",
                    isFaceDown: catDefenderFaceDown,
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "猫科"],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BRUSH-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-BRUSH-BATTLEFIELD"),
                ["P1-BRUSH-BIRD-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BRUSH-BATTLEFIELD"),
                ["P1-BRUSH-IVERN-ALLY"] = new("P1", "BATTLEFIELD", "P1-BRUSH-BATTLEFIELD"),
                ["P1-BRUSH-NONMATCHING-ALLY"] = new("P1", "BATTLEFIELD", "P1-BRUSH-BATTLEFIELD"),
                ["P2-BRUSH-CAT-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BRUSH-BATTLEFIELD"),
                ["P1-BRUSH-OTHER-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-BRUSH-OTHER-BATTLEFIELD"),
                ["P1-BRUSH-OTHER-BATTLEFIELD-PORO"] = new("P1", "BATTLEFIELD", "P1-BRUSH-OTHER-BATTLEFIELD")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BRUSH-BATTLEFIELD")]
        };
    }

    private static MatchState BattlefieldFilteredStaticKeywordState(
        bool battlefieldSourceFaceDown = false,
        bool ephemeralDefenderFaceDown = false)
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P1-BLACKFLAME-BATTLEFIELD",
                        "P1-BLACKFLAME-ATTACKER",
                        "P1-BLACKFLAME-EPHEMERAL-ALLY",
                        "P1-BLACKFLAME-NONMATCHING-ALLY",
                        "P1-BLACKFLAME-STANDBY-ALLY",
                        "P1-BLACKFLAME-OTHER-BATTLEFIELD",
                        "P1-BLACKFLAME-OTHER-BATTLEFIELD-EPHEMERAL"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BLACKFLAME-EPHEMERAL-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BLACKFLAME-BATTLEFIELD"] = new(
                    "P1-BLACKFLAME-BATTLEFIELD",
                    cardNo: "UNL-208/219",
                    isFaceDown: battlefieldSourceFaceDown,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BLACKFLAME-ATTACKER"] = new(
                    "P1-BLACKFLAME-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BLACKFLAME-EPHEMERAL-ALLY"] = new(
                    "P1-BLACKFLAME-EPHEMERAL-ALLY",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BLACKFLAME-NONMATCHING-ALLY"] = new(
                    "P1-BLACKFLAME-NONMATCHING-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BLACKFLAME-STANDBY-ALLY"] = new(
                    "P1-BLACKFLAME-STANDBY-ALLY",
                    isFaceDown: true,
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral, CardObjectTags.Standby],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BLACKFLAME-OTHER-BATTLEFIELD"] = new(
                    "P1-BLACKFLAME-OTHER-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BLACKFLAME-OTHER-BATTLEFIELD-EPHEMERAL"] = new(
                    "P1-BLACKFLAME-OTHER-BATTLEFIELD-EPHEMERAL",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BLACKFLAME-EPHEMERAL-DEFENDER"] = new(
                    "P2-BLACKFLAME-EPHEMERAL-DEFENDER",
                    cardNo: "SFD·125/221",
                    isFaceDown: ephemeralDefenderFaceDown,
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BLACKFLAME-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-BLACKFLAME-BATTLEFIELD"),
                ["P1-BLACKFLAME-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BLACKFLAME-BATTLEFIELD"),
                ["P1-BLACKFLAME-EPHEMERAL-ALLY"] = new("P1", "BATTLEFIELD", "P1-BLACKFLAME-BATTLEFIELD"),
                ["P1-BLACKFLAME-NONMATCHING-ALLY"] = new("P1", "BATTLEFIELD", "P1-BLACKFLAME-BATTLEFIELD"),
                ["P1-BLACKFLAME-STANDBY-ALLY"] = new("P1", "BATTLEFIELD", "P1-BLACKFLAME-BATTLEFIELD"),
                ["P2-BLACKFLAME-EPHEMERAL-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BLACKFLAME-BATTLEFIELD"),
                ["P1-BLACKFLAME-OTHER-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-BLACKFLAME-OTHER-BATTLEFIELD"),
                ["P1-BLACKFLAME-OTHER-BATTLEFIELD-EPHEMERAL"] = new(
                    "P1",
                    "BATTLEFIELD",
                    "P1-BLACKFLAME-OTHER-BATTLEFIELD")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BLACKFLAME-BATTLEFIELD")]
        };
    }

    private static MatchState OtherFriendlyStaticPowerState(
        bool sourceStandby = false,
        bool battlefieldAllyStandby = false)
    {
        static IReadOnlyList<string> UnitTags(bool standby)
        {
            return standby
                ? [CardObjectTags.UnitCard, CardObjectTags.Standby]
                : [CardObjectTags.UnitCard];
        }

        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = ["P1-BARON-SOURCE", "P1-BARON-BASE-ALLY"],
                    Battlefields = ["P1-BARON-BATTLEFIELD", "P1-BARON-ALLY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BARON-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BARON-SOURCE"] = new(
                    "P1-BARON-SOURCE",
                    cardNo: "UNL-147/219",
                    power: 12,
                    isFaceDown: sourceStandby,
                    tags: UnitTags(sourceStandby),
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BARON-BASE-ALLY"] = new(
                    "P1-BARON-BASE-ALLY",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: UnitTags(standby: false),
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BARON-BATTLEFIELD"] = new(
                    "P1-BARON-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BARON-ALLY"] = new(
                    "P1-BARON-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    isFaceDown: battlefieldAllyStandby,
                    tags: UnitTags(battlefieldAllyStandby),
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BARON-DEFENDER"] = new(
                    "P2-BARON-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: UnitTags(standby: false),
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BARON-SOURCE"] = new("P1", "BASE"),
                ["P1-BARON-BASE-ALLY"] = new("P1", "BASE"),
                ["P1-BARON-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-BARON-BATTLEFIELD"),
                ["P1-BARON-ALLY"] = new("P1", "BATTLEFIELD", "P1-BARON-BATTLEFIELD"),
                ["P2-BARON-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BARON-BATTLEFIELD")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BARON-BATTLEFIELD")]
        };
    }

    private static MatchState FriendlyFilteredStaticPowerState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base =
                    [
                        "P1-RUMBLE-SOURCE",
                        "P1-SOUL-SHEPHERD-SOURCE",
                        "P1-SOUL-SHEPHERD-TOKEN",
                        "P1-RUMBLE-NONMECH-BASE"
                    ],
                    Battlefields =
                    [
                        "P1-RUMBLE-BATTLEFIELD",
                        "P1-RUMBLE-MECH-ALLY"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-RUMBLE-DEFENDER", "P2-RUMBLE-MECH-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-RUMBLE-SOURCE"] = new(
                    "P1-RUMBLE-SOURCE",
                    cardNo: "SFD·089/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "机械", "约德尔人"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SOUL-SHEPHERD-SOURCE"] = new(
                    "P1-SOUL-SHEPHERD-SOURCE",
                    cardNo: "UNL-077/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SOUL-SHEPHERD-TOKEN"] = new(
                    "P1-SOUL-SHEPHERD-TOKEN",
                    cardNo: "UNL·T02",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield, "鸟类"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUMBLE-NONMECH-BASE"] = new(
                    "P1-RUMBLE-NONMECH-BASE",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUMBLE-BATTLEFIELD"] = new(
                    "P1-RUMBLE-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUMBLE-MECH-ALLY"] = new(
                    "P1-RUMBLE-MECH-ALLY",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "机械"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-RUMBLE-DEFENDER"] = new(
                    "P2-RUMBLE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-RUMBLE-MECH-DEFENDER"] = new(
                    "P2-RUMBLE-MECH-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "机械"],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-RUMBLE-SOURCE"] = new("P1", "BASE"),
                ["P1-SOUL-SHEPHERD-SOURCE"] = new("P1", "BASE"),
                ["P1-SOUL-SHEPHERD-TOKEN"] = new("P1", "BASE"),
                ["P1-RUMBLE-NONMECH-BASE"] = new("P1", "BASE"),
                ["P1-RUMBLE-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-RUMBLE-BATTLEFIELD"),
                ["P1-RUMBLE-MECH-ALLY"] = new("P1", "BATTLEFIELD", "P1-RUMBLE-BATTLEFIELD"),
                ["P2-RUMBLE-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-RUMBLE-BATTLEFIELD"),
                ["P2-RUMBLE-MECH-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-RUMBLE-BATTLEFIELD")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-RUMBLE-BATTLEFIELD")]
        };
    }

    private static MatchState FriendlyFilteredStaticKeywordState(
        bool rumbleHeroSourceFaceDown = false,
        bool rumbleMechanicalAllyFaceDown = false,
        bool includeRumbleLegend = true)
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base =
                    [
                        "P1-RUMBLE-HERO-SOURCE",
                        "P1-LILLIA-SOURCE",
                        "P1-LILLIA-TOKEN",
                        "P1-RUMBLE-NONMECH-BASE"
                    ],
                    Battlefields =
                    [
                        "P1-RUMBLE-BATTLEFIELD",
                        "P1-RUMBLE-MECH-ALLY"
                    ],
                    LegendZone = includeRumbleLegend ? ["P1-RUMBLE-LEGEND"] : []
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-LILLIA-TOKEN"],
                    Battlefields = ["P2-RUMBLE-DEFENDER", "P2-RUMBLE-MECH-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-RUMBLE-HERO-SOURCE"] = new(
                    "P1-RUMBLE-HERO-SOURCE",
                    cardNo: "SFD·026/221",
                    isFaceDown: rumbleHeroSourceFaceDown,
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "机械", "约德尔人"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LILLIA-SOURCE"] = new(
                    "P1-LILLIA-SOURCE",
                    cardNo: "UNL-058/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "仙灵"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LILLIA-TOKEN"] = new(
                    "P1-LILLIA-TOKEN",
                    cardNo: "UNL·T07",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "仙灵"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUMBLE-NONMECH-BASE"] = new(
                    "P1-RUMBLE-NONMECH-BASE",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUMBLE-LEGEND"] = new(
                    "P1-RUMBLE-LEGEND",
                    cardNo: "SFD·181/221",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUMBLE-BATTLEFIELD"] = new(
                    "P1-RUMBLE-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUMBLE-MECH-ALLY"] = new(
                    "P1-RUMBLE-MECH-ALLY",
                    cardNo: "SFD·125/221",
                    isFaceDown: rumbleMechanicalAllyFaceDown,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "机械"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-RUMBLE-DEFENDER"] = new(
                    "P2-RUMBLE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-RUMBLE-MECH-DEFENDER"] = new(
                    "P2-RUMBLE-MECH-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "机械"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-LILLIA-TOKEN"] = new(
                    "P2-LILLIA-TOKEN",
                    cardNo: "UNL·T07",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "仙灵"],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-RUMBLE-HERO-SOURCE"] = new("P1", "BASE"),
                ["P1-LILLIA-SOURCE"] = new("P1", "BASE"),
                ["P1-LILLIA-TOKEN"] = new("P1", "BASE"),
                ["P1-RUMBLE-NONMECH-BASE"] = new("P1", "BASE"),
                ["P1-RUMBLE-LEGEND"] = new("P1", "LEGEND"),
                ["P1-RUMBLE-BATTLEFIELD"] = new("P1", "BATTLEFIELD", "P1-RUMBLE-BATTLEFIELD"),
                ["P1-RUMBLE-MECH-ALLY"] = new("P1", "BATTLEFIELD", "P1-RUMBLE-BATTLEFIELD"),
                ["P2-RUMBLE-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-RUMBLE-BATTLEFIELD"),
                ["P2-RUMBLE-MECH-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-RUMBLE-BATTLEFIELD"),
                ["P2-LILLIA-TOKEN"] = new("P2", "BASE")
            },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-RUMBLE-BATTLEFIELD")]
        };
    }

    private static MatchState FriendlyFilteredMultiKeywordStaticAuraState()
    {
        return PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-INCINERATE"],
                    Base =
                    [
                        "P1-SPEEDING-MECH-SOURCE",
                        "P1-SPEEDING-NONMECH"
                    ],
                    Battlefields =
                    [
                        "P1-SPEEDING-ORIGIN",
                        "P1-SPEEDING-MECH-ROAMER"
                    ]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-SPEEDING-MECH-SOURCE"],
                    Battlefields =
                    [
                        "P2-SPEEDING-BATTLEFIELD",
                        "P2-SPEEDING-MECH-TARGET",
                        "P2-SPEEDING-NONMECH-TARGET"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPEEDING-MECH-SOURCE"] = new(
                    "P1-SPEEDING-MECH-SOURCE",
                    cardNo: "SFD·071/221",
                    power: 7,
                    tags: [CardObjectTags.UnitCard, "机械", "约德尔人"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SPEEDING-NONMECH"] = new(
                    "P1-SPEEDING-NONMECH",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SPEEDING-ORIGIN"] = new(
                    "P1-SPEEDING-ORIGIN",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SPEEDING-MECH-ROAMER"] = new(
                    "P1-SPEEDING-MECH-ROAMER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "机械"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-SPEEDING-MECH-SOURCE"] = new(
                    "P2-SPEEDING-MECH-SOURCE",
                    cardNo: "SFD·071/221",
                    power: 7,
                    tags: [CardObjectTags.UnitCard, "机械", "约德尔人"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-SPEEDING-BATTLEFIELD"] = new(
                    "P2-SPEEDING-BATTLEFIELD",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-SPEEDING-MECH-TARGET"] = new(
                    "P2-SPEEDING-MECH-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "机械"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-SPEEDING-NONMECH-TARGET"] = new(
                    "P2-SPEEDING-NONMECH-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-INCINERATE"] = new("P1", "HAND"),
                ["P1-SPEEDING-MECH-SOURCE"] = new("P1", "BASE"),
                ["P1-SPEEDING-NONMECH"] = new("P1", "BASE"),
                ["P1-SPEEDING-ORIGIN"] = new("P1", "BATTLEFIELD", "P1-SPEEDING-ORIGIN"),
                ["P1-SPEEDING-MECH-ROAMER"] = new("P1", "BATTLEFIELD", "P1-SPEEDING-ORIGIN"),
                ["P2-SPEEDING-MECH-SOURCE"] = new("P2", "BASE"),
                ["P2-SPEEDING-BATTLEFIELD"] = new("P2", "BATTLEFIELD", "P2-SPEEDING-BATTLEFIELD"),
                ["P2-SPEEDING-MECH-TARGET"] = new("P2", "BATTLEFIELD", "P2-SPEEDING-BATTLEFIELD"),
                ["P2-SPEEDING-NONMECH-TARGET"] = new("P2", "BATTLEFIELD", "P2-SPEEDING-BATTLEFIELD")
            }
        };
    }
}
