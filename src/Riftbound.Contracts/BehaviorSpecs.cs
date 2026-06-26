namespace Riftbound.Contracts;

public static class BehaviorImplementationStatuses
{
    public const string Implemented = "implemented";
    public const string ManualRuleRequired = "manual-rule-required";
    public const string Unimplemented = "unimplemented";
}

public static class BehaviorConformanceTiers
{
    public const string RepresentativeRulePass = "representative-rule-pass";
    public const string FixturePass = "fixture-pass";
    public const string FullOfficialRulePass = "full-official-rule-pass";
    public const string ManualBoundary = "manual-boundary";
    public const string Blocked = "blocked";
}

public static class BehaviorTemplateIds
{
    public const string Draw = "draw";
    public const string Damage = "damage";
    public const string Destroy = "destroy";
    public const string Move = "move";
    public const string Recall = "recall";
    public const string Recycle = "recycle";
    public const string Banish = "banish";
    public const string Stun = "stun";
    public const string TempMight = "temp_might";
    public const string Boon = "boon";
    public const string GainExperience = "gain_experience";
    public const string Assemble = "assemble";
    public const string Echo = "echo";
    public const string Ambush = "ambush";
}

public static class TriggerKinds
{
    public const string BattlefieldUnitMovedAwayPowerModifier =
        "BATTLEFIELD_UNIT_MOVED_AWAY_POWER_MODIFIER";
    public const string BattlefieldHeldNextSpellEcho =
        "BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO";
    public const string BattlefieldHeldUnitCostIncrease =
        "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE";
    public const string BattlefieldHeldDrawOne =
        "BATTLEFIELD_HELD_DRAW_ONE";
    public const string BattlefieldHeldCallRune =
        "BATTLEFIELD_HELD_CALL_RUNE";
    public const string BattlefieldHeldEachPlayerCallRune =
        "BATTLEFIELD_HELD_EACH_PLAYER_CALL_RUNE";
    public const string BattlefieldHeldMoveUnitToBase =
        "BATTLEFIELD_HELD_MOVE_UNIT_TO_BASE";
    public const string BattlefieldDefendMoveFriendlyUnitToBase =
        "BATTLEFIELD_DEFENSE_MOVE_FRIENDLY_UNIT_TO_BASE";
    public const string BattlefieldDefendGrantSteadfast =
        "BATTLEFIELD_DEFENSE_GRANT_STEADFAST_TWO";
    public const string BattlefieldHeldGrantBoon =
        "BATTLEFIELD_HELD_GRANT_BOON";
    public const string BattlefieldHeldCreateMinion =
        "BATTLEFIELD_HELD_CREATE_MINION";
    public const string BattlefieldHeldReturnHero =
        "BATTLEFIELD_HELD_RETURN_HERO_FROM_GRAVEYARD";
    public const string BattlefieldHeldSevenUnitsWin =
        "BATTLEFIELD_HELD_SEVEN_UNITS_WIN";
    public const string BattlefieldHeldPayPowerScore =
        "BATTLEFIELD_HELD_PAY_4_POWER_GAIN_SCORE";
    public const string BattlefieldConquerRevealRecycle =
        "BATTLEFIELD_CONQUERED_REVEAL_TOP_TWO_RECYCLE";
    public const string BattlefieldConquerMill =
        "BATTLEFIELD_CONQUERED_MILL_TOP_TWO";
    public const string BattlefieldConquerRecycleRune =
        "BATTLEFIELD_CONQUERED_RECYCLE_RUNE";
    public const string BattlefieldConquerConsumeBoonDraw =
        "BATTLEFIELD_CONQUERED_CONSUME_BOON_DRAW";
    public const string BattlefieldConquerDiscardDraw =
        "BATTLEFIELD_CONQUERED_DISCARD_DRAW";
    public const string BattlefieldConquerDrawForOtherBattlefields =
        "BATTLEFIELD_CONQUERED_DRAW_FOR_OTHER_BATTLEFIELDS";
    public const string BattlefieldConquerPowerfulPayDraw =
        "BATTLEFIELD_CONQUERED_POWERFUL_PAY_1_DRAW";
    public const string BattlefieldConquerReadyRunesAtEnd =
        "BATTLEFIELD_CONQUERED_READY_RUNES_AT_END";
    public const string BattlefieldConquerReadyEquipment =
        "BATTLEFIELD_CONQUERED_READY_EQUIPMENT";
    public const string BattlefieldConquerPayCreateGold =
        "BATTLEFIELD_CONQUERED_PAY_1_CREATE_GOLD";
    public const string BattlefieldConquerPayReturnUnitCreateSandSoldier =
        "BATTLEFIELD_CONQUERED_PAY_1_RETURN_UNIT_CREATE_SAND_SOLDIER";
    public const string BattlefieldConquerPayReadyLegend =
        "BATTLEFIELD_CONQUERED_PAY_1_READY_LEGEND";
    public const string BattlefieldDefendRevealTopDrawSpellOrRecycle =
        "BATTLEFIELD_DEFENSE_REVEAL_TOP_DRAW_SPELL_OR_RECYCLE";
    public const string BattlefieldConquerOverkillCreateWarhawk =
        "BATTLEFIELD_CONQUERED_OVERKILL_CREATE_WARHAWK";
    public const string BattlefieldFriendlySpellDraw =
        "BATTLEFIELD_FRIENDLY_SPELL_DRAW_ONE";
    public const string BattlefieldSpellPowerBonus =
        "BATTLEFIELD_SPELL_POWER_PLUS_1";
    public const string BattlefieldHighCostSpellInsightRecycle =
        "BATTLEFIELD_HIGH_COST_SPELL_INSIGHT_RECYCLE";
    public const string BattlefieldPlayUnitPayBoon =
        "BATTLEFIELD_PLAY_UNIT_PAY_1_GRANT_BOON";
    public const string BattlefieldUnitReturnedPayCallRune =
        "BATTLEFIELD_UNIT_RETURNED_PAY_1_CALL_RUNE";
    public const string BattlefieldFirstUnitPlayedMoveOtherToBase =
        "BATTLEFIELD_FIRST_UNIT_PLAYED_MOVE_OTHER_TO_BASE";
    public const string BattlefieldTurnStartDamageAllUnits =
        "BATTLEFIELD_TURN_START_DAMAGE_ALL_UNITS";
    public const string BattlefieldTurnStartDestroyUnitDraw =
        "BATTLEFIELD_TURN_START_DESTROY_UNIT_DRAW";
    public const string BattlefieldFirstTurnExtraRune =
        "BATTLEFIELD_FIRST_TURN_EXTRA_RUNE";
    public const string BattlefieldFirstTurnScore =
        "BATTLEFIELD_FIRST_TURN_GAIN_SCORE";
    public const string BattlefieldHeldActivateUnitConquestEffects =
        "BATTLEFIELD_HELD_ACTIVATE_UNIT_CONQUEST_EFFECTS";
    public const string UnitConquestDrawOne =
        "UNIT_CONQUEST_DRAW_ONE";
    public const string UnitConquestDrawOneOrCallRune =
        "UNIT_CONQUEST_DRAW_ONE_OR_CALL_RUNE";
    public const string UnitConquestCreateDormantGold =
        "UNIT_CONQUEST_CREATE_DORMANT_GOLD";
    public const string UnitMovedCreateDormantGold =
        "TREASURE_HUNTER_MOVE_CREATE_GOLD";
    public const string HandCardsDiscardedReadySourcePower =
        "JINX_DISCARDED_HAND_CARDS_READY_POWER_1";
    public const string UnitConquestGrantSelfBoon =
        "UNIT_CONQUEST_GRANT_SELF_BOON";
    public const string UnitConquestReadySelfOncePerTurn =
        "UNIT_CONQUEST_READY_SELF_ONCE_PER_TURN";
    public const string UnitConquestGrantFriendlyBoon =
        "UNIT_CONQUEST_GRANT_FRIENDLY_BOON";
    public const string UnitConquestAdditionalActivation =
        "UNIT_CONQUEST_ADDITIONAL_ACTIVATION";
    public const string UnitConquestFriendlyPowerUntilEndOfTurn =
        "UNIT_CONQUEST_FRIENDLY_PLUS_8_THIS_TURN";
    public const string UnitConquestDestroyEquipmentGrantSelfBoon =
        "UNIT_CONQUEST_DESTROY_EQUIPMENT_GRANT_SELF_BOON";
    public const string UnitFriendlyDestroyedGainExperience =
        "SAVAGE_JAWFISH_FRIENDLY_DESTROYED_EXPERIENCE_1";
    public const string UnitFriendlyDestroyedPowerUntilEndOfTurn =
        "GHOSTLY_CENTAUR_FRIENDLY_DESTROYED_POWER_2";
    public const string UnitFirstFriendlyDestroyedDrawOne =
        "RESONANT_SOUL_FIRST_FRIENDLY_DESTROYED_DRAW_1";
    public const string UnitDestroyedNonMinionCreateMinion =
        "VIKTOR_DESTROYED_NON_MINION_CREATE_MINION";
    public const string UnitLastBreathDrawOne =
        "WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1";
    public const string UnitLastBreathCallRuneOne =
        "SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1";
    public const string UnitLastBreathCreateMinions =
        "MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS";
    public const string UnitLastBreathCreateRobots =
        "IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS";
    public const string UnitLastBreathCreateWarhawk =
        "MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK";
    public const string UnitLastBreathDrawIfAlone =
        "SAD_PORO_LAST_BREATH_DRAW_1";
    public const string UnitLastBreathDrawIfNotAlone =
        "LOYAL_PORO_LAST_BREATH_DRAW_1";
}

