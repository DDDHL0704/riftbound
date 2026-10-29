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

    private static MatchState BuildRagingDrakeNextSpellCostReductionPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303070,
            170,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-DRAW-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-WELL-TRAINED"],
                    baseZone: ["P1-UNIT-RAGING-DRAKE"],
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
                ["P1-SPELL-WELL-TRAINED"] = new(
                    "P1-SPELL-WELL-TRAINED",
                    cardNo: "OGN·058/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-RAGING-DRAKE"] = new(
                    "P1-UNIT-RAGING-DRAKE",
                    cardNo: "OGN·031/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "龙"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-001"] = new(
                    "P2-UNIT-001",
                    cardNo: "UNL-097/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            UntilEndOfTurnEffects =
            [
                $"{RagingDrakeNextSpellCostReductionEffectPrefix}{seed.P1}:P1-UNIT-RAGING-DRAKE"
            ]
        };
    }

    private static MatchState BuildBattlefieldLegendAttachArmamentScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303068,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    baseZone: ["P1-UNIT-PORO-FORGE-TARGET", "P1-EQUIPMENT-PORO-FORGE-ARMAMENT"],
                    battlefields: ["P1-BATTLEFIELD-PORO-FORGE"],
                    legendZone: ["P1-LEGEND-PORO-FORGE"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-PORO-FORGE"] = new(
                    "P1-LEGEND-PORO-FORGE",
                    cardNo: "UNL-237/219",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-PORO-FORGE-TARGET"] = new(
                    "P1-UNIT-PORO-FORGE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-EQUIPMENT-PORO-FORGE-ARMAMENT"] = new(
                    "P1-EQUIPMENT-PORO-FORGE-ARMAMENT",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-PORO-FORGE"] = new(
                    "P1-BATTLEFIELD-PORO-FORGE",
                    cardNo: BattlefieldLegendAttachArmamentSeedCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldExtraStandbyScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303069,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(0, 1),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    hand: ["P1-STANDBY-BANDLE-TEEMO"],
                    battlefields: ["P1-BATTLEFIELD-BANDLE-TREE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-STANDBY-BANDLE-TEEMO"] = new(
                    "P1-STANDBY-BANDLE-TEEMO",
                    cardNo: "OGN·121/298",
                    manaCost: 2,
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-BANDLE-TREE"] = new(
                    "P1-BATTLEFIELD-BANDLE-TREE",
                    cardNo: "OGN·278/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldHeldActivateConquestScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303070,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-RECKONER-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-BATTLEFIELD-RECKONER-DRAW-001"],
                    battlefields:
                    [
                        "P2-BATTLEFIELD-RECKONER-ARENA",
                        "P2-BATTLEFIELD-BAD-PORO",
                        "P2-BATTLEFIELD-KAISA"
                    ],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-RECKONER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RECKONER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-RECKONER-ARENA"] = new(
                    "P2-BATTLEFIELD-RECKONER-ARENA",
                    cardNo: "OGN·286/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-BAD-PORO"] = new(
                    "P2-BATTLEFIELD-BAD-PORO",
                    cardNo: "UNL-222/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, "魄罗", "海盗"],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-KAISA"] = new(
                    "P2-BATTLEFIELD-KAISA",
                    cardNo: "OGN·039/298",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-RECKONER-DRAW-001"] = new(
                    "P2-BATTLEFIELD-RECKONER-DRAW-001",
                    cardNo: "SFD·001/221",
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldFriendlySpellDrawScenario(MatchState current, DevScenarioSeed seed)
    {
        var seeded = BuildScenarioState(
            current,
            seed,
            2603303066,
            167,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-DRAWN"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-SAVAGE-STRENGTH"],
                    battlefields: ["P1-BATTLEFIELD-DREAMTREE", "P1-BATTLEFIELD-ALLY"],
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
                ["P1-SPELL-SAVAGE-STRENGTH"] = new(
                    "P1-SPELL-SAVAGE-STRENGTH",
                    cardNo: "SFD·034/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-MAIN-DRAWN"] = new(
                    "P1-MAIN-DRAWN",
                    cardNo: "SFD·001/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-ALLY"] = new(
                    "P1-BATTLEFIELD-ALLY",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1,
                    power: 2),
                ["P1-BATTLEFIELD-DREAMTREE"] = new(
                    "P1-BATTLEFIELD-DREAMTREE",
                    cardNo: "OGN·292/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
        return seeded with { ObjectLocations = new Dictionary<string,ObjectLocationState>(seeded.ObjectLocations)
            { ["P1-BATTLEFIELD-DREAMTREE"] = new(seed.P1,"BATTLEFIELD","P1-BATTLEFIELD-DREAMTREE"), ["P1-BATTLEFIELD-ALLY"] = new(seed.P1,"BATTLEFIELD","P1-BATTLEFIELD-DREAMTREE") } };

    }

    private static MatchState BuildBattlefieldSpellPowerBonusScenario(MatchState current, DevScenarioSeed seed)
    {
        var seeded = BuildScenarioState(
            current,
            seed,
            2603303066,
            168,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-SAVAGE-STRENGTH"],
                    battlefields: ["P1-BATTLEFIELD-WASTE-HALL", "P1-BATTLEFIELD-ALLY"],
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
                ["P1-SPELL-SAVAGE-STRENGTH"] = new(
                    "P1-SPELL-SAVAGE-STRENGTH",
                    cardNo: "SFD·034/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-ALLY"] = new(
                    "P1-BATTLEFIELD-ALLY",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1,
                    power: 2),
                ["P1-BATTLEFIELD-WASTE-HALL"] = new(
                    "P1-BATTLEFIELD-WASTE-HALL",
                    cardNo: "UNL-205/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
        return seeded with { ObjectLocations = new Dictionary<string,ObjectLocationState>(seeded.ObjectLocations)
            { ["P1-BATTLEFIELD-WASTE-HALL"] = new(seed.P1,"BATTLEFIELD","P1-BATTLEFIELD-WASTE-HALL"), ["P1-BATTLEFIELD-ALLY"] = new(seed.P1,"BATTLEFIELD","P1-BATTLEFIELD-WASTE-HALL") } };

    }

    private static MatchState BuildBattlefieldHighCostSpellInsightScenario(MatchState current, DevScenarioSeed seed)
    {
        var seeded = BuildScenarioState(
            current,
            seed,
            2603303067,
            169,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(7, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-INSIGHT-RECYCLE", "P1-MAIN-KEEPER"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-MOONFALL"],
                    battlefields: ["P1-BATTLEFIELD-LOST-LIBRARY"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-ENEMY"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-MOONFALL"] = new(
                    "P1-SPELL-MOONFALL",
                    cardNo: "UNL-066/219",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-INSIGHT-RECYCLE"] = new(
                    "P1-INSIGHT-RECYCLE",
                    cardNo: "SFD·001/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-MAIN-KEEPER"] = new(
                    "P1-MAIN-KEEPER",
                    cardNo: "SFD·002/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-ENEMY"] = new(
                    "P2-BATTLEFIELD-ENEMY",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2,
                    power: 2),
                ["P1-BATTLEFIELD-LOST-LIBRARY"] = new(
                    "P1-BATTLEFIELD-LOST-LIBRARY",
                    cardNo: "UNL-211/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
        return seeded with {ObjectLocations = new Dictionary<string,ObjectLocationState>(seeded.ObjectLocations)
            { ["P1-BATTLEFIELD-LOST-LIBRARY"] = new(seed.P1,"BATTLEFIELD","P1-BATTLEFIELD-LOST-LIBRARY") }};

    }

    private static MatchState BuildBattlefieldUnitExperienceAbilityScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303075,
            175,
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
                    battlefields: ["P1-BATTLEFIELD-MUTATION-GARDEN", "P1-BATTLEFIELD-EXPERIENCE-UNIT"],
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
                ["P1-BATTLEFIELD-MUTATION-GARDEN"] = new(
                    "P1-BATTLEFIELD-MUTATION-GARDEN",
                    cardNo: "UNL-213/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-EXPERIENCE-UNIT"] = new(
                    "P1-BATTLEFIELD-EXPERIENCE-UNIT",
                    cardNo: "SFD·001/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
        return state with { ObjectLocations = new Dictionary<string, ObjectLocationState>
        {
            ["P1-BATTLEFIELD-MUTATION-GARDEN"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-MUTATION-GARDEN"),
            ["P1-BATTLEFIELD-EXPERIENCE-UNIT"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-MUTATION-GARDEN")
        } };
    }

    private static MatchState BuildBattlefieldReturnCallRuneScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303076,
            176,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-GHOST-BAY-RUNE-001", "P1-GHOST-BAY-RUNE-002"],
                    hand: ["P1-SPELL-RECONSIDER"],
                    battlefields: ["P1-BATTLEFIELD-GHOST-BAY", "P1-BATTLEFIELD-RETURN-UNIT"],
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
                ["P1-SPELL-RECONSIDER"] = new(
                    "P1-SPELL-RECONSIDER",
                    cardNo: "OGN·104/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-GHOST-BAY"] = new(
                    "P1-BATTLEFIELD-GHOST-BAY",
                    cardNo: "UNL-214/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-RETURN-UNIT"] = new(
                    "P1-BATTLEFIELD-RETURN-UNIT",
                    cardNo: "SFD·001/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-GHOST-BAY-RUNE-001"] = new(
                    "P1-GHOST-BAY-RUNE-001",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-GHOST-BAY-RUNE-002"] = new(
                    "P1-GHOST-BAY-RUNE-002",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldTargetDamageBonusScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303068,
            170,
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
                    hand: ["P1-SPELL-PUNISHMENT"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-VOID-GATE", "P2-BATTLEFIELD-TARGET"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-PUNISHMENT"] = new(
                    "P1-SPELL-PUNISHMENT",
                    cardNo: "UNL-007/219",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-TARGET"] = new(
                    "P2-BATTLEFIELD-TARGET",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2,
                    power: 5),
                ["P2-BATTLEFIELD-VOID-GATE"] = new(
                    "P2-BATTLEFIELD-VOID-GATE",
                    cardNo: "OGN·296/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
        return state with { ObjectLocations = new Dictionary<string, ObjectLocationState>
        {
            ["P2-BATTLEFIELD-VOID-GATE"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-VOID-GATE"),
            ["P2-BATTLEFIELD-TARGET"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-VOID-GATE")
        } };
    }

    private static MatchState BuildBattlefieldPlayUnitBoonScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303070,
            172,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(4, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-UNIT-CRAFTSMAN"],
                    battlefields: ["P1-BATTLEFIELD-IDOL-VALLEY"],
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
                ["P1-UNIT-CRAFTSMAN"] = new(
                    "P1-UNIT-CRAFTSMAN",
                    cardNo: "OGN·211/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-IDOL-VALLEY"] = new(
                    "P1-BATTLEFIELD-IDOL-VALLEY",
                    cardNo: "UNL-218/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldHeldUnitCostIncreaseScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303069,
            171,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(4, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-UNIT-CRAFTSMAN"],
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
                ["P1-UNIT-CRAFTSMAN"] = new(
                    "P1-UNIT-CRAFTSMAN",
                    cardNo: "OGN·211/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            }) with
            {
                UntilEndOfTurnEffects = [$"{BattlefieldHeldUnitCostIncreaseEffectPrefix}{seed.P1}"]
            };
    }

    private static MatchState BuildBattlefieldFirstUnitMoveOtherScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303071,
            173,
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
                    hand: ["P1-UNIT-CRAFTSMAN"],
                    battlefields: ["P1-BATTLEFIELD-METEOR-SPRING", "P1-BATTLEFIELD-ALLY"],
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
                ["P1-UNIT-CRAFTSMAN"] = new(
                    "P1-UNIT-CRAFTSMAN",
                    cardNo: "OGN·211/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-METEOR-SPRING"] = new(
                    "P1-BATTLEFIELD-METEOR-SPRING",
                    cardNo: "UNL-215/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-ALLY"] = new(
                    "P1-BATTLEFIELD-ALLY",
                    cardNo: "SFD·001/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1,
                    power: 2)
            });
    }

    private static MatchState BuildBattlefieldStaticPreventMoveBaseScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303062,
            162,
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
                    battlefields: ["P1-BATTLEFIELD-VILEMAW-LAIR", "P1-BATTLEFIELD-TRAPPED-UNIT"],
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
                ["P1-BATTLEFIELD-VILEMAW-LAIR"] = new(
                    "P1-BATTLEFIELD-VILEMAW-LAIR",
                    cardNo: "OGN·295/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-TRAPPED-UNIT"] = new(
                    "P1-BATTLEFIELD-TRAPPED-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
        return state with { ObjectLocations = new Dictionary<string, ObjectLocationState>
        {
            ["P1-BATTLEFIELD-VILEMAW-LAIR"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-VILEMAW-LAIR"),
            ["P1-BATTLEFIELD-TRAPPED-UNIT"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-VILEMAW-LAIR")
        } };
    }

    private static MatchState BuildBattlefieldStaticRoamScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303061,
            161,
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
                    battlefields: ["P1-BATTLEFIELD-WIND-HILL", "P1-BATTLEFIELD-WIND-RUNNER"],
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
                ["P1-BATTLEFIELD-WIND-HILL"] = new(
                    "P1-BATTLEFIELD-WIND-HILL",
                    cardNo: "OGN·297/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-WIND-RUNNER"] = new(
                    "P1-BATTLEFIELD-WIND-RUNNER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
        return state with { ObjectLocations = new Dictionary<string, ObjectLocationState>
        {
            ["P1-BATTLEFIELD-WIND-HILL"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-WIND-HILL"),
            ["P1-BATTLEFIELD-WIND-RUNNER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-WIND-HILL")
        } };
    }

    private static MatchState BuildBattlefieldConquerMillScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
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
                    mainDeck: ["P1-BATTLEFIELD-MILL-001", "P1-BATTLEFIELD-MILL-002", "P1-BATTLEFIELD-MILL-003"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-SCRAPYARD", "P1-BATTLEFIELD-CONQUER-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-CONQUER-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SCRAPYARD"] = new(
                    "P1-BATTLEFIELD-SCRAPYARD",
                    cardNo: "SFD·212/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-CONQUER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-CONQUER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-CONQUER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-CONQUER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            }) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-SCRAPYARD")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-SCRAPYARD"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-SCRAPYARD"),
                ["P1-BATTLEFIELD-CONQUER-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-SCRAPYARD"),
                ["P2-BATTLEFIELD-CONQUER-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-SCRAPYARD")
            }
        };
    }

    private static MatchState BuildBattlefieldConquerDiscardDrawScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303031,
            101,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-BATTLEFIELD-DRAW-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-BATTLEFIELD-DISCARD-001"],
                    battlefields: ["P1-BATTLEFIELD-ZAUN-SUMP", "P1-BATTLEFIELD-DISCARD-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-DISCARD-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ZAUN-SUMP"] = new(
                    "P1-BATTLEFIELD-ZAUN-SUMP",
                    cardNo: "OGN·298/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-DISCARD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-DISCARD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-DISCARD-001"] = new(
                    "P1-BATTLEFIELD-DISCARD-001",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-DISCARD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DISCARD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            }) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-ZAUN-SUMP")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-ZAUN-SUMP"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-ZAUN-SUMP"),
                ["P1-BATTLEFIELD-DISCARD-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-ZAUN-SUMP"),
                ["P2-BATTLEFIELD-DISCARD-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-ZAUN-SUMP")
            }
        };
    }

    private static MatchState BuildBattlefieldConquerRecycleRuneScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303034,
            104,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-002"],
                    baseZone: ["P1-BATTLEFIELD-RECYCLE-RUNE-001"],
                    battlefields: ["P1-BATTLEFIELD-THUNDER-RUNE", "P1-BATTLEFIELD-RECYCLE-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-RECYCLE-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-THUNDER-RUNE"] = new(
                    "P1-BATTLEFIELD-THUNDER-RUNE",
                    cardNo: "OGN·287/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-RECYCLE-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RECYCLE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-RECYCLE-RUNE-001"] = new(
                    "P1-BATTLEFIELD-RECYCLE-RUNE-001",
                    tags: [CardObjectTags.RuneCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-RECYCLE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-RECYCLE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            }) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-THUNDER-RUNE")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-THUNDER-RUNE"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-THUNDER-RUNE"),
                ["P1-BATTLEFIELD-RECYCLE-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-THUNDER-RUNE"),
                ["P2-BATTLEFIELD-RECYCLE-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-THUNDER-RUNE")
            }
        };
    }

    private static MatchState BuildBattlefieldDefendRevealSpellScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303035,
            105,
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
                    battlefields: ["P1-BATTLEFIELD-REVEAL-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-BATTLEFIELD-REVEAL-SPELL", "P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-RAVENBLOOM", "P2-BATTLEFIELD-REVEAL-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-REVEAL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-REVEAL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-RAVENBLOOM"] = new(
                    "P2-BATTLEFIELD-RAVENBLOOM",
                    cardNo: "SFD·215/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-REVEAL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-REVEAL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-REVEAL-SPELL"] = new(
                    "P2-BATTLEFIELD-REVEAL-SPELL",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldRunesScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303030,
            100,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-BATTLEFIELD-RUNE-001"],
                    battlefields: ["P1-BATTLEFIELD-RUNES-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-BATTLEFIELD-RUNE-001"],
                    battlefields: ["P2-BATTLEFIELD-CONFETTI-TREE", "P2-BATTLEFIELD-RUNES-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-RUNES-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RUNES-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-CONFETTI-TREE"] = new(
                    "P2-BATTLEFIELD-CONFETTI-TREE",
                    cardNo: "SFD·219/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-RUNES-DEFENDER"] = new(
                    "P2-BATTLEFIELD-RUNES-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldRuneScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303041,
            111,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001"],
                    battlefields: ["P1-BATTLEFIELD-SINGLE-RUNE-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-BATTLEFIELD-SINGLE-RUNE-001"],
                    battlefields: ["P2-BATTLEFIELD-STAR-PEAK", "P2-BATTLEFIELD-SINGLE-RUNE-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SINGLE-RUNE-ATTACKER"] = new(
                    "P1-BATTLEFIELD-SINGLE-RUNE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-STAR-PEAK"] = new(
                    "P2-BATTLEFIELD-STAR-PEAK",
                    cardNo: "OGN·288/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-SINGLE-RUNE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-SINGLE-RUNE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldStaticPowerScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
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
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-POWER-PLUS", "P1-BATTLEFIELD-STATIC-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-STATIC-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-POWER-PLUS"] = new(
                    "P1-BATTLEFIELD-POWER-PLUS",
                    cardNo: "OGN·294/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-STATIC-ATTACKER"] = new(
                    "P1-BATTLEFIELD-STATIC-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-STATIC-DEFENDER"] = new(
                    "P2-BATTLEFIELD-STATIC-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldDefenderSteadfastScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303033,
            103,
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
                    baseZone: ["P1-BATTLEFIELD-FORTIFIED-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-FORTIFIED-POSITION", "P2-BATTLEFIELD-FORTIFIED-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-FORTIFIED-ATTACKER"] = new(
                    "P1-BATTLEFIELD-FORTIFIED-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-FORTIFIED-POSITION"] = new(
                    "P2-BATTLEFIELD-FORTIFIED-POSITION",
                    cardNo: "OGN·279/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-FORTIFIED-DEFENDER"] = new(
                    "P2-BATTLEFIELD-FORTIFIED-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
        return state with { ObjectLocations = new Dictionary<string, ObjectLocationState> {
            ["P1-BATTLEFIELD-FORTIFIED-ATTACKER"] = new(seed.P1, "BASE"),
            ["P2-BATTLEFIELD-FORTIFIED-POSITION"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-FORTIFIED-POSITION"),
            ["P2-BATTLEFIELD-FORTIFIED-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-FORTIFIED-POSITION") } };
    }

    private static MatchState BuildBattlefieldDefendMoveToBaseScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303044,
            114,
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
                    battlefields: ["P1-BATTLEFIELD-PLUNDER-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-PLUNDER-ALLEY", "P2-BATTLEFIELD-PLUNDER-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-PLUNDER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-PLUNDER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-PLUNDER-ALLEY"] = new(
                    "P2-BATTLEFIELD-PLUNDER-ALLEY",
                    cardNo: "OGN·285/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-PLUNDER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-PLUNDER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldIsolatedDefenderScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303036,
            106,
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
                    battlefields: ["P1-BATTLEFIELD-ISOLATED-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-FORBIDDEN-WASTELAND", "P2-BATTLEFIELD-ISOLATED-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ISOLATED-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ISOLATED-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-FORBIDDEN-WASTELAND"] = new(
                    "P2-BATTLEFIELD-FORBIDDEN-WASTELAND",
                    cardNo: "UNL-210/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-ISOLATED-DEFENDER"] = new(
                    "P2-BATTLEFIELD-ISOLATED-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldConquerReadyLegendScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303037,
            107,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-LEGEND-HALL", "P1-BATTLEFIELD-READY-ATTACKER"],
                    legendZone: ["P1-LEGEND-READY-TARGET"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-READY-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-LEGEND-HALL"] = new(
                    "P1-BATTLEFIELD-LEGEND-HALL",
                    cardNo: "SFD·210/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-READY-ATTACKER"] = new(
                    "P1-BATTLEFIELD-READY-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-LEGEND-READY-TARGET"] = new(
                    "P1-LEGEND-READY-TARGET",
                    cardNo: "TEST-LEGEND-READY-TARGET",
                    isExhausted: true,
                    tags: ["CARD_TYPE:LEGEND"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-READY-DEFENDER"] = new(
                    "P2-BATTLEFIELD-READY-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            }) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-LEGEND-HALL")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-LEGEND-HALL"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-LEGEND-HALL"),
                ["P1-BATTLEFIELD-READY-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-LEGEND-HALL"),
                ["P2-BATTLEFIELD-READY-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-LEGEND-HALL")
            }
        };
    }

    private static MatchState BuildBattlefieldConquerReadyRunesEndScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303054,
            124,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-DECK-001"],
                    baseZone: ["P1-BATTLEFIELD-READY-RUNE-001", "P1-BATTLEFIELD-READY-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-MOUNT-TARGON", "P1-BATTLEFIELD-RUNE-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-RUNE-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MOUNT-TARGON"] = new(
                    "P1-BATTLEFIELD-MOUNT-TARGON",
                    cardNo: "OGN·289/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-RUNE-ATTACKER"] = new(
                    "P1-BATTLEFIELD-RUNE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-RUNE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-RUNE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-READY-RUNE-001"] = new(
                    "P1-BATTLEFIELD-READY-RUNE-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-READY-RUNE-002"] = new(
                    "P1-BATTLEFIELD-READY-RUNE-002",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            }) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-MOUNT-TARGON")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-MOUNT-TARGON"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-MOUNT-TARGON"),
                ["P1-BATTLEFIELD-RUNE-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-MOUNT-TARGON"),
                ["P2-BATTLEFIELD-RUNE-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-MOUNT-TARGON")
            }
        };
    }

    private static MatchState BuildBattlefieldConquerDrawOtherScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303038,
            108,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck:
                    [
                        "P1-BATTLEFIELD-DRAW-OTHER-001",
                        "P1-BATTLEFIELD-DRAW-OTHER-002",
                        "P1-BATTLEFIELD-DRAW-OTHER-003"
                    ],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields:
                    [
                        "P1-BATTLEFIELD-THRONE-OF-POWER",
                        "P1-BATTLEFIELD-OTHER-001",
                        "P1-BATTLEFIELD-OTHER-002",
                        "P1-BATTLEFIELD-DRAW-OTHER-ATTACKER"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-DRAW-OTHER-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-THRONE-OF-POWER"] = new(
                    "P1-BATTLEFIELD-THRONE-OF-POWER",
                    cardNo: "SFD·217/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-OTHER-001"] = new(
                    "P1-BATTLEFIELD-OTHER-001",
                    cardNo: "OGN·280/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-OTHER-002"] = new(
                    "P1-BATTLEFIELD-OTHER-002",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-DRAW-OTHER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-DRAW-OTHER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-DRAW-OTHER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-DRAW-OTHER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-DRAW-OTHER-001"] = new(
                    "P1-BATTLEFIELD-DRAW-OTHER-001",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-DRAW-OTHER-002"] = new(
                    "P1-BATTLEFIELD-DRAW-OTHER-002",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-DRAW-OTHER-003"] = new(
                    "P1-BATTLEFIELD-DRAW-OTHER-003",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            }) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-THRONE-OF-POWER")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-THRONE-OF-POWER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-THRONE-OF-POWER"),
                ["P1-BATTLEFIELD-DRAW-OTHER-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-THRONE-OF-POWER"),
                ["P2-BATTLEFIELD-DRAW-OTHER-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-THRONE-OF-POWER")
            }
        };
    }

    private static MatchState BuildBattlefieldConquerPowerfulDrawScenario(MatchState current, DevScenarioSeed seed)
    {
        var scenario = BuildScenarioState(
            current,
            seed,
            2603303039,
            109,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-BATTLEFIELD-POWERFUL-DRAW-001", "P1-BATTLEFIELD-POWERFUL-DRAW-002"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-SUNKEN-TEMPLE", "P1-BATTLEFIELD-POWERFUL-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-POWERFUL-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SUNKEN-TEMPLE"] = new(
                    "P1-BATTLEFIELD-SUNKEN-TEMPLE",
                    cardNo: "SFD·218/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-POWERFUL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-POWERFUL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-POWERFUL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-POWERFUL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-POWERFUL-DRAW-001"] = new(
                    "P1-BATTLEFIELD-POWERFUL-DRAW-001",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-POWERFUL-DRAW-002"] = new(
                    "P1-BATTLEFIELD-POWERFUL-DRAW-002",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });

        return scenario with { UntilEndOfTurnEffects = scenario.UntilEndOfTurnEffects.Append(BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-SUNKEN-TEMPLE")).Distinct(StringComparer.Ordinal).ToArray(),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(scenario.ObjectLocations) {
            ["P1-BATTLEFIELD-SUNKEN-TEMPLE"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-SUNKEN-TEMPLE"),
            ["P1-BATTLEFIELD-POWERFUL-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-SUNKEN-TEMPLE"),
            ["P2-BATTLEFIELD-POWERFUL-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-SUNKEN-TEMPLE") } };
    }

    private static MatchState BuildBattlefieldConquerSandSoldierScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303053,
            123,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-EMPEROR-SHRINE", "P1-BATTLEFIELD-SAND-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-SAND-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-EMPEROR-SHRINE"] = new(
                    "P1-BATTLEFIELD-EMPEROR-SHRINE",
                    cardNo: "SFD·207/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-SAND-ATTACKER"] = new(
                    "P1-BATTLEFIELD-SAND-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-SAND-DEFENDER"] = new(
                    "P2-BATTLEFIELD-SAND-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            }) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-EMPEROR-SHRINE")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-EMPEROR-SHRINE"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-EMPEROR-SHRINE"),
                ["P1-BATTLEFIELD-SAND-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-EMPEROR-SHRINE"),
                ["P2-BATTLEFIELD-SAND-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-EMPEROR-SHRINE")
            }
        };
    }

    private static MatchState BuildBattlefieldConquerGoldScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303040,
            110,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-TREASURE-PILE", "P1-BATTLEFIELD-GOLD-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-GOLD-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-TREASURE-PILE"] = new(
                    "P1-BATTLEFIELD-TREASURE-PILE",
                    cardNo: "SFD·220/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-GOLD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-GOLD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-GOLD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-GOLD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            }) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-TREASURE-PILE")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-TREASURE-PILE"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-TREASURE-PILE"),
                ["P1-BATTLEFIELD-GOLD-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-TREASURE-PILE"),
                ["P2-BATTLEFIELD-GOLD-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-TREASURE-PILE")
            }
        };
    }

    private static MatchState BuildBattlefieldConquerReadyEquipmentScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303042,
            112,
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
                    baseZone: ["P1-BATTLEFIELD-EQUIPMENT-HOST", "P1-BATTLEFIELD-ARMAMENT"],
                    battlefields: ["P1-BATTLEFIELD-MOONVEIL-ALTAR", "P1-BATTLEFIELD-EQUIPMENT-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-EQUIPMENT-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MOONVEIL-ALTAR"] = new(
                    "P1-BATTLEFIELD-MOONVEIL-ALTAR",
                    cardNo: "SFD·221/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-EQUIPMENT-ATTACKER"] = new(
                    "P1-BATTLEFIELD-EQUIPMENT-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-EQUIPMENT-HOST"] = new(
                    "P1-BATTLEFIELD-EQUIPMENT-HOST",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-ARMAMENT"] = new(
                    "P1-BATTLEFIELD-ARMAMENT",
                    cardNo: "SFD·022/221",
                    isExhausted: true,
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    attachedToObjectId: "P1-BATTLEFIELD-EQUIPMENT-HOST",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-EQUIPMENT-DEFENDER"] = new(
                    "P2-BATTLEFIELD-EQUIPMENT-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            }) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-MOONVEIL-ALTAR")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-MOONVEIL-ALTAR"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-MOONVEIL-ALTAR"),
                ["P1-BATTLEFIELD-EQUIPMENT-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-MOONVEIL-ALTAR"),
                ["P2-BATTLEFIELD-EQUIPMENT-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-MOONVEIL-ALTAR")
            }
        };
    }
}
