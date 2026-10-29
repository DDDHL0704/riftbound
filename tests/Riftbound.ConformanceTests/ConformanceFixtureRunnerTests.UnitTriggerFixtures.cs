using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    private static MatchState BattlefieldStaticRoamState()
    {
        return PunishmentState(mana: 0) with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-WIND-HILL"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-WIND-HILL"),
                ["P1-BATTLEFIELD-WIND-RUNNER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-WIND-HILL")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-WIND-HILL", "P1-BATTLEFIELD-WIND-RUNNER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-WIND-HILL"] = new(
                    "P1-BATTLEFIELD-WIND-HILL",
                    cardNo: "OGN·297/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-WIND-RUNNER"] = new(
                    "P1-BATTLEFIELD-WIND-RUNNER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState SharpshooterPirateBattleState(bool sharpshooterAttacks)
    {
        var p1Battlefields = sharpshooterAttacks
            ? new[] { "P1-SHARPSHOOTER-PIRATE" }
            : new[] { "P1-SHARPSHOOTER-ATTACKER" };
        var p2Battlefields = sharpshooterAttacks
            ? new[] { "P2-SHARPSHOOTER-DEFENDER" }
            : new[] { "P2-SHARPSHOOTER-PIRATE" };
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        if (sharpshooterAttacks)
        {
            cardObjects["P1-SHARPSHOOTER-PIRATE"] = new(
                "P1-SHARPSHOOTER-PIRATE",
                cardNo: "OGN·130/298",
                power: 3,
                tags: [CardObjectTags.UnitCard, "海盗"],
                ownerId: "P1",
                controllerId: "P1");
            cardObjects["P2-SHARPSHOOTER-DEFENDER"] = new(
                "P2-SHARPSHOOTER-DEFENDER",
                cardNo: "SFD·125/221",
                power: 5,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2");
        }
        else
        {
            cardObjects["P1-SHARPSHOOTER-ATTACKER"] = new(
                "P1-SHARPSHOOTER-ATTACKER",
                cardNo: "SFD·125/221",
                power: 1,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1");
            cardObjects["P2-SHARPSHOOTER-PIRATE"] = new(
                "P2-SHARPSHOOTER-PIRATE",
                cardNo: "OGN·130/298",
                power: 3,
                tags: [CardObjectTags.UnitCard, "海盗"],
                ownerId: "P2",
                controllerId: "P2");
        }

        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = p1Battlefields
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            CardObjects = cardObjects
        };
    }

    private static MatchState DuneDrakeBattleState(bool duneDrakeAttacks)
    {
        var p1Battlefields = duneDrakeAttacks
            ? new[] { "P1-DUNE-DRAKE" }
            : new[] { "P1-DUNE-ATTACKER" };
        var p2Battlefields = duneDrakeAttacks
            ? new[] { "P2-DUNE-DEFENDER" }
            : new[] { "P2-DUNE-DRAKE" };
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        if (duneDrakeAttacks)
        {
            cardObjects["P1-DUNE-DRAKE"] = new(
                "P1-DUNE-DRAKE",
                cardNo: "OGN·131/298",
                power: 5,
                tags: [CardObjectTags.UnitCard, "龙"],
                ownerId: "P1",
                controllerId: "P1");
            cardObjects["P2-DUNE-DEFENDER"] = new(
                "P2-DUNE-DEFENDER",
                cardNo: "SFD·125/221",
                power: 10,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2");
        }
        else
        {
            cardObjects["P1-DUNE-ATTACKER"] = new(
                "P1-DUNE-ATTACKER",
                cardNo: "SFD·125/221",
                power: 1,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1");
            cardObjects["P2-DUNE-DRAKE"] = new(
                "P2-DUNE-DRAKE",
                cardNo: "OGN·131/298",
                power: 5,
                tags: [CardObjectTags.UnitCard, "龙"],
                ownerId: "P2",
                controllerId: "P2");
        }

        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = p1Battlefields
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            CardObjects = cardObjects
        };
    }

    private static MatchState WithFaceDownStandby(MatchState state, string objectId)
    {
        state = WithStandbyTag(state, objectId);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            [objectId] = state.CardObjects[objectId] with { IsFaceDown = true }
        }};
    }

    private static MatchState WithStandbyTag(MatchState state, string objectId)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal);
        cardObjects[objectId] = cardObjects[objectId] with
        {
            Tags = cardObjects[objectId]
                .Tags
                .Append(CardObjectTags.Standby)
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
        return state with
        {
            CardObjects = cardObjects
        };
    }

    private static void AssertNoStaticPowerBonus(GameEvent gameEvent)
    {
        if (gameEvent.Payload.TryGetValue("staticPowerBonus", out var value))
        {
            Assert.Equal(0, value);
        }
    }

    private static MatchState BilgewaterBullyBoonRoamState(bool hasBoon)
    {
        string[] bullyTags = hasBoon
            ? [CardObjectTags.UnitCard, CardObjectTags.Boon]
            : [CardObjectTags.UnitCard];
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BILGEWATER-DOCK", "P1-BILGEWATER-BULLY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BILGEWATER-DOCK"] = new(
                    "P1-BILGEWATER-DOCK",
                    cardNo: "TEST-BATTLEFIELD",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BILGEWATER-BULLY"] = new(
                    "P1-BILGEWATER-BULLY",
                    cardNo: "OGN·125/298",
                    power: 6,
                    tags: bullyTags,
                    ownerId: "P1",
                    controllerId: "P1")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BILGEWATER-DOCK"] = new("P1", "BATTLEFIELD", "P1-BILGEWATER-DOCK"),
                ["P1-BILGEWATER-BULLY"] = new("P1", "BATTLEFIELD", "P1-BILGEWATER-DOCK")
            }
        };
    }

    private static MatchState RudePirateOptionalDiscardState(int mana)
    {
        return PunishmentState(mana) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-RUDE-PIRATE", "P1-RUDE-PIRATE-DISCARD-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-RUDE-PIRATE"] = new(
                    "P1-UNIT-RUDE-PIRATE",
                    cardNo: "OGN·002/298",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, "海盗"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-RUDE-PIRATE-DISCARD-001"] = new(
                    "P1-RUDE-PIRATE-DISCARD-001",
                    cardNo: "SFD·126/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState RagingDrakePlayState()
    {
        return PunishmentState(mana: 6) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-RAGING-DRAKE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-RAGING-DRAKE"] = new(
                    "P1-UNIT-RAGING-DRAKE",
                    cardNo: "OGN·031/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "龙"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState RagingDrakeNextSpellCostReductionState(int mana)
    {
        return PunishmentState(mana) with
        {
            UntilEndOfTurnEffects =
            [
                "RAGING_DRAKE_NEXT_SPELL_COST_REDUCTION:P1:P1-UNIT-RAGING-DRAKE"
            ]
        };
    }

    private static MatchState PoroHerderWithPoroState()
    {
        return PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-PORO-HERDER-DRAW"],
                    Hand = ["P1-UNIT-PORO-HERDER"],
                    Base = ["P1-UNIT-PLUCKY-PORO"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-PORO-HERDER"] = new(
                    "P1-UNIT-PORO-HERDER",
                    cardNo: "OGN·061/298",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-PLUCKY-PORO"] = new(
                    "P1-UNIT-PLUCKY-PORO",
                    cardNo: "UNL-220/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "魄罗", "法盾"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-PORO-HERDER-DRAW"] = new(
                    "P1-PORO-HERDER-DRAW",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState MountainApeElderBoonReadyState(bool alreadyHasBoon)
    {
        string[] tags = alreadyHasBoon
            ? [CardObjectTags.UnitCard, CardObjectTags.Boon]
            : [CardObjectTags.UnitCard];
        return PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SECRET-ART-MERCY"],
                    Base = ["P1-UNIT-MOUNTAIN-APE-ELDER"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SECRET-ART-MERCY"] = new("P1-SPELL-SECRET-ART-MERCY", cardNo: "OGN·053/298",
                    ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]),
                ["P1-UNIT-MOUNTAIN-APE-ELDER"] = new(
                    "P1-UNIT-MOUNTAIN-APE-ELDER",
                    cardNo: "SFD·047/221",
                    power: alreadyHasBoon ? 6 : 5,
                    tags: tags,
                    isExhausted: true,
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState RoyalAttendantLegendModeState(bool initialLegendExhausted)
    {
        return PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-ROYAL-ATTENDANT"],
                    LegendZone = ["P1-LEGEND-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    LegendZone = ["P2-LEGEND-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ROYAL-ATTENDANT"] = new(
                    "P1-UNIT-ROYAL-ATTENDANT",
                    cardNo: "SFD·039/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-001"] = new(
                    "P1-LEGEND-001",
                    cardNo: "FND-259/298",
                    isExhausted: initialLegendExhausted,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-LEGEND-001"] = new(
                    "P2-LEGEND-001",
                    cardNo: "OGN·257/298",
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState OrnnEquipmentLookState(string cardNo, string sourceObjectId)
    {
        return PunishmentState(mana: 5) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck =
                    [
                        "P1-ORNN-EQUIPMENT-001",
                        "P1-ORNN-UNIT-001",
                        "P1-ORNN-SPELL-001",
                        "P1-ORNN-EQUIPMENT-002",
                        "P1-ORNN-DECK-TAIL-001"
                    ],
                    Hand = [sourceObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: cardNo,
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-ORNN-EQUIPMENT-001"] = new(
                    "P1-ORNN-EQUIPMENT-001",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-ORNN-UNIT-001"] = new(
                    "P1-ORNN-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-ORNN-SPELL-001"] = new(
                    "P1-ORNN-SPELL-001",
                    cardNo: "OGN·058/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-ORNN-EQUIPMENT-002"] = new(
                    "P1-ORNN-EQUIPMENT-002",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState EclipseVanguardStunTriggerState(bool stunEnemy)
    {
        string[] p1Base = stunEnemy
            ? ["P1-UNIT-ECLIPSE-VANGUARD"]
            : ["P1-UNIT-ECLIPSE-VANGUARD", "P1-FRIENDLY-STUN-TARGET"];
        string[] p2Base = stunEnemy
            ? ["P2-ECLIPSE-STUN-TARGET"]
            : [];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-UNIT-SUN-SHIELDGUARD"] = new(
                "P1-UNIT-SUN-SHIELDGUARD",
                cardNo: "OGN·051/298",
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1"),
            ["P1-UNIT-ECLIPSE-VANGUARD"] = new(
                "P1-UNIT-ECLIPSE-VANGUARD",
                cardNo: "OGN·059/298",
                power: 7,
                tags: [CardObjectTags.UnitCard, "鸟类"],
                isExhausted: true,
                ownerId: "P1",
                controllerId: "P1")
        };

        if (stunEnemy)
        {
            cardObjects["P2-ECLIPSE-STUN-TARGET"] = new(
                "P2-ECLIPSE-STUN-TARGET",
                cardNo: "SFD·125/221",
                power: 2,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2");
        }
        else
        {
            cardObjects["P1-FRIENDLY-STUN-TARGET"] = new(
                "P1-FRIENDLY-STUN-TARGET",
                cardNo: "SFD·125/221",
                power: 2,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1");
        }

        return PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-SUN-SHIELDGUARD"],
                    Base = p1Base
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = p2Base
                }
            },
            CardObjects = cardObjects
        };
    }

    private static MatchState RavenbloomStudentSpellTriggerState()
    {
        return PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PRACTICAL-EXPERIENCE"],
                    Base = ["P1-UNIT-RAVENBLOOM-STUDENT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-RAVENBLOOM-STUDENT"] = new(
                    "P1-UNIT-RAVENBLOOM-STUDENT",
                    cardNo: "OGN·103/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SPELL-PRACTICAL-EXPERIENCE"] = new(
                    "P1-SPELL-PRACTICAL-EXPERIENCE",
                    cardNo: "UNL-031/219",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-UNIT-001"] = new(
                    "P2-BATTLEFIELD-UNIT-001",
                    cardNo: "UNL-097/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState DianaSpellTriggerState(string dianaCardNo)
    {
        return PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PRACTICAL-EXPERIENCE"],
                    Base = ["P1-UNIT-DIANA"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-DIANA"] = new(
                    "P1-UNIT-DIANA",
                    cardNo: dianaCardNo,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SPELL-PRACTICAL-EXPERIENCE"] = new(
                    "P1-SPELL-PRACTICAL-EXPERIENCE",
                    cardNo: "UNL-031/219",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-UNIT-001"] = new(
                    "P2-BATTLEFIELD-UNIT-001",
                    cardNo: "UNL-097/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState WiseElderBoonBattleState(bool hasBoon)
    {
        string[] wiseTags = hasBoon
            ? [CardObjectTags.UnitCard, CardObjectTags.Boon]
            : [CardObjectTags.UnitCard];

        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-WISE-ELDER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-WISE-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-WISE-ELDER"] = new(
                    "P1-WISE-ELDER",
                    cardNo: "OGN·065/298",
                    power: 4,
                    tags: wiseTags,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-WISE-DEFENDER"] = new(
                    "P2-WISE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 10,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState WaterbenderBattleState(bool isAttacking, bool lone)
    {
        string[] p1Battlefields = isAttacking
            ? lone ? ["P1-WATERBENDER"] : ["P1-WATERBENDER", "P1-WATERBENDER-ALLY"]
            : ["P1-WATERBENDER-ATTACKER"];
        string[] p2Battlefields = isAttacking
            ? ["P2-WATERBENDER-DEFENDER"]
            : lone ? ["P2-WATERBENDER"] : ["P2-WATERBENDER", "P2-WATERBENDER-ALLY"];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal);
        if (isAttacking)
        {
            cardObjects["P1-WATERBENDER"] = new(
                "P1-WATERBENDER",
                cardNo: "OGN·055/298",
                power: 2,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1");
            cardObjects["P2-WATERBENDER-DEFENDER"] = new(
                "P2-WATERBENDER-DEFENDER",
                cardNo: "SFD·125/221",
                power: 10,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2");
            if (!lone)
            {
                cardObjects["P1-WATERBENDER-ALLY"] = new(
                    "P1-WATERBENDER-ALLY",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1");
            }
        }
        else
        {
            cardObjects["P1-WATERBENDER-ATTACKER"] = new(
                "P1-WATERBENDER-ATTACKER",
                cardNo: "SFD·125/221",
                power: 10,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1");
            cardObjects["P2-WATERBENDER"] = new(
                "P2-WATERBENDER",
                cardNo: "OGN·055/298",
                power: 2,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2");
            if (!lone)
            {
                cardObjects["P2-WATERBENDER-ALLY"] = new(
                    "P2-WATERBENDER-ALLY",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2");
            }
        }

        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = p1Battlefields
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = p2Battlefields
                }
            },
            CardObjects = cardObjects
        };
    }

    private static MatchState BattlefieldStaticPreventMoveBaseState()
    {
        return PunishmentState(mana: 0) with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-VILEMAW-LAIR"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-VILEMAW-LAIR"),
                ["P1-BATTLEFIELD-TRAPPED-UNIT"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-VILEMAW-LAIR")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-VILEMAW-LAIR", "P1-BATTLEFIELD-TRAPPED-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-VILEMAW-LAIR"] = new(
                    "P1-BATTLEFIELD-VILEMAW-LAIR",
                    cardNo: "OGN·295/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-TRAPPED-UNIT"] = new(
                    "P1-BATTLEFIELD-TRAPPED-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldMovePowerState()
    {
        return PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralOpen, PriorityPlayerId = null, PassedPriorityPlayerIds = [],
            ObjectLocations = new Dictionary<string, ObjectLocationState> {
                ["P1-BATTLEFIELD-BACK-ALLEY-BAR"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-BACK-ALLEY-BAR"),
                ["P1-BATTLEFIELD-BAR-REGULAR"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-BACK-ALLEY-BAR") },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-BACK-ALLEY-BAR", "P1-BATTLEFIELD-BAR-REGULAR"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-BACK-ALLEY-BAR"] = new(
                    "P1-BATTLEFIELD-BACK-ALLEY-BAR",
                    cardNo: "OGN·277/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-BAR-REGULAR"] = new(
                    "P1-BATTLEFIELD-BAR-REGULAR",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldPreventPlayUnitsState()
    {
        return PunishmentState(mana: 3) with
        {
            TimingState = TimingStates.NeutralOpen,
            PriorityPlayerId = null,
            PassedPriorityPlayerIds = [],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-FALLING-ROCKS"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-FALLING-ROCKS"),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-FALLING-ROCKS")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-HAND-UNL-GLOOMY-APOTHECARY"],
                    Battlefields = ["P1-BATTLEFIELD-FALLING-ROCKS", "P1-BATTLEFIELD-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNL-GLOOMY-APOTHECARY"] = new(
                    "P1-HAND-UNL-GLOOMY-APOTHECARY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 3,
                    cardNo: "OGN·012/298"),
                ["P1-BATTLEFIELD-FALLING-ROCKS"] = new(
                    "P1-BATTLEFIELD-FALLING-ROCKS",
                    cardNo: "SFD·216/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            StackItems = []
        };
    }

    private static MatchState BattlefieldEchoCostReductionState()
    {
        return PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-CENTER-STAGE"],
                    MainDeck = ["P1-MAIN-001", "P1-MAIN-002"],
                    Battlefields = ["P1-BATTLEFIELD-MARAI-SPIRE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-CENTER-STAGE"] = new(
                    "P1-SPELL-CENTER-STAGE",
                    cardNo: "UNL-061/219",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-MARAI-SPIRE"] = new(
                    "P1-BATTLEFIELD-MARAI-SPIRE",
                    cardNo: "SFD·211/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldEquipmentCostReductionState()
    {
        return PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-LONG-SWORD"],
                    Base = ["P1-UNIT-EQUIPMENT-COST-TARGET"],
                    Battlefields = ["P1-BATTLEFIELD-ORNN-FORGE"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-EQUIPMENT-COST-TARGET"] = new(
                    "P1-UNIT-EQUIPMENT-COST-TARGET",
                    power: 3,
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-ORNN-FORGE"] = new(
                    "P1-BATTLEFIELD-ORNN-FORGE",
                    cardNo: "SFD·213/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldEagerApprenticeSpellCostReductionState()
    {
        return PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-WELL-TRAINED"],
                    MainDeck = ["P1-DRAW-001"],
                    Battlefields = ["P1-UNIT-EAGER-APPRENTICE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-WELL-TRAINED"] = new(
                    "P1-SPELL-WELL-TRAINED",
                    cardNo: "OGN·058/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-UNIT-EAGER-APPRENTICE"] = new(
                    "P1-UNIT-EAGER-APPRENTICE",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "OGN·084/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            }
        };
    }

    private static MatchState ArenaServiceCrewEquipmentReadyState(string playerId = "P1")
    {
        var p1Hand = string.Equals(playerId, "P1", StringComparison.Ordinal)
            ? new[] { "P1-EQUIPMENT-LONG-SWORD" }
            : [];
        var p2Hand = string.Equals(playerId, "P2", StringComparison.Ordinal)
            ? new[] { "P2-EQUIPMENT-LONG-SWORD" }
            : [];

        return PunishmentState(mana: 2) with
        {
            ActivePlayerId = playerId,
            TurnPlayerId = playerId,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = string.Equals(playerId, "P1", StringComparison.Ordinal) ? new RunePool(2, 0) : RunePool.Empty,
                ["P2"] = string.Equals(playerId, "P2", StringComparison.Ordinal) ? new RunePool(2, 0) : RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = p1Hand,
                    Base = ["P1-UNIT-ARENA-SERVICE-CREW"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = p2Hand,
                    Base = string.Equals(playerId, "P2", StringComparison.Ordinal)
                        ? ["P2-UNIT-EQUIPMENT-TARGET"]
                        : []
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ARENA-SERVICE-CREW"] = new(
                    "P1-UNIT-ARENA-SERVICE-CREW",
                    cardNo: "OGN·091/298",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    isExhausted: true,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-UNIT-EQUIPMENT-TARGET"] = new(
                    "P2-UNIT-EQUIPMENT-TARGET",
                    power: 3,
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState AddArenaEquipmentTarget(MatchState state)
    {
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal);
        var p1Zones = playerZones["P1"];
        playerZones["P1"] = p1Zones with
        {
            Base = p1Zones.Base
                .Append("P1-UNIT-EQUIPMENT-TARGET")
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };

        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            ["P1-UNIT-EQUIPMENT-TARGET"] = new(
                "P1-UNIT-EQUIPMENT-TARGET",
                cardNo: "SFD·125/221",
                power: 3,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1")
        };

        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    internal static MatchState BattlefieldFriendlySpellDrawState()
    {
        return PunishmentState(mana: 2) with
        {
            ObjectLocations = new Dictionary<string,ObjectLocationState> { ["P1-BATTLEFIELD-DREAMTREE"] = new("P1","BATTLEFIELD","P1-BATTLEFIELD-DREAMTREE"), ["P1-BATTLEFIELD-ALLY"] = new("P1","BATTLEFIELD","P1-BATTLEFIELD-DREAMTREE") },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-MAIN-DRAWN"],
                    Hand = ["P1-SPELL-SAVAGE-STRENGTH"],
                    Battlefields = ["P1-BATTLEFIELD-DREAMTREE", "P1-BATTLEFIELD-ALLY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SAVAGE-STRENGTH"] = new(
                    "P1-SPELL-SAVAGE-STRENGTH",
                    cardNo: "SFD·034/221",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-MAIN-DRAWN"] = new(
                    "P1-MAIN-DRAWN",
                    cardNo: "SFD·001/221",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-ALLY"] = new(
                    "P1-BATTLEFIELD-ALLY",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1",
                    power: 2),
                ["P1-BATTLEFIELD-DREAMTREE"] = new(
                    "P1-BATTLEFIELD-DREAMTREE",
                    cardNo: "OGN·292/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    internal static MatchState BattlefieldSpellPowerBonusState()
    {
        return PunishmentState(mana: 2) with
        {
            ObjectLocations = new Dictionary<string,ObjectLocationState> { ["P1-BATTLEFIELD-WASTE-HALL"] = new("P1","BATTLEFIELD","P1-BATTLEFIELD-WASTE-HALL"), ["P1-BATTLEFIELD-ALLY"] = new("P1","BATTLEFIELD","P1-BATTLEFIELD-WASTE-HALL") },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-SAVAGE-STRENGTH"],
                    Battlefields = ["P1-BATTLEFIELD-WASTE-HALL", "P1-BATTLEFIELD-ALLY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-SAVAGE-STRENGTH"] = new(
                    "P1-SPELL-SAVAGE-STRENGTH",
                    cardNo: "SFD·034/221",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-ALLY"] = new(
                    "P1-BATTLEFIELD-ALLY",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1",
                    power: 2),
                ["P1-BATTLEFIELD-WASTE-HALL"] = new(
                    "P1-BATTLEFIELD-WASTE-HALL",
                    cardNo: "UNL-205/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldHighCostSpellInsightState(
        int mana = 7,
        string sourceObjectId = "P1-SPELL-MOONFALL",
        string cardNo = "UNL-066/219")
    {
        return PunishmentState(mana: mana) with
        {
            ObjectLocations = new Dictionary<string,ObjectLocationState> { ["P1-BATTLEFIELD-LOST-LIBRARY"] = new("P1","BATTLEFIELD","P1-BATTLEFIELD-LOST-LIBRARY") },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-INSIGHT-RECYCLE", "P1-MAIN-KEEPER"],
                    Hand = [sourceObjectId],
                    Battlefields = ["P1-BATTLEFIELD-LOST-LIBRARY"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-ENEMY"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: cardNo,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-INSIGHT-RECYCLE"] = new(
                    "P1-INSIGHT-RECYCLE",
                    cardNo: "SFD·001/221",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-MAIN-KEEPER"] = new(
                    "P1-MAIN-KEEPER",
                    cardNo: "SFD·002/221",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-ENEMY"] = new(
                    "P2-BATTLEFIELD-ENEMY",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2",
                    power: 2),
                ["P1-BATTLEFIELD-LOST-LIBRARY"] = new(
                    "P1-BATTLEFIELD-LOST-LIBRARY",
                    cardNo: "UNL-211/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldUnitExperienceAbilityState(bool includeMutationGarden = true)
    {
        return PunishmentState(mana: 0) with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = 0
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-MUTATION-GARDEN"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-MUTATION-GARDEN"),
                ["P1-BATTLEFIELD-EXPERIENCE-UNIT"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-MUTATION-GARDEN")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = includeMutationGarden
                        ? ["P1-BATTLEFIELD-MUTATION-GARDEN", "P1-BATTLEFIELD-EXPERIENCE-UNIT"]
                        : ["P1-BATTLEFIELD-EXPERIENCE-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MUTATION-GARDEN"] = new(
                    "P1-BATTLEFIELD-MUTATION-GARDEN",
                    cardNo: "UNL-213/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-EXPERIENCE-UNIT"] = new(
                    "P1-BATTLEFIELD-EXPERIENCE-UNIT",
                    cardNo: "SFD·001/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldReturnCallRuneState()
    {
        return PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-RECONSIDER"],
                    RuneDeck = ["P1-GHOST-BAY-RUNE-001", "P1-GHOST-BAY-RUNE-002"],
                    Battlefields = ["P1-BATTLEFIELD-GHOST-BAY", "P1-BATTLEFIELD-RETURN-UNIT"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-RECONSIDER"] = new(
                    "P1-SPELL-RECONSIDER",
                    cardNo: "OGN·104/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-GHOST-BAY"] = new(
                    "P1-BATTLEFIELD-GHOST-BAY",
                    cardNo: "UNL-214/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-RETURN-UNIT"] = new(
                    "P1-BATTLEFIELD-RETURN-UNIT",
                    cardNo: "SFD·001/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GHOST-BAY-RUNE-001"] = new(
                    "P1-GHOST-BAY-RUNE-001",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-GHOST-BAY-RUNE-002"] = new(
                    "P1-GHOST-BAY-RUNE-002",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldTargetDamageBonusState(bool includeVoidGate = true)
    {
        return PunishmentState(mana: 2) with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P2-BATTLEFIELD-VOID-GATE"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-VOID-GATE"),
                ["P2-BATTLEFIELD-TARGET"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-VOID-GATE")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-PUNISHMENT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = includeVoidGate
                        ? ["P2-BATTLEFIELD-VOID-GATE", "P2-BATTLEFIELD-TARGET"]
                        : ["P2-BATTLEFIELD-TARGET"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-PUNISHMENT"] = new(
                    "P1-SPELL-PUNISHMENT",
                    cardNo: "UNL-007/219",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-TARGET"] = new(
                    "P2-BATTLEFIELD-TARGET",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2",
                    power: 5),
                ["P2-BATTLEFIELD-VOID-GATE"] = new(
                    "P2-BATTLEFIELD-VOID-GATE",
                    cardNo: "OGN·296/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2")
            }
                .Where(entry => includeVoidGate || !string.Equals(entry.Key, "P2-BATTLEFIELD-VOID-GATE", StringComparison.Ordinal))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
        };
    }

    private static MatchState BattlefieldHeldUnitCostIncreaseBattleState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-HELIA-VAULT", "P2-BATTLEFIELD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ATTACKER",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1",
                    power: 1),
                ["P2-BATTLEFIELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DEFENDER",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2",
                    power: 3),
                ["P2-BATTLEFIELD-HELIA-VAULT"] = new(
                    "P2-BATTLEFIELD-HELIA-VAULT",
                    cardNo: "UNL-219/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldUnitCostIncreasePlayState()
    {
        return PunishmentState(mana: 4) with
        {
            UntilEndOfTurnEffects = ["BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:P1"],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-CRAFTSMAN"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-CRAFTSMAN"] = new(
                    "P1-UNIT-CRAFTSMAN",
                    cardNo: "OGN·211/298",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldPlayUnitBoonState(int mana)
    {
        return PunishmentState(mana) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-CRAFTSMAN"],
                    Battlefields = ["P1-BATTLEFIELD-IDOL-VALLEY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-CRAFTSMAN"] = new(
                    "P1-UNIT-CRAFTSMAN",
                    cardNo: "OGN·211/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-IDOL-VALLEY"] = new(
                    "P1-BATTLEFIELD-IDOL-VALLEY",
                    cardNo: "UNL-218/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }

    private static MatchState BattlefieldFirstUnitMoveOtherState()
    {
        return PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-CRAFTSMAN"],
                    Battlefields = ["P1-BATTLEFIELD-METEOR-SPRING", "P1-BATTLEFIELD-ALLY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-CRAFTSMAN"] = new(
                    "P1-UNIT-CRAFTSMAN",
                    cardNo: "OGN·211/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-METEOR-SPRING"] = new(
                    "P1-BATTLEFIELD-METEOR-SPRING",
                    cardNo: "UNL-215/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-ALLY"] = new(
                    "P1-BATTLEFIELD-ALLY",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1",
                    power: 2)
            }
        };
    }

    private static MatchState BattlefieldWinningScoreState(int score = 7)
    {
        return PunishmentState(mana: 0) with
        {
            TurnNumber = 117,
            ActivePlayerId = "P1",
            TurnPlayerId = "P1",
            PlayerScores = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = score,
                ["P2"] = 0
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-HIGHROAD"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Graveyard = ["P2-BURNOUT-RECYCLE-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-HIGHROAD"] = new(
                    "P1-BATTLEFIELD-HIGHROAD",
                    cardNo: "OGN·276/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BURNOUT-RECYCLE-001"] = new(
                    "P2-BURNOUT-RECYCLE-001",
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldFirstTurnRuneState()
    {
        return PunishmentState(mana: 0) with
        {
            TurnNumber = 1,
            ActivePlayerId = "P1",
            TurnPlayerId = "P1",
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-RUNE-OBELISK"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-MAIN-001"],
                    RuneDeck = ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003", "P2-RUNE-004"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-RUNE-OBELISK"] = new(
                    "P1-BATTLEFIELD-RUNE-OBELISK",
                    cardNo: "OGN·284/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-MAIN-001"] = new(
                    "P2-MAIN-001",
                    ownerId: "P2",
                    controllerId: "P2"),
            }
        };
    }

    private static MatchState BattlefieldFirstTurnScoreState()
    {
        return PunishmentState(mana: 0) with
        {
            TurnNumber = 1,
            ActivePlayerId = "P1",
            TurnPlayerId = "P1",
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GLORY-ARENA"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-MAIN-001"],
                    RuneDeck = ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GLORY-ARENA"] = new(
                    "P1-BATTLEFIELD-GLORY-ARENA",
                    cardNo: "OGN·290/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-MAIN-001"] = new(
                    "P2-MAIN-001",
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldTurnStartDamageState()
    {
        return PunishmentState(mana: 0) with
        {
            TurnNumber = 1,
            ActivePlayerId = "P1",
            TurnPlayerId = "P1",
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-FROST-HOLD", "P1-BATTLEFIELD-FROST-FALLING"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-MAIN-001"],
                    RuneDeck = ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003"],
                    Battlefields = ["P2-BATTLEFIELD-FROST-OTHER", "P2-BATTLEFIELD-FROST-SURVIVOR"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-FROST-HOLD"] = new(
                    "P1-BATTLEFIELD-FROST-HOLD",
                    cardNo: "UNL-212/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-FROST-FALLING"] = new(
                    "P1-BATTLEFIELD-FROST-FALLING",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-FROST-OTHER"] = new("P2-BATTLEFIELD-FROST-OTHER", cardNo: "OGN·294/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: "P2"),
                ["P2-BATTLEFIELD-FROST-SURVIVOR"] = new(
                    "P2-BATTLEFIELD-FROST-SURVIVOR",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-FROST-HOLD"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-FROST-HOLD"),
                ["P1-BATTLEFIELD-FROST-FALLING"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-FROST-HOLD"),
                ["P2-BATTLEFIELD-FROST-OTHER"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-FROST-OTHER"),
                ["P2-BATTLEFIELD-FROST-SURVIVOR"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-FROST-OTHER"),
                ["P2-MAIN-001"] = new("P2", "MAIN_DECK"),
                ["P2-RUNE-001"] = new("P2", "RUNE_DECK"),
                ["P2-RUNE-002"] = new("P2", "RUNE_DECK"),
                ["P2-RUNE-003"] = new("P2", "RUNE_DECK")
            }
        };
    }

    private static MatchState BattlefieldTurnStartDestroyDrawState()
    {
        return PunishmentState(mana: 0) with
        {
            TurnNumber = 1,
            ActivePlayerId = "P1",
            TurnPlayerId = "P1",
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with { Battlefields = ["P2-OTHER"] },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-ROSE-DRAW-001", "P2-NORMAL-DRAW-001"],
                    RuneDeck = ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003"],
                    Battlefields = ["P2-BATTLEFIELD-AAA-OFFSITE", "P2-BATTLEFIELD-ROSE-LAB", "P2-BATTLEFIELD-ROSE-SACRIFICE"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-OTHER"] = new("P2-OTHER", cardNo: "OGN·294/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P1", controllerId: "P2"),
                ["P2-BATTLEFIELD-AAA-OFFSITE"] = new(
                    "P2-BATTLEFIELD-AAA-OFFSITE",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-ROSE-LAB"] = new(
                    "P2-BATTLEFIELD-ROSE-LAB",
                    cardNo: "UNL-209/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-ROSE-SACRIFICE"] = new(
                    "P2-BATTLEFIELD-ROSE-SACRIFICE",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-ROSE-DRAW-001"] = new(
                    "P2-ROSE-DRAW-001",
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-NORMAL-DRAW-001"] = new(
                    "P2-NORMAL-DRAW-001",
                    ownerId: "P2",
                    controllerId: "P2")
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P2-OTHER"] = new("P1", "BATTLEFIELD", "P2-OTHER"),
                ["P2-BATTLEFIELD-AAA-OFFSITE"] = new("P2", "BATTLEFIELD", "P2-OTHER"),
                ["P2-BATTLEFIELD-ROSE-LAB"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-ROSE-LAB"),
                ["P2-BATTLEFIELD-ROSE-SACRIFICE"] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-ROSE-LAB"),
                ["P2-ROSE-DRAW-001"] = new("P2", "MAIN_DECK"),
                ["P2-NORMAL-DRAW-001"] = new("P2", "MAIN_DECK"),
                ["P2-RUNE-001"] = new("P2", "RUNE_DECK"),
                ["P2-RUNE-002"] = new("P2", "RUNE_DECK"),
                ["P2-RUNE-003"] = new("P2", "RUNE_DECK")
            }
        };
    }

    private static MatchState BattlefieldHeldScoreState()
    {
        return PunishmentState(mana: 0) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(0, 4)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-ENERGY-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-ENERGY-HUB", "P2-BATTLEFIELD-ENERGY-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ENERGY-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ENERGY-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-ENERGY-HUB"] = new(
                    "P2-BATTLEFIELD-ENERGY-HUB",
                    cardNo: "SFD·214/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-ENERGY-DEFENDER"] = new(
                    "P2-BATTLEFIELD-ENERGY-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BrushHeldScoreState()
    {
        var baseState = BattlefieldHeldScoreState();
        return baseState with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(baseState.PlayerZones, StringComparer.Ordinal)
            {
                ["P2"] = baseState.PlayerZones["P2"] with
                {
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-BRUSH",
                        "P2-BATTLEFIELD-ENERGY-HUB",
                        "P2-BATTLEFIELD-ENERGY-DEFENDER"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(baseState.CardObjects, StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-BRUSH"] = new(
                    "P2-BATTLEFIELD-BRUSH",
                    cardNo: P6TokenFactoryCatalog.BrushBattlefieldTokenCardNo,
                    tags:
                    [
                        P6TokenFactoryCatalog.BattlefieldCardTag,
                        "草丛",
                        "REPLACES_BATTLEFIELD:P2-BATTLEFIELD-ENERGY-HUB"
                    ],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldNextSpellEchoState()
    {
        return PunishmentState(mana: 0) with
        {
            TurnNumber = 1,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-PILTOVER-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-PILTOVER-ACADEMY", "P2-BATTLEFIELD-PILTOVER-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-PILTOVER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-PILTOVER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-PILTOVER-ACADEMY"] = new(
                    "P2-BATTLEFIELD-PILTOVER-ACADEMY",
                    cardNo: "UNL-216/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-PILTOVER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-PILTOVER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldActivateConquestState()
    {
        return PunishmentState(mana: 0) with
        {
            TurnNumber = 1,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-RECKONER-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = ["P2-BATTLEFIELD-RECKONER-DRAW-001"],
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-RECKONER-ARENA",
                        "P2-BATTLEFIELD-BAD-PORO",
                        "P2-BATTLEFIELD-KAISA"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RECKONER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-RECKONER-ARENA"] = new(
                    "P2-BATTLEFIELD-RECKONER-ARENA",
                    cardNo: "OGN·286/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-BAD-PORO"] = new(
                    "P2-BATTLEFIELD-BAD-PORO",
                    cardNo: "UNL-222/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "魄罗", "海盗"],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-KAISA"] = new(
                    "P2-BATTLEFIELD-KAISA",
                    cardNo: "OGN·039/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-RECKONER-DRAW-001"] = new(
                    "P2-BATTLEFIELD-RECKONER-DRAW-001",
                    cardNo: "SFD·001/221",
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldActivateQiyanaConquestState(bool hasMainDeck)
    {
        string[] mainDeck = hasMainDeck ? ["P2-BATTLEFIELD-QIYANA-DRAW-001"] : [];
        string[] runeDeck = hasMainDeck ? [] : ["P2-BATTLEFIELD-QIYANA-RUNE-001"];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD-RECKONER-ATTACKER"] = new(
                "P1-BATTLEFIELD-RECKONER-ATTACKER",
                cardNo: "SFD·125/221",
                power: 1,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1"),
            ["P2-BATTLEFIELD-RECKONER-ARENA"] = new(
                "P2-BATTLEFIELD-RECKONER-ARENA",
                cardNo: "OGN·286/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P2",
                controllerId: "P2"),
            ["P2-BATTLEFIELD-QIYANA"] = new(
                "P2-BATTLEFIELD-QIYANA",
                cardNo: "OGN·155/298",
                power: 4,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2")
        };
        if (hasMainDeck)
        {
            cardObjects["P2-BATTLEFIELD-QIYANA-DRAW-001"] = new(
                "P2-BATTLEFIELD-QIYANA-DRAW-001",
                cardNo: "SFD·001/221",
                ownerId: "P2",
                controllerId: "P2");
        }
        else
        {
            cardObjects["P2-BATTLEFIELD-QIYANA-RUNE-001"] = new(
                "P2-BATTLEFIELD-QIYANA-RUNE-001",
                cardNo: "P2-RUNE-QIYANA-001",
                ownerId: "P2",
                controllerId: "P2");
        }

        return PunishmentState(mana: 0) with
        {
            TurnNumber = 1,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-RECKONER-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    MainDeck = mainDeck,
                    RuneDeck = runeDeck,
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-RECKONER-ARENA",
                        "P2-BATTLEFIELD-QIYANA"
                    ]
                }
            },
            CardObjects = cardObjects
        };
    }

    private static MatchState BattlefieldHeldActivateConquestReadinessState()
    {
        return PunishmentState(mana: 0) with
        {
            TurnNumber = 1,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-RECKONER-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-RECKONER-ARENA",
                        "P2-BATTLEFIELD-READY-DEFENDER",
                        "P2-BATTLEFIELD-LUCIAN",
                        "P2-BATTLEFIELD-SETT"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RECKONER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-RECKONER-ARENA"] = new(
                    "P2-BATTLEFIELD-RECKONER-ARENA",
                    cardNo: "OGN·286/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-READY-DEFENDER"] = new(
                    "P2-BATTLEFIELD-READY-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-LUCIAN"] = new(
                    "P2-BATTLEFIELD-LUCIAN",
                    cardNo: "SFD·113/221",
                    isExhausted: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-SETT"] = new(
                    "P2-BATTLEFIELD-SETT",
                    cardNo: "OGN·164/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldActivateConquestFriendlyBoonState()
    {
        return PunishmentState(mana: 0) with
        {
            TurnNumber = 1,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-RECKONER-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-RECKONER-ARENA",
                        "P2-BATTLEFIELD-BOON-DEFENDER",
                        "P2-BATTLEFIELD-CRIMSON-SIGNET-TREANT"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RECKONER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-RECKONER-ARENA"] = new(
                    "P2-BATTLEFIELD-RECKONER-ARENA",
                    cardNo: "OGN·286/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-BOON-DEFENDER"] = new(
                    "P2-BATTLEFIELD-BOON-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-CRIMSON-SIGNET-TREANT"] = new(
                    "P2-BATTLEFIELD-CRIMSON-SIGNET-TREANT",
                    cardNo: "UNL-029/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldActivateConquestFriendlyPowerState()
    {
        return PunishmentState(mana: 0) with
        {
            TurnNumber = 1,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-RECKONER-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-RECKONER-ARENA",
                        "P2-BATTLEFIELD-POWER-DEFENDER",
                        "P2-BATTLEFIELD-SKYVOICE-WYRMLING"
                    ]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RECKONER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-RECKONER-ARENA"] = new(
                    "P2-BATTLEFIELD-RECKONER-ARENA",
                    cardNo: "OGN·286/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-POWER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-POWER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-SKYVOICE-WYRMLING"] = new(
                    "P2-BATTLEFIELD-SKYVOICE-WYRMLING",
                    cardNo: "UNL-027/219",
                    power: 8,
                    tags: [CardObjectTags.UnitCard, "龙"],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldActivateConquestAdaptiveRobotState(bool hasEquipment)
    {
        string[] p2Base = hasEquipment ? ["P2-ADAPTIVE-EQUIPMENT"] : [];
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD-RECKONER-ATTACKER"] = new(
                "P1-BATTLEFIELD-RECKONER-ATTACKER",
                cardNo: "SFD·125/221",
                power: 1,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1"),
            ["P2-BATTLEFIELD-RECKONER-ARENA"] = new(
                "P2-BATTLEFIELD-RECKONER-ARENA",
                cardNo: "OGN·286/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P2",
                controllerId: "P2"),
            ["P2-BATTLEFIELD-ADAPTIVE-ROBOT"] = new(
                "P2-BATTLEFIELD-ADAPTIVE-ROBOT",
                cardNo: "OGN·056/298",
                power: 3,
                tags: [CardObjectTags.UnitCard, "机械"],
                ownerId: "P2",
                controllerId: "P2")
        };
        if (hasEquipment)
        {
            cardObjects["P2-ADAPTIVE-EQUIPMENT"] = new(
                "P2-ADAPTIVE-EQUIPMENT",
                cardNo: "SFD·022/221",
                tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                ownerId: "P2",
                controllerId: "P2");
        }

        return PunishmentState(mana: 0) with
        {
            TurnNumber = 1,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-RECKONER-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = p2Base,
                    Battlefields =
                    [
                        "P2-BATTLEFIELD-RECKONER-ARENA",
                        "P2-BATTLEFIELD-ADAPTIVE-ROBOT"
                    ]
                }
            },
            CardObjects = cardObjects
        };
    }

    private static MatchState BattlefieldBattleDestroyedRecallState(int mana)
    {
        return PunishmentState(mana: 0) with
        {
            TurnNumber = 1,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(mana, 0)
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-BLOOD-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-BLOOD-ALTAR", "P2-BATTLEFIELD-BLOOD-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-BLOOD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-BLOOD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-BLOOD-ALTAR"] = new(
                    "P2-BATTLEFIELD-BLOOD-ALTAR",
                    cardNo: "UNL-206/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-BLOOD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-BLOOD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };
    }

    private static MatchState BattlefieldHeldReturnHeroState()
    {
        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-TOMB-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-HALLOWED-TOMB", "P2-BATTLEFIELD-TOMB-DEFENDER"],
                    Graveyard = ["P2-HERO-TOMB-RETURN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-TOMB-ATTACKER"] = new(
                    "P1-BATTLEFIELD-TOMB-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-HALLOWED-TOMB"] = new(
                    "P2-BATTLEFIELD-HALLOWED-TOMB",
                    cardNo: "OGN·281/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-BATTLEFIELD-TOMB-DEFENDER"] = new(
                    "P2-BATTLEFIELD-TOMB-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-HERO-TOMB-RETURN"] = new(
                    "P2-HERO-TOMB-RETURN",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "CARD_CATEGORY:英雄单位"],
                    ownerId: "P2",
                    controllerId: "P2",
                    isExhausted: true,
                    damage: 2)
            }
        };
    }

    private static MatchState BattlefieldHeldSevenUnitsWinState(string battlefieldCardNo)
    {
        var defenderUnitObjectIds = Enumerable.Range(1, 7)
            .Select(index => $"P2-BATTLEFIELD-GRAND-UNIT-{index:000}")
            .ToArray();
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD-GRAND-ATTACKER"] = new(
                "P1-BATTLEFIELD-GRAND-ATTACKER",
                cardNo: "SFD·125/221",
                power: 1,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1"),
            ["P2-BATTLEFIELD-GRAND-PLAZA"] = new(
                "P2-BATTLEFIELD-GRAND-PLAZA",
                cardNo: battlefieldCardNo,
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P2",
                controllerId: "P2")
        };
        foreach (var defenderUnitObjectId in defenderUnitObjectIds)
        {
            cardObjects[defenderUnitObjectId] = new CardObjectState(
                defenderUnitObjectId,
                cardNo: "SFD·125/221",
                power: string.Equals(defenderUnitObjectId, defenderUnitObjectIds[0], StringComparison.Ordinal) ? 3 : 1,
                tags: [CardObjectTags.UnitCard],
                ownerId: "P2",
                controllerId: "P2");
        }

        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD-GRAND-ATTACKER"] = new("P1", "BATTLEFIELD"),
            ["P2-BATTLEFIELD-GRAND-PLAZA"] = new("P2", "BATTLEFIELD")
        };
        foreach (var defenderUnitObjectId in defenderUnitObjectIds)
        {
            objectLocations[defenderUnitObjectId] = new("P2", "BATTLEFIELD", "P2-BATTLEFIELD-GRAND-PLAZA");
        }

        return PunishmentState(mana: 0) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = ["P1-BATTLEFIELD-GRAND-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = new[] { "P2-BATTLEFIELD-GRAND-PLAZA" }.Concat(defenderUnitObjectIds).ToArray()
                }
            },
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };
    }

    private static MatchState BattlefieldHeldSevenUnitsWinSplitCountState()
    {
        var state = BattlefieldHeldSevenUnitsWinState("OGN·293/298");
        var sideBattlefieldObjectId = "P2-BATTLEFIELD-SIDE-PLAZA";
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P2"] = playerZones["P2"] with
        {
            Battlefields = playerZones["P2"].Battlefields
                .Concat([sideBattlefieldObjectId])
                .ToArray()
        };

        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[sideBattlefieldObjectId] = new CardObjectState(
            sideBattlefieldObjectId,
            cardNo: "OGN·280/298",
            tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
            ownerId: "P2",
            controllerId: "P2");

        var objectLocations = state.ObjectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        objectLocations[sideBattlefieldObjectId] = new("P2", "BATTLEFIELD");
        foreach (var objectId in Enumerable.Range(2, 6).Select(index => $"P2-BATTLEFIELD-GRAND-UNIT-{index:000}"))
        {
            objectLocations[objectId] = new("P2", "BATTLEFIELD", sideBattlefieldObjectId);
        }

        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations
        };
    }

    private static MatchState BattlefieldConquerRevealRecycleState()
    {
        return PunishmentState(mana: 0) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-CANDLELIT-SANCTUM")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-CANDLELIT-SANCTUM"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-CANDLELIT-SANCTUM"),
                ["P1-BATTLEFIELD-CANDLE-ATTACKER"] = new("P1", "BATTLEFIELD", "P1-BATTLEFIELD-CANDLELIT-SANCTUM"),
                ["P2-BATTLEFIELD-CANDLE-DEFENDER"] = new("P2", "BATTLEFIELD", "P1-BATTLEFIELD-CANDLELIT-SANCTUM")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck =
                    [
                        "P1-BATTLEFIELD-CANDLE-001",
                        "P1-BATTLEFIELD-CANDLE-002",
                        "P1-BATTLEFIELD-CANDLE-003"
                    ],
                    Battlefields = ["P1-BATTLEFIELD-CANDLELIT-SANCTUM", "P1-BATTLEFIELD-CANDLE-ATTACKER"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BATTLEFIELD-CANDLE-DEFENDER"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-CANDLELIT-SANCTUM"] = new(
                    "P1-BATTLEFIELD-CANDLELIT-SANCTUM",
                    cardNo: "OGN·291/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: "P1",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-CANDLE-ATTACKER"] = new(
                    "P1-BATTLEFIELD-CANDLE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BATTLEFIELD-CANDLE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-CANDLE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P1-BATTLEFIELD-CANDLE-001"] = new(
                    "P1-BATTLEFIELD-CANDLE-001",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-CANDLE-002"] = new(
                    "P1-BATTLEFIELD-CANDLE-002",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-BATTLEFIELD-CANDLE-003"] = new(
                    "P1-BATTLEFIELD-CANDLE-003",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
    }
}