public static class TriggerTimings
{
    public const string BattlefieldUnitMovedAway = "BATTLEFIELD_UNIT_MOVED_AWAY";
    public const string BattlefieldHeld = "BATTLEFIELD_HELD";
    public const string BattlefieldConquered = "BATTLEFIELD_CONQUERED";
    public const string BattlefieldDefended = "BATTLEFIELD_DEFENDED";
    public const string BattlefieldFriendlySpellTargeted = "BATTLEFIELD_FRIENDLY_SPELL_TARGETED";
    public const string BattlefieldSpellPlayed = "BATTLEFIELD_SPELL_PLAYED";
    public const string BattlefieldUnitPlayed = "BATTLEFIELD_UNIT_PLAYED";
    public const string BattlefieldUnitReturned = "BATTLEFIELD_UNIT_RETURNED";
    public const string UnitConquest = "UNIT_CONQUEST";
    public const string UnitMoved = "UNIT_MOVED";
    public const string HandCardsDiscarded = "HAND_CARDS_DISCARDED";
    public const string UnitDestroyed = "UNIT_DESTROYED";
    public const string TurnStart = "TURN_START";
}

public static class TriggerTargetScopes
{
    public const string MovedUnit = "MOVED_UNIT";
    public const string FriendlyUnitAtThisBattlefield = "FRIENDLY_UNIT_AT_THIS_BATTLEFIELD";
    public const string DefenderUnitAtThisBattlefield = "DEFENDER_UNIT_AT_THIS_BATTLEFIELD";
    public const string PlayedUnitAtThisBattlefield = "PLAYED_UNIT_AT_THIS_BATTLEFIELD";
    public const string ReturnedUnitAtThisBattlefield = "RETURNED_UNIT_AT_THIS_BATTLEFIELD";
    public const string OtherControlledUnitAtThisBattlefield = "OTHER_CONTROLLED_UNIT_AT_THIS_BATTLEFIELD";
    public const string ControlledUnitAtThisBattlefield = "CONTROLLED_UNIT_AT_THIS_BATTLEFIELD";
    public const string ControlledLegend = "CONTROLLED_LEGEND";
    public const string UnitAtThisBattlefield = "UNIT_AT_THIS_BATTLEFIELD";
    public const string EachPlayer = "EACH_PLAYER";
    public const string OwnedHeroUnitInGraveyard = "OWNED_HERO_UNIT_IN_GRAVEYARD";
    public const string ControlledUnitsAtThisBattlefield = "CONTROLLED_UNITS_AT_THIS_BATTLEFIELD";
    public const string OwnedRuneInBase = "OWNED_RUNE_IN_BASE";
    public const string ControlledBoonUnitOnField = "CONTROLLED_BOON_UNIT_ON_FIELD";
    public const string ControlledUnitOnField = "CONTROLLED_UNIT_ON_FIELD";
    public const string EquipmentOnField = "EQUIPMENT_ON_FIELD";
    public const string OtherFriendlyDestroyedUnit = "OTHER_FRIENDLY_DESTROYED_UNIT";
    public const string ControlledHandCard = "CONTROLLED_HAND_CARD";
    public const string OtherControlledBattlefields = "OTHER_CONTROLLED_BATTLEFIELDS";
    public const string SurvivingPowerfulUnitAtThisBattlefield = "SURVIVING_POWERFUL_UNIT_AT_THIS_BATTLEFIELD";
    public const string FriendlyEquipment = "FRIENDLY_EQUIPMENT";
    public const string SourceUnit = "SOURCE_UNIT";
}

