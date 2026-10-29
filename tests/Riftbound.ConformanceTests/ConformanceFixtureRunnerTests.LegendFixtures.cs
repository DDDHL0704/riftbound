using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    private static MatchState LegendActState(int experience)
    {
        return new MatchState(
            "p7-9-legend-act-room",
            0,
            905,
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
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-LEGEND-DRAW-001"],
                    LegendZone = ["P1-LEGEND-POPPY"]
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
                ["P1-LEGEND-POPPY"] = new(
                    "P1-LEGEND-POPPY",
                    cardNo: "UNL-237/219",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-DRAW-001"] = new(
                    "P1-LEGEND-DRAW-001",
                    cardNo: "SFD·125/221",
                    ownerId: "P1",
                    controllerId: "P1",
                    power: 3,
                    tags: [CardObjectTags.UnitCard])
            })
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = experience,
                ["P2"] = 0
            }
        };
    }

    private static MatchState LegendActiveAbilityState(string sourceCardNo, string sourceObjectId, int mana)
    {
        return new MatchState(
            "p7-9-legend-active-room",
            0,
            906,
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
                    Base = ["P1-LEGEND-BASE-UNIT"],
                    Battlefields = ["P1-LEGEND-BATTLEFIELD-UNIT"],
                    LegendZone = [sourceObjectId]
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
                [sourceObjectId] = new(
                    sourceObjectId,
                    cardNo: sourceCardNo,
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-BASE-UNIT"] = new(
                    "P1-LEGEND-BASE-UNIT",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-BATTLEFIELD-UNIT"] = new(
                    "P1-LEGEND-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            });
    }

    private static MatchState LegendActiveAbilityWithEphemeralSupportState(string sourceCardNo, string sourceObjectId, int mana)
    {
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana);
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Base = playerZones["P1"].Base.Concat(["P1-LEGEND-EPHEMERAL-UNIT"]).ToArray()
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-LEGEND-EPHEMERAL-UNIT"] = new(
            "P1-LEGEND-EPHEMERAL-UNIT",
            power: 1,
            tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral],
            ownerId: "P1",
            controllerId: "P1");
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static MatchState LegendSpellDuelFocusState(string sourceCardNo, string sourceObjectId)
    {
        return LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana: 0) with
        {
            TimingState = TimingStates.SpellDuelOpen,
            FocusPlayerId = "P1",
            PassedFocusPlayerIds = [],
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            }
        };
    }

    private static MatchState LegendPriorityWindowState(string sourceCardNo, string sourceObjectId, string pendingSourceTag)
    {
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana: 0);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P2-PENDING-SOURCE"] = new(
            "P2-PENDING-SOURCE",
            cardNo: string.Equals(pendingSourceTag, CardObjectTags.EquipmentCard, StringComparison.Ordinal)
                ? "SFD·123/221"
                : "UNL-159/219",
            tags: [pendingSourceTag],
            ownerId: "P2",
            controllerId: "P2");
        return state with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            PassedPriorityPlayerIds = [],
            StackItems =
            [
                new StackItemState(
                    "STACK-PENDING-LEGEND-RESOURCE",
                    "P2",
                    "P2-PENDING-SOURCE",
                    "PENDING_LEGEND_RESOURCE_TEST",
                    cardObjects["P2-PENDING-SOURCE"].CardNo,
                    [],
                    0)
            ],
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            CardObjects = cardObjects
        };
    }

    private static MatchState RengarLegendUnitPlayState(string sourceCardNo, string sourceObjectId)
    {
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana: 2);
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Hand = ["P1-UNIT-PLUCKY-PORO"]
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-UNIT-PLUCKY-PORO"] = new(
            "P1-UNIT-PLUCKY-PORO",
            cardNo: "UNL-220/219",
            tags: [CardObjectTags.UnitCard, "魄罗", "法盾"],
            ownerId: "P1",
            controllerId: "P1");
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static MatchState LeonaLegendStunState(string sourceCardNo, string sourceObjectId)
    {
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana: 3);
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Hand = ["P1-UNIT-SUN-SHIELDGUARD"]
        };
        playerZones["P2"] = playerZones["P2"] with
        {
            Base = ["P2-LEONA-STUN-TARGET"]
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-UNIT-SUN-SHIELDGUARD"] = new(
            "P1-UNIT-SUN-SHIELDGUARD",
            cardNo: "OGN·051/298",
            tags: [CardObjectTags.UnitCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P2-LEONA-STUN-TARGET"] = new(
            "P2-LEONA-STUN-TARGET",
            cardNo: "SFD·125/221",
            power: 2,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P2",
            controllerId: "P2");
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    internal static MatchState SivirRuneRecycleState(string sourceCardNo, string sourceObjectId)
    {
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana: 2);
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Hand = ["P1-SCRYING-SHELL"],
            MainDeck = ["P1-SIVIR-RUNE-RECYCLE"]
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-SCRYING-SHELL"] = new(
            "P1-SCRYING-SHELL",
            cardNo: "UNL-161/219",
            tags: [CardObjectTags.EquipmentCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P1-SIVIR-RUNE-RECYCLE"] = new(
            "P1-SIVIR-RUNE-RECYCLE",
            cardNo: "UNL-R01",
            tags: [CardObjectTags.RuneCard],
            ownerId: "P1",
            controllerId: "P1");
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static MatchState SivirEnemyDestroyState(string sourceCardNo, string sourceObjectId)
    {
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana: 2);
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Hand = ["P1-SPELL-HUNT-THE-WEAK"]
        };
        playerZones["P2"] = playerZones["P2"] with
        {
            Battlefields = ["P2-SIVIR-DESTROY-TARGET"]
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[sourceObjectId] = cardObjects[sourceObjectId] with
        {
            IsExhausted = true
        };
        cardObjects["P1-SPELL-HUNT-THE-WEAK"] = new(
            "P1-SPELL-HUNT-THE-WEAK",
            cardNo: "UNL-159/219",
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P2-SIVIR-DESTROY-TARGET"] = new(
            "P2-SIVIR-DESTROY-TARGET",
            cardNo: "SFD·125/221",
            power: 3,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P2",
            controllerId: "P2");
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static MatchState SivirOpponentSelfDestroyState(string sourceCardNo, string sourceObjectId)
    {
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana: 0);
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P2"] = PlayerZones.Empty with
        {
            Hand = ["P2-SPELL-HUNT-THE-WEAK"],
            Battlefields = ["P2-SIVIR-SELF-DESTROY-TARGET"]
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[sourceObjectId] = cardObjects[sourceObjectId] with
        {
            IsExhausted = true
        };
        cardObjects["P2-SPELL-HUNT-THE-WEAK"] = new(
            "P2-SPELL-HUNT-THE-WEAK",
            cardNo: "UNL-159/219",
            tags: [CardObjectTags.SpellCard],
            ownerId: "P2",
            controllerId: "P2");
        cardObjects["P2-SIVIR-SELF-DESTROY-TARGET"] = new(
            "P2-SIVIR-SELF-DESTROY-TARGET",
            cardNo: "SFD·125/221",
            power: 3,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P2",
            controllerId: "P2");
        return state with
        {
            ActivePlayerId = "P2",
            TurnPlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static MatchState JhinHighCostSpellCompletionState(string sourceCardNo, string sourceObjectId)
    {
        const string highCostSpellBanishedMarker = "LEGEND_HIGH_COST_SPELL_BANISHED";
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana: 9);
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Hand = ["P1-JHIN-RUINATION"],
            Banished =
            [
                "P1-JHIN-BANISHED-001",
                "P1-JHIN-BANISHED-002",
                "P1-JHIN-BANISHED-003"
            ],
            RuneDeck =
            [
                "P1-JHIN-RUNE-001",
                "P1-JHIN-RUNE-002",
                "P1-JHIN-RUNE-003",
                "P1-JHIN-RUNE-004"
            ],
            MainDeck = ["P1-JHIN-DRAW-001"]
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-JHIN-RUINATION"] = new(
            "P1-JHIN-RUINATION",
            cardNo: "UNL-180/219",
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1",
            controllerId: "P1");
        foreach (var objectId in playerZones["P1"].Banished)
        {
            cardObjects[objectId] = new(
                objectId,
                cardNo: "UNL-017/219",
                tags: [CardObjectTags.SpellCard, highCostSpellBanishedMarker],
                ownerId: "P1",
                controllerId: "P1");
        }

        foreach (var objectId in playerZones["P1"].RuneDeck)
        {
            cardObjects[objectId] = new(
                objectId,
                tags: [CardObjectTags.RuneCard],
                ownerId: "P1",
                controllerId: "P1");
        }

        cardObjects["P1-JHIN-DRAW-001"] = new(
            "P1-JHIN-DRAW-001",
            cardNo: "UNL-001/219",
            ownerId: "P1",
            controllerId: "P1");
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static MatchState EzrealEnemyTargetSpellState(
        string sourceCardNo,
        string sourceObjectId,
        int priorTargetCount)
    {
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana: 2);
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Hand = ["P1-EZREAL-HUNT-002"],
            MainDeck = ["P1-EZREAL-DRAW-001"]
        };
        playerZones["P2"] = playerZones["P2"] with
        {
            Battlefields = ["P2-EZREAL-TARGET-001", "P2-EZREAL-TARGET-002"]
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-EZREAL-HUNT-002"] = new(
            "P1-EZREAL-HUNT-002",
            cardNo: "UNL-159/219",
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P1-EZREAL-DRAW-001"] = new(
            "P1-EZREAL-DRAW-001",
            cardNo: "UNL-001/219",
            ownerId: "P1",
            controllerId: "P1");
        cardObjects["P2-EZREAL-TARGET-001"] = new(
            "P2-EZREAL-TARGET-001",
            cardNo: "SFD·125/221",
            power: 3,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P2",
            controllerId: "P2");
        cardObjects["P2-EZREAL-TARGET-002"] = new(
            "P2-EZREAL-TARGET-002",
            cardNo: "SFD·125/221",
            power: 3,
            tags: [CardObjectTags.UnitCard],
            ownerId: "P2",
            controllerId: "P2");
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            UntilEndOfTurnEffects = priorTargetCount <= 0
                ? []
                : [$"EZREAL_ENEMY_TARGETS_THIS_TURN:P1:{Math.Min(priorTargetCount, 2)}"]
        };
    }

    private static MatchState EzrealPriorityState(
        string sourceCardNo,
        string sourceObjectId,
        int targetCount)
    {
        var state = EzrealEnemyTargetSpellState(sourceCardNo, sourceObjectId, priorTargetCount: targetCount);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-EZREAL-PENDING-SPELL"] = new(
            "P1-EZREAL-PENDING-SPELL",
            cardNo: "UNL-159/219",
            tags: [CardObjectTags.SpellCard],
            ownerId: "P1",
            controllerId: "P1");
        return state with
        {
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            PassedPriorityPlayerIds = [],
            StackItems =
            [
                new StackItemState(
                    "STACK-PENDING-EZREAL-REACTION",
                    "P1",
                    "P1-EZREAL-PENDING-SPELL",
                    "EZREAL_PENDING_TARGET_TEST",
                    "UNL-159/219",
                    ["P2-EZREAL-TARGET-001"],
                    0)
            ],
            CardObjects = cardObjects
        };
    }

    private static MatchState JaxLegendActiveAbilityState(
        string sourceCardNo,
        string sourceObjectId,
        int mana,
        string? armamentAttachedToObjectId)
    {
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana);
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Base = playerZones["P1"].Base.Concat(["P1-JAX-ARMAMENT"]).ToArray()
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-JAX-ARMAMENT"] = new(
            "P1-JAX-ARMAMENT",
            cardNo: "SFD·022/221",
            tags: [CardObjectTags.EquipmentCard, "武装"],
            attachedToObjectId: armamentAttachedToObjectId,
            ownerId: "P1",
            controllerId: "P1");
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static MatchState BattlefieldLegendAttachArmamentState(bool includeForge, string? armamentAttachedToObjectId)
    {
        var state = LegendActiveAbilityState("UNL-237/219", "P1-LEGEND-PORO-FORGE", mana: 0);
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            Base = playerZones["P1"].Base.Concat(["P1-PORO-FORGE-ARMAMENT"]).ToArray(),
            Battlefields = includeForge
                ? playerZones["P1"].Battlefields.Concat(["P1-BATTLEFIELD-PORO-FORGE"]).ToArray()
                : playerZones["P1"].Battlefields
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-PORO-FORGE-ARMAMENT"] = new(
            "P1-PORO-FORGE-ARMAMENT",
            cardNo: "SFD·022/221",
            tags: [CardObjectTags.EquipmentCard, "武装"],
            attachedToObjectId: armamentAttachedToObjectId,
            ownerId: "P1",
            controllerId: "P1");
        if (includeForge)
        {
            cardObjects["P1-BATTLEFIELD-PORO-FORGE"] = new(
                "P1-BATTLEFIELD-PORO-FORGE",
                cardNo: "SFD·208/221",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P1",
                controllerId: "P1");
        }

        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static MatchState TeemoLegendRecallState(string sourceCardNo, string sourceObjectId, int mana)
    {
        var state = LegendActiveAbilityState(sourceCardNo, sourceObjectId, mana);
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones["P1"] = playerZones["P1"] with
        {
            ChampionZone = ["P1-TEEMO-UNIT"]
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects["P1-TEEMO-UNIT"] = new(
            "P1-TEEMO-UNIT",
            cardNo: "OGN·197/298",
            power: 1,
            tags: [CardObjectTags.UnitCard, "待命", "约德尔人"],
            ownerId: "P1",
            controllerId: "P1");
        return state with
        {
            PlayerZones = playerZones,
            CardObjects = cardObjects
        };
    }

    private static MatchState TeemoLegendStandbyHideState(bool hasTeemoLegend)
    {
        return new MatchState(
            "p7-9-teemo-standby-room",
            0,
            907,
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
                ["P1"] = new(1, 0),
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-STANDBY-TEEMO"],
                    LegendZone = hasTeemoLegend ? ["P1-LEGEND-TEEMO"] : []
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
                ["P1-STANDBY-TEEMO"] = new(
                    "P1-STANDBY-TEEMO",
                    cardNo: "OGN·121/298",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-LEGEND-TEEMO"] = new(
                    "P1-LEGEND-TEEMO",
                    cardNo: "OGN·263/298",
                    ownerId: "P1",
                    controllerId: "P1")
            });
    }

    private static MatchState JinxLegendTurnStartState()
    {
        return new MatchState(
            "p7-9-jinx-turn-start-room",
            0,
            907,
            "P2",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            MatchStatuses.InProgress,
            ["P1", "P2"],
            "P2",
            MatchPhases.Main,
            TimingStates.NeutralOpen,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    MainDeck = ["P1-JINX-DRAW-001", "P1-JINX-DRAW-002"],
                    LegendZone = ["P1-LEGEND-JINX"]
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
                ["P1-LEGEND-JINX"] = new(
                    "P1-LEGEND-JINX",
                    cardNo: "FND-251/298",
                    tags: ["CARD_TYPE:LEGEND"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-JINX-DRAW-001"] = new(
                    "P1-JINX-DRAW-001",
                    ownerId: "P1",
                    controllerId: "P1",
                    power: 3,
                    tags: [CardObjectTags.UnitCard]),
                ["P1-JINX-DRAW-002"] = new(
                    "P1-JINX-DRAW-002",
                    cardNo: "SFD·126/221",
                    ownerId: "P1",
                    controllerId: "P1",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-JINX"] = new("P1", "LEGEND"),
                ["P1-JINX-DRAW-001"] = new("P1", "MAIN_DECK"),
                ["P1-JINX-DRAW-002"] = new("P1", "MAIN_DECK")
            });
    }
}
