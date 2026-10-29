using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;
public sealed partial class MatchSession
{

    private static MatchState BuildUnknownRuneSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304167,
            4167,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone: ["P1-RUNE-UNKNOWN-SOURCE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-RUNE-UNKNOWN-SOURCE"] = new(
                    "P1-RUNE-UNKNOWN-SOURCE",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownDeclareBattleSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304168,
            4168,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P1-BATTLE-UNKNOWN-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BATTLE-UNKNOWN-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-UNKNOWN-ATTACKER"] = new(
                    "P1-BATTLE-UNKNOWN-ATTACKER",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-UNKNOWN-DEFENDER"] = new(
                    "P2-BATTLE-UNKNOWN-DEFENDER",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildUnknownDeclareBattleBattlefieldPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304172,
            4172,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields:
                    [
                        "P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION",
                        "P1-BATTLE-KNOWN-ATTACKER"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BATTLE-KNOWN-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION"] = new(
                    "P1-BATTLEFIELD-UNKNOWN-DECLARE-BATTLE-DESTINATION",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-KNOWN-ATTACKER"] = new(
                    "P1-BATTLE-KNOWN-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-KNOWN-DEFENDER"] = new(
                    "P2-BATTLE-KNOWN-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildAssemblePaymentRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304163,
            4163,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-ASSEMBLE-PAYMENT-001"],
                    baseZone:
                    [
                        "P1-EQUIPMENT-LONG-SWORD-ASSEMBLE-PAYMENT",
                        "P1-UNIT-ASSEMBLE-PAYMENT-TARGET",
                        "P1-RUNE-RED-ASSEMBLE-PAYMENT-001"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD-ASSEMBLE-PAYMENT"] = new(
                    "P1-EQUIPMENT-LONG-SWORD-ASSEMBLE-PAYMENT",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-ASSEMBLE-PAYMENT-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-PAYMENT-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-ASSEMBLE-PAYMENT-001"] = new(
                    "P1-RUNE-RED-ASSEMBLE-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-ASSEMBLE-PAYMENT-001"] = new(
                    "P1-RUNE-BOTTOM-ASSEMBLE-PAYMENT-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildAssembleExperienceScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603304165,
            4165,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone:
                    [
                        "P1-EQUIPMENT-SHEPHERDS-HEIRLOOM-ASSEMBLE",
                        "P1-UNIT-SHEPHERDS-HEIRLOOM-TARGET"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-SHEPHERDS-HEIRLOOM-ASSEMBLE"] = new(
                    "P1-EQUIPMENT-SHEPHERDS-HEIRLOOM-ASSEMBLE",
                    cardNo: "UNL-158/219",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-SHEPHERDS-HEIRLOOM-TARGET"] = new(
                    "P1-UNIT-SHEPHERDS-HEIRLOOM-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });

        return state with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [seed.P1] = 1,
                [seed.P2] = 0
            }
        };
    }

    private static MatchState BuildAssembleDestroyFriendlyUnitScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304166,
            4166,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Yellow] = 1
                    }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone:
                    [
                        "P1-EQUIPMENT-BLADE-RUINED-KING-ASSEMBLE",
                        "P1-UNIT-BLADE-RUINED-KING-TARGET",
                        "P1-UNIT-BLADE-RUINED-KING-COST"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-BLADE-RUINED-KING-ASSEMBLE"] = new(
                    "P1-EQUIPMENT-BLADE-RUINED-KING-ASSEMBLE",
                    cardNo: "SFD·178/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-BLADE-RUINED-KING-TARGET"] = new(
                    "P1-UNIT-BLADE-RUINED-KING-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-BLADE-RUINED-KING-COST"] = new(
                    "P1-UNIT-BLADE-RUINED-KING-COST",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildAssembleRecycleGraveyardScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304167,
            4167,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Purple] = 1
                    }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone:
                    [
                        "P1-EQUIPMENT-LAST-RITES-ASSEMBLE",
                        "P1-UNIT-LAST-RITES-TARGET"
                    ],
                    graveyard:
                    [
                        "P1-LAST-RITES-RECYCLE-001",
                        "P1-LAST-RITES-RECYCLE-002"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LAST-RITES-ASSEMBLE"] = new(
                    "P1-EQUIPMENT-LAST-RITES-ASSEMBLE",
                    cardNo: "SFD·150/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-LAST-RITES-TARGET"] = new(
                    "P1-UNIT-LAST-RITES-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-LAST-RITES-RECYCLE-001"] = new(
                    "P1-LAST-RITES-RECYCLE-001",
                    cardNo: "SFD·013/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-LAST-RITES-RECYCLE-002"] = new(
                    "P1-LAST-RITES-RECYCLE-002",
                    cardNo: "SFD·067/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildAssembleDynamicManaScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304168,
            4168,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 1),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone:
                    [
                        "P1-EQUIPMENT-HEXTECH-GAUNTLET-ASSEMBLE",
                        "P1-UNIT-HEXTECH-GAUNTLET-TARGET",
                        "P1-UNIT-HEXTECH-GAUNTLET-TOO-SMALL"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-HEXTECH-GAUNTLET-ASSEMBLE"] = new(
                    "P1-EQUIPMENT-HEXTECH-GAUNTLET-ASSEMBLE",
                    cardNo: "UNL-188/219",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-HEXTECH-GAUNTLET-TARGET"] = new(
                    "P1-UNIT-HEXTECH-GAUNTLET-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-HEXTECH-GAUNTLET-TOO-SMALL"] = new(
                    "P1-UNIT-HEXTECH-GAUNTLET-TOO-SMALL",
                    cardNo: "SFD·013/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildStandardGroupMovementScenario(MatchState current, DevScenarioSeed seed)
    {
        var scene = BuildMidgameShowcaseScenario(current, seed);
        var cards = scene.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var locations = scene.ObjectLocations.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var zones = scene.PlayerZones.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        foreach (var (player, playerZones) in scene.PlayerZones)
        {
            var units = playerZones.Battlefields.Where(id => cards.TryGetValue(id, out var card)
                && card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal) && !card.IsFaceDown).ToArray();
            zones[player] = playerZones with
            {
                Base = playerZones.Base.Concat(units).ToArray(),
                Battlefields = playerZones.Battlefields.Except(units, StringComparer.Ordinal).ToArray()
            };
            foreach (var id in units)
            {
                cards[id] = cards[id] with { IsExhausted = false, IsAttacking = false, IsDefending = false };
                locations[id] = new(player, "BASE");
            }
        }
        return scene with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            PlayerScores = new Dictionary<string, int> { [seed.P1] = 0, [seed.P2] = 0 } };
    }

    private static MatchState BuildPooledCombatScenario(MatchState current, DevScenarioSeed seed)
    {
        var scene = BuildStandardGroupMovementScenario(current, seed);
        var cards = scene.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var locations = scene.ObjectLocations.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var zones = scene.PlayerZones.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var battlefield = cards.Values.First(card => card.OwnerId == seed.P2
            && card.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)).ObjectId;
        foreach (var (id, player, power) in new[]
        {
            ("QA-POOL-A1", seed.P1, 2), ("QA-POOL-A2", seed.P1, 2),
            ("QA-POOL-D1", seed.P2, 3), ("QA-POOL-D2", seed.P2, 1)
        })
        {
            cards[id] = new(id, cardNo: "SFD·125/221", power: power, ownerId: player, controllerId: player,
                isAttacking: player == seed.P1, isDefending: player == seed.P2,
                tags: id == "QA-POOL-D1" ? [CardObjectTags.UnitCard, CardCombatKeywordNames.Bulwark] : [CardObjectTags.UnitCard]);
            locations[id] = new(player, "BATTLEFIELD", battlefield);
            zones[player] = zones[player] with { Battlefields = zones[player].Battlefields.Append(id).ToArray() };
        }
        return scene with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations,
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted(battlefield)] };
    }

    private static MatchState BuildMovementScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302093,
            68,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 1),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-RIDE-THE-WIND"],
                    battlefields: ["P1-BATTLEFIELD-UNIT-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-UNIT-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-RIDE-THE-WIND"] = new(
                    "P1-SPELL-RIDE-THE-WIND",
                    cardNo: "OGN·173/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 2,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-UNIT-001"] = new(
                    "P1-BATTLEFIELD-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    damage: 1,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-UNIT-001"] = new(
                    "P2-BATTLEFIELD-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildSpellDuelScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302019,
            9,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 1),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-HEXTECH-RAY"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-UNIT-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HEXTECH-RAY"] = new(
                    "P1-SPELL-HEXTECH-RAY",
                    cardNo: "OGN·009/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildSpellDuelFocusScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303023,
            93,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 1),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    hand: ["P1-SPELL-HEXTECH-RAY"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-UNIT-HEXTECH-RAY-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-HEXTECH-RAY"] = new(
                    "P1-SPELL-HEXTECH-RAY",
                    cardNo: "OGN·009/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-HEXTECH-RAY-001"] = new(
                    "P2-UNIT-HEXTECH-RAY-001",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            TimingState = TimingStates.SpellDuelOpen,
            FocusPlayerId = seed.P1,
            PassedFocusPlayerIds = []
        };
    }

    private static MatchState BuildBattlefieldContestStackScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303027,
            97,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P1-BATTLEFIELD-CONTEST-001", "P1-UNIT-CONTEST-001", "P1-STANDBY-CONTEST-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-UNIT-CONTEST-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-CONTEST-001"] = new(
                    "P1-BATTLEFIELD-CONTEST-001",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-CONTEST-001"] = new(
                    "P1-UNIT-CONTEST-001",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-STANDBY-CONTEST-001"] = new(
                    "P1-STANDBY-CONTEST-001",
                    cardNo: "OGN·121/298",
                    power: 2,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-CONTEST-001"] = new(
                    "P2-UNIT-CONTEST-001",
                    cardNo: "UNL-036/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            ActivePlayerId = seed.P2,
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = seed.P2,
            PassedPriorityPlayerIds = [seed.P1],
            StackItems =
            [
                new StackItemState(
                    "STACK-BATTLEFIELD-CONTEST-001",
                    seed.P1,
                    "P1-SPELL-BATTLEFIELD-CONTEST",
                    "TEST_RESOLVE",
                    "TEST-000",
                    [])
            ],
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-CONTEST-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-CONTEST-001"),
                ["P1-UNIT-CONTEST-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-CONTEST-001"),
                ["P1-STANDBY-CONTEST-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-CONTEST-001"),
                ["P2-UNIT-CONTEST-001"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-CONTEST-001")
            }
        };
    }

    private static MatchState BuildBattlefieldContestSpellDuelCleanupScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303028,
            98,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001", "P1-UNIT-SPELL-DUEL-CLEANUP-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-UNIT-SPELL-DUEL-CLEANUP-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001"] = new(
                    "P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-SPELL-DUEL-CLEANUP-001"] = new(
                    "P1-UNIT-SPELL-DUEL-CLEANUP-001",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-SPELL-DUEL-CLEANUP-001"] = new(
                    "P2-UNIT-SPELL-DUEL-CLEANUP-001",
                    cardNo: "SFD·125/221",
                    damage: 3,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            ActivePlayerId = seed.P2,
            TimingState = TimingStates.SpellDuelOpen,
            FocusPlayerId = seed.P2,
            PassedFocusPlayerIds = [seed.P1],
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001"),
                ["P1-UNIT-SPELL-DUEL-CLEANUP-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001"),
                ["P2-UNIT-SPELL-DUEL-CLEANUP-001"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-SPELL-DUEL-CLEANUP-001")
            }
        };
    }

    private static MatchState BuildEchoStackScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303061,
            61,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(4, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-DRAW-001", "P1-DRAW-002"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-CENTER-STAGE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-CENTER-STAGE"] = new(
                    "P1-SPELL-CENTER-STAGE",
                    cardNo: "UNL-061/219",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 2,
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildPriorityReactionCounterScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303136,
            136,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = new(2, 0)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    hand: ["P2-SPELL-HARD-BARGAIN"],
                    battlefields: ["P2-UNIT-HARD-BARGAIN-TARGET"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-SPELL-HARD-BARGAIN"] = new(
                    "P2-SPELL-HARD-BARGAIN",
                    cardNo: "SFD·136/221",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 2,
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-UNIT-HARD-BARGAIN-TARGET"] = new(
                    "P2-UNIT-HARD-BARGAIN-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = seed.P2,
            PassedPriorityPlayerIds = [seed.P1],
            StackItems =
            [
                new StackItemState(
                    "STACK-1-P1-SPELL-INCINERATE",
                    seed.P1,
                    "P1-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    ["P2-UNIT-HARD-BARGAIN-TARGET"])
            ]
        };
    }

    private static MatchState BuildStandbyReactionScenario(MatchState current, DevScenarioSeed seed)
    {
        const string source = "P1-FACEDOWN-OGN-TEEMO-PURPLE";
        const string field = "P1-STANDBY-BATTLEFIELD";
        var cards = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [source] = new(source, cardNo: "OGN·197/298", power: 1, manaCost: 2,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], ownerId: seed.P1, controllerId: seed.P1),
            [field] = new(field, cardNo: "OGN·278/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: seed.P1, controllerId: seed.P1),
            ["P1-STANDBY-ANCHOR"] = new("P1-STANDBY-ANCHOR", cardNo: "OGN·001/298", power: 4,
                tags: [CardObjectTags.UnitCard], ownerId: seed.P1, controllerId: seed.P1),
            ["P2-STANDBY-REPLY"] = new("P2-STANDBY-REPLY", cardNo: "OGN·048/298", manaCost: 2,
                tags: [CardObjectTags.SpellCard], ownerId: seed.P2, controllerId: seed.P2)
        };
        var locations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            [source] = new(seed.P1, "HAND"), [field] = new(seed.P1, "BATTLEFIELD", field),
            ["P1-STANDBY-ANCHOR"] = new(seed.P1, "BATTLEFIELD", field), ["P2-STANDBY-REPLY"] = new(seed.P2, "HAND")
        };
        var decks = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var player in new[] { seed.P1, seed.P2 })
        {
            var deck = Enumerable.Range(1, 8).Select(i => player + "-STANDBY-DRAW-" + i).ToArray();
            decks[player] = deck;
            foreach (var id in deck)
            {
                cards[id] = new(id, cardNo: "OGN·001/298", tags: [CardObjectTags.UnitCard], ownerId: player, controllerId: player);
                locations[id] = new(player, "MAIN_DECK");
            }
        }
        var runes = Enumerable.Range(1, 5).Select(i => "P2-STANDBY-RUNE-" + i).ToArray();
        foreach (var id in runes)
        {
            cards[id] = new(id, cardNo: "UNL-R03", tags: [CardObjectTags.RuneCard, "COLOR:blue"], ownerId: seed.P2, controllerId: seed.P2);
            locations[id] = new(seed.P2, "BASE");
        }
        return BuildScenarioState(current, seed, 2603303197, 97,
            new Dictionary<string, RunePool> { [seed.P1] = new(0, 1), [seed.P2] = RunePool.Empty },
            new Dictionary<string, PlayerZones>
            {
                [seed.P1] = PlayerZones.Empty with { Hand = [source], MainDeck = decks[seed.P1], Battlefields = [field, "P1-STANDBY-ANCHOR"] },
                [seed.P2] = PlayerZones.Empty with { Hand = ["P2-STANDBY-REPLY"], MainDeck = decks[seed.P2], Base = runes }
            }, cards) with { ObjectLocations = locations };
    }

    private async Task<(MatchState State, IReadOnlyList<GameEvent> Events)> PrepareStandbyDevelopmentScenario(
        MatchState initial, CancellationToken cancellationToken)
    {
        var current = initial;
        var events = new List<GameEvent>();
        var p1 = current.Seats.Single(s => s.Value == "P1").Key;
        var p2 = current.Seats.Single(s => s.Value == "P2").Key;
        async Task Apply(string player, GameCommand command)
        {
            var result = await ruleEngine.ResolveAsync(current,
                new("standby-seed-preparation-" + current.Tick, player, command.CmdType), command, cancellationToken).ConfigureAwait(false);
            if (!result.Accepted) throw new MatchSessionException(result.ErrorCode ?? ErrorCodes.InvalidTarget, result.ErrorMessage ?? "待命场景准备失败。");
            current = result.State;
            events.AddRange(result.Events);
        }
        await Apply(p1, new HideCardCommand("P1-FACEDOWN-OGN-TEEMO-PURPLE", "OGN·197/298", "BATTLEFIELD:P1-STANDBY-BATTLEFIELD", ["STANDBY_A"]));
        await Apply(p1, new EndTurnCommand());
        if (current.TurnStartStep is not null)
            throw new MatchSessionException(ErrorCodes.PhaseNotAllowed, "待命场景的回合开始流程尚未完成。");
        foreach (var i in new[] { 1, 2, 3 }) await Apply(p2, new TapRuneCommand("P2-STANDBY-RUNE-" + i));
        foreach (var i in new[] { 4, 5 }) await Apply(p2, new RecycleRuneCommand("P2-STANDBY-RUNE-" + i));
        await Apply(p2, new PlayCardCommand("P2-STANDBY-REPLY", "OGN·048/298", []));
        await Apply(p2, new PassPriorityCommand());
        return (current, events);
    }

    private static MatchState BuildAmbushReactionScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303021,
            21,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-HAND-UNL-GLOOMY-APOTHECARY"],
                    battlefields: ["P1-BATTLEFIELD-FRIENDLY-001", "P1-MAIN"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-HAND-UNL-GLOOMY-APOTHECARY"] = new(
                    "P1-HAND-UNL-GLOOMY-APOTHECARY",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardInteractionKeywordNames.Ambush],
                    manaCost: 3,
                    cardNo: "UNL-021/219"),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new("P1-BATTLEFIELD-FRIENDLY-001", power: 2, tags: [CardObjectTags.UnitCard], cardNo: "SFD·125/221", ownerId: seed.P1, controllerId: seed.P1),
                ["P1-MAIN"] = new("P1-MAIN", cardNo: "OGN·278/298", ownerId: seed.P1, controllerId: seed.P1, tags: [P6TokenFactoryCatalog.BattlefieldCardTag])
            });

        return state with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["P1-MAIN"] = new(seed.P1, "BATTLEFIELD", "P1-MAIN"),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(seed.P1, "BATTLEFIELD", "P1-MAIN") },
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = seed.P1,
            StackItems = [PendingProbeStackItem(seed)]
        };
    }

    private static MatchState BuildEquipmentScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302787,
            787,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(
                    2,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 1
                    }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-LONG-SWORD-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-EQUIPMENT-LONG-SWORD"],
                    baseZone: ["P1-UNIT-ASSEMBLE-TARGET"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    manaCost: 2,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    power: 3,
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildStatusShowcaseScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303555,
            555,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 1),
                [seed.P2] = new(1, 0)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    baseZone:
                    [
                        "P1-UNIT-STATUS-ANCHOR",
                        "P1-EQUIPMENT-LONG-SWORD",
                        "P1-STANDBY-FACEDOWN",
                        "P1-EPHEMERAL-UNIT"
                    ],
                    battlefields: ["P1-BATTLE-DAMAGED"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    baseZone: ["P2-STUNNED-UNIT"],
                    battlefields: ["P2-CONTROLLED-UNIT"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-STATUS-ANCHOR"] = new(
                    "P1-UNIT-STATUS-ANCHOR",
                    power: 3,
                    untilEndOfTurnPowerModifier: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield, CardCombatKeywordNames.Roam],
                    untilEndOfTurnEffects: ["BOON:+1", "POWER:+2_UNTIL_END_OF_TURN"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-EQUIPMENT-LONG-SWORD"] = new(
                    "P1-EQUIPMENT-LONG-SWORD",
                    tags: [CardObjectTags.EquipmentCard],
                    cardNo: "SFD·022/221",
                    attachedToObjectId: "P1-UNIT-STATUS-ANCHOR",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-STANDBY-FACEDOWN"] = new(
                    "P1-STANDBY-FACEDOWN",
                    isFaceDown: true,
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    manaCost: 2,
                    cardNo: "OGN·197/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-EPHEMERAL-UNIT"] = new(
                    "P1-EPHEMERAL-UNIT",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-DAMAGED"] = new(
                    "P1-BATTLE-DAMAGED",
                    damage: 2,
                    isExhausted: true,
                    isAttacking: true,
                    power: 4,
                    untilEndOfTurnPowerModifier: -1,
                    tags: [CardObjectTags.UnitCard, CardPermissionKeywordNames.Swift],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-STUNNED-UNIT"] = new(
                    "P2-STUNNED-UNIT",
                    damage: 1,
                    isExhausted: true,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "眩晕"],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-CONTROLLED-UNIT"] = new(
                    "P2-CONTROLLED-UNIT",
                    isDefending: true,
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    ownerId: seed.P2,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildResourceExperienceScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303092,
            92,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(5, 1),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-RESOURCE-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand:
                    [
                        "P1-UNIT-DEMACIA-ENVOY",
                        "P1-UNIT-MOSS-STEPPER"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-DEMACIA-ENVOY"] = new(
                    "P1-UNIT-DEMACIA-ENVOY",
                    cardNo: "UNL-092/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 3,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-MOSS-STEPPER"] = new(
                    "P1-UNIT-MOSS-STEPPER",
                    cardNo: "UNL-047/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 2,
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });

        return state with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [seed.P1] = 2,
                [seed.P2] = 0
            }
        };
    }

    private static MatchState BuildLegendActScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603307905,
            905,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-LEGEND-DRAW-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    legendZone: ["P1-LEGEND-POPPY"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-POPPY"] = new(
                    "P1-LEGEND-POPPY",
                    cardNo: "UNL-237/219",
                    ownerId: seed.P1,
                    controllerId: seed.P1,
                    tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-DRAW-001"] = new(
                    "P1-LEGEND-DRAW-001",
                    cardNo: "SFD·125/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1,
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            });

        return state with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [seed.P1] = 3,
                [seed.P2] = 0
            }
        };
    }

    private static MatchState BuildLegendActiveActionsScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603307906,
            906,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(4, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-LEGEND-DRAW-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    baseZone: ["P1-LEGEND-BASE-UNIT", "P1-LEGEND-EPHEMERAL-UNIT"],
                    battlefields: ["P1-LEGEND-BATTLEFIELD-UNIT", "P1-LEGEND-EXHAUSTED-BATTLEFIELD-UNIT"],
                    legendZone:
                    [
                        "P1-LEGEND-YASUO",
                        "P1-LEGEND-LEE-SIN",
                        "P1-LEGEND-POPPY",
                        "P1-LEGEND-VIKTOR",
                        "P1-LEGEND-MISS-FORTUNE",
                        "P1-LEGEND-KHAZIX",
                        "P1-LEGEND-PYKE",
                        "P1-LEGEND-AZIR",
                        "P1-LEGEND-LILLIA"
                    ],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-YASUO"] = new("P1-LEGEND-YASUO", cardNo: "FND-259/298", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-LEE-SIN"] = new("P1-LEGEND-LEE-SIN", cardNo: "OGN·257/298", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-POPPY"] = new("P1-LEGEND-POPPY", cardNo: "UNL-237/219", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-VIKTOR"] = new("P1-LEGEND-VIKTOR", cardNo: "FND-265/298", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-MISS-FORTUNE"] = new("P1-LEGEND-MISS-FORTUNE", cardNo: "OGN·267/298", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-KHAZIX"] = new("P1-LEGEND-KHAZIX", cardNo: "UNL-201/219", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-PYKE"] = new("P1-LEGEND-PYKE", cardNo: "UNL-185/219", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-AZIR"] = new("P1-LEGEND-AZIR", cardNo: "SFD·197/221", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-LILLIA"] = new("P1-LEGEND-LILLIA", cardNo: "UNL-189/219", ownerId: seed.P1, controllerId: seed.P1, tags: ["CARD_TYPE:LEGEND"]),
                ["P1-LEGEND-BASE-UNIT"] = new("P1-LEGEND-BASE-UNIT", power: 2, tags: [CardObjectTags.UnitCard], ownerId: seed.P1, controllerId: seed.P1),
                ["P1-LEGEND-EPHEMERAL-UNIT"] = new("P1-LEGEND-EPHEMERAL-UNIT", power: 1, tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral], ownerId: seed.P1, controllerId: seed.P1),
                ["P1-LEGEND-BATTLEFIELD-UNIT"] = new("P1-LEGEND-BATTLEFIELD-UNIT", power: 3, tags: [CardObjectTags.UnitCard], ownerId: seed.P1, controllerId: seed.P1),
                ["P1-LEGEND-EXHAUSTED-BATTLEFIELD-UNIT"] = new("P1-LEGEND-EXHAUSTED-BATTLEFIELD-UNIT", power: 2, isExhausted: true, tags: [CardObjectTags.UnitCard], ownerId: seed.P1, controllerId: seed.P1),
                ["P1-LEGEND-DRAW-001"] = new("P1-LEGEND-DRAW-001", cardNo: "SFD·125/221", ownerId: seed.P1, controllerId: seed.P1, power: 3, tags: [CardObjectTags.UnitCard])
            }) with
            {
                PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [seed.P1] = 3,
                    [seed.P2] = 0
                },
                UntilEndOfTurnEffects = [$"PLAYED_ARMAMENT_THIS_TURN:{seed.P1}"]
            };
    }

    private static MatchState BuildLifecycleEphemeralScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303801,
            801,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    baseZone: ["P1-EPHEMERAL-OTHER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    baseZone: ["P2-EPHEMERAL-BASE", "P2-KEEP-BASE"],
                    battlefields: ["P2-EPHEMERAL-BATTLEFIELD"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-EPHEMERAL-OTHER"] = new("P1-EPHEMERAL-OTHER", ownerId: seed.P1, controllerId: seed.P1, power: 1, tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral]),
                ["P2-EPHEMERAL-BASE"] = new("P2-EPHEMERAL-BASE", ownerId: seed.P2, controllerId: seed.P2, power: 1, tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral]),
                ["P2-EPHEMERAL-BATTLEFIELD"] = new("P2-EPHEMERAL-BATTLEFIELD", ownerId: seed.P2, controllerId: seed.P2, power: 2, tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral]),
                ["P2-KEEP-BASE"] = new("P2-KEEP-BASE", ownerId: seed.P2, controllerId: seed.P2, power: 3, tags: [CardObjectTags.UnitCard])
            });
    }

    private static MatchState BuildLifecycleEphemeralLeblancStaticScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303811,
            811,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    baseZone: ["P2-EPHEMERAL-BASE", "P2-KEEP-BASE"],
                    battlefields:
                    [
                        "P2-BATTLEFIELD-LEBLANC",
                        "P2-LEBLANC-STATIC",
                        "P2-EPHEMERAL-PROTECTED",
                        "P2-BATTLEFIELD-OTHER",
                        "P2-EPHEMERAL-OTHER-BATTLEFIELD"
                    ],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-EPHEMERAL-BASE"] = new(
                    "P2-EPHEMERAL-BASE",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-KEEP-BASE"] = new(
                    "P2-KEEP-BASE",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-LEBLANC"] = new(
                    "P2-BATTLEFIELD-LEBLANC",
                    cardNo: "UNL·T01",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-LEBLANC-STATIC"] = new(
                    "P2-LEBLANC-STATIC",
                    cardNo: "UNL-090a/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, CardCombatKeywordNames.BackRow],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-EPHEMERAL-PROTECTED"] = new(
                    "P2-EPHEMERAL-PROTECTED",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-OTHER"] = new(
                    "P2-BATTLEFIELD-OTHER",
                    cardNo: "UNL·T03",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-EPHEMERAL-OTHER-BATTLEFIELD"] = new(
                    "P2-EPHEMERAL-OTHER-BATTLEFIELD",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-MAIN-001"] = new(seed.P1, "MAIN_DECK"),
                ["P1-RUNE-001"] = new(seed.P1, "RUNE_DECK"),
                ["P1-RUNE-002"] = new(seed.P1, "RUNE_DECK"),
                ["P2-MAIN-001"] = new(seed.P2, "MAIN_DECK"),
                ["P2-RUNE-001"] = new(seed.P2, "RUNE_DECK"),
                ["P2-RUNE-002"] = new(seed.P2, "RUNE_DECK"),
                ["P2-EPHEMERAL-BASE"] = new(seed.P2, "BASE"),
                ["P2-KEEP-BASE"] = new(seed.P2, "BASE"),
                ["P2-BATTLEFIELD-LEBLANC"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-LEBLANC"),
                ["P2-LEBLANC-STATIC"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-LEBLANC"),
                ["P2-EPHEMERAL-PROTECTED"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-LEBLANC"),
                ["P2-BATTLEFIELD-OTHER"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-OTHER"),
                ["P2-EPHEMERAL-OTHER-BATTLEFIELD"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-OTHER")
            }
        };
    }

    private static MatchState BuildLifecycleLastBreathScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303802,
            802,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(4, 2),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-VENGEANCE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-LAST-BREATH-DRAW-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-WATCHFUL-SENTINEL-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-VENGEANCE"] = new(
                    "P1-SPELL-VENGEANCE",
                    cardNo: "OGN·229/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 4,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-WATCHFUL-SENTINEL-001"] = new(
                    "P2-WATCHFUL-SENTINEL-001",
                    cardNo: "OGN·096/298",
                    ownerId: seed.P2,
                    controllerId: seed.P2,
                    power: 1,
                    tags: [CardObjectTags.UnitCard])
            });
    }

    private static MatchState BuildControlScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302821,
            821,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(5, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-HOSTILE-TAKEOVER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-HOSTILE-TAKEOVER-TARGET"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-HOSTILE-TAKEOVER-TARGET"] = new(power: 4, isExhausted: true, tags: ["CARD_TYPE:UNIT"])
            });
    }
}