public static class TriggerDurations
{
    public const string UntilEndOfTurn = "UNTIL_END_OF_TURN";
}

public static class TriggerReadyTimings
{
    public const string EndOfTurn = "END_OF_TURN";
}

public static class TriggerMoveDestinations
{
    public const string OwnerBase = "OWNER_BASE";
}

public static class TriggerTokenDestinations
{
    public const string OwnerBase = "OWNER_BASE";
    public const string Battlefield = "BATTLEFIELD";
}

public static class TriggerZones
{
    public const string MainDeck = "MAIN_DECK";
    public const string Base = "BASE";
    public const string Battlefield = "BATTLEFIELD";
    public const string Hand = "HAND";
    public const string Graveyard = "GRAVEYARD";
    public const string Champion = "CHAMPION";
}

public static class TriggerCardFilters
{
    public const string TagPrefix = "TAG:";
}

public static class StaticAbilityKinds
{
    public const string UnitCannotBecomeActive = "UNIT_CANNOT_BECOME_ACTIVE";
    public const string BattlefieldPreventMoveToBase = "BATTLEFIELD_PREVENT_MOVE_TO_BASE";
    public const string BattlefieldPreventUnitPlay = "BATTLEFIELD_PREVENT_UNIT_PLAY";
    public const string BattlefieldEchoCostReduction = "BATTLEFIELD_ECHO_COST_REDUCTION";
    public const string BattlefieldEquipmentCostReduction = "BATTLEFIELD_EQUIPMENT_COST_REDUCTION";
    public const string BattlefieldGrantUnitExperienceAbility =
        "BATTLEFIELD_GRANT_UNIT_EXHAUST_GAIN_EXPERIENCE";
    public const string BattlefieldTargetSpellSkillDamageBonus =
        "BATTLEFIELD_TARGET_SPELL_SKILL_DAMAGE_BONUS";
    public const string BattlefieldGrantLegendAttachArmament =
        "BATTLEFIELD_GRANT_LEGEND_EXHAUST_ATTACH_ARMAMENT";
    public const string BattlefieldScoreDelayUntilTurn =
        "BATTLEFIELD_SCORE_DELAY_UNTIL_TURN";
    public const string BattlefieldWinningScoreIncrease =
        "BATTLEFIELD_WINNING_SCORE_INCREASE";
    public const string BattlefieldExtraStandbyDestination =
        "BATTLEFIELD_EXTRA_STANDBY_DESTINATION";
    public const string BattlefieldDestroyedInBattlePayRecallReplacement =
        "BATTLEFIELD_DESTROYED_IN_BATTLE_PAY_3_RECALL";
}

