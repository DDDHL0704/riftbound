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

    private static MatchState BuildBattleScoreScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302501,
            75,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-BATTLE-COMMAND-DRAW-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    baseZone: ["P1-BATTLE-COMMAND-BASE-001"],
                    battlefields: ["P1-BATTLE-COMMAND-FIELD-KEEPER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    baseZone: ["P2-BATTLE-COMMAND-BASE-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLE-COMMAND-BASE-001"] = new(power: 4, damage: 1, tags: ["CARD_TYPE:UNIT"]),
                ["P1-BATTLE-COMMAND-FIELD-KEEPER"] = new(power: 2, tags: ["CARD_TYPE:UNIT"]),
                ["P2-BATTLE-COMMAND-BASE-001"] = new(power: 3, isExhausted: true, tags: ["CARD_TYPE:UNIT"])
            });
    }

    private static MatchState BuildMidgameShowcaseScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildTestDecksScenario(current, seed);
        var p1Zones = state.PlayerZones[seed.P1];
        var p2Zones = state.PlayerZones[seed.P2];
        var playerZones = new Dictionary<string, PlayerZones>(state.PlayerZones, StringComparer.Ordinal)
        {
            [seed.P1] = p1Zones with
            {
                Base = [..p1Zones.Base, "P1-SHOWCASE-RUNE-READY"],
                Battlefields = ["P1-SHOWCASE-ARENA", ..p1Zones.Battlefields, "P1-SHOWCASE-STANDBY"],
                Graveyard = ["P1-SHOWCASE-GRAVE-UNIT", "P1-SHOWCASE-GRAVE-SPELL"],
                Banished = ["P1-SHOWCASE-BANISHED-EQUIPMENT"]
            },
            [seed.P2] = p2Zones with
            {
                Base = [..p2Zones.Base, "P2-SHOWCASE-RUNE-READY"],
                Battlefields = ["P2-SHOWCASE-ARENA", ..p2Zones.Battlefields, "P2-SHOWCASE-STANDBY"],
                Graveyard = ["P2-SHOWCASE-GRAVE-UNIT", "P2-SHOWCASE-GRAVE-SPELL"],
                Banished = ["P2-SHOWCASE-BANISHED-EQUIPMENT"]
            }
        };

        var cardObjects = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal)
        {
            ["P1-SHOWCASE-ARENA"] = new(
                "P1-SHOWCASE-ARENA",
                cardNo: "OGN·275/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: seed.P1,
                controllerId: seed.P1),
            ["P2-SHOWCASE-ARENA"] = new(
                "P2-SHOWCASE-ARENA",
                cardNo: "OGN·276/298",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: seed.P2,
                controllerId: seed.P2),
            ["P1-SHOWCASE-STANDBY"] = new(
                "P1-SHOWCASE-STANDBY",
                cardNo: "OGN·197/298",
                power: 2,
                isFaceDown: true,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                ownerId: seed.P1,
                controllerId: seed.P1),
            ["P2-SHOWCASE-STANDBY"] = new(
                "P2-SHOWCASE-STANDBY",
                cardNo: "OGN·197/298",
                power: 2,
                isFaceDown: true,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                ownerId: seed.P2,
                controllerId: seed.P2),
            ["P1-SHOWCASE-RUNE-READY"] = new(
                "P1-SHOWCASE-RUNE-READY",
                cardNo: "OGN·007/298",
                tags: [CardObjectTags.RuneCard, "COLOR:red"],
                ownerId: seed.P1,
                controllerId: seed.P1),
            ["P2-SHOWCASE-RUNE-READY"] = new(
                "P2-SHOWCASE-RUNE-READY",
                cardNo: "OGN·007/298",
                tags: [CardObjectTags.RuneCard, "COLOR:red"],
                ownerId: seed.P2,
                controllerId: seed.P2),
            ["P1-SHOWCASE-GRAVE-UNIT"] = new(
                "P1-SHOWCASE-GRAVE-UNIT",
                cardNo: "SFD·125/221",
                power: 3,
                tags: [CardObjectTags.UnitCard],
                ownerId: seed.P1,
                controllerId: seed.P1),
            ["P1-SHOWCASE-GRAVE-SPELL"] = new(
                "P1-SHOWCASE-GRAVE-SPELL",
                cardNo: "OGN·009/298",
                tags: [CardObjectTags.SpellCard],
                ownerId: seed.P1,
                controllerId: seed.P1),
            ["P1-SHOWCASE-BANISHED-EQUIPMENT"] = new(
                "P1-SHOWCASE-BANISHED-EQUIPMENT",
                cardNo: "SFD·022/221",
                tags: [CardObjectTags.EquipmentCard],
                ownerId: seed.P1,
                controllerId: seed.P1),
            ["P2-SHOWCASE-GRAVE-UNIT"] = new(
                "P2-SHOWCASE-GRAVE-UNIT",
                cardNo: "SFD·125/221",
                power: 3,
                tags: [CardObjectTags.UnitCard],
                ownerId: seed.P2,
                controllerId: seed.P2),
            ["P2-SHOWCASE-GRAVE-SPELL"] = new(
                "P2-SHOWCASE-GRAVE-SPELL",
                cardNo: "OGN·009/298",
                tags: [CardObjectTags.SpellCard],
                ownerId: seed.P2,
                controllerId: seed.P2),
            ["P2-SHOWCASE-BANISHED-EQUIPMENT"] = new(
                "P2-SHOWCASE-BANISHED-EQUIPMENT",
                cardNo: "SFD·022/221",
                tags: [CardObjectTags.EquipmentCard],
                ownerId: seed.P2,
                controllerId: seed.P2)
        };

        var objectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
        {
            ["P1-SHOWCASE-ARENA"] = new(seed.P1, "BATTLEFIELD", "P1-SHOWCASE-ARENA"),
            ["P2-SHOWCASE-ARENA"] = new(seed.P2, "BATTLEFIELD", "P2-SHOWCASE-ARENA"),
            ["P1-BATTLEFIELD-UNIT-001"] = new(seed.P1, "BATTLEFIELD", "P1-SHOWCASE-ARENA"),
            ["P1-BATTLE-ATTACKER-001"] = new(seed.P1, "BATTLEFIELD", "P2-SHOWCASE-ARENA"),
            ["P1-SHOWCASE-STANDBY"] = new(seed.P1, "BATTLEFIELD", "P1-SHOWCASE-ARENA"),
            ["P2-UNIT-001"] = new(seed.P2, "BATTLEFIELD", "P2-SHOWCASE-ARENA"),
            ["P2-HOSTILE-TAKEOVER-TARGET"] = new(seed.P2, "BATTLEFIELD", "P1-SHOWCASE-ARENA"),
            ["P2-BATTLE-DEFENDER-001"] = new(seed.P2, "BATTLEFIELD", "P2-SHOWCASE-ARENA"),
            ["P2-SHOWCASE-STANDBY"] = new(seed.P2, "BATTLEFIELD", "P2-SHOWCASE-ARENA"),
            ["P1-SHOWCASE-RUNE-READY"] = new(seed.P1, "BASE"),
            ["P2-SHOWCASE-RUNE-READY"] = new(seed.P2, "BASE"),
            ["P1-SHOWCASE-GRAVE-UNIT"] = new(seed.P1, "GRAVEYARD"),
            ["P1-SHOWCASE-GRAVE-SPELL"] = new(seed.P1, "GRAVEYARD"),
            ["P2-SHOWCASE-GRAVE-UNIT"] = new(seed.P2, "GRAVEYARD"),
            ["P2-SHOWCASE-GRAVE-SPELL"] = new(seed.P2, "GRAVEYARD"),
            ["P1-SHOWCASE-BANISHED-EQUIPMENT"] = new(seed.P1, "BANISHED"),
            ["P2-SHOWCASE-BANISHED-EQUIPMENT"] = new(seed.P2, "BANISHED")
        };

        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations,
            PlayerScores = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [seed.P1] = 3,
                [seed.P2] = 4
            },
            PlayerCardsPlayedThisTurn = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [seed.P1] = 2,
                [seed.P2] = 2
            },
            MulliganCompletedPlayerIds = [seed.P1, seed.P2]
        };
    }

    private static MatchState BuildTestDecksScenario(MatchState current, DevScenarioSeed seed)
    {
        var p1MainDeck = new[]
        {
            "P1-DECK-MIGHTY-FAERIE-001",
            "P1-DECK-MIGHTY-FAERIE-002",
            "P1-DECK-HEXTECH-RAY-001",
            "P1-DECK-LONG-SWORD-001",
            "P1-DECK-RIDE-THE-WIND-001",
            "P1-DECK-HOSTILE-TAKEOVER-001",
            "P1-DECK-DEMACIA-ENVOY-001",
            "P1-DECK-MOSS-STEPPER-001",
            "P1-DECK-MIGHTY-FAERIE-003",
            "P1-DECK-HEXTECH-RAY-002",
            "P1-DECK-LONG-SWORD-002",
            "P1-DECK-RIDE-THE-WIND-002"
        };
        var p2MainDeck = new[]
        {
            "P2-DECK-MIGHTY-FAERIE-001",
            "P2-DECK-MIGHTY-FAERIE-002",
            "P2-DECK-HEXTECH-RAY-001",
            "P2-DECK-LONG-SWORD-001",
            "P2-DECK-RIDE-THE-WIND-001",
            "P2-DECK-HOSTILE-TAKEOVER-001",
            "P2-DECK-DEMACIA-ENVOY-001",
            "P2-DECK-MOSS-STEPPER-001",
            "P2-DECK-MIGHTY-FAERIE-003",
            "P2-DECK-HEXTECH-RAY-002",
            "P2-DECK-LONG-SWORD-002",
            "P2-DECK-RIDE-THE-WIND-002"
        };
        var p1RuneDeck = Enumerable.Range(1, 8)
            .Select(index => $"P1-RUNE-DECK-{index:000}")
            .ToArray();
        var p2RuneDeck = Enumerable.Range(1, 8)
            .Select(index => $"P2-RUNE-DECK-{index:000}")
            .ToArray();

        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            ["P1-UNIT-MIGHTY-FAERIE"] = Unit("P1-UNIT-MIGHTY-FAERIE", seed.P1, "SFD·125/221", 3, manaCost: 4),
            ["P1-SPELL-HEXTECH-RAY"] = Spell("P1-SPELL-HEXTECH-RAY", seed.P1, "OGN·009/298", manaCost: 2),
            ["P1-EQUIPMENT-LONG-SWORD"] = Equipment("P1-EQUIPMENT-LONG-SWORD", seed.P1, "SFD·022/221", manaCost: 2),
            ["P1-SPELL-RIDE-THE-WIND"] = Spell("P1-SPELL-RIDE-THE-WIND", seed.P1, "OGN·173/298", manaCost: 2),
            ["P1-SPELL-HOSTILE-TAKEOVER"] = Spell("P1-SPELL-HOSTILE-TAKEOVER", seed.P1, "SFD·202/221", manaCost: 4),
            ["P2-UNIT-MIGHTY-FAERIE"] = Unit("P2-UNIT-MIGHTY-FAERIE", seed.P2, "SFD·125/221", 3, manaCost: 4),
            ["P2-SPELL-HEXTECH-RAY"] = Spell("P2-SPELL-HEXTECH-RAY", seed.P2, "OGN·009/298", manaCost: 2),
            ["P2-EQUIPMENT-LONG-SWORD"] = Equipment("P2-EQUIPMENT-LONG-SWORD", seed.P2, "SFD·022/221", manaCost: 2),
            ["P2-SPELL-RIDE-THE-WIND"] = Spell("P2-SPELL-RIDE-THE-WIND", seed.P2, "OGN·173/298", manaCost: 2),
            ["P2-SPELL-HOSTILE-TAKEOVER"] = Spell("P2-SPELL-HOSTILE-TAKEOVER", seed.P2, "SFD·202/221", manaCost: 4),
            ["P1-BASE-GUARD-001"] = Unit("P1-BASE-GUARD-001", seed.P1, "SFD·125/221", 2, damage: 1),
            ["P1-BATTLEFIELD-UNIT-001"] = Unit("P1-BATTLEFIELD-UNIT-001", seed.P1, "SFD·125/221", 3),
            ["P1-BATTLE-ATTACKER-001"] = Unit("P1-BATTLE-ATTACKER-001", seed.P1, "SFD·125/221", 4),
            ["P1-LEGEND-POPPY"] = Legend("P1-LEGEND-POPPY", seed.P1, "UNL-237/219"),
            ["P1-CHAMPION-001"] = Hero("P1-CHAMPION-001", seed.P1, "FND-259/298"),
            ["P2-BASE-GUARD-001"] = Unit("P2-BASE-GUARD-001", seed.P2, "SFD·125/221", 2),
            ["P2-UNIT-001"] = Unit("P2-UNIT-001", seed.P2, "SFD·125/221", 2),
            ["P2-HOSTILE-TAKEOVER-TARGET"] = Unit("P2-HOSTILE-TAKEOVER-TARGET", seed.P2, "SFD·125/221", 4, isExhausted: true),
            ["P2-BATTLE-DEFENDER-001"] = Unit("P2-BATTLE-DEFENDER-001", seed.P2, "SFD·125/221", 3),
            ["P2-LEGEND-YASUO"] = Legend("P2-LEGEND-YASUO", seed.P2, "FND-259/298"),
            ["P2-CHAMPION-001"] = Hero("P2-CHAMPION-001", seed.P2, "FND-259/298")
        };

        AddDeckCards(cardObjects, p1MainDeck, seed.P1);
        AddDeckCards(cardObjects, p2MainDeck, seed.P2);
        AddRuneCards(cardObjects, p1RuneDeck, seed.P1);
        AddRuneCards(cardObjects, p2RuneDeck, seed.P2);

        var state = BuildScenarioState(
            current,
            seed,
            2603307001,
            1001,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(12, 3),
                [seed.P2] = new(12, 3)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: p1MainDeck,
                    runeDeck: p1RuneDeck,
                    hand:
                    [
                        "P1-UNIT-MIGHTY-FAERIE",
                        "P1-SPELL-HEXTECH-RAY",
                        "P1-EQUIPMENT-LONG-SWORD",
                        "P1-SPELL-RIDE-THE-WIND",
                        "P1-SPELL-HOSTILE-TAKEOVER"
                    ],
                    baseZone: ["P1-BASE-GUARD-001"],
                    battlefields: ["P1-BATTLEFIELD-UNIT-001", "P1-BATTLE-ATTACKER-001"],
                    legendZone: ["P1-LEGEND-POPPY"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: p2MainDeck,
                    runeDeck: p2RuneDeck,
                    hand:
                    [
                        "P2-UNIT-MIGHTY-FAERIE",
                        "P2-SPELL-HEXTECH-RAY",
                        "P2-EQUIPMENT-LONG-SWORD",
                        "P2-SPELL-RIDE-THE-WIND",
                        "P2-SPELL-HOSTILE-TAKEOVER"
                    ],
                    baseZone: ["P2-BASE-GUARD-001"],
                    battlefields: ["P2-UNIT-001", "P2-HOSTILE-TAKEOVER-TARGET", "P2-BATTLE-DEFENDER-001"],
                    legendZone: ["P2-LEGEND-YASUO"],
                    championZone: ["P2-CHAMPION-001"])
            },
            cardObjects);

        return state with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [seed.P1] = 3,
                [seed.P2] = 3
            }
        };

        static CardObjectState Unit(
            string objectId,
            string playerId,
            string cardNo,
            int power,
            int manaCost = 0,
            int damage = 0,
            bool isExhausted = false)
        {
            return Card(objectId, playerId, cardNo, CardObjectTags.UnitCard, power, manaCost, damage, isExhausted);
        }

        static CardObjectState Spell(string objectId, string playerId, string cardNo, int manaCost)
        {
            return Card(objectId, playerId, cardNo, CardObjectTags.SpellCard, power: 0, manaCost);
        }

        static CardObjectState Equipment(string objectId, string playerId, string cardNo, int manaCost)
        {
            return Card(objectId, playerId, cardNo, CardObjectTags.EquipmentCard, power: 0, manaCost);
        }

        static CardObjectState Legend(string objectId, string playerId, string cardNo)
        {
            return Card(objectId, playerId, cardNo, "CARD_TYPE:LEGEND", power: 0, manaCost: 0);
        }

        static CardObjectState Hero(string objectId, string playerId, string cardNo)
        {
            return Card(objectId, playerId, cardNo, "CARD_TYPE:HERO", power: 0, manaCost: 0);
        }

        static CardObjectState Card(
            string objectId,
            string playerId,
            string cardNo,
            string cardType,
            int power,
            int manaCost,
            int damage = 0,
            bool isExhausted = false)
        {
            return new CardObjectState(
                objectId,
                damage: damage,
                power: power,
                isExhausted: isExhausted,
                tags: [cardType],
                manaCost: manaCost,
                cardNo: cardNo,
                ownerId: playerId,
                controllerId: playerId);
        }

        static void AddDeckCards(
            Dictionary<string, CardObjectState> cardObjects,
            IReadOnlyList<string> objectIds,
            string playerId)
        {
            var templates = new[]
            {
                ("SFD·125/221", CardObjectTags.UnitCard, 3, 4),
                ("OGN·009/298", CardObjectTags.SpellCard, 0, 2),
                ("SFD·022/221", CardObjectTags.EquipmentCard, 0, 2),
                ("OGN·173/298", CardObjectTags.SpellCard, 0, 2),
                ("SFD·202/221", CardObjectTags.SpellCard, 0, 4),
                ("SFD·125/221", CardObjectTags.UnitCard, 3, 4)
            };

            for (var index = 0; index < objectIds.Count; index++)
            {
                var template = templates[index % templates.Length];
                cardObjects[objectIds[index]] = Card(
                    objectIds[index],
                    playerId,
                    template.Item1,
                    template.Item2,
                    template.Item3,
                    template.Item4);
            }
        }

        static void AddRuneCards(
            Dictionary<string, CardObjectState> cardObjects,
            IReadOnlyList<string> objectIds,
            string playerId)
        {
            foreach (var objectId in objectIds)
            {
                cardObjects[objectId] = Card(
                    objectId,
                    playerId,
                    "OGN·007/298",
                    CardObjectTags.RuneCard,
                    power: 0,
                    manaCost: 0);
            }
        }
    }

    // Development-only acceptance position. Never used by matchmaking or deck validation.
    private static MatchState BuildNativePlayConfirmationScenario(MatchState current, DevScenarioSeed seed)
    {
        var catalog = OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo);
        var cards = new Dictionary<string, CardObjectState>();
        foreach (var (id, no) in new[] { ("QA-PLAIN", "SFD·125/221"), ("QA-TRIGGER", "OGN·087/298"),
            ("QA-SPELL", "OGN·009/298"), ("QA-HELP", "SFD·111/221"), ("QA-HARROWING", "OGN·198/298"),
            ("QA-RESCUE", "OGN·102/298"), ("QA-REPLAY", "SFD·143/221"), ("QA-DRAW", "OGN·175/298"),
            ("QA-ALLY", "OGN·175/298"), ("QA-FIELD", "OGN·294/298") })
            cards[id] = OfficialCardObject(id, seed.P1, catalog[no]);
        cards["QA-FIELD"] = cards["QA-FIELD"] with { ControllerId = seed.P1 };
        cards["QA-ENEMY-FIELD"] = OfficialCardObject("QA-ENEMY-FIELD", seed.P2, catalog["OGN·287/298"]) with { ControllerId = seed.P2 };
        cards["QA-ENEMY"] = OfficialCardObject("QA-ENEMY", seed.P2, catalog["SFD·125/221"]);
        var state = BuildScenarioState(current, seed, 2026100501, 5,
            new Dictionary<string, RunePool> { [seed.P1] = new(20, 10), [seed.P2] = new(5, 2) },
            new Dictionary<string, PlayerZones> {
                [seed.P1] = Zones(mainDeck: ["QA-DRAW"], hand: ["QA-PLAIN", "QA-TRIGGER", "QA-SPELL", "QA-HELP", "QA-HARROWING", "QA-RESCUE"],
                    graveyard: ["QA-REPLAY"], battlefields: ["QA-FIELD", "QA-ALLY"]),
                [seed.P2] = Zones(battlefields: ["QA-ENEMY-FIELD", "QA-ENEMY"]) }, cards);
        return state with { ObjectLocations = new Dictionary<string, ObjectLocationState> {
            ["QA-FIELD"] = new(seed.P1, "BATTLEFIELD", "QA-FIELD"), ["QA-ALLY"] = new(seed.P1, "BATTLEFIELD", "QA-FIELD"),
            ["QA-ENEMY-FIELD"] = new(seed.P2, "BATTLEFIELD", "QA-ENEMY-FIELD"), ["QA-ENEMY"] = new(seed.P2, "BATTLEFIELD", "QA-ENEMY-FIELD") } };
    }

    private static MatchState BuildSpecifiedHandScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302999,
            999,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(12, 2),
                [seed.P2] = new(2, 0)
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001", "P1-MAIN-002"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand:
                    [
                        "P1-UNIT-MIGHTY-FAERIE",
                        "P1-SPELL-HEXTECH-RAY",
                        "P1-EQUIPMENT-LONG-SWORD",
                        "P1-SPELL-RIDE-THE-WIND"
                    ],
                    battlefields: ["P1-BATTLEFIELD-UNIT-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    battlefields: ["P2-UNIT-001", "P2-HOSTILE-TAKEOVER-TARGET"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNIT-001"] = new(power: 3, isExhausted: true, tags: ["CARD_TYPE:UNIT"]),
                ["P2-UNIT-001"] = new(power: 2, tags: ["CARD_TYPE:UNIT"]),
                ["P2-HOSTILE-TAKEOVER-TARGET"] = new(power: 4, isExhausted: true, tags: ["CARD_TYPE:UNIT"])
            });
    }

    private static MatchState BuildScenarioState(
        MatchState current,
        DevScenarioSeed seed,
        long scenarioSeed,
        int turnNumber,
        IReadOnlyDictionary<string, RunePool> runePools,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects)
    {
        // A development scenario replaces the position. Only room identity, seats and
        // tick survive; copying the old state carries battle tasks and choices into it.
        return new MatchState(current.RoomId, current.Tick, turnNumber, seed.P1, current.Seats) with
        {
            TurnNumber = turnNumber,
            ActivePlayerId = seed.P1,
            Status = MatchStatuses.InProgress,
            ReadyPlayerIds = new[] { seed.P1, seed.P2 }.Order(StringComparer.Ordinal).ToArray(),
            TurnPlayerId = seed.P1,
            Phase = MatchPhases.Main,
            TimingState = TimingStates.NeutralOpen,
            RunePools = runePools,
            PlayerZones = playerZones,
            PlayerScores = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [seed.P1] = 0,
                [seed.P2] = 0
            },
            CardObjects = cardObjects,
            PriorityPlayerId = null,
            PassedPriorityPlayerIds = [],
            StackItems = [],
            FocusPlayerId = null,
            PassedFocusPlayerIds = [],
            WinnerPlayerId = null,
            DestroyedUnitOwnerIdsThisTurn = [],
            Seed = scenarioSeed,
            RngCursor = 0,
            UntilEndOfTurnEffects = [],
            ExtraTurnPlayerId = null,
            PendingPayment = null
        };
    }

    private static PlayerZones Zones(
        IReadOnlyList<string>? mainDeck = null,
        IReadOnlyList<string>? runeDeck = null,
        IReadOnlyList<string>? hand = null,
        IReadOnlyList<string>? baseZone = null,
        IReadOnlyList<string>? battlefields = null,
        IReadOnlyList<string>? graveyard = null,
        IReadOnlyList<string>? banished = null,
        IReadOnlyList<string>? legendZone = null,
        IReadOnlyList<string>? championZone = null)
    {
        return new PlayerZones(
            mainDeck ?? [],
            runeDeck ?? [],
            hand ?? [],
            baseZone ?? [],
            battlefields ?? [],
            graveyard ?? [],
            banished ?? [],
            legendZone ?? [],
            championZone ?? []);
    }

    private static StackItemState PendingProbeStackItem(DevScenarioSeed seed)
    {
        return new StackItemState(
            "STACK-0-P2-SPELL-PROBE",
            seed.P2,
            "P2-SPELL-PROBE",
            "PENDING_TEST_SPELL",
            "TEST-000",
            []);
    }

    private static string? PlayerBySeat(MatchState state, string seat)
    {
        return state.Seats
            .Where(entry => string.Equals(entry.Value, seat, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .FirstOrDefault();
    }

    private static string NormalizePlayerId(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new MatchSessionException(ErrorCodes.PlayerIdRequired, "玩家编号不能为空。");
        }

        return playerId.Trim();
    }
}
