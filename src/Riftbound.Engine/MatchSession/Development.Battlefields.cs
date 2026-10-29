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

    private static MatchState BuildBattleDeclareScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303025,
            95,
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
                    battlefields: ["P1-BATTLE-ATTACKER-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLE-DEFENDER-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-ATTACKER-001"] = new(
                    "P1-BATTLE-ATTACKER-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-DEFENDER-001"] = new(
                    "P2-BATTLE-DEFENDER-001",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlePromptFilterScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303071,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    baseZone: ["P1-BATTLE-PROMPT-BASE-UNIT"],
                    battlefields:
                    [
                        "P1-BATTLE-PROMPT-ATTACKER",
                        "P1-BATTLE-PROMPT-FACEDOWN",
                        "P1-BATTLE-PROMPT-ALREADY-ATTACKING"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    baseZone: ["P2-BATTLE-PROMPT-BASE-DEFENDER"],
                    battlefields:
                    [
                        "P2-BATTLE-PROMPT-DEFENDER",
                        "P2-BATTLE-PROMPT-FACEDOWN",
                        "P2-BATTLE-PROMPT-ALREADY-DEFENDING",
                        "P2-BATTLE-PROMPT-EQUIPMENT"
                    ],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-PROMPT-BASE-UNIT"] = new(
                    "P1-BATTLE-PROMPT-BASE-UNIT",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-PROMPT-ATTACKER"] = new(
                    "P1-BATTLE-PROMPT-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-PROMPT-FACEDOWN"] = new(
                    "P1-BATTLE-PROMPT-FACEDOWN",
                    isFaceDown: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-PROMPT-ALREADY-ATTACKING"] = new(
                    "P1-BATTLE-PROMPT-ALREADY-ATTACKING",
                    isAttacking: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-PROMPT-BASE-DEFENDER"] = new(
                    "P2-BATTLE-PROMPT-BASE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-PROMPT-DEFENDER"] = new(
                    "P2-BATTLE-PROMPT-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-PROMPT-FACEDOWN"] = new(
                    "P2-BATTLE-PROMPT-FACEDOWN",
                    isFaceDown: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-PROMPT-ALREADY-DEFENDING"] = new(
                    "P2-BATTLE-PROMPT-ALREADY-DEFENDING",
                    isDefending: true,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-PROMPT-EQUIPMENT"] = new(
                    "P2-BATTLE-PROMPT-EQUIPMENT",
                    cardNo: "SFD·011/221",
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattleMultiDefenderScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303072,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLE-MULTI-VOLIBEAR"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLE-MULTI-LEBLANC", "P2-BATTLE-MULTI-KITTEN"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-MULTI-VOLIBEAR"] = new(
                    "P1-BATTLE-MULTI-VOLIBEAR",
                    cardNo: "OGN·158/298",
                    power: 10,
                    tags: [CardObjectTags.UnitCard, "坚守3", "壁垒"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-MULTI-LEBLANC"] = new(
                    "P2-BATTLE-MULTI-LEBLANC",
                    cardNo: "UNL-090/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "后排"],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-MULTI-KITTEN"] = new(
                    "P2-BATTLE-MULTI-KITTEN",
                    cardNo: "UNL-036/219",
                    power: 1,
                    tags: [CardObjectTags.UnitCard, "坚守2", "壁垒", "猫科"],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattleSamePriorityBulwarkScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303074,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLE-SAME-VOLIBEAR"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLE-SAME-BULWARK-A", "P2-BATTLE-SAME-BULWARK-B"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-SAME-VOLIBEAR"] = new(
                    "P1-BATTLE-SAME-VOLIBEAR",
                    cardNo: "OGN·158/298",
                    power: 10,
                    tags: [CardObjectTags.UnitCard, "壁垒"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-SAME-BULWARK-A"] = new(
                    "P2-BATTLE-SAME-BULWARK-A",
                    cardNo: "UNL-036/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "壁垒"],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-SAME-BULWARK-B"] = new(
                    "P2-BATTLE-SAME-BULWARK-B",
                    cardNo: "UNL-036/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "壁垒"],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattleMultiAttackerScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303073,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLE-MULTI-GAREN", "P1-BATTLE-MULTI-YI"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLE-MULTI-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-MULTI-GAREN"] = new(
                    "P1-BATTLE-MULTI-GAREN",
                    cardNo: "OGS·007/024",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-MULTI-YI"] = new(
                    "P1-BATTLE-MULTI-YI",
                    cardNo: "UNL-059/219",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-MULTI-DEFENDER"] = new(
                    "P2-BATTLE-MULTI-DEFENDER",
                    cardNo: "UNL-036/219",
                    power: 6,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattleMultiParticipantScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303076,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["BATTLEFIELD:P1-MAIN", "P1-BATTLE-MULTI-PARTICIPANT-GAREN", "P1-BATTLE-MULTI-PARTICIPANT-YI"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLE-MULTI-PARTICIPANT-BULWARK", "P2-BATTLE-MULTI-PARTICIPANT-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BATTLEFIELD:P1-MAIN"] = new("BATTLEFIELD:P1-MAIN", cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: seed.P1, controllerId: seed.P2),
                ["P1-BATTLE-MULTI-PARTICIPANT-GAREN"] = new(
                    "P1-BATTLE-MULTI-PARTICIPANT-GAREN",
                    cardNo: "OGS·007/024",
                    power: 6,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLE-MULTI-PARTICIPANT-YI"] = new(
                    "P1-BATTLE-MULTI-PARTICIPANT-YI",
                    cardNo: "UNL-059/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-MULTI-PARTICIPANT-BULWARK"] = new(
                    "P2-BATTLE-MULTI-PARTICIPANT-BULWARK",
                    cardNo: "UNL-036/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "壁垒"],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLE-MULTI-PARTICIPANT-DEFENDER"] = new(
                    "P2-BATTLE-MULTI-PARTICIPANT-DEFENDER",
                    cardNo: "UNL-036/219",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
        return state with {
            ObjectLocations = new Dictionary<string, ObjectLocationState> {
                ["BATTLEFIELD:P1-MAIN"] = new(seed.P1, "BATTLEFIELD", "BATTLEFIELD:P1-MAIN"),
                ["P1-BATTLE-MULTI-PARTICIPANT-GAREN"] = new(seed.P1, "BATTLEFIELD", "BATTLEFIELD:P1-MAIN"),
                ["P1-BATTLE-MULTI-PARTICIPANT-YI"] = new(seed.P1, "BATTLEFIELD", "BATTLEFIELD:P1-MAIN"),
                ["P2-BATTLE-MULTI-PARTICIPANT-BULWARK"] = new(seed.P2, "BATTLEFIELD", "BATTLEFIELD:P1-MAIN"),
                ["P2-BATTLE-MULTI-PARTICIPANT-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "BATTLEFIELD:P1-MAIN") },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("BATTLEFIELD:P1-MAIN")] };
    }

    private static MatchState BuildBattleNoResultScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303075,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLE-NO-RESULT-GAREN"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLE-NO-RESULT-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-NO-RESULT-GAREN"] = new(
                    "P1-BATTLE-NO-RESULT-GAREN",
                    cardNo: "OGS·007/024",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLE-NO-RESULT-DEFENDER"] = new(
                    "P2-BATTLE-NO-RESULT-DEFENDER",
                    cardNo: "UNL-036/219",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldEphemeralSteadfastScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303032,
            102,
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
                    battlefields: ["P1-BATTLEFIELD-EPHEMERAL-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-BLACK-FLAME", "P2-BATTLEFIELD-EPHEMERAL-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-EPHEMERAL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-EPHEMERAL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-BLACK-FLAME"] = new(
                    "P2-BATTLEFIELD-BLACK-FLAME",
                    cardNo: "UNL-208/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-EPHEMERAL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-EPHEMERAL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldMinionScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303029,
            99,
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
                    battlefields: ["P1-BATTLEFIELD-MINION-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-UNITY-SANCTUM", "P2-BATTLEFIELD-MINION-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-MINION-ATTACKER"] = new(
                    "P1-BATTLEFIELD-MINION-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-UNITY-SANCTUM"] = new(
                    "P2-BATTLEFIELD-UNITY-SANCTUM",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-MINION-DEFENDER"] = new(
                    "P2-BATTLEFIELD-MINION-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldDrawScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303026,
            96,
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
                    battlefields: ["P1-BATTLEFIELD-HELD-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-BATTLEFIELD-HELD-DRAW-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-DREAM-TREE", "P2-BATTLEFIELD-HELD-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-HELD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-HELD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-DREAM-TREE"] = new(
                    "P2-BATTLEFIELD-DREAM-TREE",
                    cardNo: "OGN·280/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-HELD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-HELD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldMoveToBaseScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303046,
            116,
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
                    battlefields: ["P1-BATTLEFIELD-REHEARSAL-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-REHEARSAL-HALL", "P2-BATTLEFIELD-REHEARSAL-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-REHEARSAL-ATTACKER"] = new(
                    "P1-BATTLEFIELD-REHEARSAL-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-REHEARSAL-HALL"] = new(
                    "P2-BATTLEFIELD-REHEARSAL-HALL",
                    cardNo: "UNL-207/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-REHEARSAL-DEFENDER"] = new(
                    "P2-BATTLEFIELD-REHEARSAL-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldBoonScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303043,
            113,
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
                    battlefields: ["P1-BATTLEFIELD-BOON-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-NAVORI-ARENA", "P2-BATTLEFIELD-BOON-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-BOON-ATTACKER"] = new(
                    "P1-BATTLEFIELD-BOON-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-NAVORI-ARENA"] = new(
                    "P2-BATTLEFIELD-NAVORI-ARENA",
                    cardNo: "OGN·283/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-BOON-DEFENDER"] = new(
                    "P2-BATTLEFIELD-BOON-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldReturnHeroScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303059,
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
                    battlefields: ["P1-BATTLEFIELD-TOMB-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-HALLOWED-TOMB", "P2-BATTLEFIELD-TOMB-DEFENDER"],
                    graveyard: ["P2-HERO-TOMB-RETURN"],
                    legendZone: ["P2-LEGEND-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-TOMB-ATTACKER"] = new(
                    "P1-BATTLEFIELD-TOMB-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-HALLOWED-TOMB"] = new(
                    "P2-BATTLEFIELD-HALLOWED-TOMB",
                    cardNo: "OGN·281/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-TOMB-DEFENDER"] = new(
                    "P2-BATTLEFIELD-TOMB-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-HERO-TOMB-RETURN"] = new(
                    "P2-HERO-TOMB-RETURN",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, "CARD_CATEGORY:英雄单位"],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldConquerBoonDrawScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303045,
            115,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-BATTLEFIELD-BOON-DRAW-CARD"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-SHIRANA-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-SHIRANA-MONASTERY", "P2-BATTLEFIELD-SHIRANA-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-SHIRANA-ATTACKER"] = new(
                    "P1-BATTLEFIELD-SHIRANA-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Boon, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-BOON-DRAW-CARD"] = new(
                    "P1-BATTLEFIELD-BOON-DRAW-CARD",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-SHIRANA-MONASTERY"] = new(
                    "P2-BATTLEFIELD-SHIRANA-MONASTERY",
                    cardNo: "OGN·282/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-SHIRANA-DEFENDER"] = new(
                    "P2-BATTLEFIELD-SHIRANA-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            }) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P2-BATTLEFIELD-SHIRANA-MONASTERY")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P2-BATTLEFIELD-SHIRANA-MONASTERY"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-SHIRANA-MONASTERY"),
                ["P1-BATTLEFIELD-SHIRANA-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P2-BATTLEFIELD-SHIRANA-MONASTERY"),
                ["P2-BATTLEFIELD-SHIRANA-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-SHIRANA-MONASTERY")
            }
        };
    }

    private static MatchState BuildBattlefieldConquerWarhawkScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303047,
            117,
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
                    battlefields: ["P1-BATTLEFIELD-HUNTING-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-HUNTING-GROUNDS", "P2-BATTLEFIELD-HUNTING-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-HUNTING-ATTACKER"] = new(
                    "P1-BATTLEFIELD-HUNTING-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-HUNTING-GROUNDS"] = new(
                    "P2-BATTLEFIELD-HUNTING-GROUNDS",
                    cardNo: "UNL-217/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-HUNTING-DEFENDER"] = new(
                    "P2-BATTLEFIELD-HUNTING-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            }) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P2-BATTLEFIELD-HUNTING-GROUNDS")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P2-BATTLEFIELD-HUNTING-GROUNDS"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-HUNTING-GROUNDS"),
                ["P1-BATTLEFIELD-HUNTING-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P2-BATTLEFIELD-HUNTING-GROUNDS"),
                ["P2-BATTLEFIELD-HUNTING-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-HUNTING-GROUNDS")
            }
        };
    }

    private static MatchState BuildBattlefieldWinningScoreScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303048,
            117,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-HIGHROAD"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    graveyard: ["P2-BURNOUT-RECYCLE-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-HIGHROAD"] = new(
                    "P1-BATTLEFIELD-HIGHROAD",
                    cardNo: BattlefieldWinningScoreSeedCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BURNOUT-RECYCLE-001"] = new(
                    "P2-BURNOUT-RECYCLE-001",
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            }) with
            {
                PlayerScores = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [seed.P1] = 7,
                    [seed.P2] = 0
                }
            };
    }

    private static MatchState BuildBattlefieldFirstTurnRuneScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303049,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-RUNE-OBELISK"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck:
                    [
                        "P2-RUNE-001",
                        "P2-RUNE-002",
                        "P2-RUNE-003",
                        "P2-RUNE-004"
                    ],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-RUNE-OBELISK"] = new(
                    "P1-BATTLEFIELD-RUNE-OBELISK",
                    cardNo: "OGN·284/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldFirstTurnScoreScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303050,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-GLORY-ARENA"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GLORY-ARENA"] = new(
                    "P1-BATTLEFIELD-GLORY-ARENA",
                    cardNo: "OGN·290/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldScoreDelayScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303052,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-GLORY-ARENA", "P1-BATTLEFIELD-FORGOTTEN-MONUMENT"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-GLORY-ARENA"] = new(
                    "P1-BATTLEFIELD-GLORY-ARENA",
                    cardNo: "OGN·290/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-FORGOTTEN-MONUMENT"] = new(
                    "P1-BATTLEFIELD-FORGOTTEN-MONUMENT",
                    cardNo: "SFD·209/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldBattleDestroyedRecallScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303067,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = new(3, 0)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-BLOOD-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLEFIELD-BLOOD-ALTAR", "P2-BATTLEFIELD-BLOOD-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-BLOOD-ATTACKER"] = new(
                    "P1-BATTLEFIELD-BLOOD-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-BLOOD-ALTAR"] = new(
                    "P2-BATTLEFIELD-BLOOD-ALTAR",
                    cardNo: "UNL-206/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-BLOOD-DEFENDER"] = new(
                    "P2-BATTLEFIELD-BLOOD-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldTurnStartDamageScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303058,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-FROST-HOLD", "P1-BATTLEFIELD-FROST-FALLING"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003"],
                    battlefields: ["P2-BATTLEFIELD-FROST-OTHER", "P2-BATTLEFIELD-FROST-SURVIVOR"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-FROST-HOLD"] = new(
                    "P1-BATTLEFIELD-FROST-HOLD",
                    cardNo: "UNL-212/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-FROST-FALLING"] = new(
                    "P1-BATTLEFIELD-FROST-FALLING",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-FROST-SURVIVOR"] = new(
                    "P2-BATTLEFIELD-FROST-SURVIVOR",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-FROST-OTHER"] = new(
                    "P2-BATTLEFIELD-FROST-OTHER",
                    cardNo: "OGN\u00b7278/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-FROST-HOLD"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-FROST-HOLD"),
                ["P1-BATTLEFIELD-FROST-FALLING"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-FROST-HOLD"),
                ["P2-BATTLEFIELD-FROST-SURVIVOR"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-FROST-OTHER"),
                ["P2-BATTLEFIELD-FROST-OTHER"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-FROST-OTHER"),
                ["P2-MAIN-001"] = new(seed.P2, "MAIN_DECK"),
                ["P2-RUNE-001"] = new(seed.P2, "RUNE_DECK"),
                ["P2-RUNE-002"] = new(seed.P2, "RUNE_DECK"),
                ["P2-RUNE-003"] = new(seed.P2, "RUNE_DECK")
            }
        };
    }

    private static MatchState BuildBattlefieldTurnStartDestroyDrawScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303060,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-ROSE-DRAW-001", "P2-NORMAL-DRAW-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002", "P2-RUNE-003"],
                    battlefields: ["P2-BATTLEFIELD-AAA-OFFSITE", "P2-BATTLEFIELD-ROSE-LAB", "P2-BATTLEFIELD-ROSE-SACRIFICE", "P2-OTHER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-AAA-OFFSITE"] = new(
                    "P2-BATTLEFIELD-AAA-OFFSITE",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-ROSE-LAB"] = new(
                    "P2-BATTLEFIELD-ROSE-LAB",
                    cardNo: "UNL-209/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-OTHER"] = new(
                    "P2-OTHER",
                    cardNo: "OGN\u00b7278/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-ROSE-SACRIFICE"] = new(
                    "P2-BATTLEFIELD-ROSE-SACRIFICE",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-ROSE-DRAW-001"] = new(
                    "P2-ROSE-DRAW-001",
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-NORMAL-DRAW-001"] = new(
                    "P2-NORMAL-DRAW-001",
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P2-BATTLEFIELD-AAA-OFFSITE"] = new(seed.P2, "BATTLEFIELD", "P2-OTHER"),
                ["P2-OTHER"] = new(seed.P2, "BATTLEFIELD", "P2-OTHER"),
                ["P2-BATTLEFIELD-ROSE-LAB"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-ROSE-LAB"),
                ["P2-BATTLEFIELD-ROSE-SACRIFICE"] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-ROSE-LAB"),
                ["P2-ROSE-DRAW-001"] = new(seed.P2, "MAIN_DECK"),
                ["P2-NORMAL-DRAW-001"] = new(seed.P2, "MAIN_DECK"),
                ["P2-RUNE-001"] = new(seed.P2, "RUNE_DECK"),
                ["P2-RUNE-002"] = new(seed.P2, "RUNE_DECK"),
                ["P2-RUNE-003"] = new(seed.P2, "RUNE_DECK")
            }
        };
    }

    private static MatchState BuildBattlefieldHeldScoreScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303051,
            151,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = new(0, 4)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-ENERGY-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-ENERGY-HUB", "P2-BATTLEFIELD-ENERGY-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ENERGY-ATTACKER"] = new(
                    "P1-BATTLEFIELD-ENERGY-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-ENERGY-HUB"] = new(
                    "P2-BATTLEFIELD-ENERGY-HUB",
                    cardNo: "SFD·214/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-ENERGY-DEFENDER"] = new(
                    "P2-BATTLEFIELD-ENERGY-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldSevenUnitsWinScenario(MatchState current, DevScenarioSeed seed)
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
                ownerId: seed.P1,
                controllerId: seed.P1),
            ["P2-BATTLEFIELD-GRAND-PLAZA"] = new(
                "P2-BATTLEFIELD-GRAND-PLAZA",
                cardNo: "OGN·293/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: seed.P2,
                controllerId: seed.P2)
        };
        foreach (var defenderUnitObjectId in defenderUnitObjectIds)
        {
            cardObjects[defenderUnitObjectId] = new CardObjectState(
                defenderUnitObjectId,
                cardNo: "SFD·125/221",
                power: string.Equals(defenderUnitObjectId, defenderUnitObjectIds[0], StringComparison.Ordinal) ? 3 : 1,
                tags: [CardObjectTags.UnitCard],
                ownerId: seed.P2,
                controllerId: seed.P2);
        }

        var state = BuildScenarioState(
            current,
            seed,
            2603303052,
            152,
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
                    battlefields: ["P1-BATTLEFIELD-GRAND-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: new[] { "P2-BATTLEFIELD-GRAND-PLAZA" }.Concat(defenderUnitObjectIds).ToArray(),
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            cardObjects);

        var objectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
        {
            ["P1-BATTLEFIELD-GRAND-ATTACKER"] = new(seed.P1, "BATTLEFIELD"),
            ["P2-BATTLEFIELD-GRAND-PLAZA"] = new(seed.P2, "BATTLEFIELD")
        };
        foreach (var defenderUnitObjectId in defenderUnitObjectIds)
        {
            objectLocations[defenderUnitObjectId] = new(seed.P2, "BATTLEFIELD", "P2-BATTLEFIELD-GRAND-PLAZA");
        }

        return state with { ObjectLocations = objectLocations };
    }

    private static MatchState BuildBattlefieldConquerRevealRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303053,
            153,
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
                        "P1-BATTLEFIELD-CANDLE-001",
                        "P1-BATTLEFIELD-CANDLE-002",
                        "P1-BATTLEFIELD-CANDLE-003"
                    ],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    battlefields: ["P1-BATTLEFIELD-CANDLELIT-SANCTUM", "P1-BATTLEFIELD-CANDLE-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-BATTLEFIELD-CANDLE-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-CANDLELIT-SANCTUM"] = new(
                    "P1-BATTLEFIELD-CANDLELIT-SANCTUM",
                    cardNo: "OGN·291/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-CANDLE-ATTACKER"] = new(
                    "P1-BATTLEFIELD-CANDLE-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardResourceKeywordNames.Hunt],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-CANDLE-DEFENDER"] = new(
                    "P2-BATTLEFIELD-CANDLE-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P1-BATTLEFIELD-CANDLE-001"] = new(
                    "P1-BATTLEFIELD-CANDLE-001",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-CANDLE-002"] = new(
                    "P1-BATTLEFIELD-CANDLE-002",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-CANDLE-003"] = new(
                    "P1-BATTLEFIELD-CANDLE-003",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            }) with
        {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("P1-BATTLEFIELD-CANDLELIT-SANCTUM")],
            ObjectLocations = new Dictionary<string, ObjectLocationState>
            {
                ["P1-BATTLEFIELD-CANDLELIT-SANCTUM"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-CANDLELIT-SANCTUM"),
                ["P1-BATTLEFIELD-CANDLE-ATTACKER"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-CANDLELIT-SANCTUM"),
                ["P2-BATTLEFIELD-CANDLE-DEFENDER"] = new(seed.P2, "BATTLEFIELD", "P1-BATTLEFIELD-CANDLELIT-SANCTUM")
            }
        };
    }

    private static MatchState BuildBattlefieldMovePowerScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303063,
            163,
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
                    battlefields: ["P1-BATTLEFIELD-BACK-ALLEY-BAR", "P1-BATTLEFIELD-BAR-REGULAR"],
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
                ["P1-BATTLEFIELD-BACK-ALLEY-BAR"] = new(
                    "P1-BATTLEFIELD-BACK-ALLEY-BAR",
                    cardNo: "OGN·277/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-BAR-REGULAR"] = new(
                    "P1-BATTLEFIELD-BAR-REGULAR",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
        return state with { ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
            ["P1-BATTLEFIELD-BACK-ALLEY-BAR"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-BACK-ALLEY-BAR"),
            ["P1-BATTLEFIELD-BAR-REGULAR"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-BACK-ALLEY-BAR") } };
    }

    private static MatchState BuildBattlefieldStaticPreventPlayUnitsScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303064,
            164,
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
                    battlefields: ["P1-BATTLEFIELD-FALLING-ROCKS", "P1-BATTLEFIELD-FRIENDLY-001"],
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
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 3,
                    cardNo: "OGN·012/298"),
                ["P1-BATTLEFIELD-FALLING-ROCKS"] = new(
                    "P1-BATTLEFIELD-FALLING-ROCKS",
                    cardNo: "SFD·216/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new(
                    "P1-BATTLEFIELD-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });

        return state with { ObjectLocations = new Dictionary<string, ObjectLocationState>
        {
            ["P1-BATTLEFIELD-FALLING-ROCKS"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-FALLING-ROCKS"),
            ["P1-BATTLEFIELD-FRIENDLY-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-FALLING-ROCKS")
        } };
    }

    private static MatchState BuildBattlefieldStaticEchoCostReductionScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303065,
            165,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001", "P1-MAIN-002"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-CENTER-STAGE"],
                    battlefields: ["P1-BATTLEFIELD-MARAI-SPIRE"],
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
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-MARAI-SPIRE"] = new(
                    "P1-BATTLEFIELD-MARAI-SPIRE",
                    cardNo: "SFD·211/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldHeldNextSpellEchoScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303066,
            1,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-PILTOVER-ATTACKER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    battlefields: ["P2-BATTLEFIELD-PILTOVER-ACADEMY", "P2-BATTLEFIELD-PILTOVER-DEFENDER"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-PILTOVER-ATTACKER"] = new(
                    "P1-BATTLEFIELD-PILTOVER-ATTACKER",
                    cardNo: "SFD·125/221",
                    power: 1,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-BATTLEFIELD-PILTOVER-ACADEMY"] = new(
                    "P2-BATTLEFIELD-PILTOVER-ACADEMY",
                    cardNo: "UNL-216/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-BATTLEFIELD-PILTOVER-DEFENDER"] = new(
                    "P2-BATTLEFIELD-PILTOVER-DEFENDER",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildBattlefieldHeldNextSpellEchoPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303067,
            67,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = new(4, 0)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-DRAW-001", "P2-DRAW-002"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    hand: ["P2-SPELL-CENTER-STAGE"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-SPELL-CENTER-STAGE"] = new(
                    "P2-SPELL-CENTER-STAGE",
                    cardNo: "UNL-061/219",
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });

        return state with
        {
            ActivePlayerId = seed.P2,
            TurnPlayerId = seed.P2,
            PriorityPlayerId = seed.P2,
            UntilEndOfTurnEffects = [$"BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:{seed.P2}"]
        };
    }

    private static MatchState BuildBattlefieldStaticEquipmentCostReductionScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303066,
            166,
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
                    hand: ["P1-EQUIPMENT-LONG-SWORD"],
                    baseZone: ["P1-UNIT-EQUIPMENT-COST-TARGET"],
                    battlefields: ["P1-BATTLEFIELD-ORNN-FORGE"],
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
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-EQUIPMENT-COST-TARGET"] = new(
                    "P1-UNIT-EQUIPMENT-COST-TARGET",
                    power: 3,
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-ORNN-FORGE"] = new(
                    "P1-BATTLEFIELD-ORNN-FORGE",
                    cardNo: "SFD·213/221",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildBattlefieldEagerApprenticeSpellCostReductionScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303069,
            169,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-DRAW-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-SPELL-WELL-TRAINED"],
                    battlefields: ["P1-UNIT-EAGER-APPRENTICE"],
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
                ["P1-UNIT-EAGER-APPRENTICE"] = new(
                    "P1-UNIT-EAGER-APPRENTICE",
                    cardNo: "OGN·084/298",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
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
    }
}