public static class StaticAuraKinds
{
    public const string FriendlyFieldEquipmentCountToSourceUnitPower =
        "FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER";
    public const string SourceObjectFilteredPower = "SOURCE_OBJECT_FILTERED_POWER";
    public const string BattlefieldAllUnitsPowerPlusOne = "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE";
    public const string BattlefieldAllUnitsKeyword = "BATTLEFIELD_ALL_UNITS_KEYWORD";
    public const string BattlefieldFilteredUnitsPower = "BATTLEFIELD_FILTERED_UNITS_POWER";
    public const string BattlefieldFilteredUnitsKeyword = "BATTLEFIELD_FILTERED_UNITS_KEYWORD";
    public const string BattlefieldIsolatedDefenderKeywordModifier =
        "BATTLEFIELD_ISOLATED_DEFENDER_KEYWORD_MODIFIER";
    public const string SameBattlefieldOtherFriendlyUnitsPowerPlusOne =
        "SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE";
    public const string SameBattlefieldOtherFriendlyUnitsKeyword =
        "SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_KEYWORD";
    public const string SameBattlefieldOtherFriendlyFilteredUnitsPower =
        "SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_UNITS_POWER";
    public const string SameBattlefieldFriendlyFilteredUnitCountToSourcePower =
        "SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER";
    public const string SourceSameLocationOtherFriendlyUnitPower =
        "SOURCE_SAME_LOCATION_OTHER_FRIENDLY_UNIT_POWER";
    public const string FriendlySingleDefendingUnitPower = "FRIENDLY_SINGLE_DEFENDING_UNIT_POWER";
    public const string FriendlyUnitsPower = "FRIENDLY_UNITS_POWER";
    public const string OtherFriendlyUnitsPower = "OTHER_FRIENDLY_UNITS_POWER";
    public const string FriendlyFilteredUnitsPower = "FRIENDLY_FILTERED_UNITS_POWER";
    public const string FriendlyFilteredUnitsKeyword = "FRIENDLY_FILTERED_UNITS_KEYWORD";
    public const string SourceAttackingWithAnotherUnitPower = "SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER";
    public const string SourceLoneBattlePower = "SOURCE_LONE_BATTLE_POWER";
    public const string SourceAttackingReadyEnemyUnitPower = "SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER";
}

