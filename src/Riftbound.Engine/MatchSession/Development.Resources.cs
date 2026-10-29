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

    private static MatchState BuildDevScenarioState(MatchState current, string scenarioId)
    {
        var p1 = PlayerBySeat(current, "P1");
        var p2 = PlayerBySeat(current, "P2");
        if (p1 is null || p2 is null)
        {
            throw new MatchSessionException(ErrorCodes.PlayerNotInRoom, "开发测试场景需要两名玩家都已加入房间。");
        }

        var seed = new DevScenarioSeed(p1, p2);
        return scenarioId switch
        {
            "basic-play" => BuildBasicPlayScenario(current, seed),
            "hand-discard-observer" => BuildHandDiscardScenario(current, seed),
            "copied-death-observer" => BuildCopiedDeathScenario(current, seed),
            "copied-movement-observer" => BuildCopiedMovementScenario(current, seed),
            "selected-damage-bullet" or "selected-damage-bullet-gate"
                or "selected-damage-drop" or "selected-damage-drop-gate"
                or "selected-damage-crescent" or "selected-damage-crescent-gate"
                => BuildSelectedDamageScenario(current, seed, scenarioId["selected-damage-".Length..]),
            "battle-result-nidalee" or "battle-result-enforcer"
                or "battle-result-nidalee-burnout" or "battle-result-enforcer-burnout"
                or "battle-result-nidalee-late-defender" or "battle-result-enforcer-late-defender"
                or "battle-result-draven" or "battle-result-draven-alt"
                or "battle-result-draven-winning" or "battle-result-draven-alt-winning"
                => BuildBattleResultScenario(current, seed, scenarioId["battle-result-".Length..]),
            "rengar-permission-normal" or "rengar-permission-alt" or "rengar-permission-blocked" or "rengar-permission-alt-blocked"
                or "rengar-permission-poor" or "rengar-permission-alt-poor"
                => BuildRengarPermissionScenario(current, seed, scenarioId["rengar-permission-".Length..]),
            "draven-death-normal" or "draven-death-winning" or "draven-death-alt-normal" or "draven-death-alt-winning"
                or "draven-death-mutual-normal" or "draven-death-mutual-winning"
                or "draven-death-alt-mutual-normal" or "draven-death-alt-mutual-winning"
                or "draven-death-mixed-attacker" or "draven-death-mixed-defender"
                or "draven-death-altar" or "draven-death-altar-alt"
                => BuildDravenBattleDeathScenario(current, seed, scenarioId["draven-death-".Length..]),
            "victory-cleanup-garen-one" or "victory-cleanup-garen-four"
                or "victory-cleanup-healthy-low" or "victory-cleanup-healthy-win"
                or "victory-cleanup-wounded-low" or "victory-cleanup-wounded-win"
                => BuildVictoryCleanupScenario(current, seed, scenarioId["victory-cleanup-".Length..]),
            "burnout-action-conquest" or "burnout-action-draw-two" or "burnout-action-second-burnout"
                or "burnout-action-hwei" or "burnout-action-portal-tie"
                => BuildBurnoutActionScenario(current, seed, scenarioId["burnout-action-".Length..]),
            "final-conquest-draw" or "final-conquest-win" or "final-conquest-burnout"
                or "final-conquest-brush" or "final-conquest-combat"
                => BuildFinalConquestScenario(current, seed, scenarioId["final-conquest-".Length..]),
            "lightning-rush-normal" or "lightning-rush-flow" or "lightning-rush-flow-empty" or "lightning-rush-flow-echo"
                or "lightning-rush-observed" or "lightning-rush-flow-counter" or "lightning-rush-flow-return-counter" or "lightning-rush-flow-control"
                => BuildLightningRushScenario(current, seed, scenarioId["lightning-rush-".Length..]),
            "deck-pending-reinforcements" => BuildDeckPendingScenario(current, seed, "OGN·062/298"),
            "deck-pending-void-rush" => BuildDeckPendingScenario(current, seed, "SFD·188/221"),
            "deck-pending-blind-fury" => BuildDeckPendingScenario(current, seed, "OGN·025/298"),
            "deck-pending-bright-future" => BuildDeckPendingScenario(current, seed, "OGN·115/298"),
            "deck-pending-reinforcements-observed" => BuildDeckPendingScenario(current, seed, "OGN·062/298", true),
            "deck-pending-bright-future-observed" => BuildDeckPendingScenario(current, seed, "OGN·115/298", true),
            "wild-claw-unit" or "wild-claw-gear" or "wild-claw-empty" or "wild-claw-unpayable" or "wild-claw-echo"
                or "wild-claw-observed-first" or "wild-claw-observed-second" or "wild-claw-recall" or "wild-claw-reentry"
                or "wild-claw-mandatory" or "wild-claw-insight" => BuildWildClawScenario(current, seed, scenarioId["wild-claw-".Length..]),
            "native-play-confirmation" => BuildNativePlayConfirmationScenario(current, seed),
            "official-resource-costs" => BuildOfficialResourceCostsScenario(current, seed),
            "imposed-movement-standard-pay" => BuildImposedMovementScenario(current, seed, false),
            "imposed-movement-standard-decline" => BuildImposedMovementScenario(current, seed, false),
            "imposed-movement-effect-pay" => BuildImposedMovementScenario(current, seed, true),
            "imposed-movement-effect-decline" => BuildImposedMovementScenario(current, seed, true),
            "restricted-mana-lux-ogs" => BuildRestrictedManaScenario(current, seed, false),
            "restricted-mana-lux-sp6" => BuildRestrictedManaScenario(current, seed, true),
            "royal-attendant-legend-mode" => BuildRoyalAttendantLegendModeScenario(current, seed),
            "ornn-equipment-look" => BuildOrnnEquipmentLookScenario(current, seed),
            "movement" => BuildMovementScenario(current, seed),
            "standard-group-movement" => BuildStandardGroupMovementScenario(current, seed),
            "pooled-combat" => BuildPooledCombatScenario(current, seed),
            "spell-duel" => BuildSpellDuelScenario(current, seed),
            "spell-duel-focus" => BuildSpellDuelFocusScenario(current, seed),
            "battlefield-contest-stack" => BuildBattlefieldContestStackScenario(current, seed),
            "battlefield-contest-spell-duel-cleanup" => BuildBattlefieldContestSpellDuelCleanupScenario(current, seed),
            "battlefield-illegal-standby" => BuildBattlefieldIllegalStandbyScenario(current, seed),
            "battlefield-unattached-equipment-cleanup" => BuildBattlefieldUnattachedEquipmentCleanupScenario(current, seed),
            "typed-power-payment" => BuildTypedPowerPaymentScenario(current, seed),
            "pay-cost-window" => BuildPayCostWindowScenario(current, seed),
            "typed-power-payment-recycle" => BuildTypedPowerPaymentRecycleScenario(current, seed),
            "typed-power-payment-over-recycle" => BuildTypedPowerPaymentOverRecycleScenario(current, seed),
            "typed-power-payment-double-recycle" => BuildTypedPowerPaymentDoubleRecycleScenario(current, seed),
            "typed-power-payment-mixed-recycle" => BuildTypedPowerPaymentMixedRecycleScenario(current, seed),
            "typed-power-payment-generic-mixed-recycle" => BuildTypedPowerPaymentMixedRecycleScenario(current, seed),
            "haste-payment-recycle" => BuildHastePaymentRecycleScenario(current, seed),
            "haste-payment-colored-recycle" => BuildHastePaymentColoredRecycleScenario(current, seed),
            "spellshield-multiple-tax" => BuildSpellshieldMultipleTaxScenario(current, seed),
            "spellshield-tax-insufficient-prompt" => BuildSpellshieldTaxInsufficientPromptScenario(current, seed),
            "unknown-play-source-prompt" => BuildUnknownPlaySourcePromptScenario(current, seed),
            "unknown-play-target-prompt" => BuildUnknownPlayTargetPromptScenario(current, seed),
            "unknown-hide-card-source-prompt" => BuildUnknownHideCardSourcePromptScenario(current, seed),
            "unknown-reveal-card-source-prompt" => BuildUnknownRevealCardSourcePromptScenario(current, seed),
            "unknown-assemble-source-prompt" => BuildUnknownAssembleSourcePromptScenario(current, seed),
            "unknown-assemble-target-prompt" => BuildUnknownAssembleTargetPromptScenario(current, seed),
            "unknown-legend-action-source-prompt" => BuildUnknownLegendActionSourcePromptScenario(current, seed),
            "unknown-legend-action-target-prompt" => BuildUnknownLegendActionTargetPromptScenario(current, seed),
            "unknown-activate-ability-source-prompt" => BuildUnknownActivateAbilitySourcePromptScenario(current, seed),
            "unknown-activate-ability-target-prompt" => BuildUnknownActivateAbilityTargetPromptScenario(current, seed),
            "unknown-move-unit-source-prompt" => BuildUnknownMoveUnitSourcePromptScenario(current, seed),
            "unknown-move-unit-battlefield-prompt" => BuildUnknownMoveUnitBattlefieldPromptScenario(current, seed),
            "unknown-rune-source-prompt" => BuildUnknownRuneSourcePromptScenario(current, seed),
            "unknown-declare-battle-source-prompt" => BuildUnknownDeclareBattleSourcePromptScenario(current, seed),
            "unknown-declare-battle-battlefield-prompt" => BuildUnknownDeclareBattleBattlefieldPromptScenario(current, seed),
            "assemble-payment-recycle" => BuildAssemblePaymentRecycleScenario(current, seed),
            "assemble-experience" => BuildAssembleExperienceScenario(current, seed),
            "assemble-destroy-friendly-unit" => BuildAssembleDestroyFriendlyUnitScenario(current, seed),
            "assemble-recycle-graveyard" => BuildAssembleRecycleGraveyardScenario(current, seed),
            "assemble-dynamic-mana" => BuildAssembleDynamicManaScenario(current, seed),
            "echo-stack" => BuildEchoStackScenario(current, seed),
            "priority-reaction-counter" => BuildPriorityReactionCounterScenario(current, seed),
            "standby-reaction" => BuildStandbyReactionScenario(current, seed),
            "ambush-reaction" => BuildAmbushReactionScenario(current, seed),
            "equipment" => BuildEquipmentScenario(current, seed),
            "status-showcase" => BuildStatusShowcaseScenario(current, seed),
            "resource-experience" => BuildResourceExperienceScenario(current, seed),
            "legend-act" => BuildLegendActScenario(current, seed),
            "legend-active-actions" => BuildLegendActiveActionsScenario(current, seed),
            "lifecycle-ephemeral" => BuildLifecycleEphemeralScenario(current, seed),
            "lifecycle-ephemeral-leblanc-static" => BuildLifecycleEphemeralLeblancStaticScenario(current, seed),
            "lifecycle-last-breath" => BuildLifecycleLastBreathScenario(current, seed),
            "control" => BuildControlScenario(current, seed),
            "battle-declare" => BuildBattleDeclareScenario(current, seed),
            "battle-prompt-filter" => BuildBattlePromptFilterScenario(current, seed),
            "battle-multi-defender" => BuildBattleMultiDefenderScenario(current, seed),
            "battle-same-priority-bulwark" => BuildBattleSamePriorityBulwarkScenario(current, seed),
            "battle-multi-attacker" => BuildBattleMultiAttackerScenario(current, seed),
            "battle-multi-participant" => BuildBattleMultiParticipantScenario(current, seed),
            "battle-no-result" => BuildBattleNoResultScenario(current, seed),
            "battle-history-deck" => BuildBattleHistoryBurnoutScenario(current, seed, false, false),
            "battle-history-burnout" => BuildBattleHistoryBurnoutScenario(current, seed, false, true),
            "battle-history-attacker-deck" => BuildBattleHistoryBurnoutScenario(current, seed, true, false),
            "battle-history-attacker-burnout" => BuildBattleHistoryBurnoutScenario(current, seed, true, true),
            "battlefield-ephemeral-steadfast" => BuildBattlefieldEphemeralSteadfastScenario(current, seed),
            "battlefield-held-move-to-base" => BuildBattlefieldHeldMoveToBaseScenario(current, seed),
            "battlefield-held-minion" => BuildBattlefieldHeldMinionScenario(current, seed),
            "battlefield-held-draw" => BuildBattlefieldHeldDrawScenario(current, seed),
            "battlefield-held-boon" => BuildBattlefieldHeldBoonScenario(current, seed),
            "battlefield-held-return-hero" => BuildBattlefieldHeldReturnHeroScenario(current, seed),
            "battlefield-conquer-boon-draw" => BuildBattlefieldConquerBoonDrawScenario(current, seed),
            "battlefield-conquer-warhawk" => BuildBattlefieldConquerWarhawkScenario(current, seed),
            "battlefield-winning-score" => BuildBattlefieldWinningScoreScenario(current, seed),
            "battlefield-first-turn-rune" => BuildBattlefieldFirstTurnRuneScenario(current, seed),
            "battlefield-first-turn-score" => BuildBattlefieldFirstTurnScoreScenario(current, seed),
            "battlefield-score-delay" => BuildBattlefieldScoreDelayScenario(current, seed),
            "battlefield-battle-destroyed-recall" => BuildBattlefieldBattleDestroyedRecallScenario(current, seed),
            "battlefield-turn-start-damage" => BuildBattlefieldTurnStartDamageScenario(current, seed),
            "battlefield-turn-start-destroy-draw" => BuildBattlefieldTurnStartDestroyDrawScenario(current, seed),
            "battlefield-held-score" => BuildBattlefieldHeldScoreScenario(current, seed),
            "battlefield-held-seven-units-win" => BuildBattlefieldHeldSevenUnitsWinScenario(current, seed),
            "battlefield-conquer-reveal-recycle" => BuildBattlefieldConquerRevealRecycleScenario(current, seed),
            "battlefield-move-power" => BuildBattlefieldMovePowerScenario(current, seed),
            "battlefield-static-prevent-play-units" => BuildBattlefieldStaticPreventPlayUnitsScenario(current, seed),
            "battlefield-static-echo-cost-reduction" => BuildBattlefieldStaticEchoCostReductionScenario(current, seed),
            "battlefield-held-next-spell-echo" => BuildBattlefieldHeldNextSpellEchoScenario(current, seed),
            "battlefield-held-next-spell-echo-prompt" => BuildBattlefieldHeldNextSpellEchoPromptScenario(current, seed),
            "battlefield-static-equipment-cost-reduction" => BuildBattlefieldStaticEquipmentCostReductionScenario(current, seed),
            "battlefield-eager-apprentice-spell-cost-reduction" => BuildBattlefieldEagerApprenticeSpellCostReductionScenario(current, seed),
            "raging-drake-next-spell-cost-reduction-prompt" => BuildRagingDrakeNextSpellCostReductionPromptScenario(current, seed),
            "battlefield-legend-attach-armament" => BuildBattlefieldLegendAttachArmamentScenario(current, seed),
            "battlefield-extra-standby" => BuildBattlefieldExtraStandbyScenario(current, seed),
            "battlefield-held-activate-conquest" => BuildBattlefieldHeldActivateConquestScenario(current, seed),
            "battlefield-friendly-spell-draw" => BuildBattlefieldFriendlySpellDrawScenario(current, seed),
            "battlefield-spell-power-bonus" => BuildBattlefieldSpellPowerBonusScenario(current, seed),
            "battlefield-high-cost-spell-insight" => BuildBattlefieldHighCostSpellInsightScenario(current, seed),
            "battlefield-unit-experience-ability" => BuildBattlefieldUnitExperienceAbilityScenario(current, seed),
            "battlefield-return-call-rune" => BuildBattlefieldReturnCallRuneScenario(current, seed),
            "battlefield-play-unit-boon" => BuildBattlefieldPlayUnitBoonScenario(current, seed),
            "battlefield-first-unit-move-other" => BuildBattlefieldFirstUnitMoveOtherScenario(current, seed),
            "battlefield-target-damage-bonus" => BuildBattlefieldTargetDamageBonusScenario(current, seed),
            "battlefield-held-unit-cost-increase" => BuildBattlefieldHeldUnitCostIncreaseScenario(current, seed),
            "battlefield-static-prevent-move-base" => BuildBattlefieldStaticPreventMoveBaseScenario(current, seed),
            "battlefield-static-roam" => BuildBattlefieldStaticRoamScenario(current, seed),
            "battlefield-conquer-mill" => BuildBattlefieldConquerMillScenario(current, seed),
            "battlefield-conquer-discard-draw" => BuildBattlefieldConquerDiscardDrawScenario(current, seed),
            "battlefield-conquer-recycle-rune" => BuildBattlefieldConquerRecycleRuneScenario(current, seed),
            "battlefield-defend-reveal-spell" => BuildBattlefieldDefendRevealSpellScenario(current, seed),
            "battlefield-held-runes" => BuildBattlefieldHeldRunesScenario(current, seed),
            "battlefield-held-rune" => BuildBattlefieldHeldRuneScenario(current, seed),
            "battlefield-static-power" => BuildBattlefieldStaticPowerScenario(current, seed),
            "battlefield-defender-steadfast" => BuildBattlefieldDefenderSteadfastScenario(current, seed),
            "battlefield-defend-move-to-base" => BuildBattlefieldDefendMoveToBaseScenario(current, seed),
            "battlefield-isolated-defender" => BuildBattlefieldIsolatedDefenderScenario(current, seed),
            "battlefield-conquer-ready-legend" => BuildBattlefieldConquerReadyLegendScenario(current, seed),
            "battlefield-conquer-ready-runes-end" => BuildBattlefieldConquerReadyRunesEndScenario(current, seed),
            "battlefield-conquer-draw-other" => BuildBattlefieldConquerDrawOtherScenario(current, seed),
            "battlefield-conquer-powerful-draw" => BuildBattlefieldConquerPowerfulDrawScenario(current, seed),
            "battlefield-conquer-sand-soldier" => BuildBattlefieldConquerSandSoldierScenario(current, seed),
            "battlefield-conquer-gold" => BuildBattlefieldConquerGoldScenario(current, seed),
            "battlefield-conquer-ready-equipment" => BuildBattlefieldConquerReadyEquipmentScenario(current, seed),
            "battle-score" => BuildBattleScoreScenario(current, seed),
            "midgame-showcase" => BuildMidgameShowcaseScenario(current, seed),
            "test-decks" => BuildTestDecksScenario(current, seed),
            "specified-hand" => BuildSpecifiedHandScenario(current, seed),
            _ => throw new MatchSessionException(
                ErrorCodes.UnsupportedCommand,
                $"Unsupported dev scenario: {scenarioId}")
        };
    }

    private static MatchState BuildBattlefieldIllegalStandbyScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303071,
            71,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields: ["P1-BATTLEFIELD-ILLEGAL-STANDBY-001", "P1-STANDBY-ILLEGAL-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ILLEGAL-STANDBY-001"] = new(
                    "P1-BATTLEFIELD-ILLEGAL-STANDBY-001",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P2),
                ["P1-STANDBY-ILLEGAL-001"] = new(
                    "P1-STANDBY-ILLEGAL-001",
                    cardNo: "OGN·121/298",
                    power: 2,
                    isFaceDown: true,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });

        return state with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-ILLEGAL-STANDBY-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-ILLEGAL-STANDBY-001"),
                ["P1-STANDBY-ILLEGAL-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-ILLEGAL-STANDBY-001")
            }
        };
    }

    private static MatchState BuildBattlefieldUnattachedEquipmentCleanupScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603303072,
            72,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = RunePool.Empty,
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    battlefields:
                    [
                        "P1-BATTLEFIELD-UNATTACHED-EQUIPMENT-001",
                        "P1-EQUIPMENT-UNATTACHED-001"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNATTACHED-EQUIPMENT-001"] = new(
                    "P1-BATTLEFIELD-UNATTACHED-EQUIPMENT-001",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-EQUIPMENT-UNATTACHED-001"] = new(
                    "P1-EQUIPMENT-UNATTACHED-001",
                    cardNo: "SFD·022/221",
                    power: 1,
                    tags: [CardObjectTags.EquipmentCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });

        return state with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-UNATTACHED-EQUIPMENT-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-UNATTACHED-EQUIPMENT-001"),
                ["P1-EQUIPMENT-UNATTACHED-001"] = new(seed.P1, "BATTLEFIELD", "P1-BATTLEFIELD-UNATTACHED-EQUIPMENT-001")
            }
        };
    }

    private static MatchState BuildOfficialResourceCostsScenario(MatchState current, DevScenarioSeed seed)
    {
        var catalog = OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo);
        var cards = new Dictionary<string, CardObjectState>();
        foreach (var (id, no) in new[] { ("QA-MALZAHAR", "OGN·113/298"), ("QA-SENTINEL", "OGN·096/298"),
            ("QA-DRAW", "SFD·125/221"), ("QA-FIELD", "OGN·294/298") })
            cards[id] = OfficialCardObject(id, seed.P1, catalog[no]);
        cards["QA-GOLD"] = new("QA-GOLD", cardNo: "SFD·T03", tags: [CardObjectTags.EquipmentCard], ownerId: seed.P1, controllerId: seed.P1);
        cards["QA-SLEEPING-GOLD"] = cards["QA-GOLD"] with { ObjectId = "QA-SLEEPING-GOLD", IsExhausted = true };
        cards["QA-FIELD"] = cards["QA-FIELD"] with { ControllerId = seed.P1 };
        cards["QA-OTHER-FIELD"] = OfficialCardObject("QA-OTHER-FIELD", seed.P2, catalog["OGN·287/298"]) with { ControllerId = seed.P2 };
        cards["QA-OTHER-DRAW"] = OfficialCardObject("QA-OTHER-DRAW", seed.P2, catalog["SFD·125/221"]);
        var state = BuildScenarioState(current, seed, 2026100807, 5,
            new Dictionary<string, RunePool> { [seed.P1] = RunePool.Empty, [seed.P2] = RunePool.Empty },
            new Dictionary<string, PlayerZones> {
                [seed.P1] = Zones(mainDeck: ["QA-DRAW"], baseZone: ["QA-MALZAHAR", "QA-SENTINEL", "QA-GOLD", "QA-SLEEPING-GOLD"], battlefields: ["QA-FIELD"]),
                [seed.P2] = Zones(mainDeck: ["QA-OTHER-DRAW"], battlefields: ["QA-OTHER-FIELD"]) }, cards);
        var locations = new Dictionary<string, ObjectLocationState>();
        foreach (var (playerId, zones) in state.PlayerZones)
        {
            foreach (var id in zones.Base) locations[id] = new(playerId, "BASE");
            foreach (var id in zones.Battlefields) locations[id] = new(playerId, "BATTLEFIELD");
            foreach (var id in zones.MainDeck) locations[id] = new(playerId, "MAIN_DECK");
        }
        return state with { ObjectLocations = locations };
    }

    private static MatchState BuildBasicPlayScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302690,
            690,
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
                    hand: ["P1-UNIT-MIGHTY-FAERIE"],
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
                ["P1-UNIT-MIGHTY-FAERIE"] = new(
                    "P1-UNIT-MIGHTY-FAERIE",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 4,
                    cardNo: "SFD·125/221",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildRoyalAttendantLegendModeScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303072,
            172,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 1),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-UNIT-ROYAL-ATTENDANT"],
                    legendZone: ["P1-LEGEND-ROYAL-TARGET"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001", "P2-RUNE-002"],
                    legendZone: ["P2-LEGEND-ROYAL-TARGET"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-ROYAL-ATTENDANT"] = new(
                    "P1-UNIT-ROYAL-ATTENDANT",
                    cardNo: "SFD·039/221",
                    power: 4,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 3,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-LEGEND-ROYAL-TARGET"] = new(
                    "P1-LEGEND-ROYAL-TARGET",
                    cardNo: "FND-259/298",
                    isExhausted: true,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-LEGEND-ROYAL-TARGET"] = new(
                    "P2-LEGEND-ROYAL-TARGET",
                    cardNo: "OGN·257/298",
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildOrnnEquipmentLookScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603303073,
            173,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(5, 1),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck:
                    [
                        "P1-ORNN-EQUIPMENT-001",
                        "P1-ORNN-UNIT-001",
                        "P1-ORNN-SPELL-001",
                        "P1-ORNN-EQUIPMENT-002",
                        "P1-ORNN-DECK-TAIL-001"
                    ],
                    runeDeck: ["P1-RUNE-001", "P1-RUNE-002"],
                    hand: ["P1-UNIT-SFD-058-ORNN"],
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
                ["P1-UNIT-SFD-058-ORNN"] = new(
                    "P1-UNIT-SFD-058-ORNN",
                    cardNo: "SFD·058/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    manaCost: 5,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-ORNN-EQUIPMENT-001"] = new(
                    "P1-ORNN-EQUIPMENT-001",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-ORNN-UNIT-001"] = new(
                    "P1-ORNN-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-ORNN-SPELL-001"] = new(
                    "P1-ORNN-SPELL-001",
                    cardNo: "OGN·058/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-ORNN-EQUIPMENT-002"] = new(
                    "P1-ORNN-EQUIPMENT-002",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildTypedPowerPaymentScenario(MatchState current, DevScenarioSeed seed)
    {
        var scenario = BuildScenarioState(
            current,
            seed,
            2603302692,
            692,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(
                    1,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneTrait.Red] = 2
                    }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    hand: ["P1-SPELL-BULLET-TIME"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BULLET-TIME-UNIT-001", "N54-SELECTED-FIELD"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["N54-SELECTED-FIELD"] = new("N54-SELECTED-FIELD", cardNo: "OGN·278/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: seed.P2, controllerId: seed.P2),
                ["P2-BULLET-TIME-UNIT-001"] = new(
                    "P2-BULLET-TIME-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
        return scenario with { ObjectLocations = new Dictionary<string, ObjectLocationState> {
            ["N54-SELECTED-FIELD"] = new(seed.P2, "BATTLEFIELD", "N54-SELECTED-FIELD"),
            ["P2-BULLET-TIME-UNIT-001"] = new(seed.P2, "BATTLEFIELD", "N54-SELECTED-FIELD") } };
    }

    private static MatchState BuildPayCostWindowScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302691,
            691,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal))
            with
            {
                PendingPayment = new PendingPaymentState(
                    "PAY-3A-MANA-1",
                    "TEST_PAYMENT",
                    seed.P1,
                    manaCost: 1,
                    legalPaymentChoiceIds: ["SPEND_MANA:1"],
                    reason: "3A PAY_COST 最小支付窗口")
            };
    }

    private static MatchState BuildTypedPowerPaymentRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        var scenario = BuildScenarioState(
            current,
            seed,
            2603302693,
            693,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(
                    1,
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
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-001"],
                    hand: ["P1-SPELL-BULLET-TIME"],
                    baseZone: ["P1-RUNE-RED-PARTIAL-PAYMENT-001"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BULLET-TIME-UNIT-001", "N54-SELECTED-FIELD"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-PARTIAL-PAYMENT-001"] = new(
                    "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["N54-SELECTED-FIELD"] = new("N54-SELECTED-FIELD", cardNo: "OGN·278/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: seed.P2, controllerId: seed.P2),
                ["P2-BULLET-TIME-UNIT-001"] = new(
                    "P2-BULLET-TIME-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
        return scenario with { ObjectLocations = new Dictionary<string, ObjectLocationState> {
            ["N54-SELECTED-FIELD"] = new(seed.P2, "BATTLEFIELD", "N54-SELECTED-FIELD"),
            ["P2-BULLET-TIME-UNIT-001"] = new(seed.P2, "BATTLEFIELD", "N54-SELECTED-FIELD") } };
    }

    private static MatchState BuildTypedPowerPaymentOverRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        var scenario = BuildScenarioState(
            current,
            seed,
            2603302694,
            694,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(
                    1,
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
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-001"],
                    hand: ["P1-SPELL-BULLET-TIME"],
                    baseZone:
                    [
                        "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                        "P1-RUNE-RED-EXTRA-PAYMENT-001"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BULLET-TIME-UNIT-001", "N54-SELECTED-FIELD"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-PARTIAL-PAYMENT-001"] = new(
                    "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-EXTRA-PAYMENT-001"] = new(
                    "P1-RUNE-RED-EXTRA-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["N54-SELECTED-FIELD"] = new("N54-SELECTED-FIELD", cardNo: "OGN·278/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: seed.P2, controllerId: seed.P2),
                ["P2-BULLET-TIME-UNIT-001"] = new(
                    "P2-BULLET-TIME-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
        return scenario with { ObjectLocations = new Dictionary<string, ObjectLocationState> {
            ["N54-SELECTED-FIELD"] = new(seed.P2, "BATTLEFIELD", "N54-SELECTED-FIELD"),
            ["P2-BULLET-TIME-UNIT-001"] = new(seed.P2, "BATTLEFIELD", "N54-SELECTED-FIELD") } };
    }

    private static MatchState BuildTypedPowerPaymentDoubleRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        var scenario = BuildScenarioState(
            current,
            seed,
            2603302695,
            695,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-001"],
                    hand: ["P1-SPELL-BULLET-TIME"],
                    baseZone:
                    [
                        "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                        "P1-RUNE-RED-EXTRA-PAYMENT-001"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BULLET-TIME-UNIT-001", "N54-SELECTED-FIELD"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-PARTIAL-PAYMENT-001"] = new(
                    "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-EXTRA-PAYMENT-001"] = new(
                    "P1-RUNE-RED-EXTRA-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["N54-SELECTED-FIELD"] = new("N54-SELECTED-FIELD", cardNo: "OGN·278/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: seed.P2, controllerId: seed.P2),
                ["P2-BULLET-TIME-UNIT-001"] = new(
                    "P2-BULLET-TIME-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
        return scenario with { ObjectLocations = new Dictionary<string, ObjectLocationState> {
            ["N54-SELECTED-FIELD"] = new(seed.P2, "BATTLEFIELD", "N54-SELECTED-FIELD"),
            ["P2-BULLET-TIME-UNIT-001"] = new(seed.P2, "BATTLEFIELD", "N54-SELECTED-FIELD") } };
    }

    private static MatchState BuildTypedPowerPaymentMixedRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        var scenario = BuildScenarioState(
            current,
            seed,
            2603302696,
            696,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(
                    1,
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
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-001"],
                    hand: ["P1-SPELL-BULLET-TIME"],
                    baseZone:
                    [
                        "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                        "P1-RUNE-BLUE-EXTRA-PAYMENT-001"
                    ],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-BULLET-TIME-UNIT-001", "N54-SELECTED-FIELD"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-BULLET-TIME"] = new(
                    "P1-SPELL-BULLET-TIME",
                    cardNo: "OGN·268/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-RED-PARTIAL-PAYMENT-001"] = new(
                    "P1-RUNE-RED-PARTIAL-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:red"],
                    cardNo: "UNL-R01",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BLUE-EXTRA-PAYMENT-001"] = new(
                    "P1-RUNE-BLUE-EXTRA-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R02",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["N54-SELECTED-FIELD"] = new("N54-SELECTED-FIELD", cardNo: "OGN·278/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: seed.P2, controllerId: seed.P2),
                ["P2-BULLET-TIME-UNIT-001"] = new(
                    "P2-BULLET-TIME-UNIT-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
        return scenario with { ObjectLocations = new Dictionary<string, ObjectLocationState> {
            ["N54-SELECTED-FIELD"] = new(seed.P2, "BATTLEFIELD", "N54-SELECTED-FIELD"),
            ["P2-BULLET-TIME-UNIT-001"] = new(seed.P2, "BATTLEFIELD", "N54-SELECTED-FIELD") } };
    }

    private static MatchState BuildHastePaymentRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302697,
            697,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(5, 1),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-001"],
                    hand: ["P1-UNIT-SIVIR"],
                    baseZone: ["P1-RUNE-PURPLE-HASTE-PAYMENT-001"],
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
                ["P1-UNIT-SIVIR"] = new(
                    "P1-UNIT-SIVIR",
                    cardNo: "SFD·143/221",
                    tags: [CardObjectTags.UnitCard, CardPermissionKeywordNames.Haste],
                    manaCost: 4,
                    power: 4,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-PURPLE-HASTE-PAYMENT-001"] = new(
                    "P1-RUNE-PURPLE-HASTE-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:purple"],
                    cardNo: "UNL-R05",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R03",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildHastePaymentColoredRecycleScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603302698,
            698,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(5, 1),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: ["P1-RUNE-BOTTOM-001"],
                    hand: ["P1-UNIT-SIVIR"],
                    baseZone:
                    [
                        "P1-RUNE-BLUE-HASTE-PAYMENT-001",
                        "P1-RUNE-PURPLE-HASTE-PAYMENT-001"
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
                ["P1-UNIT-SIVIR"] = new(
                    "P1-UNIT-SIVIR",
                    cardNo: "SFD·143/221",
                    tags: [CardObjectTags.UnitCard, CardPermissionKeywordNames.Haste],
                    manaCost: 4,
                    power: 4,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BLUE-HASTE-PAYMENT-001"] = new(
                    "P1-RUNE-BLUE-HASTE-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R03",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-PURPLE-HASTE-PAYMENT-001"] = new(
                    "P1-RUNE-PURPLE-HASTE-PAYMENT-001",
                    isExhausted: true,
                    tags: [CardObjectTags.RuneCard, "COLOR:purple"],
                    cardNo: "UNL-R05",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-RUNE-BOTTOM-001"] = new(
                    "P1-RUNE-BOTTOM-001",
                    tags: [CardObjectTags.RuneCard, "COLOR:blue"],
                    cardNo: "UNL-R03",
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildSpellshieldMultipleTaxScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603304159,
            4159,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 3),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: ["P1-MAIN-001"],
                    runeDeck: ["P1-RUNE-001"],
                    hand: ["P1-SPELL-SPIRIT-FIRE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: ["P2-MAIN-001"],
                    runeDeck: ["P2-RUNE-001"],
                    battlefields:
                    [
                        "BF",
                        "P2-SPIRIT-FIRE-SPELLSHIELD-001",
                        "P2-SPIRIT-FIRE-SPELLSHIELD2-001",
                        "P2-SPIRIT-FIRE-KEEPER-001"
                    ],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["BF"] = new("BF", cardNo: "OGN·296/298", ownerId: seed.P2, controllerId: seed.P2, tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                ["P1-SPELL-SPIRIT-FIRE"] = new(
                    "P1-SPELL-SPIRIT-FIRE",
                    cardNo: "OGN·256/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 3,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-SPIRIT-FIRE-SPELLSHIELD-001"] = new(
                    "P2-SPIRIT-FIRE-SPELLSHIELD-001",
                    cardNo: "OGN·013/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-SPIRIT-FIRE-SPELLSHIELD2-001"] = new(
                    "P2-SPIRIT-FIRE-SPELLSHIELD2-001",
                    cardNo: "SFD·085/221",
                    power: 2,
                    untilEndOfTurnPowerModifier: -2,
                    tags: [CardObjectTags.UnitCard, "法盾2"],
                    ownerId: seed.P2,
                    controllerId: seed.P2),
                ["P2-SPIRIT-FIRE-KEEPER-001"] = new(
                    "P2-SPIRIT-FIRE-KEEPER-001",
                    cardNo: "SFD·125/221",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations);
        foreach (var id in state.PlayerZones[seed.P2].Battlefields) locations[id] = new(seed.P2, "BATTLEFIELD", "BF");
        return state with { ObjectLocations = locations };
    }

    private static MatchState BuildSpellshieldTaxInsufficientPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304160,
            4160,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    hand: ["P1-SPELL-INCINERATE"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-SPELLSHIELD-TAX-TARGET-001"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-INCINERATE"] = new(
                    "P1-SPELL-INCINERATE",
                    cardNo: "OGS·003/024",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 2,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-SPELLSHIELD-TAX-TARGET-001"] = new(
                    "P2-SPELLSHIELD-TAX-TARGET-001",
                    cardNo: "OGN·013/298",
                    power: 2,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildUnknownPlaySourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304161,
            4161,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(3, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    hand: ["P1-HAND-UNKNOWN-PLAY-SOURCE"],
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
                ["P1-HAND-UNKNOWN-PLAY-SOURCE"] = new(
                    "P1-HAND-UNKNOWN-PLAY-SOURCE",
                    tags: [CardObjectTags.SpellCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownPlayTargetPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304162,
            4162,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    hand: ["P1-SPELL-UNKNOWN-PLAY-TARGET"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-UNIT-UNKNOWN-PLAY-TARGET"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-SPELL-UNKNOWN-PLAY-TARGET"] = new(
                    "P1-SPELL-UNKNOWN-PLAY-TARGET",
                    cardNo: "OGN·009/298",
                    tags: [CardObjectTags.SpellCard],
                    manaCost: 1,
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-UNKNOWN-PLAY-TARGET"] = new(
                    "P2-UNIT-UNKNOWN-PLAY-TARGET",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildUnknownHideCardSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304169,
            4169,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(1, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    hand: ["P1-HAND-UNKNOWN-HIDE-SOURCE"],
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
                ["P1-HAND-UNKNOWN-HIDE-SOURCE"] = new(
                    "P1-HAND-UNKNOWN-HIDE-SOURCE",
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownRevealCardSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304170,
            4170,
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
                    baseZone: ["P1-FACEDOWN-UNKNOWN-REVEAL-SOURCE"],
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
                ["P1-FACEDOWN-UNKNOWN-REVEAL-SOURCE"] = new(
                    "P1-FACEDOWN-UNKNOWN-REVEAL-SOURCE",
                    isFaceDown: true,
                    power: 1,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Standby],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownAssembleSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304162,
            4162,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(0, 0, powerByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone: ["P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE", "P1-UNIT-ASSEMBLE-TARGET"],
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
                ["P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE"] = new(
                    "P1-EQUIPMENT-UNKNOWN-ASSEMBLE-SOURCE",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-ASSEMBLE-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownAssembleTargetPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304171,
            4171,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(0, 0, powerByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    baseZone: ["P1-EQUIPMENT-ASSEMBLE-TARGET-FILTER", "P1-UNIT-UNKNOWN-ASSEMBLE-TARGET"],
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
                ["P1-EQUIPMENT-ASSEMBLE-TARGET-FILTER"] = new(
                    "P1-EQUIPMENT-ASSEMBLE-TARGET-FILTER",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装", "灵便"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-UNKNOWN-ASSEMBLE-TARGET"] = new(
                    "P1-UNIT-UNKNOWN-ASSEMBLE-TARGET",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownLegendActionSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304164,
            4164,
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
                    baseZone: ["P1-UNIT-LEGEND-ACTION-TARGET", "P1-EQUIPMENT-LEGEND-ACTION-ARMAMENT"],
                    battlefields: ["P1-BATTLEFIELD-PORO-FORGE"],
                    legendZone: ["P1-LEGEND-UNKNOWN-ACTION-SOURCE"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-UNKNOWN-ACTION-SOURCE"] = new(
                    "P1-LEGEND-UNKNOWN-ACTION-SOURCE",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-PORO-FORGE"] = new(
                    "P1-BATTLEFIELD-PORO-FORGE",
                    cardNo: BattlefieldLegendAttachArmamentSeedCardNo,
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-LEGEND-ACTION-TARGET"] = new(
                    "P1-UNIT-LEGEND-ACTION-TARGET",
                    cardNo: "SFD·125/221",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-EQUIPMENT-LEGEND-ACTION-ARMAMENT"] = new(
                    "P1-EQUIPMENT-LEGEND-ACTION-ARMAMENT",
                    cardNo: "SFD·022/221",
                    tags: [CardObjectTags.EquipmentCard, "武装"],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownLegendActionTargetPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304172,
            4172,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(2, 0),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET"],
                    legendZone: ["P1-LEGEND-YASUO-TARGET-FILTER"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-LEGEND-YASUO-TARGET-FILTER"] = new(
                    "P1-LEGEND-YASUO-TARGET-FILTER",
                    cardNo: "FND-259/298",
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET"] = new(
                    "P1-UNIT-UNKNOWN-LEGEND-ACTION-TARGET",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownActivateAbilitySourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
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
                    battlefields: ["P1-BATTLEFIELD-MUTATION-GARDEN", "P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE"],
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
                ["P1-BATTLEFIELD-MUTATION-GARDEN"] = new(
                    "P1-BATTLEFIELD-MUTATION-GARDEN",
                    cardNo: "UNL-213/219",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE"] = new(
                    "P1-BATTLEFIELD-UNKNOWN-ABILITY-SOURCE",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownActivateAbilityTargetPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304175,
            4175,
            new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                [seed.P1] = new(0, 1),
                [seed.P2] = RunePool.Empty
            },
            new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                [seed.P1] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P1-UNIT-XERATH-TARGET-FILTER"],
                    legendZone: ["P1-LEGEND-001"],
                    championZone: ["P1-CHAMPION-001"]),
                [seed.P2] = Zones(
                    mainDeck: [],
                    runeDeck: [],
                    battlefields: ["P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET"],
                    legendZone: ["P2-LEGEND-001"],
                    championZone: ["P2-CHAMPION-001"])
            },
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-XERATH-TARGET-FILTER"] = new(
                    "P1-UNIT-XERATH-TARGET-FILTER",
                    cardNo: "UNL-026/219",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET"] = new(
                    "P2-UNIT-UNKNOWN-ACTIVATE-ABILITY-TARGET",
                    power: 5,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P2,
                    controllerId: seed.P2)
            });
    }

    private static MatchState BuildUnknownMoveUnitSourcePromptScenario(MatchState current, DevScenarioSeed seed)
    {
        return BuildScenarioState(
            current,
            seed,
            2603304166,
            4166,
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
                    baseZone: ["P1-UNIT-UNKNOWN-MOVE-SOURCE"],
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
                ["P1-UNIT-UNKNOWN-MOVE-SOURCE"] = new(
                    "P1-UNIT-UNKNOWN-MOVE-SOURCE",
                    power: 2,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });
    }

    private static MatchState BuildUnknownMoveUnitBattlefieldPromptScenario(MatchState current, DevScenarioSeed seed)
    {
        var state = BuildScenarioState(
            current,
            seed,
            2603304173,
            4173,
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
                        "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN",
                        "P1-UNIT-ROAM-MOVE-DESTINATION-FILTER",
                        "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"
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
                ["P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"] = new(
                    "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN",
                    cardNo: "OGN·275/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-UNIT-ROAM-MOVE-DESTINATION-FILTER"] = new(
                    "P1-UNIT-ROAM-MOVE-DESTINATION-FILTER",
                    cardNo: "SFD·096/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard, "游走"],
                    ownerId: seed.P1,
                    controllerId: seed.P1),
                ["P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"] = new(
                    "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    ownerId: seed.P1,
                    controllerId: seed.P1)
            });

        return state with
        {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations, StringComparer.Ordinal)
            {
                ["P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"] = new(
                    seed.P1,
                    "BATTLEFIELD",
                    "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"),
                ["P1-UNIT-ROAM-MOVE-DESTINATION-FILTER"] = new(
                    seed.P1,
                    "BATTLEFIELD",
                    "P1-BATTLEFIELD-KNOWN-MOVE-ORIGIN"),
                ["P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION"] = new(
                    seed.P1,
                    "BATTLEFIELD",
                    "P1-BATTLEFIELD-UNKNOWN-MOVE-DESTINATION")
            }
        };
    }
}
