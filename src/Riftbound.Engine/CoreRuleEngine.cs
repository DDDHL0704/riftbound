using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed class CoreRuleEngine : IRuleEngine
{
    private const int BaseWinningScore = 8;
    private const string AmbushPlayMode = "AMBUSH";
    private const string AmbushUnsupportedMessage = "PLAY_CARD mode AMBUSH is not implemented in P4 yet.";
    private const string GloomyApothecaryCardNo = "UNL-021/219";
    private const string StandbyHideDestination = "STANDBY";
    private const string StandbyHideOptionalCost = "STANDBY_A";
    private const string StandbyHideFreeOptionalCost = "STANDBY_FREE";
    private const string StandbyHideTeemoOptionalCost = "STANDBY_TEEMO_MANA";
    private const int StandbyHideManaCost = 1;
    private const string BasicRuneTapAbilityId = "BASIC_RUNE_EXHAUST_GAIN_1_MANA";
    private const int BasicRuneTapManaGain = 1;
    private const string BasicRuneRecycleAbilityId = "BASIC_RUNE_RECYCLE_GAIN_TRAIT_POWER";
    private const string RecycleRunePaymentOptionalCostPrefix = "RECYCLE_RUNE:";
    private const int BasicRuneRecyclePowerGain = 1;
    private const string StandbyRevealMode = "STANDBY_REVEAL";
    private const string StandbyRevealDestination = "BASE";
    private const string StandbyRevealOptionalCost = "STANDBY_REVEAL_0";
    private const string StandbyReactionMode = "STANDBY_REACTION";
    private const string StandbyReactionDestination = "STACK";
    private const string MoveUnitBaseZone = "BASE";
    private const string MoveUnitBattlefieldZone = "BATTLEFIELD";
    private const string MoveUnitRoamOptionalCost = "ROAM";
    private const string MoveUnitRoamKeyword = "游走";
    private const string AssembleEquipmentUnsupportedMessage = "ASSEMBLE_EQUIPMENT is not implemented in P4 yet.";
    private const string LongSwordCardNo = "SFD·022/221";
    private const string LongSwordAssembleOptionalCost = "ASSEMBLE_RED";
    private const int LongSwordAssemblePowerCost = 1;
    private static readonly IReadOnlyDictionary<string, int> LongSwordAssemblePowerCostByTrait =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [RuneTrait.Red] = LongSwordAssemblePowerCost
        };
    private const string WatchfulSentinelCardNo = "OGN·096/298";
    private const string WatchfulSentinelLastBreathDrawEffectKind = "WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1";
    private const string DeclareBattleBattlefieldPrefix = "BATTLEFIELD:";
    private const string DeclareBattleOptionalCost = "COMBAT_ASSIGNMENT";
    private const string GuerrillaWarfareEffectKind = "GUERRILLA_WARFARE_RETURN_STANDBY_GRAVEYARD_TO_HAND";
    private const string FreeStandbyHideEffectPrefix = "FREE_STANDBY_HIDE:";
    private const string BanishIfDestroyedThisTurnEffectId = "BANISH_IF_DESTROYED_THIS_TURN";
    private const string RecallToBaseExhaustedIfDestroyedThisTurnEffectId = "RECALL_TO_BASE_EXHAUSTED_IF_DESTROYED_THIS_TURN";
    private const string DamageReceivedDoubledThisTurnEffectId = "DAMAGE_RECEIVED_DOUBLED_THIS_TURN";
    private const string PreventNextDamageThisTurnEffectId = "PREVENT_NEXT_DAMAGE_THIS_TURN";
    private const string PreventSpellAndSkillDamageThisTurnEffectId = "PREVENT_SPELL_AND_SKILL_DAMAGE_THIS_TURN";
    private const string DestroyOnNextDamageThisTurnEffectId = "DESTROY_ON_NEXT_DAMAGE_THIS_TURN";
    private const string ReturnControlToOwnerAtTurnEndEffectPrefix = "RETURN_CONTROL_TO_OWNER_AT_TURN_END:";
    private const string ExhaustFriendlyUnitOptionalCostPrefix = "EXHAUST_FRIENDLY_UNIT:";
    private const string DestroyFriendlyUnitAdditionalCostPrefix = "DESTROY_FRIENDLY_UNIT:";
    private const string DestroyFriendlyPowerfulUnitAdditionalCostPrefix = "DESTROY_FRIENDLY_POWERFUL_UNIT:";
    private const string DestroyFriendlyTraitUnitAdditionalCostPrefix = "DESTROY_FRIENDLY_TRAIT_UNIT:";
    private const string ReturnFriendlyEquipmentAdditionalCostPrefix = "RETURN_FRIENDLY_EQUIPMENT:";
    private const string DiscardHandCardOptionalCostPrefix = "DISCARD_HAND_CARD:";
    private const string SpendPowerOptionalCostPrefix = "SPEND_POWER:";
    private const string SpendExperienceOptionalCostPrefix = "SPEND_EXPERIENCE:";
    private const string SpendManaOptionalCostPrefix = "SPEND_MANA:";
    private const string YasuoLegendCardNo = "FND-259/298";
    private const string YasuoLegendAbilityId = "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT";
    private const int YasuoLegendManaCost = 2;
    private const string YasuoLegendManaCostToken = "SPEND_MANA:2";
    private const string LeeSinLegendCardNo = "OGN·257/298";
    private const string LeeSinLegendAbilityId = "LEGEND_PAY_1_EXHAUST_GRANT_BOON";
    private const int LeeSinLegendManaCost = 1;
    private const string LeeSinLegendManaCostToken = "SPEND_MANA:1";
    private const string PoppyLegendCardNo = "UNL-237/219";
    private const string PoppyLegendAbilityId = "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW";
    private const int PoppyLegendExperienceCost = 3;
    private const string PoppyLegendExperienceCostToken = "SPEND_EXPERIENCE:3";
    private const string ViktorLegendCardNo = "FND-265/298";
    private const string ViktorLegendAbilityId = "LEGEND_PAY_1_EXHAUST_CREATE_MINION";
    private const int ViktorLegendManaCost = 1;
    private const string ViktorLegendManaCostToken = "SPEND_MANA:1";
    private const string MissFortuneLegendCardNo = "OGN·267/298";
    private const string MissFortuneLegendAbilityId = "LEGEND_EXHAUST_GRANT_ROAM";
    private const string KhazixLegendCardNo = "UNL-201/219";
    private const string KhazixLegendBoonAbilityId = "LEGEND_SPEND_1_EXPERIENCE_EXHAUST_GRANT_BOON";
    private const int KhazixLegendBoonExperienceCost = 1;
    private const string KhazixLegendBoonExperienceCostToken = "SPEND_EXPERIENCE:1";
    private const string KhazixLegendMoveAbilityId = "LEGEND_SPEND_2_EXPERIENCE_EXHAUST_MOVE_DORMANT_UNIT_TO_BASE";
    private const int KhazixLegendMoveExperienceCost = 2;
    private const string KhazixLegendMoveExperienceCostToken = "SPEND_EXPERIENCE:2";
    private const string PykeLegendCardNo = "UNL-185/219";
    private const string PykeLegendAbilityId = "LEGEND_PAY_1_EXHAUST_RECALL_BATTLEFIELD_UNIT_CREATE_COIN";
    private const int PykeLegendManaCost = 1;
    private const string PykeLegendManaCostToken = "SPEND_MANA:1";
    private const string JaxSpiritforgedLegendCardNo = "SFD·193/221";
    private const string JaxLegendAttachAbilityId = "LEGEND_PAY_1_EXHAUST_ATTACH_UNATTACHED_ARMAMENT";
    private const string JaxLegendReattachAbilityId = "LEGEND_EXHAUST_REATTACH_ATTACHED_ARMAMENT";
    private const int JaxLegendAttachManaCost = 1;
    private const string JaxLegendAttachManaCostToken = "SPEND_MANA:1";
    private const string BattlefieldGrantedLegendAttachArmamentAbilityId = "LEGEND_EXHAUST_ATTACH_CONTROLLED_ARMAMENT_FROM_BATTLEFIELD";
    private const string DariusOriginLegendCardNo = "OGN·253/298";
    private const string DariusLegendAbilityId = "LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA";
    private const int DariusLegendManaGain = 1;
    private const string DianaLegendAbilityId = "LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA";
    private const int DianaLegendManaGain = 1;
    private const string KaisaLegendAbilityId = "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL";
    private const int KaisaLegendPowerGain = 1;
    private const string OrnnLegendAbilityId = "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT";
    private const int OrnnLegendPowerGain = 1;
    private const string EzrealLegendAbilityId = "LEGEND_REACTION_EXHAUST_DRAW_AFTER_TWO_ENEMY_TARGETS";
    private const string EzrealEnemyTargetsThisTurnPrefix = "EZREAL_ENEMY_TARGETS_THIS_TURN:";
    private const int EzrealEnemyTargetThreshold = 2;
    private const string IreliaLegendCardNo = "SFD·195/221";
    private const string IreliaLegendAbilityId = "LEGEND_REACTION_PAY_1_EXHAUST_READY_TARGETED_FRIENDLY_UNIT";
    private const int IreliaLegendManaCost = 1;
    private const string IreliaLegendManaCostToken = "SPEND_MANA:1";
    private const string TeemoOriginLegendCardNo = "OGN·263/298";
    private const string TeemoLegendAbilityId = "LEGEND_PAY_1_EXHAUST_RECALL_OWNED_TEEMO_UNIT";
    private const int TeemoLegendManaCost = 1;
    private const string TeemoLegendManaCostToken = "SPEND_MANA:1";
    private const string AzirSpiritforgedLegendCardNo = "SFD·197/221";
    private const string AzirLegendAbilityId = "LEGEND_PAY_1_EXHAUST_CREATE_SAND_SOLDIER_AFTER_ARMAMENT";
    private const int AzirLegendManaCost = 1;
    private const string AzirLegendManaCostToken = "SPEND_MANA:1";
    private const string JinxLegendCardNo = "FND-251/298";
    private const string LilliaLegendCardNo = "UNL-189/219";
    private const string LilliaLegendAbilityId = "LEGEND_DYNAMIC_PAY_EXHAUST_CREATE_FAERIE";
    private const int LilliaLegendBaseManaCost = 4;
    private const string FaerieTokenCardNo = "UNL·T07";
    private const string SandSoldierTokenCardNo = "SFD·T02";
    private const string RumbleLegendCardNo = "SFD·181/221";
    private const string LucianLegendCardNo = "SFD·183/221";
    private const string MasterYiIntroLegendCardNo = "OGS·019/024";
    private const string MasterYiLevelLegendCardNo = "UNL-191/219";
    private const int MasterYiLevelPowerThreshold = 6;
    private const int MasterYiLevelReadyThreshold = 11;
    private const string AhriLegendCardNo = "OGN·255/298";
    private const string DravenLegendCardNo = "SFD·185/221";
    private const string GarenIntroLegendCardNo = "OGS·023/024";
    private const string LuxIntroLegendCardNo = "OGS·021/024";
    private const string AnnieIntroLegendCardNo = "OGS·017/024";
    private const string VolibearFoundationLegendCardNo = "FND-249/298";
    private const string FioraSpiritforgedLegendCardNo = "SFD·205/221";
    private const int PowerfulUnitPowerThreshold = 5;
    private const string RengarLegendCardNo = "UNL-183/219";
    private const string LeonaOriginLegendCardNo = "OGN·261/298";
    private const string SivirSpiritforgedLegendCardNo = "SFD·203/221";
    private const string JhinLegendCardNo = "UNL-181/219";
    private const string JhinBanishedHighCostSpellMarker = "JHIN_BANISHED_HIGH_COST_SPELL";
    private const int JhinHighCostSpellManaThreshold = 4;
    private const string ViLegendCardNo = "UNL-187/219";
    private const int ViLegendOverkillThreshold = 3;
    private const string VexLegendCardNo = "UNL-193/219";
    private const string RenataLegendCardNo = "SFD·201/221";
    private const int RenataGoldBonusWinningScoreDistance = 3;
    private const string RenataGoldBonusTag = "RENATA_GOLD_EXTRA_1_MANA";
    private const string LeblancLegendCardNo = "UNL-199/219";
    private const string ReksaiLegendCardNo = "SFD·187/221";
    private const string IvernLegendCardNo = "UNL-195/219";
    private const string BrushBattlefieldTokenCardNo = "UNL·T03";
    private const string DemaciaMinionTokenCardNo = "OGN·271/298";
    private const string WarhawkTokenCardNo = "UNL·T02";
    private const string SettLegendCardNo = "OGN·269/298";
    private const int SettLegendManaCost = 1;
    private const string BattlefieldEphemeralUnitsSteadfastCardNo = "UNL-208/219";
    private const string BattlefieldHeldMoveUnitToBaseCardNo = "UNL-207/219";
    private const string BattlefieldHoldCreateMinionCardNo = "OGN·275/298";
    private const string BattlefieldHoldDrawCardNo = "OGN·280/298";
    private const string BattlefieldHoldCallRuneCardNo = "OGN·288/298";
    private const string BattlefieldHoldGrantBoonCardNo = "OGN·283/298";
    private const string BattlefieldHeldReturnHeroCardNo = "OGN·281/298";
    private const string BattlefieldHeldPayPowerScoreCardNo = "SFD·214/221";
    private const string BattlefieldDestroyedInBattleRecallCardNo = "UNL-206/219";
    private const string BattlefieldGrantLegendAttachArmamentCardNo = "SFD·208/221";
    private const string BattlefieldExtraStandbyCardNo = "OGN·278/298";
    private const string BattlefieldExtraStandbyAltCardNo = "OGN·278a/298";
    private const string BattlefieldHeldActivateConquestEffectsCardNo = "OGN·286/298";
    private const string BattlefieldConquerConsumeBoonDrawCardNo = "OGN·282/298";
    private const string BattlefieldConquerMillTwoCardNo = "SFD·212/221";
    private const string BattlefieldHoldEachPlayerCallRuneCardNo = "SFD·219/221";
    private const string BattlefieldAllUnitsPowerPlusOneCardNo = "OGN·294/298";
    private const string BattlefieldDefenderSteadfastTwoCardNo = "OGN·279/298";
    private const string BattlefieldDefendMoveFriendlyUnitToBaseCardNo = "OGN·285/298";
    private const string BattlefieldConquerRecycleRuneCardNo = "OGN·287/298";
    private const string BattlefieldDefendRevealSpellCardNo = "SFD·215/221";
    private const string BattlefieldIsolatedDefenderSteadfastMinusTwoCardNo = "UNL-210/219";
    private const string BattlefieldConquerPayOneReadyLegendCardNo = "SFD·210/221";
    private const string BattlefieldConquerReadyTwoRunesAtEndCardNo = "OGN·289/298";
    private const string BattlefieldConquerDrawForOtherBattlefieldsCardNo = "SFD·217/221";
    private const string BattlefieldConquerPowerfulPayOneDrawCardNo = "SFD·218/221";
    private const string BattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo = "SFD·207/221";
    private const string BattlefieldConquerPayOneCreateGoldCardNo = "SFD·220/221";
    private const string BattlefieldConquerReadyEquipmentCardNo = "SFD·221/221";
    private const string BattlefieldConquerDiscardDrawCardNo = "OGN·298/298";
    private const string BattlefieldConquerOverkillCreateWarhawkCardNo = "UNL-217/219";
    private const string BattlefieldIncreaseWinningScoreCardNo = "OGN·276/298";
    private const string BattlefieldIncreaseWinningScoreAltCardNo = "OGN·276a/298";
    private const string BattlefieldFirstTurnExtraRuneCardNo = "OGN·284/298";
    private const string BattlefieldFirstTurnScoreCardNo = "OGN·290/298";
    private const string BattlefieldScoreDelayCardNo = "SFD·209/221";
    private const string BattlefieldTurnStartDamageAllUnitsCardNo = "UNL-212/219";
    private const string BattlefieldTurnStartDestroyUnitDrawCardNo = "UNL-209/219";
    private const string BattlefieldConquerRevealRecycleCardNo = "OGN·291/298";
    private const string BattlefieldMovedUnitPowerPlusOneCardNo = "OGN·277/298";
    private const string BattlefieldHeldSevenUnitsWinCardNo = "OGN·293/298";
    private const string BattlefieldHeldSevenUnitsWinAltCardNo = "OGN·293a/298";
    private const string BattlefieldPreventMoveToBaseCardNo = "OGN·295/298";
    private const string BattlefieldStaticRoamCardNo = "OGN·297/298";
    private const string BattlefieldPreventUnitPlayCardNo = "SFD·216/221";
    private const string BattlefieldEchoCostReductionCardNo = "SFD·211/221";
    private const string BattlefieldHeldNextSpellEchoCardNo = "UNL-216/219";
    private const string BattlefieldEquipmentCostReductionCardNo = "SFD·213/221";
    private const string BattlefieldFriendlySpellDrawCardNo = "OGN·292/298";
    private const string BattlefieldSpellPowerBonusCardNo = "UNL-205/219";
    private const string BattlefieldGrantUnitExperienceCardNo = "UNL-213/219";
    private const string BattlefieldHighCostSpellInsightCardNo = "UNL-211/219";
    private const string BattlefieldUnitReturnedCallRuneCardNo = "UNL-214/219";
    private const string BattlefieldPlayUnitPayOneBoonCardNo = "UNL-218/219";
    private const string BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo = "UNL-215/219";
    private const string BattlefieldTargetSpellSkillDamageBonusCardNo = "OGN·296/298";
    private const string BattlefieldHeldUnitCostIncreaseCardNo = "UNL-219/219";
    private const int BattlefieldDestroyedInBattleRecallManaCost = 3;
    private const string BattlefieldUnitGainExperienceAbilityId = "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE";
    private const int BattlefieldReadyLegendManaCost = 1;
    private const int BattlefieldPowerfulDrawManaCost = 1;
    private const int BattlefieldSandSoldierManaCost = 1;
    private const int BattlefieldGoldManaCost = 1;
    private const int BattlefieldUnitReturnedCallRuneManaCost = 1;
    private const int BattlefieldHeldScorePowerCost = 4;
    private const int BattlefieldHeldSevenUnitsWinThreshold = 7;
    private const int BattlefieldScoreDelayReleasedTurnOrdinal = 3;
    private const int JhinCompletionSpellCount = 4;
    private const string PlayedArmamentThisTurnEffectPrefix = "PLAYED_ARMAMENT_THIS_TURN:";
    private const string PlayedEquipmentThisTurnEffectPrefix = "PLAYED_EQUIPMENT_THIS_TURN:";
    private const string BattlefieldFriendlySpellDrawUsedEffectPrefix = "BATTLEFIELD_FRIENDLY_SPELL_DRAW_USED:";
    private const string BattlefieldFirstUnitPlayedMoveOtherToBaseUsedEffectPrefix = "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE_USED:";
    private const string BattlefieldConquerReadyRuneAtEndEffectPrefix = "BATTLEFIELD_CONQUER_READY_RUNE_AT_END:";
    private const string BattlefieldHeldUnitCostIncreaseEffectPrefix = "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:";
    private const string BattlefieldHeldNextSpellEchoEffectPrefix = "BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:";
    private const string UnitConquestReadySelfOnceEffectPrefix = "UNIT_CONQUEST_READY_SELF_ONCE:";
    private const string BattlefieldDestroyedInBattleRecallEffectId = "BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL";
    private const string RengarUnitPlayedTargetEffectPrefix = "RENGAR_UNIT_PLAYED_TARGET:";
    private const string LeonaStunBoonTargetEffectPrefix = "LEONA_STUN_BOON_TARGET:";

    private readonly IRuleEngine fallback = new PlaceholderRuleEngine();

    public ValueTask<ResolutionResult> ResolveAsync(
        MatchState state,
        PlayerIntent intent,
        GameCommand command,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(RejectWithCorePrompts(
                state,
                "Match is not in progress.",
                ErrorCodes.PhaseNotAllowed));
        }

        if (command is MulliganCommand mulliganCommand)
        {
            return ValueTask.FromResult(ResolveMulligan(state, intent, mulliganCommand));
        }

        if (command is SurrenderCommand)
        {
            return ValueTask.FromResult(ResolveSurrender(state, intent));
        }

        if (command is DeclareBattleCommand activeTaskDeclareBattleCommand
            && ResolutionResult.ActiveStartBattleTask(state) is not null
            && string.Equals(intent.PlayerId, state.ActivePlayerId, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(ResolveDeclareBattle(state, intent, activeTaskDeclareBattleCommand));
        }

        if (ResolutionResult.HasBlockingPendingTaskQueue(state))
        {
            return ValueTask.FromResult(RejectWithCorePrompts(
                state,
                ResolutionResult.BlockingPendingTaskQueueReason(state),
                ErrorCodes.PhaseNotAllowed));
        }

        if (command is EndTurnCommand)
        {
            return ValueTask.FromResult(ResolveEndTurn(state, intent));
        }

        if (string.Equals(state.Phase, MatchPhases.TurnStart, StringComparison.Ordinal))
        {
            if (!string.Equals(state.TurnPlayerId, intent.PlayerId, StringComparison.Ordinal))
            {
                return ValueTask.FromResult(RejectWithCorePrompts(
                    state,
                    "TURN_START can only be advanced by the turn player.",
                    ErrorCodes.PhaseNotAllowed));
            }

            return ValueTask.FromResult(ResolveTurnStart(state));
        }

        if (command is PlayCardCommand playCardCommand)
        {
            return ValueTask.FromResult(ResolvePlayCard(state, intent, playCardCommand));
        }

        if (command is ActivateAbilityCommand activateAbilityCommand)
        {
            return ValueTask.FromResult(ResolveActivateAbility(state, intent, activateAbilityCommand));
        }

        if (command is LegendActCommand legendActCommand)
        {
            return ValueTask.FromResult(ResolveLegendAct(state, intent, legendActCommand));
        }

        if (command is HideCardCommand hideCardCommand)
        {
            return ValueTask.FromResult(ResolveHideCard(state, intent, hideCardCommand));
        }

        if (command is TapRuneCommand tapRuneCommand)
        {
            return ValueTask.FromResult(ResolveTapRune(state, intent, tapRuneCommand));
        }

        if (command is RecycleRuneCommand recycleRuneCommand)
        {
            return ValueTask.FromResult(ResolveRecycleRune(state, intent, recycleRuneCommand));
        }

        if (command is RevealCardCommand revealCardCommand)
        {
            return ValueTask.FromResult(ResolveRevealCard(state, intent, revealCardCommand));
        }

        if (command is MoveUnitCommand moveUnitCommand)
        {
            return ValueTask.FromResult(ResolveMoveUnit(state, intent, moveUnitCommand));
        }

        if (command is AssembleEquipmentCommand assembleEquipmentCommand)
        {
            return ValueTask.FromResult(ResolveAssembleEquipment(state, intent, assembleEquipmentCommand));
        }

        if (command is DeclareBattleCommand declareBattleCommand)
        {
            return ValueTask.FromResult(ResolveDeclareBattle(state, intent, declareBattleCommand));
        }

        if (command is PassPriorityCommand && CanPassPriority(state, intent.PlayerId))
        {
            return ValueTask.FromResult(ResolvePassPriority(state, intent));
        }

        if (command is PassFocusCommand && CanPassFocus(state, intent.PlayerId))
        {
            return ValueTask.FromResult(ResolvePassFocus(state, intent));
        }

        if (command is PassPriorityCommand
            && string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            && string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(RejectWithCorePrompts(
                state,
                "PASS_PRIORITY is not allowed without an open priority window.",
                ErrorCodes.PhaseNotAllowed));
        }

        if (command is PassFocusCommand)
        {
            return ValueTask.FromResult(RejectWithCorePrompts(
                state,
                "PASS_FOCUS is not allowed without an open spell duel focus window.",
                ErrorCodes.PhaseNotAllowed));
        }

        return fallback.ResolveAsync(state, intent, command, cancellationToken);
    }

    private static ResolutionResult ResolvePlayCard(
        MatchState state,
        PlayerIntent intent,
        PlayCardCommand command)
    {
        if (IsAmbushPlayMode(command.Mode))
        {
            return ResolveAmbushPlayCard(state, intent, command);
        }

        if (!TryBuildPlayCardPlan(state, intent, command, out var plan, out var rejection))
        {
            return rejection;
        }

        var behavior = plan.Behavior;
        var targetObjectIds = plan.TargetObjectIds;
        var destination = string.Equals(command.Destination?.Trim(), MoveUnitBaseZone, StringComparison.Ordinal)
            ? string.Empty
            : command.Destination?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(destination)
            && (!behavior.PlaysSourceToBaseAsUnit
                || !string.Equals(destination, $"{MoveUnitBattlefieldZone}:{intent.PlayerId}-MAIN", StringComparison.Ordinal)))
        {
            return RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} has unsupported play destination.",
                ErrorCodes.InvalidTarget);
        }

        if (!string.IsNullOrWhiteSpace(destination)
            && HasBattlefieldStaticPreventUnitPlayToBattlefield(state, intent.PlayerId, destination))
        {
            return RejectWithCorePrompts(
                state,
                "PLAY_CARD blocked by battlefield static: units cannot be played to this battlefield.",
                ErrorCodes.InvalidTarget);
        }

        var paymentEvents = new List<GameEvent>();
        var playerZones = RemoveSourceCardFromHand(state, intent.PlayerId, plan.SourceZones, command.SourceObjectId);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var objectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones);
        var runePools = ApplyRecycleRunePaymentResourceActions(
            state.RunePools,
            playerZones,
            cardObjects,
            objectLocations,
            intent.PlayerId,
            plan.RecycledPaymentRuneObjectIds,
            paymentEvents);
        runePools = PayRuneCosts(
            runePools,
            intent.PlayerId,
            plan.TotalManaCost,
            plan.AnyPowerCost,
            plan.PowerCostByTrait);
        var playerExperience = PayExperienceCosts(state, intent.PlayerId, plan.TotalExperienceCost);
        objectLocations[command.SourceObjectId] = new ObjectLocationState(intent.PlayerId, "STACK");

        var stackItem = new StackItemState(
            $"STACK-{state.Tick + 1}-{command.SourceObjectId}",
            intent.PlayerId,
            command.SourceObjectId,
            behavior.EffectKind,
            command.CardNo,
            targetObjectIds,
            behavior.DamageAmountFromOptionalPowerCost ? plan.TotalPowerCost : behavior.DamageAmount,
            plan.EffectRepeatCount,
            plan.OptionalCosts,
            playedAfterAnotherCardThisTurn: ControllerPlayedAnotherCardThisTurn(state, intent.PlayerId),
            destination: destination,
            timingContext: StackTimingContextForNewStackItem(state));
        var untilEndOfTurnEffects = MarkArmamentPlayedThisTurn(
            state.UntilEndOfTurnEffects,
            intent.PlayerId,
            behavior);
        untilEndOfTurnEffects = MarkEquipmentPlayedThisTurn(
            untilEndOfTurnEffects,
            intent.PlayerId,
            behavior);
        untilEndOfTurnEffects = MarkRengarUnitPlayedTriggerTarget(
            untilEndOfTurnEffects,
            stackItem,
            plan.RengarUnitPlayedTargetObjectId);
        untilEndOfTurnEffects = MarkLeonaStunBoonTriggerTarget(
            untilEndOfTurnEffects,
            stackItem,
            plan.LeonaStunBoonTargetObjectId);
        untilEndOfTurnEffects = MarkEzrealEnemyTargetsThisTurn(
            state,
            untilEndOfTurnEffects,
            intent.PlayerId,
            command.SourceObjectId,
            targetObjectIds);
        var battlefieldFriendlySpellDrawSourceObjectId = TryGetBattlefieldFriendlySpellDrawSourceObjectId(
                state,
                intent.PlayerId,
                behavior,
                targetObjectIds,
                out var spellDrawSourceObjectId)
            ? spellDrawSourceObjectId
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(battlefieldFriendlySpellDrawSourceObjectId))
        {
            untilEndOfTurnEffects = AddUntilEndOfTurnEffect(
                untilEndOfTurnEffects,
                BuildBattlefieldFriendlySpellDrawUsedEffectId(
                    intent.PlayerId,
                    battlefieldFriendlySpellDrawSourceObjectId));
        }
        var battlefieldNextSpellEchoConsumed = BattlefieldHeldNextSpellEchoActive(state, intent.PlayerId)
            && IsSpellPlayBehavior(behavior);
        if (battlefieldNextSpellEchoConsumed)
        {
            untilEndOfTurnEffects = RemoveUntilEndOfTurnEffect(
                untilEndOfTurnEffects,
                BuildBattlefieldHeldNextSpellEchoEffectId(intent.PlayerId));
        }
        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = intent.PlayerId,
            TimingState = TimingStates.NeutralClosed,
            RunePools = runePools,
            PlayerExperience = playerExperience,
            PlayerCardsPlayedThisTurn = IncrementPlayerCardsPlayedThisTurn(state, intent.PlayerId),
            UntilEndOfTurnEffects = untilEndOfTurnEffects,
            PlayerZones = playerZones,
            ObjectLocations = objectLocations,
            CardObjects = cardObjects,
            PriorityPlayerId = intent.PlayerId,
            PassedPriorityPlayerIds = [],
            FocusPlayerId = string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                ? null
                : state.FocusPlayerId,
            PassedFocusPlayerIds = string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                ? []
                : state.PassedFocusPlayerIds,
            StackItems = state.StackItems.Concat([stackItem]).ToArray()
        };
        var destroyedAdditionalCostOwnerIds = new List<string>();

        var events = new List<GameEvent>
        {
            new GameEvent(
                "CARD_PLAYED",
                $"{intent.PlayerId} 打出{behavior.DisplayName}",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["cardNo"] = command.CardNo,
                    ["mode"] = command.Mode
                })
        };
        events.AddRange(paymentEvents);
        events.Add(
            new GameEvent(
                "COST_PAID",
                $"{intent.PlayerId} 支付 {plan.TotalManaCost} 点费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["mana"] = plan.TotalManaCost,
                    ["power"] = plan.TotalPowerCost,
                    ["powerByTrait"] = plan.PowerCostByTrait,
                    ["experience"] = plan.TotalExperienceCost,
                    ["baseMana"] = behavior.ManaCost,
                    ["costReductionMana"] = plan.CostReductionMana,
                    ["optionalCostManaReduction"] = plan.OptionalCostManaReduction,
                    ["battlefieldEchoCostReductionMana"] = plan.BattlefieldEchoCostReductionMana,
                    ["battlefieldEquipmentCostReductionMana"] = plan.BattlefieldEquipmentCostReductionMana,
                    ["battlefieldHeldUnitCostIncreaseMana"] = plan.BattlefieldHeldUnitCostIncreaseMana,
                    ["spellshieldTaxMana"] = plan.SpellshieldTaxMana,
                    ["spellshieldTaxTargetObjectIds"] = plan.SpellshieldTaxTargetObjectIds.ToArray(),
                    ["optionalCosts"] = plan.OptionalCosts.ToArray(),
                    ["paymentResourceActions"] = plan.PaymentResourceActions.ToArray(),
                    ["recycledRuneObjectIds"] = plan.RecycledPaymentRuneObjectIds.ToArray()
                }));
        if (battlefieldNextSpellEchoConsumed)
        {
            events.Add(new GameEvent(
                "BATTLEFIELD_TRIGGER_RESOLVED",
                $"{intent.PlayerId} 的下一个法术获得皮城学院回响",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["trigger"] = "BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO",
                    ["playedCardNo"] = command.CardNo,
                    ["playedCardManaCost"] = behavior.ManaCost,
                    ["optionalCosts"] = plan.OptionalCosts.ToArray(),
                    ["echoPaid"] = plan.OptionalCosts.Contains(EchoOptionalCostNames.Echo, StringComparer.Ordinal),
                    ["effectRepeatCount"] = plan.EffectRepeatCount
                }));
        }
        IReadOnlyDictionary<string, int> playerScores = state.PlayerScores;
        string? winnerPlayerId = null;
        var rngCursor = state.RngCursor;
        foreach (var optionalCostTargetObjectId in plan.ExhaustedOptionalCostTargetObjectIds)
        {
            var targetState = cardObjects.TryGetValue(optionalCostTargetObjectId, out var existingTarget)
                ? existingTarget
                : new CardObjectState(optionalCostTargetObjectId);
            targetState = ApplyExhaustState(
                targetState,
                behavior,
                stackItem,
                optionalCostTargetObjectId,
                out var exhaustedEvent);
            cardObjects[optionalCostTargetObjectId] = targetState;
            events.Add(exhaustedEvent);
        }

        foreach (var additionalCostTargetObjectId in plan.DestroyedAdditionalCostTargetObjectIds)
        {
            if (!TryDestroyTarget(playerZones, cardObjects, additionalCostTargetObjectId, out var removalResult))
            {
                continue;
            }

            events.Add(BuildFieldRemovalEvent(
                behavior.DisplayName,
                stackItem,
                additionalCostTargetObjectId,
                removalResult,
                "ADDITIONAL_COST"));
            if (removalResult.WasDestroyed)
            {
                destroyedAdditionalCostOwnerIds.Add(removalResult.OwnerPlayerId);
            }
        }

        foreach (var additionalCostTargetObjectId in plan.ReturnedAdditionalCostTargetObjectIds)
        {
            if (!TryReturnTargetToHand(playerZones, cardObjects, additionalCostTargetObjectId, out var returnedOwnerPlayerId, out var returnedWasEquipment))
            {
                continue;
            }

            events.Add(BuildReturnedToHandEvent(
                behavior.DisplayName,
                stackItem,
                additionalCostTargetObjectId,
                returnedOwnerPlayerId,
                returnedWasEquipment,
                "ADDITIONAL_COST"));
        }

        foreach (var discardedOptionalCostTargetObjectId in plan.DiscardedOptionalCostTargetObjectIds)
        {
            if (!TryDiscardCardFromHand(playerZones, cardObjects, intent.PlayerId, discardedOptionalCostTargetObjectId))
            {
                continue;
            }

            events.Add(new GameEvent(
                "CARD_DISCARDED",
                $"{behavior.DisplayName}弃置手牌作为额外费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["targetObjectId"] = discardedOptionalCostTargetObjectId,
                    ["reason"] = "OPTIONAL_COST",
                    ["destinationZone"] = "GRAVEYARD"
                }));
        }

        TryResolveBattlefieldSpellPowerBonusTrigger(
            playerZones,
            cardObjects,
            intent.PlayerId,
            behavior,
            stackItem,
            events);
        var battlefieldInsightResult = TryResolveBattlefieldHighCostSpellInsightTrigger(
            state,
            playerZones,
            cardObjects,
            intent.PlayerId,
            behavior,
            stackItem,
            plan.TotalManaCost,
            rngCursor);
        events.AddRange(battlefieldInsightResult.Events);
        rngCursor = battlefieldInsightResult.RngCursor;

        if (TryGetLuxHighCostSpellDrawCardNo(playerZones, cardObjects, intent.PlayerId, behavior, out var luxLegendCardNo))
        {
            events.Add(new GameEvent(
                "LEGEND_TRIGGER_RESOLVED",
                $"{intent.PlayerId} 的光辉女郎高费法术触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["legendCardNo"] = luxLegendCardNo,
                    ["trigger"] = "HIGH_COST_SPELL_DRAW_ONE",
                    ["playedCardNo"] = command.CardNo,
                    ["playedCardManaCost"] = behavior.ManaCost
                }));
            var luxDrawApplication = ApplyDrawToPlayer(
                state,
                playerZones,
                playerScores,
                intent.PlayerId,
                1,
                rngCursor,
                events);
            playerScores = luxDrawApplication.PlayerScores;
            winnerPlayerId = luxDrawApplication.WinnerPlayerId;
            rngCursor = luxDrawApplication.RngCursor;
        }

        if (!string.IsNullOrWhiteSpace(battlefieldFriendlySpellDrawSourceObjectId))
        {
            events.Add(new GameEvent(
                "BATTLEFIELD_TRIGGER_RESOLVED",
                $"{intent.PlayerId} 因幻梦之树抽一张牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["battlefieldObjectId"] = battlefieldFriendlySpellDrawSourceObjectId,
                    ["battlefieldCardNo"] = BattlefieldFriendlySpellDrawCardNo,
                    ["trigger"] = "BATTLEFIELD_FRIENDLY_SPELL_DRAW_ONE",
                    ["playedCardNo"] = command.CardNo,
                    ["targetObjectIds"] = targetObjectIds.ToArray()
                }));
            var battlefieldDrawApplication = ApplyDrawToPlayer(
                state,
                playerZones,
                playerScores,
                intent.PlayerId,
                1,
                rngCursor,
                events);
            playerScores = battlefieldDrawApplication.PlayerScores;
            winnerPlayerId = battlefieldDrawApplication.WinnerPlayerId ?? winnerPlayerId;
            rngCursor = battlefieldDrawApplication.RngCursor;
        }

        events.Add(
            new GameEvent(
                "STACK_ITEM_ADDED",
                $"{behavior.DisplayName}加入结算链",
                new Dictionary<string, object?>
                {
                    ["stackItemId"] = stackItem.StackItemId,
                    ["controllerId"] = stackItem.ControllerId,
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["cardNo"] = stackItem.CardNo,
                    ["targetObjectIds"] = stackItem.TargetObjectIds.ToArray(),
                    ["effectKind"] = stackItem.EffectKind,
                    ["mode"] = command.Mode,
                    ["effectRepeatCount"] = stackItem.EffectRepeatCount,
                    ["playedAfterAnotherCardThisTurn"] = stackItem.PlayedAfterAnotherCardThisTurn
                }));

        objectLocations = ReconcileObjectLocations(objectLocations, playerZones);
        nextState = nextState with
        {
            Status = winnerPlayerId is null ? state.Status : MatchStatuses.Finished,
            CardObjects = cardObjects,
            PlayerZones = playerZones,
            ObjectLocations = objectLocations,
            PlayerScores = playerScores,
            WinnerPlayerId = winnerPlayerId ?? state.WinnerPlayerId,
            RngCursor = rngCursor,
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(
                state.DestroyedUnitOwnerIdsThisTurn,
                destroyedAdditionalCostOwnerIds)
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static ResolutionResult ResolveAmbushPlayCard(
        MatchState state,
        PlayerIntent intent,
        PlayCardCommand command)
    {
        if (!TryBuildMinimalAmbushPlayCardPlan(
                state,
                intent,
                command,
                out var behavior,
                out var zones,
                out var sourceState,
                out var destination))
        {
            return RejectWithCorePrompts(
                state,
                AmbushUnsupportedMessage,
                ErrorCodes.UnsupportedCommand);
        }

        if (HasBattlefieldStaticPreventUnitPlayToBattlefield(state, intent.PlayerId, destination))
        {
            return RejectWithCorePrompts(
                state,
                "PLAY_CARD blocked by battlefield static: units cannot be played to this battlefield.",
                ErrorCodes.InvalidTarget);
        }

        var currentPool = state.RunePools.TryGetValue(intent.PlayerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < behavior.ManaCost)
        {
            return RejectWithCorePrompts(
                state,
                $"Not enough mana to play {behavior.DisplayName} with Ambush.",
                ErrorCodes.InsufficientCost);
        }

        var runePools = PayRuneCosts(state, intent.PlayerId, behavior.ManaCost, 0);
        var playerZones = RemoveSourceCardFromHand(state, intent.PlayerId, zones, command.SourceObjectId);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var objectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones);
        objectLocations[command.SourceObjectId] = new ObjectLocationState(intent.PlayerId, "STACK");
        cardObjects[command.SourceObjectId] = sourceState with
        {
            CardNo = string.IsNullOrWhiteSpace(sourceState.CardNo) ? behavior.CardNo : sourceState.CardNo,
            ManaCost = behavior.ManaCost,
            Power = behavior.SourceUnitPower > 0 ? behavior.SourceUnitPower : sourceState.Power
        };

        var stackItem = new StackItemState(
            $"STACK-{state.Tick + 1}-{command.SourceObjectId}",
            intent.PlayerId,
            command.SourceObjectId,
            behavior.EffectKind,
            command.CardNo,
            [],
            behavior.DamageAmount,
            1,
            [],
            playedAfterAnotherCardThisTurn: ControllerPlayedAnotherCardThisTurn(state, intent.PlayerId),
            destination: destination,
            timingContext: StackTimingContextForNewStackItem(state));
        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = intent.PlayerId,
            TimingState = TimingStates.NeutralClosed,
            RunePools = runePools,
            PlayerCardsPlayedThisTurn = IncrementPlayerCardsPlayedThisTurn(state, intent.PlayerId),
            PlayerZones = playerZones,
            ObjectLocations = objectLocations,
            CardObjects = cardObjects,
            PriorityPlayerId = intent.PlayerId,
            PassedPriorityPlayerIds = [],
            StackItems = state.StackItems.Concat([stackItem]).ToArray()
        };
        var events = new List<GameEvent>
        {
            new(
                "CARD_PLAYED",
                $"{intent.PlayerId} 以伏击打出{behavior.DisplayName}",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["cardNo"] = command.CardNo,
                    ["mode"] = command.Mode,
                    ["destination"] = destination
                }),
            new(
                "COST_PAID",
                $"{intent.PlayerId} 支付 {behavior.ManaCost} 点费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["mana"] = behavior.ManaCost,
                    ["power"] = 0,
                    ["experience"] = 0,
                    ["baseMana"] = behavior.ManaCost,
                    ["optionalCosts"] = Array.Empty<string>()
                }),
            new(
                "STACK_ITEM_ADDED",
                $"{behavior.DisplayName}加入结算链",
                new Dictionary<string, object?>
                {
                    ["stackItemId"] = stackItem.StackItemId,
                    ["controllerId"] = stackItem.ControllerId,
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["cardNo"] = stackItem.CardNo,
                    ["targetObjectIds"] = stackItem.TargetObjectIds.ToArray(),
                    ["effectKind"] = stackItem.EffectKind,
                    ["mode"] = command.Mode,
                    ["destination"] = destination,
                    ["effectRepeatCount"] = stackItem.EffectRepeatCount,
                    ["playedAfterAnotherCardThisTurn"] = stackItem.PlayedAfterAnotherCardThisTurn
                })
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static bool TryBuildMinimalAmbushPlayCardPlan(
        MatchState state,
        PlayerIntent intent,
        PlayCardCommand command,
        out CardBehaviorDefinition behavior,
        out PlayerZones zones,
        out CardObjectState sourceState,
        out string destination)
    {
        behavior = default!;
        zones = PlayerZones.Empty;
        sourceState = default!;
        destination = string.IsNullOrWhiteSpace(command.Destination) ? string.Empty : command.Destination.Trim();

        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralClosed, StringComparison.Ordinal)
            || state.StackItems.Count == 0
            || !string.Equals(state.PriorityPlayerId, intent.PlayerId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(
                destination,
                $"{MoveUnitBattlefieldZone}:{intent.PlayerId}-MAIN",
                StringComparison.Ordinal))
        {
            return false;
        }

        var targetObjectIds = NormalizeTargetObjectIds(command.TargetObjectIds);
        var optionalCosts = NormalizeOptionalCosts(command.OptionalCosts);
        if (targetObjectIds.Count != 0 || optionalCosts.Count != 0)
        {
            return false;
        }

        if (!string.Equals(command.CardNo, GloomyApothecaryCardNo, StringComparison.Ordinal)
            || !CardBehaviorRegistry.TryGetByCardNo(command.CardNo, out behavior)
            || !behavior.PlaysSourceToBaseAsUnit
            || !HasValidTargetCount(state, intent.PlayerId, behavior, targetObjectIds))
        {
            return false;
        }

        if (!state.PlayerZones.TryGetValue(intent.PlayerId, out var playerZones)
            || !playerZones.Hand.Contains(command.SourceObjectId, StringComparer.Ordinal)
            || !state.CardObjects.TryGetValue(command.SourceObjectId, out var knownSourceState))
        {
            return false;
        }

        zones = playerZones;
        sourceState = knownSourceState;
        if (!string.IsNullOrWhiteSpace(sourceState.CardNo)
            && !string.Equals(sourceState.CardNo, behavior.CardNo, StringComparison.Ordinal))
        {
            return false;
        }

        return sourceState.Tags.Contains(CardInteractionKeywordNames.Ambush, StringComparer.Ordinal)
            && HasFriendlyBattlefieldUnitForAmbush(state, intent.PlayerId);
    }

    private static bool HasFriendlyBattlefieldUnitForAmbush(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Battlefields.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && !cardObject.IsFaceDown
                && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal));
    }

    private static ResolutionResult ResolveAssembleEquipment(
        MatchState state,
        PlayerIntent intent,
        AssembleEquipmentCommand command)
    {
        if (!TryBuildMinimalAssembleEquipmentPlan(state, intent, command, out var equipmentState, out var targetState))
        {
            return RejectWithCorePrompts(
                state,
                AssembleEquipmentUnsupportedMessage,
                ErrorCodes.UnsupportedCommand);
        }

        var optionalCosts = NormalizeOptionalCosts(command.OptionalCosts);
        if (!TryExtractRecycleRunePaymentResourceActions(
                state,
                intent.PlayerId,
                optionalCosts,
                out _,
                out var paymentResourceActions,
                out var recycledRuneObjectIds))
        {
            return RejectWithCorePrompts(
                state,
                AssembleEquipmentUnsupportedMessage,
                ErrorCodes.UnsupportedCommand);
        }

        var currentPool = state.RunePools.TryGetValue(intent.PlayerId, out var runePool) ? runePool : RunePool.Empty;
        if (!AreRecycleRunePaymentResourceActionsRequired(
                currentPool,
                state.CardObjects,
                recycledRuneObjectIds,
                0,
                LongSwordAssemblePowerCostByTrait))
        {
            return RejectWithCorePrompts(
                state,
                "Recycle rune payment resources are not required to assemble Long Sword.",
                ErrorCodes.InsufficientCost);
        }

        var paymentEvents = new List<GameEvent>();
        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var objectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones);
        var runePools = ApplyRecycleRunePaymentResourceActions(
            state.RunePools,
            playerZones,
            cardObjects,
            objectLocations,
            intent.PlayerId,
            recycledRuneObjectIds,
            paymentEvents,
            "ASSEMBLE_EQUIPMENT");
        var paymentAdjustedPool = runePools.TryGetValue(intent.PlayerId, out var adjustedPool)
            ? adjustedPool
            : RunePool.Empty;
        if (!CanPayRuneCosts(paymentAdjustedPool, 0, 0, LongSwordAssemblePowerCostByTrait))
        {
            return RejectWithCorePrompts(
                state,
                "Not enough resources to assemble Long Sword.",
                ErrorCodes.InsufficientCost);
        }

        runePools = PayRuneCosts(runePools, intent.PlayerId, 0, 0, LongSwordAssemblePowerCostByTrait);
        var equipmentWithIdentity = WithFieldIdentityDefaults(equipmentState, intent.PlayerId);
        var targetWithIdentity = WithFieldIdentityDefaults(targetState, intent.PlayerId);
        cardObjects[command.SourceObjectId] = equipmentWithIdentity with
        {
            AttachedToObjectId = command.TargetObjectId
        };
        cardObjects[command.TargetObjectId] = targetWithIdentity;

        var nextState = state with
        {
            Tick = state.Tick + 1,
            RunePools = runePools,
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            ObjectLocations = objectLocations,
            PassedPriorityPlayerIds = []
        };
        var events = new List<GameEvent>(paymentEvents)
        {
            new(
                "COST_PAID",
                $"{intent.PlayerId} 支付长剑装配费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["mana"] = 0,
                    ["power"] = LongSwordAssemblePowerCost,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["targetObjectId"] = command.TargetObjectId,
                    ["optionalCosts"] = optionalCosts.ToArray(),
                    ["paymentResourceActions"] = paymentResourceActions.ToArray()
                }),
            new(
                "EQUIPMENT_ATTACHED",
                $"{intent.PlayerId} 装配长剑",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["equipmentObjectId"] = command.SourceObjectId,
                    ["unitObjectId"] = command.TargetObjectId,
                    ["controllerId"] = intent.PlayerId,
                    ["ownerId"] = intent.PlayerId,
                    ["attachedToObjectId"] = command.TargetObjectId,
                    ["equipmentCardNo"] = string.IsNullOrWhiteSpace(equipmentWithIdentity.CardNo) ? LongSwordCardNo : equipmentWithIdentity.CardNo,
                    ["targetPower"] = targetWithIdentity.Power,
                    ["optionalCosts"] = optionalCosts.ToArray(),
                    ["paymentResourceActions"] = paymentResourceActions.ToArray()
                })
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static bool TryBuildMinimalAssembleEquipmentPlan(
        MatchState state,
        PlayerIntent intent,
        AssembleEquipmentCommand command,
        out CardObjectState equipmentState,
        out CardObjectState targetState)
    {
        equipmentState = default!;
        targetState = default!;

        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.ActivePlayerId, intent.PlayerId, StringComparison.Ordinal)
            || state.StackItems.Count > 0)
        {
            return false;
        }

        var optionalCosts = NormalizeOptionalCosts(command.OptionalCosts);
        if (!TryExtractRecycleRunePaymentResourceActions(
                state,
                intent.PlayerId,
                optionalCosts,
                out var behaviorOptionalCosts,
                out _,
                out _)
            || behaviorOptionalCosts.Count != 1
            || !string.Equals(behaviorOptionalCosts[0], LongSwordAssembleOptionalCost, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(command.SourceObjectId)
            || string.IsNullOrWhiteSpace(command.TargetObjectId)
            || string.Equals(command.SourceObjectId, command.TargetObjectId, StringComparison.Ordinal))
        {
            return false;
        }

        var sourceLocation = FindFieldObjectLocation(state.PlayerZones, command.SourceObjectId);
        if (sourceLocation is null
            || !string.Equals(sourceLocation.Value.PlayerId, intent.PlayerId, StringComparison.Ordinal)
            || !string.Equals(sourceLocation.Value.Zone, MoveUnitBaseZone, StringComparison.Ordinal)
            || !state.CardObjects.TryGetValue(command.SourceObjectId, out var knownEquipmentState)
            || knownEquipmentState.IsFaceDown
            || !knownEquipmentState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            || !knownEquipmentState.Tags.Contains("武装", StringComparer.Ordinal)
            || !knownEquipmentState.Tags.Contains("灵便", StringComparer.Ordinal)
            || !string.IsNullOrWhiteSpace(knownEquipmentState.AttachedToObjectId)
            || !SourceObjectControlledByPlayerOrLegacyOwned(knownEquipmentState, sourceLocation.Value.PlayerId))
        {
            return false;
        }

        if (!string.Equals(knownEquipmentState.CardNo, LongSwordCardNo, StringComparison.Ordinal))
        {
            return false;
        }

        var targetLocation = FindFieldObjectLocation(state.PlayerZones, command.TargetObjectId);
        if (targetLocation is null
            || !string.Equals(targetLocation.Value.PlayerId, intent.PlayerId, StringComparison.Ordinal)
            || !state.CardObjects.TryGetValue(command.TargetObjectId, out var knownTargetState)
            || string.IsNullOrWhiteSpace(knownTargetState.CardNo)
            || knownTargetState.IsFaceDown
            || !knownTargetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || !SourceObjectControlledByPlayerOrLegacyOwned(knownTargetState, targetLocation.Value.PlayerId))
        {
            return false;
        }

        equipmentState = knownEquipmentState;
        targetState = knownTargetState;
        return true;
    }

    private static CardObjectState WithFieldIdentityDefaults(CardObjectState cardObject, string playerId)
    {
        return cardObject with
        {
            OwnerId = string.IsNullOrWhiteSpace(cardObject.OwnerId) ? playerId : cardObject.OwnerId,
            ControllerId = string.IsNullOrWhiteSpace(cardObject.ControllerId) ? playerId : cardObject.ControllerId
        };
    }

    private static bool FieldIdentityMatchesZone(CardObjectState cardObject, string playerId)
    {
        return (string.IsNullOrWhiteSpace(cardObject.OwnerId)
                || string.Equals(cardObject.OwnerId, playerId, StringComparison.Ordinal))
            && (string.IsNullOrWhiteSpace(cardObject.ControllerId)
                || string.Equals(cardObject.ControllerId, playerId, StringComparison.Ordinal));
    }

    private static bool FieldIdentityExplicitlyMatchesZone(CardObjectState cardObject, string playerId)
    {
        return string.Equals(cardObject.OwnerId, playerId, StringComparison.Ordinal)
            && string.Equals(cardObject.ControllerId, playerId, StringComparison.Ordinal);
    }

    private static string BuildReturnControlToOwnerAtTurnEndEffectId(string ownerId)
    {
        return $"{ReturnControlToOwnerAtTurnEndEffectPrefix}{ownerId}";
    }

    private static IReadOnlyList<string> AddUntilEndOfTurnEffect(
        IReadOnlyList<string> existingEffectIds,
        string effectId)
    {
        return existingEffectIds
            .Concat([effectId])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> RemoveUntilEndOfTurnEffect(
        IReadOnlyList<string> existingEffectIds,
        string effectId)
    {
        return existingEffectIds
            .Where(id => !string.Equals(id, effectId, StringComparison.Ordinal))
            .ToArray();
    }

    private static IReadOnlyList<string> MarkArmamentPlayedThisTurn(
        IReadOnlyList<string> existingEffectIds,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return IsArmamentPlayBehavior(behavior)
            ? AddUntilEndOfTurnEffect(existingEffectIds, BuildPlayedArmamentThisTurnEffectId(playerId))
            : existingEffectIds;
    }

    private static IReadOnlyList<string> MarkEquipmentPlayedThisTurn(
        IReadOnlyList<string> existingEffectIds,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return behavior.PlaysSourceToBaseAsEquipment
            ? AddUntilEndOfTurnEffect(existingEffectIds, BuildPlayedEquipmentThisTurnEffectId(playerId))
            : existingEffectIds;
    }

    private static bool IsArmamentPlayBehavior(CardBehaviorDefinition behavior)
    {
        return behavior.PlaysSourceToBaseAsEquipment
            && (ParseDelimitedValues(behavior.SourceEquipmentTags)
                    .Contains("武装", StringComparer.Ordinal)
                || IsOfficialArmamentEquipmentCardNo(behavior.CardNo));
    }

    private static bool IsOfficialArmamentEquipmentCardNo(string? cardNo)
    {
        return cardNo is "UNL-019/219"
            or "UNL-039/219"
            or "UNL-096/219"
            or "UNL-158/219"
            or "SFD·009/221"
            or "SFD·016/221"
            or "SFD·022/221"
            or "SFD·030/221"
            or "SFD·033/221"
            or "SFD·042/221"
            or "SFD·051/221"
            or "SFD·056/221"
            or "SFD·059/221"
            or "SFD·064/221"
            or "SFD·073/221"
            or "SFD·090/221"
            or "SFD·095/221"
            or "SFD·102/221"
            or "SFD·108/221"
            or "SFD·115/221"
            or "SFD·118/221"
            or "SFD·118a/221·P"
            or "SFD·124/221"
            or "SFD·133/221"
            or "SFD·134/221"
            or "SFD·139/221"
            or "SFD·150/221"
            or "SFD·153/221"
            or "SFD·161/221"
            or "SFD·172/221"
            or "SFD·178/221";
    }

    private static string BuildPlayedArmamentThisTurnEffectId(string playerId)
    {
        return $"{PlayedArmamentThisTurnEffectPrefix}{playerId}";
    }

    private static string BuildPlayedEquipmentThisTurnEffectId(string playerId)
    {
        return $"{PlayedEquipmentThisTurnEffectPrefix}{playerId}";
    }

    private static string BuildBattlefieldFriendlySpellDrawUsedEffectId(string playerId, string sourceObjectId)
    {
        return $"{BattlefieldFriendlySpellDrawUsedEffectPrefix}{playerId}:{sourceObjectId}";
    }

    private static string BuildBattlefieldFirstUnitPlayedMoveOtherToBaseUsedEffectId(
        string playerId,
        string battlefieldObjectId)
    {
        return $"{BattlefieldFirstUnitPlayedMoveOtherToBaseUsedEffectPrefix}{playerId}:{battlefieldObjectId}";
    }

    private static string BuildBattlefieldHeldUnitCostIncreaseEffectId(string playerId)
    {
        return $"{BattlefieldHeldUnitCostIncreaseEffectPrefix}{playerId}";
    }

    private static string BuildBattlefieldHeldNextSpellEchoEffectId(string playerId)
    {
        return $"{BattlefieldHeldNextSpellEchoEffectPrefix}{playerId}";
    }

    private static bool ControllerPlayedArmamentThisTurn(MatchState state, string playerId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            BuildPlayedArmamentThisTurnEffectId(playerId),
            StringComparer.Ordinal);
    }

    private static bool ControllerPlayedEquipmentThisTurn(MatchState state, string playerId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            BuildPlayedEquipmentThisTurnEffectId(playerId),
            StringComparer.Ordinal);
    }

    private static bool BattlefieldFriendlySpellDrawUsedThisTurn(
        MatchState state,
        string playerId,
        string sourceObjectId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            BuildBattlefieldFriendlySpellDrawUsedEffectId(playerId, sourceObjectId),
            StringComparer.Ordinal);
    }

    private static bool BattlefieldHeldUnitCostIncreaseActive(MatchState state, string playerId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            BuildBattlefieldHeldUnitCostIncreaseEffectId(playerId),
            StringComparer.Ordinal);
    }

    private static bool BattlefieldHeldNextSpellEchoActive(MatchState state, string playerId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            BuildBattlefieldHeldNextSpellEchoEffectId(playerId),
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> MarkRengarUnitPlayedTriggerTarget(
        IReadOnlyList<string> existingEffectIds,
        StackItemState stackItem,
        string targetObjectId)
    {
        return string.IsNullOrWhiteSpace(targetObjectId)
            ? existingEffectIds
            : AddUntilEndOfTurnEffect(
                existingEffectIds,
                $"{RengarUnitPlayedTargetEffectPrefix}{stackItem.StackItemId}:{targetObjectId}");
    }

    private static IReadOnlyList<string> MarkLeonaStunBoonTriggerTarget(
        IReadOnlyList<string> existingEffectIds,
        StackItemState stackItem,
        string targetObjectId)
    {
        return string.IsNullOrWhiteSpace(targetObjectId)
            ? existingEffectIds
            : AddUntilEndOfTurnEffect(
                existingEffectIds,
                $"{LeonaStunBoonTargetEffectPrefix}{stackItem.StackItemId}:{targetObjectId}");
    }

    private static IReadOnlyList<string> MarkEzrealEnemyTargetsThisTurn(
        MatchState state,
        IReadOnlyList<string> existingEffectIds,
        string playerId,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds)
    {
        if (!ControllerHasEzrealLegend(state, playerId)
            || !state.CardObjects.TryGetValue(sourceObjectId, out var sourceState)
            || !sourceState.Tags.Contains(CardObjectTags.SpellCard, StringComparer.Ordinal)
            || !targetObjectIds.Any(targetObjectId => IsEnemyUnitOrEquipmentTarget(state, playerId, targetObjectId)))
        {
            return existingEffectIds;
        }

        return SetEzrealEnemyTargetCount(
            existingEffectIds,
            playerId,
            EzrealEnemyTargetsThisTurnCount(existingEffectIds, playerId) + 1);
    }

    private static IReadOnlyList<string> MarkEzrealEnemyTargetsThisTurnForUnitAbility(
        MatchState state,
        IReadOnlyList<string> existingEffectIds,
        string playerId,
        IReadOnlyList<string> targetObjectIds)
    {
        if (!ControllerHasEzrealLegend(state, playerId)
            || !targetObjectIds.Any(targetObjectId => IsEnemyUnitOrEquipmentTarget(state, playerId, targetObjectId)))
        {
            return existingEffectIds;
        }

        return SetEzrealEnemyTargetCount(
            existingEffectIds,
            playerId,
            EzrealEnemyTargetsThisTurnCount(existingEffectIds, playerId) + 1);
    }

    private static IReadOnlyList<string> SetEzrealEnemyTargetCount(
        IReadOnlyList<string> existingEffectIds,
        string playerId,
        int count)
    {
        var prefix = $"{EzrealEnemyTargetsThisTurnPrefix}{playerId}:";
        return existingEffectIds
            .Where(effectId => !effectId.StartsWith(prefix, StringComparison.Ordinal))
            .Concat([$"{prefix}{Math.Min(count, EzrealEnemyTargetThreshold)}"])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(effectId => effectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static int EzrealEnemyTargetsThisTurnCount(
        IReadOnlyList<string> effectIds,
        string playerId)
    {
        var prefix = $"{EzrealEnemyTargetsThisTurnPrefix}{playerId}:";
        return effectIds
            .Where(effectId => effectId.StartsWith(prefix, StringComparison.Ordinal))
            .Select(effectId => int.TryParse(effectId[prefix.Length..], out var count) ? count : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static bool IsEnemyUnitOrEquipmentTarget(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return IsEnemyFieldObject(state, playerId, targetObjectId)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && (targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                || targetState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal));
    }

    private static bool ControllerHasEzrealLegend(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.LegendZone.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && IsEzrealLegendCardNo(cardObject.CardNo));
    }

    private static bool IsEzrealLegendCardNo(string? cardNo)
    {
        return cardNo is "SFD·199/221" or "SFD·248/221";
    }

    private static bool IsQueuedOnPlaySourcePowerTrigger(CardBehaviorDefinition behavior)
    {
        return behavior.AppliesPowerModifierToSourceUnit
            && behavior.PowerModifierAmount != 0
            && behavior.EffectKind.Contains("TEEMO", StringComparison.Ordinal)
            && behavior.EffectKind.Contains("PLAY_UNIT_SELF_POWER_PLUS_3", StringComparison.Ordinal);
    }

    private static TriggerQueueItemState BuildOnPlayTriggerQueueItem(
        StackItemState stackItem,
        CardBehaviorDefinition behavior)
    {
        return new TriggerQueueItemState(
            $"TRIGGER-{stackItem.StackItemId}-{behavior.EffectKind}",
            stackItem.ControllerId,
            stackItem.SourceObjectId,
            behavior.EffectKind,
            "UNIT_PLAYED_TO_BASE");
    }

    private static string? ResolveWatchfulSentinelLastBreathDrawPlayerId(
        CardObjectState destroyedState,
        FieldRemovalResult removalResult)
    {
        if (!removalResult.WasDestroyed
            || !removalResult.WasUnit
            || !string.Equals(removalResult.DestinationZone, "GRAVEYARD", StringComparison.Ordinal)
            || !string.Equals(destroyedState.CardNo, WatchfulSentinelCardNo, StringComparison.Ordinal))
        {
            return null;
        }

        return destroyedState.ControllerId
            ?? destroyedState.OwnerId
            ?? removalResult.OwnerPlayerId;
    }

    private static TriggerQueueItemState BuildLastBreathTriggerQueueItem(
        StackItemState stackItem,
        string sourceObjectId,
        string controllerId,
        string effectKind)
    {
        return new TriggerQueueItemState(
            $"TRIGGER-{stackItem.StackItemId}-{sourceObjectId}-{effectKind}",
            controllerId,
            sourceObjectId,
            effectKind,
            "UNIT_DESTROYED");
    }

    private static GameEvent BuildTriggerQueuedEvent(TriggerQueueItemState trigger)
    {
        return new GameEvent(
            "TRIGGER_QUEUED",
            "触发能力加入队列",
            new Dictionary<string, object?>
            {
                ["triggerId"] = trigger.TriggerId,
                ["controllerId"] = trigger.ControllerId,
                ["sourceObjectId"] = trigger.SourceObjectId,
                ["effectKind"] = trigger.EffectKind,
                ["triggeredByEventKind"] = trigger.TriggeredByEventKind
            });
    }

    private static GameEvent BuildTriggerResolvedEvent(TriggerQueueItemState trigger)
    {
        return new GameEvent(
            "TRIGGER_RESOLVED",
            "触发能力结算",
            new Dictionary<string, object?>
            {
                ["triggerId"] = trigger.TriggerId,
                ["controllerId"] = trigger.ControllerId,
                ["sourceObjectId"] = trigger.SourceObjectId,
                ["effectKind"] = trigger.EffectKind
            });
    }

    private static ResolutionResult ResolveActivateAbility(
        MatchState state,
        PlayerIntent intent,
        ActivateAbilityCommand command)
    {
        if (string.Equals(command.AbilityId, BattlefieldUnitGainExperienceAbilityId, StringComparison.Ordinal))
        {
            return ResolveBattlefieldUnitGainExperienceAbility(state, intent, command);
        }

        if (!P4ActivatedAbilityCatalog.TryGetByAbilityId(command.AbilityId, out var ability))
        {
            return RejectWithCorePrompts(
                state,
                "ACTIVATE_ABILITY is not implemented in P4 yet.",
                ErrorCodes.UnsupportedCommand);
        }

        if (string.Equals(ability.AbilityId, P4ActivatedAbilityCatalog.XerathDamageAbilityId, StringComparison.Ordinal))
        {
            return ResolveXerathDamageAbility(state, intent, command, ability);
        }

        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.ActivePlayerId, intent.PlayerId, StringComparison.Ordinal)
            || state.StackItems.Count > 0)
        {
            return RejectWithCorePrompts(
                state,
                "ACTIVATE_ABILITY is only available during the active player's open main window.",
                ErrorCodes.PhaseNotAllowed);
        }

        if (command.TargetObjectIds.Count != 0
            || NormalizeOptionalCosts(command.OptionalCosts).Count != 0)
        {
            return RejectWithCorePrompts(
                state,
                "Vi's P4 double-power ability does not accept targets or optional costs.",
                ErrorCodes.InvalidTarget);
        }

        if (!IsControlledFieldObject(state, intent.PlayerId, command.SourceObjectId)
            || !state.CardObjects.TryGetValue(command.SourceObjectId, out var sourceState)
            || !sourceState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Ability source is not a controlled field unit.",
                ErrorCodes.InvalidTarget);
        }

        if (!SourceObjectControlledByPlayerOrLegacyOwned(sourceState, intent.PlayerId))
        {
            return RejectWithCorePrompts(
                state,
                "ACTIVATE_ABILITY source must be controlled by the acting player.",
                ErrorCodes.InvalidTarget);
        }

        if (!string.Equals(sourceState.CardNo, ability.SourceCardNo, StringComparison.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Ability source does not expose Vi's P4 double-power ability.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        var currentPool = state.RunePools.TryGetValue(intent.PlayerId, out var runePool) ? runePool : RunePool.Empty;
        if (!CanPayRuneCosts(currentPool, ability.ManaCost, ability.PowerCost))
        {
            return RejectWithCorePrompts(
                state,
                "Not enough resources to activate Vi's double-power ability.",
                ErrorCodes.InsufficientCost);
        }

        var runePools = PayRuneCosts(
            state,
            intent.PlayerId,
            ability.ManaCost,
            ability.PowerCost);
        var stackItem = new StackItemState(
            $"STACK-{state.Tick + 1}-{command.SourceObjectId}-ABILITY",
            intent.PlayerId,
            command.SourceObjectId,
            ability.EffectKind,
            ability.SourceCardNo,
            [],
            0,
            1,
            []);
        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = intent.PlayerId,
            TimingState = TimingStates.NeutralClosed,
            RunePools = runePools,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, state.PlayerZones),
            PriorityPlayerId = intent.PlayerId,
            PassedPriorityPlayerIds = [],
            StackItems = state.StackItems.Concat([stackItem]).ToArray()
        };
        var events = new List<GameEvent>
        {
            new(
                "ABILITY_ACTIVATED",
                $"{intent.PlayerId} 激活蔚的技能",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["abilityId"] = command.AbilityId,
                    ["effectKind"] = ability.EffectKind
                }),
            new(
                "COST_PAID",
                $"{intent.PlayerId} 支付蔚技能费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["mana"] = ability.ManaCost,
                    ["power"] = ability.PowerCost,
                    ["abilityId"] = command.AbilityId
                }),
            new(
                "STACK_ITEM_ADDED",
                "蔚的技能加入结算链",
                new Dictionary<string, object?>
                {
                    ["stackItemId"] = stackItem.StackItemId,
                    ["controllerId"] = stackItem.ControllerId,
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["cardNo"] = stackItem.CardNo,
                    ["targetObjectIds"] = stackItem.TargetObjectIds.ToArray(),
                    ["effectKind"] = stackItem.EffectKind,
                    ["abilityId"] = command.AbilityId
                })
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static ResolutionResult ResolveBattlefieldUnitGainExperienceAbility(
        MatchState state,
        PlayerIntent intent,
        ActivateAbilityCommand command)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.ActivePlayerId, intent.PlayerId, StringComparison.Ordinal)
            || state.StackItems.Count > 0)
        {
            return RejectWithCorePrompts(
                state,
                "Battlefield unit experience ability is only available during the active player's open main window.",
                ErrorCodes.PhaseNotAllowed);
        }

        if (NormalizeTargetObjectIds(command.TargetObjectIds).Count != 0
            || NormalizeOptionalCosts(command.OptionalCosts).Count != 0)
        {
            return RejectWithCorePrompts(
                state,
                "Battlefield unit experience ability does not accept targets or optional costs.",
                ErrorCodes.InvalidTarget);
        }

        if (!state.PlayerZones.TryGetValue(intent.PlayerId, out var zones)
            || !zones.Battlefields.Contains(command.SourceObjectId, StringComparer.Ordinal)
            || !state.CardObjects.TryGetValue(command.SourceObjectId, out var sourceState)
            || !sourceState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || !string.Equals(sourceState.ControllerId, intent.PlayerId, StringComparison.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Battlefield unit experience ability source must be a controlled battlefield unit.",
                ErrorCodes.InvalidTarget);
        }

        if (string.IsNullOrWhiteSpace(sourceState.CardNo))
        {
            return RejectWithCorePrompts(
                state,
                "Battlefield unit experience ability source must expose a known unit card number.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (sourceState.IsExhausted)
        {
            return RejectWithCorePrompts(
                state,
                "Battlefield unit experience ability source must be active.",
                ErrorCodes.InvalidTarget);
        }

        var battlefieldObjectId = zones.Battlefields.FirstOrDefault(objectId =>
            !string.Equals(objectId, command.SourceObjectId, StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(objectId, out var battlefieldState)
            && IsBattlefieldGrantUnitExperienceCardNo(battlefieldState.CardNo)
            && SourceObjectControlledByPlayerOrLegacyOwned(battlefieldState, intent.PlayerId));
        if (string.IsNullOrWhiteSpace(battlefieldObjectId)
            || !state.CardObjects.TryGetValue(battlefieldObjectId, out var battlefieldCardState))
        {
            return RejectWithCorePrompts(
                state,
                "No implemented Mutation Garden battlefield grants that unit ability.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[command.SourceObjectId] = sourceState with
        {
            IsExhausted = true,
            OwnerId = string.IsNullOrWhiteSpace(sourceState.OwnerId) ? intent.PlayerId : sourceState.OwnerId,
            ControllerId = string.IsNullOrWhiteSpace(sourceState.ControllerId) ? intent.PlayerId : sourceState.ControllerId
        };
        var playerExperience = NormalizeExperienceForSeats(state);
        playerExperience[intent.PlayerId] = playerExperience.TryGetValue(intent.PlayerId, out var currentExperience)
            ? currentExperience + 1
            : 1;
        var events = new List<GameEvent>
        {
            new(
                "ABILITY_ACTIVATED",
                $"{intent.PlayerId} 激活蜕变花园授予的经验能力",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["abilityId"] = command.AbilityId,
                    ["battlefieldObjectId"] = battlefieldObjectId,
                    ["battlefieldCardNo"] = battlefieldCardState.CardNo
                }),
            new(
                "UNIT_EXHAUSTED",
                $"{command.SourceObjectId} 横置",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["abilityId"] = command.AbilityId,
                    ["battlefieldObjectId"] = battlefieldObjectId
                }),
            new(
                "BATTLEFIELD_TRIGGER_RESOLVED",
                $"{intent.PlayerId} 通过蜕变花园获得经验",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["battlefieldObjectId"] = battlefieldObjectId,
                    ["battlefieldCardNo"] = battlefieldCardState.CardNo,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["abilityId"] = command.AbilityId,
                    ["trigger"] = "BATTLEFIELD_UNIT_EXHAUST_GAIN_EXPERIENCE"
                }),
            new(
                "EXPERIENCE_GAINED",
                $"{intent.PlayerId} 获得 1 经验",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["cardNo"] = sourceState.CardNo,
                    ["amount"] = 1,
                    ["totalExperience"] = playerExperience[intent.PlayerId],
                    ["abilityId"] = command.AbilityId,
                    ["battlefieldObjectId"] = battlefieldObjectId
                })
        };
        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = intent.PlayerId,
            CardObjects = cardObjects,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, state.PlayerZones),
            PlayerExperience = playerExperience
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static ResolutionResult ResolveLegendAct(
        MatchState state,
        PlayerIntent intent,
        LegendActCommand command)
    {
        if (!TryGetLegendAbility(command.AbilityId, out var ability))
        {
            return RejectWithCorePrompts(
                state,
                "LEGEND_ACT ability is not implemented yet.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (!IsLegendAbilityTimingAllowed(state, intent.PlayerId, ability))
        {
            return RejectWithCorePrompts(
                state,
                "LEGEND_ACT is not available in the current timing window for that ability.",
                ErrorCodes.PhaseNotAllowed);
        }

        var targetObjectIds = NormalizeTargetObjectIds(command.TargetObjectIds);
        if (targetObjectIds.Count != ability.RequiredTargetCount)
        {
            return RejectWithCorePrompts(
                state,
                $"{ability.DisplayName} requires {ability.RequiredTargetCount} target(s).",
                ErrorCodes.InvalidTarget);
        }

        var optionalCosts = NormalizeOptionalCosts(command.OptionalCosts);
        var manaCost = ResolveLegendAbilityManaCost(state, intent.PlayerId, ability);
        var requiredManaCostToken = manaCost > 0 ? $"{SpendManaOptionalCostPrefix}{manaCost}" : string.Empty;
        var requiredCostToken = string.IsNullOrWhiteSpace(ability.RequiredCostToken)
            ? requiredManaCostToken
            : ability.RequiredCostToken;
        var requiredCostTokens = string.IsNullOrWhiteSpace(requiredCostToken)
            ? Array.Empty<string>()
            : [requiredCostToken];
        if (!optionalCosts.SequenceEqual(requiredCostTokens, StringComparer.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                string.IsNullOrWhiteSpace(requiredCostToken)
                    ? $"{ability.DisplayName} does not accept optional costs."
                    : $"{ability.DisplayName} requires {requiredCostToken}.",
                ErrorCodes.InsufficientCost);
        }

        if (!state.PlayerZones.TryGetValue(intent.PlayerId, out var zones)
            || !zones.LegendZone.Contains(command.SourceObjectId, StringComparer.Ordinal)
            || !state.CardObjects.TryGetValue(command.SourceObjectId, out var sourceState))
        {
            return RejectWithCorePrompts(
                state,
                "LEGEND_ACT source must be a controlled legend object.",
                ErrorCodes.InvalidTarget);
        }

        if (!SourceObjectControlledByPlayerOrLegacyOwned(sourceState, intent.PlayerId))
        {
            return RejectWithCorePrompts(
                state,
                "LEGEND_ACT source must be controlled by the acting player.",
                ErrorCodes.InvalidTarget);
        }

        if (string.IsNullOrWhiteSpace(sourceState.CardNo))
        {
            return RejectWithCorePrompts(
                state,
                "LEGEND_ACT source must expose a known legend card number.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (!LegendAbilitySourceHasAbility(state, intent.PlayerId, sourceState.CardNo, ability))
        {
            return RejectWithCorePrompts(
                state,
                "LEGEND_ACT source does not expose the requested implemented legend ability.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (sourceState.IsExhausted)
        {
            return RejectWithCorePrompts(
                state,
                "LEGEND_ACT source must be active.",
                ErrorCodes.InvalidTarget);
        }

        if (ability.RequiresPlayedAnotherCardThisTurn
            && !ControllerPlayedAnotherCardThisTurn(state, intent.PlayerId))
        {
            return RejectWithCorePrompts(
                state,
                $"{ability.DisplayName} requires the controller to have played another card this turn.",
                ErrorCodes.InsufficientCost);
        }

        if (ability.RequiresPlayedArmamentThisTurn
            && !ControllerPlayedArmamentThisTurn(state, intent.PlayerId))
        {
            return RejectWithCorePrompts(
                state,
                $"{ability.DisplayName} requires the controller to have played an armament this turn.",
                ErrorCodes.InsufficientCost);
        }

        if (ability.RequiresPendingSpellStackItem
            && !PendingStackSourceHasTag(state, CardObjectTags.SpellCard))
        {
            return RejectWithCorePrompts(
                state,
                $"{ability.DisplayName} requires a pending spell stack item.",
                ErrorCodes.InvalidTarget);
        }

        if (ability.RequiresPendingEquipmentStackItem
            && !PendingStackSourceHasTag(state, CardObjectTags.EquipmentCard))
        {
            return RejectWithCorePrompts(
                state,
                $"{ability.DisplayName} requires a pending equipment stack item.",
                ErrorCodes.InvalidTarget);
        }

        if (ability.RequiresEzrealEnemyTargetsThisTurn
            && EzrealEnemyTargetsThisTurnCount(state.UntilEndOfTurnEffects, intent.PlayerId) < EzrealEnemyTargetThreshold)
        {
            return RejectWithCorePrompts(
                state,
                $"{ability.DisplayName} requires two enemy unit or equipment targets from spells or unit skills this turn.",
                ErrorCodes.InsufficientCost);
        }

        if (ability.RequiresPendingFriendlyUnitTarget
            && (targetObjectIds.Count == 0
                || !IsPendingFriendlyUnitTarget(state, intent.PlayerId, targetObjectIds[0])))
        {
            return RejectWithCorePrompts(
                state,
                $"{ability.DisplayName} requires a pending spell or skill targeting that friendly unit.",
                ErrorCodes.InvalidTarget);
        }

        if (ability.RequiresOwnedTeemoUnitTarget
            && !IsValidOwnedTeemoUnitTarget(state, intent.PlayerId, targetObjectIds[0]))
        {
            return RejectWithCorePrompts(
                state,
                $"{ability.DisplayName} target must be an owned Teemo unit in the champion zone or on the field.",
                ErrorCodes.InvalidTarget);
        }

        if (ability.RequiresFriendlyUnitTarget
            && !IsValidLegendAbilityTarget(state, intent.PlayerId, targetObjectIds[0], ability))
        {
            return RejectWithCorePrompts(
                state,
                $"{ability.DisplayName} target must satisfy its implemented legend target restrictions.",
                ErrorCodes.InvalidTarget);
        }

        if (ability.RequiresArmamentSecondTarget
            && !IsValidLegendArmamentSecondTarget(state, intent.PlayerId, targetObjectIds[0], targetObjectIds[1], ability))
        {
            return RejectWithCorePrompts(
                state,
                $"{ability.DisplayName} equipment target must satisfy its implemented legend target restrictions.",
                ErrorCodes.InvalidTarget);
        }

        var currentPool = state.RunePools.TryGetValue(intent.PlayerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < manaCost)
        {
            return RejectWithCorePrompts(
                state,
                $"Not enough mana to activate {ability.DisplayName}.",
                ErrorCodes.InsufficientCost);
        }

        var currentExperience = NormalizeExperienceForSeats(state);
        if (ability.ExperienceCost > 0
            && (!currentExperience.TryGetValue(intent.PlayerId, out var experience)
                || experience < ability.ExperienceCost))
        {
            return RejectWithCorePrompts(
                state,
                $"Not enough experience to activate {ability.DisplayName}.",
                ErrorCodes.InsufficientCost);
        }

        var playerZones = NormalizeZonesForSeats(state);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var runePools = PayRuneCosts(state, intent.PlayerId, manaCost, 0);
        var playerExperience = PayExperienceCosts(state, intent.PlayerId, ability.ExperienceCost);
        IReadOnlyDictionary<string, int> playerScores = NormalizeScoresForSeats(state);
        var winnerPlayerId = state.WinnerPlayerId;
        var rngCursor = state.RngCursor;
        var events = new List<GameEvent>
        {
            new(
                "LEGEND_ABILITY_ACTIVATED",
                $"{intent.PlayerId} 使用{ability.DisplayName}",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["cardNo"] = sourceState.CardNo,
                    ["abilityId"] = command.AbilityId
                })
        };
        if (manaCost > 0)
        {
            events.Add(new GameEvent(
                "COST_PAID",
                $"{intent.PlayerId} 支付{ability.DisplayName}费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["mana"] = manaCost,
                    ["power"] = 0,
                    ["abilityId"] = command.AbilityId
                }));
        }

        if (ability.ExperienceCost > 0)
        {
            events.Add(new GameEvent(
                "EXPERIENCE_SPENT",
                $"{intent.PlayerId} 支付 {ability.ExperienceCost} 经验",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["amount"] = ability.ExperienceCost,
                    ["remainingExperience"] = playerExperience[intent.PlayerId],
                    ["abilityId"] = command.AbilityId
                }));
        }

        events.Add(
            new(
                "LEGEND_EXHAUSTED",
                $"{command.SourceObjectId} 横置",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["abilityId"] = command.AbilityId
                }));

        cardObjects[command.SourceObjectId] = sourceState with
        {
            IsExhausted = true,
            OwnerId = string.IsNullOrWhiteSpace(sourceState.OwnerId) ? intent.PlayerId : sourceState.OwnerId,
            ControllerId = string.IsNullOrWhiteSpace(sourceState.ControllerId) ? intent.PlayerId : sourceState.ControllerId
        };

        switch (ability.EffectKind)
        {
            case LegendAbilityEffectKinds.DrawOne:
            {
                var drawApplication = ApplyDrawToPlayer(
                    state,
                    playerZones,
                    playerScores,
                    intent.PlayerId,
                    1,
                    rngCursor,
                    events);
                playerScores = drawApplication.PlayerScores;
                winnerPlayerId = drawApplication.WinnerPlayerId ?? winnerPlayerId;
                rngCursor = drawApplication.RngCursor;
                break;
            }
            case LegendAbilityEffectKinds.MoveFriendlyUnit:
                MoveLegendTargetBetweenBaseAndBattlefield(
                    playerZones,
                    targetObjectIds[0],
                    intent.PlayerId,
                    command.SourceObjectId,
                    command.AbilityId,
                    events);
                break;
            case LegendAbilityEffectKinds.GrantBoon:
                GrantLegendBoon(
                    cardObjects,
                    targetObjectIds[0],
                    intent.PlayerId,
                    command.SourceObjectId,
                    command.AbilityId,
                    events);
                break;
            case LegendAbilityEffectKinds.GrantRoam:
                GrantLegendRoam(
                    cardObjects,
                    targetObjectIds[0],
                    intent.PlayerId,
                    command.SourceObjectId,
                    command.AbilityId,
                    events);
                break;
            case LegendAbilityEffectKinds.ReturnBattlefieldUnitAndCreateCoin:
            {
                var canResolveBattlefieldReturnTrigger = TryGetBattlefieldUnitReturnContext(
                    playerZones,
                    cardObjects,
                    targetObjectIds[0],
                    out var battlefieldReturnPlayerId);
                ReturnLegendTargetToOwnerHand(
                    playerZones,
                    cardObjects,
                    targetObjectIds[0],
                    intent.PlayerId,
                    command.SourceObjectId,
                    command.AbilityId,
                    events);
                CreateLegendEquipmentToken(
                    playerZones,
                    cardObjects,
                    intent.PlayerId,
                    command.SourceObjectId,
                    command.AbilityId,
                    "金币",
                    [CardObjectTags.EquipmentCard, "反应"],
                    isExhausted: true,
                    events);
                if (canResolveBattlefieldReturnTrigger
                    && TryResolveBattlefieldUnitReturnedCallRuneTrigger(
                        playerZones,
                        cardObjects,
                        runePools,
                        battlefieldReturnPlayerId,
                        targetObjectIds[0],
                        command.SourceObjectId,
                        events,
                        out var battlefieldReturnRunePools))
                {
                    runePools = battlefieldReturnRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                }
                break;
            }
            case LegendAbilityEffectKinds.AttachArmament:
                AddBattlefieldGrantedLegendAbilityEventIfNeeded(
                    playerZones,
                    cardObjects,
                    intent.PlayerId,
                    command.SourceObjectId,
                    targetObjectIds[0],
                    targetObjectIds[1],
                    ability,
                    events);
                AttachOrReattachLegendArmament(
                    cardObjects,
                    targetObjectIds[0],
                    targetObjectIds[1],
                    intent.PlayerId,
                    command.SourceObjectId,
                    command.AbilityId,
                    isReattach: false,
                    events);
                break;
            case LegendAbilityEffectKinds.ReattachArmament:
                AttachOrReattachLegendArmament(
                    cardObjects,
                    targetObjectIds[0],
                    targetObjectIds[1],
                    intent.PlayerId,
                    command.SourceObjectId,
                    command.AbilityId,
                    isReattach: true,
                    events);
                break;
            case LegendAbilityEffectKinds.GainMana:
                runePools = GainLegendMana(
                    runePools,
                    intent.PlayerId,
                    ability.ManaGainAmount,
                    command.SourceObjectId,
                    command.AbilityId,
                    events);
                break;
            case LegendAbilityEffectKinds.GainPower:
                runePools = GainLegendPower(
                    runePools,
                    intent.PlayerId,
                    ability.PowerGainAmount,
                    command.SourceObjectId,
                    command.AbilityId,
                    events);
                break;
            case LegendAbilityEffectKinds.ReadyFriendlyUnit:
                ReadyLegendFriendlyUnit(
                    cardObjects,
                    targetObjectIds[0],
                    intent.PlayerId,
                    command.SourceObjectId,
                    command.AbilityId,
                    events);
                break;
            case LegendAbilityEffectKinds.ReturnOwnedTeemoUnitToHand:
            {
                var canResolveBattlefieldReturnTrigger = TryGetBattlefieldUnitReturnContext(
                    playerZones,
                    cardObjects,
                    targetObjectIds[0],
                    out var battlefieldReturnPlayerId);
                ReturnOwnedTeemoUnitToHand(
                    playerZones,
                    cardObjects,
                    targetObjectIds[0],
                    intent.PlayerId,
                    command.SourceObjectId,
                    command.AbilityId,
                    events);
                if (canResolveBattlefieldReturnTrigger
                    && TryResolveBattlefieldUnitReturnedCallRuneTrigger(
                        playerZones,
                        cardObjects,
                        runePools,
                        battlefieldReturnPlayerId,
                        targetObjectIds[0],
                        command.SourceObjectId,
                        events,
                        out var battlefieldReturnRunePools))
                {
                    runePools = battlefieldReturnRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                }
                break;
            }
            case LegendAbilityEffectKinds.CreateSandSoldier:
                CreateLegendSandSoldier(
                    playerZones,
                    cardObjects,
                    intent.PlayerId,
                    command.SourceObjectId,
                    command.AbilityId,
                    events);
                break;
            case LegendAbilityEffectKinds.CreateMinion:
                CreateLegendMinion(
                    playerZones,
                    cardObjects,
                    intent.PlayerId,
                    command.SourceObjectId,
                    command.AbilityId,
                    events);
                break;
            case LegendAbilityEffectKinds.CreateFaerie:
                CreateLegendFaerie(
                    playerZones,
                    cardObjects,
                    intent.PlayerId,
                    command.SourceObjectId,
                    command.AbilityId,
                    events);
                break;
        }

        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = intent.PlayerId,
            RunePools = runePools,
            PlayerZones = playerZones,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones),
            CardObjects = cardObjects,
            PlayerExperience = playerExperience,
            PlayerScores = playerScores,
            RngCursor = rngCursor,
            Status = winnerPlayerId is null ? state.Status : MatchStatuses.Finished,
            WinnerPlayerId = winnerPlayerId
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static bool IsLegendAbilityTimingAllowed(
        MatchState state,
        string playerId,
        LegendAbilityDefinition ability)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal))
        {
            return false;
        }

        return ability.TimingKind switch
        {
            LegendAbilityTimingKinds.MainOpen =>
                string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                && string.Equals(state.ActivePlayerId, playerId, StringComparison.Ordinal)
                && state.StackItems.Count == 0,
            LegendAbilityTimingKinds.PriorityWindow =>
                state.StackItems.Count > 0
                && !string.IsNullOrWhiteSpace(state.PriorityPlayerId)
                && string.Equals(state.PriorityPlayerId, playerId, StringComparison.Ordinal),
            LegendAbilityTimingKinds.SpellDuelFocus =>
                state.StackItems.Count == 0
                && string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(state.FocusPlayerId)
                && string.Equals(state.FocusPlayerId, playerId, StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool LegendAbilitySourceHasAbility(
        MatchState state,
        string playerId,
        string? sourceCardNo,
        LegendAbilityDefinition ability)
    {
        return ability.SourceCardNos.Contains(sourceCardNo, StringComparer.Ordinal)
            || (!string.IsNullOrWhiteSpace(ability.RequiredControlledBattlefieldCardNo)
                && PlayerControlsBattlefieldCard(state, playerId, ability.RequiredControlledBattlefieldCardNo));
    }

    private static bool PlayerControlsBattlefieldCard(
        MatchState state,
        string playerId,
        string cardNo)
    {
        return TryGetControlledBattlefieldCardObject(
            state.PlayerZones,
            state.CardObjects,
            playerId,
            cardNo,
            out _,
            out _);
    }

    private static bool TryGetControlledBattlefieldCardObject(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string cardNo,
        out string battlefieldObjectId,
        out CardObjectState battlefieldState)
    {
        battlefieldObjectId = string.Empty;
        battlefieldState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.Battlefields)
        {
            if (cardObjects.TryGetValue(objectId, out var candidate)
                && string.Equals(candidate.CardNo, cardNo, StringComparison.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(candidate, playerId))
            {
                battlefieldObjectId = objectId;
                battlefieldState = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool PendingStackSourceHasTag(MatchState state, string tag)
    {
        return state.StackItems.Any(stackItem =>
            state.CardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceState)
            && sourceState.Tags.Contains(tag, StringComparer.Ordinal));
    }

    private static bool TryGetLegendAbility(
        string abilityId,
        out LegendAbilityDefinition ability)
    {
        ability = abilityId switch
        {
            YasuoLegendAbilityId => new LegendAbilityDefinition(
                YasuoLegendAbilityId,
                [YasuoLegendCardNo, "OGN·259/298", "OGN·305*/298", "OGN·305/298"],
                "亚索传奇移动技能",
                YasuoLegendManaCost,
                0,
                YasuoLegendManaCostToken,
                1,
                RequiresFriendlyUnitTarget: true,
                LegendAbilityEffectKinds.MoveFriendlyUnit),
            LeeSinLegendAbilityId => new LegendAbilityDefinition(
                LeeSinLegendAbilityId,
                [LeeSinLegendCardNo, "OGN·304*/298", "OGN·304/298"],
                "李青传奇增益技能",
                LeeSinLegendManaCost,
                0,
                LeeSinLegendManaCostToken,
                1,
                RequiresFriendlyUnitTarget: true,
                LegendAbilityEffectKinds.GrantBoon),
            PoppyLegendAbilityId => new LegendAbilityDefinition(
                PoppyLegendAbilityId,
                ["UNL-203/219", "UNL-237*/219", PoppyLegendCardNo],
                "波比传奇抽牌技能",
                0,
                PoppyLegendExperienceCost,
                PoppyLegendExperienceCostToken,
                0,
                RequiresFriendlyUnitTarget: false,
                LegendAbilityEffectKinds.DrawOne),
            ViktorLegendAbilityId => new LegendAbilityDefinition(
                ViktorLegendAbilityId,
                [ViktorLegendCardNo, "OGN·265/298", "OGN·308*/298", "OGN·308/298"],
                "维克托传奇随从技能",
                ViktorLegendManaCost,
                0,
                ViktorLegendManaCostToken,
                0,
                RequiresFriendlyUnitTarget: false,
                LegendAbilityEffectKinds.CreateMinion),
            MissFortuneLegendAbilityId => new LegendAbilityDefinition(
                MissFortuneLegendAbilityId,
                [MissFortuneLegendCardNo, "OGN·309/298", "OGN·309*/298"],
                "赏金猎人传奇游走技能",
                0,
                0,
                string.Empty,
                1,
                RequiresFriendlyUnitTarget: true,
                LegendAbilityEffectKinds.GrantRoam),
            KhazixLegendBoonAbilityId => new LegendAbilityDefinition(
                KhazixLegendBoonAbilityId,
                [KhazixLegendCardNo, "UNL-236/219", "UNL-236*/219"],
                "卡兹克传奇增益技能",
                0,
                KhazixLegendBoonExperienceCost,
                KhazixLegendBoonExperienceCostToken,
                1,
                RequiresFriendlyUnitTarget: true,
                LegendAbilityEffectKinds.GrantBoon),
            KhazixLegendMoveAbilityId => new LegendAbilityDefinition(
                KhazixLegendMoveAbilityId,
                [KhazixLegendCardNo, "UNL-236/219", "UNL-236*/219"],
                "卡兹克传奇休眠单位移动技能",
                0,
                KhazixLegendMoveExperienceCost,
                KhazixLegendMoveExperienceCostToken,
                1,
                RequiresFriendlyUnitTarget: true,
                LegendAbilityEffectKinds.MoveFriendlyUnit,
                RequiresBattlefieldTarget: true,
                RequiresExhaustedTarget: true),
            PykeLegendAbilityId => new LegendAbilityDefinition(
                PykeLegendAbilityId,
                [PykeLegendCardNo, "UNL-228/219", "UNL-228*/219"],
                "派克传奇召回金币技能",
                PykeLegendManaCost,
                0,
                PykeLegendManaCostToken,
                1,
                RequiresFriendlyUnitTarget: true,
                LegendAbilityEffectKinds.ReturnBattlefieldUnitAndCreateCoin,
                RequiresBattlefieldTarget: true),
            JaxLegendAttachAbilityId => new LegendAbilityDefinition(
                JaxLegendAttachAbilityId,
                [JaxSpiritforgedLegendCardNo, "SFD·245/221"],
                "武器大师传奇贴附技能",
                JaxLegendAttachManaCost,
                0,
                JaxLegendAttachManaCostToken,
                2,
                RequiresFriendlyUnitTarget: true,
                LegendAbilityEffectKinds.AttachArmament,
                RequiresArmamentSecondTarget: true,
                RequiresUnattachedArmamentSecondTarget: true),
            JaxLegendReattachAbilityId => new LegendAbilityDefinition(
                JaxLegendReattachAbilityId,
                [JaxSpiritforgedLegendCardNo, "SFD·245/221"],
                "武器大师传奇重贴附技能",
                0,
                0,
                string.Empty,
                2,
                RequiresFriendlyUnitTarget: true,
                LegendAbilityEffectKinds.ReattachArmament,
                RequiresArmamentSecondTarget: true,
                RequiresAttachedArmamentSecondTarget: true,
                RequiresDifferentArmamentHost: true),
            BattlefieldGrantedLegendAttachArmamentAbilityId => new LegendAbilityDefinition(
                BattlefieldGrantedLegendAttachArmamentAbilityId,
                [],
                "魄罗熔炉传奇贴附技能",
                0,
                0,
                string.Empty,
                2,
                RequiresFriendlyUnitTarget: true,
                LegendAbilityEffectKinds.AttachArmament,
                RequiresArmamentSecondTarget: true,
                RequiredControlledBattlefieldCardNo: BattlefieldGrantLegendAttachArmamentCardNo),
            DariusLegendAbilityId => new LegendAbilityDefinition(
                DariusLegendAbilityId,
                [DariusOriginLegendCardNo, "OGN·302/298", "OGN·302*/298"],
                "诺克萨斯之手传奇鼓舞技能",
                0,
                0,
                string.Empty,
                0,
                RequiresFriendlyUnitTarget: false,
                LegendAbilityEffectKinds.GainMana,
                RequiresPlayedAnotherCardThisTurn: true,
                ManaGainAmount: DariusLegendManaGain),
            DianaLegendAbilityId => new LegendAbilityDefinition(
                DianaLegendAbilityId,
                ["UNL-197/219", "UNL-234/219", "UNL-234*/219"],
                "皎月女神传奇法术对决法力技能",
                0,
                0,
                string.Empty,
                0,
                RequiresFriendlyUnitTarget: false,
                LegendAbilityEffectKinds.GainMana,
                ManaGainAmount: DianaLegendManaGain,
                TimingKind: LegendAbilityTimingKinds.SpellDuelFocus),
            KaisaLegendAbilityId => new LegendAbilityDefinition(
                KaisaLegendAbilityId,
                ["OGN·247/298", "OGN·299/298", "OGN·299*/298"],
                "虚空之女传奇法术反应符能技能",
                0,
                0,
                string.Empty,
                0,
                RequiresFriendlyUnitTarget: false,
                LegendAbilityEffectKinds.GainPower,
                PowerGainAmount: KaisaLegendPowerGain,
                TimingKind: LegendAbilityTimingKinds.PriorityWindow,
                RequiresPendingSpellStackItem: true),
            OrnnLegendAbilityId => new LegendAbilityDefinition(
                OrnnLegendAbilityId,
                ["SFD·189/221", "SFD·244/221"],
                "山隐之焰传奇装备反应符能技能",
                0,
                0,
                string.Empty,
                0,
                RequiresFriendlyUnitTarget: false,
                LegendAbilityEffectKinds.GainPower,
                PowerGainAmount: OrnnLegendPowerGain,
                TimingKind: LegendAbilityTimingKinds.PriorityWindow,
                RequiresPendingEquipmentStackItem: true),
            EzrealLegendAbilityId => new LegendAbilityDefinition(
                EzrealLegendAbilityId,
                ["SFD·199/221", "SFD·248/221"],
                "探险家传奇反应抽牌技能",
                0,
                0,
                string.Empty,
                0,
                RequiresFriendlyUnitTarget: false,
                LegendAbilityEffectKinds.DrawOne,
                TimingKind: LegendAbilityTimingKinds.PriorityWindow,
                RequiresEzrealEnemyTargetsThisTurn: true),
            IreliaLegendAbilityId => new LegendAbilityDefinition(
                IreliaLegendAbilityId,
                [IreliaLegendCardNo, "SFD·195a/221·P", "SFD·246/221"],
                "刀锋舞者传奇反应重置技能",
                IreliaLegendManaCost,
                0,
                IreliaLegendManaCostToken,
                1,
                RequiresFriendlyUnitTarget: true,
                LegendAbilityEffectKinds.ReadyFriendlyUnit,
                TimingKind: LegendAbilityTimingKinds.PriorityWindow,
                RequiresPendingFriendlyUnitTarget: true),
            TeemoLegendAbilityId => new LegendAbilityDefinition(
                TeemoLegendAbilityId,
                [TeemoOriginLegendCardNo, "OGN·263a/298", "OGN·307/298", "OGN·307*/298"],
                "迅捷斥候传奇召回技能",
                TeemoLegendManaCost,
                0,
                TeemoLegendManaCostToken,
                1,
                RequiresFriendlyUnitTarget: false,
                LegendAbilityEffectKinds.ReturnOwnedTeemoUnitToHand,
                RequiresOwnedTeemoUnitTarget: true),
            AzirLegendAbilityId => new LegendAbilityDefinition(
                AzirLegendAbilityId,
                [AzirSpiritforgedLegendCardNo, "SFD·247/221"],
                "沙漠皇帝传奇沙兵技能",
                AzirLegendManaCost,
                0,
                AzirLegendManaCostToken,
                0,
                RequiresFriendlyUnitTarget: false,
                LegendAbilityEffectKinds.CreateSandSoldier,
                RequiresPlayedArmamentThisTurn: true),
            LilliaLegendAbilityId => new LegendAbilityDefinition(
                LilliaLegendAbilityId,
                [LilliaLegendCardNo, "UNL-230/219", "UNL-230*/219"],
                "莉莉娅传奇精灵技能",
                LilliaLegendBaseManaCost,
                0,
                string.Empty,
                0,
                RequiresFriendlyUnitTarget: false,
                LegendAbilityEffectKinds.CreateFaerie,
                ManaCostReductionKind: LegendAbilityManaCostReductionKinds.FriendlyEphemeralFieldObjects),
            _ => default!
        };

        return ability is not null;
    }

    private static int ResolveLegendAbilityManaCost(
        MatchState state,
        string playerId,
        LegendAbilityDefinition ability)
    {
        var reduction = ability.ManaCostReductionKind switch
        {
            LegendAbilityManaCostReductionKinds.FriendlyEphemeralFieldObjects =>
                CountFriendlyEphemeralFieldObjects(state, playerId),
            _ => 0
        };
        return Math.Max(0, ability.ManaCost - reduction);
    }

    private static int CountFriendlyEphemeralFieldObjects(MatchState state, string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return 0;
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Count(objectId => state.CardObjects.TryGetValue(objectId, out var objectState)
                && IsControlledFieldObject(state, playerId, objectId)
                && SourceObjectControlledByPlayerOrLegacyOwned(objectState, playerId)
                && objectState.Tags.Contains(CardObjectTags.Ephemeral, StringComparer.Ordinal));
    }

    private static bool IsValidLegendAbilityTarget(
        MatchState state,
        string playerId,
        string targetObjectId,
        LegendAbilityDefinition ability)
    {
        if (!IsControlledFieldObject(state, playerId, targetObjectId)
            || !CardObjectHasTag(state.CardObjects, targetObjectId, CardObjectTags.UnitCard))
        {
            return false;
        }

        if (!state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            || string.IsNullOrWhiteSpace(targetState.CardNo)
            || !SourceObjectControlledByPlayerOrLegacyOwned(targetState, playerId))
        {
            return false;
        }

        var location = FindFieldObjectLocation(state.PlayerZones, targetObjectId);
        if (ability.RequiresBattlefieldTarget
            && (location is null || !string.Equals(location.Value.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)))
        {
            return false;
        }

        return !ability.RequiresExhaustedTarget
            || targetState.IsExhausted;
    }

    private static bool IsPendingFriendlyUnitTarget(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return IsControlledFieldObject(state, playerId, targetObjectId)
            && CardObjectHasTag(state.CardObjects, targetObjectId, CardObjectTags.UnitCard)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && !string.IsNullOrWhiteSpace(targetState.CardNo)
            && SourceObjectControlledByPlayerOrLegacyOwned(targetState, playerId)
            && state.StackItems.Any(stackItem =>
                string.Equals(stackItem.ControllerId, playerId, StringComparison.Ordinal)
                && stackItem.TargetObjectIds.Contains(targetObjectId, StringComparer.Ordinal));
    }

    private static bool IsValidLegendArmamentSecondTarget(
        MatchState state,
        string playerId,
        string unitObjectId,
        string equipmentObjectId,
        LegendAbilityDefinition ability)
    {
        if (string.Equals(unitObjectId, equipmentObjectId, StringComparison.Ordinal)
            || !IsControlledFieldObject(state, playerId, equipmentObjectId)
            || !state.CardObjects.TryGetValue(equipmentObjectId, out var equipmentState)
            || string.IsNullOrWhiteSpace(equipmentState.CardNo)
            || !SourceObjectControlledByPlayerOrLegacyOwned(equipmentState, playerId)
            || equipmentState.IsFaceDown
            || !equipmentState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            || !equipmentState.Tags.Contains("武装", StringComparer.Ordinal))
        {
            return false;
        }

        var isAttached = !string.IsNullOrWhiteSpace(equipmentState.AttachedToObjectId);
        if (ability.RequiresUnattachedArmamentSecondTarget && isAttached)
        {
            return false;
        }

        if (ability.RequiresAttachedArmamentSecondTarget && !isAttached)
        {
            return false;
        }

        return !ability.RequiresDifferentArmamentHost
            || !string.Equals(equipmentState.AttachedToObjectId, unitObjectId, StringComparison.Ordinal);
    }

    private static bool IsValidOwnedTeemoUnitTarget(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && (zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                || zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                || zones.ChampionZone.Contains(targetObjectId, StringComparer.Ordinal))
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && !targetState.IsFaceDown
            && targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && string.Equals(targetState.OwnerId, playerId, StringComparison.Ordinal)
            && IsTeemoUnitCardNo(targetState.CardNo);
    }

    private static bool IsTeemoUnitCardNo(string? cardNo)
    {
        return cardNo is "FND-196/298"
            or "OGN·121/298"
            or "OGN·121a/298"
            or "OGN·197/298"
            or "OGN·197a/298"
            or "OGN·197b/298"
            or "SFD·230/221"
            or "SFD·230*/221";
    }

    private static bool IsAzirLegendCardNo(string? cardNo)
    {
        return cardNo is AzirSpiritforgedLegendCardNo or "SFD·247/221";
    }

    private static bool IsRengarLegendCardNo(string? cardNo)
    {
        return cardNo is RengarLegendCardNo or "UNL-227/219" or "UNL-227*/219";
    }

    private static bool IsLeonaLegendCardNo(string? cardNo)
    {
        return cardNo is LeonaOriginLegendCardNo or "OGN·306/298" or "OGN·306*/298";
    }

    private static bool IsSivirLegendCardNo(string? cardNo)
    {
        return cardNo is SivirSpiritforgedLegendCardNo or "SFD·250/221";
    }

    private static bool IsJhinLegendCardNo(string? cardNo)
    {
        return cardNo is JhinLegendCardNo or "UNL-226/219" or "UNL-226*/219";
    }

    private static bool ControllerHasAzirLegend(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        return playerZones.TryGetValue(playerId, out var zones)
            && zones.LegendZone.Any(objectId =>
                cardObjects.TryGetValue(objectId, out var legendState)
                && SourceObjectControlledByPlayerOrLegacyOwned(legendState, playerId)
                && IsAzirLegendCardNo(legendState.CardNo));
    }

    private static bool ControllerHasRengarLegend(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.LegendZone.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var legendState)
                && SourceObjectControlledByPlayerOrLegacyOwned(legendState, playerId)
                && IsRengarLegendCardNo(legendState.CardNo));
    }

    private static bool ControllerHasLeonaLegend(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.LegendZone.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var legendState)
                && SourceObjectControlledByPlayerOrLegacyOwned(legendState, playerId)
                && IsLeonaLegendCardNo(legendState.CardNo));
    }

    private static bool ControllerHasJhinLegend(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        return playerZones.TryGetValue(playerId, out var zones)
            && zones.LegendZone.Any(objectId =>
                cardObjects.TryGetValue(objectId, out var legendState)
                && SourceObjectControlledByPlayerOrLegacyOwned(legendState, playerId)
                && IsJhinLegendCardNo(legendState.CardNo));
    }

    private static bool TryGetRengarLegend(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out string legendCardNo)
    {
        legendObjectId = string.Empty;
        legendCardNo = RengarLegendCardNo;
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var legendState)
                || !SourceObjectControlledByPlayerOrLegacyOwned(legendState, playerId)
                || !IsRengarLegendCardNo(legendState.CardNo))
            {
                continue;
            }

            legendObjectId = objectId;
            legendCardNo = string.IsNullOrWhiteSpace(legendState.CardNo) ? RengarLegendCardNo : legendState.CardNo;
            return true;
        }

        return false;
    }

    private static bool TryGetLeonaLegend(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out string legendCardNo)
    {
        legendObjectId = string.Empty;
        legendCardNo = LeonaOriginLegendCardNo;
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var legendState)
                || !SourceObjectControlledByPlayerOrLegacyOwned(legendState, playerId)
                || !IsLeonaLegendCardNo(legendState.CardNo))
            {
                continue;
            }

            legendObjectId = objectId;
            legendCardNo = string.IsNullOrWhiteSpace(legendState.CardNo) ? LeonaOriginLegendCardNo : legendState.CardNo;
            return true;
        }

        return false;
    }

    private static bool TryGetSivirLegend(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out CardObjectState legendState)
    {
        legendObjectId = string.Empty;
        legendState = default!;
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !SourceObjectControlledByPlayerOrLegacyOwned(candidate, playerId)
                || !IsSivirLegendCardNo(candidate.CardNo))
            {
                continue;
            }

            legendObjectId = objectId;
            legendState = candidate;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> ApplyAzirSandSoldierTemperedTags(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        IReadOnlyList<string> tokenTags)
    {
        if (!tokenTags.Contains(CardObjectTags.SandSoldier, StringComparer.Ordinal)
            || !ControllerHasAzirLegend(playerZones, cardObjects, playerId))
        {
            return tokenTags;
        }

        return tokenTags
            .Concat([CardEquipmentKeywordNames.Tempered])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
    }

    private static void MoveLegendTargetBetweenBaseAndBattlefield(
        Dictionary<string, PlayerZones> playerZones,
        string targetObjectId,
        string playerId,
        string sourceObjectId,
        string abilityId,
        List<GameEvent> events)
    {
        var location = FindFieldObjectLocation(playerZones, targetObjectId);
        if (location is null)
        {
            return;
        }

        var destinationZone = string.Equals(location.Value.Zone, MoveUnitBaseZone, StringComparison.Ordinal)
            ? MoveUnitBattlefieldZone
            : MoveUnitBaseZone;
        RemoveFieldObjectFromLocation(
            playerZones,
            location.Value.PlayerId,
            location.Value.Zone,
            targetObjectId);
        AddFieldObjectToLocation(playerZones, playerId, destinationZone, targetObjectId);
        events.Add(new GameEvent(
            string.Equals(destinationZone, MoveUnitBaseZone, StringComparison.Ordinal)
                ? "UNIT_MOVED_TO_BASE"
                : "UNIT_MOVED_TO_BATTLEFIELD",
            $"{targetObjectId} 被传奇技能移动",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["abilityId"] = abilityId,
                ["targetObjectId"] = targetObjectId,
                ["originZone"] = location.Value.Zone,
                ["destinationZone"] = destinationZone
            }));
    }

    private static void GrantLegendBoon(
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        string playerId,
        string sourceObjectId,
        string abilityId,
        List<GameEvent> events)
    {
        if (!cardObjects.TryGetValue(targetObjectId, out var targetState))
        {
            return;
        }

        var alreadyHadBoon = targetState.Tags.Contains(CardObjectTags.Boon, StringComparer.Ordinal);
        var nextTags = targetState.Tags
            .Concat([CardObjectTags.Boon])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
        cardObjects[targetObjectId] = targetState with
        {
            Power = alreadyHadBoon ? targetState.Power : targetState.Power + 1,
            Tags = nextTags
        };
        events.Add(new GameEvent(
            "BOON_GRANTED",
            $"{targetObjectId} 获得增益",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["abilityId"] = abilityId,
                ["targetObjectId"] = targetObjectId,
                ["alreadyHadBoon"] = alreadyHadBoon,
                ["power"] = alreadyHadBoon ? targetState.Power : targetState.Power + 1
            }));
    }

    private static void GrantLegendRoam(
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        string playerId,
        string sourceObjectId,
        string abilityId,
        List<GameEvent> events)
    {
        if (!cardObjects.TryGetValue(targetObjectId, out var targetState))
        {
            return;
        }

        cardObjects[targetObjectId] = targetState with
        {
            UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(targetState.UntilEndOfTurnEffects, MoveUnitRoamOptionalCost)
        };
        events.Add(new GameEvent(
            "ROAM_GRANTED",
            $"{targetObjectId} 本回合获得游走",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["abilityId"] = abilityId,
                ["targetObjectId"] = targetObjectId,
                ["effectId"] = MoveUnitRoamOptionalCost
            }));
    }

    private static Dictionary<string, RunePool> GainLegendMana(
        IReadOnlyDictionary<string, RunePool> currentRunePools,
        string playerId,
        int manaAmount,
        string sourceObjectId,
        string abilityId,
        List<GameEvent> events)
    {
        var runePools = currentRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var currentPool = runePools.TryGetValue(playerId, out var pool) ? pool : RunePool.Empty;
        var nextPool = currentPool with
        {
            Mana = currentPool.Mana + manaAmount
        };
        runePools[playerId] = nextPool;
        events.Add(new GameEvent(
            "MANA_GAINED",
            $"{playerId} 通过传奇技能获得 {manaAmount} 点法力",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["abilityId"] = abilityId,
                ["mana"] = manaAmount,
                ["manaAfter"] = nextPool.Mana
            }));
        return runePools;
    }

    private static Dictionary<string, RunePool> GainLegendPower(
        IReadOnlyDictionary<string, RunePool> currentRunePools,
        string playerId,
        int powerAmount,
        string sourceObjectId,
        string abilityId,
        List<GameEvent> events)
    {
        var runePools = currentRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var currentPool = runePools.TryGetValue(playerId, out var pool) ? pool : RunePool.Empty;
        var nextPool = currentPool with
        {
            Power = currentPool.Power + powerAmount
        };
        runePools[playerId] = nextPool;
        events.Add(new GameEvent(
            "POWER_GAINED",
            $"{playerId} 通过传奇技能获得 {powerAmount} 点符能",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["abilityId"] = abilityId,
                ["power"] = powerAmount,
                ["powerAfter"] = nextPool.Power
            }));
        return runePools;
    }

    private static void ReturnLegendTargetToOwnerHand(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        string playerId,
        string sourceObjectId,
        string abilityId,
        List<GameEvent> events)
    {
        var location = FindFieldObjectLocation(playerZones, targetObjectId);
        if (location is null
            || !cardObjects.TryGetValue(targetObjectId, out var targetState))
        {
            return;
        }

        var ownerPlayerId = !string.IsNullOrWhiteSpace(targetState.OwnerId)
            && playerZones.ContainsKey(targetState.OwnerId)
            ? targetState.OwnerId
            : playerId;
        RemoveFieldObjectFromLocation(playerZones, location.Value.PlayerId, location.Value.Zone, targetObjectId);
        var ownerZones = playerZones[ownerPlayerId];
        playerZones[ownerPlayerId] = ownerZones with
        {
            Hand = ownerZones.Hand.Contains(targetObjectId, StringComparer.Ordinal)
                ? ownerZones.Hand
                : ownerZones.Hand.Concat([targetObjectId]).ToArray()
        };
        cardObjects[targetObjectId] = targetState with
        {
            ControllerId = ownerPlayerId,
            IsAttacking = false,
            IsDefending = false
        };
        events.Add(new GameEvent(
            "UNIT_RETURNED_TO_HAND",
            $"{targetObjectId} 被传奇技能返回手牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["abilityId"] = abilityId,
                ["targetObjectId"] = targetObjectId,
                ["originZone"] = location.Value.Zone,
                ["ownerPlayerId"] = ownerPlayerId,
                ["destinationZone"] = "HAND"
            }));
    }

    private static void ReturnOwnedTeemoUnitToHand(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        string playerId,
        string sourceObjectId,
        string abilityId,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(playerId, out var zones)
            || !cardObjects.TryGetValue(targetObjectId, out var targetState))
        {
            return;
        }

        var originZone = zones.ChampionZone.Contains(targetObjectId, StringComparer.Ordinal)
            ? "CHAMPION"
            : zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                ? MoveUnitBattlefieldZone
                : MoveUnitBaseZone;
        playerZones[playerId] = zones with
        {
            Base = RemoveFromZone(zones.Base, targetObjectId),
            Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId),
            ChampionZone = RemoveFromZone(zones.ChampionZone, targetObjectId),
            Hand = zones.Hand.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Hand
                : zones.Hand.Concat([targetObjectId]).ToArray()
        };
        cardObjects[targetObjectId] = targetState with
        {
            ControllerId = playerId,
            IsAttacking = false,
            IsDefending = false,
            IsExhausted = false
        };
        events.Add(new GameEvent(
            "UNIT_RETURNED_TO_HAND",
            $"{targetObjectId} 被提莫传奇技能返回手牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["abilityId"] = abilityId,
                ["targetObjectId"] = targetObjectId,
                ["originZone"] = originZone,
                ["ownerPlayerId"] = playerId,
                ["destinationZone"] = "HAND"
            }));
    }

    private static void AttachOrReattachLegendArmament(
        Dictionary<string, CardObjectState> cardObjects,
        string unitObjectId,
        string equipmentObjectId,
        string playerId,
        string sourceObjectId,
        string abilityId,
        bool isReattach,
        List<GameEvent> events)
    {
        if (!cardObjects.TryGetValue(equipmentObjectId, out var equipmentState))
        {
            return;
        }

        var previousAttachedToObjectId = equipmentState.AttachedToObjectId;
        var isActuallyReattach = isReattach || !string.IsNullOrWhiteSpace(previousAttachedToObjectId);
        cardObjects[equipmentObjectId] = equipmentState with
        {
            AttachedToObjectId = unitObjectId
        };
        events.Add(new GameEvent(
            isActuallyReattach ? "EQUIPMENT_REATTACHED" : "EQUIPMENT_ATTACHED",
            isActuallyReattach ? $"{sourceObjectId} 重贴附武装" : $"{sourceObjectId} 贴附武装",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["abilityId"] = abilityId,
                ["unitObjectId"] = unitObjectId,
                ["equipmentObjectId"] = equipmentObjectId,
                ["controllerId"] = playerId,
                ["ownerId"] = string.IsNullOrWhiteSpace(equipmentState.OwnerId) ? playerId : equipmentState.OwnerId,
                ["attachedToObjectId"] = unitObjectId,
                ["previousAttachedToObjectId"] = previousAttachedToObjectId
            }));
    }

    private static void AddBattlefieldGrantedLegendAbilityEventIfNeeded(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string sourceObjectId,
        string unitObjectId,
        string equipmentObjectId,
        LegendAbilityDefinition ability,
        List<GameEvent> events)
    {
        if (string.IsNullOrWhiteSpace(ability.RequiredControlledBattlefieldCardNo)
            || !TryGetControlledBattlefieldCardObject(
                playerZones,
                cardObjects,
                playerId,
                ability.RequiredControlledBattlefieldCardNo,
                out var battlefieldObjectId,
                out var battlefieldState))
        {
            return;
        }

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 通过魄罗熔炉贴附武装",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_CONTROLLED_LEGEND_ATTACH_ARMAMENT",
                ["sourceObjectId"] = sourceObjectId,
                ["unitObjectId"] = unitObjectId,
                ["equipmentObjectId"] = equipmentObjectId,
                ["abilityId"] = ability.AbilityId
            }));
    }

    private static void CreateLegendEquipmentToken(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string sourceObjectId,
        string abilityId,
        string tokenName,
        IReadOnlyList<string> tokenTags,
        bool isExhausted,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return;
        }

        var tokenObjectId = NextTokenObjectId(playerZones, cardObjects, sourceObjectId, 1);
        cardObjects[tokenObjectId] = new CardObjectState(
            tokenObjectId,
            isExhausted: isExhausted,
            tags: tokenTags,
            ownerId: playerId,
            controllerId: playerId);
        playerZones[playerId] = zones with
        {
            Base = zones.Base.Concat([tokenObjectId]).ToArray()
        };
        events.Add(new GameEvent(
            "EQUIPMENT_TOKEN_CREATED",
            $"{sourceObjectId} 打出装备指示物",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["abilityId"] = abilityId,
                ["tokenObjectId"] = tokenObjectId,
                ["tokenName"] = tokenName,
                ["destinationZone"] = "BASE",
                ["isExhausted"] = isExhausted,
                ["tokenTags"] = tokenTags
            }));
    }

    private static void CreateLegendMinion(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string sourceObjectId,
        string abilityId,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return;
        }

        var tokenObjectId = NextTokenObjectId(playerZones, cardObjects, sourceObjectId, 1);
        cardObjects[tokenObjectId] = new CardObjectState(
            tokenObjectId,
            power: 1,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
        playerZones[playerId] = zones with
        {
            Base = zones.Base.Concat([tokenObjectId]).ToArray()
        };
        events.Add(new GameEvent(
            "UNIT_TOKEN_CREATED",
            $"{sourceObjectId} 打出随从",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["abilityId"] = abilityId,
                ["tokenObjectId"] = tokenObjectId,
                ["tokenName"] = "随从",
                ["power"] = 1,
                ["destinationZone"] = "BASE",
                ["tokenTags"] = new[] { CardObjectTags.UnitCard }
            }));
    }

    private static void CreateLegendFaerie(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string sourceObjectId,
        string abilityId,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return;
        }

        var tokenObjectId = NextTokenObjectId(playerZones, cardObjects, sourceObjectId, 1);
        var tokenState = P6TokenFactoryCatalog.TryGetByCardNo(FaerieTokenCardNo, out var definition)
            ? definition.CreateObject(tokenObjectId, playerId, playerId)
            : new CardObjectState(
                tokenObjectId,
                power: 3,
                tags: [CardObjectTags.UnitCard, CardObjectTags.Ephemeral, "仙灵"],
                cardNo: FaerieTokenCardNo,
                ownerId: playerId,
                controllerId: playerId);
        cardObjects[tokenObjectId] = tokenState;
        playerZones[playerId] = zones with
        {
            Base = zones.Base.Concat([tokenObjectId]).ToArray()
        };
        events.Add(new GameEvent(
            "UNIT_TOKEN_CREATED",
            $"{sourceObjectId} 打出精灵",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["abilityId"] = abilityId,
                ["tokenObjectId"] = tokenObjectId,
                ["tokenCardNo"] = tokenState.CardNo,
                ["tokenName"] = "精灵",
                ["power"] = tokenState.Power,
                ["destinationZone"] = "BASE",
                ["tokenTags"] = tokenState.Tags.ToArray()
            }));
    }

    private static void CreateLegendSandSoldier(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string sourceObjectId,
        string abilityId,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return;
        }

        var tokenObjectId = NextTokenObjectId(playerZones, cardObjects, sourceObjectId, 1);
        var tokenState = P6TokenFactoryCatalog.TryGetByCardNo(SandSoldierTokenCardNo, out var definition)
            ? definition.CreateObject(tokenObjectId, playerId, playerId)
            : new CardObjectState(
                tokenObjectId,
                power: 2,
                tags: [CardObjectTags.UnitCard, CardObjectTags.SandSoldier],
                cardNo: SandSoldierTokenCardNo,
                ownerId: playerId,
                controllerId: playerId);
        var tokenTags = ApplyAzirSandSoldierTemperedTags(
            playerZones,
            cardObjects,
            playerId,
            tokenState.Tags);
        tokenState = tokenState with
        {
            Tags = tokenTags
        };

        cardObjects[tokenObjectId] = tokenState;
        playerZones[playerId] = zones with
        {
            Base = zones.Base.Concat([tokenObjectId]).ToArray()
        };
        events.Add(new GameEvent(
            "UNIT_TOKEN_CREATED",
            $"{sourceObjectId} 打出黄沙士兵",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["abilityId"] = abilityId,
                ["tokenObjectId"] = tokenObjectId,
                ["tokenCardNo"] = tokenState.CardNo,
                ["tokenName"] = "黄沙士兵",
                ["power"] = tokenState.Power,
                ["destinationZone"] = "BASE",
                ["tokenTags"] = tokenState.Tags.ToArray(),
                ["azirTempered"] = tokenState.Tags.Contains(CardEquipmentKeywordNames.Tempered, StringComparer.Ordinal)
            }));
    }

    private static ResolutionResult ResolveXerathDamageAbility(
        MatchState state,
        PlayerIntent intent,
        ActivateAbilityCommand command,
        P4ActivatedAbilityDefinition ability)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.ActivePlayerId, intent.PlayerId, StringComparison.Ordinal)
            || state.StackItems.Count > 0)
        {
            return RejectWithCorePrompts(
                state,
                "ACTIVATE_ABILITY is only available during the active player's open main window.",
                ErrorCodes.PhaseNotAllowed);
        }

        if (NormalizeOptionalCosts(command.OptionalCosts).Count != 0)
        {
            return RejectWithCorePrompts(
                state,
                "Xerath's P4 damage ability does not accept optional costs.",
                ErrorCodes.InvalidTarget);
        }

        if (command.TargetObjectIds.Count != ability.RequiredTargetCount)
        {
            return RejectWithCorePrompts(
                state,
                "Xerath's P4 damage ability requires exactly one unit target.",
                ErrorCodes.InvalidTarget);
        }

        if (!IsControlledBattlefieldObject(state, intent.PlayerId, command.SourceObjectId)
            || !state.CardObjects.TryGetValue(command.SourceObjectId, out var sourceState)
            || !sourceState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Xerath's P4 damage ability source must be a controlled battlefield unit.",
                ErrorCodes.InvalidTarget);
        }

        if (!SourceObjectControlledByPlayerOrLegacyOwned(sourceState, intent.PlayerId))
        {
            return RejectWithCorePrompts(
                state,
                "ACTIVATE_ABILITY source must be controlled by the acting player.",
                ErrorCodes.InvalidTarget);
        }

        if (!string.Equals(sourceState.CardNo, ability.SourceCardNo, StringComparison.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Ability source does not expose Xerath's P4 damage ability.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (sourceState.IsExhausted)
        {
            return RejectWithCorePrompts(
                state,
                "Xerath's P4 damage ability source must be active.",
                ErrorCodes.InvalidTarget);
        }

        var targetObjectId = command.TargetObjectIds[0];
        if (!IsFieldObject(state.PlayerZones, targetObjectId)
            || !state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            || string.IsNullOrWhiteSpace(targetState.CardNo)
            || !targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Xerath's P4 damage ability target must be a field unit.",
                ErrorCodes.InvalidTarget);
        }

        IReadOnlyList<string> spellshieldTaxTargetObjectIds = [];
        var spellshieldTaxMana = 0;
        if (ability.AppliesSpellshieldTargetTax)
        {
            spellshieldTaxMana = ResolveSpellshieldTargetTaxMana(
                state,
                intent.PlayerId,
                command.TargetObjectIds,
                out spellshieldTaxTargetObjectIds);
        }
        var currentPool = state.RunePools.TryGetValue(intent.PlayerId, out var runePool) ? runePool : RunePool.Empty;
        if (!CanPayRuneCosts(currentPool, spellshieldTaxMana, ability.PowerCost))
        {
            return RejectWithCorePrompts(
                state,
                "Not enough resources to activate Xerath's damage ability.",
                ErrorCodes.InsufficientCost);
        }

        var runePools = PayRuneCosts(
            state,
            intent.PlayerId,
            spellshieldTaxMana,
            ability.PowerCost);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[command.SourceObjectId] = sourceState with
        {
            IsExhausted = true
        };
        var stackItem = new StackItemState(
            $"STACK-{state.Tick + 1}-{command.SourceObjectId}-ABILITY",
            intent.PlayerId,
            command.SourceObjectId,
            ability.EffectKind,
            ability.SourceCardNo,
            command.TargetObjectIds,
            ability.DamageAmount,
            1,
            []);
        var untilEndOfTurnEffects = MarkEzrealEnemyTargetsThisTurnForUnitAbility(
            state,
            state.UntilEndOfTurnEffects,
            intent.PlayerId,
            command.TargetObjectIds);
        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = intent.PlayerId,
            TimingState = TimingStates.NeutralClosed,
            RunePools = runePools,
            CardObjects = cardObjects,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, state.PlayerZones),
            UntilEndOfTurnEffects = untilEndOfTurnEffects,
            PriorityPlayerId = intent.PlayerId,
            PassedPriorityPlayerIds = [],
            StackItems = state.StackItems.Concat([stackItem]).ToArray()
        };
        var events = new List<GameEvent>
        {
            new(
                "ABILITY_ACTIVATED",
                $"{intent.PlayerId} 激活泽拉斯的技能",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["abilityId"] = command.AbilityId,
                    ["effectKind"] = ability.EffectKind,
                    ["targetObjectIds"] = command.TargetObjectIds.ToArray()
                }),
            new(
                "COST_PAID",
                $"{intent.PlayerId} 支付泽拉斯技能费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["mana"] = spellshieldTaxMana,
                    ["power"] = ability.PowerCost,
                    ["abilityId"] = command.AbilityId,
                    ["spellshieldTaxMana"] = spellshieldTaxMana,
                    ["spellshieldTaxTargetObjectIds"] = spellshieldTaxTargetObjectIds.ToArray()
                }),
            new(
                "UNIT_EXHAUSTED",
                "泽拉斯横置支付技能费用",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["targetObjectId"] = command.SourceObjectId,
                    ["wasExhausted"] = sourceState.IsExhausted,
                    ["isExhausted"] = true
                }),
            new(
                "STACK_ITEM_ADDED",
                "泽拉斯的技能加入结算链",
                new Dictionary<string, object?>
                {
                    ["stackItemId"] = stackItem.StackItemId,
                    ["controllerId"] = stackItem.ControllerId,
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["cardNo"] = stackItem.CardNo,
                    ["targetObjectIds"] = stackItem.TargetObjectIds.ToArray(),
                    ["effectKind"] = stackItem.EffectKind,
                    ["abilityId"] = command.AbilityId,
                    ["damageAmount"] = stackItem.DamageAmount
                })
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static ResolutionResult ResolveHideCard(
        MatchState state,
        PlayerIntent intent,
        HideCardCommand command)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.ActivePlayerId, intent.PlayerId, StringComparison.Ordinal)
            || state.StackItems.Count > 0)
        {
            return RejectWithCorePrompts(
                state,
                "HIDE_CARD is only available during the active player's open main window.",
                ErrorCodes.PhaseNotAllowed);
        }

        if (!CardBehaviorRegistry.TryGetByCardNo(command.CardNo, out var behavior))
        {
            return RejectWithCorePrompts(
                state,
                $"Unsupported standby card behavior: {command.CardNo}",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (!state.PlayerZones.TryGetValue(intent.PlayerId, out var zones)
            || !zones.Hand.Contains(command.SourceObjectId, StringComparer.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Source card is not in the player's hand.",
                ErrorCodes.CardNotInHand);
        }

        var destination = string.IsNullOrWhiteSpace(command.Destination)
            ? StandbyHideDestination
            : command.Destination.Trim();
        var optionalCosts = NormalizeOptionalCosts(command.OptionalCosts);
        var usesStandardStandbyHideCost = optionalCosts.Count == 1
            && string.Equals(optionalCosts[0], StandbyHideOptionalCost, StringComparison.Ordinal);
        var usesTeemoStandbyHideCost = optionalCosts.Count == 1
            && string.Equals(optionalCosts[0], StandbyHideTeemoOptionalCost, StringComparison.Ordinal)
            && HasTeemoStandbyHidePermission(state, intent.PlayerId);
        var usesFreeStandbyHideCost = optionalCosts.Count == 1
            && string.Equals(optionalCosts[0], StandbyHideFreeOptionalCost, StringComparison.Ordinal)
            && HasFreeStandbyHidePermission(state, intent.PlayerId);
        var usesBattlefieldExtraStandby = TryParseBattlefieldDestination(destination, out var extraStandbyBattlefieldObjectId);
        if (!string.Equals(destination, StandbyHideDestination, StringComparison.Ordinal)
            && !usesBattlefieldExtraStandby)
        {
            return RejectWithCorePrompts(
                state,
                $"Unsupported standby hide destination for {behavior.DisplayName}.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (!usesStandardStandbyHideCost && !usesTeemoStandbyHideCost && !usesFreeStandbyHideCost)
        {
            return RejectWithCorePrompts(
                state,
                $"Unsupported standby hide cost for {behavior.DisplayName}.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        var extraStandbyBattlefieldState = new CardObjectState();
        if (usesBattlefieldExtraStandby)
        {
            if (!TryGetBattlefieldExtraStandbyObject(
                state.PlayerZones,
                state.CardObjects,
                intent.PlayerId,
                extraStandbyBattlefieldObjectId,
                out var resolvedBattlefieldState))
            {
                return RejectWithCorePrompts(
                    state,
                    "HIDE_CARD battlefield destination requires a controlled Bandle Tree battlefield.",
                    ErrorCodes.InvalidTarget);
            }

            extraStandbyBattlefieldState = resolvedBattlefieldState;
        }

        if (!state.CardObjects.TryGetValue(command.SourceObjectId, out var existingState)
            || string.IsNullOrWhiteSpace(existingState.CardNo))
        {
            return RejectWithCorePrompts(
                state,
                "Source card identity is unknown for HIDE_CARD.",
                ErrorCodes.InvalidTarget);
        }

        if (!string.Equals(existingState.CardNo, behavior.CardNo, StringComparison.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Source card identity does not match HIDE_CARD cardNo.",
                ErrorCodes.InvalidTarget);
        }

        if (!SourceObjectControlledByPlayerOrLegacyOwned(existingState, intent.PlayerId))
        {
            return RejectWithCorePrompts(
                state,
                "Source card is not controlled by the player for HIDE_CARD.",
                ErrorCodes.InvalidTarget);
        }

        var hiddenTags = existingState.Tags
            .Concat([CardObjectTags.UnitCard])
            .Concat(ParseDelimitedValues(behavior.SourceUnitTags))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
        if (!hiddenTags.Contains(CardObjectTags.Standby, StringComparer.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} does not expose the Standby keyword for HIDE_CARD.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        var currentPool = state.RunePools.TryGetValue(intent.PlayerId, out var runePool) ? runePool : RunePool.Empty;
        var standbyHideManaCost = usesFreeStandbyHideCost ? 0 : StandbyHideManaCost;
        if (currentPool.Mana < standbyHideManaCost)
        {
            return RejectWithCorePrompts(
                state,
                $"Not enough mana to hide {behavior.DisplayName}.",
                ErrorCodes.InsufficientCost);
        }

        var runePools = PayRuneCosts(state, intent.PlayerId, standbyHideManaCost, 0);
        var playerZones = RemoveSourceCardFromHand(state, intent.PlayerId, zones, command.SourceObjectId);
        var nextZones = playerZones[intent.PlayerId];
        if (usesBattlefieldExtraStandby)
        {
            playerZones[intent.PlayerId] = nextZones with
            {
                Battlefields = nextZones.Battlefields.Contains(command.SourceObjectId, StringComparer.Ordinal)
                    ? nextZones.Battlefields
                    : nextZones.Battlefields.Concat([command.SourceObjectId]).ToArray()
            };
        }
        else
        {
            playerZones[intent.PlayerId] = nextZones with
            {
                Base = nextZones.Base.Contains(command.SourceObjectId, StringComparer.Ordinal)
                    ? nextZones.Base
                    : nextZones.Base.Concat([command.SourceObjectId]).ToArray()
            };
        }

        var hiddenState = existingState with
        {
            IsFaceDown = true,
            IsAttacking = false,
            IsDefending = false,
            IsExhausted = false,
            Power = behavior.SourceUnitPower > 0 ? behavior.SourceUnitPower : existingState.Power,
            Tags = hiddenTags,
            ManaCost = behavior.ManaCost,
            CardNo = behavior.CardNo
        };
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[command.SourceObjectId] = hiddenState;

        var nextState = state with
        {
            Tick = state.Tick + 1,
            RunePools = runePools,
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            PriorityPlayerId = null,
            PassedPriorityPlayerIds = []
        };
        var costPaidPayload = new Dictionary<string, object?>
        {
            ["playerId"] = intent.PlayerId,
            ["mana"] = standbyHideManaCost,
            ["power"] = 0,
            ["optionalCosts"] = optionalCosts.ToArray()
        };
        if (usesFreeStandbyHideCost)
        {
            costPaidPayload["standbyHideCostWaived"] = true;
        }
        if (usesTeemoStandbyHideCost)
        {
            costPaidPayload["teemoStandbyHideReplacement"] = true;
        }

        var events = new List<GameEvent>
        {
            new(
                "COST_PAID",
                $"{intent.PlayerId} 支付 {standbyHideManaCost} 点费用",
                costPaidPayload)
        };
        if (usesBattlefieldExtraStandby)
        {
            events.Add(new GameEvent(
                "BATTLEFIELD_TRIGGER_RESOLVED",
                $"{intent.PlayerId} 通过班德尔树额外布置待命牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["battlefieldObjectId"] = extraStandbyBattlefieldObjectId,
                    ["battlefieldCardNo"] = extraStandbyBattlefieldState?.CardNo,
                    ["trigger"] = "BATTLEFIELD_EXTRA_STANDBY_ARRANGED",
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["destination"] = destination
                }));
        }

        var hiddenPayload = new Dictionary<string, object?>
        {
            ["playerId"] = intent.PlayerId,
            ["sourceObjectId"] = command.SourceObjectId,
            ["destination"] = destination,
            ["destinationZone"] = usesBattlefieldExtraStandby ? "BATTLEFIELD" : "BASE",
            ["isFaceDown"] = true
        };
        if (usesBattlefieldExtraStandby)
        {
            hiddenPayload["battlefieldObjectId"] = extraStandbyBattlefieldObjectId;
            hiddenPayload["battlefieldCardNo"] = extraStandbyBattlefieldState?.CardNo;
        }

        events.Add(
            new GameEvent(
                "CARD_HIDDEN",
                $"{intent.PlayerId} 正面朝下放置一张待命牌",
                hiddenPayload));

        var objectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones);
        if (usesBattlefieldExtraStandby)
        {
            objectLocations[command.SourceObjectId] = new ObjectLocationState(
                intent.PlayerId,
                MoveUnitBattlefieldZone,
                extraStandbyBattlefieldObjectId);
        }
        var nextStateWithLocations = nextState with { ObjectLocations = objectLocations };

        return new ResolutionResult(
            true,
            null,
            nextStateWithLocations,
            events,
            ResolutionResult.BuildSnapshots(nextStateWithLocations),
            BuildCorePrompts(nextStateWithLocations));
    }

    private static ResolutionResult ResolveTapRune(
        MatchState state,
        PlayerIntent intent,
        TapRuneCommand command)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.ActivePlayerId, intent.PlayerId, StringComparison.Ordinal)
            || state.StackItems.Count > 0)
        {
            return RejectWithCorePrompts(
                state,
                "TAP_RUNE is only available during the active player's open main window.",
                ErrorCodes.PhaseNotAllowed);
        }

        if (string.IsNullOrWhiteSpace(command.SourceObjectId)
            || !state.PlayerZones.TryGetValue(intent.PlayerId, out var zones)
            || !zones.Base.Contains(command.SourceObjectId, StringComparer.Ordinal)
            || !state.CardObjects.TryGetValue(command.SourceObjectId, out var runeState)
            || !runeState.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)
            || !string.Equals(runeState.ControllerId, intent.PlayerId, StringComparison.Ordinal)
            || runeState.IsFaceDown)
        {
            return RejectWithCorePrompts(
                state,
                "TAP_RUNE requires a face-up controlled rune in the player's base.",
                ErrorCodes.InvalidTarget);
        }

        if (runeState.IsExhausted)
        {
            return RejectWithCorePrompts(
                state,
                "TAP_RUNE source rune is already exhausted.",
                ErrorCodes.InvalidTarget);
        }

        if (string.IsNullOrWhiteSpace(runeState.CardNo))
        {
            return RejectWithCorePrompts(
                state,
                "TAP_RUNE source must expose a known rune card number.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[command.SourceObjectId] = runeState with
        {
            IsExhausted = true
        };
        var runePools = state.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var currentPool = runePools.TryGetValue(intent.PlayerId, out var pool) ? pool : RunePool.Empty;
        var nextPool = currentPool with
        {
            Mana = currentPool.Mana + BasicRuneTapManaGain
        };
        runePools[intent.PlayerId] = nextPool;
        var objectLocations = ReconcileObjectLocations(state.ObjectLocations, state.PlayerZones);

        var nextState = state with
        {
            Tick = state.Tick + 1,
            CardObjects = cardObjects,
            RunePools = runePools,
            ObjectLocations = objectLocations,
            PriorityPlayerId = null,
            PassedPriorityPlayerIds = []
        };
        var events = new List<GameEvent>
        {
            new(
                "RUNE_TAPPED",
                $"{intent.PlayerId} 横置符文获得 1 点法力",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["cardNo"] = runeState.CardNo,
                    ["abilityId"] = BasicRuneTapAbilityId,
                    ["mana"] = BasicRuneTapManaGain,
                    ["manaAfter"] = nextPool.Mana
                }),
            new(
                "MANA_GAINED",
                $"{intent.PlayerId} 通过基础符文获得 1 点法力",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["abilityId"] = BasicRuneTapAbilityId,
                    ["mana"] = BasicRuneTapManaGain,
                    ["manaAfter"] = nextPool.Mana
                })
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static ResolutionResult ResolveRecycleRune(
        MatchState state,
        PlayerIntent intent,
        RecycleRuneCommand command)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.ActivePlayerId, intent.PlayerId, StringComparison.Ordinal)
            || state.StackItems.Count > 0)
        {
            return RejectWithCorePrompts(
                state,
                "RECYCLE_RUNE is only available during the active player's open main window.",
                ErrorCodes.PhaseNotAllowed);
        }

        if (string.IsNullOrWhiteSpace(command.SourceObjectId)
            || !state.PlayerZones.TryGetValue(intent.PlayerId, out var zones)
            || !zones.Base.Contains(command.SourceObjectId, StringComparer.Ordinal)
            || !state.CardObjects.TryGetValue(command.SourceObjectId, out var runeState)
            || !runeState.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)
            || !string.Equals(runeState.ControllerId, intent.PlayerId, StringComparison.Ordinal)
            || runeState.IsFaceDown)
        {
            return RejectWithCorePrompts(
                state,
                "RECYCLE_RUNE requires a face-up controlled trait rune in the player's base.",
                ErrorCodes.InvalidTarget);
        }

        if (string.IsNullOrWhiteSpace(runeState.CardNo))
        {
            return RejectWithCorePrompts(
                state,
                "RECYCLE_RUNE source must expose a known rune card number.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (!TryGetRuneTrait(runeState, out var runeTrait))
        {
            return RejectWithCorePrompts(
                state,
                "RECYCLE_RUNE requires a face-up controlled trait rune in the player's base.",
                ErrorCodes.InvalidTarget);
        }

        var playerZones = state.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        playerZones[intent.PlayerId] = zones with
        {
            Base = zones.Base
                .Where(objectId => !string.Equals(objectId, command.SourceObjectId, StringComparison.Ordinal))
                .ToArray(),
            RuneDeck = zones.RuneDeck
                .Concat([command.SourceObjectId])
                .ToArray()
        };

        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[command.SourceObjectId] = runeState with
        {
            IsFaceDown = false,
            IsExhausted = false
        };

        var runePools = state.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var currentPool = runePools.TryGetValue(intent.PlayerId, out var pool) ? pool : RunePool.Empty;
        var powerByTrait = currentPool.PowerByTrait.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        powerByTrait[runeTrait] = powerByTrait.TryGetValue(runeTrait, out var currentTraitPower)
            ? currentTraitPower + BasicRuneRecyclePowerGain
            : BasicRuneRecyclePowerGain;
        var nextPool = currentPool with
        {
            PowerByTrait = powerByTrait
        };
        runePools[intent.PlayerId] = nextPool;

        var objectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones);
        objectLocations[command.SourceObjectId] = new ObjectLocationState(intent.PlayerId, "RUNE_DECK");

        var nextState = state with
        {
            Tick = state.Tick + 1,
            PlayerZones = playerZones,
            CardObjects = cardObjects,
            RunePools = runePools,
            ObjectLocations = objectLocations,
            PriorityPlayerId = null,
            PassedPriorityPlayerIds = []
        };

        var events = new List<GameEvent>
        {
            new(
                "RUNE_RECYCLED",
                $"{intent.PlayerId} 回收符文获得 1 点{RuneTraitLabel(runeTrait)}符能",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["cardNo"] = runeState.CardNo,
                    ["abilityId"] = BasicRuneRecycleAbilityId,
                    ["trait"] = runeTrait,
                    ["power"] = BasicRuneRecyclePowerGain,
                    ["runeDeckCountAfter"] = playerZones[intent.PlayerId].RuneDeck.Count
                }),
            new(
                "POWER_GAINED",
                $"{intent.PlayerId} 通过基础符文获得 1 点{RuneTraitLabel(runeTrait)}符能",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["abilityId"] = BasicRuneRecycleAbilityId,
                    ["trait"] = runeTrait,
                    ["power"] = BasicRuneRecyclePowerGain,
                    ["powerAfter"] = nextPool.TotalPower,
                    ["traitPowerAfter"] = powerByTrait[runeTrait]
                })
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static bool TryGetRuneTrait(CardObjectState runeState, out string runeTrait)
    {
        foreach (var tag in runeState.Tags)
        {
            if (!tag.StartsWith("COLOR:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var trait = RuneTrait.Normalize(tag["COLOR:".Length..]);
            if (!string.IsNullOrWhiteSpace(trait))
            {
                runeTrait = trait;
                return true;
            }
        }

        runeTrait = string.Empty;
        return false;
    }

    private static string RuneTraitLabel(string trait)
    {
        return trait switch
        {
            RuneTrait.Red => "红色",
            RuneTrait.Green => "绿色",
            RuneTrait.Blue => "蓝色",
            RuneTrait.Yellow => "黄色",
            RuneTrait.Orange => "橙色",
            RuneTrait.Purple => "紫色",
            _ => trait
        };
    }

    private static bool TryExtractRecycleRunePaymentResourceActions(
        MatchState state,
        string playerId,
        IReadOnlyList<string> normalizedOptionalCosts,
        out IReadOnlyList<string> behaviorOptionalCosts,
        out IReadOnlyList<string> paymentResourceActions,
        out IReadOnlyList<string> recycledRuneObjectIds)
    {
        var behaviorCosts = new List<string>();
        var paymentActions = new List<string>();
        var runeObjectIds = new List<string>();
        var seenRuneObjectIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var optionalCost in normalizedOptionalCosts)
        {
            if (TryParseRecycleRunePaymentOptionalCost(optionalCost, out var runeObjectId))
            {
                if (!seenRuneObjectIds.Add(runeObjectId)
                    || !CanRecycleRuneForPayment(state, playerId, runeObjectId))
                {
                    behaviorOptionalCosts = [];
                    paymentResourceActions = [];
                    recycledRuneObjectIds = [];
                    return false;
                }

                paymentActions.Add(optionalCost);
                runeObjectIds.Add(runeObjectId);
                continue;
            }

            if (optionalCost.StartsWith(RecycleRunePaymentOptionalCostPrefix, StringComparison.Ordinal))
            {
                behaviorOptionalCosts = [];
                paymentResourceActions = [];
                recycledRuneObjectIds = [];
                return false;
            }

            behaviorCosts.Add(optionalCost);
        }

        behaviorOptionalCosts = behaviorCosts;
        paymentResourceActions = paymentActions;
        recycledRuneObjectIds = runeObjectIds;
        return true;
    }

    private static bool TryParseRecycleRunePaymentOptionalCost(
        string optionalCost,
        out string runeObjectId)
    {
        runeObjectId = string.Empty;
        if (string.IsNullOrWhiteSpace(optionalCost)
            || !optionalCost.StartsWith(RecycleRunePaymentOptionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        runeObjectId = optionalCost[RecycleRunePaymentOptionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(runeObjectId);
    }

    private static bool CanRecycleRuneForPayment(
        MatchState state,
        string playerId,
        string runeObjectId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Base.Contains(runeObjectId, StringComparer.Ordinal)
            && state.CardObjects.TryGetValue(runeObjectId, out var runeState)
            && runeState.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)
            && string.Equals(runeState.ControllerId, playerId, StringComparison.Ordinal)
            && !runeState.IsFaceDown
            && TryGetRuneTrait(runeState, out _);
    }

    private static RunePool ApplyRecycleRunePaymentToPool(
        RunePool currentPool,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyList<string> recycledRuneObjectIds)
    {
        var powerByTrait = currentPool.PowerByTrait.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var runeObjectId in recycledRuneObjectIds)
        {
            if (!cardObjects.TryGetValue(runeObjectId, out var runeState)
                || !TryGetRuneTrait(runeState, out var runeTrait))
            {
                continue;
            }

            powerByTrait[runeTrait] = powerByTrait.TryGetValue(runeTrait, out var currentTraitPower)
                ? currentTraitPower + BasicRuneRecyclePowerGain
                : BasicRuneRecyclePowerGain;
        }

        return currentPool with
        {
            PowerByTrait = powerByTrait
        };
    }

    private static bool AreRecycleRunePaymentResourceActionsRequired(
        RunePool currentPool,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyList<string> recycledRuneObjectIds,
        int extraPowerCost,
        IReadOnlyDictionary<string, int> extraPowerCostByTrait)
    {
        if (recycledRuneObjectIds.Count == 0)
        {
            return true;
        }

        if (CanPayPowerCost(currentPool, extraPowerCost, extraPowerCostByTrait))
        {
            return false;
        }

        for (var index = 0; index < recycledRuneObjectIds.Count; index++)
        {
            var adjustedPool = ApplyRecycleRunePaymentToPool(
                currentPool,
                cardObjects,
                recycledRuneObjectIds
                    .Where((_, currentIndex) => currentIndex != index)
                    .ToArray());
            if (CanPayPowerCost(adjustedPool, extraPowerCost, extraPowerCostByTrait))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, RunePool> ApplyRecycleRunePaymentResourceActions(
        IReadOnlyDictionary<string, RunePool> currentRunePools,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        Dictionary<string, ObjectLocationState> objectLocations,
        string playerId,
        IReadOnlyList<string> recycledRuneObjectIds,
        List<GameEvent> events,
        string paymentWindow = "PLAY_CARD")
    {
        var runePools = currentRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        if (recycledRuneObjectIds.Count == 0)
        {
            return runePools;
        }

        var currentPool = runePools.TryGetValue(playerId, out var existingPool) ? existingPool : RunePool.Empty;
        foreach (var runeObjectId in recycledRuneObjectIds)
        {
            if (!playerZones.TryGetValue(playerId, out var zones)
                || !zones.Base.Contains(runeObjectId, StringComparer.Ordinal)
                || !cardObjects.TryGetValue(runeObjectId, out var runeState)
                || !TryGetRuneTrait(runeState, out var runeTrait))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Base = zones.Base
                    .Where(objectId => !string.Equals(objectId, runeObjectId, StringComparison.Ordinal))
                    .ToArray(),
                RuneDeck = zones.RuneDeck
                    .Concat([runeObjectId])
                    .ToArray()
            };
            cardObjects[runeObjectId] = runeState with
            {
                IsFaceDown = false,
                IsExhausted = false
            };
            objectLocations[runeObjectId] = new ObjectLocationState(playerId, "RUNE_DECK");

            var powerByTrait = currentPool.PowerByTrait.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            powerByTrait[runeTrait] = powerByTrait.TryGetValue(runeTrait, out var currentTraitPower)
                ? currentTraitPower + BasicRuneRecyclePowerGain
                : BasicRuneRecyclePowerGain;
            currentPool = currentPool with
            {
                PowerByTrait = powerByTrait
            };
            runePools[playerId] = currentPool;

            events.Add(new GameEvent(
                "RUNE_RECYCLED",
                $"{playerId} 在支付费用时回收符文获得 1 点{RuneTraitLabel(runeTrait)}符能",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = runeObjectId,
                    ["cardNo"] = runeState.CardNo,
                    ["abilityId"] = BasicRuneRecycleAbilityId,
                    ["trait"] = runeTrait,
                    ["power"] = BasicRuneRecyclePowerGain,
                    ["paymentWindow"] = paymentWindow,
                    ["runeDeckCountAfter"] = playerZones[playerId].RuneDeck.Count
                }));
            events.Add(new GameEvent(
                "POWER_GAINED",
                $"{playerId} 通过支付资源动作获得 1 点{RuneTraitLabel(runeTrait)}符能",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = runeObjectId,
                    ["abilityId"] = BasicRuneRecycleAbilityId,
                    ["trait"] = runeTrait,
                    ["power"] = BasicRuneRecyclePowerGain,
                    ["paymentWindow"] = paymentWindow,
                    ["powerAfter"] = currentPool.TotalPower,
                    ["traitPowerAfter"] = powerByTrait[runeTrait]
                }));
        }

        return runePools;
    }

    private static bool HasFreeStandbyHidePermission(MatchState state, string playerId)
    {
        return state.UntilEndOfTurnEffects.Contains(
            FreeStandbyHideEffectId(playerId),
            StringComparer.Ordinal);
    }

    private static bool SourceObjectControlledByPlayerOrLegacyOwned(CardObjectState cardObject, string playerId)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return string.Equals(cardObject.ControllerId, playerId, StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(cardObject.OwnerId)
            || string.Equals(cardObject.OwnerId, playerId, StringComparison.Ordinal);
    }

    private static bool TryGetBattlefieldExtraStandbyObject(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldObjectId,
        out CardObjectState battlefieldState)
    {
        battlefieldState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Contains(battlefieldObjectId, StringComparer.Ordinal)
            || !cardObjects.TryGetValue(battlefieldObjectId, out var candidate)
            || !IsBattlefieldExtraStandbyCardNo(candidate.CardNo)
            || !SourceObjectControlledByPlayerOrLegacyOwned(candidate, playerId))
        {
            return false;
        }

        battlefieldState = candidate;
        return true;
    }

    private static bool HasTeemoStandbyHidePermission(MatchState state, string playerId)
    {
        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.LegendZone.Any(legendObjectId =>
                state.CardObjects.TryGetValue(legendObjectId, out var legendState)
                && IsTeemoLegendCardNo(legendState.CardNo));
    }

    private static bool IsTeemoLegendCardNo(string? cardNo)
    {
        return cardNo is TeemoOriginLegendCardNo
            or "OGN·263a/298"
            or "OGN·307/298"
            or "OGN·307*/298";
    }

    private static string FreeStandbyHideEffectId(string playerId)
    {
        return $"{FreeStandbyHideEffectPrefix}{playerId}";
    }

    private static bool TryParseBattlefieldDestination(string destination, out string battlefieldObjectId)
    {
        battlefieldObjectId = string.Empty;
        if (!destination.StartsWith(DeclareBattleBattlefieldPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        battlefieldObjectId = destination[DeclareBattleBattlefieldPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(battlefieldObjectId);
    }

    private static ResolutionResult ResolveMoveUnit(
        MatchState state,
        PlayerIntent intent,
        MoveUnitCommand command)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.ActivePlayerId, intent.PlayerId, StringComparison.Ordinal)
            || state.StackItems.Count > 0)
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT is only available during the active player's open main window.",
                ErrorCodes.PhaseNotAllowed);
        }

        if (!TryNormalizeMoveUnitZone(command.Origin, out var originZone, out var originUsesPreciseLocation)
            || !TryNormalizeMoveUnitZone(command.Destination, out var destinationZone, out var destinationUsesPreciseLocation))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT requires BASE or BATTLEFIELD origin and destination zones.",
                ErrorCodes.InvalidTarget);
        }

        var optionalCosts = NormalizeOptionalCosts(command.OptionalCosts);
        if (originUsesPreciseLocation || destinationUsesPreciseLocation)
        {
            return ResolvePreciseRoamMoveUnit(
                state,
                intent,
                command,
                originZone,
                destinationZone,
                originUsesPreciseLocation,
                destinationUsesPreciseLocation,
                optionalCosts);
        }

        if (optionalCosts.Count != 0)
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT optional costs are not implemented in P4 yet.",
                ErrorCodes.UnsupportedCommand);
        }

        if (string.Equals(originZone, destinationZone, StringComparison.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT requires a different destination zone.",
                ErrorCodes.InvalidTarget);
        }

        var sourceLocation = FindFieldObjectLocation(state.PlayerZones, command.SourceObjectId);
        if (sourceLocation is null
            || !string.Equals(sourceLocation.Value.PlayerId, intent.PlayerId, StringComparison.Ordinal)
            || !string.Equals(sourceLocation.Value.Zone, originZone, StringComparison.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Source unit is not controlled by the player in the requested origin zone.",
                ErrorCodes.InvalidTarget);
        }

        if (!state.CardObjects.TryGetValue(command.SourceObjectId, out var sourceState)
            || sourceState.IsFaceDown
            || !sourceState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT source must be a face-up unit.",
                ErrorCodes.InvalidTarget);
        }

        if (!SourceObjectControlledByPlayerOrLegacyOwned(sourceState, intent.PlayerId))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT source must be controlled by the acting player.",
                ErrorCodes.InvalidTarget);
        }

        if (string.IsNullOrWhiteSpace(sourceState.CardNo))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT source must expose a known unit card number.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (sourceState.IsAttacking || sourceState.IsDefending)
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT source must not be in combat.",
                ErrorCodes.InvalidTarget);
        }

        if (string.Equals(originZone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            && string.Equals(destinationZone, MoveUnitBaseZone, StringComparison.Ordinal)
            && HasBattlefieldStaticPreventMoveToBase(state, intent.PlayerId, command.SourceObjectId))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT blocked by battlefield static: units cannot move from this battlefield to base.",
                ErrorCodes.InvalidTarget);
        }

        var attachedEquipmentObjectIds = AttachedEquipmentObjectIds(state.CardObjects, command.SourceObjectId);
        if (attachedEquipmentObjectIds.Count > 0
            && !CanMoveExplicitAttachedEquipmentWithHost(
                state.PlayerZones,
                state.CardObjects,
                intent.PlayerId,
                sourceState,
                attachedEquipmentObjectIds))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT attached equipment movement is not implemented in P4 yet.",
                ErrorCodes.UnsupportedCommand);
        }

        var playerZones = NormalizeZonesForSeats(state);
        RemoveFieldObjectFromLocation(playerZones, intent.PlayerId, originZone, command.SourceObjectId);
        AddFieldObjectToLocation(playerZones, intent.PlayerId, destinationZone, command.SourceObjectId);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var movementTriggerEvents = ApplyBattlefieldMovedUnitPowerPlusOne(
            state,
            cardObjects,
            intent.PlayerId,
            command.SourceObjectId,
            originZone,
            destinationZone);
        var attachedEquipmentMoves = MoveAttachedEquipmentWithHost(
            playerZones,
            attachedEquipmentObjectIds,
            intent.PlayerId,
            command.SourceObjectId,
            destinationZone);
        var objectLocations = state.ObjectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        objectLocations[command.SourceObjectId] = new ObjectLocationState(intent.PlayerId, destinationZone);
        foreach (var attachedEquipmentObjectId in attachedEquipmentObjectIds)
        {
            objectLocations[attachedEquipmentObjectId] = new ObjectLocationState(intent.PlayerId, destinationZone);
        }
        var cleanupStackItem = new StackItemState(
            $"move-unit-{state.Tick + 1}",
            intent.PlayerId,
            command.SourceObjectId,
            "MOVE_UNIT",
            sourceState.CardNo,
            [command.SourceObjectId],
            optionalCosts: optionalCosts);
        var lethalCleanup = RunStateBasedCleanupLoop(
            playerZones,
            cardObjects,
            cleanupStackItem,
            state.RunePools,
            objectLocations: objectLocations);
        var runePools = lethalCleanup.RunePools;
        objectLocations = ReconcileObjectLocations(objectLocations, playerZones);

        var nextState = state with
        {
            Tick = state.Tick + 1,
            PlayerZones = playerZones,
            ObjectLocations = objectLocations,
            RunePools = runePools,
            CardObjects = cardObjects,
            PassedPriorityPlayerIds = []
        };

        var eventKind = string.Equals(destinationZone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            ? "UNIT_MOVED_TO_BATTLEFIELD"
            : "UNIT_MOVED_TO_BASE";
        var eventDescription = string.Equals(destinationZone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            ? $"{intent.PlayerId} 将单位移动到战场"
            : $"{intent.PlayerId} 将单位移动到基地";
        var events = new List<GameEvent>
        {
            new(
                eventKind,
                eventDescription,
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["targetObjectId"] = command.SourceObjectId,
                    ["originZone"] = originZone,
                    ["destinationZone"] = destinationZone,
                    ["optionalCosts"] = optionalCosts.ToArray()
                })
        };
        events.AddRange(attachedEquipmentMoves);
        events.AddRange(movementTriggerEvents);
        events.AddRange(lethalCleanup.Events);

        var taskAdvance = AdvancePendingBattlefieldTasksAfterStateChange(nextState, intent.PlayerId);
        nextState = taskAdvance.State;
        events.AddRange(taskAdvance.Events);

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static IReadOnlyList<string> AttachedEquipmentObjectIds(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string hostObjectId)
    {
        return cardObjects
            .Where(entry => string.Equals(
                entry.Value.AttachedToObjectId,
                hostObjectId,
                StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool CanMoveExplicitAttachedEquipmentWithHost(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        CardObjectState hostState,
        IReadOnlyList<string> attachedEquipmentObjectIds)
    {
        if (!FieldIdentityExplicitlyMatchesZone(hostState, playerId))
        {
            return false;
        }

        foreach (var equipmentObjectId in attachedEquipmentObjectIds)
        {
            var equipmentLocation = FindFieldObjectLocation(playerZones, equipmentObjectId);
            if (equipmentLocation is null
                || !string.Equals(equipmentLocation.Value.PlayerId, playerId, StringComparison.Ordinal)
                || !cardObjects.TryGetValue(equipmentObjectId, out var equipmentState)
                || !equipmentState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
                || !FieldIdentityExplicitlyMatchesZone(equipmentState, playerId))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<GameEvent> MoveAttachedEquipmentWithHost(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyList<string> attachedEquipmentObjectIds,
        string playerId,
        string hostObjectId,
        string destinationZone)
    {
        var events = new List<GameEvent>();
        foreach (var equipmentObjectId in attachedEquipmentObjectIds)
        {
            var equipmentLocation = FindFieldObjectLocation(playerZones, equipmentObjectId);
            if (equipmentLocation is null)
            {
                continue;
            }

            RemoveFieldObjectFromLocation(
                playerZones,
                equipmentLocation.Value.PlayerId,
                equipmentLocation.Value.Zone,
                equipmentObjectId);
            AddFieldObjectToLocation(playerZones, playerId, destinationZone, equipmentObjectId);
            events.Add(new GameEvent(
                "EQUIPMENT_MOVED_WITH_UNIT",
                $"{playerId} 的装备随单位移动",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["unitObjectId"] = hostObjectId,
                    ["equipmentObjectId"] = equipmentObjectId,
                    ["originZone"] = equipmentLocation.Value.Zone,
                    ["destinationZone"] = destinationZone,
                    ["attachedToObjectId"] = hostObjectId
                }));
        }

        return events;
    }

    private static ResolutionResult ResolvePreciseRoamMoveUnit(
        MatchState state,
        PlayerIntent intent,
        MoveUnitCommand command,
        string originZone,
        string destinationZone,
        bool originUsesPreciseLocation,
        bool destinationUsesPreciseLocation,
        IReadOnlyList<string> optionalCosts)
    {
        var isPreciseBattlefieldRoam =
            originUsesPreciseLocation
            && destinationUsesPreciseLocation
            && string.Equals(originZone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            && string.Equals(destinationZone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            && optionalCosts.Count == 1
            && string.Equals(optionalCosts[0], MoveUnitRoamOptionalCost, StringComparison.Ordinal);
        if (!isPreciseBattlefieldRoam)
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT precise battlefield locations are not implemented in P4 yet.",
                ErrorCodes.UnsupportedCommand);
        }

        var originLocation = NormalizeMoveUnitLocation(command.Origin);
        var destinationLocation = NormalizeMoveUnitLocation(command.Destination);
        if (!MoveUnitPreciseBattlefieldBelongsToPlayer(originLocation, intent.PlayerId)
            || !MoveUnitPreciseBattlefieldBelongsToPlayer(destinationLocation, intent.PlayerId)
            || string.Equals(originLocation, destinationLocation, StringComparison.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT precise roam requires two different friendly battlefields.",
                ErrorCodes.InvalidTarget);
        }

        if (!MoveUnitPreciseBattlefieldLocationIsKnownOrAbstract(state, originLocation)
            || !MoveUnitPreciseBattlefieldLocationIsKnownOrAbstract(state, destinationLocation))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT precise battlefield card locations must expose a known battlefield card number.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        var sourceLocation = FindFieldObjectLocation(state.PlayerZones, command.SourceObjectId);
        if (sourceLocation is null
            || !string.Equals(sourceLocation.Value.PlayerId, intent.PlayerId, StringComparison.Ordinal)
            || !string.Equals(sourceLocation.Value.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Source unit is not controlled by the player in the requested origin zone.",
                ErrorCodes.InvalidTarget);
        }

        var originBattlefieldObjectId = PreciseBattlefieldLocationObjectId(originLocation);
        if (state.ObjectLocations.TryGetValue(command.SourceObjectId, out var sourceObjectLocation)
            && (!string.Equals(sourceObjectLocation.PlayerId, intent.PlayerId, StringComparison.Ordinal)
                || !string.Equals(sourceObjectLocation.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
                || !string.Equals(
                    sourceObjectLocation.BattlefieldObjectId,
                    originBattlefieldObjectId,
                    StringComparison.Ordinal)))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT source precise battlefield location does not match the authoritative location.",
                ErrorCodes.InvalidTarget);
        }

        if (!state.CardObjects.TryGetValue(command.SourceObjectId, out var sourceState)
            || sourceState.IsFaceDown
            || !sourceState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT source must be a face-up unit.",
                ErrorCodes.InvalidTarget);
        }

        if (!SourceObjectControlledByPlayerOrLegacyOwned(sourceState, intent.PlayerId))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT source must be controlled by the acting player.",
                ErrorCodes.InvalidTarget);
        }

        if (string.IsNullOrWhiteSpace(sourceState.CardNo))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT source must expose a known unit card number.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (!HasRoamPermission(state, intent.PlayerId, command.SourceObjectId, sourceState))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT precise battlefield movement requires roam permission.",
                ErrorCodes.InvalidTarget);
        }

        if (sourceState.IsAttacking || sourceState.IsDefending)
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT source must not be in combat.",
                ErrorCodes.InvalidTarget);
        }

        if (state.CardObjects.Values.Any(cardObject => string.Equals(
            cardObject.AttachedToObjectId,
            command.SourceObjectId,
            StringComparison.Ordinal)))
        {
            return RejectWithCorePrompts(
                state,
                "MOVE_UNIT attached equipment movement is not implemented in P4 yet.",
                ErrorCodes.UnsupportedCommand);
        }

        var playerZones = NormalizeZonesForSeats(state);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var objectLocations = state.ObjectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        objectLocations[command.SourceObjectId] = new ObjectLocationState(
            intent.PlayerId,
            MoveUnitBattlefieldZone,
            PreciseBattlefieldLocationObjectId(destinationLocation));
        var movementTriggerEvents = ApplyBattlefieldMovedUnitPowerPlusOne(
            state,
            cardObjects,
            intent.PlayerId,
            command.SourceObjectId,
            originZone,
            destinationZone);
        var cleanupStackItem = new StackItemState(
            $"move-unit-{state.Tick + 1}",
            intent.PlayerId,
            command.SourceObjectId,
            "MOVE_UNIT_ROAM",
            sourceState.CardNo,
            [command.SourceObjectId],
            optionalCosts: optionalCosts);
        var lethalCleanup = RunStateBasedCleanupLoop(
            playerZones,
            cardObjects,
            cleanupStackItem,
            state.RunePools,
            objectLocations: objectLocations);
        var runePools = lethalCleanup.RunePools;
        objectLocations = ReconcileObjectLocations(objectLocations, playerZones);
        var nextState = state with
        {
            Tick = state.Tick + 1,
            PlayerZones = playerZones,
            ObjectLocations = objectLocations,
            RunePools = runePools,
            CardObjects = cardObjects,
            PassedPriorityPlayerIds = []
        };

        var events = new List<GameEvent>
        {
            new(
                "UNIT_MOVED_TO_BATTLEFIELD",
                $"{intent.PlayerId} 使用游走在战场间移动单位",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["targetObjectId"] = command.SourceObjectId,
                    ["originZone"] = MoveUnitBattlefieldZone,
                    ["destinationZone"] = MoveUnitBattlefieldZone,
                    ["origin"] = originLocation,
                    ["destination"] = destinationLocation,
                    ["movementKeyword"] = MoveUnitRoamKeyword,
                    ["optionalCosts"] = optionalCosts.ToArray()
                })
        };
        events.AddRange(movementTriggerEvents);
        events.AddRange(lethalCleanup.Events);

        var taskAdvance = AdvancePendingBattlefieldTasksAfterStateChange(nextState, intent.PlayerId);
        nextState = taskAdvance.State;
        events.AddRange(taskAdvance.Events);

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static ResolutionResult ResolveDeclareBattle(
        MatchState state,
        PlayerIntent intent,
        DeclareBattleCommand command)
    {
        if (!TryBuildMinimalDeclareBattle(
                state,
                intent,
                command,
                out var attackerObjectIds,
                out var defenderObjectIds,
                out var optionalCosts))
        {
            return RejectWithCorePrompts(
                state,
                "DECLARE_BATTLE is not implemented in P4 yet.",
                ErrorCodes.UnsupportedCommand);
        }

        var battlefieldId = command.BattlefieldId?.Trim() ?? string.Empty;
        if (ResolutionResult.ActiveStartBattleTask(state) is { BattlefieldObjectId.Length: > 0 } activeStartBattleTask
            && !DeclareBattleMatchesActiveStartBattleTask(
                state,
                activeStartBattleTask.BattlefieldObjectId,
                battlefieldId,
                attackerObjectIds,
                defenderObjectIds))
        {
            return RejectWithCorePrompts(
                state,
                "DECLARE_BATTLE must match the active START_BATTLE task.",
                ErrorCodes.PhaseNotAllowed);
        }

        var playerZones = NormalizeZonesForSeats(state);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var rngCursor = state.RngCursor;
        var attackerObjectId = attackerObjectIds[0];
        var attackerStates = attackerObjectIds.ToDictionary(
            attackerObjectId => attackerObjectId,
            attackerObjectId => cardObjects[attackerObjectId],
            StringComparer.Ordinal);
        var attackerState = cardObjects[attackerObjectId];
        var defenderStates = defenderObjectIds.ToDictionary(
            defenderObjectId => defenderObjectId,
            defenderObjectId => cardObjects[defenderObjectId],
            StringComparer.Ordinal);
        var huntAmount = CardResourceKeywordRules.HuntAmountFromTags(attackerState.Tags);
        foreach (var attackingObjectId in attackerObjectIds)
        {
            var attackingState = cardObjects[attackingObjectId];
            cardObjects[attackingObjectId] = attackingState with
            {
                IsAttacking = true,
                IsDefending = false
            };
        }

        foreach (var defenderObjectId in defenderObjectIds)
        {
            var defenderState = cardObjects[defenderObjectId];
            cardObjects[defenderObjectId] = defenderState with
            {
                IsAttacking = false,
                IsDefending = true
            };
        }

        var defendingPlayerId = ResolveSingleDefendingPlayerId(playerZones, defenderObjectIds);
        if (!TryResolveBattlefieldDefenderSteadfastChoice(
                state,
                playerZones,
                battlefieldId,
                command.BattlefieldTargetObjectIds,
                defenderObjectIds,
                out var battlefieldSteadfastObjectId,
                out var battlefieldSteadfastObjectSourceId,
                out var battlefieldSteadfastCardNo,
                out var battlefieldSteadfastRejection))
        {
            return battlefieldSteadfastRejection;
        }
        if (!TryResolveBattlefieldDefenderMoveToBaseChoice(
                state,
                playerZones,
                battlefieldId,
                command.BattlefieldTargetObjectIds,
                defenderObjectIds,
                out var battlefieldDefenderMoveObjectId,
                out var battlefieldDefenderMoveObjectSourceId,
                out var battlefieldDefenderMoveCardNo,
                out var battlefieldDefenderMoveRejection))
        {
            return battlefieldDefenderMoveRejection;
        }

        var combatEvents = new List<GameEvent>();
        var battlefieldRevealSpellTrigger = ResolveBattlefieldDefendRevealSpellTrigger(
            playerZones,
            cardObjects,
            defendingPlayerId ?? string.Empty,
            battlefieldId,
            attackerObjectId,
            rngCursor);
        rngCursor = battlefieldRevealSpellTrigger.RngCursor;
        combatEvents.AddRange(battlefieldRevealSpellTrigger.Events);
        if (!string.IsNullOrWhiteSpace(battlefieldSteadfastObjectId))
        {
            combatEvents.Add(new GameEvent(
                "BATTLEFIELD_TRIGGER_RESOLVED",
                $"{defendingPlayerId} 防守战场并强化单位",
                new Dictionary<string, object?>
                {
                    ["playerId"] = defendingPlayerId,
                    ["battlefieldId"] = battlefieldId,
                    ["battlefieldObjectId"] = battlefieldSteadfastObjectSourceId,
                    ["battlefieldCardNo"] = battlefieldSteadfastCardNo,
                    ["trigger"] = "BATTLEFIELD_DEFENSE_GRANT_STEADFAST_TWO",
                    ["targetObjectId"] = battlefieldSteadfastObjectId,
                    ["keyword"] = CardCombatKeywordNames.Steadfast,
                    ["keywordBonus"] = 2
                }));
        }

        var damageTriggeredDestroyTargetObjectIds = new HashSet<string>(StringComparer.Ordinal);
        var hasMultipleAttackers = attackerObjectIds.Count > 1;
        var hasMultipleDefenders = defenderObjectIds.Count > 1;
        var defenderAssignments = BuildBattleDamageAssignmentOrder(defenderObjectIds, defenderStates);
        var defendingUnitCount = defenderAssignments.Count;
        var assignedOverkillDamageToEnemyUnits = 0;
        foreach (var attackingObjectId in attackerObjectIds)
        {
            var attackingState = attackerStates[attackingObjectId];
            var attackerCombatPower = ResolveBattleCombatPower(
                state,
                playerZones,
                attackingObjectId,
                attackingState,
                true,
                0,
                defendingPlayerId,
                battlefieldId,
                battlefieldSteadfastObjectId,
                out var assaultBonus,
                out var attackerStaticPowerBonus);
            var remainingAttackerDamage = attackerCombatPower;
            for (var defenderIndex = 0; defenderIndex < defenderAssignments.Count && remainingAttackerDamage > 0; defenderIndex++)
            {
                var assignment = defenderAssignments[defenderIndex];
                var defenderState = cardObjects[assignment.ObjectId];
                var defenderCombatPower = ResolveBattleCombatPower(
                    state,
                    playerZones,
                    assignment.ObjectId,
                    defenderState,
                    false,
                    defendingUnitCount,
                    null,
                    battlefieldId,
                    battlefieldSteadfastObjectId,
                    out _,
                    out _);
                var lethalDamage = Math.Max(0, defenderCombatPower - defenderState.Damage);
                var damageAmount = defenderIndex == defenderAssignments.Count - 1
                    ? remainingAttackerDamage
                    : Math.Min(remainingAttackerDamage, lethalDamage);
                if (damageAmount <= 0)
                {
                    continue;
                }

                assignedOverkillDamageToEnemyUnits += Math.Max(0, damageAmount - lethalDamage);
                var attackerDamageApplication = ApplyDamageToCardObject(
                    cardObjects,
                    assignment.ObjectId,
                    damageAmount,
                    damageTriggeredDestroyTargetObjectIds);
                combatEvents.Add(new GameEvent(
                    "DAMAGE_APPLIED",
                    "战斗中进攻单位造成伤害",
                    BuildCombatDamagePayload(
                        attackingObjectId,
                        assignment.ObjectId,
                        attackerDamageApplication,
                        battlefieldId,
                        "ATTACKER",
                        attackingState.Power,
                        assaultBonus,
                        attackerCombatPower,
                        CardCombatKeywordNames.Assault,
                        attackerStaticPowerBonus,
                        hasMultipleDefenders ? defenderIndex + 1 : null,
                        hasMultipleDefenders ? assignment.Role : null)));
                remainingAttackerDamage -= damageAmount;
            }
        }

        var attackerAssignments = BuildBattleDamageAssignmentOrder(attackerObjectIds, attackerStates);
        foreach (var assignment in defenderAssignments)
        {
            var defenderState = defenderStates[assignment.ObjectId];
            var defenderCombatPower = ResolveBattleCombatPower(
                state,
                playerZones,
                assignment.ObjectId,
                defenderState,
                false,
                defendingUnitCount,
                null,
                battlefieldId,
                battlefieldSteadfastObjectId,
                out var steadfastBonus,
                out var defenderStaticPowerBonus);
            if (defenderCombatPower <= 0)
            {
                continue;
            }

            var remainingDefenderDamage = defenderCombatPower;
            for (var attackerIndex = 0; attackerIndex < attackerAssignments.Count && remainingDefenderDamage > 0; attackerIndex++)
            {
                var attackerAssignment = attackerAssignments[attackerIndex];
                var targetAttackerState = cardObjects[attackerAssignment.ObjectId];
                var targetAttackerCombatPower = ResolveBattleCombatPower(
                    state,
                    playerZones,
                    attackerAssignment.ObjectId,
                    targetAttackerState,
                    true,
                    0,
                    defendingPlayerId,
                    battlefieldId,
                    battlefieldSteadfastObjectId,
                    out _,
                    out _);
                var lethalDamage = Math.Max(0, targetAttackerCombatPower - targetAttackerState.Damage);
                var damageAmount = attackerIndex == attackerAssignments.Count - 1
                    ? remainingDefenderDamage
                    : Math.Min(remainingDefenderDamage, lethalDamage);
                if (damageAmount <= 0)
                {
                    continue;
                }

                var defenderDamageApplication = ApplyDamageToCardObject(
                    cardObjects,
                    attackerAssignment.ObjectId,
                    damageAmount,
                    damageTriggeredDestroyTargetObjectIds);
                combatEvents.Add(new GameEvent(
                    "DAMAGE_APPLIED",
                    "战斗中防守单位造成伤害",
                    BuildCombatDamagePayload(
                        assignment.ObjectId,
                        attackerAssignment.ObjectId,
                        defenderDamageApplication,
                        battlefieldId,
                        "DEFENDER",
                        defenderState.Power,
                        steadfastBonus,
                        defenderCombatPower,
                        CardCombatKeywordNames.Steadfast,
                        defenderStaticPowerBonus,
                        hasMultipleAttackers ? attackerIndex + 1 : null,
                        hasMultipleAttackers ? attackerAssignment.Role : null)));
                remainingDefenderDamage -= damageAmount;
            }
        }

        var combatStackItem = new StackItemState(
            stackItemId: $"declare-battle-{state.Tick + 1}",
            controllerId: intent.PlayerId,
            sourceObjectId: attackerObjectId,
            effectKind: "DECLARE_BATTLE_COMBAT_DAMAGE",
            cardNo: attackerState.CardNo,
            targetObjectIds: attackerObjectIds.Concat(defenderObjectIds).ToArray(),
            optionalCosts: optionalCosts);
        IReadOnlyDictionary<string, RunePool> runePools = state.RunePools;
        IReadOnlyList<string> untilEndOfTurnEffects = state.UntilEndOfTurnEffects;
        var lethalCleanup = RunStateBasedCleanupLoop(
            playerZones,
            cardObjects,
            combatStackItem,
            runePools,
            battlefieldId,
            damageTriggeredDestroyTargetObjectIds);
        runePools = lethalCleanup.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        combatEvents.AddRange(lethalCleanup.Events);
        if (!string.IsNullOrWhiteSpace(battlefieldDefenderMoveObjectId))
        {
            TryResolveBattlefieldDefenderMoveToBaseTrigger(
                playerZones,
                cardObjects,
                defendingPlayerId ?? string.Empty,
                battlefieldId,
                attackerObjectId,
                battlefieldDefenderMoveObjectId,
                battlefieldDefenderMoveObjectSourceId,
                battlefieldDefenderMoveCardNo,
                combatEvents);
        }

        var playerExperience = state.PlayerExperience;
        var playerScores = state.PlayerScores;
        string? winnerPlayerId = null;
        string? resolvedBattleWinnerPlayerId = null;
        if (huntAmount > 0
            && defenderObjectIds.All(defenderObjectId => lethalCleanup.DestroyedObjectIds.Contains(defenderObjectId, StringComparer.Ordinal))
            && cardObjects.TryGetValue(attackerObjectId, out var survivingAttackerState)
            && survivingAttackerState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && IsObjectOnField(playerZones, attackerObjectId))
        {
            combatEvents.Add(new GameEvent(
                "BATTLEFIELD_CONQUERED",
                $"{intent.PlayerId} 征服战场",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["battlefieldId"] = battlefieldId,
                    ["sourceObjectId"] = attackerObjectId,
                    ["defeatedObjectIds"] = defenderObjectIds.ToArray(),
                    ["huntAmount"] = huntAmount,
                    ["assignedOverkillDamageToEnemyUnits"] = assignedOverkillDamageToEnemyUnits
                }));
            playerExperience = GainExperience(
                NormalizeExperienceForSeats(state),
                intent.PlayerId,
                huntAmount,
                combatStackItem,
                combatEvents);
            TryResolveBattlefieldConquerMillTwoTrigger(
                playerZones,
                cardObjects,
                intent.PlayerId,
                battlefieldId,
                attackerObjectId,
                combatEvents);
            var battlefieldRecycleRuneTrigger = ResolveBattlefieldConquerRecycleRuneTrigger(
                playerZones,
                cardObjects,
                intent.PlayerId,
                battlefieldId,
                attackerObjectId,
                rngCursor);
            rngCursor = battlefieldRecycleRuneTrigger.RngCursor;
            combatEvents.AddRange(battlefieldRecycleRuneTrigger.Events);
            var battlefieldRevealRecycleTrigger = ResolveBattlefieldConquerRevealRecycleTrigger(
                state,
                playerZones,
                cardObjects,
                intent.PlayerId,
                battlefieldId,
                attackerObjectId,
                rngCursor);
            rngCursor = battlefieldRevealRecycleTrigger.RngCursor;
            combatEvents.AddRange(battlefieldRevealRecycleTrigger.Events);
            if (TryResolveBattlefieldConquerDiscardDrawTrigger(
                    state,
                    playerZones,
                    cardObjects,
                    playerScores,
                    intent.PlayerId,
                    battlefieldId,
                    attackerObjectId,
                    rngCursor,
                    combatEvents,
                    out var battlefieldDiscardDrawApplication))
            {
                playerScores = battlefieldDiscardDrawApplication.PlayerScores;
                winnerPlayerId = battlefieldDiscardDrawApplication.WinnerPlayerId;
                rngCursor = battlefieldDiscardDrawApplication.RngCursor;
            }
            if (TryResolveBattlefieldConquerConsumeBoonDrawTrigger(
                    state,
                    playerZones,
                    cardObjects,
                    playerScores,
                    intent.PlayerId,
                    battlefieldId,
                    attackerObjectId,
                    rngCursor,
                    combatEvents,
                    out var battlefieldBoonDrawApplication))
            {
                playerScores = battlefieldBoonDrawApplication.PlayerScores;
                winnerPlayerId = battlefieldBoonDrawApplication.WinnerPlayerId;
                rngCursor = battlefieldBoonDrawApplication.RngCursor;
            }
            var battlefieldReadyLegendTrigger = ResolveBattlefieldConquerPayOneReadyLegendTrigger(
                playerZones,
                cardObjects,
                runePools,
                intent.PlayerId,
                battlefieldId,
                attackerObjectId);
            runePools = battlefieldReadyLegendTrigger.RunePools;
            combatEvents.AddRange(battlefieldReadyLegendTrigger.Events);
            if (TryResolveBattlefieldConquerPowerfulPayOneDrawTrigger(
                    state,
                    playerZones,
                    cardObjects,
                    runePools,
                    playerScores,
                    intent.PlayerId,
                    battlefieldId,
                    attackerObjectId,
                    rngCursor,
                    combatEvents,
                    out var battlefieldPowerfulDrawRunePools,
                    out var battlefieldPowerfulDrawApplication))
            {
                runePools = battlefieldPowerfulDrawRunePools;
                playerScores = battlefieldPowerfulDrawApplication.PlayerScores;
                winnerPlayerId = battlefieldPowerfulDrawApplication.WinnerPlayerId;
                rngCursor = battlefieldPowerfulDrawApplication.RngCursor;
            }
            if (TryResolveBattlefieldConquerPayOneCreateGoldTrigger(
                    playerZones,
                    cardObjects,
                    runePools,
                    intent.PlayerId,
                    battlefieldId,
                    attackerObjectId,
                    combatEvents,
                    out var battlefieldGoldRunePools))
            {
                runePools = battlefieldGoldRunePools;
            }
            if (TryResolveBattlefieldConquerPayOneReturnUnitCreateSandSoldierTrigger(
                    playerZones,
                    cardObjects,
                    runePools,
                    intent.PlayerId,
                    battlefieldId,
                    attackerObjectId,
                    combatEvents,
                    out var battlefieldSandSoldierRunePools))
            {
                runePools = battlefieldSandSoldierRunePools;
            }
            if (TryResolveBattlefieldConquerReadyTwoRunesAtEndTrigger(
                    playerZones,
                    cardObjects,
                    untilEndOfTurnEffects,
                    intent.PlayerId,
                    battlefieldId,
                    attackerObjectId,
                    combatEvents,
                    out var battlefieldReadyRuneUntilEndOfTurnEffects))
            {
                untilEndOfTurnEffects = battlefieldReadyRuneUntilEndOfTurnEffects;
            }
            TryResolveBattlefieldConquerReadyEquipmentTrigger(
                playerZones,
                cardObjects,
                intent.PlayerId,
                battlefieldId,
                attackerObjectId,
                combatEvents);
            if (TryResolveBattlefieldConquerDrawForOtherBattlefieldsTrigger(
                    state,
                    playerZones,
                    cardObjects,
                    playerScores,
                    intent.PlayerId,
                    battlefieldId,
                    attackerObjectId,
                    rngCursor,
                    combatEvents,
                    out var battlefieldOtherDrawApplication))
            {
                playerScores = battlefieldOtherDrawApplication.PlayerScores;
                winnerPlayerId = battlefieldOtherDrawApplication.WinnerPlayerId;
                rngCursor = battlefieldOtherDrawApplication.RngCursor;
            }
            TryResolveBattlefieldConquerOverkillCreateWarhawkTrigger(
                playerZones,
                cardObjects,
                intent.PlayerId,
                battlefieldId,
                attackerObjectId,
                assignedOverkillDamageToEnemyUnits,
                combatEvents);

            combatEvents.AddRange(ResolveViLegendOverkillConquerTrigger(
                playerZones,
                cardObjects,
                intent.PlayerId,
                battlefieldId,
                attackerObjectId,
                assignedOverkillDamageToEnemyUnits));
            var ireliaConquerTrigger = ResolveIreliaLegendConquerReadyTrigger(
                playerZones,
                cardObjects,
                runePools,
                intent.PlayerId,
                battlefieldId,
                attackerObjectId);
            runePools = ireliaConquerTrigger.RunePools;
            combatEvents.AddRange(ireliaConquerTrigger.Events);
            var settConquerTrigger = ResolveSettLegendConquerReadyTrigger(
                playerZones,
                cardObjects,
                intent.PlayerId,
                battlefieldId,
                attackerObjectId);
            combatEvents.AddRange(settConquerTrigger);
            if (TryResolveLeblancLegendImageTrigger(
                    playerZones,
                    cardObjects,
                    intent.PlayerId,
                    battlefieldId,
                    attackerObjectId,
                    attackerObjectId,
                    "BATTLEFIELD_CONQUERED_CREATE_IMAGE",
                    out var leblancConquerEvents))
            {
                combatEvents.AddRange(leblancConquerEvents);
            }
            var reksaiConquerTrigger = ResolveReksaiLegendConquerRevealTrigger(
                state,
                playerZones,
                cardObjects,
                intent.PlayerId,
                battlefieldId,
                attackerObjectId,
                rngCursor);
            rngCursor = reksaiConquerTrigger.RngCursor;
            combatEvents.AddRange(reksaiConquerTrigger.Events);
            if (TryResolveIvernLegendBrushTrigger(
                    playerZones,
                    cardObjects,
                    intent.PlayerId,
                    battlefieldId,
                    attackerObjectId,
                    "BATTLEFIELD_CONQUERED_REPLACE_WITH_BRUSH",
                    out var ivernConquerEvents))
            {
                combatEvents.AddRange(ivernConquerEvents);
            }
            if (winnerPlayerId is null
                && CountControlledBattlefieldUnits(playerZones, cardObjects, intent.PlayerId) >= 4
                && TryGetGarenIntroLegendCardNo(playerZones, cardObjects, intent.PlayerId, out var garenLegendCardNo))
            {
                combatEvents.Add(new GameEvent(
                    "LEGEND_TRIGGER_RESOLVED",
                    $"{intent.PlayerId} 的德玛西亚之力因征服战场触发",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = intent.PlayerId,
                        ["legendCardNo"] = garenLegendCardNo,
                        ["trigger"] = "BATTLEFIELD_CONQUERED_DRAW_TWO",
                        ["sourceObjectId"] = attackerObjectId,
                        ["battlefieldId"] = battlefieldId,
                        ["controlledBattlefieldUnitCount"] = CountControlledBattlefieldUnits(playerZones, cardObjects, intent.PlayerId)
                    }));
                var drawApplication = ApplyDrawToPlayer(
                    state,
                    playerZones,
                    playerScores,
                    intent.PlayerId,
                    2,
                    rngCursor,
                    combatEvents);
                playerScores = drawApplication.PlayerScores;
                winnerPlayerId = drawApplication.WinnerPlayerId;
                rngCursor = drawApplication.RngCursor;
            }
        }

        if (TryResolveBattleWinnerPlayerId(
                playerZones,
                cardObjects,
                attackerObjectIds,
                defenderObjectIds,
                defendingPlayerId,
                intent.PlayerId,
                out var battleWinnerPlayerId))
        {
            resolvedBattleWinnerPlayerId = battleWinnerPlayerId;
            if (!string.IsNullOrWhiteSpace(defendingPlayerId)
                && string.Equals(battleWinnerPlayerId, defendingPlayerId, StringComparison.Ordinal))
            {
                var battlefieldHeldEventEmitted = false;
                var battlefieldTriggerEvents = new List<GameEvent>();
                if (TryResolveBattlefieldHeldDrawTrigger(
                        state,
                        playerZones,
                        cardObjects,
                        playerScores,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        rngCursor,
                        battlefieldTriggerEvents,
                        out var battlefieldDrawApplication))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(battlefieldTriggerEvents);
                    playerScores = battlefieldDrawApplication.PlayerScores;
                    winnerPlayerId = battlefieldDrawApplication.WinnerPlayerId;
                    rngCursor = battlefieldDrawApplication.RngCursor;
                }

                var battlefieldMinionEvents = new List<GameEvent>();
                if (TryResolveBattlefieldHeldCreateMinionTrigger(
                        playerZones,
                        cardObjects,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        battlefieldMinionEvents))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(battlefieldMinionEvents);
                }

                var battlefieldRuneEvents = new List<GameEvent>();
                if (TryResolveBattlefieldHeldEachPlayerCallRuneTrigger(
                        state,
                        playerZones,
                        cardObjects,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        battlefieldRuneEvents))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(battlefieldRuneEvents);
                }

                var battlefieldSingleRuneEvents = new List<GameEvent>();
                if (TryResolveBattlefieldHeldCallRuneTrigger(
                        playerZones,
                        cardObjects,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        battlefieldSingleRuneEvents))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(battlefieldSingleRuneEvents);
                }

                var battlefieldScoreEvents = new List<GameEvent>();
                if (TryResolveBattlefieldHeldPayPowerScoreTrigger(
                        state,
                        playerZones,
                        cardObjects,
                        runePools,
                        playerScores,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        EffectiveWinningScore(playerZones, cardObjects),
                        battlefieldScoreEvents,
                        out var battlefieldScoreRunePools,
                        out var battlefieldScorePlayerScores,
                        out var battlefieldScoreWinnerPlayerId))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(battlefieldScoreEvents);
                    runePools = battlefieldScoreRunePools;
                    playerScores = battlefieldScorePlayerScores;
                    winnerPlayerId = battlefieldScoreWinnerPlayerId ?? winnerPlayerId;
                }

                var battlefieldSevenUnitsEvents = new List<GameEvent>();
                if (TryResolveBattlefieldHeldSevenUnitsWinTrigger(
                        playerZones,
                        cardObjects,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        EffectiveWinningScore(playerZones, cardObjects),
                        battlefieldSevenUnitsEvents,
                        out var battlefieldSevenUnitsWinnerPlayerId))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(battlefieldSevenUnitsEvents);
                    winnerPlayerId = battlefieldSevenUnitsWinnerPlayerId ?? winnerPlayerId;
                }

                var battlefieldUnitCostEvents = new List<GameEvent>();
                if (TryResolveBattlefieldHeldUnitCostIncreaseTrigger(
                        playerZones,
                        cardObjects,
                        untilEndOfTurnEffects,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        battlefieldUnitCostEvents,
                        out var battlefieldUnitCostUntilEndOfTurnEffects))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(battlefieldUnitCostEvents);
                    untilEndOfTurnEffects = battlefieldUnitCostUntilEndOfTurnEffects;
                }

                var battlefieldNextSpellEchoEvents = new List<GameEvent>();
                if (TryResolveBattlefieldHeldNextSpellEchoTrigger(
                        playerZones,
                        cardObjects,
                        untilEndOfTurnEffects,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        battlefieldNextSpellEchoEvents,
                        out var battlefieldNextSpellEchoUntilEndOfTurnEffects))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(battlefieldNextSpellEchoEvents);
                    untilEndOfTurnEffects = battlefieldNextSpellEchoUntilEndOfTurnEffects;
                }

                var battlefieldUnitConquestEvents = new List<GameEvent>();
                if (TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger(
                        state,
                        playerZones,
                        cardObjects,
                        playerScores,
                        untilEndOfTurnEffects,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        rngCursor,
                        battlefieldUnitConquestEvents,
                        out var battlefieldUnitConquestDrawApplication,
                        out var battlefieldUnitConquestUntilEndOfTurnEffects))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(battlefieldUnitConquestEvents);
                    playerScores = battlefieldUnitConquestDrawApplication.PlayerScores;
                    winnerPlayerId = battlefieldUnitConquestDrawApplication.WinnerPlayerId ?? winnerPlayerId;
                    rngCursor = battlefieldUnitConquestDrawApplication.RngCursor;
                    untilEndOfTurnEffects = battlefieldUnitConquestUntilEndOfTurnEffects;
                }

                var battlefieldBoonEvents = new List<GameEvent>();
                if (TryResolveBattlefieldHeldGrantBoonTrigger(
                        playerZones,
                        cardObjects,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds,
                        battlefieldBoonEvents))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(battlefieldBoonEvents);
                }

                var battlefieldMoveEvents = new List<GameEvent>();
                if (TryResolveBattlefieldHeldMoveUnitToBaseTrigger(
                        playerZones,
                        cardObjects,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds,
                        battlefieldMoveEvents))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(battlefieldMoveEvents);
                }

                var battlefieldReturnHeroEvents = new List<GameEvent>();
                if (TryResolveBattlefieldHeldReturnHeroTrigger(
                        playerZones,
                        cardObjects,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        battlefieldReturnHeroEvents))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(battlefieldReturnHeroEvents);
                }

                var leblancCopySourceObjectId = defenderObjectIds.FirstOrDefault(defenderObjectId =>
                    IsObjectOnField(playerZones, defenderObjectId)
                    && CardObjectHasTag(cardObjects, defenderObjectId, CardObjectTags.UnitCard)) ?? string.Empty;
                if (TryResolveLeblancLegendImageTrigger(
                        playerZones,
                        cardObjects,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        leblancCopySourceObjectId,
                        "BATTLEFIELD_HELD_CREATE_IMAGE",
                        out var leblancHeldEvents))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(leblancHeldEvents);
                }

                if (TryResolveIvernLegendBrushTrigger(
                        playerZones,
                        cardObjects,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        "BATTLEFIELD_HELD_REPLACE_WITH_BRUSH",
                        out var ivernHeldEvents))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    combatEvents.AddRange(ivernHeldEvents);
                }

                if (TryGetActiveVexLegend(playerZones, cardObjects, battleWinnerPlayerId, out var vexLegendObjectId, out var vexLegendState))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    cardObjects[vexLegendObjectId] = vexLegendState with
                    {
                        IsExhausted = true
                    };
                    combatEvents.Add(new GameEvent(
                        "LEGEND_TRIGGER_RESOLVED",
                        $"{battleWinnerPlayerId} 的愁云使者因据守战场触发",
                        new Dictionary<string, object?>
                        {
                            ["playerId"] = battleWinnerPlayerId,
                            ["legendObjectId"] = vexLegendObjectId,
                            ["legendCardNo"] = vexLegendState.CardNo,
                            ["trigger"] = "BATTLEFIELD_HELD_DRAW_ONE",
                            ["sourceObjectId"] = attackerObjectId,
                            ["battlefieldId"] = battlefieldId
                        }));
                    combatEvents.Add(new GameEvent(
                        "LEGEND_EXHAUSTED",
                        $"{vexLegendObjectId} 变为休眠状态",
                        new Dictionary<string, object?>
                        {
                            ["playerId"] = battleWinnerPlayerId,
                            ["sourceObjectId"] = vexLegendObjectId,
                            ["reason"] = "BATTLEFIELD_HELD_DRAW_ONE"
                        }));
                    var vexDrawApplication = ApplyDrawToPlayer(
                        state,
                        playerZones,
                        playerScores,
                        battleWinnerPlayerId,
                        1,
                        rngCursor,
                        combatEvents);
                    playerScores = vexDrawApplication.PlayerScores;
                    winnerPlayerId = vexDrawApplication.WinnerPlayerId;
                    rngCursor = vexDrawApplication.RngCursor;
                }

                if (TryGetActiveRenataLegend(playerZones, cardObjects, battleWinnerPlayerId, out var renataLegendObjectId, out var renataLegendState))
                {
                    AddBattlefieldHeldEventIfNeeded(
                        combatEvents,
                        ref battlefieldHeldEventEmitted,
                        battleWinnerPlayerId,
                        battlefieldId,
                        attackerObjectId,
                        defenderObjectIds);
                    cardObjects[renataLegendObjectId] = renataLegendState with
                    {
                        IsExhausted = true
                    };
                    var renataGoldBonusActive = PlayerWithinWinningScoreDistance(
                        playerScores,
                        EffectiveWinningScore(playerZones, cardObjects),
                        battleWinnerPlayerId,
                        RenataGoldBonusWinningScoreDistance);
                    combatEvents.Add(new GameEvent(
                        "LEGEND_TRIGGER_RESOLVED",
                        $"{battleWinnerPlayerId} 的炼金男爵因据守战场触发",
                        new Dictionary<string, object?>
                        {
                            ["playerId"] = battleWinnerPlayerId,
                            ["legendObjectId"] = renataLegendObjectId,
                            ["legendCardNo"] = renataLegendState.CardNo,
                            ["trigger"] = "BATTLEFIELD_HELD_CREATE_GOLD",
                            ["sourceObjectId"] = attackerObjectId,
                            ["battlefieldId"] = battlefieldId,
                            ["renataGoldExtraManaActive"] = renataGoldBonusActive
                        }));
                    combatEvents.Add(new GameEvent(
                        "LEGEND_EXHAUSTED",
                        $"{renataLegendObjectId} 变为休眠状态",
                        new Dictionary<string, object?>
                        {
                            ["playerId"] = battleWinnerPlayerId,
                            ["sourceObjectId"] = renataLegendObjectId,
                            ["reason"] = "BATTLEFIELD_HELD_CREATE_GOLD"
                        }));
                    CreateLegendEquipmentToken(
                        playerZones,
                        cardObjects,
                        battleWinnerPlayerId,
                        renataLegendObjectId,
                        "LEGEND_TRIGGER_BATTLEFIELD_HELD_CREATE_GOLD",
                        "金币",
                        renataGoldBonusActive
                            ? [CardObjectTags.EquipmentCard, "金币", "反应", RenataGoldBonusTag]
                            : [CardObjectTags.EquipmentCard, "金币", "反应"],
                        isExhausted: true,
                        combatEvents);
                }
            }

            if (TryGetDravenLegendCardNo(playerZones, cardObjects, battleWinnerPlayerId, out var dravenLegendCardNo))
            {
                combatEvents.Add(new GameEvent(
                    "LEGEND_TRIGGER_RESOLVED",
                    $"{battleWinnerPlayerId} 的荣耀行刑官因赢得战斗触发",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = battleWinnerPlayerId,
                        ["legendCardNo"] = dravenLegendCardNo,
                        ["trigger"] = "BATTLE_WON_DRAW_ONE",
                        ["sourceObjectId"] = attackerObjectId,
                        ["attackerObjectId"] = attackerObjectId,
                        ["defenderObjectIds"] = defenderObjectIds.ToArray()
                    }));
                var drawApplication = ApplyDrawToPlayer(
                    state,
                    playerZones,
                    playerScores,
                    battleWinnerPlayerId,
                    1,
                    rngCursor,
                    combatEvents);
                playerScores = drawApplication.PlayerScores;
                winnerPlayerId = drawApplication.WinnerPlayerId;
                rngCursor = drawApplication.RngCursor;
            }
        }
        else
        {
            combatEvents.Add(BuildBattleNoResultEvent(
                playerZones,
                cardObjects,
                battlefieldId,
                attackerObjectIds,
                defenderObjectIds,
                intent.PlayerId,
                defendingPlayerId));
        }

        var objectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones);
        CloseResolvedBattle(cardObjects, battlefieldId, attackerObjectIds, defenderObjectIds, combatEvents);
        combatEvents.AddRange(ResolveBattlefieldControlAfterBattle(
            playerZones,
            cardObjects,
            objectLocations,
            battlefieldId,
            resolvedBattleWinnerPlayerId));
        objectLocations = ReconcileObjectLocations(objectLocations, playerZones);
        var battlefieldResolutions = AppendBattlefieldResolutionEvents(
            state.BattlefieldResolutions,
            combatEvents,
            state.Tick + 1);
        var battleResolutions = AppendBattleResolutionEvents(
            state.BattleResolutions,
            combatEvents,
            state.Tick + 1,
            battlefieldId,
            intent.PlayerId,
            defendingPlayerId,
            resolvedBattleWinnerPlayerId,
            attackerObjectIds,
            defenderObjectIds,
            playerZones,
            cardObjects);
        var nextState = state with
        {
            Tick = state.Tick + 1,
            PlayerZones = playerZones,
            ObjectLocations = objectLocations,
            BattlefieldResolutions = battlefieldResolutions,
            BattleResolutions = battleResolutions,
            PlayerScores = playerScores,
            CardObjects = cardObjects,
            PlayerExperience = playerExperience,
            RunePools = runePools,
            RngCursor = rngCursor,
            UntilEndOfTurnEffects = untilEndOfTurnEffects,
            PassedPriorityPlayerIds = [],
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(
                state.DestroyedUnitOwnerIdsThisTurn,
                lethalCleanup.DestroyedUnitOwnerIds),
            Status = winnerPlayerId is null ? state.Status : MatchStatuses.Finished,
            WinnerPlayerId = winnerPlayerId ?? state.WinnerPlayerId
        };
        var events = new List<GameEvent>
        {
            new(
                "BATTLE_DECLARED",
                $"{intent.PlayerId} 声明战斗",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["battlefieldId"] = battlefieldId,
                    ["attackerObjectIds"] = attackerObjectIds.ToArray(),
                    ["defenderObjectIds"] = defenderObjectIds.ToArray(),
                    ["optionalCosts"] = optionalCosts.ToArray(),
                    ["battlefieldTargetObjectIds"] = new[]
                        {
                            battlefieldSteadfastObjectId,
                            battlefieldDefenderMoveObjectId
                        }
                        .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                        .ToArray()
                })
        };
        events.AddRange(combatEvents);

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static void CloseResolvedBattle(
        Dictionary<string, CardObjectState> cardObjects,
        string battlefieldId,
        IReadOnlyList<string> attackerObjectIds,
        IReadOnlyList<string> defenderObjectIds,
        List<GameEvent> events)
    {
        var participantObjectIds = attackerObjectIds
            .Concat(defenderObjectIds)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var clearedObjectIds = new List<string>();
        foreach (var objectId in participantObjectIds)
        {
            if (!cardObjects.TryGetValue(objectId, out var cardObject)
                || (!cardObject.IsAttacking && !cardObject.IsDefending))
            {
                continue;
            }

            cardObjects[objectId] = cardObject with
            {
                IsAttacking = false,
                IsDefending = false
            };
            clearedObjectIds.Add(objectId);
        }

        if (clearedObjectIds.Count == 0)
        {
            return;
        }

        events.Add(new GameEvent(
            "BATTLE_CLOSED",
            "战斗结算完成",
            new Dictionary<string, object?>
            {
                ["battlefieldId"] = battlefieldId,
                ["participantObjectIds"] = participantObjectIds,
                ["clearedObjectIds"] = clearedObjectIds.ToArray()
            }));
    }

    private static IReadOnlyList<GameEvent> ResolveBattlefieldControlAfterBattle(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        string battlefieldId,
        string? battleWinnerPlayerId)
    {
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState))
        {
            return [];
        }

        var occupantControllerIds = objectLocations
            .Where(entry => string.Equals(entry.Value.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
                && string.Equals(entry.Value.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
                && !string.Equals(entry.Key, battlefieldObjectId, StringComparison.Ordinal)
                && IsObjectOnField(playerZones, entry.Key)
                && cardObjects.TryGetValue(entry.Key, out var cardObject)
                && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !cardObject.IsFaceDown
                && !cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)
                && !string.IsNullOrWhiteSpace(cardObject.ControllerId))
            .Select(entry => cardObjects[entry.Key].ControllerId!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(controllerId => controllerId, StringComparer.Ordinal)
            .ToArray();
        if (occupantControllerIds.Length > 1)
        {
            return [];
        }

        var previousControllerId = battlefieldState.ControllerId;
        var nextControllerId = occupantControllerIds.Length == 1 ? occupantControllerIds[0] : null;
        var changed = !string.Equals(previousControllerId, nextControllerId, StringComparison.Ordinal);
        if (changed)
        {
            cardObjects[battlefieldObjectId] = battlefieldState with
            {
                ControllerId = nextControllerId
            };
        }

        var resolution = nextControllerId is null
            ? "UNCONTROLLED"
            : changed ? "CONTROL_CHANGED" : "CONTROL_CONFIRMED";
        var description = nextControllerId is null
            ? "战场变为未受控制"
            : changed
                ? $"{nextControllerId} 确立战场控制"
                : $"{nextControllerId} 保持战场控制";

        var events = new List<GameEvent>
        {
            new GameEvent(
                "BATTLEFIELD_CONTROL_RESOLVED",
                description,
                new Dictionary<string, object?>
                {
                    ["playerId"] = nextControllerId,
                    ["battlefieldId"] = battlefieldId,
                    ["battlefieldObjectId"] = battlefieldObjectId,
                    ["previousControllerId"] = previousControllerId,
                    ["controllerId"] = nextControllerId,
                    ["changed"] = changed,
                    ["resolution"] = resolution,
                    ["battleWinnerPlayerId"] = battleWinnerPlayerId,
                    ["occupantControllerIds"] = occupantControllerIds
                })
        };
        events.AddRange(RemoveIllegalStandbyAfterBattlefieldControl(
            playerZones,
            cardObjects,
            objectLocations,
            battlefieldObjectId,
            nextControllerId));
        return events;
    }

    private static IReadOnlyList<GameEvent> RemoveIllegalStandbyAfterBattlefieldControl(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        string battlefieldObjectId,
        string? battlefieldControllerId)
    {
        var removed = new List<Dictionary<string, object?>>();
        var standbyObjectIds = objectLocations
            .Where(entry => string.Equals(entry.Value.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
                && string.Equals(entry.Value.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
                && !string.Equals(entry.Key, battlefieldObjectId, StringComparison.Ordinal)
                && IsObjectOnField(playerZones, entry.Key)
                && cardObjects.TryGetValue(entry.Key, out var cardObject)
                && (cardObject.IsFaceDown || cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal))
                && !string.Equals(cardObject.ControllerId, battlefieldControllerId, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        foreach (var objectId in standbyObjectIds)
        {
            if (!cardObjects.TryGetValue(objectId, out var cardObject))
            {
                continue;
            }

            var ownerPlayerId = cardObject.OwnerId;
            if (string.IsNullOrWhiteSpace(ownerPlayerId)
                && objectLocations.TryGetValue(objectId, out var location))
            {
                ownerPlayerId = location.PlayerId;
            }
            if (string.IsNullOrWhiteSpace(ownerPlayerId)
                || !playerZones.ContainsKey(ownerPlayerId))
            {
                continue;
            }

            foreach (var playerId in playerZones.Keys.ToArray())
            {
                var zones = playerZones[playerId];
                playerZones[playerId] = zones with
                {
                    Base = RemoveFromZone(zones.Base, objectId),
                    Battlefields = RemoveFromZone(zones.Battlefields, objectId)
                };
            }

            var ownerZones = playerZones[ownerPlayerId];
            playerZones[ownerPlayerId] = ownerZones with
            {
                Graveyard = ownerZones.Graveyard.Contains(objectId, StringComparer.Ordinal)
                    ? ownerZones.Graveyard
                    : ownerZones.Graveyard.Concat([objectId]).ToArray()
            };
            cardObjects[objectId] = cardObject with
            {
                Damage = 0,
                IsFaceDown = false,
                IsAttacking = false,
                IsDefending = false,
                ControllerId = ownerPlayerId
            };
            removed.Add(new Dictionary<string, object?>
            {
                ["objectId"] = objectId,
                ["ownerPlayerId"] = ownerPlayerId,
                ["previousControllerId"] = cardObject.ControllerId,
                ["destinationZone"] = "GRAVEYARD"
            });
        }

        if (removed.Count == 0)
        {
            return [];
        }

        return
        [
            new GameEvent(
                "BATTLEFIELD_STANDBY_REMOVED",
                "战场控制结算清理待命牌",
                new Dictionary<string, object?>
                {
                    ["battlefieldObjectId"] = battlefieldObjectId,
                    ["controllerId"] = battlefieldControllerId,
                    ["removedObjectIds"] = removed.Select(item => item["objectId"]).ToArray(),
                    ["removedCards"] = removed,
                    ["reason"] = "BATTLEFIELD_CONTROL_CLEANUP"
                })
        ];
    }

    private static IReadOnlyList<BattlefieldResolutionState> AppendBattlefieldResolutionEvents(
        IReadOnlyList<BattlefieldResolutionState> existing,
        IReadOnlyList<GameEvent> events,
        long tick)
    {
        var newResolutions = new List<BattlefieldResolutionState>();
        for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            var gameEvent = events[eventIndex];
            var kind = gameEvent.Kind switch
            {
                "BATTLEFIELD_HELD" => "HELD",
                "BATTLEFIELD_CONQUERED" => "CONQUERED",
                "BATTLEFIELD_CONTROL_RESOLVED" => "CONTROL_RESOLVED",
                _ => string.Empty
            };
            if (string.IsNullOrWhiteSpace(kind))
            {
                continue;
            }

            var battlefieldObjectId = EventPayloadString(gameEvent, "battlefieldObjectId")
                ?? EventPayloadString(gameEvent, "battlefieldId")
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(battlefieldObjectId))
            {
                continue;
            }

            var playerId = EventPayloadString(gameEvent, "playerId")
                ?? EventPayloadString(gameEvent, "controllerId");
            var sourceObjectId = EventPayloadString(gameEvent, "sourceObjectId");
            var participantObjectIds = EventPayloadStringList(gameEvent, "defenderObjectIds")
                .Concat(EventPayloadStringList(gameEvent, "defeatedObjectIds"))
                .Concat(sourceObjectId is null ? Array.Empty<string>() : new[] { sourceObjectId })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var reason = EventPayloadString(gameEvent, "resolution")
                ?? EventPayloadString(gameEvent, "trigger")
                ?? gameEvent.Kind;

            newResolutions.Add(new BattlefieldResolutionState(
                $"battlefield-result:{tick}:{eventIndex}:{kind}:{battlefieldObjectId}",
                tick,
                kind,
                reason,
                battlefieldObjectId,
                playerId,
                EventPayloadString(gameEvent, "previousControllerId"),
                EventPayloadString(gameEvent, "controllerId"),
                sourceObjectId,
                participantObjectIds,
                [gameEvent.Kind]));
        }

        if (newResolutions.Count == 0)
        {
            return existing;
        }

        return newResolutions
            .Concat(existing)
            .GroupBy(resolution => resolution.ResolutionId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(12)
            .ToArray();
    }

    private static IReadOnlyList<BattleResolutionState> AppendBattleResolutionEvents(
        IReadOnlyList<BattleResolutionState> existing,
        IReadOnlyList<GameEvent> combatEvents,
        long tick,
        string battlefieldId,
        string attackingPlayerId,
        string? defendingPlayerId,
        string? winnerPlayerId,
        IReadOnlyList<string> attackerObjectIds,
        IReadOnlyList<string> defenderObjectIds,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects)
    {
        var noResultEvent = combatEvents.FirstOrDefault(gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_NO_RESULT", StringComparison.Ordinal));
        var closedEvent = combatEvents.FirstOrDefault(gameEvent =>
            string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal));
        if (noResultEvent is null && closedEvent is null)
        {
            return existing;
        }

        var destroyedObjectIds = combatEvents
            .Where(gameEvent => string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal))
            .Select(gameEvent => EventPayloadString(gameEvent, "targetObjectId"))
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Select(objectId => objectId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var destroyedObjectIdSet = destroyedObjectIds.ToHashSet(StringComparer.Ordinal);
        var survivingAttackerObjectIds = noResultEvent is null
            ? attackerObjectIds
                .Where(objectId => !destroyedObjectIdSet.Contains(objectId)
                    && cardObjects.ContainsKey(objectId)
                    && IsObjectOnField(playerZones, objectId))
                .ToArray()
            : EventPayloadStringList(noResultEvent, "survivingAttackerObjectIds");
        var survivingDefenderObjectIds = noResultEvent is null
            ? defenderObjectIds
                .Where(objectId => !destroyedObjectIdSet.Contains(objectId)
                    && cardObjects.ContainsKey(objectId)
                    && IsObjectOnField(playerZones, objectId))
                .ToArray()
            : EventPayloadStringList(noResultEvent, "survivingDefenderObjectIds");
        var kind = noResultEvent is null ? "CLOSED" : "NO_RESULT";
        var reason = noResultEvent is null
            ? "BATTLE_CLOSED"
            : EventPayloadString(noResultEvent, "reason") ?? "BATTLE_NO_RESULT";
        var resolution = new BattleResolutionState(
            $"battle-result:{tick}:{kind}:{battlefieldId}:{attackerObjectIds.FirstOrDefault() ?? attackingPlayerId}",
            tick,
            kind,
            reason,
            battlefieldId,
            attackingPlayerId,
            defendingPlayerId,
            winnerPlayerId,
            attackerObjectIds.ToArray(),
            defenderObjectIds.ToArray(),
            survivingAttackerObjectIds,
            survivingDefenderObjectIds,
            destroyedObjectIds,
            combatEvents
                .Where(gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal)
                    || string.Equals(gameEvent.Kind, "UNIT_DESTROYED", StringComparison.Ordinal)
                    || string.Equals(gameEvent.Kind, "BATTLE_CLOSED", StringComparison.Ordinal)
                    || string.Equals(gameEvent.Kind, "BATTLE_NO_RESULT", StringComparison.Ordinal))
                .Select(gameEvent => gameEvent.Kind)
                .Distinct(StringComparer.Ordinal)
                .ToArray());

        return new[] { resolution }
            .Concat(existing)
            .GroupBy(item => item.ResolutionId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(12)
            .ToArray();
    }

    private static string? EventPayloadString(GameEvent gameEvent, string key)
    {
        if (!gameEvent.Payload.TryGetValue(key, out var value))
        {
            return null;
        }

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            _ => null
        };
    }

    private static IReadOnlyList<string> EventPayloadStringList(GameEvent gameEvent, string key)
    {
        if (!gameEvent.Payload.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        if (value is IEnumerable<string> strings)
        {
            return strings
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();
        }

        if (value is IEnumerable<object?> objects)
        {
            return objects
                .OfType<string>()
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();
        }

        return value is string text && !string.IsNullOrWhiteSpace(text) ? [text] : [];
    }

    private static bool TryBuildMinimalDeclareBattle(
        MatchState state,
        PlayerIntent intent,
        DeclareBattleCommand command,
        out IReadOnlyList<string> attackerObjectIds,
        out IReadOnlyList<string> defenderObjectIds,
        out IReadOnlyList<string> optionalCosts)
    {
        attackerObjectIds = [];
        defenderObjectIds = [];
        optionalCosts = NormalizeOptionalCosts(command.OptionalCosts);

        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.ActivePlayerId, intent.PlayerId, StringComparison.Ordinal)
            || state.StackItems.Count > 0)
        {
            return false;
        }

        if (!IsSupportedDeclareBattlefieldId(state, intent.PlayerId, command.BattlefieldId?.Trim()))
        {
            return false;
        }

        if (optionalCosts.Count != 1
            || !string.Equals(optionalCosts[0], DeclareBattleOptionalCost, StringComparison.Ordinal))
        {
            return false;
        }

        var normalizedAttackerObjectIds = NormalizeTargetObjectIds(command.AttackerObjectIds ?? []);
        var normalizedDefenderObjectIds = NormalizeTargetObjectIds(command.DefenderObjectIds ?? []);
        if (normalizedAttackerObjectIds.Count is < 1 or > 2
            || normalizedDefenderObjectIds.Count is < 1 or > 2
            || HasDuplicateObjectIds(normalizedAttackerObjectIds)
            || HasDuplicateObjectIds(normalizedDefenderObjectIds)
            || normalizedAttackerObjectIds.Any(attackingObjectId =>
                normalizedDefenderObjectIds.Contains(attackingObjectId, StringComparer.Ordinal)))
        {
            return false;
        }

        attackerObjectIds = normalizedAttackerObjectIds;
        defenderObjectIds = normalizedDefenderObjectIds;

        foreach (var attackingObjectId in attackerObjectIds)
        {
            var attackerLocation = FindFieldObjectLocation(state.PlayerZones, attackingObjectId);
            if (attackerLocation is null
                || !string.Equals(attackerLocation.Value.PlayerId, intent.PlayerId, StringComparison.Ordinal)
                || !string.Equals(attackerLocation.Value.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal))
            {
                return false;
            }

            if (!state.CardObjects.TryGetValue(attackingObjectId, out var attackerState)
                || !SourceObjectControlledByPlayerOrLegacyOwned(attackerState, intent.PlayerId)
                || !IsReadyFaceUpUnitForMinimalBattle(attackerState))
            {
                return false;
            }
        }

        var hasAssignmentOrderingKeyword = false;
        foreach (var defenderObjectId in defenderObjectIds)
        {
            var defenderLocation = FindFieldObjectLocation(state.PlayerZones, defenderObjectId);
            if (defenderLocation is null
                || string.Equals(defenderLocation.Value.PlayerId, intent.PlayerId, StringComparison.Ordinal)
                || !string.Equals(defenderLocation.Value.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
                || !state.CardObjects.TryGetValue(defenderObjectId, out var defenderState)
                || !SourceObjectControlledByPlayerOrLegacyOwned(defenderState, defenderLocation.Value.PlayerId)
                || !IsReadyFaceUpUnitForMinimalBattle(defenderState))
            {
                return false;
            }

            hasAssignmentOrderingKeyword |= HasBattleDamageAssignmentKeyword(defenderState.Tags);
        }

        return defenderObjectIds.Count == 1 || hasAssignmentOrderingKeyword;
    }

    private static bool IsSupportedDeclareBattlefieldId(MatchState state, string playerId, string? battlefieldId)
    {
        if (string.Equals(
                battlefieldId,
                $"{DeclareBattleBattlefieldPrefix}{playerId}-MAIN",
                StringComparison.Ordinal))
        {
            return true;
        }

        return TryGetBattlefieldCardObject(state.PlayerZones, state.CardObjects, battlefieldId, out _, out var battlefieldState)
            && !string.IsNullOrWhiteSpace(battlefieldState.CardNo);
    }

    private static bool DeclareBattleMatchesActiveStartBattleTask(
        MatchState state,
        string battlefieldObjectId,
        string commandBattlefieldId,
        IReadOnlyList<string> attackerObjectIds,
        IReadOnlyList<string> defenderObjectIds)
    {
        return string.Equals(commandBattlefieldId, battlefieldObjectId, StringComparison.Ordinal)
            && attackerObjectIds.All(attackerObjectId => IsObjectLocatedAtBattlefield(state, attackerObjectId, battlefieldObjectId))
            && defenderObjectIds.All(defenderObjectId => IsObjectLocatedAtBattlefield(state, defenderObjectId, battlefieldObjectId));
    }

    private static bool IsObjectLocatedAtBattlefield(
        MatchState state,
        string objectId,
        string battlefieldObjectId)
    {
        return state.ObjectLocations.TryGetValue(objectId, out var location)
            && string.Equals(location.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            && string.Equals(location.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal);
    }

    private static int ResolveBattleCombatPower(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        CardObjectState cardObject,
        bool isAttacking,
        int defendingUnitCount,
        string? defendingPlayerId,
        string battlefieldId,
        string? battlefieldSteadfastObjectId,
        out int keywordBonus,
        out int staticPowerBonus)
    {
        keywordBonus = CombatKeywordAmount(
            cardObject.Tags,
            isAttacking ? CardCombatKeywordNames.Assault : CardCombatKeywordNames.Steadfast);
        staticPowerBonus = 0;
        if (isAttacking)
        {
            keywordBonus += CountLucianLegendEquipmentAssaultBonus(state, playerZones, objectId);
            staticPowerBonus += ResolveAhriLegendAttackPowerPenalty(
                state,
                playerZones,
                cardObject,
                keywordBonus,
                defendingPlayerId);
        }

        if (!isAttacking && HasRumbleLegendMechanicalSteadfastBonus(state, playerZones, objectId, cardObject))
        {
            keywordBonus += 1;
        }

        if (!isAttacking && HasBattlefieldEphemeralSteadfastBonus(state, playerZones, battlefieldId, cardObject))
        {
            keywordBonus += 1;
        }

        if (!isAttacking && HasBattlefieldIsolatedDefenderSteadfastPenalty(state, playerZones, battlefieldId, cardObject, defendingUnitCount))
        {
            keywordBonus -= 2;
        }

        if (!isAttacking
            && string.Equals(objectId, battlefieldSteadfastObjectId, StringComparison.Ordinal))
        {
            keywordBonus += 2;
        }

        if (!isAttacking && HasMasterYiSingleDefenderBonus(state, playerZones, objectId, defendingUnitCount))
        {
            staticPowerBonus += 2;
        }

        staticPowerBonus += ResolveMasterYiLevelLegendPowerBonus(state, playerZones, objectId);
        staticPowerBonus += ResolveBattlefieldAllUnitsPowerBonus(state, playerZones, battlefieldId, cardObject);

        return Math.Max(0, cardObject.Power + keywordBonus + staticPowerBonus);
    }

    private static int ResolveBattlefieldAllUnitsPowerBonus(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string battlefieldId,
        CardObjectState cardObject)
    {
        return cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && TryGetBattlefieldCardObject(playerZones, state.CardObjects, battlefieldId, out _, out var battlefieldState)
            && IsBattlefieldAllUnitsPowerPlusOneCardNo(battlefieldState.CardNo)
            ? 1
            : 0;
    }

    private static bool HasBattlefieldEphemeralSteadfastBonus(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string battlefieldId,
        CardObjectState cardObject)
    {
        return cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && cardObject.Tags.Contains(CardObjectTags.Ephemeral, StringComparer.Ordinal)
            && TryGetBattlefieldCardObject(playerZones, state.CardObjects, battlefieldId, out _, out var battlefieldState)
            && IsBattlefieldEphemeralUnitsSteadfastCardNo(battlefieldState.CardNo);
    }

    private static bool HasBattlefieldIsolatedDefenderSteadfastPenalty(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string battlefieldId,
        CardObjectState cardObject,
        int defendingUnitCount)
    {
        return defendingUnitCount == 1
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && TryGetBattlefieldCardObject(playerZones, state.CardObjects, battlefieldId, out _, out var battlefieldState)
            && IsBattlefieldIsolatedDefenderSteadfastMinusTwoCardNo(battlefieldState.CardNo);
    }

    private static string? ResolveSingleDefendingPlayerId(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyList<string> defenderObjectIds)
    {
        string? defendingPlayerId = null;
        foreach (var defenderObjectId in defenderObjectIds)
        {
            var location = FindFieldObjectLocation(playerZones, defenderObjectId);
            if (location is null)
            {
                return null;
            }

            if (defendingPlayerId is null)
            {
                defendingPlayerId = location.Value.PlayerId;
                continue;
            }

            if (!string.Equals(defendingPlayerId, location.Value.PlayerId, StringComparison.Ordinal))
            {
                return null;
            }
        }

        return defendingPlayerId;
    }

    private static int ResolveAhriLegendAttackPowerPenalty(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        CardObjectState attackerState,
        int attackerKeywordBonus,
        string? defendingPlayerId)
    {
        if (string.IsNullOrWhiteSpace(defendingPlayerId)
            || !playerZones.TryGetValue(defendingPlayerId, out var defenderZones)
            || !defenderZones.LegendZone.Any(legendObjectId =>
                state.CardObjects.TryGetValue(legendObjectId, out var legendState)
                && IsAhriLegendCardNo(legendState.CardNo)))
        {
            return 0;
        }

        return attackerState.Power + attackerKeywordBonus > 1 ? -1 : 0;
    }

    private static bool IsAhriLegendCardNo(string? cardNo)
    {
        return cardNo is AhriLegendCardNo or "OGN·303/298" or "OGN·303*/298";
    }

    private static int CountLucianLegendEquipmentAssaultBonus(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId)
    {
        var location = FindFieldObjectLocation(playerZones, objectId);
        if (location is null
            || !playerZones.TryGetValue(location.Value.PlayerId, out var zones)
            || !zones.LegendZone.Any(legendObjectId =>
                state.CardObjects.TryGetValue(legendObjectId, out var legendState)
                && IsLucianLegendCardNo(legendState.CardNo)))
        {
            return 0;
        }

        return AttachedEquipmentObjectIds(state.CardObjects, objectId)
            .Count(equipmentObjectId =>
            {
                var equipmentLocation = FindFieldObjectLocation(playerZones, equipmentObjectId);
                return equipmentLocation is not null
                    && string.Equals(equipmentLocation.Value.PlayerId, location.Value.PlayerId, StringComparison.Ordinal)
                    && state.CardObjects.TryGetValue(equipmentObjectId, out var equipmentState)
                    && equipmentState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal);
            });
    }

    private static bool IsLucianLegendCardNo(string? cardNo)
    {
        return cardNo is LucianLegendCardNo or "SFD·241/221";
    }

    private static bool HasMasterYiSingleDefenderBonus(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        int defendingUnitCount)
    {
        if (defendingUnitCount != 1)
        {
            return false;
        }

        var location = FindFieldObjectLocation(playerZones, objectId);
        if (location is null || !playerZones.TryGetValue(location.Value.PlayerId, out var zones))
        {
            return false;
        }

        return zones.LegendZone.Any(legendObjectId =>
            state.CardObjects.TryGetValue(legendObjectId, out var legendState)
            && string.Equals(legendState.CardNo, MasterYiIntroLegendCardNo, StringComparison.Ordinal));
    }

    private static int ResolveMasterYiLevelLegendPowerBonus(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId)
    {
        var location = FindFieldObjectLocation(playerZones, objectId);
        if (location is null)
        {
            return 0;
        }

        return ControllerHasMasterYiLevelLegend(
            playerZones,
            state.CardObjects,
            location.Value.PlayerId,
            state.PlayerExperience,
            MasterYiLevelPowerThreshold)
            ? 1
            : 0;
    }

    private static bool ControllerHasMasterYiLevelLegend(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        IReadOnlyDictionary<string, int> playerExperience,
        int experienceThreshold)
    {
        return playerExperience.TryGetValue(playerId, out var experience)
            && experience >= experienceThreshold
            && playerZones.TryGetValue(playerId, out var zones)
            && zones.LegendZone.Any(legendObjectId =>
                cardObjects.TryGetValue(legendObjectId, out var legendState)
                && IsMasterYiLevelLegendCardNo(legendState.CardNo));
    }

    private static bool IsMasterYiLevelLegendCardNo(string? cardNo)
    {
        return cardNo is MasterYiLevelLegendCardNo or "UNL-231/219" or "UNL-231*/219";
    }

    private static GameEvent BuildBattleNoResultEvent(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string battlefieldId,
        IReadOnlyList<string> attackerObjectIds,
        IReadOnlyList<string> defenderObjectIds,
        string attackingPlayerId,
        string? defendingPlayerId)
    {
        var survivingAttackerObjectIds = SurvivingBattleUnitObjectIds(playerZones, cardObjects, attackerObjectIds);
        var survivingDefenderObjectIds = SurvivingBattleUnitObjectIds(playerZones, cardObjects, defenderObjectIds);
        var reason = survivingAttackerObjectIds.Length == 0 && survivingDefenderObjectIds.Length == 0
            ? "ALL_PARTICIPANTS_DESTROYED"
            : "BOTH_SIDES_RETAIN_UNITS";

        return new GameEvent(
            "BATTLE_NO_RESULT",
            "战斗没有胜者",
            new Dictionary<string, object?>
            {
                ["battlefieldId"] = battlefieldId,
                ["attackingPlayerId"] = attackingPlayerId,
                ["defendingPlayerId"] = defendingPlayerId,
                ["attackerObjectIds"] = attackerObjectIds.ToArray(),
                ["defenderObjectIds"] = defenderObjectIds.ToArray(),
                ["survivingAttackerObjectIds"] = survivingAttackerObjectIds,
                ["survivingDefenderObjectIds"] = survivingDefenderObjectIds,
                ["reason"] = reason
            });
    }

    private static string[] SurvivingBattleUnitObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyList<string> objectIds)
    {
        return objectIds
            .Where(objectId => cardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && IsObjectOnField(playerZones, objectId))
            .ToArray();
    }

    private static bool TryResolveBattleWinnerPlayerId(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyList<string> attackerObjectIds,
        IReadOnlyList<string> defenderObjectIds,
        string? defendingPlayerId,
        string attackingPlayerId,
        out string winnerPlayerId)
    {
        winnerPlayerId = string.Empty;
        var attackerSurvived = attackerObjectIds.Any(attackerObjectId =>
            cardObjects.TryGetValue(attackerObjectId, out var attackerState)
            && attackerState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && IsObjectOnField(playerZones, attackerObjectId));
        var anyDefenderSurvived = defenderObjectIds.Any(defenderObjectId =>
            cardObjects.TryGetValue(defenderObjectId, out var defenderState)
            && defenderState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && IsObjectOnField(playerZones, defenderObjectId));

        if (attackerSurvived == anyDefenderSurvived)
        {
            return false;
        }

        if (attackerSurvived)
        {
            winnerPlayerId = attackingPlayerId;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(defendingPlayerId))
        {
            winnerPlayerId = defendingPlayerId;
            return true;
        }

        return false;
    }

    private static bool TryGetDravenLegendCardNo(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string cardNo)
    {
        cardNo = DravenLegendCardNo;
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var legendObjectId in zones.LegendZone)
        {
            if (cardObjects.TryGetValue(legendObjectId, out var legendState)
                && IsDravenLegendCardNo(legendState.CardNo))
            {
                cardNo = legendState.CardNo ?? DravenLegendCardNo;
                return true;
            }
        }

        return false;
    }

    private static bool IsDravenLegendCardNo(string? cardNo)
    {
        return cardNo is DravenLegendCardNo or "SFD·242/221";
    }

    private static bool TryGetGarenIntroLegendCardNo(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string cardNo)
    {
        cardNo = GarenIntroLegendCardNo;
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var legendObjectId in zones.LegendZone)
        {
            if (cardObjects.TryGetValue(legendObjectId, out var legendState)
                && string.Equals(legendState.CardNo, GarenIntroLegendCardNo, StringComparison.Ordinal))
            {
                cardNo = legendState.CardNo ?? GarenIntroLegendCardNo;
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<GameEvent> ResolveViLegendOverkillConquerTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string attackerObjectId,
        int assignedOverkillDamageToEnemyUnits)
    {
        if (assignedOverkillDamageToEnemyUnits < ViLegendOverkillThreshold
            || !TryGetActiveViLegend(
                playerZones,
                cardObjects,
                playerId,
                out var legendObjectId,
                out var legendState)
            || !TryGetViLegendReadyTarget(
                playerZones,
                cardObjects,
                playerId,
                out var readyTargetObjectId,
                out var readyTargetState))
        {
            return [];
        }

        cardObjects[legendObjectId] = legendState with
        {
            IsExhausted = true
        };
        cardObjects[readyTargetObjectId] = readyTargetState with
        {
            IsExhausted = false
        };

        return
        [
            new GameEvent(
                "LEGEND_TRIGGER_RESOLVED",
                $"{playerId} 的皮城执法官因过量伤害征服触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["legendObjectId"] = legendObjectId,
                    ["legendCardNo"] = legendState.CardNo,
                    ["trigger"] = "OVERKILL_CONQUER_READY_UNIT",
                    ["sourceObjectId"] = attackerObjectId,
                    ["battlefieldId"] = battlefieldId,
                    ["assignedOverkillDamageToEnemyUnits"] = assignedOverkillDamageToEnemyUnits,
                    ["readyTargetObjectId"] = readyTargetObjectId
                }),
            new GameEvent(
                "LEGEND_EXHAUSTED",
                $"{legendObjectId} 变为休眠状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["reason"] = "OVERKILL_CONQUER_READY_UNIT"
                }),
            new GameEvent(
                "UNIT_READIED",
                $"{readyTargetObjectId} 变为活跃状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["targetObjectId"] = readyTargetObjectId,
                    ["wasExhausted"] = true,
                    ["isExhausted"] = false,
                    ["reason"] = "OVERKILL_CONQUER_READY_UNIT"
                })
        ];
    }

    private static void ReadyLegendFriendlyUnit(
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        string playerId,
        string legendObjectId,
        string abilityId,
        List<GameEvent> events)
    {
        if (!cardObjects.TryGetValue(targetObjectId, out var targetState))
        {
            return;
        }

        var wasExhausted = targetState.IsExhausted;
        cardObjects[targetObjectId] = targetState with
        {
            IsExhausted = false
        };
        events.Add(new GameEvent(
            "UNIT_READIED",
            $"{targetObjectId} 变为活跃状态",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = legendObjectId,
                ["targetObjectId"] = targetObjectId,
                ["wasExhausted"] = wasExhausted,
                ["isExhausted"] = false,
                ["abilityId"] = abilityId,
                ["reason"] = "FRIENDLY_UNIT_TARGETED_READY"
            }));
    }

    private static (IReadOnlyDictionary<string, RunePool> RunePools, IReadOnlyList<GameEvent> Events)
        ResolveIreliaLegendConquerReadyTrigger(
            IReadOnlyDictionary<string, PlayerZones> playerZones,
            Dictionary<string, CardObjectState> cardObjects,
            IReadOnlyDictionary<string, RunePool> runePools,
            string playerId,
            string battlefieldId,
            string attackerObjectId)
    {
        if (!TryGetExhaustedIreliaLegend(
                playerZones,
                cardObjects,
                playerId,
                out var legendObjectId,
                out var legendState))
        {
            return (runePools, []);
        }

        var currentPool = runePools.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < IreliaLegendManaCost)
        {
            return (runePools, []);
        }

        var nextRunePools = runePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        nextRunePools[playerId] = currentPool with
        {
            Mana = currentPool.Mana - IreliaLegendManaCost
        };
        cardObjects[legendObjectId] = legendState with
        {
            IsExhausted = false
        };

        return (nextRunePools,
        [
            new GameEvent(
                "LEGEND_TRIGGER_RESOLVED",
                $"{playerId} 的刀锋舞者因征服战场触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["legendObjectId"] = legendObjectId,
                    ["legendCardNo"] = legendState.CardNo,
                    ["trigger"] = "BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND",
                    ["sourceObjectId"] = attackerObjectId,
                    ["battlefieldId"] = battlefieldId
                }),
            new GameEvent(
                "COST_PAID",
                $"{playerId} 支付刀锋舞者征服触发费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["mana"] = IreliaLegendManaCost,
                    ["power"] = 0,
                    ["reason"] = "BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND"
                }),
            new GameEvent(
                "LEGEND_READIED",
                $"{legendObjectId} 变为活跃状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["reason"] = "BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND"
            })
        ]);
    }

    private static IReadOnlyList<GameEvent> ResolveSettLegendConquerReadyTrigger(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string attackerObjectId)
    {
        if (!TryGetExhaustedSettLegend(playerZones, cardObjects, playerId, out var legendObjectId, out var legendState))
        {
            return [];
        }

        cardObjects[legendObjectId] = legendState with
        {
            IsExhausted = false
        };

        return
        [
            new GameEvent(
                "LEGEND_TRIGGER_RESOLVED",
                $"{playerId} 的腕豪因征服战场触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["legendObjectId"] = legendObjectId,
                    ["legendCardNo"] = legendState.CardNo,
                    ["trigger"] = "BATTLEFIELD_CONQUERED_READY_LEGEND",
                    ["sourceObjectId"] = attackerObjectId,
                    ["battlefieldId"] = battlefieldId
                }),
            new GameEvent(
                "LEGEND_READIED",
                $"{legendObjectId} 变为活跃状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["reason"] = "BATTLEFIELD_CONQUERED_READY_LEGEND"
                })
        ];
    }

    private static bool TryApplySettLegendDestroyReplacement(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, RunePool> runePools,
        string targetObjectId,
        StackItemState stackItem,
        string destroyReason,
        out IReadOnlyDictionary<string, RunePool> nextRunePools,
        out IReadOnlyList<GameEvent> events)
    {
        nextRunePools = runePools;
        events = [];
        var location = FindFieldObjectLocation(playerZones, targetObjectId);
        if (location is null
            || !cardObjects.TryGetValue(targetObjectId, out var targetState)
            || !targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || !targetState.Tags.Contains(CardObjectTags.Boon, StringComparer.Ordinal)
            || !TryGetActiveSettLegend(playerZones, cardObjects, location.Value.PlayerId, out var legendObjectId, out var legendState))
        {
            return false;
        }

        var currentPool = runePools.TryGetValue(location.Value.PlayerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < SettLegendManaCost)
        {
            return false;
        }

        var mutableRunePools = runePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        mutableRunePools[location.Value.PlayerId] = currentPool with
        {
            Mana = currentPool.Mana - SettLegendManaCost
        };
        nextRunePools = mutableRunePools;

        var nextTags = targetState.Tags
            .Where(tag => !string.Equals(tag, CardObjectTags.Boon, StringComparison.Ordinal))
            .ToArray();
        var zones = playerZones[location.Value.PlayerId];
        playerZones[location.Value.PlayerId] = zones with
        {
            Base = zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Base
                : zones.Base.Concat([targetObjectId]).ToArray(),
            Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId)
        };
        cardObjects[targetObjectId] = targetState with
        {
            Damage = 0,
            Power = Math.Max(0, targetState.Power - 1),
            IsExhausted = true,
            Tags = nextTags
        };
        cardObjects[legendObjectId] = legendState with
        {
            IsExhausted = true
        };

        events =
        [
            new GameEvent(
                "LEGEND_TRIGGER_RESOLVED",
                $"{location.Value.PlayerId} 的腕豪替代单位摧毁",
                new Dictionary<string, object?>
                {
                    ["playerId"] = location.Value.PlayerId,
                    ["legendObjectId"] = legendObjectId,
                    ["legendCardNo"] = legendState.CardNo,
                    ["trigger"] = "BOON_UNIT_DESTROYED_PAY_1_RECALL_EXHAUSTED",
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = targetObjectId,
                    ["destroyReason"] = destroyReason
                }),
            new GameEvent(
                "COST_PAID",
                $"{location.Value.PlayerId} 支付腕豪替代费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = location.Value.PlayerId,
                    ["mana"] = SettLegendManaCost,
                    ["power"] = 0,
                    ["reason"] = "BOON_UNIT_DESTROYED_PAY_1_RECALL_EXHAUSTED"
                }),
            new GameEvent(
                "BOON_CONSUMED",
                $"{targetObjectId} 消耗增益",
                new Dictionary<string, object?>
                {
                    ["playerId"] = location.Value.PlayerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["targetObjectId"] = targetObjectId,
                    ["previousPower"] = targetState.Power,
                    ["power"] = Math.Max(0, targetState.Power - 1)
                }),
            new GameEvent(
                "LEGEND_EXHAUSTED",
                $"{legendObjectId} 变为休眠状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = location.Value.PlayerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["reason"] = "BOON_UNIT_DESTROYED_PAY_1_RECALL_EXHAUSTED"
                }),
            new GameEvent(
                "UNIT_RECALLED_TO_BASE",
                $"{targetObjectId} 改为休眠召回",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = legendObjectId,
                    ["targetObjectId"] = targetObjectId,
                    ["ownerPlayerId"] = location.Value.PlayerId,
                    ["destinationZone"] = "BASE",
                    ["replacementEffectId"] = "SETT_BOON_UNIT_DESTROYED_RECALL_EXHAUSTED",
                    ["destroyReason"] = destroyReason,
                    ["isExhausted"] = true
                })
        ];
        return true;
    }

    private static bool TryApplyBattlefieldDestroyedInBattleRecallReplacement(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, RunePool> runePools,
        string targetObjectId,
        StackItemState stackItem,
        string destroyReason,
        string? battlefieldId,
        out IReadOnlyDictionary<string, RunePool> nextRunePools,
        out IReadOnlyList<GameEvent> events)
    {
        nextRunePools = runePools;
        events = [];
        var location = FindFieldObjectLocation(playerZones, targetObjectId);
        if (!string.Equals(stackItem.EffectKind, "DECLARE_BATTLE_COMBAT_DAMAGE", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(battlefieldId)
            || location is null
            || !string.Equals(location.Value.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            || !cardObjects.TryGetValue(targetObjectId, out var targetState)
            || !targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || !TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldDestroyedInBattleRecallCardNo(battlefieldState.CardNo))
        {
            return false;
        }

        var controllerId = !string.IsNullOrWhiteSpace(targetState.ControllerId)
            && playerZones.ContainsKey(targetState.ControllerId)
                ? targetState.ControllerId
                : location.Value.PlayerId;
        var ownerId = string.IsNullOrWhiteSpace(targetState.OwnerId) ? location.Value.PlayerId : targetState.OwnerId;
        var currentPool = runePools.TryGetValue(controllerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < BattlefieldDestroyedInBattleRecallManaCost)
        {
            return false;
        }

        var mutableRunePools = runePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        mutableRunePools[controllerId] = currentPool with
        {
            Mana = currentPool.Mana - BattlefieldDestroyedInBattleRecallManaCost
        };
        nextRunePools = mutableRunePools;

        foreach (var (playerId, zones) in playerZones.ToArray())
        {
            playerZones[playerId] = zones with
            {
                Base = RemoveFromZone(zones.Base, targetObjectId),
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId)
            };
        }

        var controllerZones = playerZones[controllerId];
        playerZones[controllerId] = controllerZones with
        {
            Base = controllerZones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                ? controllerZones.Base
                : controllerZones.Base.Concat([targetObjectId]).ToArray()
        };
        cardObjects[targetObjectId] = targetState with
        {
            Damage = 0,
            IsExhausted = true,
            OwnerId = ownerId,
            ControllerId = controllerId,
            IsAttacking = false,
            IsDefending = false
        };

        events =
        [
            new GameEvent(
                "BATTLEFIELD_TRIGGER_RESOLVED",
                $"{controllerId} 支付鲜血祭坛替代单位摧毁",
                new Dictionary<string, object?>
                {
                    ["playerId"] = controllerId,
                    ["battlefieldId"] = battlefieldId,
                    ["battlefieldObjectId"] = battlefieldObjectId,
                    ["battlefieldCardNo"] = battlefieldState.CardNo,
                    ["trigger"] = BattlefieldDestroyedInBattleRecallEffectId,
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = targetObjectId,
                    ["destroyReason"] = destroyReason,
                    ["previousDamage"] = targetState.Damage
                }),
            new GameEvent(
                "COST_PAID",
                $"{controllerId} 支付鲜血祭坛替代费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = controllerId,
                    ["mana"] = BattlefieldDestroyedInBattleRecallManaCost,
                    ["power"] = 0,
                    ["reason"] = BattlefieldDestroyedInBattleRecallEffectId
                }),
            new GameEvent(
                "UNIT_RECALLED_TO_BASE",
                $"{targetObjectId} 移除伤害并休眠召回",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = battlefieldObjectId,
                    ["targetObjectId"] = targetObjectId,
                    ["ownerPlayerId"] = ownerId,
                    ["controllerId"] = controllerId,
                    ["destinationZone"] = "BASE",
                    ["replacementEffectId"] = BattlefieldDestroyedInBattleRecallEffectId,
                    ["destroyReason"] = destroyReason,
                    ["previousDamage"] = targetState.Damage,
                    ["damage"] = 0,
                    ["isExhausted"] = true
                })
        ];
        return true;
    }

    private static bool TryGetExhaustedSettLegend(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out CardObjectState legendState)
    {
        legendObjectId = string.Empty;
        legendState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !IsSettLegendCardNo(candidate.CardNo)
                || !candidate.IsExhausted)
            {
                continue;
            }

            legendObjectId = objectId;
            legendState = candidate;
            return true;
        }

        return false;
    }

    private static bool TryGetActiveSettLegend(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out CardObjectState legendState)
    {
        legendObjectId = string.Empty;
        legendState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !IsSettLegendCardNo(candidate.CardNo)
                || candidate.IsExhausted)
            {
                continue;
            }

            legendObjectId = objectId;
            legendState = candidate;
            return true;
        }

        return false;
    }

    private static bool IsSettLegendCardNo(string? cardNo)
    {
        return cardNo is SettLegendCardNo or "OGN·310/298" or "OGN·310*/298";
    }

    private static bool TryGetExhaustedIreliaLegend(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out CardObjectState legendState)
    {
        legendObjectId = string.Empty;
        legendState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !SourceObjectControlledByPlayerOrLegacyOwned(candidate, playerId)
                || !IsIreliaLegendCardNo(candidate.CardNo)
                || !candidate.IsExhausted)
            {
                continue;
            }

            legendObjectId = objectId;
            legendState = candidate;
            return true;
        }

        return false;
    }

    private static bool TryGetFirstExhaustedLegend(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out CardObjectState legendState)
    {
        legendObjectId = string.Empty;
        legendState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !SourceObjectControlledByPlayerOrLegacyOwned(candidate, playerId)
                || !candidate.IsExhausted)
            {
                continue;
            }

            legendObjectId = objectId;
            legendState = candidate;
            return true;
        }

        return false;
    }

    private static bool IsIreliaLegendCardNo(string? cardNo)
    {
        return cardNo is IreliaLegendCardNo or "SFD·195a/221·P" or "SFD·246/221";
    }

    private static bool TryGetActiveViLegend(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out CardObjectState legendState)
    {
        legendObjectId = string.Empty;
        legendState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !IsViLegendCardNo(candidate.CardNo)
                || candidate.IsExhausted)
            {
                continue;
            }

            legendObjectId = objectId;
            legendState = candidate;
            return true;
        }

        return false;
    }

    private static bool IsViLegendCardNo(string? cardNo)
    {
        return cardNo is ViLegendCardNo or "UNL-229/219" or "UNL-229*/219";
    }

    private static bool TryGetViLegendReadyTarget(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string readyTargetObjectId,
        out CardObjectState readyTargetState)
    {
        readyTargetObjectId = string.Empty;
        readyTargetState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.Base.Concat(zones.Battlefields).OrderBy(objectId => objectId, StringComparer.Ordinal))
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !candidate.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                || !candidate.IsExhausted)
            {
                continue;
            }

            readyTargetObjectId = objectId;
            readyTargetState = candidate;
            return true;
        }

        return false;
    }

    private static bool TryGetActiveVexLegend(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out CardObjectState legendState)
    {
        legendObjectId = string.Empty;
        legendState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !IsVexLegendCardNo(candidate.CardNo)
                || candidate.IsExhausted)
            {
                continue;
            }

            legendObjectId = objectId;
            legendState = candidate;
            return true;
        }

        return false;
    }

    private static bool IsVexLegendCardNo(string? cardNo)
    {
        return cardNo is VexLegendCardNo or "UNL-232/219" or "UNL-232*/219";
    }

    private static bool TryGetActiveRenataLegend(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out CardObjectState legendState)
    {
        legendObjectId = string.Empty;
        legendState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !IsRenataLegendCardNo(candidate.CardNo)
                || candidate.IsExhausted)
            {
                continue;
            }

            legendObjectId = objectId;
            legendState = candidate;
            return true;
        }

        return false;
    }

    private static bool IsRenataLegendCardNo(string? cardNo)
    {
        return cardNo is RenataLegendCardNo or "SFD·249/221";
    }

    private static RecycleResult ResolveReksaiLegendConquerRevealTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string attackerObjectId,
        long rngCursor)
    {
        var events = new List<GameEvent>();
        if (!TryGetActiveReksaiLegend(playerZones, cardObjects, playerId, out var legendObjectId, out var legendState)
            || !playerZones.TryGetValue(playerId, out var zones)
            || zones.MainDeck.Count == 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var revealedObjectIds = TakeControlledMainDeckPrefix(cardObjects, playerId, zones.MainDeck, 2);
        if (revealedObjectIds.Length == 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var playedObjectId = revealedObjectIds.FirstOrDefault(objectId =>
            cardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)) ?? string.Empty;
        var recycledObjectIds = string.IsNullOrWhiteSpace(playedObjectId)
            ? revealedObjectIds
            : revealedObjectIds
                .Where(objectId => !string.Equals(objectId, playedObjectId, StringComparison.Ordinal))
                .ToArray();
        var randomizedRecycledObjectIds = RandomizeForMainDeckBottom(
            recycledObjectIds,
            state.Seed,
            rngCursor,
            legendObjectId);
        if (recycledObjectIds.Length > 1)
        {
            rngCursor++;
        }

        var nextBase = string.IsNullOrWhiteSpace(playedObjectId) || zones.Base.Contains(playedObjectId, StringComparer.Ordinal)
            ? zones.Base
            : zones.Base.Concat([playedObjectId]).ToArray();
        playerZones[playerId] = zones with
        {
            MainDeck = zones.MainDeck
                .Skip(revealedObjectIds.Length)
                .Concat(randomizedRecycledObjectIds)
                .ToArray(),
            Base = nextBase
        };
        cardObjects[legendObjectId] = legendState with
        {
            IsExhausted = true
        };
        if (!string.IsNullOrWhiteSpace(playedObjectId)
            && cardObjects.TryGetValue(playedObjectId, out var playedState))
        {
            cardObjects[playedObjectId] = playedState with
            {
                Damage = 0,
                UntilEndOfTurnEffects = [],
                UntilEndOfTurnPowerModifier = 0,
                IsExhausted = false,
                OwnerId = string.IsNullOrWhiteSpace(playedState.OwnerId) ? playerId : playedState.OwnerId,
                ControllerId = playerId
            };
        }

        events.Add(new GameEvent(
            "LEGEND_TRIGGER_RESOLVED",
            $"{playerId} 的虚空遁地兽因征服战场触发",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["legendObjectId"] = legendObjectId,
                ["legendCardNo"] = legendState.CardNo,
                ["trigger"] = "BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_PLAY_ONE_RECYCLE_REST",
                ["sourceObjectId"] = attackerObjectId,
                ["battlefieldId"] = battlefieldId,
                ["revealedObjectIds"] = revealedObjectIds,
                ["playedObjectId"] = playedObjectId,
                ["recycledObjectIds"] = randomizedRecycledObjectIds
            }));
        events.Add(new GameEvent(
            "LEGEND_EXHAUSTED",
            $"{legendObjectId} 变为休眠状态",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = legendObjectId,
                ["reason"] = "BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_PLAY_ONE_RECYCLE_REST"
            }));
        events.Add(new GameEvent(
            "CARDS_REVEALED",
            $"{playerId} 展示主牌堆顶部 {revealedObjectIds.Length} 张牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = legendObjectId,
                ["cardIds"] = revealedObjectIds,
                ["count"] = revealedObjectIds.Length,
                ["zone"] = "MAIN_DECK"
            }));
        if (!string.IsNullOrWhiteSpace(playedObjectId))
        {
            events.Add(new GameEvent(
                "UNIT_PLAYED_TO_BASE",
                $"{playerId} 打出展示的单位到基地",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["targetObjectId"] = playedObjectId,
                    ["ownerPlayerId"] = playerId,
                    ["playedByPlayerId"] = playerId,
                    ["sourceZone"] = "MAIN_DECK",
                    ["destinationZone"] = "BASE"
                }));
        }

        if (randomizedRecycledObjectIds.Count > 0)
        {
            events.Add(new GameEvent(
                "CARDS_RECYCLED",
                $"{playerId} 回收 {randomizedRecycledObjectIds.Count} 张牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["cardIds"] = randomizedRecycledObjectIds,
                    ["count"] = randomizedRecycledObjectIds.Count
                }));
        }

        return new RecycleResult(events, rngCursor);
    }

    private static bool TryGetActiveReksaiLegend(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out CardObjectState legendState)
    {
        legendObjectId = string.Empty;
        legendState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !IsReksaiLegendCardNo(candidate.CardNo)
                || candidate.IsExhausted)
            {
                continue;
            }

            legendObjectId = objectId;
            legendState = candidate;
            return true;
        }

        return false;
    }

    private static bool IsReksaiLegendCardNo(string? cardNo)
    {
        return cardNo is ReksaiLegendCardNo or "SFD·243/221";
    }

    private static bool TryResolveIvernLegendBrushTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string battleSourceObjectId,
        string trigger,
        out IReadOnlyList<GameEvent> events)
    {
        events = [];
        if (!TryGetActiveIvernLegend(playerZones, cardObjects, playerId, out var legendObjectId, out var legendState)
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        var tokenObjectId = NextTokenObjectId(playerZones, cardObjects, legendObjectId, 1);
        var tokenState = P6TokenFactoryCatalog.TryGetByCardNo(BrushBattlefieldTokenCardNo, out var definition)
            ? definition.CreateObject(tokenObjectId, playerId, playerId)
            : new CardObjectState(
                tokenObjectId,
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                cardNo: BrushBattlefieldTokenCardNo,
                ownerId: playerId,
                controllerId: playerId);
        var tokenTags = tokenState.Tags
            .Concat(["草丛", $"REPLACES_BATTLEFIELD:{battlefieldId}"])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();

        cardObjects[legendObjectId] = legendState with
        {
            IsExhausted = true
        };
        cardObjects[tokenObjectId] = tokenState with
        {
            Tags = tokenTags
        };
        playerZones[playerId] = zones with
        {
            Battlefields = zones.Battlefields.Contains(tokenObjectId, StringComparer.Ordinal)
                ? zones.Battlefields
                : zones.Battlefields.Concat([tokenObjectId]).ToArray()
        };

        events =
        [
            new GameEvent(
                "LEGEND_TRIGGER_RESOLVED",
                $"{playerId} 的翠神因战场结果触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["legendObjectId"] = legendObjectId,
                    ["legendCardNo"] = legendState.CardNo,
                    ["trigger"] = trigger,
                    ["sourceObjectId"] = battleSourceObjectId,
                    ["battlefieldId"] = battlefieldId,
                    ["tokenObjectId"] = tokenObjectId,
                    ["tokenCardNo"] = BrushBattlefieldTokenCardNo
                }),
            new GameEvent(
                "LEGEND_EXHAUSTED",
                $"{legendObjectId} 变为休眠状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["reason"] = trigger
                }),
            new GameEvent(
                "BATTLEFIELD_TOKEN_CREATED",
                $"{legendObjectId} 将战场替换为草丛",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["tokenObjectId"] = tokenObjectId,
                    ["tokenCardNo"] = BrushBattlefieldTokenCardNo,
                    ["tokenName"] = "草丛",
                    ["battlefieldId"] = battlefieldId,
                    ["destinationZone"] = "BATTLEFIELD",
                    ["tokenTags"] = tokenTags,
                    ["trigger"] = trigger
                }),
            new GameEvent(
                "BATTLEFIELD_REPLACED",
                $"{battlefieldId} 被草丛替换",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["battlefieldId"] = battlefieldId,
                    ["replacementTokenObjectId"] = tokenObjectId,
                    ["replacementTokenCardNo"] = BrushBattlefieldTokenCardNo,
                    ["replacementTokenName"] = "草丛",
                    ["trigger"] = trigger
                })
        ];
        return true;
    }

    private static bool TryGetActiveIvernLegend(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out CardObjectState legendState)
    {
        legendObjectId = string.Empty;
        legendState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !IsIvernLegendCardNo(candidate.CardNo)
                || candidate.IsExhausted)
            {
                continue;
            }

            legendObjectId = objectId;
            legendState = candidate;
            return true;
        }

        return false;
    }

    private static bool IsIvernLegendCardNo(string? cardNo)
    {
        return cardNo is IvernLegendCardNo or "UNL-233/219" or "UNL-233*/219";
    }

    private static bool TryResolveLeblancLegendImageTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string battleSourceObjectId,
        string copySourceObjectId,
        string trigger,
        out IReadOnlyList<GameEvent> events)
    {
        events = [];
        if (!TryGetActiveLeblancLegend(playerZones, cardObjects, playerId, out var legendObjectId, out var legendState)
            || string.IsNullOrWhiteSpace(copySourceObjectId)
            || !playerZones.TryGetValue(playerId, out var zones)
            || zones.Hand.Count == 0
            || !cardObjects.TryGetValue(copySourceObjectId, out var copySourceState)
            || !copySourceState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || !IsObjectOnField(playerZones, copySourceObjectId))
        {
            return false;
        }

        var discardedObjectId = zones.Hand.FirstOrDefault(objectId =>
            IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId)) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(discardedObjectId))
        {
            return false;
        }

        if (!TryDiscardCardFromHand(playerZones, cardObjects, playerId, discardedObjectId))
        {
            return false;
        }

        var tokenObjectId = NextTokenObjectId(playerZones, cardObjects, legendObjectId, 1);
        var tokenTags = copySourceState.Tags
            .Concat([CardObjectTags.UnitCard, CardObjectTags.Ephemeral, "映像"])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
        cardObjects[legendObjectId] = legendState with
        {
            IsExhausted = true
        };
        cardObjects[tokenObjectId] = new CardObjectState(
            tokenObjectId,
            power: copySourceState.Power,
            tags: tokenTags,
            cardNo: copySourceState.CardNo,
            ownerId: playerId,
            controllerId: playerId);
        var updatedZones = playerZones[playerId];
        playerZones[playerId] = updatedZones with
        {
            Battlefields = updatedZones.Battlefields.Concat([tokenObjectId]).ToArray()
        };

        events =
        [
            new GameEvent(
                "CARD_DISCARDED",
                $"{playerId} 弃置一张手牌以触发诡术妖姬",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["targetObjectId"] = discardedObjectId,
                    ["reason"] = trigger,
                    ["destinationZone"] = "GRAVEYARD"
                }),
            new GameEvent(
                "LEGEND_TRIGGER_RESOLVED",
                $"{playerId} 的诡术妖姬因战场结果触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["legendObjectId"] = legendObjectId,
                    ["legendCardNo"] = legendState.CardNo,
                    ["trigger"] = trigger,
                    ["sourceObjectId"] = battleSourceObjectId,
                    ["battlefieldId"] = battlefieldId,
                    ["discardedObjectId"] = discardedObjectId,
                    ["copiedTargetObjectId"] = copySourceObjectId
                }),
            new GameEvent(
                "LEGEND_EXHAUSTED",
                $"{legendObjectId} 变为休眠状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["reason"] = trigger
                }),
            new GameEvent(
                "UNIT_TOKEN_CREATED",
                $"{legendObjectId} 在战场打出映像",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["tokenObjectId"] = tokenObjectId,
                    ["tokenName"] = "映像",
                    ["tokenCardNo"] = copySourceState.CardNo,
                    ["power"] = copySourceState.Power,
                    ["destinationZone"] = "BATTLEFIELD",
                    ["battlefieldId"] = battlefieldId,
                    ["copiedTargetObjectId"] = copySourceObjectId,
                    ["copiedCardNo"] = copySourceState.CardNo,
                    ["isExhausted"] = false,
                    ["tokenTags"] = tokenTags,
                    ["trigger"] = trigger
                })
        ];
        return true;
    }

    private static bool TryGetActiveLeblancLegend(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string legendObjectId,
        out CardObjectState legendState)
    {
        legendObjectId = string.Empty;
        legendState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !IsLeblancLegendCardNo(candidate.CardNo)
                || candidate.IsExhausted
                || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId))
            {
                continue;
            }

            legendObjectId = objectId;
            legendState = candidate;
            return true;
        }

        return false;
    }

    private static bool IsLeblancLegendCardNo(string? cardNo)
    {
        return cardNo is LeblancLegendCardNo or "UNL-235/219" or "UNL-235*/219";
    }

    private static void AddBattlefieldHeldEventIfNeeded(
        List<GameEvent> events,
        ref bool battlefieldHeldEventEmitted,
        string playerId,
        string battlefieldId,
        string attackerObjectId,
        IReadOnlyList<string> defenderObjectIds)
    {
        if (battlefieldHeldEventEmitted)
        {
            return;
        }

        events.Add(new GameEvent(
            "BATTLEFIELD_HELD",
            $"{playerId} 据守战场",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["sourceObjectId"] = attackerObjectId,
                ["defenderObjectIds"] = defenderObjectIds.ToArray()
        }));
        battlefieldHeldEventEmitted = true;
    }

    private static bool TryResolveBattlefieldHeldDrawTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, int> playerScores,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        long rngCursor,
        List<GameEvent> events,
        out DrawApplicationResult drawApplication)
    {
        drawApplication = new DrawApplicationResult(playerScores, null, rngCursor);
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldHoldDrawCardNo(battlefieldState.CardNo))
        {
            return false;
        }

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 据守战场并抽牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_HELD_DRAW_ONE",
                ["sourceObjectId"] = sourceObjectId,
                ["drawCount"] = 1
            }));
        drawApplication = ApplyDrawToPlayer(
            state,
            playerZones,
            playerScores,
            playerId,
            1,
            rngCursor,
            events);
        return true;
    }

    private static bool TryResolveBattlefieldHeldCreateMinionTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        List<GameEvent> events)
    {
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldHoldCreateMinionCardNo(battlefieldState.CardNo))
        {
            return false;
        }

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 据守战场并打出随从",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_HELD_CREATE_MINION",
                ["sourceObjectId"] = sourceObjectId,
                ["tokenName"] = "随从"
            }));
        CreateBattlefieldMinionTokenInBase(
            playerZones,
            cardObjects,
            playerId,
            battlefieldObjectId,
            events);
        return true;
    }

    private static bool TryResolveBattlefieldHeldEachPlayerCallRuneTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        List<GameEvent> events)
    {
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldHoldEachPlayerCallRuneCardNo(battlefieldState.CardNo))
        {
            return false;
        }

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 据守战场并让每名玩家召出符文",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_HELD_EACH_PLAYER_CALL_RUNE",
                ["sourceObjectId"] = sourceObjectId
            }));

        foreach (var runePlayerId in ControllerAndOtherPlayerIds(state, playerId))
        {
            var runeCallResult = CallRunes(
                playerZones,
                cardObjects,
                runePlayerId,
                1);
            events.Add(new GameEvent(
                "RUNES_CALLED",
                $"{runePlayerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
                new Dictionary<string, object?>
                {
                    ["playerId"] = runePlayerId,
                    ["sourceObjectId"] = battlefieldObjectId,
                    ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                    ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray()
                }));
        }

        return true;
    }

    private static bool TryResolveBattlefieldHeldCallRuneTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        List<GameEvent> events)
    {
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldHoldCallRuneCardNo(battlefieldState.CardNo))
        {
            return false;
        }

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 据守战场并召出符文",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_HELD_CALL_RUNE",
                ["sourceObjectId"] = sourceObjectId
            }));
        var runeCallResult = CallRunes(
            playerZones,
            cardObjects,
            playerId,
            1);
        events.Add(new GameEvent(
            "RUNES_CALLED",
            $"{playerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray()
            }));
        return true;
    }

    private static bool TryResolveBattlefieldUnitReturnedCallRuneTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, RunePool> runePools,
        string playerId,
        string returnedObjectId,
        string sourceObjectId,
        List<GameEvent> events,
        out IReadOnlyDictionary<string, RunePool> nextRunePools)
    {
        const string trigger = "BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE";
        nextRunePools = runePools;
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        var battlefieldObjectId = zones.Battlefields.FirstOrDefault(objectId =>
            cardObjects.TryGetValue(objectId, out var candidate)
            && IsBattlefieldUnitReturnedCallRuneCardNo(candidate.CardNo)
            && SourceObjectControlledByPlayerOrLegacyOwned(candidate, playerId));
        if (string.IsNullOrWhiteSpace(battlefieldObjectId)
            || !cardObjects.TryGetValue(battlefieldObjectId, out var battlefieldState))
        {
            return false;
        }

        if (zones.RuneDeck.Count == 0)
        {
            return false;
        }

        var currentPool = runePools.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < BattlefieldUnitReturnedCallRuneManaCost)
        {
            return false;
        }

        var mutableRunePools = runePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        mutableRunePools[playerId] = currentPool with
        {
            Mana = currentPool.Mana - BattlefieldUnitReturnedCallRuneManaCost
        };
        nextRunePools = mutableRunePools;

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 的单位返回手牌并通过鬼影湾召出符文",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = trigger,
                ["sourceObjectId"] = sourceObjectId,
                ["returnedObjectId"] = returnedObjectId
            }));
        events.Add(new GameEvent(
            "COST_PAID",
            $"{playerId} 支付鬼影湾触发费用",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["mana"] = BattlefieldUnitReturnedCallRuneManaCost,
                ["power"] = 0,
                ["reason"] = trigger
            }));
        var runeCallResult = CallRunes(
            playerZones,
            cardObjects,
            playerId,
            1);
        events.Add(new GameEvent(
            "RUNES_CALLED",
            $"{playerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray(),
                ["reason"] = trigger,
                ["returnedObjectId"] = returnedObjectId
            }));
        return true;
    }

    private static bool TryResolveBattlefieldHeldGrantBoonTrigger(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        IReadOnlyList<string> defenderObjectIds,
        List<GameEvent> events)
    {
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldHoldGrantBoonCardNo(battlefieldState.CardNo)
            || !TryGetFirstSurvivingBattlefieldUnit(cardObjects, playerZones, defenderObjectIds, out var targetObjectId))
        {
            return false;
        }

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 据守战场并给予单位增益",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_HELD_GRANT_BOON",
                ["sourceObjectId"] = sourceObjectId,
                ["targetObjectId"] = targetObjectId
            }));
        GrantLegendBoon(
            cardObjects,
            targetObjectId,
            playerId,
            battlefieldObjectId,
            "BATTLEFIELD_HELD_GRANT_BOON",
            events);
        return true;
    }

    private static bool TryResolveBattlefieldHeldMoveUnitToBaseTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        IReadOnlyList<string> defenderObjectIds,
        List<GameEvent> events)
    {
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldHeldMoveUnitToBaseCardNo(battlefieldState.CardNo)
            || !TryGetFirstBattlefieldZoneUnit(cardObjects, playerZones, defenderObjectIds.Concat([sourceObjectId]), out var targetObjectId))
        {
            return false;
        }

        if (!TryMoveTargetToOwnerBase(playerZones, cardObjects, targetObjectId, out var targetPlayerId)
            || !cardObjects.TryGetValue(targetObjectId, out var targetState))
        {
            return false;
        }

        cardObjects[targetObjectId] = targetState with
        {
            IsAttacking = false,
            IsDefending = false
        };
        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 据守战场并将单位移动到基地",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_HELD_MOVE_UNIT_TO_BASE",
                ["sourceObjectId"] = sourceObjectId,
                ["targetObjectId"] = targetObjectId
            }));
        events.Add(new GameEvent(
            "UNIT_MOVED_TO_BASE",
            $"{targetObjectId} 因据守战场移动到基地",
            new Dictionary<string, object?>
            {
                ["playerId"] = targetPlayerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["targetObjectId"] = targetObjectId,
                ["originZone"] = MoveUnitBattlefieldZone,
                ["destinationZone"] = MoveUnitBaseZone,
                ["reason"] = "BATTLEFIELD_HELD_MOVE_UNIT_TO_BASE"
            }));
        return true;
    }

    private static bool TryResolveBattlefieldHeldReturnHeroTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        List<GameEvent> events)
    {
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldHeldReturnHeroCardNo(battlefieldState.CardNo)
            || !playerZones.TryGetValue(playerId, out var zones)
            || zones.ChampionZone.Count > 0)
        {
            return false;
        }

        var targetObjectId = zones.Graveyard
            .Where(objectId => cardObjects.TryGetValue(objectId, out var cardObject)
                && string.Equals(cardObject.OwnerId, playerId, StringComparison.Ordinal)
                && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(targetObjectId)
            || !cardObjects.TryGetValue(targetObjectId, out var targetState))
        {
            return false;
        }

        playerZones[playerId] = zones with
        {
            Graveyard = RemoveFromZone(zones.Graveyard, targetObjectId),
            ChampionZone = zones.ChampionZone.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.ChampionZone
                : zones.ChampionZone.Concat([targetObjectId]).ToArray()
        };
        cardObjects[targetObjectId] = targetState with
        {
            ControllerId = playerId,
            Damage = 0,
            IsAttacking = false,
            IsDefending = false,
            IsExhausted = false
        };
        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 据守战场并让英雄返回英雄区域",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_HELD_RETURN_HERO_FROM_GRAVEYARD",
                ["sourceObjectId"] = sourceObjectId,
                ["targetObjectId"] = targetObjectId,
                ["originZone"] = "GRAVEYARD",
                ["destinationZone"] = "CHAMPION"
            }));
        events.Add(new GameEvent(
            "UNIT_RETURNED_TO_CHAMPION_ZONE",
            $"{targetObjectId} 返回英雄区域",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["targetObjectId"] = targetObjectId,
                ["originZone"] = "GRAVEYARD",
                ["destinationZone"] = "CHAMPION",
                ["reason"] = "BATTLEFIELD_HELD_RETURN_HERO_FROM_GRAVEYARD"
            }));
        return true;
    }

    private static bool TryResolveBattlefieldHeldPayPowerScoreTrigger(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, RunePool> runePools,
        IReadOnlyDictionary<string, int> playerScores,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        int winningScore,
        List<GameEvent> events,
        out IReadOnlyDictionary<string, RunePool> nextRunePools,
        out IReadOnlyDictionary<string, int> nextPlayerScores,
        out string? winnerPlayerId)
    {
        nextRunePools = runePools;
        nextPlayerScores = playerScores;
        winnerPlayerId = null;
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldHeldPayPowerScoreCardNo(battlefieldState.CardNo))
        {
            return false;
        }

        if (TryBuildBattlefieldScorePreventedEvent(
                state,
                playerId,
                "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE",
                [battlefieldObjectId],
                out var scorePreventedEvent)
            && scorePreventedEvent is not null)
        {
            events.Add(scorePreventedEvent);
            return true;
        }

        var currentPool = runePools.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty;
        if (!CanPayRuneCosts(currentPool, 0, BattlefieldHeldScorePowerCost))
        {
            return false;
        }

        var mutableRunePools = PayRuneCosts(runePools, playerId, 0, BattlefieldHeldScorePowerCost);
        var mutablePlayerScores = playerZones.Keys.ToDictionary(
            scorePlayerId => scorePlayerId,
            scorePlayerId => playerScores.TryGetValue(scorePlayerId, out var currentScore) ? currentScore : 0,
            StringComparer.Ordinal);
        mutablePlayerScores[playerId] = mutablePlayerScores.TryGetValue(playerId, out var score) ? score + 1 : 1;
        winnerPlayerId = WinningPlayerId(mutablePlayerScores, winningScore);

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 据守战场并支付能量获得分数",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE",
                ["sourceObjectId"] = sourceObjectId,
                ["powerCost"] = BattlefieldHeldScorePowerCost,
                ["amount"] = 1,
                ["score"] = mutablePlayerScores[playerId]
            }));
        events.Add(new GameEvent(
            "COST_PAID",
            $"{playerId} 支付能量枢纽据守触发费用",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["mana"] = 0,
                ["power"] = BattlefieldHeldScorePowerCost,
                ["reason"] = "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE"
            }));
        events.Add(new GameEvent(
            "SCORE_GAINED",
            $"{playerId} 获得 1 分",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["amount"] = 1,
                ["score"] = mutablePlayerScores[playerId],
                ["reason"] = "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE",
                ["sourceObjectId"] = battlefieldObjectId
            }));
        if (winnerPlayerId is not null)
        {
            events.Add(new GameEvent(
                "MATCH_WON",
                $"{winnerPlayerId} 达到获胜分数并获胜",
                new Dictionary<string, object?>
                {
                    ["winnerPlayerId"] = winnerPlayerId,
                    ["winningScore"] = winningScore
                }));
        }

        nextRunePools = mutableRunePools;
        nextPlayerScores = mutablePlayerScores;
        return true;
    }

    private static bool TryResolveBattlefieldHeldSevenUnitsWinTrigger(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        int winningScore,
        List<GameEvent> events,
        out string? winnerPlayerId)
    {
        winnerPlayerId = null;
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldHeldSevenUnitsWinCardNo(battlefieldState.CardNo))
        {
            return false;
        }

        var controlledBattlefieldUnitCount = CountControlledBattlefieldUnits(playerZones, cardObjects, playerId);
        if (controlledBattlefieldUnitCount < BattlefieldHeldSevenUnitsWinThreshold)
        {
            return false;
        }

        winnerPlayerId = playerId;
        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 据守战场并满足单位数量胜利条件",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_HELD_SEVEN_UNITS_WIN",
                ["sourceObjectId"] = sourceObjectId,
                ["controlledBattlefieldUnitCount"] = controlledBattlefieldUnitCount,
                ["requiredUnitCount"] = BattlefieldHeldSevenUnitsWinThreshold
            }));
        events.Add(new GameEvent(
            "MATCH_WON",
            $"{playerId} 因据守战场达到特殊胜利条件并获胜",
            new Dictionary<string, object?>
            {
                ["winnerPlayerId"] = playerId,
                ["winningScore"] = winningScore,
                ["reason"] = "BATTLEFIELD_HELD_SEVEN_UNITS_WIN",
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["controlledBattlefieldUnitCount"] = controlledBattlefieldUnitCount,
                ["requiredUnitCount"] = BattlefieldHeldSevenUnitsWinThreshold
            }));
        return true;
    }

    private static bool TryResolveBattlefieldHeldUnitCostIncreaseTrigger(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyList<string> untilEndOfTurnEffects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        List<GameEvent> events,
        out IReadOnlyList<string> nextUntilEndOfTurnEffects)
    {
        nextUntilEndOfTurnEffects = untilEndOfTurnEffects;
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldHeldUnitCostIncreaseCardNo(battlefieldState.CardNo))
        {
            return false;
        }

        var effectId = BuildBattlefieldHeldUnitCostIncreaseEffectId(playerId);
        nextUntilEndOfTurnEffects = AddUntilEndOfTurnEffect(untilEndOfTurnEffects, effectId);
        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 据守海力亚秘库，本回合非指示物单位费用增加",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE",
                ["sourceObjectId"] = sourceObjectId,
                ["effectId"] = effectId,
                ["manaIncrease"] = 1
            }));
        return true;
    }

    private static bool TryResolveBattlefieldHeldNextSpellEchoTrigger(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyList<string> untilEndOfTurnEffects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        List<GameEvent> events,
        out IReadOnlyList<string> nextUntilEndOfTurnEffects)
    {
        nextUntilEndOfTurnEffects = untilEndOfTurnEffects;
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldHeldNextSpellEchoCardNo(battlefieldState.CardNo))
        {
            return false;
        }

        var effectId = BuildBattlefieldHeldNextSpellEchoEffectId(playerId);
        nextUntilEndOfTurnEffects = AddUntilEndOfTurnEffect(untilEndOfTurnEffects, effectId);
        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 据守皮城学院，下一个法术获得回响",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO",
                ["sourceObjectId"] = sourceObjectId,
                ["effectId"] = effectId
            }));
        return true;
    }

    private static bool TryResolveBattlefieldHeldActivateUnitConquestEffectsTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, int> playerScores,
        IReadOnlyList<string> untilEndOfTurnEffects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        long rngCursor,
        List<GameEvent> events,
        out DrawApplicationResult drawApplication,
        out IReadOnlyList<string> nextUntilEndOfTurnEffects)
    {
        const string trigger = "BATTLEFIELD_HELD_ACTIVATE_UNIT_CONQUEST_EFFECTS";
        drawApplication = new DrawApplicationResult(playerScores, null, rngCursor);
        nextUntilEndOfTurnEffects = untilEndOfTurnEffects;
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldHeldActivateConquestEffectsCardNo(battlefieldState.CardNo)
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        var unitObjectIds = zones.Battlefields
            .Where(objectId => cardObjects.TryGetValue(objectId, out var unitState)
                && unitState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && !unitState.IsFaceDown
                && SourceObjectControlledByPlayerOrLegacyOwned(unitState, playerId)
                && HasBattlefieldHeldArenaUnitConquestEffect(unitState.CardNo))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (unitObjectIds.Length == 0)
        {
            return false;
        }

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 据守清算人竞技场并激活单位征服效果",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = trigger,
                ["sourceObjectId"] = sourceObjectId,
                ["activatedUnitObjectIds"] = unitObjectIds
            }));

        var nextPlayerScores = playerScores;
        string? winnerPlayerId = null;
        var nextRngCursor = rngCursor;
        foreach (var unitObjectId in unitObjectIds)
        {
            if (!cardObjects.TryGetValue(unitObjectId, out var unitState))
            {
                continue;
            }

            if (IsBadPoroUnitConquestGoldCardNo(unitState.CardNo))
            {
                AddUnitConquestEffectActivatedEvent(
                    events,
                    playerId,
                    unitObjectId,
                    unitState.CardNo,
                    "UNIT_CONQUEST_CREATE_DORMANT_GOLD",
                    battlefieldObjectId,
                    trigger);
                CreateLegendEquipmentToken(
                    playerZones,
                    cardObjects,
                    playerId,
                    unitObjectId,
                    "UNIT_CONQUEST_CREATE_DORMANT_GOLD",
                    "金币",
                    [CardObjectTags.EquipmentCard, "金币", "反应"],
                    isExhausted: true,
                    events);
                continue;
            }

            if (IsKaisaUnitConquestDrawCardNo(unitState.CardNo))
            {
                AddUnitConquestEffectActivatedEvent(
                    events,
                    playerId,
                    unitObjectId,
                    unitState.CardNo,
                    "UNIT_CONQUEST_DRAW_ONE",
                    battlefieldObjectId,
                    trigger);
                var drawResult = ApplyDrawToPlayer(
                    state,
                    playerZones,
                    nextPlayerScores,
                    playerId,
                    1,
                    nextRngCursor,
                    events);
                nextPlayerScores = drawResult.PlayerScores;
                winnerPlayerId = drawResult.WinnerPlayerId ?? winnerPlayerId;
                nextRngCursor = drawResult.RngCursor;
                continue;
            }

            if (IsQiyanaUnitConquestDrawOrRuneCardNo(unitState.CardNo))
            {
                AddUnitConquestEffectActivatedEvent(
                    events,
                    playerId,
                    unitObjectId,
                    unitState.CardNo,
                    "UNIT_CONQUEST_DRAW_ONE_OR_CALL_RUNE",
                    battlefieldObjectId,
                    trigger);
                if (playerZones.TryGetValue(playerId, out var currentZones)
                    && currentZones.MainDeck.Count > 0)
                {
                    var drawResult = ApplyDrawToPlayer(
                        state,
                        playerZones,
                        nextPlayerScores,
                        playerId,
                        1,
                        nextRngCursor,
                        events);
                    nextPlayerScores = drawResult.PlayerScores;
                    winnerPlayerId = drawResult.WinnerPlayerId ?? winnerPlayerId;
                    nextRngCursor = drawResult.RngCursor;
                }
                else
                {
                    var runeCallResult = CallRunes(playerZones, cardObjects, playerId, 1);
                    events.Add(new GameEvent(
                        "RUNES_CALLED",
                        $"{playerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
                        new Dictionary<string, object?>
                        {
                            ["playerId"] = playerId,
                            ["sourceObjectId"] = unitObjectId,
                            ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                            ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray(),
                            ["reason"] = "UNIT_CONQUEST_DRAW_ONE_OR_CALL_RUNE"
                        }));
                }

                continue;
            }

            if (IsSettUnitConquestSelfBoonCardNo(unitState.CardNo))
            {
                AddUnitConquestEffectActivatedEvent(
                    events,
                    playerId,
                    unitObjectId,
                    unitState.CardNo,
                    "UNIT_CONQUEST_GRANT_SELF_BOON",
                    battlefieldObjectId,
                    trigger);
                GrantLegendBoon(
                    cardObjects,
                    unitObjectId,
                    playerId,
                    unitObjectId,
                    "UNIT_CONQUEST_GRANT_SELF_BOON",
                    events);
                continue;
            }

            if (IsLucianUnitConquestReadyCardNo(unitState.CardNo))
            {
                var readyEffectId = BuildUnitConquestReadySelfOnceEffectId(playerId, unitObjectId);
                if (nextUntilEndOfTurnEffects.Contains(readyEffectId, StringComparer.Ordinal))
                {
                    continue;
                }

                AddUnitConquestEffectActivatedEvent(
                    events,
                    playerId,
                    unitObjectId,
                    unitState.CardNo,
                    "UNIT_CONQUEST_READY_SELF_ONCE_PER_TURN",
                    battlefieldObjectId,
                    trigger);
                nextUntilEndOfTurnEffects = AddUntilEndOfTurnEffect(nextUntilEndOfTurnEffects, readyEffectId);
                var wasExhausted = unitState.IsExhausted;
                cardObjects[unitObjectId] = unitState with
                {
                    IsExhausted = false
                };
                events.Add(new GameEvent(
                    "UNIT_READIED",
                    $"{unitObjectId} 因征服效果变为活跃状态",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = playerId,
                        ["sourceObjectId"] = unitObjectId,
                        ["targetObjectId"] = unitObjectId,
                        ["wasExhausted"] = wasExhausted,
                        ["isExhausted"] = false,
                        ["effectId"] = readyEffectId,
                        ["reason"] = "UNIT_CONQUEST_READY_SELF_ONCE_PER_TURN"
                    }));
                continue;
            }

            if (IsFriendlyBoonUnitConquestCardNo(unitState.CardNo)
                && TryGetFirstControlledBattlefieldUnit(
                    playerZones,
                    cardObjects,
                    playerId,
                    unitObjectId,
                    out var boonTargetObjectId))
            {
                AddUnitConquestEffectActivatedEvent(
                    events,
                    playerId,
                    unitObjectId,
                    unitState.CardNo,
                    "UNIT_CONQUEST_GRANT_FRIENDLY_BOON",
                    battlefieldObjectId,
                    trigger,
                    boonTargetObjectId);
                GrantLegendBoon(
                    cardObjects,
                    boonTargetObjectId,
                    playerId,
                    unitObjectId,
                    "UNIT_CONQUEST_GRANT_FRIENDLY_BOON",
                    events);
                continue;
            }

            if (IsFriendlyPowerUnitConquestCardNo(unitState.CardNo)
                && TryGetFirstControlledBattlefieldUnit(
                    playerZones,
                    cardObjects,
                    playerId,
                    unitObjectId,
                    out var powerTargetObjectId)
                && cardObjects.TryGetValue(powerTargetObjectId, out var powerTargetState))
            {
                const int powerDelta = 8;
                AddUnitConquestEffectActivatedEvent(
                    events,
                    playerId,
                    unitObjectId,
                    unitState.CardNo,
                    "UNIT_CONQUEST_FRIENDLY_PLUS_8_THIS_TURN",
                    battlefieldObjectId,
                    trigger,
                    powerTargetObjectId);
                cardObjects[powerTargetObjectId] = powerTargetState with
                {
                    Power = powerTargetState.Power + powerDelta,
                    UntilEndOfTurnPowerModifier = powerTargetState.UntilEndOfTurnPowerModifier + powerDelta
                };
                events.Add(new GameEvent(
                    "POWER_MODIFIED_UNTIL_END_OF_TURN",
                    $"{unitObjectId} 的征服效果临时修正战力",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = playerId,
                        ["sourceObjectId"] = unitObjectId,
                        ["targetObjectId"] = powerTargetObjectId,
                        ["powerDelta"] = powerDelta,
                        ["appliedPowerDelta"] = powerDelta,
                        ["resultingPower"] = powerTargetState.Power + powerDelta,
                        ["reason"] = "UNIT_CONQUEST_FRIENDLY_PLUS_8_THIS_TURN"
                    }));
                continue;
            }

            if (IsDestroyEquipmentBoonUnitConquestCardNo(unitState.CardNo)
                && TryGetFirstFieldEquipment(playerZones, cardObjects, out var equipmentObjectId))
            {
                AddUnitConquestEffectActivatedEvent(
                    events,
                    playerId,
                    unitObjectId,
                    unitState.CardNo,
                    "UNIT_CONQUEST_DESTROY_EQUIPMENT_GRANT_SELF_BOON",
                    battlefieldObjectId,
                    trigger,
                    equipmentObjectId);
                var stackItem = new StackItemState(
                    $"unit-conquest-{unitObjectId}",
                    playerId,
                    unitObjectId,
                    "UNIT_CONQUEST_DESTROY_EQUIPMENT_GRANT_SELF_BOON",
                    unitState.CardNo,
                    [equipmentObjectId]);
                if (TryDestroyTarget(playerZones, cardObjects, equipmentObjectId, out var removalResult))
                {
                    events.Add(BuildFieldRemovalEvent(
                        "单位征服效果",
                        stackItem,
                        equipmentObjectId,
                        removalResult,
                        "UNIT_CONQUEST_DESTROY_EQUIPMENT_GRANT_SELF_BOON"));
                    GrantLegendBoon(
                        cardObjects,
                        unitObjectId,
                        playerId,
                        unitObjectId,
                        "UNIT_CONQUEST_DESTROY_EQUIPMENT_GRANT_SELF_BOON",
                        events);
                }
            }
        }

        drawApplication = new DrawApplicationResult(nextPlayerScores, winnerPlayerId, nextRngCursor);
        return true;
    }

    private static void AddUnitConquestEffectActivatedEvent(
        List<GameEvent> events,
        string playerId,
        string unitObjectId,
        string? unitCardNo,
        string effectId,
        string battlefieldObjectId,
        string reason,
        string? targetObjectId = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["playerId"] = playerId,
            ["sourceObjectId"] = unitObjectId,
            ["unitObjectId"] = unitObjectId,
            ["unitCardNo"] = unitCardNo,
            ["effectId"] = effectId,
            ["battlefieldObjectId"] = battlefieldObjectId,
            ["reason"] = reason
        };
        if (!string.IsNullOrWhiteSpace(targetObjectId))
        {
            payload["targetObjectId"] = targetObjectId;
        }

        events.Add(new GameEvent(
            "UNIT_CONQUEST_EFFECT_ACTIVATED",
            $"{unitObjectId} 的征服效果已激活",
            payload));
    }

    private static bool TryGetFirstFieldEquipment(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        out string equipmentObjectId)
    {
        equipmentObjectId = playerZones
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Where(objectId => cardObjects.TryGetValue(objectId, out var objectState)
                && objectState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(equipmentObjectId);
    }

    private static bool HasBattlefieldHeldArenaUnitConquestEffect(string? cardNo)
    {
        return IsBadPoroUnitConquestGoldCardNo(cardNo)
            || IsKaisaUnitConquestDrawCardNo(cardNo)
            || IsQiyanaUnitConquestDrawOrRuneCardNo(cardNo)
            || IsSettUnitConquestSelfBoonCardNo(cardNo)
            || IsLucianUnitConquestReadyCardNo(cardNo)
            || IsFriendlyBoonUnitConquestCardNo(cardNo)
            || IsFriendlyPowerUnitConquestCardNo(cardNo)
            || IsDestroyEquipmentBoonUnitConquestCardNo(cardNo);
    }

    private static bool IsBadPoroUnitConquestGoldCardNo(string? cardNo)
    {
        return cardNo is "UNL-222/219" or "SFD·069/221";
    }

    private static bool IsKaisaUnitConquestDrawCardNo(string? cardNo)
    {
        return cardNo is "OGN·039/298" or "OGN·039a/298";
    }

    private static bool IsQiyanaUnitConquestDrawOrRuneCardNo(string? cardNo)
    {
        return string.Equals(cardNo, "OGN·155/298", StringComparison.Ordinal);
    }

    private static bool IsSettUnitConquestSelfBoonCardNo(string? cardNo)
    {
        return cardNo is "SFD·232/221" or "SFD·232*/221" or "OGN·164/298" or "OGN·164a/298";
    }

    private static bool IsLucianUnitConquestReadyCardNo(string? cardNo)
    {
        return cardNo is "SFD·113/221" or "SFD·113a/221";
    }

    private static bool IsFriendlyBoonUnitConquestCardNo(string? cardNo)
    {
        return cardNo is "UNL-029/219" or "UNL-029a/219";
    }

    private static bool IsFriendlyPowerUnitConquestCardNo(string? cardNo)
    {
        return string.Equals(cardNo, "UNL-027/219", StringComparison.Ordinal);
    }

    private static bool IsDestroyEquipmentBoonUnitConquestCardNo(string? cardNo)
    {
        return string.Equals(cardNo, "OGN·056/298", StringComparison.Ordinal);
    }

    private static string BuildUnitConquestReadySelfOnceEffectId(string playerId, string unitObjectId)
    {
        return $"{UnitConquestReadySelfOnceEffectPrefix}{playerId}:{unitObjectId}";
    }

    private static bool TryGetFirstSurvivingBattlefieldUnit(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyList<string> candidateObjectIds,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        foreach (var candidateObjectId in candidateObjectIds.OrderBy(objectId => objectId, StringComparer.Ordinal))
        {
            if (cardObjects.TryGetValue(candidateObjectId, out var candidate)
                && candidate.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && IsObjectOnField(playerZones, candidateObjectId))
            {
                targetObjectId = candidateObjectId;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetFirstBattlefieldZoneUnit(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IEnumerable<string> candidateObjectIds,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        foreach (var candidateObjectId in candidateObjectIds
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal))
        {
            var location = FindFieldObjectLocation(playerZones, candidateObjectId);
            if (location is not null
                && string.Equals(location.Value.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
                && cardObjects.TryGetValue(candidateObjectId, out var candidate)
                && candidate.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
            {
                targetObjectId = candidateObjectId;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveBattlefieldDefenderSteadfastChoice(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string battlefieldId,
        IReadOnlyList<string>? battlefieldTargetObjectIds,
        IReadOnlyList<string> defenderObjectIds,
        out string? targetObjectId,
        out string battlefieldObjectId,
        out string? battlefieldCardNo,
        out ResolutionResult rejection)
    {
        targetObjectId = null;
        battlefieldObjectId = string.Empty;
        battlefieldCardNo = null;
        rejection = default!;

        var requestedTargetObjectIds = NormalizeTargetObjectIds(battlefieldTargetObjectIds ?? []);
        var hasBattlefieldObject = TryGetBattlefieldCardObject(playerZones, state.CardObjects, battlefieldId, out battlefieldObjectId, out var battlefieldState);
        if (!hasBattlefieldObject
            || !IsBattlefieldDefenderSteadfastTwoCardNo(battlefieldState.CardNo))
        {
            if (requestedTargetObjectIds.Count > 0
                && (!hasBattlefieldObject || !IsBattlefieldDefendMoveFriendlyUnitToBaseCardNo(battlefieldState.CardNo)))
            {
                rejection = RejectWithCorePrompts(
                    state,
                    "Battlefield target choices are only supported for battlefield effects that require them.",
                    ErrorCodes.InvalidTarget);
                return false;
            }

            return true;
        }

        battlefieldCardNo = battlefieldState.CardNo;
        var selectedTargetObjectId = requestedTargetObjectIds.Count == 0 && defenderObjectIds.Count == 1
            ? defenderObjectIds[0]
            : requestedTargetObjectIds.Count == 1
                ? requestedTargetObjectIds[0]
                : string.Empty;
        if (string.IsNullOrWhiteSpace(selectedTargetObjectId)
            || !defenderObjectIds.Contains(selectedTargetObjectId, StringComparer.Ordinal))
        {
            rejection = RejectWithCorePrompts(
                state,
                "Fortified Position requires exactly one defending unit as its battlefield target.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        targetObjectId = selectedTargetObjectId;
        return true;
    }

    private static bool TryResolveBattlefieldDefenderMoveToBaseChoice(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string battlefieldId,
        IReadOnlyList<string>? battlefieldTargetObjectIds,
        IReadOnlyList<string> defenderObjectIds,
        out string? targetObjectId,
        out string battlefieldObjectId,
        out string? battlefieldCardNo,
        out ResolutionResult rejection)
    {
        targetObjectId = null;
        battlefieldObjectId = string.Empty;
        battlefieldCardNo = null;
        rejection = default!;

        if (!TryGetBattlefieldCardObject(playerZones, state.CardObjects, battlefieldId, out battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldDefendMoveFriendlyUnitToBaseCardNo(battlefieldState.CardNo))
        {
            return true;
        }

        battlefieldCardNo = battlefieldState.CardNo;
        var requestedTargetObjectIds = NormalizeTargetObjectIds(battlefieldTargetObjectIds ?? []);
        if (requestedTargetObjectIds.Count == 0)
        {
            return true;
        }

        if (requestedTargetObjectIds.Count != 1
            || !defenderObjectIds.Contains(requestedTargetObjectIds[0], StringComparer.Ordinal))
        {
            rejection = RejectWithCorePrompts(
                state,
                "Plunder Ship Alley requires zero or one defending unit as its battlefield target.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        targetObjectId = requestedTargetObjectIds[0];
        return true;
    }

    private static bool TryResolveBattlefieldDefenderMoveToBaseTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        string targetObjectId,
        string battlefieldObjectId,
        string? battlefieldCardNo,
        List<GameEvent> events)
    {
        if (string.IsNullOrWhiteSpace(playerId)
            || !playerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
            || !cardObjects.TryGetValue(targetObjectId, out var targetState)
            || !targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || (!string.IsNullOrWhiteSpace(targetState.ControllerId)
                && !string.Equals(targetState.ControllerId, playerId, StringComparison.Ordinal)))
        {
            return false;
        }

        playerZones[playerId] = zones with
        {
            Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId),
            Base = zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Base
                : zones.Base.Concat([targetObjectId]).ToArray()
        };
        cardObjects[targetObjectId] = targetState with
        {
            IsAttacking = false,
            IsDefending = false
        };

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 防守战场并将单位移动到基地",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldCardNo,
                ["trigger"] = "BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE",
                ["sourceObjectId"] = sourceObjectId,
                ["targetObjectId"] = targetObjectId
            }));
        events.Add(new GameEvent(
            "UNIT_MOVED_TO_BASE",
            $"{targetObjectId} 因战场防守触发移动到基地",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["targetObjectId"] = targetObjectId,
                ["originZone"] = MoveUnitBattlefieldZone,
                ["destinationZone"] = MoveUnitBaseZone,
                ["reason"] = "BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE"
            }));
        return true;
    }

    private static void CreateBattlefieldMinionTokenInBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldObjectId,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(playerId, out var zones)
            || !P6TokenFactoryCatalog.TryGetByCardNo(DemaciaMinionTokenCardNo, out var tokenDefinition))
        {
            return;
        }

        var tokenObjectId = NextTokenObjectId(playerZones, cardObjects, battlefieldObjectId, 1);
        cardObjects[tokenObjectId] = tokenDefinition.CreateObject(
            tokenObjectId,
            playerId,
            playerId);
        playerZones[playerId] = zones with
        {
            Base = zones.Base.Concat([tokenObjectId]).ToArray()
        };
        events.Add(new GameEvent(
            "UNIT_TOKEN_CREATED",
            $"{battlefieldObjectId} 打出随从",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["abilityId"] = "BATTLEFIELD_HELD_CREATE_MINION",
                ["tokenObjectId"] = tokenObjectId,
                ["tokenCardNo"] = tokenDefinition.CardNo,
                ["tokenName"] = tokenDefinition.TokenFamilyName,
                ["power"] = tokenDefinition.DefaultPower,
                ["destinationZone"] = "BASE",
                ["tokenTags"] = tokenDefinition.Tags.ToArray()
            }));
    }

    private static bool TryResolveBattlefieldConquerMillTwoTrigger(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        List<GameEvent> events)
    {
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerMillTwoCardNo(battlefieldState.CardNo)
            || !playerZones.TryGetValue(playerId, out var zones)
            || zones.MainDeck.Count == 0)
        {
            return false;
        }

        var movedCardIds = TakeControlledMainDeckPrefix(cardObjects, playerId, zones.MainDeck, 2);
        if (movedCardIds.Length == 0)
        {
            return false;
        }

        playerZones[playerId] = zones with
        {
            MainDeck = zones.MainDeck.Skip(movedCardIds.Length).ToArray(),
            Graveyard = zones.Graveyard.Concat(movedCardIds).ToArray()
        };

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 征服战场并弃置主牌堆顶牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_CONQUERED_MILL_TOP_TWO",
                ["sourceObjectId"] = sourceObjectId,
                ["count"] = movedCardIds.Length
            }));
        events.Add(new GameEvent(
            "CARDS_MILLED",
            $"{playerId} 将主牌堆顶 {movedCardIds.Length} 张牌放入废牌堆",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["cardIds"] = movedCardIds,
                ["count"] = movedCardIds.Length,
                ["sourceZone"] = "MAIN_DECK",
                ["destinationZone"] = "GRAVEYARD"
            }));
        return true;
    }

    private static RecycleResult ResolveBattlefieldDefendRevealSpellTrigger(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        long rngCursor)
    {
        var events = new List<GameEvent>();
        if (string.IsNullOrWhiteSpace(playerId)
            || !TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldDefendRevealSpellCardNo(battlefieldState.CardNo)
            || !playerZones.TryGetValue(playerId, out var zones)
            || zones.MainDeck.Count == 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var revealedObjectIds = TakeControlledMainDeckPrefix(cardObjects, playerId, zones.MainDeck, 1);
        if (revealedObjectIds.Length == 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var revealedObjectId = revealedObjectIds[0];
        var revealedIsSpell = cardObjects.TryGetValue(revealedObjectId, out var revealedState)
            && revealedState.Tags.Contains(CardObjectTags.SpellCard, StringComparer.Ordinal);
        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 防守战场并展示主牌堆顶牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_DEFENSE_REVEAL_TOP_DRAW_SPELL_OR_RECYCLE",
                ["sourceObjectId"] = sourceObjectId,
                ["revealedObjectId"] = revealedObjectId,
                ["revealedIsSpell"] = revealedIsSpell
            }));
        events.Add(new GameEvent(
            "CARDS_REVEALED",
            $"{playerId} 展示主牌堆顶部 1 张牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["cardIds"] = new[] { revealedObjectId },
                ["count"] = 1,
                ["zone"] = "MAIN_DECK"
            }));

        if (revealedIsSpell)
        {
            playerZones[playerId] = zones with
            {
                MainDeck = zones.MainDeck.Skip(1).ToArray(),
                Hand = zones.Hand.Concat([revealedObjectId]).ToArray()
            };
            events.Add(new GameEvent(
                "CARD_DRAWN",
                $"{playerId} 将展示的法术牌置入手牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = battlefieldObjectId,
                    ["count"] = 1,
                    ["cardIds"] = new[] { revealedObjectId }
                }));
        }
        else
        {
            playerZones[playerId] = zones with
            {
                MainDeck = zones.MainDeck.Skip(1).Concat([revealedObjectId]).ToArray()
            };
            events.Add(new GameEvent(
                "CARDS_RECYCLED",
                $"{playerId} 回收展示的牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = battlefieldObjectId,
                    ["cardIds"] = new[] { revealedObjectId },
                    ["count"] = 1,
                    ["sourceZone"] = "MAIN_DECK",
                    ["destinationZone"] = "MAIN_DECK"
                }));
        }

        return new RecycleResult(events, rngCursor);
    }

    private static RecycleResult ResolveBattlefieldConquerRecycleRuneTrigger(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        long rngCursor)
    {
        var events = new List<GameEvent>();
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerRecycleRuneCardNo(battlefieldState.CardNo)
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return new RecycleResult(events, rngCursor);
        }

        var recycledRuneObjectId = zones.Base.FirstOrDefault(objectId =>
            cardObjects.TryGetValue(objectId, out var cardObject)
            && IsRuneObject(objectId, cardObject)
            && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId)) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(recycledRuneObjectId))
        {
            return new RecycleResult(events, rngCursor);
        }

        playerZones[playerId] = zones with
        {
            Base = zones.Base
                .Where(objectId => !string.Equals(objectId, recycledRuneObjectId, StringComparison.Ordinal))
                .ToArray(),
            MainDeck = zones.MainDeck.Concat([recycledRuneObjectId]).ToArray()
        };

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 征服战场并回收符文",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_CONQUERED_RECYCLE_RUNE",
                ["sourceObjectId"] = sourceObjectId,
                ["targetObjectId"] = recycledRuneObjectId
            }));
        events.Add(new GameEvent(
            "CARDS_RECYCLED",
            $"{playerId} 回收 1 枚符文",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["cardIds"] = new[] { recycledRuneObjectId },
                ["count"] = 1,
                ["sourceZone"] = "BASE",
                ["destinationZone"] = "MAIN_DECK"
            }));

        return new RecycleResult(events, rngCursor);
    }

    private static RecycleResult ResolveBattlefieldConquerRevealRecycleTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        long rngCursor)
    {
        var events = new List<GameEvent>();
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerRevealRecycleCardNo(battlefieldState.CardNo)
            || !playerZones.TryGetValue(playerId, out var zones)
            || zones.MainDeck.Count == 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var revealedObjectIds = TakeControlledMainDeckPrefix(cardObjects, playerId, zones.MainDeck, 2);
        if (revealedObjectIds.Length == 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var recycledObjectIds = RandomizeForMainDeckBottom(
            revealedObjectIds,
            state.Seed,
            rngCursor,
            battlefieldObjectId);
        if (revealedObjectIds.Length > 1)
        {
            rngCursor++;
        }

        playerZones[playerId] = zones with
        {
            MainDeck = zones.MainDeck
                .Skip(revealedObjectIds.Length)
                .Concat(recycledObjectIds)
                .ToArray()
        };

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 征服战场并展示回收顶牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_RECYCLE",
                ["sourceObjectId"] = sourceObjectId,
                ["revealedObjectIds"] = revealedObjectIds,
                ["recycledObjectIds"] = recycledObjectIds,
                ["returnedObjectIds"] = Array.Empty<string>()
            }));
        events.Add(new GameEvent(
            "CARDS_REVEALED",
            $"{playerId} 展示主牌堆顶部 {revealedObjectIds.Length} 张牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["cardIds"] = revealedObjectIds,
                ["count"] = revealedObjectIds.Length,
                ["zone"] = "MAIN_DECK"
            }));
        events.Add(new GameEvent(
            "CARDS_RECYCLED",
            $"{playerId} 回收 {recycledObjectIds.Count} 张牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["cardIds"] = recycledObjectIds,
                ["count"] = recycledObjectIds.Count,
                ["reason"] = "BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_RECYCLE"
            }));
        return new RecycleResult(events, rngCursor);
    }

    private static bool TryResolveBattlefieldConquerDiscardDrawTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, int> playerScores,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        long rngCursor,
        List<GameEvent> events,
        out DrawApplicationResult drawApplication)
    {
        drawApplication = new DrawApplicationResult(playerScores, null, rngCursor);
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerDiscardDrawCardNo(battlefieldState.CardNo))
        {
            return false;
        }

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 征服战场并弃牌抽牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_CONQUERED_DISCARD_DRAW",
                ["sourceObjectId"] = sourceObjectId,
                ["drawCount"] = 1
            }));

        if (playerZones.TryGetValue(playerId, out var zones)
            && zones.Hand.Count > 0)
        {
            var discardedObjectId = zones.Hand.FirstOrDefault(objectId =>
                IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId)) ?? string.Empty;
            if (TryDiscardCardFromHand(playerZones, cardObjects, playerId, discardedObjectId))
            {
                events.Add(new GameEvent(
                    "CARD_DISCARDED",
                    $"{playerId} 因征服战场弃置手牌",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = playerId,
                        ["sourceObjectId"] = battlefieldObjectId,
                        ["targetObjectId"] = discardedObjectId,
                        ["reason"] = "BATTLEFIELD_CONQUERED_DISCARD_DRAW",
                        ["destinationZone"] = "GRAVEYARD"
                    }));
            }
        }

        drawApplication = ApplyDrawToPlayer(
            state,
            playerZones,
            playerScores,
            playerId,
            1,
            rngCursor,
            events);
        return true;
    }

    private static bool TryResolveBattlefieldConquerConsumeBoonDrawTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, int> playerScores,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        long rngCursor,
        List<GameEvent> events,
        out DrawApplicationResult drawApplication)
    {
        drawApplication = new DrawApplicationResult(playerScores, null, rngCursor);
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerConsumeBoonDrawCardNo(battlefieldState.CardNo)
            || !TryGetFirstControlledBoonUnit(playerZones, cardObjects, playerId, sourceObjectId, out var targetObjectId, out var targetState))
        {
            return false;
        }

        var nextPower = Math.Max(0, targetState.Power - 1);
        cardObjects[targetObjectId] = targetState with
        {
            Power = nextPower,
            Tags = targetState.Tags
                .Where(tag => !string.Equals(tag, CardObjectTags.Boon, StringComparison.Ordinal))
                .ToArray()
        };

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 征服战场并消耗增益抽牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_CONQUERED_CONSUME_BOON_DRAW",
                ["sourceObjectId"] = sourceObjectId,
                ["targetObjectId"] = targetObjectId,
                ["drawCount"] = 1
            }));
        events.Add(new GameEvent(
            "BOON_CONSUMED",
            $"{targetObjectId} 因征服战场消耗增益",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["targetObjectId"] = targetObjectId,
                ["previousPower"] = targetState.Power,
                ["power"] = nextPower,
                ["reason"] = "BATTLEFIELD_CONQUERED_CONSUME_BOON_DRAW"
            }));

        drawApplication = ApplyDrawToPlayer(
            state,
            playerZones,
            playerScores,
            playerId,
            1,
            rngCursor,
            events);
        return true;
    }

    private static bool TryGetFirstControlledBoonUnit(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string preferredObjectId,
        out string targetObjectId,
        out CardObjectState targetState)
    {
        targetObjectId = string.Empty;
        targetState = default!;
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        var candidateObjectIds = new[] { preferredObjectId }
            .Concat(zones.Battlefields)
            .Concat(zones.Base)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var candidateObjectId in candidateObjectIds)
        {
            if (cardObjects.TryGetValue(candidateObjectId, out var candidate)
                && candidate.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && candidate.Tags.Contains(CardObjectTags.Boon, StringComparer.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(candidate, playerId)
                && IsObjectOnField(playerZones, candidateObjectId))
            {
                targetObjectId = candidateObjectId;
                targetState = candidate;
                return true;
            }
        }

        return false;
    }

    private static (IReadOnlyDictionary<string, RunePool> RunePools, IReadOnlyList<GameEvent> Events)
        ResolveBattlefieldConquerPayOneReadyLegendTrigger(
            IReadOnlyDictionary<string, PlayerZones> playerZones,
            Dictionary<string, CardObjectState> cardObjects,
            IReadOnlyDictionary<string, RunePool> runePools,
            string playerId,
            string battlefieldId,
            string sourceObjectId)
    {
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerPayOneReadyLegendCardNo(battlefieldState.CardNo)
            || !TryGetFirstExhaustedLegend(playerZones, cardObjects, playerId, out var legendObjectId, out var legendState))
        {
            return (runePools, []);
        }

        var currentPool = runePools.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < BattlefieldReadyLegendManaCost)
        {
            return (runePools, []);
        }

        var nextRunePools = runePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        nextRunePools[playerId] = currentPool with
        {
            Mana = currentPool.Mana - BattlefieldReadyLegendManaCost
        };
        cardObjects[legendObjectId] = legendState with
        {
            IsExhausted = false
        };

        return (nextRunePools,
        [
            new GameEvent(
                "BATTLEFIELD_TRIGGER_RESOLVED",
                $"{playerId} 征服战场并让传奇变为活跃状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["battlefieldId"] = battlefieldId,
                    ["battlefieldObjectId"] = battlefieldObjectId,
                    ["battlefieldCardNo"] = battlefieldState.CardNo,
                    ["trigger"] = "BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND",
                    ["sourceObjectId"] = sourceObjectId,
                    ["legendObjectId"] = legendObjectId
                }),
            new GameEvent(
                "COST_PAID",
                $"{playerId} 支付传奇殿堂征服触发费用",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["mana"] = BattlefieldReadyLegendManaCost,
                    ["power"] = 0,
                    ["reason"] = "BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND"
                }),
            new GameEvent(
                "LEGEND_READIED",
                $"{legendObjectId} 变为活跃状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["reason"] = "BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND"
                })
        ]);
    }

    private static bool TryResolveBattlefieldConquerPowerfulPayOneDrawTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, RunePool> runePools,
        IReadOnlyDictionary<string, int> playerScores,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        long rngCursor,
        List<GameEvent> events,
        out IReadOnlyDictionary<string, RunePool> nextRunePools,
        out DrawApplicationResult drawApplication)
    {
        nextRunePools = runePools;
        drawApplication = new DrawApplicationResult(playerScores, null, rngCursor);
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerPowerfulPayOneDrawCardNo(battlefieldState.CardNo)
            || !TryGetSurvivingPowerfulUnit(cardObjects, playerZones, sourceObjectId, out var powerfulObjectId))
        {
            return false;
        }

        var currentPool = runePools.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < BattlefieldPowerfulDrawManaCost)
        {
            return false;
        }

        var mutableRunePools = runePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        mutableRunePools[playerId] = currentPool with
        {
            Mana = currentPool.Mana - BattlefieldPowerfulDrawManaCost
        };
        nextRunePools = mutableRunePools;
        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 征服战场并以强力单位抽牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW",
                ["sourceObjectId"] = sourceObjectId,
                ["powerfulObjectId"] = powerfulObjectId,
                ["drawCount"] = 1
            }));
        events.Add(new GameEvent(
            "COST_PAID",
            $"{playerId} 支付沉没神庙征服触发费用",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["mana"] = BattlefieldPowerfulDrawManaCost,
                ["power"] = 0,
                ["reason"] = "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW"
            }));
        drawApplication = ApplyDrawToPlayer(
            state,
            playerZones,
            playerScores,
            playerId,
            1,
            rngCursor,
            events);
        return true;
    }

    private static bool TryGetSurvivingPowerfulUnit(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        out string powerfulObjectId)
    {
        powerfulObjectId = string.Empty;
        if (!cardObjects.TryGetValue(objectId, out var candidate)
            || candidate.Power < PowerfulUnitPowerThreshold
            || !candidate.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || !IsObjectOnField(playerZones, objectId))
        {
            return false;
        }

        powerfulObjectId = objectId;
        return true;
    }

    private static bool TryResolveBattlefieldConquerPayOneCreateGoldTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, RunePool> runePools,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        List<GameEvent> events,
        out IReadOnlyDictionary<string, RunePool> nextRunePools)
    {
        nextRunePools = runePools;
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerPayOneCreateGoldCardNo(battlefieldState.CardNo))
        {
            return false;
        }

        var currentPool = runePools.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < BattlefieldGoldManaCost)
        {
            return false;
        }

        var mutableRunePools = runePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        mutableRunePools[playerId] = currentPool with
        {
            Mana = currentPool.Mana - BattlefieldGoldManaCost
        };
        nextRunePools = mutableRunePools;

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 征服战场并打出金币",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD",
                ["sourceObjectId"] = sourceObjectId,
                ["tokenName"] = "金币"
            }));
        events.Add(new GameEvent(
            "COST_PAID",
            $"{playerId} 支付珍宝堆征服触发费用",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["mana"] = BattlefieldGoldManaCost,
                ["power"] = 0,
                ["reason"] = "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD"
            }));
        CreateLegendEquipmentToken(
            playerZones,
            cardObjects,
            playerId,
            battlefieldObjectId,
            "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD",
            "金币",
            [CardObjectTags.EquipmentCard, "金币", "反应"],
            isExhausted: true,
            events);
        return true;
    }

    private static bool TryResolveBattlefieldConquerPayOneReturnUnitCreateSandSoldierTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, RunePool> runePools,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        List<GameEvent> events,
        out IReadOnlyDictionary<string, RunePool> nextRunePools)
    {
        const string abilityId = "BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER";
        nextRunePools = runePools;
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo(battlefieldState.CardNo)
            || !TryGetFirstControlledBattlefieldUnit(
                playerZones,
                cardObjects,
                playerId,
                sourceObjectId,
                out var targetObjectId)
            || !P6TokenFactoryCatalog.TryGetByCardNo(SandSoldierTokenCardNo, out var tokenDefinition))
        {
            return false;
        }

        var currentPool = runePools.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty;
        if (currentPool.Mana < BattlefieldSandSoldierManaCost)
        {
            return false;
        }

        var canResolveBattlefieldReturnTrigger = TryGetBattlefieldUnitReturnContext(
            playerZones,
            cardObjects,
            targetObjectId,
            out var battlefieldReturnPlayerId);
        if (!TryReturnTargetToHand(playerZones, cardObjects, targetObjectId, out var ownerPlayerId, out _))
        {
            return false;
        }

        var mutableRunePools = runePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        mutableRunePools[playerId] = currentPool with
        {
            Mana = currentPool.Mana - BattlefieldSandSoldierManaCost
        };
        nextRunePools = mutableRunePools;

        var tokenObjectId = NextTokenObjectId(playerZones, cardObjects, battlefieldObjectId, 1);
        var tokenState = tokenDefinition.CreateObject(tokenObjectId, playerId, playerId);
        tokenState = tokenState with
        {
            Tags = ApplyAzirSandSoldierTemperedTags(
                playerZones,
                cardObjects,
                playerId,
                tokenState.Tags)
        };
        cardObjects[tokenObjectId] = tokenState;
        var zones = playerZones[playerId];
        playerZones[playerId] = zones with
        {
            Battlefields = zones.Battlefields.Concat([tokenObjectId]).ToArray()
        };

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 征服战场并打出黄沙士兵",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = abilityId,
                ["sourceObjectId"] = sourceObjectId,
                ["returnedObjectId"] = targetObjectId,
                ["ownerPlayerId"] = ownerPlayerId,
                ["tokenObjectId"] = tokenObjectId,
                ["tokenCardNo"] = tokenState.CardNo
            }));
        events.Add(new GameEvent(
            "COST_PAID",
            $"{playerId} 支付帝王神坛征服触发费用",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["mana"] = BattlefieldSandSoldierManaCost,
                ["power"] = 0,
                ["reason"] = abilityId
            }));
        events.Add(new GameEvent(
            "UNIT_RETURNED_TO_HAND",
            $"{targetObjectId} 返回手牌",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = battlefieldObjectId,
                ["targetObjectId"] = targetObjectId,
                ["ownerPlayerId"] = ownerPlayerId,
                ["reason"] = abilityId
            }));
        if (canResolveBattlefieldReturnTrigger
            && TryResolveBattlefieldUnitReturnedCallRuneTrigger(
                playerZones,
                cardObjects,
                nextRunePools,
                battlefieldReturnPlayerId,
                targetObjectId,
                battlefieldObjectId,
                events,
                out var battlefieldReturnRunePools))
        {
            nextRunePools = battlefieldReturnRunePools;
        }
        events.Add(new GameEvent(
            "UNIT_TOKEN_CREATED",
            $"{battlefieldObjectId} 打出黄沙士兵",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["abilityId"] = abilityId,
                ["tokenObjectId"] = tokenObjectId,
                ["tokenCardNo"] = tokenState.CardNo,
                ["tokenName"] = tokenDefinition.TokenFamilyName,
                ["power"] = tokenState.Power,
                ["destinationZone"] = "BATTLEFIELD",
                ["tokenTags"] = tokenState.Tags.ToArray(),
                ["azirTempered"] = tokenState.Tags.Contains(CardEquipmentKeywordNames.Tempered, StringComparer.Ordinal)
            }));
        return true;
    }

    private static bool TryGetFirstControlledBattlefieldUnit(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string preferredObjectId,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        var candidateObjectIds = new[] { preferredObjectId }
            .Concat(zones.Battlefields)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal);
        foreach (var candidateObjectId in candidateObjectIds)
        {
            if (cardObjects.TryGetValue(candidateObjectId, out var candidate)
                && candidate.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(candidate, playerId))
            {
                targetObjectId = candidateObjectId;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveBattlefieldConquerReadyTwoRunesAtEndTrigger(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyList<string> untilEndOfTurnEffects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        List<GameEvent> events,
        out IReadOnlyList<string> nextUntilEndOfTurnEffects)
    {
        const string abilityId = "BATTLEFIELD_CONQUERED_READY_TWO_RUNES_AT_END";
        nextUntilEndOfTurnEffects = untilEndOfTurnEffects;
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerReadyTwoRunesAtEndCardNo(battlefieldState.CardNo)
            || !TryGetFirstRuneObjectsInBase(playerZones, cardObjects, playerId, requiredCount: 2, out var runeObjectIds))
        {
            return false;
        }

        var effectIds = runeObjectIds
            .Select(runeObjectId => BuildBattlefieldConquerReadyRuneAtEndEffectId(playerId, battlefieldObjectId, runeObjectId))
            .ToArray();
        nextUntilEndOfTurnEffects = untilEndOfTurnEffects
            .Concat(effectIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(effectId => effectId, StringComparer.Ordinal)
            .ToArray();

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 征服战场并选择回合结束重置符文",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = abilityId,
                ["sourceObjectId"] = sourceObjectId,
                ["runeObjectIds"] = runeObjectIds,
                ["effectIds"] = effectIds
            }));
        events.Add(new GameEvent(
            "RUNE_READY_SCHEDULED",
            $"{playerId} 选择 {runeObjectIds.Count} 枚符文在回合结束时变为活跃状态",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["abilityId"] = abilityId,
                ["runeObjectIds"] = runeObjectIds,
                ["effectIds"] = effectIds
            }));
        return true;
    }

    private static bool TryGetFirstRuneObjectsInBase(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        int requiredCount,
        out IReadOnlyList<string> runeObjectIds)
    {
        runeObjectIds = [];
        if (requiredCount <= 0
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        var candidates = zones.Base
            .Where(objectId =>
                cardObjects.TryGetValue(objectId, out var cardObject)
                && IsRuneObject(objectId, cardObject)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId))
            .Take(requiredCount)
            .ToArray();
        if (candidates.Length < requiredCount)
        {
            return false;
        }

        runeObjectIds = candidates;
        return true;
    }

    private static string BuildBattlefieldConquerReadyRuneAtEndEffectId(
        string playerId,
        string battlefieldObjectId,
        string runeObjectId)
    {
        return $"{BattlefieldConquerReadyRuneAtEndEffectPrefix}{playerId}:{battlefieldObjectId}:{runeObjectId}";
    }

    private static bool TryParseBattlefieldConquerReadyRuneAtEndEffectId(
        string effectId,
        out string playerId,
        out string battlefieldObjectId,
        out string runeObjectId)
    {
        playerId = string.Empty;
        battlefieldObjectId = string.Empty;
        runeObjectId = string.Empty;
        if (string.IsNullOrWhiteSpace(effectId)
            || !effectId.StartsWith(BattlefieldConquerReadyRuneAtEndEffectPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = effectId[BattlefieldConquerReadyRuneAtEndEffectPrefix.Length..].Split(':');
        if (parts.Length != 3
            || string.IsNullOrWhiteSpace(parts[0])
            || string.IsNullOrWhiteSpace(parts[1])
            || string.IsNullOrWhiteSpace(parts[2]))
        {
            return false;
        }

        playerId = parts[0];
        battlefieldObjectId = parts[1];
        runeObjectId = parts[2];
        return true;
    }

    private static bool TryResolveBattlefieldConquerReadyEquipmentTrigger(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        List<GameEvent> events)
    {
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerReadyEquipmentCardNo(battlefieldState.CardNo)
            || !TryGetFirstExhaustedFriendlyEquipment(playerZones, cardObjects, playerId, out var equipmentObjectId, out var equipmentState))
        {
            return false;
        }

        var previousAttachedToObjectId = equipmentState.AttachedToObjectId;
        var detachesArmament = equipmentState.Tags.Contains("武装", StringComparer.Ordinal)
            && !string.IsNullOrWhiteSpace(previousAttachedToObjectId);
        cardObjects[equipmentObjectId] = equipmentState with
        {
            IsExhausted = false,
            AttachedToObjectId = detachesArmament ? null : equipmentState.AttachedToObjectId
        };

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 征服战场并重置装备",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_CONQUERED_READY_EQUIPMENT",
                ["sourceObjectId"] = sourceObjectId,
                ["equipmentObjectId"] = equipmentObjectId,
                ["detachesArmament"] = detachesArmament
            }));
        events.Add(new GameEvent(
            "EQUIPMENT_READIED",
            $"{equipmentObjectId} 变为活跃状态",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = equipmentObjectId,
                ["reason"] = "BATTLEFIELD_CONQUERED_READY_EQUIPMENT",
                ["wasExhausted"] = equipmentState.IsExhausted,
                ["isExhausted"] = false
            }));
        if (detachesArmament)
        {
            events.Add(new GameEvent(
                "EQUIPMENT_DETACHED",
                $"{equipmentObjectId} 从单位卸除",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = battlefieldObjectId,
                    ["abilityId"] = "BATTLEFIELD_CONQUERED_READY_EQUIPMENT",
                    ["unitObjectId"] = previousAttachedToObjectId,
                    ["equipmentObjectId"] = equipmentObjectId,
                    ["controllerId"] = playerId,
                    ["ownerId"] = string.IsNullOrWhiteSpace(equipmentState.OwnerId) ? playerId : equipmentState.OwnerId,
                    ["previousAttachedToObjectId"] = previousAttachedToObjectId
                }));
        }

        return true;
    }

    private static bool TryResolveBattlefieldConquerOverkillCreateWarhawkTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        int assignedOverkillDamageToEnemyUnits,
        List<GameEvent> events)
    {
        if (assignedOverkillDamageToEnemyUnits < 3
            || !TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerOverkillCreateWarhawkCardNo(battlefieldState.CardNo)
            || !playerZones.TryGetValue(playerId, out var zones)
            || !P6TokenFactoryCatalog.TryGetByCardNo(WarhawkTokenCardNo, out var tokenDefinition))
        {
            return false;
        }

        var tokenObjectId = NextTokenObjectId(playerZones, cardObjects, battlefieldObjectId, 1);
        cardObjects[tokenObjectId] = tokenDefinition.CreateObject(
            tokenObjectId,
            playerId,
            playerId);
        playerZones[playerId] = zones with
        {
            Battlefields = zones.Battlefields.Concat([tokenObjectId]).ToArray()
        };

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 征服战场并打出战鹰",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_CONQUERED_OVERKILL_CREATE_WARHAWK",
                ["sourceObjectId"] = sourceObjectId,
                ["assignedOverkillDamageToEnemyUnits"] = assignedOverkillDamageToEnemyUnits,
                ["tokenObjectId"] = tokenObjectId
            }));
        events.Add(new GameEvent(
            "UNIT_TOKEN_CREATED",
            $"{battlefieldObjectId} 打出战鹰",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["abilityId"] = "BATTLEFIELD_CONQUERED_OVERKILL_CREATE_WARHAWK",
                ["tokenObjectId"] = tokenObjectId,
                ["tokenCardNo"] = tokenDefinition.CardNo,
                ["tokenName"] = tokenDefinition.TokenFamilyName,
                ["power"] = tokenDefinition.DefaultPower,
                ["destinationZone"] = "BATTLEFIELD",
                ["tokenTags"] = tokenDefinition.Tags.ToArray()
            }));
        return true;
    }

    private static bool TryGetFirstExhaustedFriendlyEquipment(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string equipmentObjectId,
        out CardObjectState equipmentState)
    {
        equipmentObjectId = string.Empty;
        equipmentState = new CardObjectState();
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.Base.Concat(zones.Battlefields).OrderBy(objectId => objectId, StringComparer.Ordinal))
        {
            if (!cardObjects.TryGetValue(objectId, out var candidate)
                || !candidate.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
                || !SourceObjectControlledByPlayerOrLegacyOwned(candidate, playerId)
                || !candidate.IsExhausted)
            {
                continue;
            }

            equipmentObjectId = objectId;
            equipmentState = candidate;
            return true;
        }

        return false;
    }

    private static bool TryResolveBattlefieldConquerDrawForOtherBattlefieldsTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, int> playerScores,
        string playerId,
        string battlefieldId,
        string sourceObjectId,
        long rngCursor,
        List<GameEvent> events,
        out DrawApplicationResult drawApplication)
    {
        drawApplication = new DrawApplicationResult(playerScores, null, rngCursor);
        if (!TryGetBattlefieldCardObject(playerZones, cardObjects, battlefieldId, out var battlefieldObjectId, out var battlefieldState)
            || !IsBattlefieldConquerDrawForOtherBattlefieldsCardNo(battlefieldState.CardNo)
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        var otherBattlefieldObjectIds = zones.Battlefields
            .Where(objectId => !string.Equals(objectId, battlefieldObjectId, StringComparison.Ordinal))
            .Where(objectId =>
                cardObjects.TryGetValue(objectId, out var candidate)
                && IsBattlefieldCardObject(candidate)
                && SourceObjectControlledByPlayerOrLegacyOwned(candidate, playerId))
            .ToArray();
        if (otherBattlefieldObjectIds.Length == 0)
        {
            return false;
        }

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 征服战场并按其他战场抽牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldId"] = battlefieldId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_CONQUERED_DRAW_FOR_OTHER_BATTLEFIELDS",
                ["sourceObjectId"] = sourceObjectId,
                ["otherBattlefieldObjectIds"] = otherBattlefieldObjectIds,
                ["drawCount"] = otherBattlefieldObjectIds.Length
            }));
        drawApplication = ApplyDrawToPlayer(
            state,
            playerZones,
            playerScores,
            playerId,
            otherBattlefieldObjectIds.Length,
            rngCursor,
            events);
        return true;
    }

    private static bool TryGetBattlefieldCardObject(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string? battlefieldId,
        out string battlefieldObjectId,
        out CardObjectState battlefieldState)
    {
        battlefieldObjectId = string.Empty;
        battlefieldState = new CardObjectState();
        if (string.IsNullOrWhiteSpace(battlefieldId)
            || !cardObjects.TryGetValue(battlefieldId.Trim(), out var candidate)
            || !IsBattlefieldCardObject(candidate)
            || !IsObjectOnField(playerZones, battlefieldId.Trim()))
        {
            return false;
        }

        battlefieldObjectId = battlefieldId.Trim();
        battlefieldState = candidate;
        return true;
    }

    private static bool TryGetBattlefieldUnitReturnContext(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        out string ownerPlayerId)
    {
        ownerPlayerId = string.Empty;
        foreach (var (playerId, zones) in playerZones)
        {
            if (!zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                || !cardObjects.TryGetValue(targetObjectId, out var targetState)
                || !targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
            {
                continue;
            }

            ownerPlayerId = !string.IsNullOrWhiteSpace(targetState.OwnerId)
                && playerZones.ContainsKey(targetState.OwnerId)
                    ? targetState.OwnerId
                    : playerId;
            return true;
        }

        return false;
    }

    private static bool IsBattlefieldCardObject(CardObjectState cardObject)
    {
        return cardObject.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)
            || IsImplementedBattlefieldCardNo(cardObject.CardNo);
    }

    private static bool IsImplementedBattlefieldCardNo(string? cardNo)
    {
        return IsBattlefieldEphemeralUnitsSteadfastCardNo(cardNo)
            || IsBattlefieldHeldMoveUnitToBaseCardNo(cardNo)
            || IsBattlefieldHoldCreateMinionCardNo(cardNo)
            || IsBattlefieldHoldDrawCardNo(cardNo)
            || IsBattlefieldHoldCallRuneCardNo(cardNo)
            || IsBattlefieldHoldGrantBoonCardNo(cardNo)
            || IsBattlefieldHeldReturnHeroCardNo(cardNo)
            || IsBattlefieldHeldPayPowerScoreCardNo(cardNo)
            || IsBattlefieldDestroyedInBattleRecallCardNo(cardNo)
            || IsBattlefieldGrantLegendAttachArmamentCardNo(cardNo)
            || IsBattlefieldExtraStandbyCardNo(cardNo)
            || IsBattlefieldHeldActivateConquestEffectsCardNo(cardNo)
            || IsBattlefieldConquerConsumeBoonDrawCardNo(cardNo)
            || IsBattlefieldConquerMillTwoCardNo(cardNo)
            || IsBattlefieldHoldEachPlayerCallRuneCardNo(cardNo)
            || IsBattlefieldAllUnitsPowerPlusOneCardNo(cardNo)
            || IsBattlefieldDefenderSteadfastTwoCardNo(cardNo)
            || IsBattlefieldDefendMoveFriendlyUnitToBaseCardNo(cardNo)
            || IsBattlefieldConquerRecycleRuneCardNo(cardNo)
            || IsBattlefieldDefendRevealSpellCardNo(cardNo)
            || IsBattlefieldIsolatedDefenderSteadfastMinusTwoCardNo(cardNo)
            || IsBattlefieldConquerPayOneReadyLegendCardNo(cardNo)
            || IsBattlefieldConquerReadyTwoRunesAtEndCardNo(cardNo)
            || IsBattlefieldConquerDrawForOtherBattlefieldsCardNo(cardNo)
            || IsBattlefieldConquerPowerfulPayOneDrawCardNo(cardNo)
            || IsBattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo(cardNo)
            || IsBattlefieldConquerPayOneCreateGoldCardNo(cardNo)
            || IsBattlefieldConquerReadyEquipmentCardNo(cardNo)
            || IsBattlefieldConquerDiscardDrawCardNo(cardNo)
            || IsBattlefieldConquerOverkillCreateWarhawkCardNo(cardNo)
            || IsBattlefieldIncreaseWinningScoreCardNo(cardNo)
            || IsBattlefieldFirstTurnExtraRuneCardNo(cardNo)
            || IsBattlefieldFirstTurnScoreCardNo(cardNo)
            || IsBattlefieldScoreDelayCardNo(cardNo)
            || IsBattlefieldTurnStartDamageAllUnitsCardNo(cardNo)
            || IsBattlefieldTurnStartDestroyUnitDrawCardNo(cardNo)
            || IsBattlefieldConquerRevealRecycleCardNo(cardNo)
            || IsBattlefieldMovedUnitPowerPlusOneCardNo(cardNo)
            || IsBattlefieldHeldSevenUnitsWinCardNo(cardNo)
            || IsBattlefieldPreventMoveToBaseCardNo(cardNo)
            || IsBattlefieldStaticRoamCardNo(cardNo)
            || IsBattlefieldPreventUnitPlayCardNo(cardNo)
            || IsBattlefieldEchoCostReductionCardNo(cardNo)
            || IsBattlefieldHeldNextSpellEchoCardNo(cardNo)
            || IsBattlefieldEquipmentCostReductionCardNo(cardNo)
            || IsBattlefieldFriendlySpellDrawCardNo(cardNo)
            || IsBattlefieldSpellPowerBonusCardNo(cardNo)
            || IsBattlefieldGrantUnitExperienceCardNo(cardNo)
            || IsBattlefieldHighCostSpellInsightCardNo(cardNo)
            || IsBattlefieldUnitReturnedCallRuneCardNo(cardNo)
            || IsBattlefieldPlayUnitPayOneBoonCardNo(cardNo)
            || IsBattlefieldFirstUnitPlayedMoveOtherToBaseCardNo(cardNo)
            || IsBattlefieldTargetSpellSkillDamageBonusCardNo(cardNo)
            || IsBattlefieldHeldUnitCostIncreaseCardNo(cardNo);
    }

    private static bool IsBattlefieldEphemeralUnitsSteadfastCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldEphemeralUnitsSteadfastCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHeldMoveUnitToBaseCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHeldMoveUnitToBaseCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHoldCreateMinionCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHoldCreateMinionCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHoldDrawCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHoldDrawCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHoldCallRuneCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHoldCallRuneCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHoldGrantBoonCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHoldGrantBoonCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHeldReturnHeroCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHeldReturnHeroCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHeldPayPowerScoreCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHeldPayPowerScoreCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldDestroyedInBattleRecallCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldDestroyedInBattleRecallCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldGrantLegendAttachArmamentCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldGrantLegendAttachArmamentCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldExtraStandbyCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldExtraStandbyCardNo, StringComparison.Ordinal)
            || string.Equals(cardNo, BattlefieldExtraStandbyAltCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHeldActivateConquestEffectsCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHeldActivateConquestEffectsCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerConsumeBoonDrawCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerConsumeBoonDrawCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerMillTwoCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerMillTwoCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHoldEachPlayerCallRuneCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHoldEachPlayerCallRuneCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldAllUnitsPowerPlusOneCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldAllUnitsPowerPlusOneCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldDefenderSteadfastTwoCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldDefenderSteadfastTwoCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldDefendMoveFriendlyUnitToBaseCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldDefendMoveFriendlyUnitToBaseCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerRecycleRuneCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerRecycleRuneCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldDefendRevealSpellCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldDefendRevealSpellCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldIsolatedDefenderSteadfastMinusTwoCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldIsolatedDefenderSteadfastMinusTwoCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerPayOneReadyLegendCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerPayOneReadyLegendCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerReadyTwoRunesAtEndCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerReadyTwoRunesAtEndCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerDrawForOtherBattlefieldsCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerDrawForOtherBattlefieldsCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerPowerfulPayOneDrawCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerPowerfulPayOneDrawCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerPayOneReturnUnitCreateSandSoldierCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerPayOneCreateGoldCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerPayOneCreateGoldCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerReadyEquipmentCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerReadyEquipmentCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerDiscardDrawCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerDiscardDrawCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerOverkillCreateWarhawkCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerOverkillCreateWarhawkCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldIncreaseWinningScoreCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldIncreaseWinningScoreCardNo, StringComparison.Ordinal)
            || string.Equals(cardNo, BattlefieldIncreaseWinningScoreAltCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldFirstTurnExtraRuneCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldFirstTurnExtraRuneCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldFirstTurnScoreCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldFirstTurnScoreCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldScoreDelayCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldScoreDelayCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldTurnStartDamageAllUnitsCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldTurnStartDamageAllUnitsCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldTurnStartDestroyUnitDrawCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldTurnStartDestroyUnitDrawCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldConquerRevealRecycleCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldConquerRevealRecycleCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldMovedUnitPowerPlusOneCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldMovedUnitPowerPlusOneCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHeldSevenUnitsWinCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHeldSevenUnitsWinCardNo, StringComparison.Ordinal)
            || string.Equals(cardNo, BattlefieldHeldSevenUnitsWinAltCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldPreventMoveToBaseCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldPreventMoveToBaseCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldStaticRoamCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldStaticRoamCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldPreventUnitPlayCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldPreventUnitPlayCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldEchoCostReductionCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldEchoCostReductionCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHeldNextSpellEchoCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHeldNextSpellEchoCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldEquipmentCostReductionCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldEquipmentCostReductionCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldFriendlySpellDrawCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldFriendlySpellDrawCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldSpellPowerBonusCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldSpellPowerBonusCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldGrantUnitExperienceCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldGrantUnitExperienceCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHighCostSpellInsightCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHighCostSpellInsightCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldUnitReturnedCallRuneCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldUnitReturnedCallRuneCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldPlayUnitPayOneBoonCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldPlayUnitPayOneBoonCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldFirstUnitPlayedMoveOtherToBaseCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldFirstUnitPlayedMoveOtherToBaseCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldTargetSpellSkillDamageBonusCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldTargetSpellSkillDamageBonusCardNo, StringComparison.Ordinal);
    }

    private static bool IsBattlefieldHeldUnitCostIncreaseCardNo(string? cardNo)
    {
        return string.Equals(cardNo, BattlefieldHeldUnitCostIncreaseCardNo, StringComparison.Ordinal);
    }

    private static int EffectiveWinningScore(MatchState state)
    {
        return EffectiveWinningScore(state.PlayerZones, state.CardObjects);
    }

    private static int EffectiveWinningScore(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects)
    {
        var battlefieldModifier = playerZones
            .Sum(entry => entry.Value.Battlefields.Count(objectId =>
                cardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldIncreaseWinningScoreCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, entry.Key)));
        return BaseWinningScore + battlefieldModifier;
    }

    private static bool PlayerWithinWinningScoreDistance(
        IReadOnlyDictionary<string, int> playerScores,
        int winningScore,
        string playerId,
        int distance)
    {
        return playerScores.TryGetValue(playerId, out var score)
            && score >= winningScore - distance;
    }

    private static int CountControlledBattlefieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        return playerZones.TryGetValue(playerId, out var zones)
            ? zones.Battlefields.Count(objectId =>
                cardObjects.TryGetValue(objectId, out var objectState)
                && objectState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
            : 0;
    }

    private static bool HasRumbleLegendMechanicalSteadfastBonus(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        CardObjectState cardObject)
    {
        if (!cardObject.Tags.Contains("机械", StringComparer.Ordinal))
        {
            return false;
        }

        var location = FindFieldObjectLocation(playerZones, objectId);
        if (location is null || !playerZones.TryGetValue(location.Value.PlayerId, out var zones))
        {
            return false;
        }

        return zones.LegendZone.Any(legendObjectId =>
            state.CardObjects.TryGetValue(legendObjectId, out var legendState)
            && IsRumbleLegendCardNo(legendState.CardNo));
    }

    private static bool IsRumbleLegendCardNo(string? cardNo)
    {
        return cardNo is RumbleLegendCardNo or "SFD·240/221";
    }

    private static int CombatKeywordAmount(
        IReadOnlyList<string> tags,
        string keyword)
    {
        var exactMatchFound = false;
        foreach (var tag in tags)
        {
            if (string.Equals(tag, keyword, StringComparison.Ordinal))
            {
                exactMatchFound = true;
                continue;
            }

            if (tag.StartsWith(keyword, StringComparison.Ordinal)
                && int.TryParse(tag[keyword.Length..], out var amount)
                && amount > 0)
            {
                return amount;
            }
        }

        return exactMatchFound ? 1 : 0;
    }

    private static Dictionary<string, object?> BuildCombatDamagePayload(
        string sourceObjectId,
        string targetObjectId,
        DamageApplicationResult damageApplication,
        string battlefieldId,
        string combatRole,
        int basePower,
        int keywordBonus,
        int combatPower,
        string keyword,
        int staticPowerBonus = 0,
        int? assignmentIndex = null,
        string? assignmentRole = null)
    {
        var payload = BuildDamagePayload(sourceObjectId, targetObjectId, damageApplication);
        payload["battlefieldId"] = battlefieldId;
        payload["combatRole"] = combatRole;
        payload["basePower"] = basePower;
        payload["keyword"] = keyword;
        payload["keywordBonus"] = keywordBonus;
        payload["combatPower"] = combatPower;
        if (staticPowerBonus != 0)
        {
            payload["staticPowerBonus"] = staticPowerBonus;
        }

        if (assignmentIndex is not null)
        {
            payload["assignmentIndex"] = assignmentIndex.Value;
        }

        if (!string.IsNullOrWhiteSpace(assignmentRole))
        {
            payload["assignmentRole"] = assignmentRole;
        }

        return payload;
    }

    private static IReadOnlyList<BattleDamageAssignmentTarget> BuildBattleDamageAssignmentOrder(
        IReadOnlyList<string> defenderObjectIds,
        IReadOnlyDictionary<string, CardObjectState> defenderStates)
    {
        return defenderObjectIds
            .Select((objectId, index) => new
            {
                ObjectId = objectId,
                Index = index,
                Role = BattleDamageAssignmentRole(defenderStates[objectId].Tags),
                Priority = BattleDamageAssignmentPriority(defenderStates[objectId].Tags)
            })
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.Index)
            .Select(item => new BattleDamageAssignmentTarget(item.ObjectId, item.Role))
            .ToArray();
    }

    private static bool HasBattleDamageAssignmentKeyword(IReadOnlyList<string> tags)
    {
        return HasExactCombatKeyword(tags, CardCombatKeywordNames.Bulwark)
            || HasExactCombatKeyword(tags, CardCombatKeywordNames.BackRow);
    }

    private static int BattleDamageAssignmentPriority(IReadOnlyList<string> tags)
    {
        if (HasExactCombatKeyword(tags, CardCombatKeywordNames.Bulwark))
        {
            return 0;
        }

        return HasExactCombatKeyword(tags, CardCombatKeywordNames.BackRow) ? 2 : 1;
    }

    private static string BattleDamageAssignmentRole(IReadOnlyList<string> tags)
    {
        if (HasExactCombatKeyword(tags, CardCombatKeywordNames.Bulwark))
        {
            return "BULWARK_FIRST";
        }

        return HasExactCombatKeyword(tags, CardCombatKeywordNames.BackRow)
            ? "BACK_ROW_LAST"
            : "NORMAL";
    }

    private static bool HasExactCombatKeyword(
        IReadOnlyList<string> tags,
        string keyword)
    {
        return tags.Any(tag => string.Equals(tag, keyword, StringComparison.Ordinal));
    }

    private static bool HasDuplicateObjectIds(IReadOnlyList<string> objectIds)
    {
        var seenObjectIds = new HashSet<string>(StringComparer.Ordinal);
        return objectIds.Any(objectId => !seenObjectIds.Add(objectId));
    }

    private static bool IsReadyFaceUpUnitForMinimalBattle(CardObjectState cardObject)
    {
        return !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && !cardObject.IsFaceDown
            && !cardObject.IsAttacking
            && !cardObject.IsDefending
            && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal);
    }

    private static ResolutionResult ResolveRevealCard(
        MatchState state,
        PlayerIntent intent,
        RevealCardCommand command)
    {
        if (!CardBehaviorRegistry.TryGetByCardNo(command.CardNo, out var behavior))
        {
            return RejectWithCorePrompts(
                state,
                $"Unsupported standby reveal card behavior: {command.CardNo}",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (!state.PlayerZones.TryGetValue(intent.PlayerId, out var zones)
            || !zones.Base.Contains(command.SourceObjectId, StringComparer.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Source card is not in the player's base.",
                ErrorCodes.InvalidTarget);
        }

        var mode = string.IsNullOrWhiteSpace(command.Mode) ? StandbyRevealMode : command.Mode.Trim();
        var destination = string.IsNullOrWhiteSpace(command.Destination)
            ? StandbyRevealDestination
            : command.Destination.Trim();
        var targetObjectIds = NormalizeTargetObjectIds(command.TargetObjectIds);
        var optionalCosts = NormalizeOptionalCosts(command.OptionalCosts);
        var paysStandbyRevealCost = optionalCosts.Count == 1
            && string.Equals(optionalCosts[0], StandbyRevealOptionalCost, StringComparison.Ordinal);
        var revealsInBase = string.Equals(mode, StandbyRevealMode, StringComparison.Ordinal)
            && string.Equals(destination, StandbyRevealDestination, StringComparison.Ordinal)
            && targetObjectIds.Count == 0
            && paysStandbyRevealCost;
        var playsReactionToStack = string.Equals(mode, StandbyReactionMode, StringComparison.Ordinal)
            && string.Equals(destination, StandbyReactionDestination, StringComparison.Ordinal)
            && targetObjectIds.Count == 0
            && paysStandbyRevealCost;
        if (!revealsInBase && !playsReactionToStack)
        {
            return RejectWithCorePrompts(
                state,
                $"Unsupported standby reveal mode for {behavior.DisplayName}.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        if (revealsInBase
            && (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
                || !string.Equals(state.ActivePlayerId, intent.PlayerId, StringComparison.Ordinal)
                || state.StackItems.Count > 0))
        {
            return RejectWithCorePrompts(
                state,
                "REVEAL_CARD is only available during the active player's open main window.",
                ErrorCodes.PhaseNotAllowed);
        }

        if (playsReactionToStack
            && (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
                || !string.Equals(state.TimingState, TimingStates.NeutralClosed, StringComparison.Ordinal)
                || state.StackItems.Count == 0
                || !string.Equals(state.PriorityPlayerId, intent.PlayerId, StringComparison.Ordinal)))
        {
            return RejectWithCorePrompts(
                state,
                "REVEAL_CARD standby reactions require that player to hold priority on a pending stack.",
                ErrorCodes.PhaseNotAllowed);
        }

        if (!state.CardObjects.TryGetValue(command.SourceObjectId, out var sourceState)
            || !sourceState.IsFaceDown)
        {
            return RejectWithCorePrompts(
                state,
                "Source card is not face down.",
                ErrorCodes.InvalidTarget);
        }

        if (string.IsNullOrWhiteSpace(sourceState.CardNo))
        {
            return RejectWithCorePrompts(
                state,
                "Source card identity is unknown for REVEAL_CARD.",
                ErrorCodes.InvalidTarget);
        }

        if (!string.Equals(sourceState.CardNo, behavior.CardNo, StringComparison.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "Source card identity does not match REVEAL_CARD cardNo.",
                ErrorCodes.InvalidTarget);
        }

        if (!SourceObjectControlledByPlayerOrLegacyOwned(sourceState, intent.PlayerId))
        {
            return RejectWithCorePrompts(
                state,
                "Source card is not controlled by the player for REVEAL_CARD.",
                ErrorCodes.InvalidTarget);
        }

        var revealedTags = sourceState.Tags
            .Concat([CardObjectTags.UnitCard])
            .Concat(ParseDelimitedValues(behavior.SourceUnitTags))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
        if (!revealedTags.Contains(CardObjectTags.Standby, StringComparer.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} does not expose the Standby keyword for REVEAL_CARD.",
                ErrorCodes.UnsupportedCardBehavior);
        }

        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        cardObjects[command.SourceObjectId] = sourceState with
        {
            IsFaceDown = false,
            IsAttacking = false,
            IsDefending = false,
            Power = behavior.SourceUnitPower > 0 ? behavior.SourceUnitPower : sourceState.Power,
            Tags = revealedTags,
            ManaCost = behavior.ManaCost,
            CardNo = behavior.CardNo
        };

        IReadOnlyDictionary<string, PlayerZones> playerZones = state.PlayerZones;
        StackItemState? stackItem = null;
        if (playsReactionToStack)
        {
            var reactionPlayerZones = NormalizeZonesForSeats(state);
            reactionPlayerZones[intent.PlayerId] = zones with
            {
                Base = RemoveFromZone(zones.Base, command.SourceObjectId)
            };
            playerZones = reactionPlayerZones;
            stackItem = new StackItemState(
                $"STACK-{state.Tick + 1}-{command.SourceObjectId}",
                intent.PlayerId,
                command.SourceObjectId,
                behavior.EffectKind,
                command.CardNo,
                [],
                behavior.DamageAmount,
                1,
                optionalCosts,
                playedAfterAnotherCardThisTurn: ControllerPlayedAnotherCardThisTurn(state, intent.PlayerId),
                timingContext: StackTimingContextForNewStackItem(state));
        }

        var objectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones);
        if (playsReactionToStack)
        {
            objectLocations[command.SourceObjectId] = new ObjectLocationState(intent.PlayerId, "STACK");
        }

        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = playsReactionToStack ? intent.PlayerId : state.ActivePlayerId,
            CardObjects = cardObjects,
            PlayerZones = playerZones,
            ObjectLocations = objectLocations,
            PriorityPlayerId = playsReactionToStack ? intent.PlayerId : null,
            PassedPriorityPlayerIds = [],
            StackItems = stackItem is null
                ? state.StackItems
                : state.StackItems.Concat([stackItem]).ToArray(),
            PlayerCardsPlayedThisTurn = playsReactionToStack
                ? IncrementPlayerCardsPlayedThisTurn(state, intent.PlayerId)
                : state.PlayerCardsPlayedThisTurn
        };
        var events = new List<GameEvent>
        {
            new(
                "CARD_REVEALED",
                $"{intent.PlayerId} 翻开一张待命牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["sourceObjectId"] = command.SourceObjectId,
                    ["cardNo"] = command.CardNo,
                    ["mode"] = mode,
                    ["destination"] = destination,
                    ["optionalCosts"] = optionalCosts.ToArray(),
                    ["isFaceDown"] = false
                })
        };
        if (stackItem is not null)
        {
            events.AddRange(
            [
                new GameEvent(
                    "CARD_PLAYED",
                    $"{intent.PlayerId} 将{behavior.DisplayName}当作反应牌打出",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = intent.PlayerId,
                        ["sourceObjectId"] = command.SourceObjectId,
                        ["cardNo"] = command.CardNo,
                        ["mode"] = mode,
                        ["destination"] = destination
                    }),
                new GameEvent(
                    "COST_PAID",
                    $"{intent.PlayerId} 支付 0 点费用",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = intent.PlayerId,
                        ["mana"] = 0,
                        ["power"] = 0,
                        ["baseMana"] = 0,
                        ["optionalCosts"] = optionalCosts.ToArray()
                    }),
                new GameEvent(
                    "STACK_ITEM_ADDED",
                    $"{behavior.DisplayName}加入结算链",
                    new Dictionary<string, object?>
                    {
                        ["stackItemId"] = stackItem.StackItemId,
                        ["controllerId"] = stackItem.ControllerId,
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["cardNo"] = stackItem.CardNo,
                        ["targetObjectIds"] = stackItem.TargetObjectIds.ToArray(),
                        ["effectKind"] = stackItem.EffectKind,
                        ["mode"] = mode,
                        ["effectRepeatCount"] = stackItem.EffectRepeatCount,
                        ["playedAfterAnotherCardThisTurn"] = stackItem.PlayedAfterAnotherCardThisTurn
                    })
            ]);
        }

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static bool TryBuildPlayCardPlan(
        MatchState state,
        PlayerIntent intent,
        PlayCardCommand command,
        out PlayCardPlan plan,
        out ResolutionResult rejection)
    {
        plan = default!;
        rejection = default!;

        if (!CardBehaviorRegistry.TryGetByCardNoAndMode(command.CardNo, command.Mode, out var behavior))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Unsupported card behavior or mode: {command.CardNo} {command.Mode}",
                ErrorCodes.UnsupportedCardBehavior);
            return false;
        }

        var timingDecision = CardPermissionKeywordRules.EvaluatePlayTiming(state, intent.PlayerId, behavior);
        if (!timingDecision.IsAllowed)
        {
            rejection = RejectWithCorePrompts(
                state,
                timingDecision.Reason,
                ErrorCodes.PhaseNotAllowed);
            return false;
        }

        if (!state.PlayerZones.TryGetValue(intent.PlayerId, out var zones)
            || !zones.Hand.Contains(command.SourceObjectId, StringComparer.Ordinal))
        {
            rejection = RejectWithCorePrompts(
                state,
                "Source card is not in the player's hand.",
                ErrorCodes.CardNotInHand);
            return false;
        }

        if (state.CardObjects.TryGetValue(command.SourceObjectId, out var sourceState))
        {
            if (string.IsNullOrWhiteSpace(sourceState.CardNo))
            {
                rejection = RejectWithCorePrompts(
                    state,
                    "PLAY_CARD source must expose a known card number.",
                    ErrorCodes.UnsupportedCardBehavior);
                return false;
            }

            if (!string.Equals(sourceState.CardNo, behavior.CardNo, StringComparison.Ordinal))
            {
                rejection = RejectWithCorePrompts(
                    state,
                    "PLAY_CARD source card number must match the submitted card number.",
                    ErrorCodes.InvalidTarget);
                return false;
            }

            if (!SourceObjectControlledByPlayerOrLegacyOwned(sourceState, intent.PlayerId))
            {
                rejection = RejectWithCorePrompts(
                    state,
                    "PLAY_CARD source must be controlled by the acting player.",
                    ErrorCodes.InvalidTarget);
                return false;
            }
        }

        var targetObjectIds = NormalizeTargetObjectIds(command.TargetObjectIds);
        var rengarUnitPlayedTargetObjectId = ExtractRengarUnitPlayedTriggerTarget(
            state,
            intent.PlayerId,
            command.SourceObjectId,
            behavior,
            targetObjectIds,
            out targetObjectIds);
        var leonaStunBoonTargetObjectId = ExtractLeonaStunBoonTriggerTarget(
            state,
            intent.PlayerId,
            command.SourceObjectId,
            behavior,
            targetObjectIds,
            out targetObjectIds);
        if (!HasValidTargetCount(state, intent.PlayerId, behavior, targetObjectIds)
            || !PlayCardTargetsExposeKnownCardNumbers(state, targetObjectIds)
            || !HasValidTotalTargetPower(state, behavior, targetObjectIds)
            || !AreTargetsAfterFirstPowerLessThanFirstTarget(state, behavior, targetObjectIds)
            || !HasRequiredAnyTargetTag(state, behavior, targetObjectIds)
            || !AreAttachDetachTargetsAllowed(state, behavior, targetObjectIds)
            || !HasValidSacredJudgmentKeepTargets(state, command.SourceObjectId, behavior, targetObjectIds)
            || !HasValidEachPlayerTopFiveUnitTargets(state, behavior, targetObjectIds)
            || !IsMainDeckLookWindowControlledByPlayer(state, intent.PlayerId, behavior)
            || targetObjectIds.Where((targetObjectId, targetIndex) =>
                !IsTargetObjectInScope(state, intent.PlayerId, targetObjectId, behavior.TargetScope, targetIndex)
                || !IsMainDeckLookTargetAllowed(state, intent.PlayerId, targetObjectId, targetIndex, behavior)
                || !IsMainDeckTargetTagAllowed(state, targetObjectId, targetIndex, behavior)
                || !IsTargetRequiredTagAllowed(state, targetObjectId, behavior)
                || !IsTargetTagAllowed(state, targetObjectId, behavior)
                || !IsTargetManaCostAllowed(state, intent.PlayerId, targetObjectId, behavior)
                || !IsStackItemTargetConditionAllowed(state, intent.PlayerId, targetObjectId, targetIndex, targetObjectIds, behavior)
                || !IsTargetPowerAllowed(state, targetObjectId, behavior)).Any())
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires {DescribeTargetCount(state, intent.PlayerId, behavior)} {DescribeTargetScope(behavior.TargetScope)} target(s).",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rengarUnitPlayedTargetObjectId)
            && !IsValidRengarUnitPlayedTriggerTarget(
                state,
                command.SourceObjectId,
                rengarUnitPlayedTargetObjectId))
        {
            rejection = RejectWithCorePrompts(
                state,
                "Rengar legend trigger target must be a unit on the board or the played source unit.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(leonaStunBoonTargetObjectId)
            && !IsValidLeonaStunBoonTriggerTarget(
                state,
                intent.PlayerId,
                command.SourceObjectId,
                behavior,
                leonaStunBoonTargetObjectId))
        {
            rejection = RejectWithCorePrompts(
                state,
                "Leona legend trigger target must be a friendly unit on the board or the played source unit.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.DiscardsTargetFromHand
            && targetObjectIds.Contains(command.SourceObjectId, StringComparer.Ordinal))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a different hand card to discard.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        var normalizedCommandOptionalCosts = NormalizeOptionalCosts(command.OptionalCosts);
        if (!TryExtractRecycleRunePaymentResourceActions(
                state,
                intent.PlayerId,
                normalizedCommandOptionalCosts,
                out var behaviorOptionalCosts,
                out var paymentResourceActions,
                out var recycledPaymentRuneObjectIds))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Unsupported payment resource action for {behavior.DisplayName}.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (!TryBuildOptionalCostPlan(
                state,
                intent.PlayerId,
                behaviorOptionalCosts,
                behavior,
                out var optionalCosts,
                out var extraManaCost,
                out var extraPowerCost,
                out var extraPowerCostByTrait,
                out var experienceCost,
                out var optionalCostManaReduction,
                out var effectRepeatCount,
                out var exhaustedOptionalCostTargetObjectIds,
                out var destroyedAdditionalCostTargetObjectIds,
                out var returnedAdditionalCostTargetObjectIds,
                out var discardedOptionalCostTargetObjectIds))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Unsupported optional cost for {behavior.DisplayName}.",
                ErrorCodes.UnsupportedCardBehavior);
            return false;
        }

        if (behavior.RequiresDestroyFriendlyUnitAdditionalCost
            && destroyedAdditionalCostTargetObjectIds.Count != 1)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly unit as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresDestroyFriendlyPowerfulUnitAdditionalCost
            && destroyedAdditionalCostTargetObjectIds.Count != 1)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly powerful unit as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresDestroyFriendlyTraitUnitAdditionalCost
            && destroyedAdditionalCostTargetObjectIds.Count != 1)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly Bird, Cat, Dog, or Poro unit as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresReturnFriendlyEquipmentAdditionalCost
            && returnedAdditionalCostTargetObjectIds.Count != 1)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly equipment as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (exhaustedOptionalCostTargetObjectIds.Any(targetObjectId =>
                !CanExhaustFriendlyUnitAsOptionalCost(state, intent.PlayerId, targetObjectId)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires an active friendly unit for its optional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresDestroyFriendlyUnitAdditionalCost
            && destroyedAdditionalCostTargetObjectIds.Any(targetObjectId =>
                !CanDestroyFriendlyUnitAsAdditionalCost(state, intent.PlayerId, targetObjectId)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly unit as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresDestroyFriendlyPowerfulUnitAdditionalCost
            && destroyedAdditionalCostTargetObjectIds.Any(targetObjectId =>
                !CanDestroyFriendlyPowerfulUnitAsAdditionalCost(state, intent.PlayerId, targetObjectId)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly powerful unit as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresDestroyFriendlyTraitUnitAdditionalCost
            && destroyedAdditionalCostTargetObjectIds.Any(targetObjectId =>
                !CanDestroyFriendlyTraitUnitAsAdditionalCost(state, intent.PlayerId, targetObjectId)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly Bird, Cat, Dog, or Poro unit as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (behavior.RequiresReturnFriendlyEquipmentAdditionalCost
            && returnedAdditionalCostTargetObjectIds.Any(targetObjectId =>
                !CanReturnFriendlyEquipmentAsAdditionalCost(state, intent.PlayerId, targetObjectId)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a friendly equipment as an additional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (!AreTargetsManaCostAtMostDestroyedAdditionalCostUnit(
                state,
                behavior,
                targetObjectIds,
                destroyedAdditionalCostTargetObjectIds))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a graveyard unit with mana cost no greater than the destroyed unit.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (discardedOptionalCostTargetObjectIds.Any(targetObjectId =>
                !CanDiscardHandCardAsOptionalCost(state, intent.PlayerId, command.SourceObjectId, targetObjectId)))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"{behavior.DisplayName} requires a different hand card for its optional cost.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        var battlefieldEchoCostReductionMana = ResolveBattlefieldEchoCostReductionMana(
            state,
            intent.PlayerId,
            behavior,
            optionalCosts,
            extraManaCost);
        extraManaCost = Math.Max(0, extraManaCost - battlefieldEchoCostReductionMana);
        var costReductionMana = ResolveCostReductionMana(state, intent.PlayerId, behavior);
        var battlefieldEquipmentCostReductionMana = ResolveBattlefieldEquipmentCostReductionMana(
            state,
            intent.PlayerId,
            behavior);
        var battlefieldHeldUnitCostIncreaseMana = ResolveBattlefieldHeldUnitCostIncreaseMana(
            state,
            intent.PlayerId,
            behavior);
        var spellshieldTaxMana = ResolveSpellshieldTargetTaxMana(
            state,
            intent.PlayerId,
            behavior,
            targetObjectIds,
            out var spellshieldTaxTargetObjectIds);
        var totalManaCost = Math.Max(0, behavior.ManaCost
                - costReductionMana
                - optionalCostManaReduction
                - battlefieldEquipmentCostReductionMana)
            + extraManaCost
            + battlefieldHeldUnitCostIncreaseMana
            + spellshieldTaxMana;
        var totalPowerCost = extraPowerCost + extraPowerCostByTrait.Values.Sum();
        var totalExperienceCost = experienceCost;
        var currentPool = state.RunePools.TryGetValue(intent.PlayerId, out var runePool) ? runePool : RunePool.Empty;
        var paymentAdjustedPool = ApplyRecycleRunePaymentToPool(
            currentPool,
            state.CardObjects,
            recycledPaymentRuneObjectIds);
        if (!AreRecycleRunePaymentResourceActionsRequired(
                currentPool,
                state.CardObjects,
                recycledPaymentRuneObjectIds,
                extraPowerCost,
                extraPowerCostByTrait))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Payment resource actions are not required to play {behavior.DisplayName}.",
                ErrorCodes.InvalidTarget);
            return false;
        }

        if (currentPool.Mana < totalManaCost)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Not enough mana to play {behavior.DisplayName}.",
                ErrorCodes.InsufficientCost);
            return false;
        }

        if (!CanPayPowerCost(paymentAdjustedPool, extraPowerCost, extraPowerCostByTrait))
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Not enough power to play {behavior.DisplayName}.",
                ErrorCodes.InsufficientCost);
            return false;
        }

        var currentExperience = state.PlayerExperience.TryGetValue(intent.PlayerId, out var availableExperience)
            ? availableExperience
            : 0;
        if (currentExperience < totalExperienceCost)
        {
            rejection = RejectWithCorePrompts(
                state,
                $"Not enough experience to play {behavior.DisplayName}.",
                ErrorCodes.InsufficientCost);
            return false;
        }

        plan = new PlayCardPlan(
            behavior,
            zones,
            targetObjectIds,
            totalManaCost,
            extraPowerCost,
            extraPowerCostByTrait,
            totalPowerCost,
            totalExperienceCost,
            effectRepeatCount,
            optionalCosts,
            costReductionMana,
            optionalCostManaReduction,
            battlefieldEchoCostReductionMana,
            battlefieldEquipmentCostReductionMana,
            battlefieldHeldUnitCostIncreaseMana,
            spellshieldTaxMana,
            spellshieldTaxTargetObjectIds,
            exhaustedOptionalCostTargetObjectIds,
            destroyedAdditionalCostTargetObjectIds,
            returnedAdditionalCostTargetObjectIds,
            discardedOptionalCostTargetObjectIds,
            paymentResourceActions,
            recycledPaymentRuneObjectIds,
            rengarUnitPlayedTargetObjectId,
            leonaStunBoonTargetObjectId);
        return true;
    }

    private static bool CanPassPriority(MatchState state, string playerId)
    {
        return string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal)
            && state.StackItems.Count > 0
            && !string.IsNullOrWhiteSpace(state.PriorityPlayerId)
            && string.Equals(state.PriorityPlayerId, playerId, StringComparison.Ordinal);
    }

    private static bool CanPassFocus(MatchState state, string playerId)
    {
        return string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal)
            && state.StackItems.Count == 0
            && string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.FocusPlayerId)
            && string.Equals(state.FocusPlayerId, playerId, StringComparison.Ordinal);
    }

    private static ResolutionResult ResolvePassPriority(MatchState state, PlayerIntent intent)
    {
        var passedPlayerIds = state.PassedPriorityPlayerIds
            .Concat([intent.PlayerId])
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var seatPlayerIds = SeatPlayerIds(state);
        var events = new List<GameEvent>
        {
            new(
                "PRIORITY_PASSED",
                $"{intent.PlayerId} 让过优先行动权",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["priorityPlayerId"] = state.PriorityPlayerId
                })
        };

        MatchState nextState;
        string? pendingBattlefieldTaskCausePlayerId = null;
        if (seatPlayerIds.All(passedPlayerIds.Contains))
        {
            var resolvedItem = state.StackItems[^1];
            pendingBattlefieldTaskCausePlayerId = resolvedItem.ControllerId;
            var remainingStack = state.StackItems.Take(state.StackItems.Count - 1).ToArray();
            var stackResolution = ResolveStackItemEffect(state, resolvedItem);
            var resolvedStack = stackResolution.StackItems ?? remainingStack;
            var nextStack = RemoveCounteredStackItems(resolvedStack, stackResolution.CounteredStackItemIds);
            var resolvedPlayerZones = stackResolution.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            var resolvedCardObjects = stackResolution.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            var resolvedRunePools = stackResolution.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            var objectLocations = ReconcileObjectLocations(state.ObjectLocations, resolvedPlayerZones);
            var postStackCleanupEvents = Array.Empty<GameEvent>();
            var resolvedDestroyedUnitOwnerIds = stackResolution.DestroyedUnitOwnerIds;
            if (stackResolution.WinnerPlayerId is null)
            {
                var postStackCleanup = RunStateBasedCleanupLoop(
                    resolvedPlayerZones,
                    resolvedCardObjects,
                    resolvedItem,
                    resolvedRunePools,
                    objectLocations: objectLocations);
                resolvedRunePools = postStackCleanup.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                postStackCleanupEvents = postStackCleanup.Events.ToArray();
                resolvedDestroyedUnitOwnerIds = stackResolution.DestroyedUnitOwnerIds
                    .Concat(postStackCleanup.DestroyedUnitOwnerIds)
                    .Where(ownerId => !string.IsNullOrWhiteSpace(ownerId))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(ownerId => ownerId, StringComparer.Ordinal)
                    .ToArray();
            }

            objectLocations = ReconcileObjectLocations(objectLocations, resolvedPlayerZones);
            ApplyResolvedStackSourceLocation(objectLocations, resolvedPlayerZones, resolvedItem);
            var returnsToSpellDuel = nextStack.Length == 0
                && string.Equals(resolvedItem.TimingContext, TimingStates.SpellDuelOpen, StringComparison.Ordinal);
            var nextFocusPlayerId = returnsToSpellDuel
                ? NextPlayerIdAfter(state, resolvedItem.ControllerId)
                : null;
            var nextPriorityPlayerId = nextStack.Length == 0
                ? null
                : nextStack[^1].ControllerId;
            nextState = state with
            {
                Tick = state.Tick + 1,
                ActivePlayerId = nextFocusPlayerId ?? nextPriorityPlayerId ?? state.TurnPlayerId,
                TimingState = nextStack.Length == 0
                    ? returnsToSpellDuel
                        ? TimingStates.SpellDuelOpen
                        : TimingStates.NeutralOpen
                    : state.TimingState,
                PriorityPlayerId = nextPriorityPlayerId,
                PassedPriorityPlayerIds = [],
                StackItems = nextStack,
                PlayerZones = resolvedPlayerZones,
                ObjectLocations = objectLocations,
                PlayerScores = stackResolution.PlayerScores,
                PlayerExperience = stackResolution.PlayerExperience,
                RunePools = resolvedRunePools,
                CardObjects = resolvedCardObjects,
                UntilEndOfTurnEffects = stackResolution.UntilEndOfTurnEffects,
                RngCursor = stackResolution.RngCursor,
                DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(
                    state.DestroyedUnitOwnerIdsThisTurn,
                    resolvedDestroyedUnitOwnerIds),
                Status = stackResolution.WinnerPlayerId is null ? state.Status : MatchStatuses.Finished,
                WinnerPlayerId = stackResolution.WinnerPlayerId ?? state.WinnerPlayerId,
                ExtraTurnPlayerId = stackResolution.ExtraTurnPlayerId ?? state.ExtraTurnPlayerId,
                FocusPlayerId = nextFocusPlayerId,
                PassedFocusPlayerIds = returnsToSpellDuel ? [] : state.PassedFocusPlayerIds
            };
            events.Add(new GameEvent(
                "STACK_ITEM_RESOLVED",
                $"{resolvedItem.StackItemId} 结算",
                new Dictionary<string, object?>
                {
                    ["stackItemId"] = resolvedItem.StackItemId,
                    ["controllerId"] = resolvedItem.ControllerId,
                    ["sourceObjectId"] = resolvedItem.SourceObjectId,
                    ["effectKind"] = resolvedItem.EffectKind
                }));
            events.AddRange(stackResolution.Events);
            events.AddRange(postStackCleanupEvents);
        }
        else
        {
            var nextPriorityPlayerId = NextUnpassedPlayerId(state, intent.PlayerId, passedPlayerIds);
            nextState = state with
            {
                Tick = state.Tick + 1,
                ActivePlayerId = nextPriorityPlayerId,
                PriorityPlayerId = nextPriorityPlayerId,
                PassedPriorityPlayerIds = passedPlayerIds
                    .OrderBy(playerId => playerId, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        if (!string.IsNullOrWhiteSpace(pendingBattlefieldTaskCausePlayerId))
        {
            var taskAdvance = AdvancePendingBattlefieldTasksAfterStateChange(
                nextState,
                pendingBattlefieldTaskCausePlayerId);
            nextState = taskAdvance.State;
            events.AddRange(taskAdvance.Events);
        }

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static ResolutionResult ResolvePassFocus(MatchState state, PlayerIntent intent)
    {
        var passedPlayerIds = state.PassedFocusPlayerIds
            .Concat([intent.PlayerId])
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var seatPlayerIds = SeatPlayerIds(state);
        var events = new List<GameEvent>
        {
            new(
                "FOCUS_PASSED",
                $"{intent.PlayerId} 让过焦点",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["focusPlayerId"] = state.FocusPlayerId
                })
        };

        MatchState nextState;
        if (seatPlayerIds.All(passedPlayerIds.Contains))
        {
            var activeBattlefieldSpellDuelTask = state.PendingTaskQueue.Tasks.FirstOrDefault(task =>
                string.Equals(task.TaskId, state.PendingTaskQueue.ActiveTaskId, StringComparison.Ordinal)
                && string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(task.BattlefieldObjectId));
            var completedBattlefieldObjectIds = activeBattlefieldSpellDuelTask?.BattlefieldObjectId is { Length: > 0 } battlefieldObjectId
                ? new[] { battlefieldObjectId }
                : Array.Empty<string>();
            var untilEndOfTurnEffects = state.UntilEndOfTurnEffects
                .Concat(completedBattlefieldObjectIds.Select(BattlefieldTaskMarkers.SpellDuelCompleted))
                .Where(effectId => !string.IsNullOrWhiteSpace(effectId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(effectId => effectId, StringComparer.Ordinal)
                .ToArray();
            nextState = state with
            {
                Tick = state.Tick + 1,
                ActivePlayerId = state.TurnPlayerId,
                TimingState = TimingStates.NeutralOpen,
                FocusPlayerId = null,
                PassedFocusPlayerIds = [],
                UntilEndOfTurnEffects = untilEndOfTurnEffects
            };
            events.Add(new GameEvent(
                "SPELL_DUEL_CLOSED",
                "所有玩家让过焦点，法术对决关闭",
                new Dictionary<string, object?>
                {
                    ["turnPlayerId"] = state.TurnPlayerId,
                    ["completedBattlefieldObjectIds"] = completedBattlefieldObjectIds
                }));
        }
        else
        {
            var nextFocusPlayerId = NextUnpassedPlayerId(state, intent.PlayerId, passedPlayerIds);
            nextState = state with
            {
                Tick = state.Tick + 1,
                ActivePlayerId = nextFocusPlayerId,
                FocusPlayerId = nextFocusPlayerId,
                PassedFocusPlayerIds = passedPlayerIds
                    .OrderBy(playerId => playerId, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static (MatchState State, IReadOnlyList<GameEvent> Events) AdvancePendingBattlefieldTasksAfterStateChange(
        MatchState state,
        string? causingPlayerId)
    {
        if (string.IsNullOrWhiteSpace(causingPlayerId)
            || !string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal)
            || !string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || state.StackItems.Count > 0
            || state.SpellDuelState.IsActive
            || state.BattleState.IsActive
            || state.PendingCleanupTasks.Any(task => IsPendingStateBasedCleanupTask(task.Kind)))
        {
            return (state, []);
        }

        var battlefield = state.BattlefieldStates.Values
            .Where(candidate => candidate.Contested
                && !state.UntilEndOfTurnEffects.Contains(
                    BattlefieldTaskMarkers.SpellDuelCompleted(candidate.BattlefieldObjectId),
                    StringComparer.Ordinal))
            .OrderBy(candidate => candidate.BattlefieldObjectId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (battlefield is null || battlefield.OccupantControllerIds.Count < 2)
        {
            return (state, []);
        }

        var focusPlayerId = battlefield.OccupantControllerIds.Contains(causingPlayerId, StringComparer.Ordinal)
            ? causingPlayerId.Trim()
            : battlefield.OccupantControllerIds.FirstOrDefault(playerId => state.Seats.ContainsKey(playerId));
        if (string.IsNullOrWhiteSpace(focusPlayerId))
        {
            return (state, []);
        }

        var nextState = state with
        {
            ActivePlayerId = focusPlayerId,
            TimingState = TimingStates.SpellDuelOpen,
            FocusPlayerId = focusPlayerId,
            PassedFocusPlayerIds = [],
            PassedPriorityPlayerIds = []
        };
        var events = new GameEvent[]
        {
            new(
                "BATTLEFIELD_CONTESTED",
                "战场进入争夺状态",
                new Dictionary<string, object?>
                {
                    ["battlefieldObjectId"] = battlefield.BattlefieldObjectId,
                    ["playerId"] = focusPlayerId,
                    ["causedByPlayerId"] = causingPlayerId.Trim(),
                    ["participantControllerIds"] = battlefield.OccupantControllerIds.ToArray(),
                    ["participantObjectIds"] = battlefield.OccupantObjectIds.ToArray()
                }),
            new(
                "SPELL_DUEL_STARTED",
                "争夺战场触发法术对决",
                new Dictionary<string, object?>
                {
                    ["battlefieldObjectId"] = battlefield.BattlefieldObjectId,
                    ["taskId"] = $"task:start-spell-duel:{battlefield.BattlefieldObjectId}",
                    ["reason"] = "BATTLEFIELD_CONTESTED",
                    ["playerId"] = focusPlayerId,
                    ["focusPlayerId"] = focusPlayerId,
                    ["causedByPlayerId"] = causingPlayerId.Trim(),
                    ["participantControllerIds"] = battlefield.OccupantControllerIds.ToArray(),
                    ["participantObjectIds"] = battlefield.OccupantObjectIds.ToArray()
                })
        };

        return (nextState, events);
    }

    private static bool IsPendingStateBasedCleanupTask(string kind)
    {
        return string.Equals(kind, "DESTROY_LETHAL_UNIT", StringComparison.Ordinal)
            || string.Equals(kind, "DESTROY_ZERO_POWER_UNIT", StringComparison.Ordinal)
            || string.Equals(kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal);
    }

    private static ResolutionResult ResolveEndTurn(MatchState state, PlayerIntent intent)
    {
        if (!string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(state.ActivePlayerId, intent.PlayerId, StringComparison.Ordinal)
            || !string.Equals(state.TurnPlayerId, intent.PlayerId, StringComparison.Ordinal)
            || state.StackItems.Count > 0)
        {
            return RejectWithCorePrompts(
                state,
                "END_TURN is only available to the active player during an open main window.",
                ErrorCodes.PhaseNotAllowed);
        }

        var nextPlayerId = !string.IsNullOrWhiteSpace(state.ExtraTurnPlayerId)
            && state.Seats.ContainsKey(state.ExtraTurnPlayerId)
            ? state.ExtraTurnPlayerId
            : NextPlayerId(state);
        var controlReturnResult = ReturnTemporaryControlAtEndTurn(state);
        var turnEndCardObjectsBeforeCleanup = controlReturnResult.CardObjects.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        var battlefieldEndTurnRuneEvents = ReadyRunesForBattlefieldAtTurnEnd(
            controlReturnResult.PlayerZones,
            turnEndCardObjectsBeforeCleanup,
            state.UntilEndOfTurnEffects,
            state.TurnPlayerId);
        var cleanupState = state with
        {
            PlayerZones = controlReturnResult.PlayerZones,
            CardObjects = turnEndCardObjectsBeforeCleanup
        };
        var cleanupResult = ApplyTurnEndCleanup(cleanupState);
        var turnEndPlayerZones = controlReturnResult.PlayerZones.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var turnEndCardObjects = cleanupResult.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var objectLocations = ReconcileObjectLocations(state.ObjectLocations, turnEndPlayerZones);
        var stateBasedCleanup = RunStateBasedCleanupLoop(
            turnEndPlayerZones,
            turnEndCardObjects,
            new StackItemState(
                $"turn-end-cleanup-{state.Tick + 1}",
                state.TurnPlayerId,
                "TURN_END_CLEANUP",
                "TURN_END_CLEANUP"),
            cleanupState.RunePools,
            objectLocations: objectLocations);
        objectLocations = ReconcileObjectLocations(objectLocations, turnEndPlayerZones);
        var annieTurnEndEvents = ReadyRunesForAnnieAtTurnEnd(
            turnEndPlayerZones,
            turnEndCardObjects,
            state.TurnPlayerId);
        var nextTurnState = cleanupState with
        {
            TurnNumber = state.TurnNumber + 1,
            ActivePlayerId = nextPlayerId,
            TurnPlayerId = nextPlayerId,
            Phase = MatchPhases.TurnStart,
            TimingState = TimingStates.NeutralClosed,
            RunePools = ClearRunePools(state),
            PlayerZones = turnEndPlayerZones,
            CardObjects = turnEndCardObjects,
            ObjectLocations = objectLocations,
            UntilEndOfTurnEffects = cleanupResult.UntilEndOfTurnEffects,
            DestroyedUnitOwnerIdsThisTurn = [],
            PlayerCardsPlayedThisTurn = new Dictionary<string, int>(StringComparer.Ordinal),
            ExtraTurnPlayerId = null
        };
        var turnStartResult = ResolveTurnStart(nextTurnState);
        var turnEndEvents = BuildTurnEndEvents(
                state,
                intent.PlayerId,
                nextPlayerId,
                cleanupResult,
                controlReturnResult.Events.Concat(battlefieldEndTurnRuneEvents).ToArray())
            .ToList();
        InsertTurnEndStateBasedCleanupEvents(turnEndEvents, stateBasedCleanup.Events);
        var events = turnEndEvents
            .Concat(annieTurnEndEvents)
            .Concat(turnStartResult.Events)
            .ToArray();

        return turnStartResult with
        {
            Events = events
        };
    }

    private static void InsertTurnEndStateBasedCleanupEvents(
        List<GameEvent> turnEndEvents,
        IReadOnlyList<GameEvent> stateBasedCleanupEvents)
    {
        if (stateBasedCleanupEvents.Count == 0)
        {
            return;
        }

        var turnPlayerAdvancedIndex = turnEndEvents.FindIndex(gameEvent =>
            string.Equals(gameEvent.Kind, "TURN_PLAYER_ADVANCED", StringComparison.Ordinal));
        if (turnPlayerAdvancedIndex < 0)
        {
            turnEndEvents.AddRange(stateBasedCleanupEvents);
            return;
        }

        turnEndEvents.InsertRange(turnPlayerAdvancedIndex, stateBasedCleanupEvents);
    }

    private static ResolutionResult ResolveMulligan(
        MatchState state,
        PlayerIntent intent,
        MulliganCommand command)
    {
        if (!string.Equals(state.Phase, MatchPhases.Mulligan, StringComparison.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "MULLIGAN is only allowed during the opening mulligan phase.",
                ErrorCodes.PhaseNotAllowed);
        }

        var mulliganPlayerId = ResolutionResult.OpeningMulliganPlayerId(state);
        if (!string.Equals(intent.PlayerId, mulliganPlayerId, StringComparison.Ordinal))
        {
            return RejectWithCorePrompts(
                state,
                "It is not this player's mulligan turn.",
                ErrorCodes.PhaseNotAllowed);
        }

        var selectedObjectIds = NormalizeTargetObjectIds(command.HandObjectIds);
        if (selectedObjectIds.Count > OfficialDeckValidator.MaximumMulliganCount)
        {
            return RejectWithCorePrompts(
                state,
                $"MULLIGAN can set aside at most {OfficialDeckValidator.MaximumMulliganCount} cards.",
                ErrorCodes.InvalidTarget);
        }

        if (HasDuplicateObjectIds(selectedObjectIds))
        {
            return RejectWithCorePrompts(
                state,
                "MULLIGAN selected cards must be unique.",
                ErrorCodes.InvalidTarget);
        }

        var playerZones = NormalizeZonesForSeats(state);
        if (!playerZones.TryGetValue(intent.PlayerId, out var zones))
        {
            return RejectWithCorePrompts(
                state,
                "MULLIGAN player has no zones.",
                ErrorCodes.InvalidTarget);
        }

        if (selectedObjectIds.Any(objectId =>
            !zones.Hand.Contains(objectId, StringComparer.Ordinal)
            || !IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, intent.PlayerId, objectId)))
        {
            return RejectWithCorePrompts(
                state,
                "MULLIGAN can only select cards from the player's opening hand.",
                ErrorCodes.InvalidTarget);
        }

        var drawCount = Math.Min(selectedObjectIds.Count, zones.MainDeck.Count);
        var drawnObjectIds = zones.MainDeck.Take(drawCount).ToArray();
        if (drawnObjectIds.Any(objectId =>
            !IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, intent.PlayerId, objectId)))
        {
            return RejectWithCorePrompts(
                state,
                "MULLIGAN can only draw replacement cards from the player's main deck.",
                ErrorCodes.InvalidTarget);
        }

        var remainingMainDeck = zones.MainDeck.Skip(drawCount).ToList();
        var returnedObjectIds = RandomizeForMainDeckBottom(
            selectedObjectIds,
            state.Seed,
            state.RngCursor,
            $"MULLIGAN:{intent.PlayerId}");
        var rngCursor = state.RngCursor;
        if (selectedObjectIds.Count > 1)
        {
            rngCursor++;
        }

        var selectedSet = selectedObjectIds.ToHashSet(StringComparer.Ordinal);
        var nextHand = zones.Hand
            .Where(objectId => !selectedSet.Contains(objectId))
            .Concat(drawnObjectIds)
            .ToArray();
        remainingMainDeck.AddRange(returnedObjectIds);
        playerZones[intent.PlayerId] = zones with
        {
            MainDeck = remainingMainDeck.ToArray(),
            Hand = nextHand
        };
        var objectLocations = state.ObjectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var objectId in drawnObjectIds)
        {
            objectLocations[objectId] = new ObjectLocationState(intent.PlayerId, "HAND");
        }
        foreach (var objectId in returnedObjectIds)
        {
            objectLocations[objectId] = new ObjectLocationState(intent.PlayerId, "MAIN_DECK");
        }

        var completed = state.MulliganCompletedPlayerIds
            .Concat([intent.PlayerId])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
        var nextState = state with
        {
            Tick = state.Tick + 1,
            PlayerZones = playerZones,
            ObjectLocations = objectLocations,
            MulliganCompletedPlayerIds = completed,
            RngCursor = rngCursor
        };
        var events = new List<GameEvent>
        {
            new(
                "MULLIGAN_COMPLETED",
                $"{intent.PlayerId} 完成起手调度",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId,
                    ["setAsideCount"] = selectedObjectIds.Count,
                    ["drawnCount"] = drawnObjectIds.Length,
                    ["returnedCount"] = returnedObjectIds.Count
                })
        };

        if (ResolutionResult.OpeningMulliganPlayerId(nextState) is not null)
        {
            return new ResolutionResult(
                true,
                null,
                nextState,
                events,
                ResolutionResult.BuildSnapshots(nextState),
                BuildCorePrompts(nextState));
        }

        events.Add(new GameEvent(
            "MULLIGAN_PHASE_COMPLETED",
            "双方完成起手调度，开始第一个回合",
            new Dictionary<string, object?>
            {
                ["activePlayerId"] = nextState.ActivePlayerId,
                ["secondActionPlayerId"] = nextState.OpeningSecondActionPlayerId
            }));
        var turnStartState = nextState with
        {
            Phase = MatchPhases.TurnStart,
            TimingState = TimingStates.NeutralClosed
        };
        var turnStart = ResolveTurnStart(turnStartState);
        return turnStart with
        {
            Events = events.Concat(turnStart.Events).ToArray()
        };
    }

    private static ResolutionResult ResolveTurnStart(MatchState state)
    {
        var turnPlayerId = state.TurnPlayerId;
        var playerZones = NormalizeZonesForSeats(state);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var ephemeralCleanupResult = DestroyEphemeralObjectsAtTurnStart(
            playerZones,
            cardObjects,
            turnPlayerId,
            state.Tick);
        var battlefieldStartDamageResult = ApplyBattlefieldTurnStartDamageAllUnits(
            playerZones,
            cardObjects,
            turnPlayerId,
            state.Tick,
            state.RunePools);
        var battlefieldStartDrawResult = ApplyBattlefieldTurnStartDestroyUnitDraw(
            state with
            {
                PlayerZones = playerZones,
                CardObjects = cardObjects
            },
            playerZones,
            cardObjects,
            turnPlayerId,
            state.RngCursor);
        var preStartState = state with
        {
            PlayerZones = playerZones,
            PlayerScores = battlefieldStartDrawResult.PlayerScores,
            RunePools = battlefieldStartDamageResult.RunePools,
            CardObjects = cardObjects,
            RngCursor = battlefieldStartDrawResult.RngCursor
        };
        var currentZones = playerZones.TryGetValue(turnPlayerId, out var zones)
            ? zones
            : PlayerZones.Empty;
        var firstTurnScoreResult = battlefieldStartDrawResult.WinnerPlayerId is null
            ? ApplyBattlefieldFirstTurnScore(preStartState, turnPlayerId)
            : new ScoreApplicationResult(
                battlefieldStartDrawResult.PlayerScores,
                battlefieldStartDrawResult.WinnerPlayerId,
                []);

        var calledRuneTarget = firstTurnScoreResult.WinnerPlayerId is null ? RuneCallCount(preStartState) : 0;
        var calledRunes = TakeControlledRuneDeckPrefix(
            cardObjects,
            turnPlayerId,
            currentZones.RuneDeck,
            calledRuneTarget);
        var remainingRuneDeck = currentZones.RuneDeck.Skip(calledRunes.Length).ToArray();
        var drawResult = firstTurnScoreResult.WinnerPlayerId is null
            ? DrawOne(
                preStartState with
                {
                    PlayerScores = firstTurnScoreResult.PlayerScores
                },
                turnPlayerId,
                currentZones)
            : new DrawResult(
                currentZones.MainDeck,
                currentZones.Graveyard,
                [],
                [],
                firstTurnScoreResult.WinnerPlayerId,
                firstTurnScoreResult.PlayerScores,
                preStartState.RngCursor,
                EffectiveWinningScore(preStartState));

        playerZones[turnPlayerId] = currentZones with
        {
            MainDeck = drawResult.MainDeck,
            RuneDeck = remainingRuneDeck,
            Hand = currentZones.Hand.Concat(drawResult.DrawnCards).ToArray(),
            Graveyard = drawResult.Graveyard,
            Base = currentZones.Base.Concat(calledRunes).ToArray()
        };
        var objectLocations = ReconcileObjectLocations(state.ObjectLocations, playerZones);
        foreach (var runeObjectId in calledRunes)
        {
            objectLocations[runeObjectId] = new ObjectLocationState(turnPlayerId, MoveUnitBaseZone);
        }
        foreach (var drawnObjectId in drawResult.DrawnCards)
        {
            objectLocations[drawnObjectId] = new ObjectLocationState(turnPlayerId, "HAND");
        }
        var events = BuildTurnStartEvents(
                state,
                calledRunes.Length,
                drawResult,
                ephemeralCleanupResult.Events
                    .Concat(battlefieldStartDamageResult.Events)
                    .Concat(battlefieldStartDrawResult.Events)
                    .Concat(firstTurnScoreResult.Events)
                    .ToArray())
            .ToList();
        var playerScores = drawResult.PlayerScores;
        var winnerPlayerId = drawResult.WinnerPlayerId;
        var rngCursor = drawResult.RngCursor;
        if (winnerPlayerId is null
            && TryGetJinxTurnStartDrawCardNo(playerZones, cardObjects, turnPlayerId, out var jinxLegendCardNo))
        {
            events.Add(new GameEvent(
                "LEGEND_TRIGGER_RESOLVED",
                $"{turnPlayerId} 的暴走萝莉回合开始触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = turnPlayerId,
                    ["legendCardNo"] = jinxLegendCardNo,
                    ["trigger"] = "JINX_TURN_START_DRAW_IF_HAND_BELOW_TWO"
                }));
            var jinxDrawApplication = ApplyDrawToPlayer(
                state,
                playerZones,
                playerScores,
                turnPlayerId,
                1,
                rngCursor,
                events);
            playerScores = jinxDrawApplication.PlayerScores;
            winnerPlayerId = jinxDrawApplication.WinnerPlayerId;
            rngCursor = jinxDrawApplication.RngCursor;
            objectLocations = ReconcileObjectLocations(objectLocations, playerZones);
        }

        var nextState = state with
        {
            Tick = state.Tick + 1,
            ActivePlayerId = turnPlayerId,
            Status = winnerPlayerId is null ? state.Status : MatchStatuses.Finished,
            Phase = winnerPlayerId is null ? MatchPhases.Main : state.Phase,
            TimingState = winnerPlayerId is null ? TimingStates.NeutralOpen : state.TimingState,
            RunePools = winnerPlayerId is null ? ClearRunePools(state) : state.RunePools,
            PlayerZones = playerZones,
            PlayerScores = playerScores,
            ObjectLocations = objectLocations,
            CardObjects = cardObjects,
            WinnerPlayerId = winnerPlayerId,
            DestroyedUnitOwnerIdsThisTurn = ephemeralCleanupResult.DestroyedUnitOwnerIds
                .Concat(battlefieldStartDamageResult.DestroyedUnitOwnerIds)
                .Concat(battlefieldStartDrawResult.DestroyedUnitOwnerIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ownerId => ownerId, StringComparer.Ordinal)
                .ToArray(),
            RngCursor = rngCursor
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            BuildCorePrompts(nextState));
    }

    private static bool TryGetJinxTurnStartDrawCardNo(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out string cardNo)
    {
        cardNo = JinxLegendCardNo;
        if (!playerZones.TryGetValue(playerId, out var zones)
            || zones.Hand.Count >= 2)
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (cardObjects.TryGetValue(objectId, out var legendState)
                && IsJinxLegendCardNo(legendState.CardNo))
            {
                cardNo = legendState.CardNo ?? JinxLegendCardNo;
                return true;
            }
        }

        return false;
    }

    private static bool IsJinxLegendCardNo(string? cardNo)
    {
        return cardNo is JinxLegendCardNo or "OGN·251/298" or "OGN·301/298" or "OGN·301*/298";
    }

    private static bool TryGetLuxHighCostSpellDrawCardNo(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        CardBehaviorDefinition playedBehavior,
        out string cardNo)
    {
        cardNo = LuxIntroLegendCardNo;
        if (!IsHighCostSpellForLux(playedBehavior)
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        foreach (var objectId in zones.LegendZone)
        {
            if (cardObjects.TryGetValue(objectId, out var legendState)
                && string.Equals(legendState.CardNo, LuxIntroLegendCardNo, StringComparison.Ordinal))
            {
                cardNo = legendState.CardNo ?? LuxIntroLegendCardNo;
                return true;
            }
        }

        return false;
    }

    private static bool IsHighCostSpellForLux(CardBehaviorDefinition behavior)
    {
        return behavior.ManaCost >= 5
            && !behavior.PlaysSourceToBaseAsUnit
            && !behavior.PlaysSourceToBaseAsEquipment;
    }

    private static IReadOnlyList<GameEvent> ReadyRunesForAnnieAtTurnEnd(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones)
            || !zones.LegendZone.Any(legendObjectId =>
                cardObjects.TryGetValue(legendObjectId, out var legendState)
                && string.Equals(legendState.CardNo, AnnieIntroLegendCardNo, StringComparison.Ordinal)))
        {
            return [];
        }

        var readiedRuneObjectIds = zones.Base
            .Where(objectId =>
                cardObjects.TryGetValue(objectId, out var runeState)
                && runeState.IsExhausted
                && IsRuneObject(objectId, runeState))
            .Take(2)
            .ToArray();
        if (readiedRuneObjectIds.Length == 0)
        {
            return [];
        }

        foreach (var objectId in readiedRuneObjectIds)
        {
            cardObjects[objectId] = cardObjects[objectId] with { IsExhausted = false };
        }

        return
        [
            new GameEvent(
                "LEGEND_TRIGGER_RESOLVED",
                $"{playerId} 的黑暗之女回合结束触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["legendCardNo"] = AnnieIntroLegendCardNo,
                    ["trigger"] = "TURN_END_READY_TWO_RUNES",
                    ["runeObjectIds"] = readiedRuneObjectIds
                }),
            new GameEvent(
                "RUNE_READIED",
                $"{playerId} 让 {readiedRuneObjectIds.Length} 枚符文变为活跃状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["count"] = readiedRuneObjectIds.Length,
                    ["objectIds"] = readiedRuneObjectIds
                })
        ];
    }

    private static IReadOnlyList<GameEvent> ReadyRunesForBattlefieldAtTurnEnd(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyList<string> untilEndOfTurnEffects,
        string turnPlayerId)
    {
        if (!playerZones.TryGetValue(turnPlayerId, out var zones))
        {
            return [];
        }

        var runeObjectIdsByBattlefield = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var effectId in untilEndOfTurnEffects.Distinct(StringComparer.Ordinal))
        {
            if (!TryParseBattlefieldConquerReadyRuneAtEndEffectId(
                    effectId,
                    out var playerId,
                    out var battlefieldObjectId,
                    out var runeObjectId)
                || !string.Equals(playerId, turnPlayerId, StringComparison.Ordinal)
                || !zones.Base.Contains(runeObjectId, StringComparer.Ordinal)
                || !cardObjects.TryGetValue(runeObjectId, out var runeState)
                || !IsRuneObject(runeObjectId, runeState))
            {
                continue;
            }

            cardObjects[runeObjectId] = runeState with
            {
                IsExhausted = false
            };
            if (!runeObjectIdsByBattlefield.TryGetValue(battlefieldObjectId, out var runeObjectIds))
            {
                runeObjectIds = [];
                runeObjectIdsByBattlefield[battlefieldObjectId] = runeObjectIds;
            }
            runeObjectIds.Add(runeObjectId);
        }

        if (runeObjectIdsByBattlefield.Count == 0)
        {
            return [];
        }

        var events = new List<GameEvent>();
        foreach (var (battlefieldObjectId, runeObjectIds) in runeObjectIdsByBattlefield.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var distinctRuneObjectIds = runeObjectIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(objectId => objectId, StringComparer.Ordinal)
                .ToArray();
            var battlefieldCardNo = cardObjects.TryGetValue(battlefieldObjectId, out var battlefieldState)
                ? battlefieldState.CardNo
                : null;
            events.Add(new GameEvent(
                "BATTLEFIELD_TRIGGER_RESOLVED",
                $"{turnPlayerId} 的战场回合结束重置符文",
                new Dictionary<string, object?>
                {
                    ["playerId"] = turnPlayerId,
                    ["battlefieldObjectId"] = battlefieldObjectId,
                    ["battlefieldCardNo"] = battlefieldCardNo,
                    ["trigger"] = "BATTLEFIELD_END_TURN_READY_RUNES",
                    ["runeObjectIds"] = distinctRuneObjectIds
                }));
            events.Add(new GameEvent(
                "RUNE_READIED",
                $"{turnPlayerId} 让 {distinctRuneObjectIds.Length} 枚符文变为活跃状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = turnPlayerId,
                    ["sourceObjectId"] = battlefieldObjectId,
                    ["reason"] = "BATTLEFIELD_END_TURN_READY_RUNES",
                    ["count"] = distinctRuneObjectIds.Length,
                    ["objectIds"] = distinctRuneObjectIds
                }));
        }

        return events;
    }

    private static bool IsRuneObject(string objectId, CardObjectState cardObject)
    {
        return cardObject.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)
            || objectId.Contains("RUNE", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<GameEvent> ResolvePowerfulUnitPlayedRuneLegendTriggers(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string playedUnitObjectId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones)
            || !cardObjects.TryGetValue(playedUnitObjectId, out var playedUnit)
            || !playedUnit.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || playedUnit.Power < PowerfulUnitPowerThreshold)
        {
            return [];
        }

        foreach (var legendObjectId in zones.LegendZone)
        {
            if (!cardObjects.TryGetValue(legendObjectId, out var legendState)
                || legendState.IsExhausted
                || !IsPowerfulUnitRuneLegendCardNo(legendState.CardNo))
            {
                continue;
            }

            var runeCallResult = CallRunes(playerZones, cardObjects, playerId, 1);
            if (runeCallResult.CalledRuneObjectIds.Count == 0)
            {
                return [];
            }

            cardObjects[legendObjectId] = legendState with { IsExhausted = true };
            return
            [
                new GameEvent(
                    "LEGEND_TRIGGER_RESOLVED",
                    $"{playerId} 的强力单位传奇触发",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = playerId,
                        ["legendObjectId"] = legendObjectId,
                        ["legendCardNo"] = legendState.CardNo,
                        ["trigger"] = "POWERFUL_UNIT_PLAYED_CALL_RUNE",
                        ["playedUnitObjectId"] = playedUnitObjectId,
                        ["playedUnitPower"] = playedUnit.Power
                    }),
                new GameEvent(
                    "LEGEND_EXHAUSTED",
                    $"{playerId} 横置传奇以召出符文",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = playerId,
                        ["legendObjectId"] = legendObjectId,
                        ["legendCardNo"] = legendState.CardNo,
                        ["reason"] = "POWERFUL_UNIT_PLAYED_CALL_RUNE"
                    }),
                new GameEvent(
                    "RUNES_CALLED",
                    $"{playerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = playerId,
                        ["sourceObjectId"] = legendObjectId,
                        ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                        ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray()
                    })
            ];
        }

        return [];
    }

    private static IReadOnlyList<GameEvent> ResolveRengarUnitPlayedPowerTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        StackItemState stackItem)
    {
        if (!TryGetRengarUnitPlayedTarget(state, stackItem.StackItemId, out var targetObjectId)
            || !TryGetRengarLegend(
                playerZones,
                cardObjects,
                stackItem.ControllerId,
                out var legendObjectId,
                out var legendCardNo)
            || !IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, targetObjectId)
            || !cardObjects.TryGetValue(targetObjectId, out var targetState)
            || !targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || targetState.IsFaceDown)
        {
            return [];
        }

        var nextTargetState = targetState with
        {
            Power = targetState.Power + 1,
            UntilEndOfTurnPowerModifier = targetState.UntilEndOfTurnPowerModifier + 1
        };
        cardObjects[targetObjectId] = nextTargetState;
        return
        [
            new GameEvent(
                "LEGEND_TRIGGER_RESOLVED",
                $"{stackItem.ControllerId} 的傲之追猎者打出单位触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = stackItem.ControllerId,
                    ["legendObjectId"] = legendObjectId,
                    ["legendCardNo"] = legendCardNo,
                    ["trigger"] = "UNIT_PLAYED_POWER_PLUS_1",
                    ["playedUnitObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = targetObjectId
                }),
            new GameEvent(
                "POWER_MODIFIED_UNTIL_END_OF_TURN",
                $"{targetObjectId} 本回合战力 +1",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = legendObjectId,
                    ["targetObjectId"] = targetObjectId,
                    ["powerDelta"] = 1,
                    ["appliedPowerDelta"] = 1,
                    ["minimumPower"] = 0,
                    ["resultingPower"] = nextTargetState.Power,
                    ["reason"] = "UNIT_PLAYED_POWER_PLUS_1"
                })
        ];
    }

    private static bool TryGetRengarUnitPlayedTarget(
        MatchState state,
        string stackItemId,
        out string targetObjectId)
    {
        var prefix = $"{RengarUnitPlayedTargetEffectPrefix}{stackItemId}:";
        var effectId = state.UntilEndOfTurnEffects.FirstOrDefault(id =>
            id.StartsWith(prefix, StringComparison.Ordinal));
        targetObjectId = string.IsNullOrWhiteSpace(effectId) ? string.Empty : effectId[prefix.Length..];
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static IReadOnlyList<GameEvent> ResolveLeonaStunBoonTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        StackItemState stackItem,
        IReadOnlyList<GameEvent> stackEvents)
    {
        if (!TryGetLeonaStunBoonTarget(state, stackItem.StackItemId, out var targetObjectId)
            || !TryGetLeonaLegend(
                playerZones,
                cardObjects,
                stackItem.ControllerId,
                out var legendObjectId,
                out var legendCardNo)
            || !StackEventsStunnedEnemyUnit(
                playerZones,
                cardObjects,
                stackItem.ControllerId,
                stackEvents)
            || !IsControlledFieldUnit(playerZones, cardObjects, stackItem.ControllerId, targetObjectId))
        {
            return [];
        }

        var events = new List<GameEvent>
        {
            new(
                "LEGEND_TRIGGER_RESOLVED",
                $"{stackItem.ControllerId} 的曙光女神眩晕触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = stackItem.ControllerId,
                    ["legendObjectId"] = legendObjectId,
                    ["legendCardNo"] = legendCardNo,
                    ["trigger"] = "ENEMY_STUNNED_GRANT_BOON",
                    ["targetObjectId"] = targetObjectId
                })
        };
        GrantLegendBoon(
            cardObjects,
            targetObjectId,
            stackItem.ControllerId,
            legendObjectId,
            "LEGEND_TRIGGER_ENEMY_STUNNED_GRANT_BOON",
            events);
        return events;
    }

    private static bool TryGetLeonaStunBoonTarget(
        MatchState state,
        string stackItemId,
        out string targetObjectId)
    {
        var prefix = $"{LeonaStunBoonTargetEffectPrefix}{stackItemId}:";
        var effectId = state.UntilEndOfTurnEffects.FirstOrDefault(id =>
            id.StartsWith(prefix, StringComparison.Ordinal));
        targetObjectId = string.IsNullOrWhiteSpace(effectId) ? string.Empty : effectId[prefix.Length..];
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool StackEventsStunnedEnemyUnit(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        IReadOnlyList<GameEvent> stackEvents)
    {
        return stackEvents.Any(gameEvent =>
            string.Equals(gameEvent.Kind, "STATUS_EFFECT_APPLIED", StringComparison.Ordinal)
            && TryGetPayloadString(gameEvent, "effectId", out var effectId)
            && string.Equals(effectId, "STUNNED", StringComparison.Ordinal)
            && TryGetPayloadString(gameEvent, "targetObjectId", out var targetObjectId)
            && IsEnemyFieldUnit(playerZones, cardObjects, playerId, targetObjectId));
    }

    private static bool TryGetPayloadString(GameEvent gameEvent, string key, out string value)
    {
        value = string.Empty;
        if (!gameEvent.Payload.TryGetValue(key, out var rawValue)
            || rawValue is not string stringValue)
        {
            return false;
        }

        value = stringValue;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static IReadOnlyList<GameEvent> ResolveSivirRuneRecycledGoldTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        StackItemState stackItem,
        IReadOnlyList<GameEvent> stackEvents)
    {
        if (!TryGetSivirLegend(
                playerZones,
                cardObjects,
                stackItem.ControllerId,
                out var legendObjectId,
                out var legendState)
            || legendState.IsExhausted
            || !StackEventsRecycledControllerRune(cardObjects, stackItem.ControllerId, stackEvents))
        {
            return [];
        }

        cardObjects[legendObjectId] = legendState with { IsExhausted = true };
        var events = new List<GameEvent>
        {
            new(
                "LEGEND_TRIGGER_RESOLVED",
                $"{stackItem.ControllerId} 的战争女神回收符文触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = stackItem.ControllerId,
                    ["legendObjectId"] = legendObjectId,
                    ["legendCardNo"] = legendState.CardNo,
                    ["trigger"] = "RUNE_RECYCLED_CREATE_GOLD"
                }),
            new(
                "LEGEND_EXHAUSTED",
                $"{legendObjectId} 变为休眠状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = stackItem.ControllerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["reason"] = "RUNE_RECYCLED_CREATE_GOLD"
                })
        };
        CreateLegendEquipmentToken(
            playerZones,
            cardObjects,
            stackItem.ControllerId,
            legendObjectId,
            "LEGEND_TRIGGER_RUNE_RECYCLED_CREATE_GOLD",
            "金币",
            [CardObjectTags.EquipmentCard],
            isExhausted: true,
            events);
        return events;
    }

    private static IReadOnlyList<GameEvent> ResolveSivirEnemyDestroyedReadyTriggers(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyList<string> destroyedUnitOwnerIds)
    {
        if (destroyedUnitOwnerIds.Count == 0)
        {
            return [];
        }

        var events = new List<GameEvent>();
        foreach (var playerId in playerZones.Keys.OrderBy(playerId => playerId, StringComparer.Ordinal))
        {
            if (!destroyedUnitOwnerIds.Any(ownerId => !string.Equals(ownerId, playerId, StringComparison.Ordinal))
                || !TryGetSivirLegend(
                    playerZones,
                    cardObjects,
                    playerId,
                    out var legendObjectId,
                    out var legendState)
                || !legendState.IsExhausted)
            {
                continue;
            }

            cardObjects[legendObjectId] = legendState with { IsExhausted = false };
            events.Add(new GameEvent(
                "LEGEND_TRIGGER_RESOLVED",
                $"{playerId} 的战争女神敌方单位被摧毁触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["legendObjectId"] = legendObjectId,
                    ["legendCardNo"] = legendState.CardNo,
                    ["trigger"] = "ENEMY_UNIT_DESTROYED_READY"
                }));
            events.Add(new GameEvent(
                "LEGEND_READIED",
                $"{legendObjectId} 变为活跃状态",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = legendObjectId,
                    ["reason"] = "ENEMY_UNIT_DESTROYED_READY"
                }));
        }

        return events;
    }

    private static bool StackEventsRecycledControllerRune(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        IReadOnlyList<GameEvent> stackEvents)
    {
        return stackEvents.Any(gameEvent =>
            string.Equals(gameEvent.Kind, "CARDS_RECYCLED", StringComparison.Ordinal)
            && TryGetPayloadString(gameEvent, "playerId", out var recyclePlayerId)
            && string.Equals(recyclePlayerId, playerId, StringComparison.Ordinal)
            && PayloadStringValues(gameEvent, "cardIds")
                .Any(cardId => cardObjects.TryGetValue(cardId, out var cardState)
                    && cardState.Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)));
    }

    private static IReadOnlyList<string> PayloadStringValues(GameEvent gameEvent, string key)
    {
        if (!gameEvent.Payload.TryGetValue(key, out var rawValue)
            || rawValue is string)
        {
            return [];
        }

        if (rawValue is IEnumerable<string> stringValues)
        {
            return stringValues
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }

        return [];
    }

    private static JhinHighCostSpellTriggerResult ResolveJhinHighCostSpellTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, int> playerScores,
        StackItemState stackItem,
        CardBehaviorDefinition behavior,
        long rngCursor)
    {
        if (behavior.ManaCost < JhinHighCostSpellManaThreshold
            || !ControllerHasJhinLegend(playerZones, cardObjects, stackItem.ControllerId)
            || !cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceState)
            || !sourceState.Tags.Contains(CardObjectTags.SpellCard, StringComparer.Ordinal)
            || !playerZones.TryGetValue(stackItem.ControllerId, out var controllerZones))
        {
            return new JhinHighCostSpellTriggerResult(false, [], playerScores, null, rngCursor);
        }

        var events = new List<GameEvent>();
        var trackedTags = sourceState.Tags
            .Concat([JhinBanishedHighCostSpellMarker])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
        cardObjects[stackItem.SourceObjectId] = sourceState with { Tags = trackedTags };
        controllerZones = controllerZones with
        {
            Banished = controllerZones.Banished.Contains(stackItem.SourceObjectId, StringComparer.Ordinal)
                ? controllerZones.Banished
                : controllerZones.Banished.Concat([stackItem.SourceObjectId]).ToArray()
        };
        playerZones[stackItem.ControllerId] = controllerZones;
        events.Add(new GameEvent(
            "LEGEND_TRIGGER_RESOLVED",
            $"{stackItem.ControllerId} 的戏命师放逐高费法术",
            new Dictionary<string, object?>
            {
                ["playerId"] = stackItem.ControllerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["cardNo"] = stackItem.CardNo,
                ["trigger"] = "HIGH_COST_SPELL_BANISHED"
            }));

        var trackedSpellObjectIds = controllerZones.Banished
            .Where(objectId => cardObjects.TryGetValue(objectId, out var spellState)
                && spellState.Tags.Contains(JhinBanishedHighCostSpellMarker, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        if (trackedSpellObjectIds.Length < JhinCompletionSpellCount)
        {
            return new JhinHighCostSpellTriggerResult(true, events, playerScores, null, rngCursor);
        }

        var completedSpellObjectIds = trackedSpellObjectIds
            .Take(JhinCompletionSpellCount)
            .ToArray();
        controllerZones = playerZones[stackItem.ControllerId];
        playerZones[stackItem.ControllerId] = controllerZones with
        {
            Banished = controllerZones.Banished
                .Where(objectId => !completedSpellObjectIds.Contains(objectId, StringComparer.Ordinal))
                .ToArray(),
            Graveyard = controllerZones.Graveyard
                .Concat(completedSpellObjectIds)
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
        foreach (var objectId in completedSpellObjectIds)
        {
            if (!cardObjects.TryGetValue(objectId, out var completedState))
            {
                continue;
            }

            cardObjects[objectId] = completedState with
            {
                Tags = completedState.Tags
                    .Where(tag => !string.Equals(tag, JhinBanishedHighCostSpellMarker, StringComparison.Ordinal))
                    .ToArray()
            };
        }

        events.Add(new GameEvent(
            "LEGEND_TRIGGER_RESOLVED",
            $"{stackItem.ControllerId} 的戏命师完成四张法术放逐",
            new Dictionary<string, object?>
            {
                ["playerId"] = stackItem.ControllerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["cardNo"] = stackItem.CardNo,
                ["trigger"] = "FOUR_HIGH_COST_SPELLS_COMPLETED",
                ["spellObjectIds"] = completedSpellObjectIds
            }));
        var runeCallResult = CallRunes(
            playerZones,
            cardObjects,
            stackItem.ControllerId,
            JhinCompletionSpellCount);
        events.Add(new GameEvent(
            "RUNES_CALLED",
            $"{stackItem.ControllerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
            new Dictionary<string, object?>
            {
                ["playerId"] = stackItem.ControllerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray()
            }));

        var drawApplication = ApplyDrawToPlayer(
            state,
            playerZones,
            playerScores,
            stackItem.ControllerId,
            1,
            rngCursor,
            events);
        return new JhinHighCostSpellTriggerResult(
            true,
            events,
            drawApplication.PlayerScores,
            drawApplication.WinnerPlayerId,
            drawApplication.RngCursor);
    }

    private static bool IsPowerfulUnitRuneLegendCardNo(string? cardNo)
    {
        return cardNo is VolibearFoundationLegendCardNo
            or "OGN·249/298"
            or "OGN·300/298"
            or "OGN·300*/298"
            or FioraSpiritforgedLegendCardNo
            or "SFD·251/221";
    }

    private static EphemeralCleanupResult DestroyEphemeralObjectsAtTurnStart(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string turnPlayerId,
        long currentTick)
    {
        if (!playerZones.TryGetValue(turnPlayerId, out var zones))
        {
            return EphemeralCleanupResult.Empty;
        }

        var controlledFieldObjectIds = zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId => cardObjects.TryGetValue(objectId, out var objectState)
                && objectState.Tags.Contains(CardObjectTags.Ephemeral, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        if (controlledFieldObjectIds.Length == 0)
        {
            return EphemeralCleanupResult.Empty;
        }

        var events = new List<GameEvent>();
        var destroyedUnitOwnerIds = new List<string>();
        foreach (var objectId in controlledFieldObjectIds)
        {
            var pseudoStackItem = new StackItemState(
                $"TURN-START-{currentTick}-{objectId}",
                turnPlayerId,
                objectId,
                "EPHEMERAL_TURN_START_DESTROY",
                string.Empty,
                [],
                0);
            if (!TryDestroyTarget(playerZones, cardObjects, objectId, out var removalResult))
            {
                continue;
            }

            events.Add(BuildFieldRemovalEvent(
                CardObjectTags.Ephemeral,
                pseudoStackItem,
                objectId,
                removalResult,
                "EPHEMERAL_TURN_START"));
            if (removalResult.WasDestroyed && removalResult.WasUnit)
            {
                destroyedUnitOwnerIds.Add(removalResult.OwnerPlayerId);
            }
        }

        return new EphemeralCleanupResult(
            events,
            destroyedUnitOwnerIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ownerId => ownerId, StringComparer.Ordinal)
                .ToArray());
    }

    private static ResolutionResult RejectWithCorePrompts(
        MatchState state,
        string error,
        string errorCode)
    {
        return new ResolutionResult(
            false,
            error,
            state,
            [],
            ResolutionResult.BuildSnapshots(state),
            BuildCorePrompts(state),
            errorCode);
    }

    private static Dictionary<string, PlayerZones> NormalizeZonesForSeats(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            playerId => state.PlayerZones.TryGetValue(playerId, out var zones) ? zones : PlayerZones.Empty,
            StringComparer.Ordinal);
    }

    private static string StackTimingContextForNewStackItem(MatchState state)
    {
        if (state.StackItems.Count > 0)
        {
            var inheritedContext = state.StackItems[^1].TimingContext;
            if (!string.IsNullOrWhiteSpace(inheritedContext))
            {
                return inheritedContext;
            }
        }

        return state.TimingState;
    }

    private static Dictionary<string, ObjectLocationState> ReconcileObjectLocations(
        IReadOnlyDictionary<string, ObjectLocationState> currentLocations,
        IReadOnlyDictionary<string, PlayerZones> playerZones)
    {
        var next = new Dictionary<string, ObjectLocationState>(currentLocations, StringComparer.Ordinal);
        foreach (var (playerId, zones) in playerZones)
        {
            SetZoneLocations(next, currentLocations, playerId, "MAIN_DECK", zones.MainDeck);
            SetZoneLocations(next, currentLocations, playerId, "RUNE_DECK", zones.RuneDeck);
            SetZoneLocations(next, currentLocations, playerId, "HAND", zones.Hand);
            SetZoneLocations(next, currentLocations, playerId, "BASE", zones.Base);
            SetZoneLocations(next, currentLocations, playerId, MoveUnitBattlefieldZone, zones.Battlefields);
            SetZoneLocations(next, currentLocations, playerId, "GRAVEYARD", zones.Graveyard);
            SetZoneLocations(next, currentLocations, playerId, "BANISHED", zones.Banished);
            SetZoneLocations(next, currentLocations, playerId, "LEGEND", zones.LegendZone);
            SetZoneLocations(next, currentLocations, playerId, "CHAMPION", zones.ChampionZone);
        }

        return next;
    }

    private static void SetZoneLocations(
        Dictionary<string, ObjectLocationState> next,
        IReadOnlyDictionary<string, ObjectLocationState> currentLocations,
        string playerId,
        string zone,
        IReadOnlyList<string> objectIds)
    {
        foreach (var objectId in objectIds.Where(objectId => !string.IsNullOrWhiteSpace(objectId)))
        {
            var preciseBattlefieldObjectId = string.Empty;
            if (string.Equals(zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
                && currentLocations.TryGetValue(objectId, out var currentLocation)
                && string.Equals(currentLocation.PlayerId, playerId, StringComparison.Ordinal)
                && string.Equals(currentLocation.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal))
            {
                preciseBattlefieldObjectId = currentLocation.BattlefieldObjectId ?? string.Empty;
            }

            next[objectId] = new ObjectLocationState(
                playerId,
                zone,
                string.IsNullOrWhiteSpace(preciseBattlefieldObjectId) ? null : preciseBattlefieldObjectId);
        }
    }

    private static void ApplyResolvedStackSourceLocation(
        Dictionary<string, ObjectLocationState> objectLocations,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        StackItemState resolvedItem)
    {
        if (string.IsNullOrWhiteSpace(resolvedItem.SourceObjectId))
        {
            return;
        }

        var sourceObjectId = resolvedItem.SourceObjectId;
        var controllerId = resolvedItem.ControllerId;
        if (!playerZones.TryGetValue(controllerId, out var zones))
        {
            return;
        }

        if (zones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal))
        {
            var preciseDestination = IsStackItemBattlefieldDestination(resolvedItem)
                ? PreciseBattlefieldLocationObjectId(NormalizeMoveUnitLocation(resolvedItem.Destination))
                : null;
            objectLocations[sourceObjectId] = new ObjectLocationState(
                controllerId,
                MoveUnitBattlefieldZone,
                string.IsNullOrWhiteSpace(preciseDestination) ? null : preciseDestination);
            return;
        }

        if (zones.Base.Contains(sourceObjectId, StringComparer.Ordinal))
        {
            objectLocations[sourceObjectId] = new ObjectLocationState(controllerId, "BASE");
            return;
        }

        if (zones.Graveyard.Contains(sourceObjectId, StringComparer.Ordinal))
        {
            objectLocations[sourceObjectId] = new ObjectLocationState(controllerId, "GRAVEYARD");
            return;
        }

        if (zones.Banished.Contains(sourceObjectId, StringComparer.Ordinal))
        {
            objectLocations[sourceObjectId] = new ObjectLocationState(controllerId, "BANISHED");
        }
    }

    private static IReadOnlyList<string> NormalizeTargetObjectIds(IReadOnlyList<string> targetObjectIds)
    {
        return targetObjectIds
            .Where(targetObjectId => !string.IsNullOrWhiteSpace(targetObjectId))
            .Select(targetObjectId => targetObjectId.Trim())
            .ToArray();
    }

    private static ResolutionResult ResolveSurrender(MatchState state, PlayerIntent intent)
    {
        var winnerPlayerId = state.Seats.Keys.FirstOrDefault(playerId =>
            !string.Equals(playerId, intent.PlayerId, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(winnerPlayerId))
        {
            return RejectWithCorePrompts(
                state,
                "SURRENDER requires an opponent.",
                ErrorCodes.PhaseNotAllowed);
        }

        var nextState = state with
        {
            Tick = state.Tick + 1,
            Status = MatchStatuses.Finished,
            WinnerPlayerId = winnerPlayerId
        };
        var winningScore = EffectiveWinningScore(state);
        var events = new[]
        {
            new GameEvent(
                "MATCH_WON",
                $"{winnerPlayerId} 因 {intent.PlayerId} 投降获胜",
                new Dictionary<string, object?>
                {
                    ["winnerPlayerId"] = winnerPlayerId,
                    ["surrenderedPlayerId"] = intent.PlayerId,
                    ["winningScore"] = winningScore,
                    ["reason"] = "SURRENDER"
                })
        };

        return new ResolutionResult(
            true,
            null,
            nextState,
            events,
            ResolutionResult.BuildSnapshots(nextState),
            ResolutionResult.BuildPrompts(nextState));
    }

    private static bool IsAmbushPlayMode(string? mode)
    {
        return string.Equals(mode?.Trim(), AmbushPlayMode, StringComparison.Ordinal);
    }

    private static bool IsStackItemBattlefieldDestination(StackItemState stackItem)
    {
        return stackItem.Destination.StartsWith(
            $"{MoveUnitBattlefieldZone}:",
            StringComparison.Ordinal);
    }

    private static bool TryNormalizeMoveUnitZone(
        string value,
        out string zone,
        out bool usesPreciseLocation)
    {
        zone = string.Empty;
        usesPreciseLocation = false;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (string.Equals(normalized, MoveUnitBaseZone, StringComparison.Ordinal)
            || string.Equals(normalized, MoveUnitBattlefieldZone, StringComparison.Ordinal))
        {
            zone = normalized;
            return true;
        }

        if (normalized.StartsWith(MoveUnitBaseZone + ":", StringComparison.Ordinal)
            || normalized.StartsWith(MoveUnitBattlefieldZone + ":", StringComparison.Ordinal))
        {
            zone = normalized[..normalized.IndexOf(':', StringComparison.Ordinal)];
            usesPreciseLocation = true;
            return true;
        }

        return false;
    }

    private static string NormalizeMoveUnitLocation(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static string PreciseBattlefieldLocationObjectId(string location)
    {
        var index = location.IndexOf(':', StringComparison.Ordinal);
        return index < 0 || index == location.Length - 1
            ? string.Empty
            : location[(index + 1)..];
    }

    private static bool MoveUnitPreciseBattlefieldBelongsToPlayer(string location, string playerId)
    {
        return location.StartsWith(
            $"{MoveUnitBattlefieldZone}:{playerId}-",
            StringComparison.Ordinal);
    }

    private static bool MoveUnitPreciseBattlefieldLocationIsKnownOrAbstract(MatchState state, string location)
    {
        var battlefieldObjectId = PreciseBattlefieldLocationObjectId(location);
        if (string.IsNullOrWhiteSpace(battlefieldObjectId)
            || !state.CardObjects.TryGetValue(battlefieldObjectId, out var cardObject))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(cardObject.CardNo)
            && IsBattlefieldCardObject(cardObject);
    }

    private static bool HasRoamPermission(
        MatchState state,
        string playerId,
        string sourceObjectId,
        CardObjectState sourceState)
    {
        return sourceState.Tags.Contains(MoveUnitRoamKeyword, StringComparer.Ordinal)
            || sourceState.UntilEndOfTurnEffects.Contains(MoveUnitRoamOptionalCost, StringComparer.Ordinal)
            || HasBattlefieldStaticRoamPermission(state, playerId, sourceObjectId);
    }

    private static bool HasBattlefieldStaticRoamPermission(MatchState state, string playerId, string sourceObjectId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal))
        {
            return false;
        }

        return zones.Battlefields.Any(objectId =>
            state.CardObjects.TryGetValue(objectId, out var cardObject)
            && IsBattlefieldStaticRoamCardNo(cardObject.CardNo)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId));
    }

    private static bool HasBattlefieldStaticPreventMoveToBase(MatchState state, string playerId, string sourceObjectId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal))
        {
            return false;
        }

        return zones.Battlefields.Any(objectId =>
            state.CardObjects.TryGetValue(objectId, out var cardObject)
            && IsBattlefieldPreventMoveToBaseCardNo(cardObject.CardNo)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId));
    }

    private static bool HasBattlefieldStaticPreventUnitPlayToBattlefield(
        MatchState state,
        string playerId,
        string destination)
    {
        return destination.StartsWith($"{MoveUnitBattlefieldZone}:", StringComparison.Ordinal)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Battlefields.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldPreventUnitPlayCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId));
    }

    private static IReadOnlyList<GameEvent> ApplyBattlefieldMovedUnitPowerPlusOne(
        MatchState state,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string sourceObjectId,
        string originZone,
        string destinationZone)
    {
        if (!string.Equals(originZone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            || string.Equals(originZone, destinationZone, StringComparison.Ordinal)
            || !state.PlayerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal)
            || !zones.Battlefields.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldMovedUnitPowerPlusOneCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId))
            || !cardObjects.TryGetValue(sourceObjectId, out var sourceState)
            || !sourceState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || sourceState.IsFaceDown)
        {
            return [];
        }

        var nextSourceState = sourceState with
        {
            Power = sourceState.Power + 1,
            UntilEndOfTurnPowerModifier = sourceState.UntilEndOfTurnPowerModifier + 1
        };
        cardObjects[sourceObjectId] = nextSourceState;
        return
        [
            new GameEvent(
                "BATTLEFIELD_TRIGGER_RESOLVED",
                $"{playerId} 的后巷酒吧移动单位触发",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["trigger"] = "BATTLEFIELD_UNIT_MOVED_POWER_PLUS_1",
                    ["targetObjectId"] = sourceObjectId,
                    ["originZone"] = originZone,
                    ["destinationZone"] = destinationZone
                }),
            new GameEvent(
                "POWER_MODIFIED_UNTIL_END_OF_TURN",
                $"{sourceObjectId} 本回合战力 +1",
                new Dictionary<string, object?>
                {
                    ["targetObjectId"] = sourceObjectId,
                    ["powerDelta"] = 1,
                    ["appliedPowerDelta"] = 1,
                    ["minimumPower"] = 0,
                    ["resultingPower"] = nextSourceState.Power,
                    ["reason"] = "BATTLEFIELD_UNIT_MOVED_POWER_PLUS_1"
                })
        ];
    }

    private static IReadOnlyList<string> NormalizeOptionalCosts(IReadOnlyList<string>? optionalCosts)
    {
        return (optionalCosts ?? [])
            .Where(optionalCost => !string.IsNullOrWhiteSpace(optionalCost))
            .Select(optionalCost => optionalCost.Trim())
            .ToArray();
    }

    private static IReadOnlyList<string> MergeDestroyedUnitOwnerIds(
        IReadOnlyList<string> existingOwnerIds,
        IReadOnlyList<string> newOwnerIds)
    {
        return existingOwnerIds
            .Concat(newOwnerIds)
            .Where(ownerId => !string.IsNullOrWhiteSpace(ownerId))
            .Select(ownerId => ownerId.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ownerId => ownerId, StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, RunePool> PayRuneCosts(
        MatchState state,
        string playerId,
        int manaCost,
        int powerCost,
        IReadOnlyDictionary<string, int>? powerCostByTrait = null)
    {
        return PayRuneCosts(
            state.RunePools,
            playerId,
            manaCost,
            powerCost,
            powerCostByTrait);
    }

    private static Dictionary<string, RunePool> PayRuneCosts(
        IReadOnlyDictionary<string, RunePool> currentRunePools,
        string playerId,
        int manaCost,
        int powerCost,
        IReadOnlyDictionary<string, int>? powerCostByTrait = null)
    {
        var runePools = currentRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var currentPool = runePools.TryGetValue(playerId, out var runePool) ? runePool : RunePool.Empty;
        var (remainingAnyPower, remainingPowerByTrait) = PayPowerCost(
            currentPool,
            powerCost,
            powerCostByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal));
        runePools[playerId] = new RunePool(
            currentPool.Mana - manaCost,
            remainingAnyPower,
            remainingPowerByTrait);

        return runePools;
    }

    private static bool CanPayRuneCosts(
        RunePool pool,
        int manaCost,
        int anyPowerCost,
        IReadOnlyDictionary<string, int>? powerCostByTrait = null)
    {
        return manaCost >= 0
            && anyPowerCost >= 0
            && pool.Mana >= manaCost
            && CanPayPowerCost(
                pool,
                anyPowerCost,
                powerCostByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal));
    }

    private static bool CanPayPowerCost(
        RunePool pool,
        int anyPowerCost,
        IReadOnlyDictionary<string, int> powerCostByTrait)
    {
        if (anyPowerCost < 0)
        {
            return false;
        }

        var remainingPowerByTrait = pool.PowerByTrait.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var cost in NormalizePowerCostByTrait(powerCostByTrait))
        {
            if (!remainingPowerByTrait.TryGetValue(cost.Key, out var available)
                || available < cost.Value)
            {
                return false;
            }

            remainingPowerByTrait[cost.Key] = available - cost.Value;
        }

        return pool.Power + remainingPowerByTrait.Values.Sum() >= anyPowerCost;
    }

    private static (int AnyPower, IReadOnlyDictionary<string, int> PowerByTrait) PayPowerCost(
        RunePool pool,
        int anyPowerCost,
        IReadOnlyDictionary<string, int> powerCostByTrait)
    {
        var remainingAnyPower = pool.Power;
        var remainingPowerByTrait = pool.PowerByTrait.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var cost in NormalizePowerCostByTrait(powerCostByTrait))
        {
            remainingPowerByTrait[cost.Key] -= cost.Value;
        }

        var remainingAnyCost = anyPowerCost;
        var paidFromAny = Math.Min(remainingAnyPower, remainingAnyCost);
        remainingAnyPower -= paidFromAny;
        remainingAnyCost -= paidFromAny;

        foreach (var trait in remainingPowerByTrait.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray())
        {
            if (remainingAnyCost <= 0)
            {
                break;
            }

            var paidFromTrait = Math.Min(remainingPowerByTrait[trait], remainingAnyCost);
            remainingPowerByTrait[trait] -= paidFromTrait;
            remainingAnyCost -= paidFromTrait;
        }

        return (
            remainingAnyPower,
            remainingPowerByTrait
                .Where(entry => entry.Value > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal));
    }

    private static IReadOnlyDictionary<string, int> NormalizePowerCostByTrait(
        IReadOnlyDictionary<string, int> powerCostByTrait)
    {
        return powerCostByTrait
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Value > 0)
            .GroupBy(entry => RuneTrait.Normalize(entry.Key), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(entry => Math.Max(0, entry.Value)),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, PlayerZones> RemoveSourceCardFromHand(
        MatchState state,
        string playerId,
        PlayerZones zones,
        string sourceObjectId)
    {
        var playerZones = NormalizeZonesForSeats(state);
        playerZones[playerId] = zones with
        {
            Hand = zones.Hand
                .Where(cardId => !string.Equals(cardId, sourceObjectId, StringComparison.Ordinal))
                .ToArray()
        };

        return playerZones;
    }

    private static IReadOnlyDictionary<string, RunePool> ClearRunePools(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            _ => RunePool.Empty,
            StringComparer.Ordinal);
    }

    private static bool IsBattlefieldObject(MatchState state, string objectId)
    {
        var location = FindFieldObjectLocation(state.PlayerZones, objectId);
        return location is not null
            && string.Equals(location.Value.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && IsCardObjectControlledByPlayerOrLegacyOwned(state.CardObjects, location.Value.PlayerId, objectId);
    }

    private static bool IsTargetObjectInScope(
        MatchState state,
        string playerId,
        string objectId,
        string targetScope,
        int targetIndex = 0)
    {
        return targetScope switch
        {
            CardTargetScopes.BattlefieldUnitOrEquipment => IsBattlefieldObject(state, objectId)
                || IsEquipmentObject(state, objectId),
            CardTargetScopes.AnyUnit => IsBattlefieldObject(state, objectId) || IsBaseObject(state, objectId),
            CardTargetScopes.BaseUnit => IsBaseObject(state, objectId),
            CardTargetScopes.FriendlyUnit => IsPlayerControlledFieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyUnitThenFriendlyUnit => IsPlayerControlledFieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyThenEnemyUnits => targetIndex == 0
                ? IsPlayerControlledFieldObject(state, playerId, objectId)
                : IsEnemyFieldObject(state, playerId, objectId),
            CardTargetScopes.UnitThenItsControllersWeapon => targetIndex == 0
                ? IsFieldUnitObject(state, objectId)
                : IsEquipmentObject(state, objectId),
            CardTargetScopes.FriendlyEquipmentThenEnemyEquipment => targetIndex == 0
                ? IsFriendlyEquipmentObject(state, playerId, objectId)
                : IsEnemyEquipmentObject(state, playerId, objectId),
            CardTargetScopes.FriendlyThenEnemyBattlefieldUnits => targetIndex == 0
                ? IsPlayerControlledFieldObject(state, playerId, objectId)
                : IsEnemyBattlefieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyBattlefieldThenEnemyBattlefieldUnits => targetIndex == 0
                ? IsPlayerControlledBattlefieldObject(state, playerId, objectId)
                : IsEnemyBattlefieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell => targetIndex == 0
                ? IsPlayerControlledBattlefieldObject(state, playerId, objectId)
                : IsStackSpellItem(state, objectId),
            CardTargetScopes.AnyUnitThenFriendlyMainDeckCard => targetIndex == 0
                ? IsBattlefieldObject(state, objectId) || IsBaseObject(state, objectId)
                : IsFriendlyMainDeckCard(state, playerId, objectId),
            CardTargetScopes.FriendlyBattlefieldUnit => IsPlayerControlledBattlefieldObject(state, playerId, objectId),
            CardTargetScopes.FriendlyHandCard => IsFriendlyHandCard(state, playerId, objectId),
            CardTargetScopes.AnyHandCard => IsAnyHandCard(state, objectId),
            CardTargetScopes.FriendlyHandCardThenBattlefieldUnit => targetIndex == 0
                ? IsFriendlyHandCard(state, playerId, objectId)
                : IsBattlefieldObject(state, objectId),
            CardTargetScopes.FriendlyMainDeckCard => IsFriendlyMainDeckCard(state, playerId, objectId),
            CardTargetScopes.FriendlyGraveyardCard => IsFriendlyGraveyardCard(state, playerId, objectId),
            CardTargetScopes.FriendlyBaseUnit => IsPlayerControlledBaseObject(state, playerId, objectId),
            CardTargetScopes.AttackingUnit => IsAttackingBattlefieldObject(state, objectId),
            CardTargetScopes.EnemyAttackingUnit => IsEnemyFieldObject(state, playerId, objectId)
                && IsAttackingBattlefieldObject(state, objectId),
            CardTargetScopes.EnemyBattlefieldUnit => IsEnemyBattlefieldObject(state, playerId, objectId),
            CardTargetScopes.EnemyUnit => IsEnemyFieldObject(state, playerId, objectId),
            CardTargetScopes.EnemyUnitThenEnemyUnit => IsEnemyFieldObject(state, playerId, objectId),
            CardTargetScopes.OpponentHandCard => IsOpponentHandCard(state, playerId, objectId),
            CardTargetScopes.OpponentGraveyardCard => IsOpponentGraveyardCard(state, playerId, objectId),
            CardTargetScopes.OpponentMainDeckTopCard => IsOpponentMainDeckTopCard(state, playerId, objectId),
            CardTargetScopes.AnyMainDeckTopFiveCard => IsAnyMainDeckTopCards(state, objectId, 5),
            CardTargetScopes.Equipment => IsEquipmentObject(state, objectId),
            CardTargetScopes.StackSpell => IsStackSpellItem(state, objectId),
            CardTargetScopes.SacredJudgmentKeepCard => IsSacredJudgmentKeepCandidate(state, objectId),
            _ => IsBattlefieldObject(state, objectId)
        };
    }

    private static bool IsControlledFieldObject(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.CardObjects.ContainsKey(objectId)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && (zones.Base.Contains(objectId, StringComparer.Ordinal)
                || zones.Battlefields.Contains(objectId, StringComparer.Ordinal));
    }

    private static bool IsPlayerControlledFieldObject(MatchState state, string playerId, string objectId)
    {
        return IsControlledFieldObject(state, playerId, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId);
    }

    private static bool IsControlledBattlefieldObject(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.CardObjects.ContainsKey(objectId)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Battlefields.Contains(objectId, StringComparer.Ordinal);
    }

    private static bool IsPlayerControlledBattlefieldObject(MatchState state, string playerId, string objectId)
    {
        return IsControlledBattlefieldObject(state, playerId, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId);
    }

    private static bool IsControlledBaseObject(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.CardObjects.ContainsKey(objectId)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Base.Contains(objectId, StringComparer.Ordinal);
    }

    private static bool IsPlayerControlledBaseObject(MatchState state, string playerId, string objectId)
    {
        return IsControlledBaseObject(state, playerId, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId);
    }

    private static bool IsFriendlyHandCard(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Hand.Contains(objectId, StringComparer.Ordinal)
            && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, playerId, objectId);
    }

    private static bool IsOpponentHandCard(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.Any(entry =>
                !string.Equals(entry.Key, playerId, StringComparison.Ordinal)
                && entry.Value.Hand.Contains(objectId, StringComparer.Ordinal)
                && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, entry.Key, objectId));
    }

    private static bool IsAnyHandCard(MatchState state, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.Any(entry =>
                entry.Value.Hand.Contains(objectId, StringComparer.Ordinal)
                && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, entry.Key, objectId));
    }

    private static bool IsFriendlyMainDeckCard(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.MainDeck.Contains(objectId, StringComparer.Ordinal)
            && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, playerId, objectId);
    }

    private static bool IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(
        MatchState state,
        string playerId,
        string objectId)
    {
        return !state.CardObjects.TryGetValue(objectId, out var cardObject)
            || SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId);
    }

    private static bool IsOpponentMainDeckTopCard(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.Any(entry =>
                !string.Equals(entry.Key, playerId, StringComparison.Ordinal)
                && entry.Value.MainDeck.Count > 0
                && string.Equals(entry.Value.MainDeck[0], objectId, StringComparison.Ordinal)
                && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, entry.Key, objectId));
    }

    private static bool IsAnyMainDeckTopCards(MatchState state, string objectId, int lookCount)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && lookCount > 0
            && state.PlayerZones.Any(entry =>
                entry.Value.MainDeck.Take(lookCount).Contains(objectId, StringComparer.Ordinal)
                && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, entry.Key, objectId));
    }

    private static bool TryGetTopMainDeckOwner(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        int lookCount,
        out string playerId)
    {
        playerId = string.Empty;
        if (string.IsNullOrWhiteSpace(objectId)
            || lookCount <= 0)
        {
            return false;
        }

        foreach (var (candidatePlayerId, zones) in playerZones)
        {
            if (zones.MainDeck
                .Take(lookCount)
                .Contains(objectId, StringComparer.Ordinal)
                && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, candidatePlayerId, objectId))
            {
                playerId = candidatePlayerId;
                return true;
            }
        }

        return false;
    }

    private static bool IsMainDeckLookTargetAllowed(
        MatchState state,
        string playerId,
        string objectId,
        int targetIndex,
        CardBehaviorDefinition behavior)
    {
        if (behavior.MainDeckLookCount <= 0)
        {
            return true;
        }

        if (behavior.PlaysEachPlayerTopFiveUnitToBase)
        {
            return true;
        }

        if (targetIndex < behavior.MainDeckLookTargetStartIndex)
        {
            return true;
        }

        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.MainDeck
                .Take(behavior.MainDeckLookCount)
                .Contains(objectId, StringComparer.Ordinal)
            && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, playerId, objectId);
    }

    private static bool IsMainDeckLookWindowControlledByPlayer(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (behavior.MainDeckLookCount <= 0
            || behavior.PlaysEachPlayerTopFiveUnitToBase)
        {
            return true;
        }

        return state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.MainDeck
                .Take(behavior.MainDeckLookCount)
                .All(objectId => IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, playerId, objectId));
    }

    private static bool IsMainDeckTargetTagAllowed(
        MatchState state,
        string objectId,
        int targetIndex,
        CardBehaviorDefinition behavior)
    {
        if (string.IsNullOrWhiteSpace(behavior.MainDeckTargetRequiredTag)
            || targetIndex < behavior.MainDeckLookTargetStartIndex)
        {
            return true;
        }

        return CardObjectHasTag(state.CardObjects, objectId, behavior.MainDeckTargetRequiredTag);
    }

    private static bool IsTargetTagAllowed(
        MatchState state,
        string objectId,
        CardBehaviorDefinition behavior)
    {
        return string.IsNullOrWhiteSpace(behavior.TargetForbiddenTag)
            || !CardObjectHasTag(state.CardObjects, objectId, behavior.TargetForbiddenTag);
    }

    private static bool IsTargetRequiredTagAllowed(
        MatchState state,
        string objectId,
        CardBehaviorDefinition behavior)
    {
        return string.IsNullOrWhiteSpace(behavior.TargetRequiredTag)
            || CardObjectHasTag(state.CardObjects, objectId, behavior.TargetRequiredTag);
    }

    private static bool IsTargetManaCostAllowed(
        MatchState state,
        string playerId,
        string objectId,
        CardBehaviorDefinition behavior)
    {
        if (behavior.MaxTargetManaCost <= 0
            && !behavior.RequiresTargetManaCostAtMostControllerPower)
        {
            return true;
        }

        if (!TryGetTargetManaCost(state, objectId, behavior, out var targetManaCost))
        {
            return false;
        }

        if (behavior.MaxTargetManaCost > 0
            && targetManaCost > behavior.MaxTargetManaCost)
        {
            return false;
        }

        return !behavior.RequiresTargetManaCostAtMostControllerPower
            || state.RunePools.TryGetValue(playerId, out var runePool)
                && targetManaCost <= runePool.Power;
    }

    private static bool AreTargetsManaCostAtMostDestroyedAdditionalCostUnit(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds,
        IReadOnlyList<string> destroyedAdditionalCostTargetObjectIds)
    {
        if (!behavior.RequiresTargetManaCostAtMostDestroyedAdditionalCostUnit)
        {
            return true;
        }

        if (destroyedAdditionalCostTargetObjectIds.Count != 1
            || !TryGetTargetManaCost(state, destroyedAdditionalCostTargetObjectIds[0], behavior, out var destroyedUnitManaCost))
        {
            return false;
        }

        return targetObjectIds.All(targetObjectId =>
            TryGetTargetManaCost(state, targetObjectId, behavior, out var targetManaCost)
            && targetManaCost <= destroyedUnitManaCost);
    }

    private static bool TryGetTargetManaCost(
        MatchState state,
        string objectId,
        CardBehaviorDefinition behavior,
        out int targetManaCost)
    {
        targetManaCost = 0;
        if (string.Equals(behavior.TargetScope, CardTargetScopes.StackSpell, StringComparison.Ordinal))
        {
            return TryGetStackItemManaCost(state, objectId, out targetManaCost);
        }

        if (!state.CardObjects.TryGetValue(objectId, out var targetState))
        {
            return false;
        }

        targetManaCost = targetState.ManaCost;
        return true;
    }

    private static bool TryGetStackItemManaCost(MatchState state, string stackItemId, out int manaCost)
    {
        manaCost = 0;
        var stackItem = state.StackItems.FirstOrDefault(candidate =>
            string.Equals(candidate.StackItemId, stackItemId, StringComparison.Ordinal));
        if (stackItem is null
            || !CardBehaviorRegistry.TryGetByEffectKind(stackItem.EffectKind, out var targetBehavior))
        {
            return false;
        }

        manaCost = targetBehavior.ManaCost;
        return true;
    }

    private static bool IsStackItemTargetConditionAllowed(
        MatchState state,
        string playerId,
        string objectId,
        int targetIndex,
        IReadOnlyList<string> targetObjectIds,
        CardBehaviorDefinition behavior)
    {
        if (!behavior.RequiresTargetStackItemControlledByEnemy
            && !behavior.RequiresTargetStackItemTargetsFriendlyUnitOrEquipment
            && !behavior.RequiresTargetStackItemTargetsFirstTarget
            && !behavior.RequiresTargetStackItemTargetsNoOtherFriendlyUnits
            && !behavior.AppliesPowerModifierToFirstTargetFromSecondStackSpellManaCost)
        {
            return true;
        }

        if (!IsStackItemTargetIndex(behavior.TargetScope, targetIndex))
        {
            return true;
        }

        var stackItem = state.StackItems.FirstOrDefault(candidate =>
            string.Equals(candidate.StackItemId, objectId, StringComparison.Ordinal));
        if (stackItem is null)
        {
            return false;
        }

        if (behavior.RequiresTargetStackItemControlledByEnemy
            && string.Equals(stackItem.ControllerId, playerId, StringComparison.Ordinal))
        {
            return false;
        }

        if (behavior.RequiresTargetStackItemTargetsFriendlyUnitOrEquipment
            && !stackItem.TargetObjectIds.Any(targetObjectId =>
                IsFriendlyUnitOrEquipmentObject(state, playerId, targetObjectId)))
        {
            return false;
        }

        if (behavior.RequiresTargetStackItemTargetsFirstTarget
            && (targetObjectIds.Count == 0
                || !stackItem.TargetObjectIds.Contains(targetObjectIds[0], StringComparer.Ordinal)))
        {
            return false;
        }

        return !behavior.RequiresTargetStackItemTargetsNoOtherFriendlyUnits
            || !stackItem.TargetObjectIds.Any(targetObjectId =>
                targetObjectIds.Count > 0
                && !string.Equals(targetObjectId, targetObjectIds[0], StringComparison.Ordinal)
                && IsPlayerControlledFieldObject(state, playerId, targetObjectId)
                && CardObjectHasTag(state.CardObjects, targetObjectId, CardObjectTags.UnitCard));
    }

    private static bool IsStackItemTargetIndex(string targetScope, int targetIndex)
    {
        return string.Equals(targetScope, CardTargetScopes.StackSpell, StringComparison.Ordinal)
            ? targetIndex == 0
            : string.Equals(targetScope, CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell, StringComparison.Ordinal)
                && targetIndex == 1;
    }

    private static int ResolveSpellshieldTargetTaxMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds,
        out IReadOnlyList<string> spellshieldTaxTargetObjectIds)
    {
        spellshieldTaxTargetObjectIds = [];
        if (behavior.PlaysSourceToBaseAsUnit
            || behavior.PlaysSourceToBaseAsEquipment
            || targetObjectIds.Count == 0)
        {
            return 0;
        }

        return ResolveSpellshieldTargetTaxMana(
            state,
            playerId,
            targetObjectIds,
            out spellshieldTaxTargetObjectIds);
    }

    private static int ResolveSpellshieldTargetTaxMana(
        MatchState state,
        string playerId,
        IReadOnlyList<string> targetObjectIds,
        out IReadOnlyList<string> spellshieldTaxTargetObjectIds)
    {
        spellshieldTaxTargetObjectIds = [];
        if (targetObjectIds.Count == 0)
        {
            return 0;
        }

        var taxedTargetObjectIds = new List<string>();
        var taxMana = 0;
        foreach (var targetObjectId in targetObjectIds)
        {
            if (!IsEnemyFieldObject(state, playerId, targetObjectId)
                || !state.CardObjects.TryGetValue(targetObjectId, out var targetState))
            {
                continue;
            }

            var targetTax = CardResourceKeywordRules.SpellshieldTaxFromTags(targetState.Tags);
            if (targetTax <= 0)
            {
                continue;
            }

            taxMana += targetTax;
            taxedTargetObjectIds.Add(targetObjectId);
        }

        spellshieldTaxTargetObjectIds = taxedTargetObjectIds.ToArray();
        return taxMana;
    }

    private static bool IsFriendlyUnitOrEquipmentObject(MatchState state, string playerId, string objectId)
    {
        return IsPlayerControlledFieldObject(state, playerId, objectId)
            && (CardObjectHasTag(state.CardObjects, objectId, CardObjectTags.UnitCard)
                || CardObjectHasTag(state.CardObjects, objectId, CardObjectTags.EquipmentCard));
    }

    private static bool AreAttachDetachTargetsAllowed(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        if (!behavior.AttachesOrDetachesSecondTargetEquipmentToFirstTarget)
        {
            return true;
        }

        if (targetObjectIds.Count != 2)
        {
            return false;
        }

        var unitObjectId = targetObjectIds[0];
        var equipmentObjectId = targetObjectIds[1];
        if (!IsFieldUnitObject(state, unitObjectId)
            || !IsEquipmentObject(state, equipmentObjectId)
            || !CardObjectHasTag(state.CardObjects, equipmentObjectId, "武装")
            || !TryGetFieldControllerId(state.PlayerZones, unitObjectId, out var unitControllerId)
            || !TryGetFieldControllerId(state.PlayerZones, equipmentObjectId, out var equipmentControllerId)
            || !string.Equals(unitControllerId, equipmentControllerId, StringComparison.Ordinal)
            || !state.CardObjects.TryGetValue(unitObjectId, out var unitState)
            || !state.CardObjects.TryGetValue(equipmentObjectId, out var equipmentState))
        {
            return false;
        }

        return FieldIdentityMatchesZone(unitState, unitControllerId)
            && FieldIdentityMatchesZone(equipmentState, equipmentControllerId)
            && (string.IsNullOrWhiteSpace(equipmentState.AttachedToObjectId)
                || string.Equals(equipmentState.AttachedToObjectId, unitObjectId, StringComparison.Ordinal));
    }

    private static bool HasValidSacredJudgmentKeepTargets(
        MatchState state,
        string sourceObjectId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        if (!behavior.RecyclesUnkeptSacredJudgmentCards)
        {
            return true;
        }

        var targetSet = targetObjectIds.ToHashSet(StringComparer.Ordinal);
        if (targetSet.Contains(sourceObjectId))
        {
            return false;
        }

        var categorizedTargetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (playerId, zones) in state.PlayerZones)
        {
            if (!HasExactlyTwoSacredJudgmentTargets(
                    targetSet,
                    SacredJudgmentFieldUnitIds(state.CardObjects, playerId, zones),
                    categorizedTargetIds)
                || !HasExactlyTwoSacredJudgmentTargets(
                    targetSet,
                    SacredJudgmentEquipmentIds(state.CardObjects, playerId, zones),
                    categorizedTargetIds)
                || !HasExactlyTwoSacredJudgmentTargets(
                    targetSet,
                    SacredJudgmentRuneIds(state.CardObjects, playerId, zones),
                    categorizedTargetIds)
                || !HasExactlyTwoSacredJudgmentTargets(
                    targetSet,
                    zones.Hand.Where(cardId =>
                        !string.Equals(cardId, sourceObjectId, StringComparison.Ordinal)
                        && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, playerId, cardId)),
                    categorizedTargetIds))
            {
                return false;
            }
        }

        return targetSet.SetEquals(categorizedTargetIds);
    }

    private static bool HasExactlyTwoSacredJudgmentTargets(
        ISet<string> targetSet,
        IEnumerable<string> categoryObjectIds,
        ISet<string> categorizedTargetIds)
    {
        var selectedObjectIds = categoryObjectIds
            .Where(targetSet.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var objectId in selectedObjectIds)
        {
            categorizedTargetIds.Add(objectId);
        }

        return selectedObjectIds.Length == 2;
    }

    private static bool HasValidEachPlayerTopFiveUnitTargets(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        if (!behavior.PlaysEachPlayerTopFiveUnitToBase)
        {
            return true;
        }

        var playerIds = SeatPlayerIds(state);
        if (targetObjectIds.Count != playerIds.Length)
        {
            return false;
        }

        var selectedPlayerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var targetObjectId in targetObjectIds)
        {
            if (!CardObjectHasTag(state.CardObjects, targetObjectId, CardObjectTags.UnitCard)
                || !TryGetTopMainDeckOwner(state, state.PlayerZones, targetObjectId, behavior.MainDeckLookCount, out var playerId)
                || !selectedPlayerIds.Add(playerId))
            {
                return false;
            }
        }

        return selectedPlayerIds.SetEquals(playerIds);
    }

    private static bool PlayCardTargetsExposeKnownCardNumbers(
        MatchState state,
        IReadOnlyList<string> targetObjectIds)
    {
        return targetObjectIds.All(targetObjectId =>
            !state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            || !PlayCardTargetRequiresKnownCardNumber(targetState)
            || !string.IsNullOrWhiteSpace(targetState.CardNo));
    }

    private static bool PlayCardTargetRequiresKnownCardNumber(CardObjectState targetState)
    {
        return !string.IsNullOrWhiteSpace(targetState.OwnerId)
            || !string.IsNullOrWhiteSpace(targetState.ControllerId);
    }

    private static bool HasRequiredAnyTargetTag(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        return string.IsNullOrWhiteSpace(behavior.AnyTargetRequiredTag)
            || targetObjectIds.Any(targetObjectId =>
                CardObjectHasTag(state.CardObjects, targetObjectId, behavior.AnyTargetRequiredTag));
    }

    private static bool IsFriendlyGraveyardCard(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Graveyard.Contains(objectId, StringComparer.Ordinal)
            && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, playerId, objectId);
    }

    private static bool IsEnemyFieldObject(MatchState state, string playerId, string objectId)
    {
        var location = FindFieldObjectLocation(state.PlayerZones, objectId);
        return location is not null
            && !string.Equals(location.Value.PlayerId, playerId, StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, location.Value.PlayerId);
    }

    private static bool IsEnemyBattlefieldObject(MatchState state, string playerId, string objectId)
    {
        var location = FindFieldObjectLocation(state.PlayerZones, objectId);
        return location is not null
            && string.Equals(location.Value.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && !string.Equals(location.Value.PlayerId, playerId, StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, location.Value.PlayerId);
    }

    private static bool IsOpponentGraveyardCard(MatchState state, string playerId, string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && state.PlayerZones.Any(entry =>
                !string.Equals(entry.Key, playerId, StringComparison.Ordinal)
                && entry.Value.Graveyard.Contains(objectId, StringComparer.Ordinal)
                && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, entry.Key, objectId));
    }

    private static bool IsBaseObject(MatchState state, string objectId)
    {
        var location = FindFieldObjectLocation(state.PlayerZones, objectId);
        return location is not null
            && string.Equals(location.Value.Zone, MoveUnitBaseZone, StringComparison.Ordinal)
            && state.CardObjects.ContainsKey(objectId)
            && IsCardObjectControlledByPlayerOrLegacyOwned(state.CardObjects, location.Value.PlayerId, objectId);
    }

    private static bool IsFieldUnitObject(MatchState state, string objectId)
    {
        return IsFieldObject(state.PlayerZones, objectId)
            && IsFieldObjectControlledByZonePlayer(state.PlayerZones, state.CardObjects, objectId)
            && !CardObjectHasTag(state.CardObjects, objectId, CardObjectTags.EquipmentCard);
    }

    private static bool IsSacredJudgmentKeepCandidate(MatchState state, string objectId)
    {
        return state.PlayerZones.Any(entry =>
            entry.Value.Hand.Contains(objectId, StringComparer.Ordinal)
                && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, entry.Key, objectId)
            || (entry.Value.Base.Contains(objectId, StringComparer.Ordinal)
                    || entry.Value.Battlefields.Contains(objectId, StringComparer.Ordinal))
                && IsCardObjectControlledByPlayerOrLegacyOwned(state.CardObjects, entry.Key, objectId));
    }

    private static bool IsEquipmentObject(MatchState state, string objectId)
    {
        return IsFieldObject(state.PlayerZones, objectId)
            && IsFieldObjectControlledByZonePlayer(state.PlayerZones, state.CardObjects, objectId)
            && CardObjectHasTag(state.CardObjects, objectId, CardObjectTags.EquipmentCard);
    }

    private static bool IsStackSpellItem(MatchState state, string stackItemId)
    {
        return state.StackItems.Any(stackItem =>
            string.Equals(stackItem.StackItemId, stackItemId, StringComparison.Ordinal)
            && CardBehaviorRegistry.TryGetByEffectKind(stackItem.EffectKind, out var behavior)
            && !behavior.PlaysSourceToBaseAsUnit
            && !behavior.PlaysSourceToBaseAsEquipment);
    }

    private static bool IsFriendlyEquipmentObject(MatchState state, string playerId, string objectId)
    {
        return IsPlayerControlledFieldObject(state, playerId, objectId)
            && CardObjectHasTag(state.CardObjects, objectId, CardObjectTags.EquipmentCard);
    }

    private static bool IsEnemyEquipmentObject(MatchState state, string playerId, string objectId)
    {
        return IsEnemyFieldObject(state, playerId, objectId)
            && CardObjectHasTag(state.CardObjects, objectId, CardObjectTags.EquipmentCard);
    }

    private static bool IsAttackingBattlefieldObject(MatchState state, string objectId)
    {
        return IsBattlefieldObject(state, objectId)
            && state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.IsAttacking;
    }

    private static bool IsTargetPowerAllowed(MatchState state, string objectId, CardBehaviorDefinition behavior)
    {
        return behavior.MaxTargetPower <= 0
            || (state.CardObjects.TryGetValue(objectId, out var targetState)
                && targetState.Power > 0
                && targetState.Power <= behavior.MaxTargetPower);
    }

    private static bool HasValidTotalTargetPower(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        if (behavior.MaxTotalTargetPower <= 0)
        {
            return true;
        }

        var totalPower = 0;
        foreach (var targetObjectId in targetObjectIds)
        {
            if (!state.CardObjects.TryGetValue(targetObjectId, out var targetState)
                || targetState.Power <= 0)
            {
                return false;
            }

            totalPower += targetState.Power;
            if (totalPower > behavior.MaxTotalTargetPower)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreTargetsAfterFirstPowerLessThanFirstTarget(
        MatchState state,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        if (!behavior.RequiresTargetsAfterFirstPowerLessThanFirstTarget)
        {
            return true;
        }

        if (targetObjectIds.Count < 2
            || !state.CardObjects.TryGetValue(targetObjectIds[0], out var firstTargetState))
        {
            return false;
        }

        for (var targetIndex = 1; targetIndex < targetObjectIds.Count; targetIndex++)
        {
            if (!state.CardObjects.TryGetValue(targetObjectIds[targetIndex], out var targetState)
                || targetState.Power >= firstTargetState.Power)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasValidTargetCount(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds)
    {
        var targetCountConditionApplies = TargetCountConditionApplies(state, playerId, behavior);
        var minTargetCount = MinTargetCount(behavior, targetCountConditionApplies);
        var maxTargetCount = MaxTargetCount(state, playerId, behavior, targetCountConditionApplies);
        return targetObjectIds.Count >= minTargetCount
            && targetObjectIds.Count <= maxTargetCount
            && (behavior.AllowsRepeatedTargets
                || targetObjectIds.Distinct(StringComparer.Ordinal).Count() == targetObjectIds.Count);
    }

    private static bool HasValidResolvedTargetCount(
        CardBehaviorDefinition behavior,
        StackItemState stackItem)
    {
        var targetCountConditionApplies = TargetCountConditionApplies(behavior, stackItem);
        var minTargetCount = MinTargetCount(behavior, targetCountConditionApplies);
        var maxTargetCount = !targetCountConditionApplies
            ? 0
            : behavior.UsesFriendlyBattlefieldUnitCountAsMaxTargetCount
                ? stackItem.TargetObjectIds.Count
                : behavior.RequiredTargetCount;
        return stackItem.TargetObjectIds.Count >= minTargetCount
            && stackItem.TargetObjectIds.Count <= maxTargetCount
            && (behavior.AllowsRepeatedTargets
                || stackItem.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() == stackItem.TargetObjectIds.Count);
    }

    private static bool TryBuildOptionalCostPlan(
        MatchState state,
        string playerId,
        IReadOnlyList<string>? optionalCosts,
        CardBehaviorDefinition behavior,
        out IReadOnlyList<string> normalizedOptionalCosts,
        out int extraManaCost,
        out int extraPowerCost,
        out IReadOnlyDictionary<string, int> extraPowerCostByTrait,
        out int experienceCost,
        out int optionalCostManaReduction,
        out int effectRepeatCount,
        out IReadOnlyList<string> exhaustedOptionalCostTargetObjectIds,
        out IReadOnlyList<string> destroyedAdditionalCostTargetObjectIds,
        out IReadOnlyList<string> returnedAdditionalCostTargetObjectIds,
        out IReadOnlyList<string> discardedOptionalCostTargetObjectIds)
    {
        normalizedOptionalCosts = NormalizeOptionalCosts(optionalCosts);
        extraManaCost = 0;
        extraPowerCost = 0;
        extraPowerCostByTrait = new Dictionary<string, int>(StringComparer.Ordinal);
        experienceCost = 0;
        optionalCostManaReduction = 0;
        effectRepeatCount = 1;
        exhaustedOptionalCostTargetObjectIds = [];
        destroyedAdditionalCostTargetObjectIds = [];
        returnedAdditionalCostTargetObjectIds = [];
        discardedOptionalCostTargetObjectIds = [];

        if (normalizedOptionalCosts.Count == 0)
        {
            return true;
        }

        if (TryBuildBattlefieldHeldNextSpellEchoOptionalCost(
                state,
                playerId,
                normalizedOptionalCosts,
                behavior,
                out var battlefieldEchoExtraManaCost,
                out var battlefieldEchoEffectRepeatCount))
        {
            extraManaCost = battlefieldEchoExtraManaCost;
            effectRepeatCount = battlefieldEchoEffectRepeatCount;
            return true;
        }

        if (CardInteractionKeywordRules.TryBuildEchoOptionalCost(
            normalizedOptionalCosts,
            behavior,
            out var echoExtraManaCost,
            out var echoEffectRepeatCount))
        {
            extraManaCost = echoExtraManaCost;
            effectRepeatCount = echoEffectRepeatCount;
            return true;
        }

        if (CardPermissionKeywordRules.TryBuildHasteReadyOptionalCost(
            normalizedOptionalCosts,
            behavior,
            out var hasteExtraManaCost,
            out var hasteExtraPowerCost,
            out var hasteExtraPowerTrait))
        {
            extraManaCost = hasteExtraManaCost;
            if (string.IsNullOrWhiteSpace(hasteExtraPowerTrait))
            {
                extraPowerCost = hasteExtraPowerCost;
            }
            else
            {
                extraPowerCostByTrait = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [hasteExtraPowerTrait] = hasteExtraPowerCost
                };
            }

            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.EffectRepeatCountIfExhaustFriendlyUnitOptionalCost > 0
            && TryParseExhaustFriendlyUnitOptionalCost(normalizedOptionalCosts[0], out var exhaustedTargetObjectId))
        {
            effectRepeatCount = behavior.EffectRepeatCountIfExhaustFriendlyUnitOptionalCost;
            exhaustedOptionalCostTargetObjectIds = [exhaustedTargetObjectId];
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.RequiresDestroyFriendlyUnitAdditionalCost
            && TryParseDestroyFriendlyUnitAdditionalCost(
                normalizedOptionalCosts[0],
                out var destroyedFriendlyUnitTargetObjectId))
        {
            destroyedAdditionalCostTargetObjectIds = [destroyedFriendlyUnitTargetObjectId];
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.RequiresDestroyFriendlyPowerfulUnitAdditionalCost
            && TryParseDestroyFriendlyPowerfulUnitAdditionalCost(
                normalizedOptionalCosts[0],
                out var destroyedTargetObjectId))
        {
            destroyedAdditionalCostTargetObjectIds = [destroyedTargetObjectId];
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.RequiresDestroyFriendlyTraitUnitAdditionalCost
            && TryParseDestroyFriendlyTraitUnitAdditionalCost(
                normalizedOptionalCosts[0],
                out var destroyedTraitTargetObjectId))
        {
            destroyedAdditionalCostTargetObjectIds = [destroyedTraitTargetObjectId];
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.RequiresReturnFriendlyEquipmentAdditionalCost
            && TryParseReturnFriendlyEquipmentAdditionalCost(
                normalizedOptionalCosts[0],
                out var returnedEquipmentTargetObjectId))
        {
            returnedAdditionalCostTargetObjectIds = [returnedEquipmentTargetObjectId];
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.DamageAmountFromOptionalPowerCost
            && TryParseSpendPowerOptionalCost(normalizedOptionalCosts[0], out var powerCost, out var powerTrait))
        {
            if (string.IsNullOrWhiteSpace(powerTrait))
            {
                extraPowerCost = powerCost;
            }
            else
            {
                extraPowerCostByTrait = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [powerTrait] = powerCost
                };
            }

            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.OptionalExperienceCost > 0
            && behavior.ManaReductionIfExperiencePaid > 0
            && TryParseSpendExperienceOptionalCost(normalizedOptionalCosts[0], out var spendExperienceCost)
            && spendExperienceCost == behavior.OptionalExperienceCost)
        {
            experienceCost = spendExperienceCost;
            optionalCostManaReduction = behavior.ManaReductionIfExperiencePaid;
            return true;
        }

        if (normalizedOptionalCosts.Count == 1
            && behavior.EffectRepeatCountIfDiscardHandCardOptionalCost > 0
            && TryParseDiscardHandCardOptionalCost(
                normalizedOptionalCosts[0],
                out var discardedTargetObjectId))
        {
            effectRepeatCount = behavior.EffectRepeatCountIfDiscardHandCardOptionalCost;
            discardedOptionalCostTargetObjectIds = [discardedTargetObjectId];
            return true;
        }

        return false;
    }

    private static bool TryBuildBattlefieldHeldNextSpellEchoOptionalCost(
        MatchState state,
        string playerId,
        IReadOnlyList<string> normalizedOptionalCosts,
        CardBehaviorDefinition behavior,
        out int extraManaCost,
        out int effectRepeatCount)
    {
        extraManaCost = 0;
        effectRepeatCount = 1;
        if (normalizedOptionalCosts.Count != 1
            || !string.Equals(normalizedOptionalCosts[0], EchoOptionalCostNames.Echo, StringComparison.Ordinal)
            || !BattlefieldHeldNextSpellEchoActive(state, playerId)
            || !IsSpellPlayBehavior(behavior))
        {
            return false;
        }

        extraManaCost = behavior.ManaCost;
        effectRepeatCount = 2;
        return true;
    }

    private static bool TryParseExhaustFriendlyUnitOptionalCost(string optionalCost, out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!optionalCost.StartsWith(ExhaustFriendlyUnitOptionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        targetObjectId = optionalCost[ExhaustFriendlyUnitOptionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool TryParseDestroyFriendlyPowerfulUnitAdditionalCost(
        string optionalCost,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!optionalCost.StartsWith(DestroyFriendlyPowerfulUnitAdditionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        targetObjectId = optionalCost[DestroyFriendlyPowerfulUnitAdditionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool TryParseDestroyFriendlyUnitAdditionalCost(
        string optionalCost,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!optionalCost.StartsWith(DestroyFriendlyUnitAdditionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        targetObjectId = optionalCost[DestroyFriendlyUnitAdditionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool TryParseDestroyFriendlyTraitUnitAdditionalCost(
        string optionalCost,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!optionalCost.StartsWith(DestroyFriendlyTraitUnitAdditionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        targetObjectId = optionalCost[DestroyFriendlyTraitUnitAdditionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool TryParseReturnFriendlyEquipmentAdditionalCost(
        string optionalCost,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!optionalCost.StartsWith(ReturnFriendlyEquipmentAdditionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        targetObjectId = optionalCost[ReturnFriendlyEquipmentAdditionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool TryParseDiscardHandCardOptionalCost(
        string optionalCost,
        out string targetObjectId)
    {
        targetObjectId = string.Empty;
        if (!optionalCost.StartsWith(DiscardHandCardOptionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        targetObjectId = optionalCost[DiscardHandCardOptionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(targetObjectId);
    }

    private static bool TryParseSpendPowerOptionalCost(
        string optionalCost,
        out int powerCost,
        out string powerTrait)
    {
        powerCost = 0;
        powerTrait = string.Empty;
        if (!optionalCost.StartsWith(SpendPowerOptionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rawCost = optionalCost[SpendPowerOptionalCostPrefix.Length..].Trim();
        var parts = rawCost
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return int.TryParse(parts[0], out powerCost) && powerCost >= 0;
        }

        if (parts.Length == 2 && int.TryParse(parts[0], out powerCost) && powerCost >= 0)
        {
            powerTrait = RuneTrait.Normalize(parts[1]);
            return !string.IsNullOrWhiteSpace(powerTrait);
        }

        if (parts.Length == 2 && int.TryParse(parts[1], out powerCost) && powerCost >= 0)
        {
            powerTrait = RuneTrait.Normalize(parts[0]);
            return !string.IsNullOrWhiteSpace(powerTrait);
        }

        return false;
    }

    private static bool TryParseSpendExperienceOptionalCost(string optionalCost, out int experienceCost)
    {
        experienceCost = 0;
        if (!optionalCost.StartsWith(SpendExperienceOptionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(
                optionalCost[SpendExperienceOptionalCostPrefix.Length..].Trim(),
                out experienceCost)
            && experienceCost > 0;
    }

    private static bool CanExhaustFriendlyUnitAsOptionalCost(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return IsPlayerControlledFieldObject(state, playerId, targetObjectId)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && !targetState.IsExhausted;
    }

    private static bool CanDestroyFriendlyPowerfulUnitAsAdditionalCost(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return IsPlayerControlledFieldObject(state, playerId, targetObjectId)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && targetState.Power >= 5;
    }

    private static bool CanDestroyFriendlyUnitAsAdditionalCost(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return IsPlayerControlledFieldObject(state, playerId, targetObjectId)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal);
    }

    private static bool CanDestroyFriendlyTraitUnitAsAdditionalCost(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        if (!IsPlayerControlledFieldObject(state, playerId, targetObjectId)
            || !state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            || !targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
        {
            return false;
        }

        return targetState.Tags.Contains("鸟类", StringComparer.Ordinal)
            || targetState.Tags.Contains("猫科", StringComparer.Ordinal)
            || targetState.Tags.Contains("犬形", StringComparer.Ordinal)
            || targetState.Tags.Contains("魄罗", StringComparer.Ordinal);
    }

    private static bool CanReturnFriendlyEquipmentAsAdditionalCost(
        MatchState state,
        string playerId,
        string targetObjectId)
    {
        return IsFriendlyEquipmentObject(state, playerId, targetObjectId);
    }

    private static bool CanDiscardHandCardAsOptionalCost(
        MatchState state,
        string playerId,
        string sourceObjectId,
        string targetObjectId)
    {
        return !string.Equals(sourceObjectId, targetObjectId, StringComparison.Ordinal)
            && IsFriendlyHandCard(state, playerId, targetObjectId);
    }

    private static int ResolveCostReductionMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (behavior.CostReductionMana <= 0
            || string.Equals(behavior.CostReductionConditionKind, CardCostReductionConditionKinds.None, StringComparison.Ordinal))
        {
            return 0;
        }

        return behavior.CostReductionConditionKind switch
        {
            CardCostReductionConditionKinds.EnemyUnitDestroyedThisTurn
                => EnemyUnitDestroyedThisTurn(state, playerId) ? behavior.CostReductionMana : 0,
            CardCostReductionConditionKinds.ControllerHighestUnitPower
                => Math.Min(behavior.CostReductionMana, HighestControlledUnitPower(state, playerId)),
            CardCostReductionConditionKinds.OpponentWithinThreeOfWinningScore
                => OpponentWithinWinningScoreDistance(state, playerId, 3) ? behavior.CostReductionMana : 0,
            CardCostReductionConditionKinds.ControllerControlsTaggedUnit
                => ControllerControlsTaggedUnit(state, playerId, behavior.CostReductionUnitTag)
                    ? behavior.CostReductionMana
                    : 0,
            CardCostReductionConditionKinds.ControllerPlayedAnotherCardThisTurn
                => ControllerPlayedAnotherCardThisTurn(state, playerId) ? behavior.CostReductionMana : 0,
            _ => 0
        };
    }

    private static int ResolveBattlefieldEchoCostReductionMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> optionalCosts,
        int echoExtraManaCost)
    {
        if (echoExtraManaCost <= 0
            || behavior.EchoManaCost <= 0
            || !optionalCosts.Contains(EchoOptionalCostNames.Echo, StringComparer.Ordinal)
            || !state.PlayerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldEchoCostReductionCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)))
        {
            return 0;
        }

        return Math.Min(1, echoExtraManaCost);
    }

    private static int ResolveBattlefieldEquipmentCostReductionMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (behavior.ManaCost <= 0
            || !behavior.PlaysSourceToBaseAsEquipment
            || ControllerPlayedEquipmentThisTurn(state, playerId)
            || !state.PlayerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Any(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldEquipmentCostReductionCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)))
        {
            return 0;
        }

        return Math.Min(1, behavior.ManaCost);
    }

    private static int ResolveBattlefieldHeldUnitCostIncreaseMana(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        if (!behavior.PlaysSourceToBaseAsUnit
            || behavior.ManaCost <= 0
            || P6TokenFactoryCatalog.TryGetByCardNo(behavior.CardNo, out _)
            || !BattlefieldHeldUnitCostIncreaseActive(state, playerId))
        {
            return 0;
        }

        return 1;
    }

    private static bool TryGetBattlefieldFriendlySpellDrawSourceObjectId(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds,
        out string sourceObjectId)
    {
        sourceObjectId = string.Empty;
        if (!IsSpellPlayBehavior(behavior)
            || targetObjectIds.Count == 0
            || !state.PlayerZones.TryGetValue(playerId, out var zones)
            || !targetObjectIds.Any(targetObjectId =>
                IsPlayerControlledBattlefieldObject(state, playerId, targetObjectId)
                && CardObjectHasTag(state.CardObjects, targetObjectId, CardObjectTags.UnitCard)))
        {
            return false;
        }

        sourceObjectId = zones.Battlefields
            .Where(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldFriendlySpellDrawCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
                && !BattlefieldFriendlySpellDrawUsedThisTurn(state, playerId, objectId))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .FirstOrDefault() ?? string.Empty;

        return !string.IsNullOrWhiteSpace(sourceObjectId);
    }

    private static bool TryResolveBattlefieldSpellPowerBonusTrigger(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        if (!IsSpellPlayBehavior(behavior)
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        var sourceObjectId = zones.Battlefields
            .Where(objectId => cardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldSpellPowerBonusCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sourceObjectId))
        {
            return false;
        }

        var targetObjectId = zones.Battlefields
            .Where(objectId => IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId)
                && CardObjectHasTag(cardObjects, objectId, CardObjectTags.UnitCard))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(targetObjectId)
            || !cardObjects.TryGetValue(targetObjectId, out var targetState))
        {
            return false;
        }

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 因废弃大厅强化单位",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldObjectId"] = sourceObjectId,
                ["battlefieldCardNo"] = BattlefieldSpellPowerBonusCardNo,
                ["trigger"] = "BATTLEFIELD_SPELL_POWER_PLUS_1",
                ["playedCardNo"] = stackItem.CardNo,
                ["targetObjectId"] = targetObjectId,
                ["powerDelta"] = 1
            }));
        cardObjects[targetObjectId] = ApplyPowerModifier(
            targetState,
            behavior,
            stackItem,
            targetObjectId,
            1,
            out var powerEvent);
        events.Add(powerEvent);
        return true;
    }

    private static bool TryResolveBattlefieldPlayUnitPayOneBoonTrigger(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        IReadOnlyDictionary<string, RunePool> runePools,
        string playerId,
        StackItemState stackItem,
        out IReadOnlyDictionary<string, RunePool> nextRunePools,
        List<GameEvent> events)
    {
        nextRunePools = runePools;
        if (!IsStackItemBattlefieldDestination(stackItem)
            || !playerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Contains(stackItem.SourceObjectId, StringComparer.Ordinal)
            || !cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceUnitState)
            || !sourceUnitState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || !SourceObjectControlledByPlayerOrLegacyOwned(sourceUnitState, playerId)
            || sourceUnitState.Tags.Contains(CardObjectTags.Boon, StringComparer.Ordinal)
            || !runePools.TryGetValue(playerId, out var currentPool)
            || currentPool.Mana < 1)
        {
            return false;
        }

        var battlefieldObjectId = zones.Battlefields
            .Where(objectId => cardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldPlayUnitPayOneBoonCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(battlefieldObjectId)
            || !cardObjects.TryGetValue(battlefieldObjectId, out var battlefieldState))
        {
            return false;
        }

        var mutableRunePools = runePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        mutableRunePools[playerId] = currentPool with
        {
            Mana = currentPool.Mana - 1
        };
        nextRunePools = mutableRunePools;

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 在偶像谷打出单位并支付 1 给予增益",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON",
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["targetObjectId"] = stackItem.SourceObjectId,
                ["destination"] = stackItem.Destination,
                ["manaCost"] = 1
            }));
        events.Add(new GameEvent(
            "COST_PAID",
            $"{playerId} 支付偶像谷单位增益触发费用",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["mana"] = 1,
                ["power"] = 0,
                ["reason"] = "BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON"
            }));
        GrantLegendBoon(
            cardObjects,
            stackItem.SourceObjectId,
            playerId,
            battlefieldObjectId,
            "BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON",
            events);
        return true;
    }

    private static bool TryResolveBattlefieldFirstUnitPlayedMoveOtherToBaseTrigger(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        StackItemState stackItem,
        List<string> untilEndOfTurnEffects,
        List<GameEvent> events)
    {
        if (!IsStackItemBattlefieldDestination(stackItem)
            || P6TokenFactoryCatalog.TryGetByCardNo(stackItem.CardNo, out _)
            || !playerZones.TryGetValue(playerId, out var zones)
            || !zones.Battlefields.Contains(stackItem.SourceObjectId, StringComparer.Ordinal)
            || !cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceUnitState)
            || !sourceUnitState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || !SourceObjectControlledByPlayerOrLegacyOwned(sourceUnitState, playerId))
        {
            return false;
        }

        var battlefieldObjectId = zones.Battlefields
            .Where(objectId => cardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldFirstUnitPlayedMoveOtherToBaseCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(battlefieldObjectId)
            || !cardObjects.TryGetValue(battlefieldObjectId, out var battlefieldState))
        {
            return false;
        }

        var effectId = BuildBattlefieldFirstUnitPlayedMoveOtherToBaseUsedEffectId(playerId, battlefieldObjectId);
        if (untilEndOfTurnEffects.Contains(effectId, StringComparer.Ordinal))
        {
            return false;
        }

        var targetObjectId = zones.Battlefields
            .Where(objectId => !string.Equals(objectId, stackItem.SourceObjectId, StringComparison.Ordinal)
                && !string.Equals(objectId, battlefieldObjectId, StringComparison.Ordinal)
                && cardObjects.TryGetValue(objectId, out var cardObject)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId)
                && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(targetObjectId)
            || !TryMoveTargetToOwnerBase(playerZones, cardObjects, targetObjectId, out var targetPlayerId)
            || !cardObjects.TryGetValue(targetObjectId, out var targetState))
        {
            return false;
        }

        cardObjects[targetObjectId] = targetState with
        {
            IsAttacking = false,
            IsDefending = false
        };
        untilEndOfTurnEffects.Add(effectId);
        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 在流星疗泉打出本回合首个单位并移动另一名单位",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldObjectId"] = battlefieldObjectId,
                ["battlefieldCardNo"] = battlefieldState.CardNo,
                ["trigger"] = "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE",
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["playedUnitObjectId"] = stackItem.SourceObjectId,
                ["targetObjectId"] = targetObjectId,
                ["destination"] = stackItem.Destination,
                ["effectId"] = effectId
            }));
        events.Add(new GameEvent(
            "UNIT_MOVED_TO_BASE",
            $"{targetObjectId} 因流星疗泉移动到基地",
            new Dictionary<string, object?>
            {
                ["playerId"] = targetPlayerId,
                ["sourceObjectId"] = battlefieldObjectId,
                ["targetObjectId"] = targetObjectId,
                ["originZone"] = MoveUnitBattlefieldZone,
                ["destinationZone"] = MoveUnitBaseZone,
                ["reason"] = "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE"
            }));
        return true;
    }

    private static RecycleResult TryResolveBattlefieldHighCostSpellInsightTrigger(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        int paidMana,
        long rngCursor)
    {
        var events = new List<GameEvent>();
        if (!IsSpellPlayBehavior(behavior)
            || paidMana < 4
            || !playerZones.TryGetValue(playerId, out var zones)
            || zones.MainDeck.Count == 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var sourceObjectId = zones.Battlefields
            .Where(objectId => cardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldHighCostSpellInsightCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sourceObjectId))
        {
            return new RecycleResult(events, rngCursor);
        }

        var recycledCardIds = TakeControlledMainDeckPrefix(cardObjects, playerId, zones.MainDeck, 1);
        if (recycledCardIds.Length == 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var randomizedRecycledCardIds = RandomizeForMainDeckBottom(
            recycledCardIds,
            state.Seed,
            rngCursor,
            sourceObjectId);
        playerZones[playerId] = zones with
        {
            MainDeck = zones.MainDeck
                .Skip(recycledCardIds.Length)
                .Concat(randomizedRecycledCardIds)
                .ToArray()
        };

        events.Add(new GameEvent(
            "BATTLEFIELD_TRIGGER_RESOLVED",
            $"{playerId} 因失落书库洞察并回收顶部牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["battlefieldObjectId"] = sourceObjectId,
                ["battlefieldCardNo"] = BattlefieldHighCostSpellInsightCardNo,
                ["trigger"] = "BATTLEFIELD_HIGH_COST_SPELL_INSIGHT_RECYCLE",
                ["playedCardNo"] = stackItem.CardNo,
                ["paidMana"] = paidMana,
                ["recycledCardIds"] = randomizedRecycledCardIds.ToArray()
            }));
        events.Add(new GameEvent(
            "CARDS_RECYCLED",
            $"{playerId} 洞察并回收 1 张牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["cardIds"] = randomizedRecycledCardIds.ToArray(),
                ["count"] = randomizedRecycledCardIds.Count,
                ["reason"] = "BATTLEFIELD_HIGH_COST_SPELL_INSIGHT_RECYCLE"
            }));
        return new RecycleResult(events, rngCursor);
    }

    private static bool IsSpellPlayBehavior(CardBehaviorDefinition behavior)
    {
        return !behavior.PlaysSourceToBaseAsUnit
            && !behavior.PlaysSourceToBaseAsEquipment;
    }

    private static bool ControllerPlayedAnotherCardThisTurn(MatchState state, string playerId)
    {
        return state.PlayerCardsPlayedThisTurn.TryGetValue(playerId, out var count) && count > 0;
    }

    private static bool ShouldGrantBoonToSourceUnit(
        CardBehaviorDefinition behavior,
        StackItemState stackItem)
    {
        if (!behavior.GrantsBoonToSourceUnit)
        {
            return false;
        }

        return behavior.SourceBoonConditionKind switch
        {
            CardSourceBoonConditionKinds.None => true,
            CardSourceBoonConditionKinds.PlayedAfterAnotherCardThisTurn
                => stackItem.PlayedAfterAnotherCardThisTurn,
            _ => false
        };
    }

    private static bool TargetCountConditionApplies(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior)
    {
        return behavior.TargetCountConditionKind switch
        {
            CardTargetCountConditionKinds.None => true,
            CardTargetCountConditionKinds.PlayedAfterAnotherCardThisTurn
                => ControllerPlayedAnotherCardThisTurn(state, playerId),
            _ => false
        };
    }

    private static bool TargetCountConditionApplies(
        CardBehaviorDefinition behavior,
        StackItemState stackItem)
    {
        return behavior.TargetCountConditionKind switch
        {
            CardTargetCountConditionKinds.None => true,
            CardTargetCountConditionKinds.PlayedAfterAnotherCardThisTurn
                => stackItem.PlayedAfterAnotherCardThisTurn,
            _ => false
        };
    }

    private static bool ShouldCreateBaseUnitTokens(
        CardBehaviorDefinition behavior,
        StackItemState stackItem)
    {
        return behavior.CreatedBaseUnitTokenConditionKind switch
        {
            CardTokenCreationConditionKinds.None => true,
            CardTokenCreationConditionKinds.PlayedAfterAnotherCardThisTurn
                => stackItem.PlayedAfterAnotherCardThisTurn,
            _ => false
        };
    }

    private static bool EnemyUnitDestroyedThisTurn(MatchState state, string playerId)
    {
        return state.DestroyedUnitOwnerIdsThisTurn.Any(ownerPlayerId =>
            !string.Equals(ownerPlayerId, playerId, StringComparison.Ordinal));
    }

    private static int HighestControlledUnitPower(MatchState state, string playerId)
    {
        if (!state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return 0;
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Select(objectId => state.CardObjects.TryGetValue(objectId, out var cardObject) ? cardObject.Power : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static bool ControllerControlsTaggedUnit(MatchState state, string playerId, string requiredTag)
    {
        if (string.IsNullOrWhiteSpace(requiredTag)
            || !state.PlayerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Any(objectId => CardObjectHasTag(state.CardObjects, objectId, requiredTag));
    }

    private static bool CardObjectHasTag(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string objectId,
        string requiredTag)
    {
        if (string.IsNullOrWhiteSpace(requiredTag))
        {
            return true;
        }

        return cardObjects.TryGetValue(objectId, out var cardObject)
            && ParseDelimitedValues(requiredTag)
                .All(tag => cardObject.Tags.Contains(tag, StringComparer.Ordinal));
    }

    private static bool OpponentWithinWinningScoreDistance(MatchState state, string playerId, int distance)
    {
        var opponentId = OpponentOf(state, playerId);
        return opponentId is not null
            && state.PlayerScores.TryGetValue(opponentId, out var opponentScore)
            && opponentScore >= EffectiveWinningScore(state) - distance;
    }

    private static int MinTargetCount(
        CardBehaviorDefinition behavior,
        bool targetCountConditionApplies = true)
    {
        if (!targetCountConditionApplies)
        {
            return 0;
        }

        return behavior.MinTargetCount < 0 ? behavior.RequiredTargetCount : behavior.MinTargetCount;
    }

    private static int MaxTargetCount(
        MatchState state,
        string playerId,
        CardBehaviorDefinition behavior,
        bool targetCountConditionApplies = true)
    {
        if (!targetCountConditionApplies)
        {
            return 0;
        }

        if (!behavior.UsesFriendlyBattlefieldUnitCountAsMaxTargetCount)
        {
            return behavior.RequiredTargetCount;
        }

        return state.PlayerZones.TryGetValue(playerId, out var zones)
            ? zones.Battlefields.Count(objectId => state.CardObjects.ContainsKey(objectId))
            : 0;
    }

    private static string DescribeTargetCount(MatchState state, string playerId, CardBehaviorDefinition behavior)
    {
        var targetCountConditionApplies = TargetCountConditionApplies(state, playerId, behavior);
        var minTargetCount = MinTargetCount(behavior, targetCountConditionApplies);
        var maxTargetCount = MaxTargetCount(state, playerId, behavior, targetCountConditionApplies);
        return minTargetCount == maxTargetCount
            ? maxTargetCount.ToString()
            : $"{minTargetCount}-{maxTargetCount}";
    }

    private static string ExtractRengarUnitPlayedTriggerTarget(
        MatchState state,
        string playerId,
        string sourceObjectId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds,
        out IReadOnlyList<string> behaviorTargetObjectIds)
    {
        behaviorTargetObjectIds = targetObjectIds;
        if (!behavior.PlaysSourceToBaseAsUnit
            || !ControllerHasRengarLegend(state, playerId))
        {
            return string.Empty;
        }

        if (targetObjectIds.Count == 0
            || HasValidTargetCount(state, playerId, behavior, targetObjectIds))
        {
            return string.Empty;
        }

        var behaviorTargetCandidates = targetObjectIds.Take(targetObjectIds.Count - 1).ToArray();
        if (!HasValidTargetCount(state, playerId, behavior, behaviorTargetCandidates))
        {
            return string.Empty;
        }

        var triggerTargetObjectId = targetObjectIds[^1];
        behaviorTargetObjectIds = behaviorTargetCandidates;
        return triggerTargetObjectId;
    }

    private static bool IsValidRengarUnitPlayedTriggerTarget(
        MatchState state,
        string sourceObjectId,
        string targetObjectId)
    {
        if (string.Equals(targetObjectId, sourceObjectId, StringComparison.Ordinal))
        {
            return true;
        }

        return FindFieldObjectLocation(state.PlayerZones, targetObjectId) is not null
            && IsFieldObjectControlledByZonePlayer(state.PlayerZones, state.CardObjects, targetObjectId)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !targetState.IsFaceDown;
    }

    private static string ExtractLeonaStunBoonTriggerTarget(
        MatchState state,
        string playerId,
        string sourceObjectId,
        CardBehaviorDefinition behavior,
        IReadOnlyList<string> targetObjectIds,
        out IReadOnlyList<string> behaviorTargetObjectIds)
    {
        behaviorTargetObjectIds = targetObjectIds;
        if (!BehaviorCanTriggerLeonaStunBoon(behavior)
            || !ControllerHasLeonaLegend(state, playerId))
        {
            return string.Empty;
        }

        if (targetObjectIds.Count == 0
            || HasValidTargetCount(state, playerId, behavior, targetObjectIds))
        {
            return string.Empty;
        }

        var behaviorTargetCandidates = targetObjectIds.Take(targetObjectIds.Count - 1).ToArray();
        if (!HasValidTargetCount(state, playerId, behavior, behaviorTargetCandidates))
        {
            return string.Empty;
        }

        var triggerTargetObjectId = targetObjectIds[^1];
        behaviorTargetObjectIds = behaviorTargetCandidates;
        return triggerTargetObjectId;
    }

    private static bool BehaviorCanTriggerLeonaStunBoon(CardBehaviorDefinition behavior)
    {
        return ResolveStatusEffectIds(behavior).Contains("STUNNED", StringComparer.Ordinal);
    }

    private static bool IsValidLeonaStunBoonTriggerTarget(
        MatchState state,
        string playerId,
        string sourceObjectId,
        CardBehaviorDefinition behavior,
        string targetObjectId)
    {
        if (string.Equals(targetObjectId, sourceObjectId, StringComparison.Ordinal)
            && behavior.PlaysSourceToBaseAsUnit)
        {
            return true;
        }

        return IsPlayerControlledFieldObject(state, playerId, targetObjectId)
            && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
            && targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !targetState.IsFaceDown;
    }

    private static string DescribeTargetScope(string targetScope)
    {
        return string.Equals(targetScope, CardTargetScopes.AnyUnit, StringComparison.Ordinal)
            ? "unit"
            : string.Equals(targetScope, CardTargetScopes.BaseUnit, StringComparison.Ordinal)
                ? "base unit"
                : string.Equals(targetScope, CardTargetScopes.BattlefieldUnitOrEquipment, StringComparison.Ordinal)
                    ? "battlefield unit or equipment"
            : string.Equals(targetScope, CardTargetScopes.FriendlyUnit, StringComparison.Ordinal)
                ? "friendly unit"
                : string.Equals(targetScope, CardTargetScopes.FriendlyUnitThenFriendlyUnit, StringComparison.Ordinal)
                    ? "friendly unit then another friendly unit"
                    : string.Equals(targetScope, CardTargetScopes.FriendlyThenEnemyUnits, StringComparison.Ordinal)
                        ? "friendly unit then enemy unit"
                        : string.Equals(targetScope, CardTargetScopes.FriendlyEquipmentThenEnemyEquipment, StringComparison.Ordinal)
                            ? "friendly equipment then enemy equipment"
                            : string.Equals(targetScope, CardTargetScopes.FriendlyThenEnemyBattlefieldUnits, StringComparison.Ordinal)
                                ? "friendly unit then enemy battlefield unit"
                                : string.Equals(targetScope, CardTargetScopes.FriendlyBattlefieldThenEnemyBattlefieldUnits, StringComparison.Ordinal)
                                    ? "friendly battlefield unit then enemy battlefield unit"
                                    : string.Equals(targetScope, CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell, StringComparison.Ordinal)
                                        ? "friendly battlefield unit then spell on the stack"
                                        : string.Equals(targetScope, CardTargetScopes.FriendlyBattlefieldUnit, StringComparison.Ordinal)
                                            ? "friendly battlefield unit"
                                            : string.Equals(targetScope, CardTargetScopes.AnyUnitThenFriendlyMainDeckCard, StringComparison.Ordinal)
                                                ? "unit then friendly main deck card"
                                                : string.Equals(targetScope, CardTargetScopes.FriendlyHandCard, StringComparison.Ordinal)
                                                    ? "friendly hand card"
                                                    : string.Equals(targetScope, CardTargetScopes.FriendlyHandCardThenBattlefieldUnit, StringComparison.Ordinal)
                                                        ? "friendly hand card then battlefield unit"
                                                        : string.Equals(targetScope, CardTargetScopes.FriendlyMainDeckCard, StringComparison.Ordinal)
                                                            ? "friendly main deck card"
                                                            : string.Equals(targetScope, CardTargetScopes.FriendlyGraveyardCard, StringComparison.Ordinal)
                                                                ? "friendly graveyard card"
                                                                : string.Equals(targetScope, CardTargetScopes.FriendlyBaseUnit, StringComparison.Ordinal)
                                                                    ? "friendly base unit"
                                                                    : string.Equals(targetScope, CardTargetScopes.AttackingUnit, StringComparison.Ordinal)
                                                                        ? "attacking unit"
                                                                        : string.Equals(targetScope, CardTargetScopes.EnemyAttackingUnit, StringComparison.Ordinal)
                                                                            ? "enemy attacking unit"
                                                                            : string.Equals(targetScope, CardTargetScopes.EnemyBattlefieldUnit, StringComparison.Ordinal)
                                                                                ? "enemy battlefield unit"
                                                                                : string.Equals(targetScope, CardTargetScopes.EnemyUnit, StringComparison.Ordinal)
                                                                                    ? "enemy unit"
                                                                                    : string.Equals(targetScope, CardTargetScopes.EnemyUnitThenEnemyUnit, StringComparison.Ordinal)
                                                                                        ? "enemy unit then another enemy unit"
                                                                                        : string.Equals(targetScope, CardTargetScopes.OpponentHandCard, StringComparison.Ordinal)
                                                                                            ? "opponent hand card"
                                                                                            : string.Equals(targetScope, CardTargetScopes.OpponentGraveyardCard, StringComparison.Ordinal)
                                                                                                ? "opponent graveyard card"
                                                                                                : string.Equals(targetScope, CardTargetScopes.OpponentMainDeckTopCard, StringComparison.Ordinal)
                                                                                                    ? "opponent main deck top card"
                                                                                                    : string.Equals(targetScope, CardTargetScopes.AnyMainDeckTopFiveCard, StringComparison.Ordinal)
                                                                                                        ? "main deck top five card"
                                                                                                        : string.Equals(targetScope, CardTargetScopes.Equipment, StringComparison.Ordinal)
                                                                                                            ? "equipment"
                                                                                                            : string.Equals(targetScope, CardTargetScopes.StackSpell, StringComparison.Ordinal)
                                                                                                                ? "spell on the stack"
                                                                                                                : string.Equals(targetScope, CardTargetScopes.UnitThenItsControllersWeapon, StringComparison.Ordinal)
                                                                                                                    ? "unit and its controller's weapon"
                                                                                                                    : string.Equals(targetScope, CardTargetScopes.SacredJudgmentKeepCard, StringComparison.Ordinal)
                                                                                                                        ? "cards kept for Judgment Day"
                                                                                                                        : "battlefield unit";
    }

    private static StackResolutionResult ResolveStackItemEffect(MatchState state, StackItemState stackItem)
    {
        if (string.Equals(stackItem.EffectKind, P4ActivatedAbilityCatalog.ViDoublePowerAbilityEffectKind, StringComparison.Ordinal))
        {
            return ResolveViDoublePowerAbilityStackItem(state, stackItem);
        }

        if (string.Equals(stackItem.EffectKind, P4ActivatedAbilityCatalog.XerathDamageAbilityEffectKind, StringComparison.Ordinal))
        {
            return ResolveXerathDamageAbilityStackItem(state, stackItem);
        }

        if (!CardBehaviorRegistry.TryGetByEffectKind(stackItem.EffectKind, out var behavior)
            || !HasValidResolvedTargetCount(behavior, stackItem))
        {
            return new StackResolutionResult(
                state.PlayerZones,
                state.CardObjects,
                state.PlayerScores,
                state.PlayerExperience,
                state.RunePools,
                state.UntilEndOfTurnEffects,
                null,
                [],
                [],
                null,
                [],
                null,
                state.RngCursor);
        }

        var playerZones = NormalizeZonesForSeats(state);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var untilEndOfTurnEffects = state.UntilEndOfTurnEffects
            .Where(effectId => !string.IsNullOrWhiteSpace(effectId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(effectId => effectId, StringComparer.Ordinal)
            .ToList();
        var events = new List<GameEvent>();
        var destroyedObjectIds = new List<string>();
        var destroyedUnitOwnerIds = new List<string>();
        var counteredStackItemIds = new List<string>();
        var targetControllerDrawRecipientIds = new List<string>();
        var damageTriggeredDestroyTargetObjectIds = new HashSet<string>(StringComparer.Ordinal);
        var rngCursor = state.RngCursor;
        var playerScores = state.PlayerScores;
        var playerExperience = NormalizeExperienceForSeats(state);
        var runePools = state.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        string? winnerPlayerId = null;
        string? extraTurnPlayerId = null;
        int? drawCountOverride = null;
        var preventDamageFromThisStackItem = ShouldPreventSpellOrSkillDamage(state, behavior);
        StackItemState[]? updatedStackItems = null;

        if (behavior.PlaysSourceToBaseAsEquipment)
        {
            PlaySourceEquipmentToBase(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }

        if (behavior.PlaysSourceToBaseAsUnit)
        {
            if (IsStackItemBattlefieldDestination(stackItem))
            {
                PlaySourceUnitToBattlefield(
                    playerZones,
                    cardObjects,
                    behavior,
                    stackItem,
                    state.PlayerExperience,
                    events);
            }
            else
            {
                PlaySourceUnitToBase(
                    playerZones,
                    cardObjects,
                    behavior,
                    stackItem,
                    state.PlayerExperience,
                    events);
            }

            if (TryResolveBattlefieldPlayUnitPayOneBoonTrigger(
                    playerZones,
                    cardObjects,
                    runePools,
                    stackItem.ControllerId,
                    stackItem,
                    out var battlefieldBoonRunePools,
                    events))
            {
                runePools = battlefieldBoonRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            }

            TryResolveBattlefieldFirstUnitPlayedMoveOtherToBaseTrigger(
                playerZones,
                cardObjects,
                stackItem.ControllerId,
                stackItem,
                untilEndOfTurnEffects,
                events);

            events.AddRange(ResolvePowerfulUnitPlayedRuneLegendTriggers(
                playerZones,
                cardObjects,
                stackItem.ControllerId,
                stackItem.SourceObjectId));
            events.AddRange(ResolveRengarUnitPlayedPowerTrigger(
                state,
                playerZones,
                cardObjects,
                stackItem));
        }

        if (behavior.GainExperienceOnPlay > 0)
        {
            playerExperience = GainExperience(
                playerExperience,
                stackItem.ControllerId,
                behavior.GainExperienceOnPlay,
                stackItem,
                events);
        }

        if (behavior.GainExperienceOnPlayPerFriendlyFieldUnit > 0)
        {
            var experienceAmount = CountControlledFieldUnitObjects(
                playerZones,
                cardObjects,
                stackItem.ControllerId) * behavior.GainExperienceOnPlayPerFriendlyFieldUnit;
            if (experienceAmount > 0)
            {
                playerExperience = GainExperience(
                    playerExperience,
                    stackItem.ControllerId,
                    experienceAmount,
                    stackItem,
                    events);
            }
        }

        if (behavior.LevelDrawOnPlayCount > 0
            && ControllerMeetsLevelExperienceThreshold(behavior, stackItem.ControllerId, playerExperience))
        {
            var drawApplication = ApplyDrawToPlayer(
                state,
                playerZones,
                playerScores,
                stackItem.ControllerId,
                behavior.LevelDrawOnPlayCount,
                rngCursor,
                events);
            playerScores = drawApplication.PlayerScores;
            winnerPlayerId = drawApplication.WinnerPlayerId;
            rngCursor = drawApplication.RngCursor;
        }

        if (behavior.RemovesDamageFromAllFriendlyBattlefieldUnits)
        {
            RemoveDamageFromFriendlyBattlefieldUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }

        if (behavior.CountersTargetStackSpell)
        {
            foreach (var targetStackItemId in stackItem.TargetObjectIds)
            {
                if (!TryCounterStackItem(
                        state,
                        playerZones,
                        targetStackItemId,
                        stackItem,
                        behavior,
                        out var counteredEvent))
                {
                    continue;
                }

                counteredStackItemIds.Add(targetStackItemId);
                events.Add(counteredEvent);
            }
        }

        if (behavior.GainsControlOfTargetStackSpell
            && TryGainControlOfTargetStackSpell(
                state.StackItems,
                behavior,
                stackItem,
                out var controlledStackItems,
                out var stackControlEvent))
        {
            updatedStackItems = controlledStackItems;
            events.Add(stackControlEvent);
        }

        if (behavior.AppliesPowerModifierToFirstTargetFromSecondStackSpellManaCost
            && stackItem.TargetObjectIds.Count >= 2
            && cardObjects.TryGetValue(stackItem.TargetObjectIds[0], out var dynamicPowerTargetState)
            && TryGetStackItemManaCost(state, stackItem.TargetObjectIds[1], out var stackSpellManaCost)
            && stackSpellManaCost != 0)
        {
            var modifiedFirstTargetState = ApplyPowerModifier(
                dynamicPowerTargetState,
                behavior,
                stackItem,
                stackItem.TargetObjectIds[0],
                stackSpellManaCost,
                out var powerEvent);
            cardObjects[stackItem.TargetObjectIds[0]] = modifiedFirstTargetState;
            events.Add(powerEvent);
        }

        if (behavior.AppliesPowerModifierToSourceUnit
            && behavior.PowerModifierAmount != 0
            && cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceUnitState))
        {
            if (IsQueuedOnPlaySourcePowerTrigger(behavior))
            {
                var trigger = BuildOnPlayTriggerQueueItem(stackItem, behavior);
                events.Add(BuildTriggerQueuedEvent(trigger));
                events.Add(BuildTriggerResolvedEvent(trigger));
            }

            var modifiedSourceUnitState = ApplyPowerModifier(
                sourceUnitState,
                behavior,
                stackItem,
                stackItem.SourceObjectId,
                behavior.PowerModifierAmount,
                out var powerEvent);
            cardObjects[stackItem.SourceObjectId] = modifiedSourceUnitState;
            events.Add(powerEvent);
        }

        if (ShouldGrantBoonToSourceUnit(behavior, stackItem)
            && cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceUnitStateForBoon))
        {
            var nextSourceUnitState = ApplyBoon(
                sourceUnitStateForBoon,
                behavior,
                stackItem,
                stackItem.SourceObjectId,
                out var boonEvents);
            cardObjects[stackItem.SourceObjectId] = nextSourceUnitState;
            events.AddRange(boonEvents);
        }

        if (behavior.BanishesAllFriendlyGraveyardUnits)
        {
            BanishFriendlyGraveyardUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }

        if (behavior.PreventsAllSpellAndSkillDamageThisTurn)
        {
            untilEndOfTurnEffects = untilEndOfTurnEffects
                .Append(PreventSpellAndSkillDamageThisTurnEffectId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(effectId => effectId, StringComparer.Ordinal)
                .ToList();
            events.Add(new GameEvent(
                "STATUS_EFFECT_APPLIED",
                $"{behavior.DisplayName}无效化本回合法术或技能伤害",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["effectId"] = PreventSpellAndSkillDamageThisTurnEffectId,
                    ["scope"] = "SPELL_OR_SKILL_DAMAGE_THIS_TURN"
                }));
        }

        if (behavior.AttachesOrDetachesSecondTargetEquipmentToFirstTarget
            && TryAttachOrDetachSecondTargetEquipmentToFirstTarget(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                out var attachmentEvent))
        {
            events.Add(attachmentEvent);
        }

        if (behavior.DiscardsTargetFromHand)
        {
            foreach (var targetObjectId in stackItem.TargetObjectIds)
            {
                if (!TryDiscardCardFromHand(playerZones, cardObjects, stackItem.ControllerId, targetObjectId))
                {
                    continue;
                }

                var payload = new Dictionary<string, object?>
                {
                    ["playerId"] = stackItem.ControllerId,
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = targetObjectId,
                    ["destinationZone"] = "GRAVEYARD"
                };
                if (cardObjects.TryGetValue(targetObjectId, out var discardedObjectState)
                    && discardedObjectState.ManaCost > 0)
                {
                    payload["discardedManaCost"] = discardedObjectState.ManaCost;
                }

                events.Add(new GameEvent(
                    "CARD_DISCARDED",
                    $"{behavior.DisplayName}弃置手牌",
                    payload));
            }
        }

        if (behavior.DiscardsTargetFromOwnerHand)
        {
            foreach (var targetObjectId in stackItem.TargetObjectIds)
            {
                if (!TryDiscardCardFromAnyHand(playerZones, cardObjects, targetObjectId, out var ownerPlayerId))
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "CARD_DISCARDED",
                    $"{behavior.DisplayName}弃置目标手牌",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = ownerPlayerId,
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["discardedByPlayerId"] = stackItem.ControllerId,
                        ["destinationZone"] = "GRAVEYARD"
                    }));
            }
        }

        if (behavior.DrawsControllerAndOtherPlayers)
        {
            foreach (var drawPlayerId in ControllerAndOtherPlayerIds(state, stackItem.ControllerId))
            {
                var drawApplication = ApplyDrawToPlayer(
                    state,
                    playerZones,
                    playerScores,
                    drawPlayerId,
                    behavior.DrawCount * stackItem.EffectRepeatCount,
                    rngCursor,
                    events);
                playerScores = drawApplication.PlayerScores;
                winnerPlayerId = drawApplication.WinnerPlayerId;
                rngCursor = drawApplication.RngCursor;

                if (winnerPlayerId is not null)
                {
                    break;
                }
            }

            drawCountOverride = 0;
        }
        else if (behavior.CallsRuneForControllerAndOtherPlayers)
        {
            foreach (var runePlayerId in ControllerAndOtherPlayerIds(state, stackItem.ControllerId))
            {
                var runeCallResult = CallRunes(
                    playerZones,
                    cardObjects,
                    runePlayerId,
                    behavior.RuneCallCount);
                events.Add(new GameEvent(
                    "RUNES_CALLED",
                    $"{runePlayerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = runePlayerId,
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                        ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray()
                    }));
            }
        }
        else if (behavior.PlaysEachPlayerTopFiveUnitToBase)
        {
            var topDeckPlayResult = PlayEachPlayerTopFiveUnitsToBase(
                state,
                playerZones,
                cardObjects,
                stackItem.ControllerId,
                stackItem.SourceObjectId,
                stackItem.TargetObjectIds,
                behavior.MainDeckLookCount,
                rngCursor);
            events.AddRange(topDeckPlayResult.Events);
            rngCursor = topDeckPlayResult.RngCursor;
            drawCountOverride = 0;
        }
        else if (behavior.DrawsSelectedMainDeckTarget)
        {
            var topDeckSelectionResult = DrawSelectedMainDeckTargetsAndRecycleRest(
                state,
                playerZones,
                stackItem.ControllerId,
                stackItem.SourceObjectId,
                MainDeckTargetObjectIds(stackItem.TargetObjectIds, behavior),
                behavior.MainDeckLookCount,
                behavior.RecyclesUnselectedMainDeckLookCards);
            events.AddRange(topDeckSelectionResult.Events);
            rngCursor = topDeckSelectionResult.RngCursor;
            drawCountOverride = 0;
        }
        else if (behavior.RecyclesUnkeptSacredJudgmentCards)
        {
            var recycleResult = RecycleUnkeptSacredJudgmentCards(
                state,
                playerZones,
                cardObjects,
                stackItem.SourceObjectId,
                stackItem.TargetObjectIds,
                rngCursor);
            events.AddRange(recycleResult.Events);
            rngCursor = recycleResult.RngCursor;
        }
        else if (behavior.RecyclesTargets)
        {
            var recycleResult = RecycleTargetCards(
                state,
                playerZones,
                stackItem.ControllerId,
                stackItem.SourceObjectId,
                stackItem.TargetObjectIds);
            events.AddRange(recycleResult.Events);
            rngCursor = recycleResult.RngCursor;
        }
        else if (behavior.RuneCallCount > 0)
        {
            if (behavior.DrawsBeforeRuneCall)
            {
                var preRuneDrawCount = ResolveDrawCount(playerZones, cardObjects, stackItem.ControllerId, behavior);
                if (ShouldDrawForBehavior(behavior, stackItem, destroyedObjectIds, preRuneDrawCount))
                {
                    foreach (var drawPlayerId in DrawRecipientPlayerIds(behavior, stackItem.ControllerId, targetControllerDrawRecipientIds))
                    {
                        var drawApplication = ApplyDrawToPlayer(
                            state,
                            playerZones,
                            playerScores,
                            drawPlayerId,
                            preRuneDrawCount * stackItem.EffectRepeatCount,
                            rngCursor,
                            events);
                        playerScores = drawApplication.PlayerScores;
                        winnerPlayerId = drawApplication.WinnerPlayerId;
                        rngCursor = drawApplication.RngCursor;

                        if (winnerPlayerId is not null)
                        {
                            break;
                        }
                    }
                }

                drawCountOverride = 0;
            }

            if (winnerPlayerId is null)
            {
                var runeCallResult = CallRunes(
                    playerZones,
                    cardObjects,
                    stackItem.ControllerId,
                    behavior.RuneCallCount);
                events.Add(new GameEvent(
                    "RUNES_CALLED",
                    $"{stackItem.ControllerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = stackItem.ControllerId,
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                        ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray()
                    }));
                if (runeCallResult.CalledRuneObjectIds.Count < behavior.RuneCallCount
                    && behavior.DrawCountIfRuneCallFails > 0)
                {
                    drawCountOverride = behavior.DrawCountIfRuneCallFails;
                }
            }
        }
        else if (behavior.DestroysAllUnits)
        {
            foreach (var targetObjectId in GetFieldUnitObjectIds(playerZones, cardObjects))
            {
                if (!TryDestroyTarget(playerZones, cardObjects, targetObjectId, out var removalResult))
                {
                    continue;
                }

                events.Add(BuildFieldRemovalEvent(
                    behavior.DisplayName,
                    stackItem,
                    targetObjectId,
                    removalResult));
                if (!removalResult.WasDestroyed)
                {
                    continue;
                }

                destroyedObjectIds.Add(targetObjectId);
                if (removalResult.WasUnit)
                {
                    destroyedUnitOwnerIds.Add(removalResult.OwnerPlayerId);
                }
            }
        }
        else if (behavior.DestroysAllEquipment)
        {
            foreach (var targetObjectId in GetFieldEquipmentObjectIds(playerZones, cardObjects))
            {
                if (!TryDestroyTarget(playerZones, cardObjects, targetObjectId, out var removalResult))
                {
                    continue;
                }

                events.Add(BuildFieldRemovalEvent(
                    behavior.DisplayName,
                    stackItem,
                    targetObjectId,
                    removalResult));
                destroyedObjectIds.Add(targetObjectId);
            }
        }
        else if (behavior.ReturnsAllUnitsToHand)
        {
            foreach (var targetObjectId in GetFieldUnitObjectIds(playerZones, cardObjects))
            {
                var canResolveBattlefieldReturnTrigger = TryGetBattlefieldUnitReturnContext(
                    playerZones,
                    cardObjects,
                    targetObjectId,
                    out var battlefieldReturnPlayerId);
                if (!TryReturnTargetToHand(playerZones, cardObjects, targetObjectId, out var returnedOwnerPlayerId, out _))
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "UNIT_RETURNED_TO_HAND",
                    $"{behavior.DisplayName}让单位返回手牌",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["ownerPlayerId"] = returnedOwnerPlayerId
                    }));
                if (canResolveBattlefieldReturnTrigger
                    && TryResolveBattlefieldUnitReturnedCallRuneTrigger(
                        playerZones,
                        cardObjects,
                        runePools,
                        battlefieldReturnPlayerId,
                        targetObjectId,
                        stackItem.SourceObjectId,
                        events,
                        out var battlefieldReturnRunePools))
                {
                    runePools = battlefieldReturnRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                }
            }
        }
        else if (behavior.ReturnsAllFieldObjectsToHand)
        {
            foreach (var targetObjectId in GetFieldObjectIds(playerZones, cardObjects))
            {
                var canResolveBattlefieldReturnTrigger = TryGetBattlefieldUnitReturnContext(
                    playerZones,
                    cardObjects,
                    targetObjectId,
                    out var battlefieldReturnPlayerId);
                if (!TryReturnTargetToHand(playerZones, cardObjects, targetObjectId, out var returnedOwnerPlayerId, out var returnedWasEquipment))
                {
                    continue;
                }

                events.Add(BuildReturnedToHandEvent(
                    behavior.DisplayName,
                    stackItem,
                    targetObjectId,
                    returnedOwnerPlayerId,
                    returnedWasEquipment));
                if (canResolveBattlefieldReturnTrigger
                    && !returnedWasEquipment
                    && TryResolveBattlefieldUnitReturnedCallRuneTrigger(
                        playerZones,
                        cardObjects,
                        runePools,
                        battlefieldReturnPlayerId,
                        targetObjectId,
                        stackItem.SourceObjectId,
                        events,
                        out var battlefieldReturnRunePools))
                {
                    runePools = battlefieldReturnRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                }
            }
        }
        else if (behavior.AppliesStatusEffectToAllUnits)
        {
            ApplyStatusEffectsToFieldUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }
        else if (behavior.ExhaustsAllFriendlyUnits)
        {
            for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
            {
                foreach (var targetObjectId in GetControlledFieldUnitObjectIds(playerZones, cardObjects, stackItem.ControllerId))
                {
                    var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                        ? existingTarget
                        : new CardObjectState(targetObjectId);

                    targetState = ApplyExhaustState(
                        targetState,
                        behavior,
                        stackItem,
                        targetObjectId,
                        out var exhaustedEvent);
                    cardObjects[targetObjectId] = targetState;
                    events.Add(exhaustedEvent);
                }
            }

            if (behavior.DamagesAllBattlefieldUnits)
            {
                ApplyDamageToBattlefieldUnits(
                    playerZones,
                    cardObjects,
                    behavior,
                    stackItem,
                    ResolveDamageAmount(state, stackItem, behavior),
                    events,
                    damageTriggeredDestroyTargetObjectIds,
                    preventDamageFromThisStackItem,
                    PreventSpellAndSkillDamageThisTurnEffectId);
            }
        }
        else if (behavior.DamagesAllBattlefieldUnits)
        {
            ApplyDamageToBattlefieldUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                ResolveDamageAmount(state, stackItem, behavior),
                events,
                damageTriggeredDestroyTargetObjectIds,
                preventDamageFromThisStackItem,
                PreventSpellAndSkillDamageThisTurnEffectId);
        }
        else if (behavior.DamagesAllEnemyCombatUnits)
        {
            ApplyDamageToEnemyCombatUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                ResolveDamageAmount(state, stackItem, behavior),
                events,
                damageTriggeredDestroyTargetObjectIds,
                preventDamageFromThisStackItem,
                PreventSpellAndSkillDamageThisTurnEffectId);
        }
        else if (behavior.DamagesAllEnemyBattlefieldUnits)
        {
            ApplyDamageToEnemyBattlefieldUnits(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                ResolveDamageAmount(state, stackItem, behavior),
                events,
                damageTriggeredDestroyTargetObjectIds,
                preventDamageFromThisStackItem,
                PreventSpellAndSkillDamageThisTurnEffectId);
        }
        else if (behavior.ModifiesAllFriendlyBattlefieldUnits
            || behavior.ModifiesAllEnemyBattlefieldUnits)
        {
            ApplyBattlefieldPowerModifiers(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }
        else if (behavior.ModifiesAllFriendlyUnits
            && behavior.PowerModifierAmount != 0)
        {
            for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
            {
                foreach (var targetObjectId in GetControlledFieldUnitObjectIds(playerZones, cardObjects, stackItem.ControllerId))
                {
                    if (!CardObjectHasTag(cardObjects, targetObjectId, behavior.PowerModifierUnitTag))
                    {
                        continue;
                    }

                    var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                        ? existingTarget
                        : new CardObjectState(targetObjectId);

                    targetState = ApplyPowerModifier(
                        targetState,
                        behavior,
                        stackItem,
                        targetObjectId,
                        behavior.PowerModifierAmount,
                        out var powerEvent);
                    cardObjects[targetObjectId] = targetState;
                    events.Add(powerEvent);
                }
            }
        }
        else if (behavior.ModifiesAllEnemyUnits
            && behavior.PowerModifierAmount != 0)
        {
            for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
            {
                foreach (var targetObjectId in GetEnemyFieldUnitObjectIds(playerZones, cardObjects, stackItem.ControllerId))
                {
                    if (!CardObjectHasTag(cardObjects, targetObjectId, behavior.PowerModifierUnitTag))
                    {
                        continue;
                    }

                    var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                        ? existingTarget
                        : new CardObjectState(targetObjectId);

                    targetState = ApplyPowerModifier(
                        targetState,
                        behavior,
                        stackItem,
                        targetObjectId,
                        behavior.PowerModifierAmount,
                        out var powerEvent);
                    cardObjects[targetObjectId] = targetState;
                    events.Add(powerEvent);
                }
            }
        }
        else if (behavior.GrantsBoonToAllFriendlyUnits)
        {
            foreach (var targetObjectId in GetControlledFieldUnitObjectIds(playerZones, cardObjects, stackItem.ControllerId))
            {
                var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                    ? existingTarget
                    : new CardObjectState(targetObjectId);

                targetState = ApplyBoon(
                    targetState,
                    behavior,
                    stackItem,
                    targetObjectId,
                    out var boonEvents);
                cardObjects[targetObjectId] = targetState;
                events.AddRange(boonEvents);
            }
        }
        else if (behavior.ReadiesAllFriendlyUnits)
        {
            for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
            {
                foreach (var targetObjectId in GetControlledFieldUnitObjectIds(playerZones, cardObjects, stackItem.ControllerId))
                {
                    var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                        ? existingTarget
                        : new CardObjectState(targetObjectId);

                    targetState = ApplyReadyState(
                        targetState,
                        behavior,
                        stackItem,
                        targetObjectId,
                        out var readyEvent);
                    cardObjects[targetObjectId] = targetState;
                    events.Add(readyEvent);
                }
            }
        }
        else if (behavior.CreatedBaseUnitTokenCount > 0
            && !behavior.CreatedBaseUnitTokenCopiesFirstTarget
            && ShouldCreateBaseUnitTokens(behavior, stackItem))
        {
            CreateBaseUnitTokens(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }
        else if (behavior.DiscardsAllPlayersHandsThenDraws)
        {
            foreach (var discardPlayerId in SeatPlayerIds(state))
            {
                if (!TryDiscardPlayerHand(playerZones, cardObjects, discardPlayerId, out var discardedObjectIds)
                    || discardedObjectIds.Count == 0)
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "CARDS_DISCARDED",
                    $"{behavior.DisplayName}弃置玩家手牌",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = discardPlayerId,
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["count"] = discardedObjectIds.Count,
                        ["objectIds"] = discardedObjectIds.ToArray(),
                        ["destinationZone"] = "GRAVEYARD"
                    }));
            }

            var perPlayerDrawCount = ResolveDrawCount(playerZones, cardObjects, stackItem.ControllerId, behavior);
            foreach (var drawPlayerId in SeatPlayerIds(state))
            {
                var drawApplication = ApplyDrawToPlayer(
                    state,
                    playerZones,
                    playerScores,
                    drawPlayerId,
                    perPlayerDrawCount * stackItem.EffectRepeatCount,
                    rngCursor,
                    events);
                playerScores = drawApplication.PlayerScores;
                winnerPlayerId = drawApplication.WinnerPlayerId;
                rngCursor = drawApplication.RngCursor;

                if (winnerPlayerId is not null)
                {
                    break;
                }
            }

            drawCountOverride = 0;
        }
        else if (behavior.ReturnsGraveyardTargetToHand)
        {
            foreach (var targetObjectId in stackItem.TargetObjectIds)
            {
                if (!TryReturnGraveyardCardToHand(playerZones, cardObjects, stackItem.ControllerId, targetObjectId))
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "CARD_RETURNED_TO_HAND",
                    $"{behavior.DisplayName}让废牌堆里的牌返回手牌",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = stackItem.ControllerId,
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["sourceZone"] = "GRAVEYARD",
                        ["destinationZone"] = "HAND"
                }));
            }

            if (ShouldGrantFreeStandbyHidePermission(behavior))
            {
                var effectId = FreeStandbyHideEffectId(stackItem.ControllerId);
                untilEndOfTurnEffects = untilEndOfTurnEffects
                    .Append(effectId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(effectId => effectId, StringComparer.Ordinal)
                    .ToList();
                events.Add(new GameEvent(
                    "STANDBY_HIDE_PERMISSION_GRANTED",
                    $"{behavior.DisplayName}允许本回合免费正面朝下布置待命牌",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = stackItem.ControllerId,
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["effectId"] = effectId,
                        ["optionalCost"] = StandbyHideFreeOptionalCost
                    }));
            }
        }
        else if (behavior.PlaysGraveyardTargetToBase)
        {
            foreach (var targetObjectId in stackItem.TargetObjectIds)
            {
                if (!TryPlayGraveyardCardToBase(playerZones, cardObjects, stackItem.ControllerId, targetObjectId))
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "UNIT_PLAYED_TO_BASE",
                    $"{behavior.DisplayName}打出废牌堆里的单位到基地",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["ownerPlayerId"] = stackItem.ControllerId,
                        ["sourceZone"] = "GRAVEYARD",
                        ["destinationZone"] = "BASE"
                    }));
            }
        }
        else if (behavior.PlaysHandTargetToBase)
        {
            foreach (var targetObjectId in stackItem.TargetObjectIds)
            {
                if (!TryPlayHandCardToBase(
                        playerZones,
                        cardObjects,
                        targetObjectId,
                        behavior.StatusEffectId,
                        out var ownerPlayerId,
                        out _))
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "UNIT_PLAYED_TO_BASE",
                    $"{behavior.DisplayName}打出手牌里的单位到基地",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["ownerPlayerId"] = ownerPlayerId,
                        ["sourceZone"] = "HAND",
                        ["destinationZone"] = "BASE"
                    }));

                if (!string.IsNullOrWhiteSpace(behavior.StatusEffectId))
                {
                    events.Add(new GameEvent(
                        "STATUS_EFFECT_APPLIED",
                        $"{behavior.DisplayName}施加{behavior.StatusEffectId}",
                        new Dictionary<string, object?>
                        {
                            ["sourceObjectId"] = stackItem.SourceObjectId,
                            ["targetObjectId"] = targetObjectId,
                            ["effectId"] = behavior.StatusEffectId
                        }));
                }
            }
        }
        else if (behavior.PlaysOpponentTopMainDeckUnitToBase)
        {
            foreach (var targetObjectId in stackItem.TargetObjectIds)
            {
                if (!TryPlayOpponentTopMainDeckUnitToBase(
                        playerZones,
                        cardObjects,
                        stackItem.ControllerId,
                        targetObjectId,
                        out var ownerPlayerId,
                        out _))
                {
                    continue;
                }

                events.Add(new GameEvent(
                    "UNIT_PLAYED_TO_BASE",
                    $"{behavior.DisplayName}打出对手主牌堆顶部单位到己方基地",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["ownerPlayerId"] = ownerPlayerId,
                        ["playedByPlayerId"] = stackItem.ControllerId,
                        ["sourceZone"] = "MAIN_DECK",
                        ["destinationZone"] = "BASE"
                    }));
            }
        }
        else if (behavior.DealsMutualTargetPowerDamage
            && stackItem.TargetObjectIds.Count >= 2)
        {
            var firstTargetObjectId = stackItem.TargetObjectIds[0];
            var secondTargetObjectId = stackItem.TargetObjectIds[1];

            var canResolveTargets = IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, firstTargetObjectId)
                && IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, secondTargetObjectId)
                && (!behavior.MovesFirstTargetToSecondTargetLocation
                || CanMoveFirstTargetToSecondTargetLocation(
                    playerZones,
                    cardObjects,
                    firstTargetObjectId,
                    secondTargetObjectId));
            if (canResolveTargets)
            {
                if (behavior.MovesFirstTargetToSecondTargetLocation
                    && TryMoveFirstTargetToSecondTargetLocation(
                        playerZones,
                        cardObjects,
                        firstTargetObjectId,
                        secondTargetObjectId,
                        out var destinationPlayerId,
                        out var destinationZone))
                {
                    events.Add(new GameEvent(
                        "UNIT_MOVED_TO_UNIT_LOCATION",
                        $"{behavior.DisplayName}让单位移动到另一名单位所在位置",
                        new Dictionary<string, object?>
                        {
                            ["sourceObjectId"] = stackItem.SourceObjectId,
                            ["targetObjectId"] = firstTargetObjectId,
                            ["destinationTargetObjectId"] = secondTargetObjectId,
                            ["destinationPlayerId"] = destinationPlayerId,
                            ["destinationZone"] = destinationZone
                        }));
                }

                if (behavior.PowerModifierAmount != 0
                    && cardObjects.TryGetValue(firstTargetObjectId, out var currentFirstTargetStateForModifier))
                {
                    var modifiedFirstTargetState = ApplyPowerModifier(
                        currentFirstTargetStateForModifier,
                        behavior,
                        stackItem,
                        firstTargetObjectId,
                        behavior.PowerModifierAmount,
                        out var powerEvent);
                    cardObjects[firstTargetObjectId] = modifiedFirstTargetState;
                    events.Add(powerEvent);
                }

                for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
                {
                    var firstTargetPower = cardObjects.TryGetValue(firstTargetObjectId, out var firstTargetState)
                        ? Math.Max(0, firstTargetState.Power)
                        : 0;
                    var secondTargetPower = cardObjects.TryGetValue(secondTargetObjectId, out var secondTargetState)
                        ? Math.Max(0, secondTargetState.Power)
                        : 0;

                    if (firstTargetPower > 0
                        && cardObjects.ContainsKey(secondTargetObjectId))
                    {
                        var damageApplication = ApplyDamageToCardObject(
                            cardObjects,
                            secondTargetObjectId,
                            firstTargetPower,
                            damageTriggeredDestroyTargetObjectIds,
                            preventDamageFromThisStackItem,
                            PreventSpellAndSkillDamageThisTurnEffectId);
                        events.Add(new GameEvent(
                            "DAMAGE_APPLIED",
                            $"{behavior.DisplayName}造成单位互斗伤害",
                            BuildDamagePayload(
                                stackItem.SourceObjectId,
                                secondTargetObjectId,
                                damageApplication,
                                firstTargetObjectId)));
                    }

                    if (secondTargetPower > 0
                        && cardObjects.ContainsKey(firstTargetObjectId))
                    {
                        var damageApplication = ApplyDamageToCardObject(
                            cardObjects,
                            firstTargetObjectId,
                            secondTargetPower,
                            damageTriggeredDestroyTargetObjectIds,
                            preventDamageFromThisStackItem,
                            PreventSpellAndSkillDamageThisTurnEffectId);
                        events.Add(new GameEvent(
                            "DAMAGE_APPLIED",
                            $"{behavior.DisplayName}造成单位互斗伤害",
                            BuildDamagePayload(
                                stackItem.SourceObjectId,
                                firstTargetObjectId,
                                damageApplication,
                                secondTargetObjectId)));
                    }
                }
            }
        }
        else if (behavior.DealsSourceAndTargetPowerDamage
            && stackItem.TargetObjectIds.Count >= 1)
        {
            var targetObjectId = stackItem.TargetObjectIds[0];
            if (IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, stackItem.SourceObjectId)
                && IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, targetObjectId))
            {
                var sourcePower = cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceState)
                    ? Math.Max(0, sourceState.Power)
                    : 0;
                var targetPower = cardObjects.TryGetValue(targetObjectId, out var targetState)
                    ? Math.Max(0, targetState.Power)
                    : 0;

                if (sourcePower > 0
                    && cardObjects.ContainsKey(targetObjectId))
                {
                    var damageApplication = ApplyDamageToCardObject(
                        cardObjects,
                        targetObjectId,
                        sourcePower,
                        damageTriggeredDestroyTargetObjectIds,
                        preventDamageFromThisStackItem,
                        PreventSpellAndSkillDamageThisTurnEffectId);
                    events.Add(new GameEvent(
                        "DAMAGE_APPLIED",
                        $"{behavior.DisplayName}造成源单位互斗伤害",
                        BuildDamagePayload(
                            stackItem.SourceObjectId,
                            targetObjectId,
                            damageApplication,
                            stackItem.SourceObjectId)));
                }

                if (targetPower > 0
                    && cardObjects.ContainsKey(stackItem.SourceObjectId))
                {
                    var damageApplication = ApplyDamageToCardObject(
                        cardObjects,
                        stackItem.SourceObjectId,
                        targetPower,
                        damageTriggeredDestroyTargetObjectIds,
                        preventDamageFromThisStackItem,
                        PreventSpellAndSkillDamageThisTurnEffectId);
                    events.Add(new GameEvent(
                        "DAMAGE_APPLIED",
                        $"{behavior.DisplayName}造成目标单位互斗伤害",
                        BuildDamagePayload(
                            stackItem.SourceObjectId,
                            stackItem.SourceObjectId,
                            damageApplication,
                            targetObjectId)));
                }
            }
        }
        else if (behavior.DamagesSecondTargetByFirstTargetPower
            && stackItem.TargetObjectIds.Count >= 2)
        {
            var firstTargetObjectId = stackItem.TargetObjectIds[0];
            var damagedTargetObjectId = stackItem.TargetObjectIds[1];

            if (IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, firstTargetObjectId)
                && IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, damagedTargetObjectId))
            {
                var damageAmount = cardObjects.TryGetValue(firstTargetObjectId, out var firstTargetState)
                    ? Math.Max(0, firstTargetState.Power)
                    : 0;
                if (damageAmount > 0
                    && cardObjects.ContainsKey(damagedTargetObjectId))
                {
                    var damageApplication = ApplyDamageToCardObject(
                        cardObjects,
                        damagedTargetObjectId,
                        damageAmount,
                        damageTriggeredDestroyTargetObjectIds,
                        preventDamageFromThisStackItem,
                        PreventSpellAndSkillDamageThisTurnEffectId);
                    events.Add(new GameEvent(
                        "DAMAGE_APPLIED",
                        $"{behavior.DisplayName}按友方单位战力造成伤害",
                        BuildDamagePayload(
                            stackItem.SourceObjectId,
                            damagedTargetObjectId,
                            damageApplication,
                            firstTargetObjectId)));
                }
            }
        }
        else if (behavior.ReadiesFirstTargetAndDamagesSecondByFirstPower
            && stackItem.TargetObjectIds.Count >= 2)
        {
            var readyTargetObjectId = stackItem.TargetObjectIds[0];
            var damagedTargetObjectId = stackItem.TargetObjectIds[1];

            if (IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, readyTargetObjectId)
                && IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, damagedTargetObjectId)
                && cardObjects.TryGetValue(readyTargetObjectId, out var readyTargetState))
            {
                var readiedTargetState = ApplyReadyState(
                    readyTargetState,
                    behavior,
                    stackItem,
                    readyTargetObjectId,
                    out var readyEvent);
                cardObjects[readyTargetObjectId] = readiedTargetState;
                events.Add(readyEvent);

                var damageAmount = Math.Max(0, readiedTargetState.Power);
                if (damageAmount > 0
                    && IsFieldObject(playerZones, damagedTargetObjectId)
                    && cardObjects.ContainsKey(damagedTargetObjectId))
                {
                    var damageApplication = ApplyDamageToCardObject(
                        cardObjects,
                        damagedTargetObjectId,
                        damageAmount,
                        damageTriggeredDestroyTargetObjectIds,
                        preventDamageFromThisStackItem,
                        PreventSpellAndSkillDamageThisTurnEffectId);
                    events.Add(new GameEvent(
                        "DAMAGE_APPLIED",
                        $"{behavior.DisplayName}按友方单位战力造成伤害",
                        BuildDamagePayload(
                            stackItem.SourceObjectId,
                            damagedTargetObjectId,
                            damageApplication,
                            readyTargetObjectId)));
                }
            }
        }
        else if (behavior.SetsFirstTargetPowerToSecondIfLower
            && stackItem.TargetObjectIds.Count >= 2)
        {
            var modifiedTargetObjectId = stackItem.TargetObjectIds[0];
            var comparisonTargetObjectId = stackItem.TargetObjectIds[1];
            if (IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, modifiedTargetObjectId)
                && IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, comparisonTargetObjectId)
                && cardObjects.TryGetValue(modifiedTargetObjectId, out var modifiedTargetState)
                && cardObjects.TryGetValue(comparisonTargetObjectId, out var comparisonTargetState)
                && modifiedTargetState.Power < comparisonTargetState.Power)
            {
                var powerModifierAmount = comparisonTargetState.Power - modifiedTargetState.Power;
                var nextTargetState = ApplyPowerModifier(
                    modifiedTargetState,
                    behavior,
                    stackItem,
                    modifiedTargetObjectId,
                    powerModifierAmount,
                    out var powerEvent);
                cardObjects[modifiedTargetObjectId] = nextTargetState;
                events.Add(powerEvent);
            }
        }
        else if (behavior.SwapsTargetPowersUntilEndOfTurn
            && stackItem.TargetObjectIds.Count >= 2)
        {
            var firstTargetObjectId = stackItem.TargetObjectIds[0];
            var secondTargetObjectId = stackItem.TargetObjectIds[1];
            if (IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, firstTargetObjectId)
                && IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, secondTargetObjectId)
                && cardObjects.TryGetValue(firstTargetObjectId, out var firstTargetState)
                && cardObjects.TryGetValue(secondTargetObjectId, out var secondTargetState))
            {
                var firstPowerDelta = secondTargetState.Power - firstTargetState.Power;
                var secondPowerDelta = firstTargetState.Power - secondTargetState.Power;

                if (firstPowerDelta != 0)
                {
                    var nextFirstTargetState = ApplyPowerModifier(
                        firstTargetState,
                        behavior,
                        stackItem,
                        firstTargetObjectId,
                        firstPowerDelta,
                        out var firstPowerEvent);
                    cardObjects[firstTargetObjectId] = nextFirstTargetState;
                    events.Add(firstPowerEvent);
                }

                if (secondPowerDelta != 0)
                {
                    var nextSecondTargetState = ApplyPowerModifier(
                        secondTargetState,
                        behavior,
                        stackItem,
                        secondTargetObjectId,
                        secondPowerDelta,
                        out var secondPowerEvent);
                    cardObjects[secondTargetObjectId] = nextSecondTargetState;
                    events.Add(secondPowerEvent);
                }
            }
        }
        else if (behavior.DamagesAllEnemyBattlefieldUnitsByFirstTargetPower
            && stackItem.TargetObjectIds.Count >= 1)
        {
            var firstTargetObjectId = stackItem.TargetObjectIds[0];
            var canResolveTargetMove = IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, firstTargetObjectId)
                && (!behavior.MovesTargetToBattlefield
                || CanMoveTargetToControllerBattlefield(
                    playerZones,
                    cardObjects,
                    stackItem.ControllerId,
                    firstTargetObjectId));
            if (canResolveTargetMove)
            {
                var damageAmount = cardObjects.TryGetValue(firstTargetObjectId, out var firstTargetState)
                    ? Math.Max(0, firstTargetState.Power)
                    : 0;
                ApplyDamageToEnemyBattlefieldUnits(
                    playerZones,
                    cardObjects,
                    behavior,
                    stackItem,
                    damageAmount,
                    events,
                    damageTriggeredDestroyTargetObjectIds,
                    preventDamageFromThisStackItem,
                    PreventSpellAndSkillDamageThisTurnEffectId);

                if (behavior.MovesTargetToBattlefield
                    && TryMoveTargetToControllerBattlefield(
                        playerZones,
                        cardObjects,
                        stackItem.ControllerId,
                        firstTargetObjectId))
                {
                    events.Add(new GameEvent(
                        "UNIT_MOVED_TO_BATTLEFIELD",
                        $"{behavior.DisplayName}让单位移动到战场",
                        new Dictionary<string, object?>
                        {
                            ["sourceObjectId"] = stackItem.SourceObjectId,
                            ["targetObjectId"] = firstTargetObjectId,
                            ["controllerId"] = stackItem.ControllerId,
                            ["destinationZone"] = "BATTLEFIELD"
                        }));
                }
            }
        }
        else if (behavior.DestroysFirstTargetAndBuffsSecondByDestroyedPower
            && stackItem.TargetObjectIds.Count >= 2)
        {
            var destroyedTargetObjectId = stackItem.TargetObjectIds[0];
            var buffedTargetObjectId = stackItem.TargetObjectIds[1];
            var destroyedPower = cardObjects.TryGetValue(destroyedTargetObjectId, out var destroyedTargetState)
                ? Math.Max(0, destroyedTargetState.Power)
                : 0;

            if (TryDestroyControlledFieldTarget(playerZones, cardObjects, destroyedTargetObjectId, out var removalResult))
            {
                events.Add(BuildFieldRemovalEvent(
                    behavior.DisplayName,
                    stackItem,
                    destroyedTargetObjectId,
                    removalResult));
                if (removalResult.WasDestroyed)
                {
                    destroyedObjectIds.Add(destroyedTargetObjectId);
                    if (removalResult.WasUnit)
                    {
                        destroyedUnitOwnerIds.Add(removalResult.OwnerPlayerId);
                    }
                }
            }

            if (removalResult.WasDestroyed
                && destroyedPower > 0
                && IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, buffedTargetObjectId)
                && cardObjects.TryGetValue(buffedTargetObjectId, out var buffedTargetState))
            {
                var modifiedTargetState = ApplyPowerModifier(
                    buffedTargetState,
                    behavior,
                    stackItem,
                    buffedTargetObjectId,
                    destroyedPower,
                    out var powerEvent);
                cardObjects[buffedTargetObjectId] = modifiedTargetState;
                events.Add(powerEvent);
            }
        }
        else if (behavior.MovesFirstTargetToSecondTargetLocation
            && stackItem.TargetObjectIds.Count >= 2
            && TryMoveFirstTargetToSecondTargetLocation(
                playerZones,
                cardObjects,
                stackItem.TargetObjectIds[0],
                stackItem.TargetObjectIds[1],
                out var destinationPlayerId,
                out var destinationZone))
        {
            events.Add(new GameEvent(
                "UNIT_MOVED_TO_UNIT_LOCATION",
                $"{behavior.DisplayName}让单位移动到另一名单位所在位置",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = stackItem.TargetObjectIds[0],
                    ["destinationTargetObjectId"] = stackItem.TargetObjectIds[1],
                    ["destinationPlayerId"] = destinationPlayerId,
                    ["destinationZone"] = destinationZone
                }));
        }
        else if (behavior.SwapsTargetLocations
            && stackItem.TargetObjectIds.Count >= 2
            && TrySwapTargetLocations(
                playerZones,
                cardObjects,
                stackItem.TargetObjectIds[0],
                stackItem.TargetObjectIds[1],
                out var firstDestinationPlayerId,
                out var firstDestinationZone,
                out var secondDestinationPlayerId,
                out var secondDestinationZone))
        {
            events.Add(new GameEvent(
                "UNIT_LOCATIONS_SWAPPED",
                $"{behavior.DisplayName}交换两名单位位置",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["firstTargetObjectId"] = stackItem.TargetObjectIds[0],
                    ["secondTargetObjectId"] = stackItem.TargetObjectIds[1],
                    ["firstDestinationPlayerId"] = firstDestinationPlayerId,
                    ["firstDestinationZone"] = firstDestinationZone,
                    ["secondDestinationPlayerId"] = secondDestinationPlayerId,
                    ["secondDestinationZone"] = secondDestinationZone
                }));
        }
        else if (behavior.RequiredTargetCount > 0
            || behavior.UsesFriendlyBattlefieldUnitCountAsMaxTargetCount)
        {
            for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
            {
                for (var targetIndex = 0; targetIndex < stackItem.TargetObjectIds.Count; targetIndex++)
                {
                    var targetObjectId = stackItem.TargetObjectIds[targetIndex];
                    if (!IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, targetObjectId))
                    {
                        continue;
                    }

                    if (behavior.MovesTargetToBattlefield
                        && !CanMoveTargetToControllerBattlefield(
                            playerZones,
                            cardObjects,
                            stackItem.ControllerId,
                            targetObjectId))
                    {
                        continue;
                    }

                    var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                        ? existingTarget
                        : new CardObjectState(targetObjectId);

                    var damageAmount = ShouldApplyDamageToTarget(behavior, targetIndex)
                        ? ResolveDamageAmount(state, stackItem, behavior, targetObjectId)
                        : 0;
                    if (behavior.BanishesIfDestroyedThisTurn)
                    {
                        targetState = targetState with
                        {
                            UntilEndOfTurnEffects = targetState.UntilEndOfTurnEffects
                                .Concat([BanishIfDestroyedThisTurnEffectId])
                                .Distinct(StringComparer.Ordinal)
                                .OrderBy(effectId => effectId, StringComparer.Ordinal)
                                .ToArray()
                        };
                        events.Add(new GameEvent(
                            "DESTROY_REPLACEMENT_EFFECT_APPLIED",
                            $"{behavior.DisplayName}设置本回合摧毁改为放逐",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["effectId"] = BanishIfDestroyedThisTurnEffectId
                            }));
                        cardObjects[targetObjectId] = targetState;
                    }

                    if (damageAmount > 0)
                    {
                        var damageApplication = ApplyDamageToCardObject(
                            cardObjects,
                            targetObjectId,
                            damageAmount,
                            damageTriggeredDestroyTargetObjectIds,
                            preventDamageFromThisStackItem,
                            PreventSpellAndSkillDamageThisTurnEffectId);
                        targetState = cardObjects[targetObjectId];
                        events.Add(new GameEvent(
                            "DAMAGE_APPLIED",
                            $"{behavior.DisplayName}造成 {damageApplication.DamageAmount} 点伤害",
                            BuildDamagePayload(stackItem.SourceObjectId, targetObjectId, damageApplication)));
                    }

                    var statusEffectIds = ShouldApplyStatusEffectsToTarget(behavior, targetIndex)
                        ? ResolveStatusEffectIds(behavior)
                        : Array.Empty<string>();
                    if (statusEffectIds.Length > 0)
                    {
                        var primaryStatusEffectId = statusEffectIds[0];
                        if (behavior.ReturnsTargetToHandIfAlreadyHasStatusEffect
                            && targetState.UntilEndOfTurnEffects.Contains(primaryStatusEffectId, StringComparer.Ordinal)
                            && TryReturnTargetToHand(playerZones, cardObjects, targetObjectId, out var statusReturnOwnerPlayerId, out _))
                        {
                            events.Add(new GameEvent(
                                "UNIT_RETURNED_TO_HAND",
                                $"{behavior.DisplayName}让已受状态影响的单位返回手牌",
                                new Dictionary<string, object?>
                                {
                                    ["sourceObjectId"] = stackItem.SourceObjectId,
                                    ["targetObjectId"] = targetObjectId,
                                    ["ownerPlayerId"] = statusReturnOwnerPlayerId,
                                    ["statusEffectId"] = primaryStatusEffectId
                                }));
                            continue;
                        }

                        if (behavior.DestroysTargetIfAlreadyHasStatusEffect
                            && targetState.UntilEndOfTurnEffects.Contains(primaryStatusEffectId, StringComparer.Ordinal)
                            && TryDestroyControlledFieldTarget(playerZones, cardObjects, targetObjectId, out var statusRemovalResult))
                        {
                            events.Add(BuildFieldRemovalEvent(
                                behavior.DisplayName,
                                stackItem,
                                targetObjectId,
                                statusRemovalResult,
                                "ALREADY_HAS_STATUS_EFFECT"));
                            if (statusRemovalResult.WasDestroyed)
                            {
                                destroyedObjectIds.Add(targetObjectId);
                                targetControllerDrawRecipientIds.Add(statusRemovalResult.OwnerPlayerId);
                                if (statusRemovalResult.WasUnit)
                                {
                                    destroyedUnitOwnerIds.Add(statusRemovalResult.OwnerPlayerId);
                                }
                            }

                            continue;
                        }

                        foreach (var statusEffectId in statusEffectIds)
                        {
                            targetState = targetState with
                            {
                                UntilEndOfTurnEffects = targetState.UntilEndOfTurnEffects
                                    .Concat([statusEffectId])
                                    .Distinct(StringComparer.Ordinal)
                                    .OrderBy(effectId => effectId, StringComparer.Ordinal)
                                    .ToArray()
                            };
                            events.Add(new GameEvent(
                                "STATUS_EFFECT_APPLIED",
                                $"{behavior.DisplayName}施加{statusEffectId}",
                                new Dictionary<string, object?>
                                {
                                    ["sourceObjectId"] = stackItem.SourceObjectId,
                                    ["targetObjectId"] = targetObjectId,
                                    ["effectId"] = statusEffectId
                                }));
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(behavior.TargetAddedTag))
                    {
                        targetState = ApplyTargetTag(
                            targetState,
                            behavior,
                            stackItem,
                            targetObjectId,
                            out var tagEvent);
                        events.Add(tagEvent);
                    }

                    if (behavior.GrantsBoon)
                    {
                        targetState = ApplyBoon(
                            targetState,
                            behavior,
                            stackItem,
                            targetObjectId,
                            out var boonEvents);
                        events.AddRange(boonEvents);
                    }

                    var powerModifierAmount = ShouldApplyPowerModifierToTarget(behavior, targetIndex)
                        ? ResolvePowerModifierAmount(
                            behavior,
                            targetIndex,
                            playerZones,
                            cardObjects,
                            stackItem.ControllerId,
                            targetObjectId)
                        : 0;
                    if (powerModifierAmount != 0
                        && PowerModifierConditionApplies(targetState, behavior))
                    {
                        targetState = ApplyPowerModifier(
                            targetState,
                            behavior,
                            stackItem,
                            targetObjectId,
                            powerModifierAmount,
                            out var powerEvent);
                        events.Add(powerEvent);
                    }

                    if (behavior.ReadiesTarget)
                    {
                        targetState = ApplyReadyState(
                            targetState,
                            behavior,
                            stackItem,
                            targetObjectId,
                            out var readyEvent);
                        events.Add(readyEvent);
                    }

                    cardObjects[targetObjectId] = targetState;

                    if (behavior.DestroysTarget
                        && TryDestroyControlledFieldTarget(playerZones, cardObjects, targetObjectId, out var removalResult))
                    {
                        events.Add(BuildFieldRemovalEvent(
                            behavior.DisplayName,
                            stackItem,
                            targetObjectId,
                            removalResult));
                        if (removalResult.WasDestroyed)
                        {
                            destroyedObjectIds.Add(targetObjectId);
                            targetControllerDrawRecipientIds.Add(removalResult.OwnerPlayerId);
                            if (removalResult.WasUnit)
                            {
                                destroyedUnitOwnerIds.Add(removalResult.OwnerPlayerId);
                            }

                            var lastBreathDrawPlayerId = ResolveWatchfulSentinelLastBreathDrawPlayerId(
                                targetState,
                                removalResult);
                            if (lastBreathDrawPlayerId is not null)
                            {
                                var trigger = BuildLastBreathTriggerQueueItem(
                                    stackItem,
                                    targetObjectId,
                                    lastBreathDrawPlayerId,
                                    WatchfulSentinelLastBreathDrawEffectKind);
                                events.Add(BuildTriggerQueuedEvent(trigger));
                                events.Add(BuildTriggerResolvedEvent(trigger));

                                var drawApplication = ApplyDrawToPlayer(
                                    state,
                                    playerZones,
                                    playerScores,
                                    lastBreathDrawPlayerId,
                                    1,
                                    rngCursor,
                                    events);
                                playerScores = drawApplication.PlayerScores;
                                winnerPlayerId = drawApplication.WinnerPlayerId ?? winnerPlayerId;
                                rngCursor = drawApplication.RngCursor;
                            }

                            var conditionalEquipmentTokenCount = ConditionalEquipmentTokenCountForDestroyedUnit(
                                behavior,
                                stackItem.ControllerId,
                                removalResult);
                            if (conditionalEquipmentTokenCount > 0)
                            {
                                CreateBaseEquipmentTokens(
                                    playerZones,
                                    cardObjects,
                                    behavior,
                                    stackItem,
                                    events,
                                    conditionalEquipmentTokenCount);
                            }
                        }

                        continue;
                    }

                    if (behavior.ReturnsTargetToHand
                        && TryGetBattlefieldUnitReturnContext(
                            playerZones,
                            cardObjects,
                            targetObjectId,
                            out var battlefieldReturnPlayerId))
                    {
                        if (TryReturnTargetToHand(playerZones, cardObjects, targetObjectId, out var battlefieldReturnedOwnerPlayerId, out var battlefieldReturnedWasEquipment))
                        {
                            events.Add(BuildReturnedToHandEvent(
                                behavior.DisplayName,
                                stackItem,
                                targetObjectId,
                                battlefieldReturnedOwnerPlayerId,
                                battlefieldReturnedWasEquipment));
                            if (behavior.RuneCallCountAfterTargetReturn > 0)
                            {
                                var runeCallResult = CallRunes(
                                    playerZones,
                                    cardObjects,
                                    battlefieldReturnedOwnerPlayerId,
                                    behavior.RuneCallCountAfterTargetReturn);
                                events.Add(new GameEvent(
                                    "RUNES_CALLED",
                                    $"{battlefieldReturnedOwnerPlayerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
                                    new Dictionary<string, object?>
                                    {
                                        ["playerId"] = battlefieldReturnedOwnerPlayerId,
                                        ["sourceObjectId"] = stackItem.SourceObjectId,
                                        ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                                        ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray()
                                    }));
                            }

                            if (!battlefieldReturnedWasEquipment
                                && TryResolveBattlefieldUnitReturnedCallRuneTrigger(
                                    playerZones,
                                    cardObjects,
                                    runePools,
                                    battlefieldReturnPlayerId,
                                    targetObjectId,
                                    stackItem.SourceObjectId,
                                    events,
                                    out var battlefieldReturnRunePools))
                            {
                                runePools = battlefieldReturnRunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                            }
                        }

                        continue;
                    }

                    if (behavior.ReturnsTargetToHand
                        && TryReturnTargetToHand(playerZones, cardObjects, targetObjectId, out var normalReturnedOwnerPlayerId, out var normalReturnedWasEquipment))
                    {
                        events.Add(BuildReturnedToHandEvent(
                            behavior.DisplayName,
                            stackItem,
                            targetObjectId,
                            normalReturnedOwnerPlayerId,
                            normalReturnedWasEquipment));
                        if (behavior.RuneCallCountAfterTargetReturn > 0)
                        {
                            var runeCallResult = CallRunes(
                                playerZones,
                                cardObjects,
                                normalReturnedOwnerPlayerId,
                                behavior.RuneCallCountAfterTargetReturn);
                            events.Add(new GameEvent(
                                "RUNES_CALLED",
                                $"{normalReturnedOwnerPlayerId} 召出 {runeCallResult.CalledRuneObjectIds.Count} 张符文",
                                new Dictionary<string, object?>
                                {
                                    ["playerId"] = normalReturnedOwnerPlayerId,
                                    ["sourceObjectId"] = stackItem.SourceObjectId,
                                    ["count"] = runeCallResult.CalledRuneObjectIds.Count,
                                    ["runeObjectIds"] = runeCallResult.CalledRuneObjectIds.ToArray()
                                }));
                        }

                        continue;
                    }

                    if (ShouldApplyBanishPlayToTarget(behavior, targetIndex)
                        && (behavior.BanishesTargetThenPlaysToBase || behavior.BanishesTargetThenPlaysToBattlefield)
                        && TryBanishTargetThenPlayToOwnerField(
                            playerZones,
                            cardObjects,
                            targetObjectId,
                            behavior.BanishesTargetThenPlaysToBattlefield ? "BATTLEFIELD" : "BASE",
                            out var rescuedOwnerPlayerId))
                    {
                        var playedDestinationZone = behavior.BanishesTargetThenPlaysToBattlefield ? "BATTLEFIELD" : "BASE";
                        events.Add(new GameEvent(
                            "UNIT_BANISHED",
                            $"{behavior.DisplayName}放逐单位",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerPlayerId"] = rescuedOwnerPlayerId,
                                ["destinationZone"] = "BANISHED"
                            }));
                        events.Add(new GameEvent(
                            behavior.BanishesTargetThenPlaysToBattlefield ? "UNIT_PLAYED_TO_BATTLEFIELD" : "UNIT_PLAYED_TO_BASE",
                            behavior.BanishesTargetThenPlaysToBattlefield
                                ? $"{behavior.DisplayName}将单位打出到战场"
                                : $"{behavior.DisplayName}将单位打出到基地",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerPlayerId"] = rescuedOwnerPlayerId,
                                ["destinationZone"] = playedDestinationZone
                            }));
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(behavior.TargetOwnerMainDeckDestination)
                        && TryMoveTargetToOwnerMainDeck(
                            playerZones,
                            cardObjects,
                            targetObjectId,
                            behavior.TargetOwnerMainDeckDestination,
                            out var deckOwnerPlayerId,
                            out var deckPosition))
                    {
                        events.Add(new GameEvent(
                            "UNIT_RETURNED_TO_DECK",
                            $"{behavior.DisplayName}让单位放到所属者主牌堆",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerPlayerId"] = deckOwnerPlayerId,
                                ["destinationZone"] = "MAIN_DECK",
                                ["deckPosition"] = deckPosition
                            }));
                        continue;
                    }

                    if (behavior.GainsControlOfTargetToBase
                        && TryGainControlOfTargetToBase(
                            playerZones,
                            cardObjects,
                            stackItem.ControllerId,
                            targetObjectId,
                            behavior.ExhaustsControlledTarget,
                            out var previousControllerId,
                            out var controlledTargetState))
                    {
                        events.Add(new GameEvent(
                            "UNIT_CONTROL_GAINED",
                            $"{behavior.DisplayName}获得单位控制权并召回",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerId"] = controlledTargetState.OwnerId,
                                ["controllerId"] = stackItem.ControllerId,
                                ["previousControllerId"] = previousControllerId,
                                ["destinationZone"] = "BASE",
                                ["isExhausted"] = controlledTargetState.IsExhausted
                            }));
                        continue;
                    }

                    if (behavior.GainsControlOfTargetToBattlefield
                        && TryGainControlOfTargetToBattlefield(
                            playerZones,
                            cardObjects,
                            stackItem.ControllerId,
                            targetObjectId,
                            out var previousBattlefieldControllerId,
                            out var battleControlledTargetState))
                    {
                        events.Add(new GameEvent(
                            "UNIT_CONTROL_GAINED",
                            $"{behavior.DisplayName}获得战场单位控制权",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerId"] = battleControlledTargetState.OwnerId,
                                ["controllerId"] = stackItem.ControllerId,
                                ["previousControllerId"] = previousBattlefieldControllerId,
                                ["destinationZone"] = "BATTLEFIELD",
                                ["isExhausted"] = battleControlledTargetState.IsExhausted,
                                ["returnEffectId"] = BuildReturnControlToOwnerAtTurnEndEffectId(battleControlledTargetState.OwnerId ?? previousBattlefieldControllerId)
                            }));
                        continue;
                    }

                    if (behavior.MovesTargetToBase
                        && targetIndex >= behavior.MoveToBaseTargetStartIndex
                        && TryMoveTargetToOwnerBase(playerZones, cardObjects, targetObjectId, out var movedOwnerPlayerId))
                    {
                        events.Add(new GameEvent(
                            "UNIT_MOVED_TO_BASE",
                            $"{behavior.DisplayName}让单位移动到所属基地",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerPlayerId"] = movedOwnerPlayerId
                            }));
                        continue;
                    }

                    if (behavior.MovesTargetsToOwnerBattlefields
                        && TryMoveTargetToOwnerBattlefield(
                            playerZones,
                            cardObjects,
                            targetObjectId,
                            out var ownerBattlefieldPlayerId))
                    {
                        events.Add(new GameEvent(
                            "UNIT_MOVED_TO_BATTLEFIELD",
                            $"{behavior.DisplayName}让单位移动到战场",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["ownerPlayerId"] = ownerBattlefieldPlayerId,
                                ["destinationZone"] = "BATTLEFIELD"
                            }));
                        continue;
                    }

                    if (behavior.MovesTargetToBattlefield
                        && TryMoveTargetToControllerBattlefield(
                            playerZones,
                            cardObjects,
                            stackItem.ControllerId,
                            targetObjectId))
                    {
                        events.Add(new GameEvent(
                            "UNIT_MOVED_TO_BATTLEFIELD",
                            $"{behavior.DisplayName}让单位移动到战场",
                            new Dictionary<string, object?>
                            {
                                ["sourceObjectId"] = stackItem.SourceObjectId,
                                ["targetObjectId"] = targetObjectId,
                                ["controllerId"] = stackItem.ControllerId,
                                ["destinationZone"] = "BATTLEFIELD"
                            }));
                        continue;
                    }

                    cardObjects[targetObjectId] = targetState;
                }

                if (behavior.OtherEnemyBattlefieldUnitsDamageAmount > 0)
                {
                    ApplyDamageToOtherEnemyBattlefieldUnits(
                        playerZones,
                        cardObjects,
                        behavior,
                        stackItem,
                        behavior.OtherEnemyBattlefieldUnitsDamageAmount,
                        stackItem.TargetObjectIds,
                        events,
                        damageTriggeredDestroyTargetObjectIds,
                        preventDamageFromThisStackItem,
                        PreventSpellAndSkillDamageThisTurnEffectId);
                }
            }
        }

        if (behavior.CreatedBaseUnitTokenCount > 0
            && behavior.CreatedBaseUnitTokenCopiesFirstTarget)
        {
            CreateBaseUnitTokens(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }

        if (behavior.CreatedBaseEquipmentTokenCount > 0)
        {
            CreateBaseEquipmentTokens(
                playerZones,
                cardObjects,
                behavior,
                stackItem,
                events);
        }

        if (behavior.RecyclesSelectedMainDeckTargets)
        {
            var recycleResult = RecycleSelectedMainDeckTargets(
                state,
                playerZones,
                stackItem.ControllerId,
                stackItem.SourceObjectId,
                MainDeckTargetObjectIds(stackItem.TargetObjectIds, behavior),
                behavior.MainDeckLookCount);
            events.AddRange(recycleResult.Events);
            rngCursor = recycleResult.RngCursor;
        }

        var lethalCleanup = RunStateBasedCleanupLoop(
            playerZones,
            cardObjects,
            stackItem,
            runePools,
            damageTriggeredDestroyTargetObjectIds: damageTriggeredDestroyTargetObjectIds);
        runePools = lethalCleanup.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        events.AddRange(lethalCleanup.Events);
        destroyedObjectIds.AddRange(lethalCleanup.DestroyedObjectIds
            .Where(objectId => stackItem.TargetObjectIds.Contains(objectId, StringComparer.Ordinal)));
        destroyedUnitOwnerIds.AddRange(lethalCleanup.DestroyedUnitOwnerIds);

        var drawCount = drawCountOverride ?? ResolveDrawCount(playerZones, cardObjects, stackItem.ControllerId, behavior);
        if (ShouldDrawForBehavior(behavior, stackItem, destroyedObjectIds, drawCount))
        {
            foreach (var drawPlayerId in DrawRecipientPlayerIds(behavior, stackItem.ControllerId, targetControllerDrawRecipientIds))
            {
                var drawApplication = ApplyDrawToPlayer(
                    state,
                    playerZones,
                    playerScores,
                    drawPlayerId,
                    drawCount * stackItem.EffectRepeatCount,
                    rngCursor,
                    events);
                playerScores = drawApplication.PlayerScores;
                winnerPlayerId = drawApplication.WinnerPlayerId;
                rngCursor = drawApplication.RngCursor;

                if (winnerPlayerId is not null)
                {
                    break;
                }
            }
        }

        if (behavior.SchedulesExtraTurnForController)
        {
            extraTurnPlayerId = stackItem.ControllerId;
            events.Add(new GameEvent(
                "EXTRA_TURN_SCHEDULED",
                $"{behavior.DisplayName}安排额外回合",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["playerId"] = stackItem.ControllerId
                }));
        }

        events.AddRange(ResolveLeonaStunBoonTrigger(
            state,
            playerZones,
            cardObjects,
            stackItem,
            events));
        events.AddRange(ResolveSivirRuneRecycledGoldTrigger(
            playerZones,
            cardObjects,
            stackItem,
            events));
        events.AddRange(ResolveSivirEnemyDestroyedReadyTriggers(
            playerZones,
            cardObjects,
            destroyedUnitOwnerIds));
        var jhinTrigger = ResolveJhinHighCostSpellTrigger(
            state,
            playerZones,
            cardObjects,
            playerScores,
            stackItem,
            behavior,
            rngCursor);
        events.AddRange(jhinTrigger.Events);
        playerScores = jhinTrigger.PlayerScores;
        winnerPlayerId = jhinTrigger.WinnerPlayerId ?? winnerPlayerId;
        rngCursor = jhinTrigger.RngCursor;

        if (!behavior.PlaysSourceToBaseAsEquipment
            && !behavior.PlaysSourceToBaseAsUnit
            && !jhinTrigger.HandledSourceMovement
            && playerZones.TryGetValue(stackItem.ControllerId, out var controllerZones))
        {
            if (behavior.BanishesSourceOnResolution)
            {
                if (!controllerZones.Banished.Contains(stackItem.SourceObjectId, StringComparer.Ordinal))
                {
                    controllerZones = controllerZones with
                    {
                        Banished = controllerZones.Banished.Concat([stackItem.SourceObjectId]).ToArray()
                    };
                }
            }
            else if (!controllerZones.Graveyard.Contains(stackItem.SourceObjectId, StringComparer.Ordinal))
            {
                controllerZones = controllerZones with
                {
                    Graveyard = controllerZones.Graveyard.Concat([stackItem.SourceObjectId]).ToArray()
                };
            }

            playerZones[stackItem.ControllerId] = controllerZones;
        }

        return new StackResolutionResult(
            playerZones,
            cardObjects,
            playerScores,
            playerExperience,
            runePools,
            untilEndOfTurnEffects.ToArray(),
            winnerPlayerId,
            events,
            destroyedUnitOwnerIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ownerId => ownerId, StringComparer.Ordinal)
                .ToArray(),
            updatedStackItems,
            counteredStackItemIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(stackItemId => stackItemId, StringComparer.Ordinal)
                .ToArray(),
            extraTurnPlayerId,
            rngCursor);
    }

    private static StackResolutionResult ResolveViDoublePowerAbilityStackItem(
        MatchState state,
        StackItemState stackItem)
    {
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        if (cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceState))
        {
            var appliedPowerDelta = sourceState.Power;
            var nextSourceState = sourceState with
            {
                Power = sourceState.Power + appliedPowerDelta,
                UntilEndOfTurnPowerModifier = sourceState.UntilEndOfTurnPowerModifier + appliedPowerDelta
            };
            cardObjects[stackItem.SourceObjectId] = nextSourceState;
            events.Add(new GameEvent(
                "POWER_MODIFIED_UNTIL_END_OF_TURN",
                "蔚令自身本回合内战力翻倍",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = stackItem.SourceObjectId,
                    ["powerDelta"] = appliedPowerDelta,
                    ["appliedPowerDelta"] = appliedPowerDelta,
                    ["minimumPower"] = 0,
                    ["resultingPower"] = nextSourceState.Power
                }));
        }

        return new StackResolutionResult(
            NormalizeZonesForSeats(state),
            cardObjects,
            state.PlayerScores,
            NormalizeExperienceForSeats(state),
            state.RunePools,
            state.UntilEndOfTurnEffects,
            null,
            events,
            [],
            null,
            [],
            null,
            state.RngCursor);
    }

    private static StackResolutionResult ResolveXerathDamageAbilityStackItem(
        MatchState state,
        StackItemState stackItem)
    {
        var playerZones = NormalizeZonesForSeats(state);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var destroyedUnitOwnerIds = new List<string>();
        var damageTriggeredDestroyTargetObjectIds = new HashSet<string>(StringComparer.Ordinal);
        var runePools = state.RunePools;
        var targetObjectId = stackItem.TargetObjectIds.FirstOrDefault() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(targetObjectId)
            && IsFieldObject(playerZones, targetObjectId)
            && CardObjectHasTag(cardObjects, targetObjectId, CardObjectTags.UnitCard))
        {
            var damageAmount = stackItem.DamageAmount > 0
                ? stackItem.DamageAmount
                : P4ActivatedAbilityCatalog.XerathDamageAbilityDamageAmount;
            damageAmount = ApplyBattlefieldTargetSpellSkillDamageBonus(
                playerZones,
                cardObjects,
                targetObjectId,
                damageAmount);
            var preventDamage = state.UntilEndOfTurnEffects.Contains(
                PreventSpellAndSkillDamageThisTurnEffectId,
                StringComparer.Ordinal);
            var damageApplication = ApplyDamageToCardObject(
                cardObjects,
                targetObjectId,
                damageAmount,
                damageTriggeredDestroyTargetObjectIds,
                preventDamage,
                PreventSpellAndSkillDamageThisTurnEffectId);
            events.Add(new GameEvent(
                "DAMAGE_APPLIED",
                "泽拉斯的技能造成 3 点伤害",
                BuildDamagePayload(stackItem.SourceObjectId, targetObjectId, damageApplication)));

            var lethalCleanup = RunStateBasedCleanupLoop(
                playerZones,
                cardObjects,
                stackItem,
                state.RunePools,
                damageTriggeredDestroyTargetObjectIds: damageTriggeredDestroyTargetObjectIds);
            runePools = lethalCleanup.RunePools;
            events.AddRange(lethalCleanup.Events);
            destroyedUnitOwnerIds.AddRange(lethalCleanup.DestroyedUnitOwnerIds);
        }

        return new StackResolutionResult(
            playerZones,
            cardObjects,
            state.PlayerScores,
            NormalizeExperienceForSeats(state),
            runePools,
            state.UntilEndOfTurnEffects,
            null,
            events,
            destroyedUnitOwnerIds,
            null,
            [],
            null,
            state.RngCursor);
    }

    private static StackItemState[] RemoveCounteredStackItems(
        IReadOnlyList<StackItemState> stackItems,
        IReadOnlyList<string> counteredStackItemIds)
    {
        if (counteredStackItemIds.Count == 0)
        {
            return stackItems.ToArray();
        }

        return stackItems
            .Where(stackItem => !counteredStackItemIds.Contains(stackItem.StackItemId, StringComparer.Ordinal))
            .ToArray();
    }

    private static bool TryCounterStackItem(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        string targetStackItemId,
        StackItemState counteringStackItem,
        CardBehaviorDefinition counteringBehavior,
        out GameEvent counteredEvent)
    {
        counteredEvent = default!;
        var targetStackItem = state.StackItems.FirstOrDefault(stackItem =>
            string.Equals(stackItem.StackItemId, targetStackItemId, StringComparison.Ordinal));
        if (targetStackItem is null
            || !CardBehaviorRegistry.TryGetByEffectKind(targetStackItem.EffectKind, out var targetBehavior)
            || targetBehavior.PlaysSourceToBaseAsUnit
            || targetBehavior.PlaysSourceToBaseAsEquipment
            || !playerZones.TryGetValue(targetStackItem.ControllerId, out var targetControllerZones))
        {
            return false;
        }

        var destinationZone = string.Equals(
            counteringBehavior.CounteredStackItemDestination,
            CardCounteredStackItemDestinationZones.Hand,
            StringComparison.Ordinal)
            ? CardCounteredStackItemDestinationZones.Hand
            : CardCounteredStackItemDestinationZones.Graveyard;

        if (string.Equals(destinationZone, CardCounteredStackItemDestinationZones.Hand, StringComparison.Ordinal))
        {
            if (!targetControllerZones.Hand.Contains(targetStackItem.SourceObjectId, StringComparer.Ordinal))
            {
                playerZones[targetStackItem.ControllerId] = targetControllerZones with
                {
                    Hand = targetControllerZones.Hand.Concat([targetStackItem.SourceObjectId]).ToArray()
                };
            }
        }
        else if (!targetControllerZones.Graveyard.Contains(targetStackItem.SourceObjectId, StringComparer.Ordinal))
        {
            playerZones[targetStackItem.ControllerId] = targetControllerZones with
            {
                Graveyard = targetControllerZones.Graveyard.Concat([targetStackItem.SourceObjectId]).ToArray()
            };
        }

        counteredEvent = new GameEvent(
            "STACK_ITEM_COUNTERED",
            $"{counteringStackItem.SourceObjectId}无效化法术",
            new Dictionary<string, object?>
            {
                ["stackItemId"] = targetStackItem.StackItemId,
                ["counteredByStackItemId"] = counteringStackItem.StackItemId,
                ["sourceObjectId"] = targetStackItem.SourceObjectId,
                ["controllerId"] = targetStackItem.ControllerId,
                ["counteredByPlayerId"] = counteringStackItem.ControllerId,
                ["destinationZone"] = destinationZone
            });
        return true;
    }

    private static bool TryGainControlOfTargetStackSpell(
        IReadOnlyList<StackItemState> stackItems,
        CardBehaviorDefinition behavior,
        StackItemState gainingStackItem,
        out StackItemState[] updatedStackItems,
        out GameEvent controlEvent)
    {
        updatedStackItems = [];
        controlEvent = default!;
        if (gainingStackItem.TargetObjectIds.Count == 0)
        {
            return false;
        }

        var targetStackItemId = gainingStackItem.TargetObjectIds[0];
        var targetStackItem = stackItems.FirstOrDefault(stackItem =>
            string.Equals(stackItem.StackItemId, targetStackItemId, StringComparison.Ordinal));
        if (targetStackItem is null)
        {
            return false;
        }

        updatedStackItems = stackItems
            .Where(stackItem => !string.Equals(stackItem.StackItemId, gainingStackItem.StackItemId, StringComparison.Ordinal))
            .Select(stackItem => string.Equals(stackItem.StackItemId, targetStackItemId, StringComparison.Ordinal)
                ? stackItem with { ControllerId = gainingStackItem.ControllerId }
                : stackItem)
            .ToArray();

        controlEvent = new GameEvent(
            "STACK_ITEM_CONTROL_GAINED",
            $"{behavior.DisplayName}获得法术控制权",
            new Dictionary<string, object?>
            {
                ["stackItemId"] = targetStackItem.StackItemId,
                ["sourceObjectId"] = gainingStackItem.SourceObjectId,
                ["targetSourceObjectId"] = targetStackItem.SourceObjectId,
                ["previousControllerId"] = targetStackItem.ControllerId,
                ["controllerId"] = gainingStackItem.ControllerId
            });
        return true;
    }

    private static IEnumerable<string> SacredJudgmentFieldUnitIds(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        PlayerZones zones)
    {
        return zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId =>
                CardObjectHasTag(cardObjects, objectId, CardObjectTags.UnitCard)
                && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId));
    }

    private static IEnumerable<string> SacredJudgmentEquipmentIds(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        PlayerZones zones)
    {
        return zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId =>
                CardObjectHasTag(cardObjects, objectId, CardObjectTags.EquipmentCard)
                && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId));
    }

    private static IEnumerable<string> SacredJudgmentRuneIds(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        PlayerZones zones)
    {
        return zones.Base
            .Where(objectId =>
                CardObjectHasTag(cardObjects, objectId, CardObjectTags.RuneCard)
                && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId));
    }

    private static bool IsCardObjectControlledByPlayerOrLegacyOwned(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string objectId)
    {
        return !cardObjects.TryGetValue(objectId, out var cardObject)
            || SourceObjectControlledByPlayerOrLegacyOwned(cardObject, playerId);
    }

    private static void ApplyStatusEffectsToFieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        ICollection<GameEvent> events)
    {
        var statusEffectIds = ResolveStatusEffectIds(behavior);
        if (statusEffectIds.Length == 0)
        {
            return;
        }

        foreach (var targetObjectId in GetFieldUnitObjectIds(playerZones, cardObjects))
        {
            var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                ? existingTarget
                : new CardObjectState(targetObjectId);

            foreach (var statusEffectId in statusEffectIds)
            {
                targetState = targetState with
                {
                    UntilEndOfTurnEffects = targetState.UntilEndOfTurnEffects
                        .Concat([statusEffectId])
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(effectId => effectId, StringComparer.Ordinal)
                        .ToArray()
                };
                events.Add(new GameEvent(
                    "STATUS_EFFECT_APPLIED",
                    $"{behavior.DisplayName}施加{statusEffectId}",
                    new Dictionary<string, object?>
                    {
                        ["sourceObjectId"] = stackItem.SourceObjectId,
                        ["targetObjectId"] = targetObjectId,
                        ["effectId"] = statusEffectId
                    }));
            }

            cardObjects[targetObjectId] = targetState;
        }
    }

    private static IReadOnlyList<string> DrawRecipientPlayerIds(
        CardBehaviorDefinition behavior,
        string controllerId,
        IReadOnlyList<string> targetControllerPlayerIds)
    {
        return behavior.DrawRecipientKind switch
        {
            CardDrawRecipientKinds.TargetController => targetControllerPlayerIds
                .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(playerId => playerId, StringComparer.Ordinal)
                .ToArray(),
            _ => [controllerId]
        };
    }

    private static int ConditionalEquipmentTokenCountForDestroyedUnit(
        CardBehaviorDefinition behavior,
        string controllerId,
        FieldRemovalResult removalResult)
    {
        if (!removalResult.WasDestroyed || !removalResult.WasUnit)
        {
            return 0;
        }

        return string.Equals(removalResult.OwnerPlayerId, controllerId, StringComparison.Ordinal)
            ? behavior.CreatedBaseEquipmentTokenCountIfDestroyedFriendlyUnit
            : behavior.CreatedBaseEquipmentTokenCountIfDestroyedEnemyUnit;
    }

    private static IReadOnlyList<string> GetBattlefieldObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects)
    {
        return playerZones
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Where(objectId => IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetEnemyCombatObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string controllerId)
    {
        return playerZones
            .Where(entry => !string.Equals(entry.Key, controllerId, StringComparison.Ordinal))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields)
            .Where(objectId => IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, objectId))
            .Where(objectId => cardObjects.TryGetValue(objectId, out var objectState)
                && (objectState.IsAttacking || objectState.IsDefending))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetEnemyBattlefieldObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string controllerId)
    {
        return playerZones
            .Where(entry => !string.Equals(entry.Key, controllerId, StringComparison.Ordinal))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Where(objectId => IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetFieldUnitObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects)
    {
        return playerZones
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Where(objectId => IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetFieldObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects)
    {
        return playerZones
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Where(objectId => IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetFieldEquipmentObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects)
    {
        return playerZones
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Where(objectId => IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, objectId))
            .Where(objectId => CardObjectHasTag(cardObjects, objectId, CardObjectTags.EquipmentCard))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetControlledFieldUnitObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Where(objectId => IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId))
            .Where(objectId => !CardObjectHasTag(cardObjects, objectId, CardObjectTags.EquipmentCard))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static int CountControlledFieldUnitObjects(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return 0;
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .Count(objectId => IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId)
                && CardObjectHasTag(cardObjects, objectId, CardObjectTags.UnitCard));
    }

    private static IReadOnlyList<string> GetEnemyFieldUnitObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        return playerZones
            .Where(entry => !string.Equals(entry.Key, playerId, StringComparison.Ordinal))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Base.Concat(entry.Value.Battlefields))
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Where(objectId => IsFieldObjectControlledByZonePlayer(playerZones, cardObjects, objectId))
            .Where(objectId => !CardObjectHasTag(cardObjects, objectId, CardObjectTags.EquipmentCard))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> GetControlledBattlefieldUnitObjectIds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return [];
        }

        return zones.Battlefields
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Where(objectId => IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsFieldObject(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId)
    {
        return !string.IsNullOrWhiteSpace(objectId)
            && playerZones.Values.Any(zones =>
                zones.Base.Contains(objectId, StringComparer.Ordinal)
                || zones.Battlefields.Contains(objectId, StringComparer.Ordinal));
    }

    private static bool IsFieldObjectControlledByZonePlayer(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string objectId)
    {
        var location = FindFieldObjectLocation(playerZones, objectId);
        return location is not null
            && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, location.Value.PlayerId, objectId);
    }

    private static bool IsControlledFieldUnit(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string objectId)
    {
        var location = FindFieldObjectLocation(playerZones, objectId);
        return location is not null
            && string.Equals(location.Value.PlayerId, playerId, StringComparison.Ordinal)
            && cardObjects.TryGetValue(objectId, out var objectState)
            && objectState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !objectState.IsFaceDown
            && SourceObjectControlledByPlayerOrLegacyOwned(objectState, playerId);
    }

    private static bool IsEnemyFieldUnit(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string objectId)
    {
        var location = FindFieldObjectLocation(playerZones, objectId);
        return location is not null
            && !string.Equals(location.Value.PlayerId, playerId, StringComparison.Ordinal)
            && cardObjects.TryGetValue(objectId, out var objectState)
            && objectState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !objectState.IsFaceDown
            && SourceObjectControlledByPlayerOrLegacyOwned(objectState, location.Value.PlayerId);
    }

    private static bool TryGetFieldControllerId(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId,
        out string controllerId)
    {
        foreach (var (playerId, zones) in playerZones)
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal)
                || zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                controllerId = playerId;
                return true;
            }
        }

        controllerId = string.Empty;
        return false;
    }

    private static bool TryAttachOrDetachSecondTargetEquipmentToFirstTarget(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        out GameEvent attachmentEvent)
    {
        attachmentEvent = default!;
        if (stackItem.TargetObjectIds.Count < 2)
        {
            return false;
        }

        var unitObjectId = stackItem.TargetObjectIds[0];
        var equipmentObjectId = stackItem.TargetObjectIds[1];
        if (!IsFieldObject(playerZones, unitObjectId)
            || !IsFieldObject(playerZones, equipmentObjectId)
            || !cardObjects.TryGetValue(unitObjectId, out var unitState)
            || !cardObjects.TryGetValue(equipmentObjectId, out var equipmentState)
            || !equipmentState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
            || !equipmentState.Tags.Contains("武装", StringComparer.Ordinal)
            || !TryGetFieldControllerId(playerZones, unitObjectId, out var unitControllerId)
            || !TryGetFieldControllerId(playerZones, equipmentObjectId, out var equipmentControllerId)
            || !string.Equals(unitControllerId, equipmentControllerId, StringComparison.Ordinal)
            || !FieldIdentityMatchesZone(unitState, unitControllerId)
            || !FieldIdentityMatchesZone(equipmentState, equipmentControllerId))
        {
            return false;
        }

        if (string.Equals(equipmentState.AttachedToObjectId, unitObjectId, StringComparison.Ordinal))
        {
            cardObjects[equipmentObjectId] = equipmentState with
            {
                AttachedToObjectId = null
            };
            attachmentEvent = new GameEvent(
                "EQUIPMENT_DETACHED",
                $"{behavior.DisplayName}卸除武装",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["unitObjectId"] = unitObjectId,
                    ["equipmentObjectId"] = equipmentObjectId,
                    ["controllerId"] = unitControllerId,
                    ["ownerId"] = string.IsNullOrWhiteSpace(equipmentState.OwnerId) ? unitControllerId : equipmentState.OwnerId
                });
            return true;
        }

        if (!string.IsNullOrWhiteSpace(equipmentState.AttachedToObjectId))
        {
            return false;
        }

        cardObjects[equipmentObjectId] = equipmentState with
        {
            AttachedToObjectId = unitObjectId
        };
        attachmentEvent = new GameEvent(
            "EQUIPMENT_ATTACHED",
            $"{behavior.DisplayName}贴附武装",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["unitObjectId"] = unitObjectId,
                ["equipmentObjectId"] = equipmentObjectId,
                ["controllerId"] = unitControllerId,
                ["ownerId"] = string.IsNullOrWhiteSpace(equipmentState.OwnerId) ? unitControllerId : equipmentState.OwnerId,
                ["attachedToObjectId"] = unitObjectId
            });
        return true;
    }

    private static void ApplyDamageToBattlefieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        int damageAmount,
        List<GameEvent> events,
        ISet<string> damageTriggeredDestroyTargetObjectIds,
        bool preventDamage = false,
        string preventionEffectId = "")
    {
        if (damageAmount <= 0)
        {
            return;
        }

        foreach (var targetObjectId in GetBattlefieldObjectIds(playerZones, cardObjects))
        {
            var damageApplication = ApplyDamageToCardObject(
                cardObjects,
                targetObjectId,
                damageAmount,
                damageTriggeredDestroyTargetObjectIds,
                preventDamage,
                preventionEffectId);
            events.Add(new GameEvent(
                "DAMAGE_APPLIED",
                $"{behavior.DisplayName}造成 {damageApplication.DamageAmount} 点伤害",
                BuildDamagePayload(stackItem.SourceObjectId, targetObjectId, damageApplication)));
        }
    }

    private static void ApplyBattlefieldPowerModifiers(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        for (var repeatIndex = 0; repeatIndex < stackItem.EffectRepeatCount; repeatIndex++)
        {
            if (behavior.ModifiesAllFriendlyBattlefieldUnits
                && behavior.PowerModifierAmount != 0)
            {
                foreach (var targetObjectId in GetControlledBattlefieldUnitObjectIds(playerZones, cardObjects, stackItem.ControllerId))
                {
                    if (!cardObjects.TryGetValue(targetObjectId, out var targetState))
                    {
                        continue;
                    }

                    cardObjects[targetObjectId] = ApplyPowerModifier(
                        targetState,
                        behavior,
                        stackItem,
                        targetObjectId,
                        behavior.PowerModifierAmount,
                        out var powerEvent);
                    events.Add(powerEvent);
                }
            }

            var enemyPowerModifierAmount = behavior.SecondaryPowerModifierAmount != 0
                ? behavior.SecondaryPowerModifierAmount
                : behavior.PowerModifierAmount;
            if (behavior.ModifiesAllEnemyBattlefieldUnits
                && enemyPowerModifierAmount != 0)
            {
                foreach (var targetObjectId in GetEnemyBattlefieldObjectIds(playerZones, cardObjects, stackItem.ControllerId))
                {
                    if (!cardObjects.TryGetValue(targetObjectId, out var targetState))
                    {
                        continue;
                    }

                    cardObjects[targetObjectId] = ApplyPowerModifier(
                        targetState,
                        behavior,
                        stackItem,
                        targetObjectId,
                        enemyPowerModifierAmount,
                        out var powerEvent);
                    events.Add(powerEvent);
                }
            }
        }
    }

    private static void ApplyDamageToEnemyCombatUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        int damageAmount,
        List<GameEvent> events,
        ISet<string> damageTriggeredDestroyTargetObjectIds,
        bool preventDamage = false,
        string preventionEffectId = "")
    {
        if (damageAmount <= 0)
        {
            return;
        }

        foreach (var targetObjectId in GetEnemyCombatObjectIds(playerZones, cardObjects, stackItem.ControllerId))
        {
            var damageApplication = ApplyDamageToCardObject(
                cardObjects,
                targetObjectId,
                damageAmount,
                damageTriggeredDestroyTargetObjectIds,
                preventDamage,
                preventionEffectId);
            events.Add(new GameEvent(
                "DAMAGE_APPLIED",
                $"{behavior.DisplayName}造成 {damageApplication.DamageAmount} 点伤害",
                BuildDamagePayload(stackItem.SourceObjectId, targetObjectId, damageApplication)));
        }
    }

    private static void ApplyDamageToEnemyBattlefieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        int damageAmount,
        List<GameEvent> events,
        ISet<string> damageTriggeredDestroyTargetObjectIds,
        bool preventDamage = false,
        string preventionEffectId = "")
    {
        if (damageAmount <= 0)
        {
            return;
        }

        foreach (var targetObjectId in GetEnemyBattlefieldObjectIds(playerZones, cardObjects, stackItem.ControllerId))
        {
            var damageApplication = ApplyDamageToCardObject(
                cardObjects,
                targetObjectId,
                damageAmount,
                damageTriggeredDestroyTargetObjectIds,
                preventDamage,
                preventionEffectId);
            events.Add(new GameEvent(
                "DAMAGE_APPLIED",
                $"{behavior.DisplayName}造成 {damageApplication.DamageAmount} 点伤害",
                BuildDamagePayload(stackItem.SourceObjectId, targetObjectId, damageApplication)));
        }
    }

    private static void ApplyDamageToOtherEnemyBattlefieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        int damageAmount,
        IReadOnlyCollection<string> excludedObjectIds,
        List<GameEvent> events,
        ISet<string> damageTriggeredDestroyTargetObjectIds,
        bool preventDamage = false,
        string preventionEffectId = "")
    {
        if (damageAmount <= 0)
        {
            return;
        }

        foreach (var targetObjectId in GetEnemyBattlefieldObjectIds(playerZones, cardObjects, stackItem.ControllerId))
        {
            if (excludedObjectIds.Contains(targetObjectId, StringComparer.Ordinal))
            {
                continue;
            }

            var damageApplication = ApplyDamageToCardObject(
                cardObjects,
                targetObjectId,
                damageAmount,
                damageTriggeredDestroyTargetObjectIds,
                preventDamage,
                preventionEffectId);
            events.Add(new GameEvent(
                "DAMAGE_APPLIED",
                $"{behavior.DisplayName}造成 {damageApplication.DamageAmount} 点溅射伤害",
                BuildDamagePayload(stackItem.SourceObjectId, targetObjectId, damageApplication)));
        }
    }

    private static DamageApplicationResult ApplyDamageToCardObject(
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        int damageAmount,
        ISet<string>? damageTriggeredDestroyTargetObjectIds = null,
        bool preventDamage = false,
        string preventionEffectId = "")
    {
        if (damageAmount <= 0)
        {
            return new DamageApplicationResult(0, 0, false, string.Empty);
        }

        var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
            ? existingTarget
            : new CardObjectState(targetObjectId);
        var preventsWithTargetEffect = !preventDamage
            && targetState.UntilEndOfTurnEffects.Contains(
                PreventNextDamageThisTurnEffectId,
                StringComparer.Ordinal);
        var preventsDamage = preventDamage || preventsWithTargetEffect;
        var adjustedDamageAmount = preventsDamage
            ? 0
            : targetState.UntilEndOfTurnEffects.Contains(DamageReceivedDoubledThisTurnEffectId, StringComparer.Ordinal)
                ? damageAmount * 2
                : damageAmount;
        var triggersDestroyOnDamage = adjustedDamageAmount > 0
            && targetState.UntilEndOfTurnEffects.Contains(
                DestroyOnNextDamageThisTurnEffectId,
                StringComparer.Ordinal);
        var nextEffects = targetState.UntilEndOfTurnEffects
            .Where(effectId => !preventsWithTargetEffect
                || !string.Equals(effectId, PreventNextDamageThisTurnEffectId, StringComparison.Ordinal))
            .Where(effectId => !triggersDestroyOnDamage
                || !string.Equals(effectId, DestroyOnNextDamageThisTurnEffectId, StringComparison.Ordinal))
            .ToArray();

        cardObjects[targetObjectId] = targetState with
        {
            Damage = targetState.Damage + adjustedDamageAmount,
            UntilEndOfTurnEffects = nextEffects
        };
        if (triggersDestroyOnDamage)
        {
            damageTriggeredDestroyTargetObjectIds?.Add(targetObjectId);
        }

        return new DamageApplicationResult(
            adjustedDamageAmount,
            damageAmount,
            preventsDamage,
            preventsWithTargetEffect
                ? PreventNextDamageThisTurnEffectId
                : preventDamage
                    ? preventionEffectId
                    : string.Empty);
    }

    private static Dictionary<string, object?> BuildDamagePayload(
        string sourceObjectId,
        string targetObjectId,
        DamageApplicationResult damageApplication,
        string damageSourceObjectId = "")
    {
        var payload = new Dictionary<string, object?>
        {
            ["sourceObjectId"] = sourceObjectId,
            ["targetObjectId"] = targetObjectId,
            ["damage"] = damageApplication.DamageAmount
        };

        if (!string.IsNullOrWhiteSpace(damageSourceObjectId))
        {
            payload["damageSourceObjectId"] = damageSourceObjectId;
        }

        if (damageApplication.Prevented)
        {
            payload["preventedDamage"] = damageApplication.OriginalDamageAmount;
            payload["preventionEffectId"] = damageApplication.PreventionEffectId;
        }

        return payload;
    }

    private static void CreateBaseUnitTokens(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var copiedTargetObjectId = behavior.CreatedBaseUnitTokenCopiesFirstTarget
            ? stackItem.TargetObjectIds.FirstOrDefault() ?? string.Empty
            : string.Empty;
        CardObjectState? copiedTargetState = null;
        var copiesTarget = !string.IsNullOrWhiteSpace(copiedTargetObjectId)
            && cardObjects.TryGetValue(copiedTargetObjectId, out copiedTargetState);
        if (behavior.CreatedBaseUnitTokenCopiesFirstTarget
            && !copiesTarget)
        {
            return;
        }

        var tokenPower = copiedTargetState is not null
            ? copiedTargetState.Power
            : behavior.CreatedBaseUnitTokenPower;
        if (!behavior.CreatedBaseUnitTokenCopiesFirstTarget
            && tokenPower <= 0)
        {
            return;
        }

        var tokenCount = behavior.CreatedBaseUnitTokenCount * Math.Max(1, stackItem.EffectRepeatCount);
        var tokenTags = copiedTargetState is not null
            ? copiedTargetState.Tags
                .Concat(ParseDelimitedValues(behavior.CreatedBaseUnitTokenTags))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
            : ParseDelimitedValues(behavior.CreatedBaseUnitTokenTags);
        tokenTags = ApplyAzirSandSoldierTemperedTags(
            playerZones,
            cardObjects,
            stackItem.ControllerId,
            tokenTags);
        var createdTokenObjectIds = new List<string>();
        for (var tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
        {
            var tokenObjectId = NextTokenObjectId(
                playerZones,
                cardObjects,
                stackItem.SourceObjectId,
                tokenIndex + 1);
            createdTokenObjectIds.Add(tokenObjectId);
            cardObjects[tokenObjectId] = new CardObjectState(
                tokenObjectId,
                power: tokenPower,
                tags: tokenTags);
            var payload = new Dictionary<string, object?>
            {
                ["playerId"] = stackItem.ControllerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["tokenObjectId"] = tokenObjectId,
                ["tokenName"] = behavior.CreatedBaseUnitTokenName,
                ["power"] = tokenPower,
                ["destinationZone"] = "BASE"
            };
            if (copiesTarget)
            {
                payload["copiedTargetObjectId"] = copiedTargetObjectId;
            }

            if (tokenTags.Count > 0)
            {
                payload["tokenTags"] = tokenTags;
            }

            events.Add(new GameEvent(
                "UNIT_TOKEN_CREATED",
                $"{behavior.DisplayName}打出单位指示物到基地",
                payload));
        }

        playerZones[stackItem.ControllerId] = zones with
        {
            Base = zones.Base.Concat(createdTokenObjectIds).ToArray()
        };
    }

    private static void CreateBaseEquipmentTokens(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events,
        int tokenCountOverride = 0)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var baseTokenCount = tokenCountOverride > 0
            ? tokenCountOverride
            : behavior.CreatedBaseEquipmentTokenCount;
        var tokenCount = baseTokenCount * Math.Max(1, stackItem.EffectRepeatCount);
        var tokenTags = ParseDelimitedValues(behavior.CreatedBaseEquipmentTokenTags);
        var createdTokenObjectIds = new List<string>();
        for (var tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
        {
            var tokenObjectId = NextTokenObjectId(
                playerZones,
                cardObjects,
                stackItem.SourceObjectId,
                tokenIndex + 1);
            createdTokenObjectIds.Add(tokenObjectId);
            cardObjects[tokenObjectId] = new CardObjectState(
                tokenObjectId,
                isExhausted: behavior.CreatedBaseEquipmentTokenIsExhausted,
                tags: tokenTags);
            var payload = new Dictionary<string, object?>
            {
                ["playerId"] = stackItem.ControllerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["tokenObjectId"] = tokenObjectId,
                ["tokenName"] = behavior.CreatedBaseEquipmentTokenName,
                ["destinationZone"] = "BASE",
                ["isExhausted"] = behavior.CreatedBaseEquipmentTokenIsExhausted
            };
            if (tokenTags.Count > 0)
            {
                payload["tokenTags"] = tokenTags;
            }

            events.Add(new GameEvent(
                "EQUIPMENT_TOKEN_CREATED",
                $"{behavior.DisplayName}打出装备指示物到基地",
                payload));
        }

        playerZones[stackItem.ControllerId] = zones with
        {
            Base = zones.Base.Concat(createdTokenObjectIds).ToArray()
        };
    }

    private static void PlaySourceEquipmentToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var existingState = cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceState)
            ? sourceState
            : new CardObjectState(stackItem.SourceObjectId);
        var equipmentState = existingState with
        {
            IsExhausted = existingState.IsExhausted || behavior.SourceEquipmentIsExhausted,
            CardNo = string.IsNullOrWhiteSpace(existingState.CardNo) ? behavior.CardNo : existingState.CardNo,
            Tags = existingState.Tags
                .Concat([CardObjectTags.EquipmentCard])
                .Concat(ParseDelimitedValues(behavior.SourceEquipmentTags))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
        };
        cardObjects[stackItem.SourceObjectId] = equipmentState;

        playerZones[stackItem.ControllerId] = zones with
        {
            Base = zones.Base.Contains(stackItem.SourceObjectId, StringComparer.Ordinal)
                ? zones.Base
                : zones.Base.Concat([stackItem.SourceObjectId]).ToArray()
        };

        events.Add(new GameEvent(
            "EQUIPMENT_PLAYED_TO_BASE",
            $"{behavior.DisplayName}打出装备到基地",
            CreateEquipmentPlayedPayload(
                stackItem,
                behavior,
                equipmentState)));
    }

    private static Dictionary<string, object?> CreateEquipmentPlayedPayload(
        StackItemState stackItem,
        CardBehaviorDefinition behavior,
        CardObjectState equipmentState)
    {
        var payload = new Dictionary<string, object?>
            {
                ["playerId"] = stackItem.ControllerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["equipmentObjectId"] = stackItem.SourceObjectId,
                ["equipmentName"] = behavior.DisplayName,
                ["destinationZone"] = "BASE",
                ["tags"] = equipmentState.Tags.ToArray()
            };
        if (equipmentState.IsExhausted)
        {
            payload["isExhausted"] = true;
        }

        return payload;
    }

    private static void PlaySourceUnitToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        IReadOnlyDictionary<string, int> playerExperience,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var existingState = cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceState)
            ? sourceState
            : new CardObjectState(stackItem.SourceObjectId);
        var baseUnitPower = behavior.SourceUnitPower > 0
            ? behavior.SourceUnitPower
            : existingState.Power;
        var unitPower = behavior.AddsControllerGraveyardCountToSourceUnitPower
            ? baseUnitPower + zones.Graveyard.Count
            : baseUnitPower;
        var levelApplies = ControllerMeetsLevelExperienceThreshold(
            behavior,
            stackItem.ControllerId,
            playerExperience);
        var entersActiveFromMasterYiLevel = ControllerHasMasterYiLevelLegend(
            playerZones,
            cardObjects,
            stackItem.ControllerId,
            playerExperience,
            MasterYiLevelReadyThreshold);
        if (levelApplies)
        {
            unitPower += behavior.LevelSourceUnitPowerBonus;
        }

        var hasteReadyOptionalCostPaid = CardPermissionKeywordRules.IsHasteReadyOptionalCostPaid(
            behavior,
            stackItem.OptionalCosts);
        var unitState = existingState with
        {
            Power = unitPower,
            IsExhausted = entersActiveFromMasterYiLevel
                ? false
                : existingState.IsExhausted || (behavior.SourceUnitIsExhausted && !hasteReadyOptionalCostPaid),
            CardNo = string.IsNullOrWhiteSpace(existingState.CardNo) ? behavior.CardNo : existingState.CardNo,
            Tags = existingState.Tags
                .Concat([CardObjectTags.UnitCard])
                .Concat(ParseDelimitedValues(behavior.SourceUnitTags))
                .Concat(levelApplies ? ParseDelimitedValues(behavior.LevelSourceUnitTags) : [])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
        };
        cardObjects[stackItem.SourceObjectId] = unitState;

        playerZones[stackItem.ControllerId] = zones with
        {
            Base = zones.Base.Contains(stackItem.SourceObjectId, StringComparer.Ordinal)
                ? zones.Base
                : zones.Base.Concat([stackItem.SourceObjectId]).ToArray()
        };

        events.Add(new GameEvent(
            "UNIT_PLAYED_TO_BASE",
            $"{behavior.DisplayName}打出单位到基地",
            CreateUnitPlayedPayload(
                stackItem,
                behavior,
                unitState,
            hasteReadyOptionalCostPaid)));
    }

    private static void PlaySourceUnitToBattlefield(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        IReadOnlyDictionary<string, int> playerExperience,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var existingState = cardObjects.TryGetValue(stackItem.SourceObjectId, out var sourceState)
            ? sourceState
            : new CardObjectState(stackItem.SourceObjectId);
        var baseUnitPower = behavior.SourceUnitPower > 0
            ? behavior.SourceUnitPower
            : existingState.Power;
        var unitPower = behavior.AddsControllerGraveyardCountToSourceUnitPower
            ? baseUnitPower + zones.Graveyard.Count
            : baseUnitPower;
        var levelApplies = ControllerMeetsLevelExperienceThreshold(
            behavior,
            stackItem.ControllerId,
            playerExperience);
        var entersActiveFromMasterYiLevel = ControllerHasMasterYiLevelLegend(
            playerZones,
            cardObjects,
            stackItem.ControllerId,
            playerExperience,
            MasterYiLevelReadyThreshold);
        if (levelApplies)
        {
            unitPower += behavior.LevelSourceUnitPowerBonus;
        }

        var unitState = existingState with
        {
            Power = unitPower,
            IsExhausted = entersActiveFromMasterYiLevel
                ? false
                : existingState.IsExhausted || behavior.SourceUnitIsExhausted,
            CardNo = string.IsNullOrWhiteSpace(existingState.CardNo) ? behavior.CardNo : existingState.CardNo,
            Tags = existingState.Tags
                .Concat([CardObjectTags.UnitCard])
                .Concat(ParseDelimitedValues(behavior.SourceUnitTags))
                .Concat(levelApplies ? ParseDelimitedValues(behavior.LevelSourceUnitTags) : [])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
        };
        cardObjects[stackItem.SourceObjectId] = unitState;

        playerZones[stackItem.ControllerId] = zones with
        {
            Battlefields = zones.Battlefields.Contains(stackItem.SourceObjectId, StringComparer.Ordinal)
                ? zones.Battlefields
                : zones.Battlefields.Concat([stackItem.SourceObjectId]).ToArray()
        };

        events.Add(new GameEvent(
            "UNIT_PLAYED_TO_BATTLEFIELD",
            $"{behavior.DisplayName}打出单位到战场",
            CreateUnitPlayedToBattlefieldPayload(
                stackItem,
                behavior,
                unitState)));
    }

    private static bool ControllerMeetsLevelExperienceThreshold(
        CardBehaviorDefinition behavior,
        string playerId,
        IReadOnlyDictionary<string, int> playerExperience)
    {
        return behavior.LevelExperienceThreshold > 0
            && playerExperience.TryGetValue(playerId, out var experience)
            && experience >= behavior.LevelExperienceThreshold;
    }

    private static Dictionary<string, object?> CreateUnitPlayedPayload(
        StackItemState stackItem,
        CardBehaviorDefinition behavior,
        CardObjectState unitState,
        bool hasteReadyOptionalCostPaid)
    {
        var payload = new Dictionary<string, object?>
        {
            ["playerId"] = stackItem.ControllerId,
            ["sourceObjectId"] = stackItem.SourceObjectId,
            ["unitObjectId"] = stackItem.SourceObjectId,
            ["unitName"] = behavior.DisplayName,
            ["destinationZone"] = "BASE",
            ["power"] = unitState.Power,
            ["tags"] = unitState.Tags.ToArray()
        };
        if (unitState.IsExhausted)
        {
            payload["isExhausted"] = true;
        }

        if (hasteReadyOptionalCostPaid)
        {
            payload["isExhausted"] = unitState.IsExhausted;
            payload["hasteReadyOptionalCostPaid"] = true;
        }

        return payload;
    }

    private static Dictionary<string, object?> CreateUnitPlayedToBattlefieldPayload(
        StackItemState stackItem,
        CardBehaviorDefinition behavior,
        CardObjectState unitState)
    {
        var payload = new Dictionary<string, object?>
        {
            ["playerId"] = stackItem.ControllerId,
            ["sourceObjectId"] = stackItem.SourceObjectId,
            ["unitObjectId"] = stackItem.SourceObjectId,
            ["unitName"] = behavior.DisplayName,
            ["destinationZone"] = "BATTLEFIELD",
            ["destination"] = stackItem.Destination,
            ["power"] = unitState.Power,
            ["tags"] = unitState.Tags.ToArray()
        };
        if (unitState.IsExhausted)
        {
            payload["isExhausted"] = true;
        }

        return payload;
    }

    private static void BanishFriendlyGraveyardUnits(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var banishedCardIds = zones.Graveyard
            .Where(cardId =>
                CardObjectHasTag(cardObjects, cardId, CardObjectTags.UnitCard)
                && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, stackItem.ControllerId, cardId))
            .ToArray();
        if (banishedCardIds.Length == 0)
        {
            return;
        }

        var banishedCardIdSet = banishedCardIds.ToHashSet(StringComparer.Ordinal);
        playerZones[stackItem.ControllerId] = zones with
        {
            Graveyard = zones.Graveyard
                .Where(cardId => !banishedCardIdSet.Contains(cardId))
                .ToArray(),
            Banished = zones.Banished
                .Concat(banishedCardIds.Where(cardId => !zones.Banished.Contains(cardId, StringComparer.Ordinal)))
                .ToArray()
        };

        events.Add(new GameEvent(
            "CARDS_BANISHED",
            $"{behavior.DisplayName}放逐废牌堆单位牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = stackItem.ControllerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["cardIds"] = banishedCardIds,
                ["count"] = banishedCardIds.Length,
                ["fromZone"] = "GRAVEYARD",
                ["destinationZone"] = "BANISHED"
            }));
    }

    private static IReadOnlyList<string> ParseDelimitedValues(string values)
    {
        if (string.IsNullOrWhiteSpace(values))
        {
            return [];
        }

        return values
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NextTokenObjectId(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string sourceObjectId,
        int initialTokenNumber)
    {
        var tokenNumber = Math.Max(1, initialTokenNumber);
        while (true)
        {
            var candidate = $"{sourceObjectId}-TOKEN-{tokenNumber:000}";
            if (!cardObjects.ContainsKey(candidate)
                && !IsObjectIdInAnyZone(playerZones, candidate))
            {
                return candidate;
            }

            tokenNumber++;
        }
    }

    private static bool IsObjectIdInAnyZone(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId)
    {
        return playerZones.Values.Any(zones =>
            zones.MainDeck.Contains(objectId, StringComparer.Ordinal)
            || zones.RuneDeck.Contains(objectId, StringComparer.Ordinal)
            || zones.Hand.Contains(objectId, StringComparer.Ordinal)
            || zones.Base.Contains(objectId, StringComparer.Ordinal)
            || zones.Battlefields.Contains(objectId, StringComparer.Ordinal)
            || zones.Graveyard.Contains(objectId, StringComparer.Ordinal)
            || zones.Banished.Contains(objectId, StringComparer.Ordinal)
            || zones.LegendZone.Contains(objectId, StringComparer.Ordinal)
            || zones.ChampionZone.Contains(objectId, StringComparer.Ordinal));
    }

    private static CardObjectState ApplyPowerModifier(
        CardObjectState targetState,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        string targetObjectId,
        int powerModifierAmount,
        out GameEvent powerEvent)
    {
        var previousPower = targetState.Power;
        var resultingPower = Math.Max(
            behavior.MinimumPowerAfterModifier,
            previousPower + powerModifierAmount);
        var appliedPowerDelta = resultingPower - previousPower;
        var nextTargetState = targetState with
        {
            Power = resultingPower,
            UntilEndOfTurnPowerModifier = targetState.UntilEndOfTurnPowerModifier
                + appliedPowerDelta
        };
        powerEvent = new GameEvent(
            "POWER_MODIFIED_UNTIL_END_OF_TURN",
            $"{behavior.DisplayName}临时修正战力",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["targetObjectId"] = targetObjectId,
                ["powerDelta"] = powerModifierAmount,
                ["appliedPowerDelta"] = appliedPowerDelta,
                ["minimumPower"] = behavior.MinimumPowerAfterModifier,
                ["resultingPower"] = nextTargetState.Power
            });

        return nextTargetState;
    }

    private static CardObjectState ApplyBoon(
        CardObjectState targetState,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        string targetObjectId,
        out IReadOnlyList<GameEvent> boonEvents)
    {
        if (targetState.Tags.Contains(CardObjectTags.Boon, StringComparer.Ordinal))
        {
            boonEvents = [];
            return targetState;
        }

        var nextTargetState = targetState with
        {
            Power = targetState.Power + 1,
            Tags = targetState.Tags
                .Concat([CardObjectTags.Boon])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
        };
        boonEvents =
        [
            new GameEvent(
                "OBJECT_TAG_ADDED",
                $"{behavior.DisplayName}给予增益标签",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = targetObjectId,
                    ["tag"] = CardObjectTags.Boon
                }),
            new GameEvent(
                "BOON_GRANTED",
                $"{behavior.DisplayName}给予增益",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = stackItem.SourceObjectId,
                    ["targetObjectId"] = targetObjectId,
                    ["powerDelta"] = 1,
                    ["resultingPower"] = nextTargetState.Power
                })
        ];

        return nextTargetState;
    }

    private static CardObjectState ApplyTargetTag(
        CardObjectState targetState,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        string targetObjectId,
        out GameEvent tagEvent)
    {
        var nextTargetState = targetState with
        {
            Tags = targetState.Tags
                .Concat([behavior.TargetAddedTag])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray()
        };
        tagEvent = new GameEvent(
            "OBJECT_TAG_ADDED",
            $"{behavior.DisplayName}给予对象标签",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["targetObjectId"] = targetObjectId,
                ["tag"] = behavior.TargetAddedTag
            });

        return nextTargetState;
    }

    private static CardObjectState ApplyReadyState(
        CardObjectState targetState,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        string targetObjectId,
        out GameEvent readyEvent)
    {
        var wasExhausted = targetState.IsExhausted;
        var nextTargetState = targetState with
        {
            IsExhausted = false
        };
        readyEvent = new GameEvent(
            "UNIT_READIED",
            $"{behavior.DisplayName}让单位变为活跃状态",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["targetObjectId"] = targetObjectId,
                ["wasExhausted"] = wasExhausted,
                ["isExhausted"] = nextTargetState.IsExhausted
            });

        return nextTargetState;
    }

    private static CardObjectState ApplyExhaustState(
        CardObjectState targetState,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        string targetObjectId,
        out GameEvent exhaustedEvent)
    {
        var wasExhausted = targetState.IsExhausted;
        var nextTargetState = targetState with
        {
            IsExhausted = true
        };
        exhaustedEvent = new GameEvent(
            "UNIT_EXHAUSTED",
            $"{behavior.DisplayName}让单位变为休眠状态",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["targetObjectId"] = targetObjectId,
                ["wasExhausted"] = wasExhausted,
                ["isExhausted"] = nextTargetState.IsExhausted
            });

        return nextTargetState;
    }

    private static int ResolvePowerModifierAmount(
        CardBehaviorDefinition behavior,
        int targetIndex,
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string targetObjectId)
    {
        if (behavior.UsesTargetCurrentPowerAsPowerModifier
            && cardObjects.TryGetValue(targetObjectId, out var targetState))
        {
            return targetState.Power;
        }

        if (behavior.UsesEnemyBattlefieldUnitCountAsPowerModifierMultiplier)
        {
            return behavior.PowerModifierAmount * CountEnemyBattlefieldUnits(playerZones, cardObjects, controllerId);
        }

        if (!string.IsNullOrWhiteSpace(behavior.PowerModifierControlledUnitTags))
        {
            return CountControlledUnitTagKinds(
                playerZones,
                cardObjects,
                controllerId,
                behavior.PowerModifierControlledUnitTags);
        }

        return targetIndex == 1 && behavior.SecondaryPowerModifierAmount != 0
            ? behavior.SecondaryPowerModifierAmount
            : behavior.PowerModifierAmount;
    }

    private static int CountEnemyBattlefieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId)
    {
        return playerZones
            .Where(entry => !string.Equals(entry.Key, playerId, StringComparison.Ordinal))
            .SelectMany(entry => entry.Value.Battlefields)
            .Count(objectId => cardObjects.ContainsKey(objectId));
    }

    private static int CountControlledUnitTagKinds(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string delimitedTags)
    {
        var trackedTags = delimitedTags
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        if (trackedTags.Count == 0)
        {
            return 0;
        }

        var presentTags = new HashSet<string>(StringComparer.Ordinal);
        foreach (var objectId in GetControlledFieldUnitObjectIds(playerZones, cardObjects, playerId))
        {
            if (!cardObjects.TryGetValue(objectId, out var cardObject))
            {
                continue;
            }

            foreach (var tag in cardObject.Tags)
            {
                if (trackedTags.Contains(tag))
                {
                    presentTags.Add(tag);
                }
            }
        }

        return presentTags.Count;
    }

    private static string[] ResolveStatusEffectIds(CardBehaviorDefinition behavior)
    {
        return new[] { behavior.StatusEffectId, behavior.SecondaryStatusEffectId }
            .Where(statusEffectId => !string.IsNullOrWhiteSpace(statusEffectId))
            .ToArray();
    }

    private static bool ShouldApplyStatusEffectsToTarget(CardBehaviorDefinition behavior, int targetIndex)
    {
        return behavior.StatusEffectTargetIndex < 0 || behavior.StatusEffectTargetIndex == targetIndex;
    }

    private static bool ShouldApplyDamageToTarget(CardBehaviorDefinition behavior, int targetIndex)
    {
        return behavior.DamageTargetIndex < 0 || behavior.DamageTargetIndex == targetIndex;
    }

    private static bool ShouldPreventSpellOrSkillDamage(
        MatchState state,
        CardBehaviorDefinition behavior)
    {
        if (!state.UntilEndOfTurnEffects.Contains(
                PreventSpellAndSkillDamageThisTurnEffectId,
                StringComparer.Ordinal))
        {
            return false;
        }

        return !behavior.PlaysSourceToBaseAsUnit
            && !behavior.PlaysSourceToBaseAsEquipment;
    }

    private static bool ShouldGrantFreeStandbyHidePermission(CardBehaviorDefinition behavior)
    {
        return string.Equals(
            behavior.EffectKind,
            GuerrillaWarfareEffectKind,
            StringComparison.Ordinal);
    }

    private static bool ShouldApplyBanishPlayToTarget(CardBehaviorDefinition behavior, int targetIndex)
    {
        return behavior.BanishPlayTargetIndex < 0 || behavior.BanishPlayTargetIndex == targetIndex;
    }

    private static bool ShouldApplyPowerModifierToTarget(CardBehaviorDefinition behavior, int targetIndex)
    {
        return behavior.PowerModifierTargetIndex < 0 || behavior.PowerModifierTargetIndex == targetIndex;
    }

    private static bool PowerModifierConditionApplies(
        CardObjectState targetState,
        CardBehaviorDefinition behavior)
    {
        return behavior.PowerModifierConditionKind switch
        {
            CardPowerModifierConditionKinds.TargetIsAttacking => targetState.IsAttacking,
            CardPowerModifierConditionKinds.TargetIsDefending => targetState.IsDefending,
            _ => true
        };
    }

    private static RecycleResult RecycleTargetCards(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        string controllerId,
        string sourceObjectId,
        IReadOnlyList<string> targetObjectIds)
    {
        var events = new List<GameEvent>();
        var rngCursor = state.RngCursor;
        foreach (var (ownerPlayerId, zones) in playerZones
            .Where(entry => !string.Equals(entry.Key, controllerId, StringComparison.Ordinal)))
        {
            var recycledCardIds = targetObjectIds
                .Where(cardId =>
                    (zones.Graveyard.Contains(cardId, StringComparer.Ordinal)
                        || zones.Hand.Contains(cardId, StringComparer.Ordinal))
                    && IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, ownerPlayerId, cardId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (recycledCardIds.Length == 0)
            {
                continue;
            }

            var randomizedCardIds = RandomizeForMainDeckBottom(
                recycledCardIds,
                state.Seed,
                rngCursor,
                sourceObjectId);
            if (recycledCardIds.Length > 1)
            {
                rngCursor++;
            }

            playerZones[ownerPlayerId] = zones with
            {
                MainDeck = zones.MainDeck.Concat(randomizedCardIds).ToArray(),
                Graveyard = zones.Graveyard
                    .Where(cardId => !recycledCardIds.Contains(cardId, StringComparer.Ordinal))
                    .ToArray(),
                Hand = zones.Hand
                    .Where(cardId => !recycledCardIds.Contains(cardId, StringComparer.Ordinal))
                    .ToArray()
            };
            events.Add(new GameEvent(
                "CARDS_RECYCLED",
                $"{ownerPlayerId} 回收 {recycledCardIds.Length} 张牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = ownerPlayerId,
                    ["sourceObjectId"] = sourceObjectId,
                    ["cardIds"] = randomizedCardIds,
                    ["count"] = randomizedCardIds.Count
                }));
        }

        return new RecycleResult(events, rngCursor);
    }

    private static RecycleResult RecycleUnkeptSacredJudgmentCards(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string sourceObjectId,
        IReadOnlyList<string> keptObjectIds,
        long rngCursor)
    {
        var events = new List<GameEvent>();
        var keptSet = keptObjectIds.ToHashSet(StringComparer.Ordinal);
        foreach (var (playerId, zones) in playerZones.ToArray())
        {
            var recycledCardIds = SacredJudgmentFieldUnitIds(cardObjects, playerId, zones)
                .Concat(SacredJudgmentEquipmentIds(cardObjects, playerId, zones))
                .Concat(SacredJudgmentRuneIds(cardObjects, playerId, zones))
                .Concat(zones.Hand.Where(cardId =>
                    IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, cardId)))
                .Where(cardId => !keptSet.Contains(cardId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (recycledCardIds.Length == 0)
            {
                continue;
            }

            var randomizedCardIds = RandomizeForMainDeckBottom(
                recycledCardIds,
                state.Seed,
                rngCursor,
                sourceObjectId);
            if (recycledCardIds.Length > 1)
            {
                rngCursor++;
            }

            playerZones[playerId] = zones with
            {
                MainDeck = zones.MainDeck.Concat(randomizedCardIds).ToArray(),
                Hand = zones.Hand
                    .Where(cardId => !recycledCardIds.Contains(cardId, StringComparer.Ordinal))
                    .ToArray(),
                Base = zones.Base
                    .Where(cardId => !recycledCardIds.Contains(cardId, StringComparer.Ordinal))
                    .ToArray(),
                Battlefields = zones.Battlefields
                    .Where(cardId => !recycledCardIds.Contains(cardId, StringComparer.Ordinal))
                    .ToArray()
            };
            events.Add(new GameEvent(
                "CARDS_RECYCLED",
                $"{playerId} 回收 {recycledCardIds.Length} 张牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = sourceObjectId,
                    ["cardIds"] = randomizedCardIds,
                    ["count"] = randomizedCardIds.Count
                }));
        }

        return new RecycleResult(events, rngCursor);
    }

    private static RecycleResult PlayEachPlayerTopFiveUnitsToBase(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string sourceObjectId,
        IReadOnlyList<string> selectedObjectIds,
        int lookCount,
        long rngCursor)
    {
        var events = new List<GameEvent>();
        if (lookCount <= 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var selectedByPlayerId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var selectedObjectId in selectedObjectIds)
        {
            if (TryGetTopMainDeckOwner(state, playerZones, selectedObjectId, lookCount, out var playerId))
            {
                selectedByPlayerId[playerId] = selectedObjectId;
            }
        }

        foreach (var playerId in SeatPlayerIds(state))
        {
            if (!selectedByPlayerId.TryGetValue(playerId, out var selectedObjectId)
                || !playerZones.TryGetValue(playerId, out var zones))
            {
                continue;
            }

            var viewedCardIds = zones.MainDeck.Take(lookCount).ToArray();
            var unselectedCardIds = viewedCardIds
                .Where(cardId => !string.Equals(cardId, selectedObjectId, StringComparison.Ordinal))
                .ToArray();
            var removedCardIds = viewedCardIds.ToHashSet(StringComparer.Ordinal);
            var randomizedRecycledCardIds = RandomizeForMainDeckBottom(
                unselectedCardIds,
                state.Seed,
                rngCursor,
                sourceObjectId);
            if (unselectedCardIds.Length > 1)
            {
                rngCursor++;
            }

            playerZones[playerId] = zones with
            {
                MainDeck = zones.MainDeck
                    .Where(cardId => !removedCardIds.Contains(cardId))
                    .Concat(randomizedRecycledCardIds)
                    .ToArray()
            };

            if (randomizedRecycledCardIds.Count > 0)
            {
                events.Add(new GameEvent(
                    "CARDS_RECYCLED",
                    $"{playerId} 回收 {randomizedRecycledCardIds.Count} 张牌",
                    new Dictionary<string, object?>
                    {
                        ["playerId"] = playerId,
                        ["sourceObjectId"] = sourceObjectId,
                        ["cardIds"] = randomizedRecycledCardIds,
                        ["count"] = randomizedRecycledCardIds.Count
                    }));
            }
        }

        foreach (var playerId in PlayerIdsStartingAfter(state, controllerId))
        {
            if (!selectedByPlayerId.TryGetValue(playerId, out var selectedObjectId)
                || !TryPutSelectedMainDeckUnitToBase(
                    playerZones,
                    cardObjects,
                    playerId,
                    selectedObjectId,
                    out _))
            {
                continue;
            }

            events.Add(new GameEvent(
                "UNIT_PLAYED_TO_BASE",
                $"{playerId} 打出主牌堆选择的单位到基地",
                new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = sourceObjectId,
                    ["targetObjectId"] = selectedObjectId,
                    ["ownerPlayerId"] = playerId,
                    ["playedByPlayerId"] = playerId,
                    ["sourceZone"] = "MAIN_DECK",
                    ["destinationZone"] = "BASE"
                }));
        }

        return new RecycleResult(events, rngCursor);
    }

    private static bool TryPutSelectedMainDeckUnitToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string targetObjectId,
        out CardObjectState targetState)
    {
        targetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
            ? existingTargetState
            : new CardObjectState(targetObjectId);
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        playerZones[playerId] = zones with
        {
            Base = zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Base
                : zones.Base.Concat([targetObjectId]).ToArray()
        };

        targetState = targetState with
        {
            Damage = 0,
            Power = Math.Max(0, targetState.Power - targetState.UntilEndOfTurnPowerModifier),
            UntilEndOfTurnEffects = [],
            UntilEndOfTurnPowerModifier = 0,
            IsExhausted = false
        };
        cardObjects[targetObjectId] = targetState;
        return true;
    }

    private static IReadOnlyList<string> MainDeckTargetObjectIds(
        IReadOnlyList<string> targetObjectIds,
        CardBehaviorDefinition behavior)
    {
        return targetObjectIds
            .Skip(Math.Max(0, behavior.MainDeckLookTargetStartIndex))
            .ToArray();
    }

    private static RecycleResult RecycleSelectedMainDeckTargets(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        string playerId,
        string sourceObjectId,
        IReadOnlyList<string> selectedObjectIds,
        int lookCount)
    {
        var events = new List<GameEvent>();
        var rngCursor = state.RngCursor;
        if (lookCount <= 0
            || selectedObjectIds.Count == 0
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return new RecycleResult(events, rngCursor);
        }

        var viewedCardIds = zones.MainDeck.Take(lookCount).ToArray();
        var recycledCardIds = selectedObjectIds
            .Where(cardId => viewedCardIds.Contains(cardId, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (recycledCardIds.Length == 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var randomizedRecycledCardIds = RandomizeForMainDeckBottom(
            recycledCardIds,
            state.Seed,
            rngCursor,
            sourceObjectId);
        if (recycledCardIds.Length > 1)
        {
            rngCursor++;
        }

        var recycledCardIdSet = recycledCardIds.ToHashSet(StringComparer.Ordinal);
        playerZones[playerId] = zones with
        {
            MainDeck = zones.MainDeck
                .Where(cardId => !recycledCardIdSet.Contains(cardId))
                .Concat(randomizedRecycledCardIds)
                .ToArray()
        };

        events.Add(new GameEvent(
            "CARDS_RECYCLED",
            $"{playerId} 回收 {randomizedRecycledCardIds.Count} 张牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = sourceObjectId,
                ["cardIds"] = randomizedRecycledCardIds,
                ["count"] = randomizedRecycledCardIds.Count
            }));

        return new RecycleResult(events, rngCursor);
    }

    private static RecycleResult DrawSelectedMainDeckTargetsAndRecycleRest(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        string playerId,
        string sourceObjectId,
        IReadOnlyList<string> selectedObjectIds,
        int lookCount,
        bool recycleUnselectedCards)
    {
        var events = new List<GameEvent>();
        var rngCursor = state.RngCursor;
        if (lookCount <= 0
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return new RecycleResult(events, rngCursor);
        }

        var viewedCardIds = zones.MainDeck.Take(lookCount).ToArray();
        var selectedCardIds = selectedObjectIds
            .Where(cardId => viewedCardIds.Contains(cardId, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var unselectedCardIds = recycleUnselectedCards
            ? viewedCardIds
                .Where(cardId => !selectedCardIds.Contains(cardId, StringComparer.Ordinal))
                .ToArray()
            : Array.Empty<string>();
        if (selectedCardIds.Length == 0
            && unselectedCardIds.Length == 0)
        {
            return new RecycleResult(events, rngCursor);
        }

        var removedCardIds = selectedCardIds
            .Concat(unselectedCardIds)
            .ToHashSet(StringComparer.Ordinal);
        var randomizedRecycledCardIds = RandomizeForMainDeckBottom(
            unselectedCardIds,
            state.Seed,
            rngCursor,
            sourceObjectId);
        if (unselectedCardIds.Length > 1)
        {
            rngCursor++;
        }

        playerZones[playerId] = zones with
        {
            MainDeck = zones.MainDeck
                .Where(cardId => !removedCardIds.Contains(cardId))
                .Concat(randomizedRecycledCardIds)
                .ToArray(),
            Hand = zones.Hand.Concat(selectedCardIds).ToArray()
        };

        if (selectedCardIds.Length > 0)
        {
            events.Add(new GameEvent(
                "CARD_DRAWN",
                $"{playerId} 抽 {selectedCardIds.Length} 张牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = sourceObjectId,
                    ["count"] = selectedCardIds.Length
                }));
        }

        if (randomizedRecycledCardIds.Count > 0)
        {
            events.Add(new GameEvent(
                "CARDS_RECYCLED",
                $"{playerId} 回收 {randomizedRecycledCardIds.Count} 张牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["sourceObjectId"] = sourceObjectId,
                    ["cardIds"] = randomizedRecycledCardIds,
                    ["count"] = randomizedRecycledCardIds.Count
                }));
        }

        return new RecycleResult(events, rngCursor);
    }

    private static RuneCallResult CallRunes(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        int runeCallCount)
    {
        if (runeCallCount <= 0
            || !playerZones.TryGetValue(playerId, out var zones))
        {
            return new RuneCallResult([]);
        }

        var calledRunes = TakeControlledRuneDeckPrefix(
            cardObjects,
            playerId,
            zones.RuneDeck,
            runeCallCount);
        if (calledRunes.Length == 0)
        {
            return new RuneCallResult([]);
        }

        playerZones[playerId] = zones with
        {
            RuneDeck = zones.RuneDeck.Skip(calledRunes.Length).ToArray(),
            Base = zones.Base.Concat(calledRunes).ToArray()
        };

        foreach (var runeObjectId in calledRunes)
        {
            var runeState = cardObjects.TryGetValue(runeObjectId, out var existingRuneState)
                ? existingRuneState
                : new CardObjectState(runeObjectId);
            cardObjects[runeObjectId] = runeState with
            {
                IsExhausted = true
            };
        }

        return new RuneCallResult(calledRunes);
    }

    private static string[] TakeControlledRuneDeckPrefix(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        IReadOnlyList<string> runeDeck,
        int runeCallCount)
    {
        if (runeCallCount <= 0)
        {
            return [];
        }

        var calledRunes = new List<string>();
        foreach (var runeObjectId in runeDeck.Take(runeCallCount))
        {
            if (!IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, runeObjectId))
            {
                break;
            }

            calledRunes.Add(runeObjectId);
        }

        return calledRunes.ToArray();
    }

    private static string[] TakeControlledMainDeckPrefix(
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        IReadOnlyList<string> mainDeck,
        int lookCount)
    {
        if (lookCount <= 0)
        {
            return [];
        }

        var objectIds = new List<string>();
        foreach (var objectId in mainDeck.Take(lookCount))
        {
            if (!IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId))
            {
                break;
            }

            objectIds.Add(objectId);
        }

        return objectIds.ToArray();
    }

    private static IReadOnlyList<string> RandomizeForMainDeckBottom(
        IReadOnlyList<string> cardIds,
        long seed,
        long rngCursor,
        string sourceObjectId)
    {
        if (cardIds.Count <= 1)
        {
            return cardIds.ToArray();
        }

        return cardIds
            .Select((cardId, index) => new
            {
                CardId = cardId,
                Index = index,
                Order = StableHash($"{seed}:{rngCursor}:{sourceObjectId}:{cardId}:{index}")
            })
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.Index)
            .Select(entry => entry.CardId)
            .ToArray();
    }

    private static ulong StableHash(string value)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }

    private static bool TryDestroyTarget(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        out FieldRemovalResult removalResult)
    {
        removalResult = FieldRemovalResult.Empty;
        foreach (var (playerId, zones) in playerZones)
        {
            var isInBase = zones.Base.Contains(targetObjectId, StringComparer.Ordinal);
            var isInBattlefield = zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal);
            if (!isInBase && !isInBattlefield)
            {
                continue;
            }

            var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
                ? existingTargetState
                : new CardObjectState(targetObjectId);
            var wasEquipment = targetState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal);
            var wasUnit = !wasEquipment
                || targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal);
            var shouldRecallToBase = targetState.UntilEndOfTurnEffects.Contains(
                RecallToBaseExhaustedIfDestroyedThisTurnEffectId,
                StringComparer.Ordinal);
            var shouldBanish = !shouldRecallToBase
                && targetState.UntilEndOfTurnEffects.Contains(
                    BanishIfDestroyedThisTurnEffectId,
                    StringComparer.Ordinal);
            if (shouldRecallToBase)
            {
                playerZones[playerId] = zones with
                {
                    Base = zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                        ? zones.Base
                        : zones.Base.Concat([targetObjectId]).ToArray(),
                    Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId)
                };
                cardObjects[targetObjectId] = targetState with
                {
                    Damage = 0,
                    IsExhausted = true,
                    UntilEndOfTurnEffects = targetState.UntilEndOfTurnEffects
                        .Where(effectId => !string.Equals(
                            effectId,
                            RecallToBaseExhaustedIfDestroyedThisTurnEffectId,
                            StringComparison.Ordinal))
                        .ToArray()
                };
                removalResult = new FieldRemovalResult(
                    playerId,
                    "BASE",
                    false,
                    true,
                    wasEquipment,
                    wasUnit,
                    []);
                return true;
            }

            var detachedEquipmentObjectIds = DetachEquipmentFromRemovedHost(cardObjects, targetObjectId);
            playerZones[playerId] = zones with
            {
                Base = RemoveFromZone(zones.Base, targetObjectId),
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId),
                Graveyard = shouldBanish || zones.Graveyard.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Graveyard
                    : zones.Graveyard.Concat([targetObjectId]).ToArray(),
                Banished = shouldBanish && !zones.Banished.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Banished.Concat([targetObjectId]).ToArray()
                    : zones.Banished
            };
            cardObjects.Remove(targetObjectId);
            removalResult = new FieldRemovalResult(
                playerId,
                shouldBanish ? "BANISHED" : "GRAVEYARD",
                shouldBanish,
                false,
                wasEquipment,
                wasUnit,
                detachedEquipmentObjectIds);
            return true;
        }

        return false;
    }

    private static bool TryDestroyControlledFieldTarget(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        out FieldRemovalResult removalResult)
    {
        removalResult = FieldRemovalResult.Empty;
        var location = FindFieldObjectLocation(playerZones, targetObjectId);
        if (location is null
            || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, location.Value.PlayerId, targetObjectId))
        {
            return false;
        }

        return TryDestroyTarget(playerZones, cardObjects, targetObjectId, out removalResult);
    }

    private static IReadOnlyList<string> DetachEquipmentFromRemovedHost(
        Dictionary<string, CardObjectState> cardObjects,
        string hostObjectId)
    {
        var attachedEquipmentObjectIds = cardObjects
            .Where(entry => string.Equals(
                entry.Value.AttachedToObjectId,
                hostObjectId,
                StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();

        foreach (var equipmentObjectId in attachedEquipmentObjectIds)
        {
            cardObjects[equipmentObjectId] = cardObjects[equipmentObjectId] with
            {
                AttachedToObjectId = null
            };
        }

        return attachedEquipmentObjectIds;
    }

    private static GameEvent BuildFieldRemovalEvent(
        string displayName,
        StackItemState stackItem,
        string targetObjectId,
        FieldRemovalResult removalResult,
        string? reason = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["sourceObjectId"] = stackItem.SourceObjectId,
            ["targetObjectId"] = targetObjectId,
            ["ownerPlayerId"] = removalResult.OwnerPlayerId,
            ["destroyedByPlayerId"] = stackItem.ControllerId,
            ["destinationZone"] = removalResult.DestinationZone
        };
        if (!string.IsNullOrWhiteSpace(reason))
        {
            payload["reason"] = reason;
        }

        if (removalResult.WasBanished)
        {
            payload["replacementEffectId"] = BanishIfDestroyedThisTurnEffectId;
        }

        if (removalResult.DetachedEquipmentObjectIds.Count > 0)
        {
            payload["detachedEquipmentObjectIds"] = removalResult.DetachedEquipmentObjectIds.ToArray();
        }

        if (removalResult.WasRecalledToBase)
        {
            payload["replacementEffectId"] = RecallToBaseExhaustedIfDestroyedThisTurnEffectId;
            return new GameEvent("UNIT_RECALLED_TO_BASE", $"{displayName}改为休眠召回单位", payload);
        }

        if (removalResult.WasBanished)
        {
            return new GameEvent("UNIT_BANISHED", $"{displayName}改为放逐单位", payload);
        }

        return removalResult.WasEquipment
            ? new GameEvent("EQUIPMENT_DESTROYED", $"{displayName}摧毁装备", payload)
            : new GameEvent("UNIT_DESTROYED", $"{displayName}摧毁单位", payload);
    }

    private static GameEvent BuildReturnedToHandEvent(
        string displayName,
        StackItemState stackItem,
        string targetObjectId,
        string ownerPlayerId,
        bool wasEquipment,
        string reason = "")
    {
        var payload = new Dictionary<string, object?>
        {
            ["sourceObjectId"] = stackItem.SourceObjectId,
            ["targetObjectId"] = targetObjectId,
            ["ownerPlayerId"] = ownerPlayerId
        };
        if (!string.IsNullOrWhiteSpace(reason))
        {
            payload["reason"] = reason;
        }

        return new GameEvent(
            wasEquipment ? "EQUIPMENT_RETURNED_TO_HAND" : "UNIT_RETURNED_TO_HAND",
            wasEquipment ? $"{displayName}让装备返回手牌" : $"{displayName}让单位返回手牌",
            payload);
    }

    private static bool TryReturnTargetToHand(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        out string ownerPlayerId,
        out bool wasEquipment)
    {
        ownerPlayerId = string.Empty;
        wasEquipment = false;
        foreach (var (playerId, zones) in playerZones)
        {
            var isInBase = zones.Base.Contains(targetObjectId, StringComparer.Ordinal);
            var isInBattlefield = zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal);
            if ((!isInBase && !isInBattlefield)
                || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Base = RemoveFromZone(zones.Base, targetObjectId),
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId),
                Hand = zones.Hand.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Hand
                    : zones.Hand.Concat([targetObjectId]).ToArray()
            };
            wasEquipment = cardObjects.TryGetValue(targetObjectId, out var targetState)
                && targetState.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal);
            cardObjects.Remove(targetObjectId);
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryMoveTargetToOwnerMainDeck(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        string destination,
        out string ownerPlayerId,
        out string deckPosition)
    {
        ownerPlayerId = string.Empty;
        deckPosition = NormalizeMainDeckDestination(destination);
        if (string.IsNullOrWhiteSpace(deckPosition))
        {
            return false;
        }

        foreach (var (playerId, zones) in playerZones)
        {
            var isInBase = zones.Base.Contains(targetObjectId, StringComparer.Ordinal);
            var isInBattlefield = zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal);
            if ((!isInBase && !isInBattlefield)
                || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
            {
                continue;
            }

            var remainingMainDeck = RemoveFromZone(zones.MainDeck, targetObjectId);
            var mainDeck = string.Equals(deckPosition, "TOP", StringComparison.Ordinal)
                ? new[] { targetObjectId }.Concat(remainingMainDeck).ToArray()
                : remainingMainDeck.Concat([targetObjectId]).ToArray();
            playerZones[playerId] = zones with
            {
                Base = RemoveFromZone(zones.Base, targetObjectId),
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId),
                MainDeck = mainDeck
            };
            cardObjects.Remove(targetObjectId);
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static string NormalizeMainDeckDestination(string destination)
    {
        var normalizedDestination = destination.Trim().ToUpperInvariant();
        return normalizedDestination is "TOP" or "BOTTOM" ? normalizedDestination : string.Empty;
    }

    private static bool TryBanishTargetThenPlayToOwnerField(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        string destinationZone,
        out string ownerPlayerId)
    {
        ownerPlayerId = string.Empty;
        var playToBattlefield = string.Equals(destinationZone, "BATTLEFIELD", StringComparison.Ordinal);
        foreach (var (playerId, zones) in playerZones)
        {
            var isInBase = zones.Base.Contains(targetObjectId, StringComparer.Ordinal);
            var isInBattlefield = zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal);
            if ((!isInBase && !isInBattlefield)
                || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
            {
                continue;
            }

            var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
                ? existingTargetState
                : new CardObjectState(targetObjectId);
            playerZones[playerId] = zones with
            {
                Base = RemoveFromZone(zones.Base, targetObjectId),
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId),
                Banished = zones.Banished.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Banished
                    : zones.Banished.Concat([targetObjectId]).ToArray()
            };

            var banishedZones = playerZones[playerId];
            playerZones[playerId] = banishedZones with
            {
                Banished = RemoveFromZone(banishedZones.Banished, targetObjectId),
                Base = playToBattlefield || banishedZones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                    ? banishedZones.Base
                    : banishedZones.Base.Concat([targetObjectId]).ToArray(),
                Battlefields = !playToBattlefield || banishedZones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                    ? banishedZones.Battlefields
                    : banishedZones.Battlefields.Concat([targetObjectId]).ToArray()
            };
            cardObjects[targetObjectId] = targetState with
            {
                Damage = 0,
                Power = Math.Max(0, targetState.Power - targetState.UntilEndOfTurnPowerModifier),
                UntilEndOfTurnEffects = [],
                UntilEndOfTurnPowerModifier = 0,
                IsExhausted = false
            };
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryDiscardCardFromHand(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string targetObjectId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones)
            || !zones.Hand.Contains(targetObjectId, StringComparer.Ordinal)
            || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
        {
            return false;
        }

        playerZones[playerId] = zones with
        {
            Hand = RemoveFromZone(zones.Hand, targetObjectId),
            Graveyard = zones.Graveyard.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Graveyard
                : zones.Graveyard.Concat([targetObjectId]).ToArray()
        };
        return true;
    }

    private static bool TryDiscardCardFromAnyHand(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        out string ownerPlayerId)
    {
        ownerPlayerId = string.Empty;
        foreach (var (playerId, zones) in playerZones)
        {
            if (!zones.Hand.Contains(targetObjectId, StringComparer.Ordinal)
                || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Hand = RemoveFromZone(zones.Hand, targetObjectId),
                Graveyard = zones.Graveyard.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Graveyard
                    : zones.Graveyard.Concat([targetObjectId]).ToArray()
            };
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryDiscardPlayerHand(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        out IReadOnlyList<string> discardedObjectIds)
    {
        discardedObjectIds = [];
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return false;
        }

        discardedObjectIds = zones.Hand
            .Where(objectId =>
                !string.IsNullOrWhiteSpace(objectId)
                && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, objectId))
            .ToArray();
        var discardedObjectIdSet = discardedObjectIds.ToHashSet(StringComparer.Ordinal);
        playerZones[playerId] = zones with
        {
            Hand = zones.Hand
                .Where(objectId => !discardedObjectIdSet.Contains(objectId))
                .ToArray(),
            Graveyard = zones.Graveyard
                .Concat(discardedObjectIds.Where(objectId =>
                    !zones.Graveyard.Contains(objectId, StringComparer.Ordinal)))
                .ToArray()
        };
        return true;
    }

    private static bool TryReturnGraveyardCardToHand(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        string targetObjectId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones)
            || !zones.Graveyard.Contains(targetObjectId, StringComparer.Ordinal)
            || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
        {
            return false;
        }

        playerZones[playerId] = zones with
        {
            Graveyard = RemoveFromZone(zones.Graveyard, targetObjectId),
            Hand = zones.Hand.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Hand
                : zones.Hand.Concat([targetObjectId]).ToArray()
        };
        return true;
    }

    private static bool TryGainControlOfTargetToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string targetObjectId,
        bool isExhausted,
        out string previousControllerId,
        out CardObjectState controlledTargetState)
    {
        previousControllerId = string.Empty;
        controlledTargetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
            ? existingTargetState
            : new CardObjectState(targetObjectId);
        if (!playerZones.ContainsKey(controllerId))
        {
            return false;
        }

        foreach (var (playerId, zones) in playerZones)
        {
            if (string.Equals(playerId, controllerId, StringComparison.Ordinal)
                || !zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId)
            };
            var ownerId = string.IsNullOrWhiteSpace(controlledTargetState.OwnerId)
                ? playerId
                : controlledTargetState.OwnerId;
            var controllerZones = playerZones[controllerId];
            playerZones[controllerId] = controllerZones with
            {
                Base = controllerZones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                    ? controllerZones.Base
                    : controllerZones.Base.Concat([targetObjectId]).ToArray()
            };
            controlledTargetState = controlledTargetState with
            {
                OwnerId = ownerId,
                ControllerId = controllerId,
                IsExhausted = controlledTargetState.IsExhausted || isExhausted
            };
            cardObjects[targetObjectId] = controlledTargetState;
            previousControllerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryPlayGraveyardCardToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string playerId,
        string targetObjectId)
    {
        if (!playerZones.TryGetValue(playerId, out var zones)
            || !zones.Graveyard.Contains(targetObjectId, StringComparer.Ordinal)
            || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
        {
            return false;
        }

        playerZones[playerId] = zones with
        {
            Graveyard = RemoveFromZone(zones.Graveyard, targetObjectId),
            Base = zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Base
                : zones.Base.Concat([targetObjectId]).ToArray()
        };

        var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
            ? existingTargetState
            : new CardObjectState(targetObjectId);
        cardObjects[targetObjectId] = targetState with
        {
            Damage = 0,
            Power = Math.Max(0, targetState.Power - targetState.UntilEndOfTurnPowerModifier),
            UntilEndOfTurnEffects = [],
            UntilEndOfTurnPowerModifier = 0,
            IsExhausted = false
        };
        return true;
    }

    private static bool TryPlayHandCardToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        string statusEffectId,
        out string ownerPlayerId,
        out CardObjectState targetState)
    {
        ownerPlayerId = string.Empty;
        targetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
            ? existingTargetState
            : new CardObjectState(targetObjectId);

        foreach (var (playerId, zones) in playerZones)
        {
            if (!zones.Hand.Contains(targetObjectId, StringComparer.Ordinal)
                || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Hand = RemoveFromZone(zones.Hand, targetObjectId),
                Base = zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Base
                    : zones.Base.Concat([targetObjectId]).ToArray()
            };

            targetState = targetState with
            {
                Damage = 0,
                Power = Math.Max(0, targetState.Power - targetState.UntilEndOfTurnPowerModifier),
                UntilEndOfTurnEffects = string.IsNullOrWhiteSpace(statusEffectId)
                    ? []
                    : targetState.UntilEndOfTurnEffects
                        .Concat([statusEffectId])
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(effectId => effectId, StringComparer.Ordinal)
                        .ToArray(),
                UntilEndOfTurnPowerModifier = 0,
                IsExhausted = false
            };
            cardObjects[targetObjectId] = targetState;
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryPlayOpponentTopMainDeckUnitToBase(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string targetObjectId,
        out string ownerPlayerId,
        out CardObjectState targetState)
    {
        ownerPlayerId = string.Empty;
        targetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
            ? existingTargetState
            : new CardObjectState(targetObjectId);
        if (!playerZones.ContainsKey(controllerId))
        {
            return false;
        }

        foreach (var (playerId, zones) in playerZones)
        {
            if (string.Equals(playerId, controllerId, StringComparison.Ordinal)
                || zones.MainDeck.Count == 0
                || !string.Equals(zones.MainDeck[0], targetObjectId, StringComparison.Ordinal)
                || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                MainDeck = RemoveFromZone(zones.MainDeck, targetObjectId)
            };

            var controllerZones = playerZones[controllerId];
            playerZones[controllerId] = controllerZones with
            {
                Base = controllerZones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                    ? controllerZones.Base
                    : controllerZones.Base.Concat([targetObjectId]).ToArray()
            };

            targetState = targetState with
            {
                Damage = 0,
                Power = Math.Max(0, targetState.Power - targetState.UntilEndOfTurnPowerModifier),
                UntilEndOfTurnEffects = [],
                UntilEndOfTurnPowerModifier = 0,
                IsExhausted = false
            };
            cardObjects[targetObjectId] = targetState;
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryGainControlOfTargetToBattlefield(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string targetObjectId,
        out string previousControllerId,
        out CardObjectState controlledTargetState)
    {
        previousControllerId = string.Empty;
        controlledTargetState = cardObjects.TryGetValue(targetObjectId, out var existingTargetState)
            ? existingTargetState
            : new CardObjectState(targetObjectId);
        if (!playerZones.ContainsKey(controllerId))
        {
            return false;
        }

        foreach (var (playerId, zones) in playerZones)
        {
            if (string.Equals(playerId, controllerId, StringComparison.Ordinal)
                || !zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId)
            };
            var ownerId = string.IsNullOrWhiteSpace(controlledTargetState.OwnerId)
                ? playerId
                : controlledTargetState.OwnerId;
            var controllerZones = playerZones[controllerId];
            playerZones[controllerId] = controllerZones with
            {
                Battlefields = controllerZones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                    ? controllerZones.Battlefields
                    : controllerZones.Battlefields.Concat([targetObjectId]).ToArray()
            };
            controlledTargetState = controlledTargetState with
            {
                OwnerId = ownerId,
                ControllerId = controllerId,
                UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(
                    controlledTargetState.UntilEndOfTurnEffects,
                    BuildReturnControlToOwnerAtTurnEndEffectId(ownerId))
            };
            cardObjects[targetObjectId] = controlledTargetState;
            previousControllerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryMoveTargetToOwnerBase(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        out string ownerPlayerId)
    {
        ownerPlayerId = string.Empty;
        foreach (var (playerId, zones) in playerZones)
        {
            if (!zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Battlefields = RemoveFromZone(zones.Battlefields, targetObjectId),
                Base = zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Base
                    : zones.Base.Concat([targetObjectId]).ToArray()
            };
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static void RemoveDamageFromFriendlyBattlefieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        if (!playerZones.TryGetValue(stackItem.ControllerId, out var zones))
        {
            return;
        }

        var damagedObjectIds = zones.Battlefields
            .Where(objectId => cardObjects.TryGetValue(objectId, out var state)
                && state.Damage > 0
                && state.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (damagedObjectIds.Length == 0)
        {
            return;
        }

        foreach (var objectId in damagedObjectIds)
        {
            cardObjects[objectId] = cardObjects[objectId] with { Damage = 0 };
        }

        events.Add(new GameEvent(
            "DAMAGE_REMOVED",
            $"{behavior.DisplayName}移除己方战场单位伤害",
            new Dictionary<string, object?>
            {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["playerId"] = stackItem.ControllerId,
                ["objectIds"] = damagedObjectIds,
                ["count"] = damagedObjectIds.Length
            }));
    }

    private static bool TryMoveTargetToOwnerBattlefield(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string targetObjectId,
        out string ownerPlayerId)
    {
        ownerPlayerId = string.Empty;
        foreach (var (playerId, zones) in playerZones)
        {
            if (!zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
                || !IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, playerId, targetObjectId))
            {
                continue;
            }

            playerZones[playerId] = zones with
            {
                Base = RemoveFromZone(zones.Base, targetObjectId),
                Battlefields = zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                    ? zones.Battlefields
                    : zones.Battlefields.Concat([targetObjectId]).ToArray()
            };
            ownerPlayerId = playerId;
            return true;
        }

        return false;
    }

    private static bool TryMoveTargetToControllerBattlefield(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string targetObjectId)
    {
        if (!CanMoveTargetToControllerBattlefield(
                playerZones,
                cardObjects,
                controllerId,
                targetObjectId)
            || !playerZones.TryGetValue(controllerId, out var zones))
        {
            return false;
        }

        playerZones[controllerId] = zones with
        {
            Base = RemoveFromZone(zones.Base, targetObjectId),
            Battlefields = zones.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
                ? zones.Battlefields
                : zones.Battlefields.Concat([targetObjectId]).ToArray()
        };
        return true;
    }

    private static bool CanMoveTargetToControllerBattlefield(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string controllerId,
        string targetObjectId)
    {
        return playerZones.TryGetValue(controllerId, out var zones)
            && zones.Base.Contains(targetObjectId, StringComparer.Ordinal)
            && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, controllerId, targetObjectId);
    }

    private static bool TryMoveFirstTargetToSecondTargetLocation(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string movedObjectId,
        string destinationObjectId,
        out string destinationPlayerId,
        out string destinationZone)
    {
        destinationPlayerId = string.Empty;
        destinationZone = string.Empty;

        if (!CanMoveFirstTargetToSecondTargetLocation(
                playerZones,
                cardObjects,
                movedObjectId,
                destinationObjectId))
        {
            return false;
        }

        var movedLocation = FindFieldObjectLocation(playerZones, movedObjectId)!.Value;
        var targetLocation = FindFieldObjectLocation(playerZones, destinationObjectId)!.Value;

        RemoveFieldObjectFromLocation(playerZones, movedLocation.PlayerId, movedLocation.Zone, movedObjectId);
        AddFieldObjectToLocation(playerZones, targetLocation.PlayerId, targetLocation.Zone, movedObjectId);
        destinationPlayerId = targetLocation.PlayerId;
        destinationZone = targetLocation.Zone;
        return true;
    }

    private static bool CanMoveFirstTargetToSecondTargetLocation(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string movedObjectId,
        string destinationObjectId)
    {
        if (string.Equals(movedObjectId, destinationObjectId, StringComparison.Ordinal))
        {
            return false;
        }

        var movedLocation = FindFieldObjectLocation(playerZones, movedObjectId);
        var targetLocation = FindFieldObjectLocation(playerZones, destinationObjectId);
        if (movedLocation is null || targetLocation is null)
        {
            return false;
        }

        return (!string.Equals(movedLocation.Value.PlayerId, targetLocation.Value.PlayerId, StringComparison.Ordinal)
                || !string.Equals(movedLocation.Value.Zone, targetLocation.Value.Zone, StringComparison.Ordinal))
            && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, movedLocation.Value.PlayerId, movedObjectId)
            && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, targetLocation.Value.PlayerId, destinationObjectId);
    }

    private static bool TrySwapTargetLocations(
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string firstObjectId,
        string secondObjectId,
        out string firstDestinationPlayerId,
        out string firstDestinationZone,
        out string secondDestinationPlayerId,
        out string secondDestinationZone)
    {
        firstDestinationPlayerId = string.Empty;
        firstDestinationZone = string.Empty;
        secondDestinationPlayerId = string.Empty;
        secondDestinationZone = string.Empty;

        if (!CanSwapTargetLocations(playerZones, cardObjects, firstObjectId, secondObjectId))
        {
            return false;
        }

        var firstLocation = FindFieldObjectLocation(playerZones, firstObjectId)!.Value;
        var secondLocation = FindFieldObjectLocation(playerZones, secondObjectId)!.Value;

        RemoveFieldObjectFromLocation(playerZones, firstLocation.PlayerId, firstLocation.Zone, firstObjectId);
        RemoveFieldObjectFromLocation(playerZones, secondLocation.PlayerId, secondLocation.Zone, secondObjectId);
        AddFieldObjectToLocation(playerZones, secondLocation.PlayerId, secondLocation.Zone, firstObjectId);
        AddFieldObjectToLocation(playerZones, firstLocation.PlayerId, firstLocation.Zone, secondObjectId);

        firstDestinationPlayerId = secondLocation.PlayerId;
        firstDestinationZone = secondLocation.Zone;
        secondDestinationPlayerId = firstLocation.PlayerId;
        secondDestinationZone = firstLocation.Zone;
        return true;
    }

    private static bool CanSwapTargetLocations(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string firstObjectId,
        string secondObjectId)
    {
        if (string.Equals(firstObjectId, secondObjectId, StringComparison.Ordinal))
        {
            return false;
        }

        var firstLocation = FindFieldObjectLocation(playerZones, firstObjectId);
        var secondLocation = FindFieldObjectLocation(playerZones, secondObjectId);
        if (firstLocation is null || secondLocation is null)
        {
            return false;
        }

        return (!string.Equals(firstLocation.Value.PlayerId, secondLocation.Value.PlayerId, StringComparison.Ordinal)
                || !string.Equals(firstLocation.Value.Zone, secondLocation.Value.Zone, StringComparison.Ordinal))
            && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, firstLocation.Value.PlayerId, firstObjectId)
            && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, secondLocation.Value.PlayerId, secondObjectId);
    }

    private static (string PlayerId, string Zone)? FindFieldObjectLocation(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId)
    {
        foreach (var (playerId, zones) in playerZones)
        {
            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                return (playerId, "BASE");
            }

            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                return (playerId, "BATTLEFIELD");
            }
        }

        return null;
    }

    private static void RemoveFieldObjectFromLocation(
        Dictionary<string, PlayerZones> playerZones,
        string playerId,
        string zone,
        string objectId)
    {
        var zones = playerZones[playerId];
        playerZones[playerId] = string.Equals(zone, "BASE", StringComparison.Ordinal)
            ? zones with { Base = RemoveFromZone(zones.Base, objectId) }
            : zones with { Battlefields = RemoveFromZone(zones.Battlefields, objectId) };
    }

    private static void AddFieldObjectToLocation(
        Dictionary<string, PlayerZones> playerZones,
        string playerId,
        string zone,
        string objectId)
    {
        var zones = playerZones[playerId];
        playerZones[playerId] = string.Equals(zone, "BASE", StringComparison.Ordinal)
            ? zones with
            {
                Base = zones.Base.Contains(objectId, StringComparer.Ordinal)
                    ? zones.Base
                    : zones.Base.Concat([objectId]).ToArray()
            }
            : zones with
            {
                Battlefields = zones.Battlefields.Contains(objectId, StringComparer.Ordinal)
                    ? zones.Battlefields
                    : zones.Battlefields.Concat([objectId]).ToArray()
            };
    }

    private static bool ShouldDrawForBehavior(
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        IReadOnlyList<string> destroyedObjectIds,
        int drawCount)
    {
        if (drawCount <= 0)
        {
            return false;
        }

        return behavior.DrawConditionKind switch
        {
            CardDrawConditionKinds.None => true,
            CardDrawConditionKinds.TargetDestroyedByThisEffect => destroyedObjectIds.Count > 0,
            CardDrawConditionKinds.PlayedAfterAnotherCardThisTurn => stackItem.PlayedAfterAnotherCardThisTurn,
            _ => false
        };
    }

    private static int ResolveDrawCount(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string controllerId,
        CardBehaviorDefinition behavior)
    {
        return behavior.DynamicDrawCountKind switch
        {
            CardDynamicDrawCountKinds.ControllerPowerfulUnits => CountControlledUnitsWithPowerAtLeast(
                playerZones,
                cardObjects,
                controllerId,
                5),
            _ => behavior.DrawCount
        };
    }

    private static int CountControlledUnitsWithPowerAtLeast(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string playerId,
        int minimumPower)
    {
        if (!playerZones.TryGetValue(playerId, out var zones))
        {
            return 0;
        }

        return zones.Base
            .Concat(zones.Battlefields)
            .Count(objectId => cardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Power >= minimumPower);
    }

    private static StateBasedCleanupResult RunStateBasedCleanupLoop(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        StackItemState stackItem,
        IReadOnlyDictionary<string, RunePool> runePools,
        string? battlefieldId = null,
        IReadOnlySet<string>? damageTriggeredDestroyTargetObjectIds = null,
        Dictionary<string, ObjectLocationState>? objectLocations = null)
    {
        var events = new List<GameEvent>();
        var destroyedObjectIds = new List<string>();
        var destroyedUnitOwnerIds = new List<string>();
        var nextRunePools = runePools;
        var firstPassDamageTriggeredDestroyTargetObjectIds =
            damageTriggeredDestroyTargetObjectIds ?? new HashSet<string>(StringComparer.Ordinal);

        for (var pass = 0; pass < 32; pass++)
        {
            var cleanup = ApplyLethalDamageCleanup(
                playerZones,
                cardObjects,
                stackItem,
                pass == 0
                    ? firstPassDamageTriggeredDestroyTargetObjectIds
                    : new HashSet<string>(StringComparer.Ordinal),
                nextRunePools,
                battlefieldId);
            var illegalStandbyCleanup = objectLocations is null
                ? IllegalStandbyCleanupResult.Empty
                : ApplyIllegalStandbyCleanup(playerZones, cardObjects, objectLocations);
            if (cleanup.Events.Count == 0
                && cleanup.DestroyedObjectIds.Count == 0
                && illegalStandbyCleanup.Events.Count == 0
                && illegalStandbyCleanup.RemovedObjectIds.Count == 0)
            {
                break;
            }

            events.AddRange(cleanup.Events);
            events.AddRange(illegalStandbyCleanup.Events);
            destroyedObjectIds.AddRange(cleanup.DestroyedObjectIds);
            destroyedUnitOwnerIds.AddRange(cleanup.DestroyedUnitOwnerIds);
            nextRunePools = cleanup.RunePools;
        }

        return new StateBasedCleanupResult(
            events,
            destroyedObjectIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(objectId => objectId, StringComparer.Ordinal)
                .ToArray(),
            destroyedUnitOwnerIds
                .Where(ownerId => !string.IsNullOrWhiteSpace(ownerId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ownerId => ownerId, StringComparer.Ordinal)
                .ToArray(),
            nextRunePools);
    }

    private static IllegalStandbyCleanupResult ApplyIllegalStandbyCleanup(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        Dictionary<string, ObjectLocationState> objectLocations)
    {
        var removedByBattlefield = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);
        var standbyObjectIds = objectLocations
            .Where(entry => string.Equals(entry.Value.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(entry.Value.BattlefieldObjectId)
                && !string.Equals(entry.Key, entry.Value.BattlefieldObjectId, StringComparison.Ordinal)
                && IsObjectOnField(playerZones, entry.Key)
                && cardObjects.TryGetValue(entry.Key, out var cardObject)
                && (cardObject.IsFaceDown || cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal))
                && cardObjects.TryGetValue(entry.Value.BattlefieldObjectId, out var battlefieldState)
                && !string.Equals(cardObject.ControllerId, battlefieldState.ControllerId, StringComparison.Ordinal))
            .OrderBy(entry => entry.Value.BattlefieldObjectId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var objectId in standbyObjectIds)
        {
            if (!cardObjects.TryGetValue(objectId, out var cardObject)
                || !objectLocations.TryGetValue(objectId, out var location)
                || string.IsNullOrWhiteSpace(location.BattlefieldObjectId))
            {
                continue;
            }

            var ownerPlayerId = cardObject.OwnerId;
            if (string.IsNullOrWhiteSpace(ownerPlayerId))
            {
                ownerPlayerId = location.PlayerId;
            }
            if (string.IsNullOrWhiteSpace(ownerPlayerId)
                || !playerZones.ContainsKey(ownerPlayerId))
            {
                continue;
            }

            foreach (var playerId in playerZones.Keys.ToArray())
            {
                var zones = playerZones[playerId];
                playerZones[playerId] = zones with
                {
                    Base = RemoveFromZone(zones.Base, objectId),
                    Battlefields = RemoveFromZone(zones.Battlefields, objectId)
                };
            }

            var ownerZones = playerZones[ownerPlayerId];
            playerZones[ownerPlayerId] = ownerZones with
            {
                Graveyard = ownerZones.Graveyard.Contains(objectId, StringComparer.Ordinal)
                    ? ownerZones.Graveyard
                    : ownerZones.Graveyard.Concat([objectId]).ToArray()
            };
            cardObjects[objectId] = cardObject with
            {
                Damage = 0,
                IsFaceDown = false,
                IsAttacking = false,
                IsDefending = false,
                ControllerId = ownerPlayerId
            };
            objectLocations[objectId] = new ObjectLocationState(ownerPlayerId, "GRAVEYARD");

            if (!removedByBattlefield.TryGetValue(location.BattlefieldObjectId, out var removed))
            {
                removed = [];
                removedByBattlefield[location.BattlefieldObjectId] = removed;
            }
            removed.Add(new Dictionary<string, object?>
            {
                ["objectId"] = objectId,
                ["ownerPlayerId"] = ownerPlayerId,
                ["previousControllerId"] = cardObject.ControllerId,
                ["destinationZone"] = "GRAVEYARD"
            });
        }

        var events = removedByBattlefield
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry =>
            {
                var battlefieldObjectId = entry.Key;
                cardObjects.TryGetValue(battlefieldObjectId, out var battlefieldState);
                return new GameEvent(
                    "BATTLEFIELD_STANDBY_REMOVED",
                    "状态清理移除非法待命牌",
                    new Dictionary<string, object?>
                    {
                        ["battlefieldObjectId"] = battlefieldObjectId,
                        ["controllerId"] = battlefieldState?.ControllerId,
                        ["removedObjectIds"] = entry.Value.Select(item => item["objectId"]).ToArray(),
                        ["removedCards"] = entry.Value,
                        ["reason"] = "BATTLEFIELD_CONTROL_CLEANUP"
                    });
            })
            .ToArray();

        return new IllegalStandbyCleanupResult(
            events,
            removedByBattlefield.Values
                .SelectMany(items => items)
                .Select(item => item["objectId"] as string)
                .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .OrderBy(objectId => objectId, StringComparer.Ordinal)
                .ToArray());
    }

    private static LethalDamageCleanupResult ApplyLethalDamageCleanup(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        StackItemState stackItem,
        IReadOnlySet<string> damageTriggeredDestroyTargetObjectIds,
        IReadOnlyDictionary<string, RunePool>? runePools = null,
        string? battlefieldId = null)
    {
        var events = new List<GameEvent>();
        var destroyedObjectIds = new List<string>();
        var destroyedUnitOwnerIds = new List<string>();
        var nextRunePools = runePools;
        var stateBasedRemovalObjectIds = cardObjects
            .Where(entry => ((entry.Value.Power <= 0
                        && entry.Value.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                        && !string.IsNullOrWhiteSpace(entry.Value.OwnerId)
                        && !string.IsNullOrWhiteSpace(entry.Value.ControllerId))
                    || (entry.Value.Power > 0
                        && entry.Value.Damage > 0
                        && entry.Value.Damage >= entry.Value.Power)
                    || damageTriggeredDestroyTargetObjectIds.Contains(entry.Key))
                && IsObjectOnField(playerZones, entry.Key))
            .Select(entry => entry.Key)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();

        foreach (var objectId in stateBasedRemovalObjectIds)
        {
            var destroyReason = damageTriggeredDestroyTargetObjectIds.Contains(objectId)
                ? "DAMAGE_TRIGGERED_DESTROY"
                : cardObjects.TryGetValue(objectId, out var objectState) && objectState.Power <= 0
                    ? "ZERO_POWER"
                : "LETHAL_DAMAGE";
            if (nextRunePools is not null
                && TryApplySettLegendDestroyReplacement(
                    playerZones,
                    cardObjects,
                    nextRunePools,
                    objectId,
                    stackItem,
                    destroyReason,
                    out var settRunePools,
                    out var settReplacementEvents))
            {
                nextRunePools = settRunePools;
                events.AddRange(settReplacementEvents);
                continue;
            }
            if (nextRunePools is not null
                && TryApplyBattlefieldDestroyedInBattleRecallReplacement(
                    playerZones,
                    cardObjects,
                    nextRunePools,
                    objectId,
                    stackItem,
                    destroyReason,
                    battlefieldId,
                    out var battlefieldRunePools,
                    out var battlefieldReplacementEvents))
            {
                nextRunePools = battlefieldRunePools;
                events.AddRange(battlefieldReplacementEvents);
                continue;
            }

            if (!TryDestroyTarget(playerZones, cardObjects, objectId, out var removalResult))
            {
                continue;
            }

            var removalDescription = destroyReason switch
            {
                "DAMAGE_TRIGGERED_DESTROY" => "伤害触发效果",
                "ZERO_POWER" => "0 战力清理",
                _ => "致命伤害"
            };
            events.Add(BuildFieldRemovalEvent(
                removalDescription,
                stackItem,
                objectId,
                removalResult,
                destroyReason));
            if (!removalResult.WasDestroyed)
            {
                continue;
            }

            destroyedObjectIds.Add(objectId);
            if (removalResult.WasUnit)
            {
                destroyedUnitOwnerIds.Add(removalResult.OwnerPlayerId);
            }
        }

        return new LethalDamageCleanupResult(
            events,
            destroyedObjectIds,
            destroyedUnitOwnerIds,
            nextRunePools ?? new Dictionary<string, RunePool>(StringComparer.Ordinal));
    }

    private static bool IsObjectOnField(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        string objectId)
    {
        return playerZones.Values.Any(zones =>
            zones.Base.Contains(objectId, StringComparer.Ordinal)
            || zones.Battlefields.Contains(objectId, StringComparer.Ordinal));
    }

    private static IReadOnlyList<string> RemoveFromZone(IReadOnlyList<string> zone, string objectId)
    {
        return zone
            .Where(existingObjectId => !string.Equals(existingObjectId, objectId, StringComparison.Ordinal))
            .ToArray();
    }

    private static DrawResult DrawCards(MatchState state, string playerId, PlayerZones zones, int drawCount)
    {
        DrawResult result = new(
            zones.MainDeck,
            zones.Graveyard,
            [],
            [],
            null,
            NormalizeScoresForSeats(state),
            state.RngCursor,
            EffectiveWinningScore(state));
        var currentZones = zones;
        var drawnCards = new List<string>();
        var burnouts = new List<BurnoutResult>();

        for (var i = 0; i < drawCount && result.WinnerPlayerId is null; i++)
        {
            result = DrawOne(
                state with
                {
                    PlayerScores = result.PlayerScores,
                    RngCursor = result.RngCursor
                },
                playerId,
                currentZones);
            drawnCards.AddRange(result.DrawnCards);
            burnouts.AddRange(result.Burnouts);
            currentZones = currentZones with
            {
                MainDeck = result.MainDeck,
                Graveyard = result.Graveyard
            };
        }

        return result with
        {
            DrawnCards = drawnCards.ToArray(),
            Burnouts = burnouts.ToArray()
        };
    }

    private static DrawApplicationResult ApplyDrawToPlayer(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, int> playerScores,
        string playerId,
        int drawCount,
        long rngCursor,
        List<GameEvent> events)
    {
        if (drawCount <= 0
            || !playerZones.TryGetValue(playerId, out var drawPlayerZones))
        {
            return new DrawApplicationResult(playerScores, null, rngCursor);
        }

        var drawState = state with
        {
            PlayerScores = playerScores,
            RngCursor = rngCursor
        };
        var drawResult = DrawCards(
            drawState,
            playerId,
            drawPlayerZones,
            drawCount);
        drawPlayerZones = drawPlayerZones with
        {
            MainDeck = drawResult.MainDeck,
            Hand = drawPlayerZones.Hand.Concat(drawResult.DrawnCards).ToArray(),
            Graveyard = drawResult.Graveyard
        };
        playerZones[playerId] = drawPlayerZones;
        events.AddRange(BuildCardDrawEvents(playerId, drawResult));

        return new DrawApplicationResult(
            drawResult.PlayerScores,
            drawResult.WinnerPlayerId,
            drawResult.RngCursor);
    }

    private static IReadOnlyList<GameEvent> BuildCardDrawEvents(string playerId, DrawResult drawResult)
    {
        var events = new List<GameEvent>();
        foreach (var (burnout, index) in drawResult.Burnouts.Select((burnout, index) => (burnout, index)))
        {
            events.Add(new GameEvent(
                "BURNOUT_APPLIED",
                $"{playerId} 执行燃尽",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["scoredPlayerId"] = burnout.ScoredPlayerId,
                    ["scoredPlayerScore"] = burnout.ScoredPlayerScore,
                    ["burnoutIndex"] = index + 1
                }));
        }

        if (drawResult.WinnerPlayerId is not null)
        {
            events.Add(new GameEvent(
                "MATCH_WON",
                $"{drawResult.WinnerPlayerId} 达到获胜分数并获胜",
                new Dictionary<string, object?>
                {
                    ["winnerPlayerId"] = drawResult.WinnerPlayerId,
                    ["winningScore"] = drawResult.WinningScore
                }));
            return events;
        }

        events.Add(new GameEvent(
            "CARD_DRAWN",
            $"{playerId} 抽 {drawResult.DrawnCards.Count} 张牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["count"] = drawResult.DrawnCards.Count
            }));
        return events;
    }

    private static int ResolveDamageAmount(
        MatchState state,
        StackItemState stackItem,
        CardBehaviorDefinition behavior,
        string? targetObjectId = null)
    {
        int damageAmount;
        if (behavior.DamageAmountFromFirstTargetManaCost
            && stackItem.TargetObjectIds.Count > 0
            && state.CardObjects.TryGetValue(stackItem.TargetObjectIds[0], out var firstTargetState))
        {
            damageAmount = firstTargetState.ManaCost;
        }
        else if (behavior.ConditionalDamageAmount > 0
            && DamageConditionApplies(state, stackItem.ControllerId, behavior.DamageConditionKind, targetObjectId))
        {
            damageAmount = behavior.ConditionalDamageAmount;
        }
        else
        {
            damageAmount = stackItem.DamageAmount > 0 ? stackItem.DamageAmount : behavior.DamageAmount;
        }

        if (!IsSpellPlayBehavior(behavior))
        {
            return damageAmount;
        }

        return ApplyBattlefieldTargetSpellSkillDamageBonus(
            state.PlayerZones,
            state.CardObjects,
            targetObjectId,
            damageAmount);
    }

    private static int ApplyBattlefieldTargetSpellSkillDamageBonus(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        string? targetObjectId,
        int damageAmount)
    {
        if (damageAmount <= 0
            || string.IsNullOrWhiteSpace(targetObjectId)
            || !cardObjects.TryGetValue(targetObjectId, out var targetState)
            || !targetState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
        {
            return damageAmount;
        }

        var hasBattlefieldBonus = playerZones.Any(entry =>
            entry.Value.Battlefields.Contains(targetObjectId, StringComparer.Ordinal)
            && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, entry.Key, targetObjectId)
            && entry.Value.Battlefields.Any(objectId =>
                cardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldTargetSpellSkillDamageBonusCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, entry.Key)));

        return hasBattlefieldBonus ? damageAmount + 1 : damageAmount;
    }

    private static bool DamageConditionApplies(
        MatchState state,
        string controllerId,
        string conditionKind,
        string? targetObjectId)
    {
        return conditionKind switch
        {
            CardDamageConditionKinds.ControllerHasFaceDownCard => ControllerControlsFaceDownCard(state, controllerId),
            CardDamageConditionKinds.TargetIsAttacking
                => !string.IsNullOrWhiteSpace(targetObjectId)
                    && state.CardObjects.TryGetValue(targetObjectId, out var targetState)
                    && targetState.IsAttacking,
            _ => false
        };
    }

    private static bool ControllerControlsFaceDownCard(MatchState state, string controllerId)
    {
        if (!state.PlayerZones.TryGetValue(controllerId, out var zones))
        {
            return false;
        }

        return zones.Battlefields.Any(objectId =>
            state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.IsFaceDown);
    }

    private static string NextPlayerId(MatchState state)
    {
        var players = SeatPlayerIds(state);
        if (players.Length == 0)
        {
            return state.TurnPlayerId;
        }

        var turnPlayerIndex = Array.IndexOf(players, state.TurnPlayerId);
        if (turnPlayerIndex < 0)
        {
            return players[0];
        }

        return players[(turnPlayerIndex + 1) % players.Length];
    }

    private static string NextPlayerIdAfter(MatchState state, string playerId)
    {
        return PlayerIdsStartingAfter(state, playerId).FirstOrDefault()
            ?? state.TurnPlayerId;
    }

    private static string[] SeatPlayerIds(MatchState state)
    {
        return state.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .ToArray();
    }

    private static string[] ControllerAndOtherPlayerIds(MatchState state, string controllerId)
    {
        return new[] { controllerId }
            .Concat(SeatPlayerIds(state).Where(playerId => !string.Equals(playerId, controllerId, StringComparison.Ordinal)))
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] PlayerIdsStartingAfter(MatchState state, string playerId)
    {
        var players = SeatPlayerIds(state);
        if (players.Length == 0)
        {
            return [];
        }

        var playerIndex = Array.IndexOf(players, playerId);
        if (playerIndex < 0)
        {
            return players;
        }

        return players
            .Skip(playerIndex + 1)
            .Concat(players.Take(playerIndex + 1))
            .ToArray();
    }

    private static string NextUnpassedPlayerId(
        MatchState state,
        string currentPlayerId,
        IReadOnlySet<string> passedPlayerIds)
    {
        var players = SeatPlayerIds(state);
        if (players.Length == 0)
        {
            return currentPlayerId;
        }

        var currentIndex = Array.IndexOf(players, currentPlayerId);
        if (currentIndex < 0)
        {
            return players.First(playerId => !passedPlayerIds.Contains(playerId));
        }

        for (var offset = 1; offset <= players.Length; offset++)
        {
            var candidate = players[(currentIndex + offset) % players.Length];
            if (!passedPlayerIds.Contains(candidate))
            {
                return candidate;
            }
        }

        return currentPlayerId;
    }

    private static TemporaryControlReturnResult ReturnTemporaryControlAtEndTurn(MatchState state)
    {
        var playerZones = NormalizeZonesForSeats(state);
        var cardObjects = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        foreach (var (objectId, objectState) in state.CardObjects)
        {
            var returnEffectId = objectState.UntilEndOfTurnEffects.FirstOrDefault(effectId =>
                !string.IsNullOrWhiteSpace(effectId)
                && effectId.StartsWith(ReturnControlToOwnerAtTurnEndEffectPrefix, StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(returnEffectId))
            {
                continue;
            }

            var ownerPlayerId = returnEffectId[ReturnControlToOwnerAtTurnEndEffectPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(ownerPlayerId)
                || !playerZones.ContainsKey(ownerPlayerId))
            {
                continue;
            }

            var currentLocation = FindFieldObjectLocation(playerZones, objectId);
            if (currentLocation is null)
            {
                continue;
            }

            RemoveFieldObjectFromLocation(
                playerZones,
                currentLocation.Value.PlayerId,
                currentLocation.Value.Zone,
                objectId);
            AddFieldObjectToLocation(playerZones, ownerPlayerId, MoveUnitBaseZone, objectId);

            cardObjects[objectId] = objectState with
            {
                OwnerId = string.IsNullOrWhiteSpace(objectState.OwnerId) ? ownerPlayerId : objectState.OwnerId,
                ControllerId = ownerPlayerId,
                UntilEndOfTurnEffects = objectState.UntilEndOfTurnEffects
                    .Where(effectId => !string.Equals(effectId, returnEffectId, StringComparison.Ordinal))
                    .ToArray()
            };
            events.Add(new GameEvent(
                "UNIT_CONTROL_RETURNED",
                $"{ownerPlayerId} 取回单位控制权",
                new Dictionary<string, object?>
                {
                    ["targetObjectId"] = objectId,
                    ["ownerId"] = ownerPlayerId,
                    ["controllerId"] = ownerPlayerId,
                    ["previousControllerId"] = currentLocation.Value.PlayerId
                }));
            events.Add(new GameEvent(
                "UNIT_RECALLED_TO_OWNER_BASE",
                $"{ownerPlayerId} 将单位召回基地",
                new Dictionary<string, object?>
                {
                    ["targetObjectId"] = objectId,
                    ["ownerId"] = ownerPlayerId,
                    ["controllerId"] = ownerPlayerId,
                    ["originZone"] = currentLocation.Value.Zone,
                    ["destinationZone"] = MoveUnitBaseZone
                }));
        }

        return new TemporaryControlReturnResult(playerZones, cardObjects, events);
    }

    private static CleanupResult ApplyTurnEndCleanup(MatchState state)
    {
        var damagedObjectIds = new List<string>();
        var expiredEffectIds = new List<string>();
        var expiredPowerModifierObjectIds = new List<string>();
        var expiredGlobalEffectIds = state.UntilEndOfTurnEffects
            .Where(effectId => !string.IsNullOrWhiteSpace(effectId))
            .ToArray();
        var cardObjects = state.CardObjects.ToDictionary(
            entry => entry.Key,
            entry =>
            {
                var objectState = entry.Value;
                var untilEndEffects = objectState.UntilEndOfTurnEffects
                    .Where(effectId => !string.IsNullOrWhiteSpace(effectId))
                    .ToArray();
                if (objectState.Damage <= 0
                    && untilEndEffects.Length == 0
                    && objectState.UntilEndOfTurnPowerModifier == 0)
                {
                    return objectState;
                }

                if (objectState.Damage > 0)
                {
                    damagedObjectIds.Add(entry.Key);
                }
                expiredEffectIds.AddRange(untilEndEffects);
                if (objectState.UntilEndOfTurnPowerModifier != 0)
                {
                    expiredPowerModifierObjectIds.Add(entry.Key);
                }

                return objectState with
                {
                    Damage = 0,
                    UntilEndOfTurnEffects = [],
                    Power = Math.Max(0, objectState.Power - objectState.UntilEndOfTurnPowerModifier),
                    UntilEndOfTurnPowerModifier = 0
                };
            },
            StringComparer.Ordinal);

        return new CleanupResult(
            cardObjects,
            [],
            damagedObjectIds.OrderBy(objectId => objectId, StringComparer.Ordinal).ToArray(),
            expiredEffectIds
                .Concat(expiredGlobalEffectIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(effectId => effectId, StringComparer.Ordinal)
                .ToArray(),
            expiredPowerModifierObjectIds.OrderBy(objectId => objectId, StringComparer.Ordinal).ToArray(),
            damagedObjectIds.Count > 0
                || expiredEffectIds.Count > 0
                || expiredGlobalEffectIds.Length > 0
                || expiredPowerModifierObjectIds.Count > 0);
    }

    private static int RuneCallCount(MatchState state)
    {
        var baseRuneCount = IsSecondActionPlayersFirstTurn(state) ? 3 : 2;
        return baseRuneCount + BattlefieldFirstTurnExtraRuneCount(state);
    }

    private static BattlefieldStartDamageResult ApplyBattlefieldTurnStartDamageAllUnits(
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string turnPlayerId,
        long currentTick,
        IReadOnlyDictionary<string, RunePool> runePools)
    {
        var sourceObjectIds = playerZones
            .SelectMany(entry => entry.Value.Battlefields.Where(objectId =>
                cardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldTurnStartDamageAllUnitsCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, entry.Key)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        if (sourceObjectIds.Length == 0)
        {
            return BattlefieldStartDamageResult.Empty;
        }

        var targetObjectIds = playerZones
            .SelectMany(entry => entry.Value.Battlefields.Where(objectId =>
                cardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, entry.Key, objectId)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        if (targetObjectIds.Length == 0)
        {
            return BattlefieldStartDamageResult.Empty;
        }

        var damageAmount = sourceObjectIds.Length;
        var events = new List<GameEvent>
        {
            new(
                "BATTLEFIELD_TRIGGER_RESOLVED",
                $"{turnPlayerId} 回合开始时冰霜要塞造成伤害",
                new Dictionary<string, object?>
                {
                    ["playerId"] = turnPlayerId,
                    ["trigger"] = "BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS",
                    ["sourceObjectIds"] = sourceObjectIds,
                    ["targetObjectIds"] = targetObjectIds,
                    ["damage"] = damageAmount,
                    ["timing"] = MatchPhases.TurnStart,
                    ["beforeScoring"] = true
                })
        };

        var damageTriggeredDestroyTargetObjectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var targetObjectId in targetObjectIds)
        {
            var damageApplication = ApplyDamageToCardObject(
                cardObjects,
                targetObjectId,
                damageAmount,
                damageTriggeredDestroyTargetObjectIds);
            var payload = BuildDamagePayload(sourceObjectIds[0], targetObjectId, damageApplication);
            payload["sourceObjectIds"] = sourceObjectIds;
            payload["reason"] = "BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS";
            payload["timing"] = MatchPhases.TurnStart;
            events.Add(new GameEvent(
                "DAMAGE_APPLIED",
                $"{targetObjectId} 因冰霜要塞受到 {damageApplication.DamageAmount} 点伤害",
                payload));
        }

        var pseudoStackItem = new StackItemState(
            $"TURN-START-BATTLEFIELD-DAMAGE-{currentTick}",
            turnPlayerId,
            sourceObjectIds[0],
            "BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS",
            string.Empty,
            [],
            0);
        var lethalCleanup = RunStateBasedCleanupLoop(
            playerZones,
            cardObjects,
            pseudoStackItem,
            runePools,
            damageTriggeredDestroyTargetObjectIds: damageTriggeredDestroyTargetObjectIds);
        events.AddRange(lethalCleanup.Events);

        return new BattlefieldStartDamageResult(
            events,
            lethalCleanup.DestroyedUnitOwnerIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ownerId => ownerId, StringComparer.Ordinal)
                .ToArray(),
            lethalCleanup.RunePools);
    }

    private static BattlefieldStartDrawResult ApplyBattlefieldTurnStartDestroyUnitDraw(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        string turnPlayerId,
        long rngCursor)
    {
        var playerScores = NormalizeScoresForSeats(state);
        if (!playerZones.TryGetValue(turnPlayerId, out var zones))
        {
            return new BattlefieldStartDrawResult([], [], playerScores, null, rngCursor);
        }

        var sourceObjectIds = zones.Battlefields
            .Where(objectId => cardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldTurnStartDestroyUnitDrawCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, turnPlayerId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        if (sourceObjectIds.Length == 0)
        {
            return new BattlefieldStartDrawResult([], [], playerScores, null, rngCursor);
        }

        var targetObjectId = zones.Battlefields
            .Where(objectId => cardObjects.TryGetValue(objectId, out var cardObject)
                && cardObject.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && IsCardObjectControlledByPlayerOrLegacyOwned(cardObjects, turnPlayerId, objectId))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(targetObjectId))
        {
            return new BattlefieldStartDrawResult([], [], playerScores, null, rngCursor);
        }

        var events = new List<GameEvent>
        {
            new(
                "BATTLEFIELD_TRIGGER_RESOLVED",
                $"{turnPlayerId} 回合开始时摧毁单位并抽牌",
                new Dictionary<string, object?>
                {
                    ["playerId"] = turnPlayerId,
                    ["trigger"] = "BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW",
                    ["sourceObjectIds"] = sourceObjectIds,
                    ["targetObjectId"] = targetObjectId,
                    ["drawCount"] = 1,
                    ["timing"] = MatchPhases.TurnStart,
                    ["beforeScoring"] = true
                })
        };
        var pseudoStackItem = new StackItemState(
            $"TURN-START-BATTLEFIELD-DRAW-{state.Tick}",
            turnPlayerId,
            sourceObjectIds[0],
            "BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW",
            string.Empty,
            [],
            0);
        if (!TryDestroyTarget(playerZones, cardObjects, targetObjectId, out var removalResult))
        {
            return new BattlefieldStartDrawResult(events, [], playerScores, null, rngCursor);
        }

        events.Add(BuildFieldRemovalEvent(
            "暮色玫瑰实验室",
            pseudoStackItem,
            targetObjectId,
            removalResult,
            "BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW"));
        var destroyedUnitOwnerIds = removalResult.WasDestroyed && removalResult.WasUnit
            ? new[] { removalResult.OwnerPlayerId }
            : [];
        var drawApplication = ApplyDrawToPlayer(
            state with
            {
                PlayerZones = playerZones,
                PlayerScores = playerScores,
                CardObjects = cardObjects
            },
            playerZones,
            playerScores,
            turnPlayerId,
            1,
            rngCursor,
            events);

        return new BattlefieldStartDrawResult(
            events,
            destroyedUnitOwnerIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ownerId => ownerId, StringComparer.Ordinal)
                .ToArray(),
            drawApplication.PlayerScores,
            drawApplication.WinnerPlayerId,
            drawApplication.RngCursor);
    }

    private static ScoreApplicationResult ApplyBattlefieldFirstTurnScore(MatchState state, string playerId)
    {
        var playerScores = NormalizeScoresForSeats(state);
        if (!IsTurnPlayersFirstTurn(state))
        {
            return new ScoreApplicationResult(playerScores, null, []);
        }

        var sourceObjectIds = state.PlayerZones
            .SelectMany(entry => entry.Value.Battlefields.Where(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldFirstTurnScoreCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, entry.Key)))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        if (sourceObjectIds.Length == 0)
        {
            return new ScoreApplicationResult(playerScores, null, []);
        }

        if (TryBuildBattlefieldScorePreventedEvent(
                state,
                playerId,
                "BATTLEFIELD_FIRST_TURN_GAIN_SCORE",
                sourceObjectIds,
                out var scorePreventedEvent)
            && scorePreventedEvent is not null)
        {
            return new ScoreApplicationResult(playerScores, null, [scorePreventedEvent]);
        }

        playerScores[playerId] = playerScores.TryGetValue(playerId, out var score)
            ? score + sourceObjectIds.Length
            : sourceObjectIds.Length;
        var events = new List<GameEvent>
        {
            new(
                "BATTLEFIELD_TRIGGER_RESOLVED",
                $"{playerId} 因战场获得分数",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["trigger"] = "BATTLEFIELD_FIRST_TURN_GAIN_SCORE",
                    ["sourceObjectIds"] = sourceObjectIds,
                    ["amount"] = sourceObjectIds.Length,
                    ["score"] = playerScores[playerId]
                }),
            new(
                "SCORE_GAINED",
                $"{playerId} 获得 {sourceObjectIds.Length} 分",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["amount"] = sourceObjectIds.Length,
                    ["score"] = playerScores[playerId],
                    ["reason"] = "BATTLEFIELD_FIRST_TURN_GAIN_SCORE",
                    ["sourceObjectIds"] = sourceObjectIds
                })
        };
        return new ScoreApplicationResult(
            playerScores,
            WinningPlayerId(playerScores, EffectiveWinningScore(state)),
            events);
    }

    private static bool TryBuildBattlefieldScorePreventedEvent(
        MatchState state,
        string playerId,
        string preventedReason,
        IReadOnlyList<string> scoreSourceObjectIds,
        out GameEvent? scorePreventedEvent)
    {
        scorePreventedEvent = null;
        var turnOrdinal = PlayerTurnOrdinal(state, playerId);
        if (turnOrdinal < 0
            || turnOrdinal >= BattlefieldScoreDelayReleasedTurnOrdinal)
        {
            return false;
        }

        var sourceObjectIds = state.PlayerZones
            .SelectMany(entry => entry.Value.Battlefields.Where(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldScoreDelayCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, entry.Key)))
            .OrderBy(objectId => objectId, StringComparer.Ordinal)
            .ToArray();
        if (sourceObjectIds.Length == 0)
        {
            return false;
        }

        scorePreventedEvent = new GameEvent(
            "BATTLEFIELD_SCORE_PREVENTED",
            $"{playerId} 尚未进入第 3 回合，遗忘丰碑阻止其从战场获得分数",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["trigger"] = "BATTLEFIELD_SCORE_DELAY_UNTIL_THIRD_TURN",
                ["sourceObjectIds"] = sourceObjectIds,
                ["preventedReason"] = preventedReason,
                ["scoreSourceObjectIds"] = scoreSourceObjectIds.ToArray(),
                ["turnOrdinal"] = turnOrdinal,
                ["releasedTurnOrdinal"] = BattlefieldScoreDelayReleasedTurnOrdinal
            });
        return true;
    }

    private static int PlayerTurnOrdinal(MatchState state, string playerId)
    {
        var players = SeatPlayerIds(state);
        var playerIndex = Array.IndexOf(players, playerId);
        if (players.Length == 0
            || playerIndex < 0)
        {
            return -1;
        }

        if (state.TurnNumber < playerIndex + 1)
        {
            return 0;
        }

        return ((state.TurnNumber - (playerIndex + 1)) / players.Length) + 1;
    }

    private static bool IsSecondActionPlayersFirstTurn(MatchState state)
    {
        if (state.TurnNumber != 2)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(state.OpeningSecondActionPlayerId))
        {
            return string.Equals(state.TurnPlayerId, state.OpeningSecondActionPlayerId, StringComparison.Ordinal);
        }

        return state.Seats.TryGetValue(state.TurnPlayerId, out var seat)
            && string.Equals(seat, "P2", StringComparison.Ordinal);
    }

    private static int BattlefieldFirstTurnExtraRuneCount(MatchState state)
    {
        if (!IsTurnPlayersFirstTurn(state))
        {
            return 0;
        }

        return state.PlayerZones
            .Sum(entry => entry.Value.Battlefields.Count(objectId =>
                state.CardObjects.TryGetValue(objectId, out var cardObject)
                && IsBattlefieldFirstTurnExtraRuneCardNo(cardObject.CardNo)
                && SourceObjectControlledByPlayerOrLegacyOwned(cardObject, entry.Key)));
    }

    private static bool IsTurnPlayersFirstTurn(MatchState state)
    {
        var players = SeatPlayerIds(state);
        var turnPlayerIndex = Array.IndexOf(players, state.TurnPlayerId);
        return turnPlayerIndex >= 0
            && state.TurnNumber == turnPlayerIndex + 1;
    }

    private static DrawResult DrawOne(MatchState state, string playerId, PlayerZones zones)
    {
        var playerScores = NormalizeScoresForSeats(state);
        var mainDeck = zones.MainDeck.ToList();
        var graveyard = zones.Graveyard.ToList();
        var drawnCards = new List<string>();
        var burnouts = new List<BurnoutResult>();
        string? winnerPlayerId = null;
        var remainingDrawCount = 1;
        var rngCursor = state.RngCursor;

        while (remainingDrawCount > 0 && winnerPlayerId is null)
        {
            if (mainDeck.Count > 0)
            {
                var topCardObjectId = mainDeck[0];
                if (!IsCardObjectControlledByPlayerOrLegacyOwned(state.CardObjects, playerId, topCardObjectId))
                {
                    break;
                }

                drawnCards.Add(topCardObjectId);
                mainDeck.RemoveAt(0);
                remainingDrawCount--;
                continue;
            }

            var opponentId = OpponentOf(state, playerId);
            if (opponentId is null)
            {
                break;
            }

            if (graveyard.Count > 0)
            {
                var recyclableCards = graveyard
                    .Where(objectId => IsCardObjectControlledByPlayerOrLegacyOwned(state.CardObjects, playerId, objectId))
                    .ToArray();
                var recycledCards = RandomizeForMainDeckBottom(
                    recyclableCards,
                    state.Seed,
                    rngCursor,
                    $"BURNOUT:{playerId}");
                if (recyclableCards.Length > 1)
                {
                    rngCursor++;
                }

                mainDeck.AddRange(recycledCards);
                var recycledCardIds = recycledCards.ToHashSet(StringComparer.Ordinal);
                graveyard.RemoveAll(objectId => recycledCardIds.Contains(objectId));
            }

            playerScores[opponentId] = playerScores.TryGetValue(opponentId, out var score) ? score + 1 : 1;
            burnouts.Add(new BurnoutResult(opponentId, playerScores[opponentId]));
            winnerPlayerId = WinningPlayerId(playerScores, EffectiveWinningScore(state));
        }

        return new DrawResult(
            mainDeck.ToArray(),
            graveyard.ToArray(),
            drawnCards.ToArray(),
            burnouts,
            winnerPlayerId,
            playerScores,
            rngCursor,
            EffectiveWinningScore(state));
    }

    private static string? WinningPlayerId(IReadOnlyDictionary<string, int> playerScores, int winningScore)
    {
        return playerScores
            .Where(candidate => candidate.Value >= winningScore
                && playerScores
                    .Where(other => !string.Equals(other.Key, candidate.Key, StringComparison.Ordinal))
                    .All(other => candidate.Value > other.Value))
            .Select(candidate => candidate.Key)
            .FirstOrDefault();
    }

    private static Dictionary<string, int> NormalizeScoresForSeats(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            playerId => state.PlayerScores.TryGetValue(playerId, out var score) ? score : 0,
            StringComparer.Ordinal);
    }

    private static Dictionary<string, int> GainExperience(
        IReadOnlyDictionary<string, int> currentExperience,
        string playerId,
        int amount,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        var playerExperience = currentExperience.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        playerExperience[playerId] = playerExperience.TryGetValue(playerId, out var current)
            ? current + amount
            : amount;
        events.Add(new GameEvent(
            "EXPERIENCE_GAINED",
            $"{playerId} 获得 {amount} 经验",
            new Dictionary<string, object?>
            {
                ["playerId"] = playerId,
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["cardNo"] = stackItem.CardNo,
                ["amount"] = amount,
                ["totalExperience"] = playerExperience[playerId]
            }));

        return playerExperience;
    }

    private static Dictionary<string, int> NormalizeExperienceForSeats(MatchState state)
    {
        return state.Seats.Keys.ToDictionary(
            playerId => playerId,
            playerId => state.PlayerExperience.TryGetValue(playerId, out var experience) ? experience : 0,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> PayExperienceCosts(
        MatchState state,
        string playerId,
        int experienceCost)
    {
        if (experienceCost <= 0)
        {
            return state.PlayerExperience;
        }

        var playerExperience = NormalizeExperienceForSeats(state);
        playerExperience[playerId] = Math.Max(
            0,
            playerExperience.TryGetValue(playerId, out var currentExperience)
                ? currentExperience - experienceCost
                : 0);
        return playerExperience;
    }

    private static IReadOnlyDictionary<string, int> IncrementPlayerCardsPlayedThisTurn(
        MatchState state,
        string playerId)
    {
        var playerCardsPlayedThisTurn = state.PlayerCardsPlayedThisTurn.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal);
        playerCardsPlayedThisTurn[playerId] = playerCardsPlayedThisTurn.TryGetValue(playerId, out var count)
            ? count + 1
            : 1;
        return playerCardsPlayedThisTurn;
    }

    private static string? OpponentOf(MatchState state, string playerId)
    {
        return state.Seats
            .OrderBy(entry => entry.Value, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .FirstOrDefault(candidate => !string.Equals(candidate, playerId, StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, ActionPromptDto> BuildCorePrompts(MatchState state)
    {
        if (state.Status != MatchStatuses.InProgress)
        {
            return ResolutionResult.BuildPrompts(state);
        }

        if (string.Equals(state.Phase, MatchPhases.Mulligan, StringComparison.Ordinal))
        {
            return ResolutionResult.BuildPrompts(state);
        }

        if (state.StackItems.Count > 0 && !string.IsNullOrWhiteSpace(state.PriorityPlayerId))
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal)
                    ? "当前玩家可让过优先行动权"
                    : "等待对手优先行动",
                string.Equals(playerId, state.PriorityPlayerId, StringComparison.Ordinal)
                    ? WithSurrender(ActionPromptBuilder.StackPriorityActions(state, playerId))
                    : WithSurrender("WAIT")));
        }

        if (string.Equals(state.TimingState, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(state.FocusPlayerId))
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal),
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal)
                    ? "当前玩家可让过焦点"
                    : "等待对手焦点行动",
                string.Equals(playerId, state.FocusPlayerId, StringComparison.Ordinal)
                    ? WithSurrender(ActionPromptBuilder.SpellDuelFocusActions(state, playerId))
                    : WithSurrender("WAIT")));
        }

        if (ResolutionResult.ActiveStartBattleTask(state) is not null)
        {
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId =>
            {
                var canDeclareBattle = string.Equals(playerId, state.ActivePlayerId, StringComparison.Ordinal)
                    && ActionPromptBuilder.CanDeclareBattleForActiveTask(state, playerId);
                return ActionPromptBuilder.Build(
                    state,
                    playerId,
                    canDeclareBattle,
                    canDeclareBattle
                        ? "请为争夺战场声明战斗"
                        : "等待对手处理争夺战场战斗任务",
                    canDeclareBattle ? WithSurrender("DECLARE_BATTLE") : WithSurrender("WAIT"));
            });
        }

        if (ResolutionResult.HasBlockingPendingTaskQueue(state))
        {
            var reason = ResolutionResult.BlockingPendingTaskQueueReason(state);
            return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
                state,
                playerId,
                false,
                reason,
                WithSurrender("WAIT")));
        }

        return state.Seats.Keys.ToDictionary(playerId => playerId, playerId => ActionPromptBuilder.Build(
            state,
            playerId,
            playerId == state.ActivePlayerId,
            playerId == state.ActivePlayerId ? "当前玩家普通开环行动" : "等待对手行动",
            playerId == state.ActivePlayerId ? WithSurrender(ImplementedMainOpenActions(state, playerId)) : WithSurrender("WAIT")));
    }

    private static IReadOnlyList<string> WithSurrender(params string[] actions)
    {
        return WithSurrender((IReadOnlyList<string>)actions);
    }

    private static IReadOnlyList<string> WithSurrender(IReadOnlyList<string> actions)
    {
        return actions.Contains("SURRENDER", StringComparer.Ordinal)
            ? actions
            : actions.Concat(["SURRENDER"]).ToArray();
    }

    private static IReadOnlyList<string> ImplementedMainOpenActions(MatchState state, string playerId)
    {
        var reason = "当前玩家普通开环行动";
        var implementedSourceDrivenActions = new[]
        {
            "PLAY_CARD",
            "ACTIVATE_ABILITY",
            "ASSEMBLE_EQUIPMENT",
            "MOVE_UNIT",
            "DECLARE_BATTLE",
            "HIDE_CARD",
            "REVEAL_CARD",
            "TAP_RUNE",
            "RECYCLE_RUNE",
            "LEGEND_ACT"
        };
        var actions = implementedSourceDrivenActions
            .Where(action =>
            {
                var prompt = ActionPromptBuilder.Build(state, playerId, true, reason, [action]);
                var candidate = prompt.Candidates?.FirstOrDefault();
                return candidate?.Enabled == true;
            })
            .ToList();

        actions.Add("END_TURN");
        return actions;
    }

    private static IReadOnlyList<GameEvent> BuildTurnEndEvents(
        MatchState state,
        string playerId,
        string nextTurnPlayerId,
        CleanupResult cleanupResult,
        IReadOnlyList<GameEvent> cleanupEvents)
    {
        List<GameEvent> events =
        [
            new GameEvent(
                "TURN_END_DECLARED",
                $"{playerId} 声明结束回合",
                new Dictionary<string, object?>
                {
                    ["playerId"] = playerId,
                    ["turnPlayerId"] = state.TurnPlayerId
                }),
            new GameEvent(
                "TURN_END_CLEANUP_STARTED",
                $"{state.TurnPlayerId} 回合结束特殊清理开始",
                new Dictionary<string, object?>
                {
                    ["turnPlayerId"] = state.TurnPlayerId,
                    ["phase"] = state.Phase
                })
        ];

        events.AddRange(cleanupEvents);

        if (cleanupResult.DamagedObjectIds.Count > 0)
        {
            events.Add(new GameEvent(
                "DAMAGE_REMOVED",
                "回合结束特殊清理移除单位伤害",
                new Dictionary<string, object?>
                {
                    ["objectIds"] = cleanupResult.DamagedObjectIds.ToArray(),
                    ["count"] = cleanupResult.DamagedObjectIds.Count
                }));
        }

        if (cleanupResult.ExpiredEffectIds.Count > 0)
        {
            events.Add(new GameEvent(
                "UNTIL_END_OF_TURN_EXPIRED",
                "期限为本回合内的效果同时失效",
                new Dictionary<string, object?>
                {
                    ["effectIds"] = cleanupResult.ExpiredEffectIds.ToArray(),
                    ["count"] = cleanupResult.ExpiredEffectIds.Count
                }));
        }

        if (cleanupResult.ExpiredPowerModifierObjectIds.Count > 0)
        {
            events.Add(new GameEvent(
                "POWER_MODIFIER_EXPIRED",
                "期限为本回合内的战力修正失效",
                new Dictionary<string, object?>
                {
                    ["objectIds"] = cleanupResult.ExpiredPowerModifierObjectIds.ToArray(),
                    ["count"] = cleanupResult.ExpiredPowerModifierObjectIds.Count
                }));
        }

        events.Add(new GameEvent(
            "RUNE_POOL_CLEARED",
            "回合结束时所有玩家的符文池已清空",
            new Dictionary<string, object?>
            {
                ["playerIds"] = state.Seats.Keys.ToArray(),
                ["timing"] = MatchPhases.TurnEnd
            }));

        if (cleanupResult.RequiresFollowUpCleanup)
        {
            events.Add(new GameEvent(
                "CLEANUP_REPEATED",
                "特殊清理造成状态变化后进行一次常规清理检查",
                new Dictionary<string, object?>
                {
                    ["cleanupKind"] = "NORMAL",
                    ["previousCleanupKind"] = "TURN_END_SPECIAL"
                }));
        }

        events.Add(new GameEvent(
                "TURN_PLAYER_ADVANCED",
                $"回合推进至 {nextTurnPlayerId}",
                new Dictionary<string, object?>
                {
                    ["previousTurnPlayerId"] = state.TurnPlayerId,
                    ["turnPlayerId"] = nextTurnPlayerId,
                    ["turnNumber"] = state.TurnNumber + 1
                }));

        return events;
    }

    private static IReadOnlyList<GameEvent> BuildTurnStartEvents(
        MatchState state,
        int calledRuneCount,
        DrawResult drawResult,
        IReadOnlyList<GameEvent> preRuneCallEvents)
    {
        List<GameEvent> events =
        [
            new GameEvent(
                "TURN_START_BEGAN",
                $"{state.TurnPlayerId} 开始回合开始流程",
                new Dictionary<string, object?>
                {
                    ["turnPlayerId"] = state.TurnPlayerId
                })
        ];

        events.AddRange(preRuneCallEvents);
        events.Add(
            new GameEvent(
                "RUNES_CALLED",
                $"{state.TurnPlayerId} 召出 {calledRuneCount} 张符文",
                new Dictionary<string, object?>
                {
                    ["playerId"] = state.TurnPlayerId,
                    ["count"] = calledRuneCount
                }));

        foreach (var (burnout, index) in drawResult.Burnouts.Select((burnout, index) => (burnout, index)))
        {
            events.Add(new GameEvent(
                "BURNOUT_APPLIED",
                $"{state.TurnPlayerId} 执行燃尽",
                new Dictionary<string, object?>
                {
                    ["playerId"] = state.TurnPlayerId,
                    ["scoredPlayerId"] = burnout.ScoredPlayerId,
                    ["scoredPlayerScore"] = burnout.ScoredPlayerScore,
                    ["burnoutIndex"] = index + 1
                }));
        }

        if (drawResult.WinnerPlayerId is not null)
        {
            events.Add(new GameEvent(
                "MATCH_WON",
                $"{drawResult.WinnerPlayerId} 达到获胜分数并获胜",
                new Dictionary<string, object?>
                {
                    ["winnerPlayerId"] = drawResult.WinnerPlayerId,
                    ["winningScore"] = drawResult.WinningScore
                }));
            return events;
        }

        events.Add(new GameEvent(
            "CARD_DRAWN",
            $"{state.TurnPlayerId} 抽 {drawResult.DrawnCards.Count} 张牌",
            new Dictionary<string, object?>
            {
                ["playerId"] = state.TurnPlayerId,
                ["count"] = drawResult.DrawnCards.Count
            }));
        events.Add(new GameEvent(
            "RUNE_POOL_CLEARED",
            "所有玩家的符文池已清空",
            new Dictionary<string, object?>
            {
                ["playerIds"] = state.Seats.Keys.ToArray()
            }));
        events.Add(new GameEvent(
            "MAIN_PHASE_BEGAN",
            $"{state.TurnPlayerId} 进入主阶段",
            new Dictionary<string, object?>
            {
                ["turnPlayerId"] = state.TurnPlayerId
            }));

        return events;
    }

    private sealed record DrawResult(
        IReadOnlyList<string> MainDeck,
        IReadOnlyList<string> Graveyard,
        IReadOnlyList<string> DrawnCards,
        IReadOnlyList<BurnoutResult> Burnouts,
        string? WinnerPlayerId,
        IReadOnlyDictionary<string, int> PlayerScores,
        long RngCursor,
        int WinningScore);

    private sealed record EphemeralCleanupResult(
        IReadOnlyList<GameEvent> Events,
        IReadOnlyList<string> DestroyedUnitOwnerIds)
    {
        public static EphemeralCleanupResult Empty { get; } = new([], []);
    }

    private sealed record BattlefieldStartDamageResult(
        IReadOnlyList<GameEvent> Events,
        IReadOnlyList<string> DestroyedUnitOwnerIds,
        IReadOnlyDictionary<string, RunePool> RunePools)
    {
        public static BattlefieldStartDamageResult Empty { get; } = new(
            [],
            [],
            new Dictionary<string, RunePool>(StringComparer.Ordinal));
    }

    private sealed record BattlefieldStartDrawResult(
        IReadOnlyList<GameEvent> Events,
        IReadOnlyList<string> DestroyedUnitOwnerIds,
        IReadOnlyDictionary<string, int> PlayerScores,
        string? WinnerPlayerId,
        long RngCursor);

    private sealed record DrawApplicationResult(
        IReadOnlyDictionary<string, int> PlayerScores,
        string? WinnerPlayerId,
        long RngCursor);

    private sealed record ScoreApplicationResult(
        IReadOnlyDictionary<string, int> PlayerScores,
        string? WinnerPlayerId,
        IReadOnlyList<GameEvent> Events);

    private sealed record BurnoutResult(
        string ScoredPlayerId,
        int ScoredPlayerScore);

    private sealed record DamageApplicationResult(
        int DamageAmount,
        int OriginalDamageAmount,
        bool Prevented,
        string PreventionEffectId);

    private sealed record BattleDamageAssignmentTarget(
        string ObjectId,
        string Role);

    private sealed record LegendAbilityDefinition(
        string AbilityId,
        IReadOnlyList<string> SourceCardNos,
        string DisplayName,
        int ManaCost,
        int ExperienceCost,
        string RequiredCostToken,
        int RequiredTargetCount,
        bool RequiresFriendlyUnitTarget,
        string EffectKind,
        bool RequiresBattlefieldTarget = false,
        bool RequiresExhaustedTarget = false,
        bool RequiresArmamentSecondTarget = false,
        bool RequiresUnattachedArmamentSecondTarget = false,
        bool RequiresAttachedArmamentSecondTarget = false,
        bool RequiresDifferentArmamentHost = false,
        bool RequiresPlayedAnotherCardThisTurn = false,
        bool RequiresPlayedArmamentThisTurn = false,
        bool RequiresOwnedTeemoUnitTarget = false,
        string ManaCostReductionKind = "",
        int ManaGainAmount = 0,
        int PowerGainAmount = 0,
        string TimingKind = LegendAbilityTimingKinds.MainOpen,
        bool RequiresPendingSpellStackItem = false,
        bool RequiresPendingEquipmentStackItem = false,
        bool RequiresEzrealEnemyTargetsThisTurn = false,
        bool RequiresPendingFriendlyUnitTarget = false,
        string RequiredControlledBattlefieldCardNo = "");

    private static class LegendAbilityEffectKinds
    {
        public const string DrawOne = "DRAW_ONE";
        public const string MoveFriendlyUnit = "MOVE_FRIENDLY_UNIT";
        public const string GrantBoon = "GRANT_BOON";
        public const string GrantRoam = "GRANT_ROAM";
        public const string ReturnBattlefieldUnitAndCreateCoin = "RETURN_BATTLEFIELD_UNIT_AND_CREATE_COIN";
        public const string AttachArmament = "ATTACH_ARMAMENT";
        public const string ReattachArmament = "REATTACH_ARMAMENT";
        public const string GainMana = "GAIN_MANA";
        public const string GainPower = "GAIN_POWER";
        public const string ReadyFriendlyUnit = "READY_FRIENDLY_UNIT";
        public const string ReturnOwnedTeemoUnitToHand = "RETURN_OWNED_TEEMO_UNIT_TO_HAND";
        public const string CreateSandSoldier = "CREATE_SAND_SOLDIER";
        public const string CreateMinion = "CREATE_MINION";
        public const string CreateFaerie = "CREATE_FAERIE";
    }

    private static class LegendAbilityTimingKinds
    {
        public const string MainOpen = "MAIN_OPEN";
        public const string PriorityWindow = "PRIORITY_WINDOW";
        public const string SpellDuelFocus = "SPELL_DUEL_FOCUS";
    }

    private static class LegendAbilityManaCostReductionKinds
    {
        public const string FriendlyEphemeralFieldObjects = "FRIENDLY_EPHEMERAL_FIELD_OBJECTS";
    }

    private sealed record PlayCardPlan(
        CardBehaviorDefinition Behavior,
        PlayerZones SourceZones,
        IReadOnlyList<string> TargetObjectIds,
        int TotalManaCost,
        int AnyPowerCost,
        IReadOnlyDictionary<string, int> PowerCostByTrait,
        int TotalPowerCost,
        int TotalExperienceCost,
        int EffectRepeatCount,
        IReadOnlyList<string> OptionalCosts,
        int CostReductionMana,
        int OptionalCostManaReduction,
        int BattlefieldEchoCostReductionMana,
        int BattlefieldEquipmentCostReductionMana,
        int BattlefieldHeldUnitCostIncreaseMana,
        int SpellshieldTaxMana,
        IReadOnlyList<string> SpellshieldTaxTargetObjectIds,
        IReadOnlyList<string> ExhaustedOptionalCostTargetObjectIds,
        IReadOnlyList<string> DestroyedAdditionalCostTargetObjectIds,
        IReadOnlyList<string> ReturnedAdditionalCostTargetObjectIds,
        IReadOnlyList<string> DiscardedOptionalCostTargetObjectIds,
        IReadOnlyList<string> PaymentResourceActions,
        IReadOnlyList<string> RecycledPaymentRuneObjectIds,
        string RengarUnitPlayedTargetObjectId,
        string LeonaStunBoonTargetObjectId);

    private sealed record StackResolutionResult(
        IReadOnlyDictionary<string, PlayerZones> PlayerZones,
        IReadOnlyDictionary<string, CardObjectState> CardObjects,
        IReadOnlyDictionary<string, int> PlayerScores,
        IReadOnlyDictionary<string, int> PlayerExperience,
        IReadOnlyDictionary<string, RunePool> RunePools,
        IReadOnlyList<string> UntilEndOfTurnEffects,
        string? WinnerPlayerId,
        IReadOnlyList<GameEvent> Events,
        IReadOnlyList<string> DestroyedUnitOwnerIds,
        IReadOnlyList<StackItemState>? StackItems,
        IReadOnlyList<string> CounteredStackItemIds,
        string? ExtraTurnPlayerId,
        long RngCursor);

    private sealed record RecycleResult(
        IReadOnlyList<GameEvent> Events,
        long RngCursor);

    private sealed record RuneCallResult(
        IReadOnlyList<string> CalledRuneObjectIds);

    private sealed record JhinHighCostSpellTriggerResult(
        bool HandledSourceMovement,
        IReadOnlyList<GameEvent> Events,
        IReadOnlyDictionary<string, int> PlayerScores,
        string? WinnerPlayerId,
        long RngCursor);

    private sealed record FieldRemovalResult(
        string OwnerPlayerId,
        string DestinationZone,
        bool WasBanished,
        bool WasRecalledToBase,
        bool WasEquipment,
        bool WasUnit,
        IReadOnlyList<string> DetachedEquipmentObjectIds)
    {
        public bool WasDestroyed => !WasBanished && !WasRecalledToBase;

        public static FieldRemovalResult Empty { get; } = new(string.Empty, string.Empty, false, false, false, false, []);
    }

    private sealed record LethalDamageCleanupResult(
        IReadOnlyList<GameEvent> Events,
        IReadOnlyList<string> DestroyedObjectIds,
        IReadOnlyList<string> DestroyedUnitOwnerIds,
        IReadOnlyDictionary<string, RunePool> RunePools);

    private sealed record IllegalStandbyCleanupResult(
        IReadOnlyList<GameEvent> Events,
        IReadOnlyList<string> RemovedObjectIds)
    {
        public static IllegalStandbyCleanupResult Empty { get; } = new([], []);
    }

    private sealed record StateBasedCleanupResult(
        IReadOnlyList<GameEvent> Events,
        IReadOnlyList<string> DestroyedObjectIds,
        IReadOnlyList<string> DestroyedUnitOwnerIds,
        IReadOnlyDictionary<string, RunePool> RunePools);

    private sealed record TemporaryControlReturnResult(
        IReadOnlyDictionary<string, PlayerZones> PlayerZones,
        IReadOnlyDictionary<string, CardObjectState> CardObjects,
        IReadOnlyList<GameEvent> Events);

    private sealed record CleanupResult(
        IReadOnlyDictionary<string, CardObjectState> CardObjects,
        IReadOnlyList<string> UntilEndOfTurnEffects,
        IReadOnlyList<string> DamagedObjectIds,
        IReadOnlyList<string> ExpiredEffectIds,
        IReadOnlyList<string> ExpiredPowerModifierObjectIds,
        bool RequiresFollowUpCleanup);
}