public static class StaticAuraTargetScopes
{
    public const string SourceObject = "SOURCE_OBJECT";
    public const string SameBattlefieldUnits = "SAME_BATTLEFIELD_UNITS";
    public const string SameBattlefieldFilteredUnits = "SAME_BATTLEFIELD_FILTERED_UNITS";
    public const string SameBattlefieldIsolatedDefender = "SAME_BATTLEFIELD_ISOLATED_DEFENDER";
    public const string SameBattlefieldOtherFriendlyUnits = "SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS";
    public const string SameBattlefieldOtherFriendlyFilteredUnits =
        "SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_UNITS";
    public const string FriendlySingleDefendingBattlefieldUnit = "FRIENDLY_SINGLE_DEFENDING_BATTLEFIELD_UNIT";
    public const string FriendlyUnits = "FRIENDLY_UNITS";
    public const string OtherFriendlyUnits = "OTHER_FRIENDLY_UNITS";
    public const string FriendlyFilteredUnits = "FRIENDLY_FILTERED_UNITS";
}

public static class StaticAuraParticipantScopes
{
    public const string SourceObject = "SOURCE_OBJECT";
    public const string FriendlyPublicFieldEquipment = "FRIENDLY_PUBLIC_FIELD_EQUIPMENT";
    public const string SameBattlefieldPublicUnits = "SAME_BATTLEFIELD_PUBLIC_UNITS";
    public const string SameBattlefieldFilteredPublicUnits = "SAME_BATTLEFIELD_FILTERED_PUBLIC_UNITS";
    public const string SameBattlefieldIsolatedDefender = "SAME_BATTLEFIELD_ISOLATED_DEFENDER";
    public const string SameBattlefieldFriendlyFilteredPublicUnits =
        "SAME_BATTLEFIELD_FRIENDLY_FILTERED_PUBLIC_UNITS";
    public const string SameLocationOtherFriendlyPublicUnits = "SAME_LOCATION_OTHER_FRIENDLY_PUBLIC_UNITS";
    public const string SingleFriendlyDefendingBattlefieldUnit = "SINGLE_FRIENDLY_DEFENDING_BATTLEFIELD_UNIT";
    public const string SameBattlefieldOtherFriendlyPublicUnits = "SAME_BATTLEFIELD_OTHER_FRIENDLY_PUBLIC_UNITS";
    public const string SameBattlefieldOtherFriendlyFilteredPublicUnits =
        "SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_PUBLIC_UNITS";
    public const string FriendlyPublicUnits = "FRIENDLY_PUBLIC_UNITS";
    public const string OtherFriendlyPublicUnits = "OTHER_FRIENDLY_PUBLIC_UNITS";
    public const string FriendlyFilteredPublicUnits = "FRIENDLY_FILTERED_PUBLIC_UNITS";
    public const string BattlefieldPublicUnits = "BATTLEFIELD_PUBLIC_UNITS";
    public const string AttackingBattlefieldPublicUnits = "ATTACKING_BATTLEFIELD_PUBLIC_UNITS";
    public const string ReadyEnemyBattlefieldPublicUnits = "READY_ENEMY_BATTLEFIELD_PUBLIC_UNITS";
}

public static class StaticAuraTargetFilters
{
    public const string AnyPrefix = "ANY:";
    public const string CardNamePrefix = "CARD_NAME:";
    public const string UnitToken = "UNIT_TOKEN";
    public const string TagPrefix = "TAG:";
}

public sealed record BehaviorSpec(
    string CardNo,
    string CardName,
    string CardCategoryName,
    string FunctionalUnitId,
    string Status,
    string Reason,
    string OfficialText,
    string FrontImage,
    string BackImage,
    ParsedCostSpec Cost,
    IReadOnlyList<KeywordSpec> Keywords,
    IReadOnlyList<TargetSpec> Targets,
    IReadOnlyList<TriggerSpec> Triggers,
    IReadOnlyList<ReplacementSpec> Replacements,
    IReadOnlyList<ActivatedAbilitySpec> ActivatedAbilities,
    IReadOnlyList<StaticAbilitySpec> StaticAbilities,
    IReadOnlyList<StaticAuraSpec> StaticAuras,
    IReadOnlyList<EffectPhraseSpec> Effects,
    IReadOnlyList<string> TemplateIds,
    string? ImplementedEffectKind = null,
    string? ImplementedByCardNo = null,
    string ConformanceTier = BehaviorConformanceTiers.RepresentativeRulePass,
    string ConformanceReason = "");

public sealed record KeywordSpec(
    string Keyword,
    string RawText,
    string? Value = null);

public sealed record ParsedCostSpec(
    int? Mana,
    int? ReturnEnergy,
    int? Power,
    IReadOnlyList<string> AdditionalCosts,
    IReadOnlyList<string> OptionalCosts);

public sealed record TargetSpec(
    string Scope,
    int MinCount,
    int? MaxCount,
    string Text,
    bool Optional = false);

public sealed record TriggerSpec(
    string Kind,
    string Timing,
    string Text,
    string Reason = "",
    string? TargetScope = null,
    int? PowerDelta = null,
    string? Duration = null,
    int? ManaDelta = null,
    int? DrawCount = null,
    int? DrawCountPerParticipant = null,
    int? MinimumPaidMana = null,
    int? RequiredOverkillDamage = null,
    int? RevealCount = null,
    string? RevealSourceZone = null,
    string? RevealMatchCardFilter = null,
    string? RevealMatchDestinationZone = null,
    string? RevealMissDestinationZone = null,
    int? RecycleCount = null,
    string? RecycleSourceZone = null,
    string? RecycleDestinationZone = null,
    int? MillCount = null,
    string? MillSourceZone = null,
    string? MillDestinationZone = null,
    int? DiscardCount = null,
    string? DiscardSourceZone = null,
    string? DiscardDestinationZone = null,
    int? ManaCost = null,
    int? PowerCost = null,
    int? BoonCount = null,
    int? AdditionalTriggerCount = null,
    int? ConsumedBoonCount = null,
    int? RuneCallCount = null,
    int? RuneReadyCount = null,
    bool? ReadiesSource = null,
    string? ReadyTiming = null,
    int? EquipmentReadyCount = null,
    int? LegendReadyCount = null,
    bool? DetachesArmament = null,
    int? MoveCount = null,
    string? MoveDestination = null,
    bool? OncePerTurn = null,
    bool? ExcludesTokens = null,
    int? CreatedTokenCount = null,
    string? CreatedTokenName = null,
    int? CreatedTokenPower = null,
    string? CreatedTokenDestination = null,
    bool? CreatedTokenExhausted = null,
    IReadOnlyList<string>? CreatedTokenKeywords = null,
    int? ReturnCount = null,
    string? RequiredEmptyZone = null,
    string? ReturnOriginZone = null,
    string? ReturnDestinationZone = null,
    string? ReturnCardFilter = null,
    int? RequiredUnitCount = null,
    int? RequiredPowerThreshold = null,
    bool? WinsGame = null,
    int? DamageAmount = null,
    int? DestroyCount = null,
    bool? Optional = null,
    bool? FirstTurnOnly = null,
    int? ScoreAmount = null,
    string? GrantedKeyword = null,
    int? KeywordBonus = null,
    int? ExperienceCount = null,
    bool? RequiresNoOtherFriendlyUnitAtSamePosition = null,
    bool? RequiresOtherFriendlyUnitAtSamePosition = null);

public sealed record ReplacementSpec(
    string Kind,
    string AppliesTo,
    string Text,
    string Reason = "");

public sealed record ActivatedAbilitySpec(
    string CostText,
    string EffectText,
    IReadOnlyList<string> TemplateIds,
    string Status,
    string Reason);

public sealed record StaticAbilitySpec(
    string Kind,
    string Text,
    string Status,
    string Reason,
    int Amount = 0);

public sealed record StaticAuraSpec(
    string Kind,
    string Layer,
    string Duration,
    string TargetScope,
    string ParticipantScope,
    int PowerDeltaPerParticipant,
    string Text,
    string Status,
    string Reason,
    string? TargetFilter = null,
    string? GrantedKeyword = null,
    int? RequiredAttackingUnitCount = null,
    int? RequiredDefendingUnitCount = null,
    int? RequiredReadyEnemyUnitCount = null,
    int? RequiredParticipantCount = null,
    int? RequiredPlayerExperience = null);

public sealed record EffectPhraseSpec(
    string TemplateId,
    string Phrase,
    string Status,
    string Reason);
