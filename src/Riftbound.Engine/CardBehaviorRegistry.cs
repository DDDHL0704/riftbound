using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record UnitEventAbilitySpec(string Timing, string Effect, int Amount = 0, bool RequiresNoDamageThisTurn = false,
    bool RequiresEmpowered = false, int LeadingPowerCost = 0, string? LeadingPowerTrait = null, bool Optional = false,
    bool AnyNumberOfTargets = false, bool LeadingSelfBanish = false, int LeadingManaCost = 0,
    int LeadingDestroyOtherFriendlyPermanents = 0);

public sealed record CardBehaviorDefinition(
    string CardNo,
    string DisplayName,
    int ManaCost,
    string EffectKind,
    int DamageAmount,
    int RequiredTargetCount,
    string DamageConditionKind = CardDamageConditionKinds.None,
    int ConditionalDamageAmount = 0,
    string StatusEffectId = "",
    int DrawCount = 0,
    bool DestroysTarget = false,
    string TargetScope = CardTargetScopes.BattlefieldUnit,
    int MinTargetCount = -1,
    string Mode = "",
    string DrawConditionKind = CardDrawConditionKinds.None,
    string DynamicDrawCountKind = CardDynamicDrawCountKinds.None,
    bool AllowsRepeatedTargets = false,
    string CostReductionConditionKind = CardCostReductionConditionKinds.None,
    int CostReductionMana = 0,
    string CostReductionUnitTag = "",
    bool RecyclesTargets = false,
    RevealedHandChoiceSpec? HandChoice = null,
    ConquestDamageSpec? ConquestDamage = null,
    MovementScoreSpec? MovementScore = null,
    UnitEventAbilitySpec? UnitEventAbility = null,
    bool GrantsNormalGraveyardPlay = false, bool SkipsDrawPhase = false, bool BanishesNonDeckGraveyardEntries = false,
    int DamageImmunityAfterMoves = 0,
    bool DamagesAllBattlefieldUnits = false,
    bool DamagesAllEnemyCombatUnits = false,
    bool DestroysAllEquipment = false,
    int OtherEnemyBattlefieldUnitsDamageAmount = 0,
    bool ModifiesAllFriendlyUnits = false,
    int MaxTargetManaCost = 0,
    int MaxTargetPower = 0,
    string DrawRecipientKind = CardDrawRecipientKinds.Controller,
    bool DestroysAllUnits = false,
    bool ReturnsTargetToHand = false,
    bool ReturnsAllUnitsToHand = false,
    bool ReturnsAllFieldObjectsToHand = false,
    int PowerModifierAmount = 0,
    string PowerModifierConditionKind = CardPowerModifierConditionKinds.None,
    string PowerModifierUnitTag = "",
    string PowerModifierControlledUnitTags = "",
    int MinimumPowerAfterModifier = 0,
    bool BanishesIfDestroyedThisTurn = false,
    int RuneCallCount = 0,
    int DrawCountIfRuneCallFails = 0,
    int RuneCallCountAfterTargetReturn = 0,
    bool MovesTargetToBase = false,
    bool MovesWeakerEnemiesAtSelectedBattlefieldToBase = false,
    bool RemovesDamageFromAllFriendlyBattlefieldUnits = false,
    int SecondaryPowerModifierAmount = 0,
    bool ReadiesTarget = false,
    bool ReadiesAllFriendlyUnits = false,
    bool ExhaustsAllFriendlyUnits = false,
    bool DrawsBeforeRuneCall = false,
    bool ReturnsTargetToHandIfAlreadyHasStatusEffect = false,
    bool DestroysTargetIfAlreadyHasStatusEffect = false,
    int EffectRepeatCountIfExhaustFriendlyUnitOptionalCost = 0,
    bool DiscardsTargetFromHand = false,
    bool DiscardsTargetFromOwnerHand = false,
    bool ReturnsGraveyardTargetToHand = false,
    bool DealsMutualTargetPowerDamage = false,
    bool DealsSourceAndTargetPowerDamage = false,
    bool CountersTargetStackSpell = false,
    int InsightCount = 0,
    int DrawAfterInsightCount = 0,
    bool PreventsTargetSpellControllerPlayingSpellsThisTurn = false,
    bool CanPlayDuringPriority = false,
    int CreatedBaseUnitTokenCount = 0,
    bool CreatedBaseUnitTokenPerEquipment = false,
    int CreatedTokenReadyCount = 0,
    string CreatedTokenReadyPowerTrait = "",
    bool CreatedBaseUnitTokenEntersReady = false,
    int CreatedBaseUnitTokenPower = 0,
    string CreatedBaseUnitTokenName = "",
    string CreatedBaseUnitTokenTags = "",
    bool CreatedBaseUnitTokenCopiesFirstTarget = false,
    bool DiscardsAllPlayersHandsThenDraws = false,
    bool RequiresDestroyFriendlyUnitAdditionalCost = false,
    bool RequiresDestroyFriendlyPowerfulUnitAdditionalCost = false,
    bool RequiresReturnFriendlyEquipmentAdditionalCost = false,
    bool DestroysFirstTargetAndBuffsSecondByDestroyedPower = false,
    string SecondaryStatusEffectId = "",
    int EffectRepeatCountIfDiscardHandCardOptionalCost = 0,
    bool DamagesSecondTargetByFirstTargetPower = false,
    bool ReadiesFirstTargetAndDamagesSecondByFirstPower = false,
    bool SetsFirstTargetPowerToSecondIfLower = false,
    bool SwapsTargetPowersUntilEndOfTurn = false,
    string TargetOwnerMainDeckDestination = "",
    int MainDeckLookCount = 0,
    int MainDeckLookTargetStartIndex = 0,
    string MainDeckTargetRequiredTag = "",
    int MainDeckTargetMinimumManaCost = 0,
    bool DrawsSelectedMainDeckTarget = false,
    bool MainDeckSelectionIsDraw = true,
    bool RecyclesSelectedMainDeckTargets = false,
    bool RecyclesUnselectedMainDeckLookCards = false,
    bool UsesFriendlyBattlefieldUnitCountAsMaxTargetCount = false,
    bool UsesEnemyBattlefieldUnitCountAsPowerModifierMultiplier = false,
    int PowerModifierTargetIndex = -1,
    int StatusEffectTargetIndex = -1,
    string TargetForbiddenTag = "",
    bool ModifiesAllFriendlyBattlefieldUnits = false,
    bool ModifiesAllEnemyBattlefieldUnits = false,
    int MaxTotalTargetPower = 0,
    bool AnyNumberOfTargets = false,
    bool TargetsShareBattlefield = false,
    bool TargetsShareController = false,
    bool MovesTargetsToChosenLocation = false,
    bool ReadiesTargetsAfterChosenMovement = false,
    bool OptionalEquipmentAfterMovement = false,
    string OptionalEquipmentRequiredTag = "",
    bool GrantsConquestReturnToBaseAfterMovement = false,
    bool CreatesGoldWhenTargetDestroyedThisTurn = false,
    SourceReplaySpec? SourceReplay = null,
    int FriendlyBoonBonusUntilEndOfTurn = 0,
    int DamageAlternativeDrawCount = 0,
    string TurnUnitPlayGrantKind = "",
    bool QueuesDestinationDuel = false,
    bool MakesControllerDamageLethal = false,
    int PlayDamagePerLocation = 0,
    CommandedMovementSpec? CommandedMovement = null,
    TargetReferencedMovementSpec? TargetReferencedMovement = null,
    bool AppliesStatusEffectToAllUnits = false,
    bool MovesFirstTargetToSecondTargetLocation = false,
    string TargetRequiredTag = "",
    string AnyTargetRequiredTag = "",
    string TargetAddedTag = "",
    bool UsesTargetCurrentPowerAsPowerModifier = false,
    int CreatedBaseEquipmentTokenCount = 0,
    string CreatedBaseEquipmentTokenName = "",
    string CreatedBaseEquipmentTokenTags = "",
    bool CreatedBaseEquipmentTokenIsExhausted = false,
    int CreatedBaseEquipmentTokenCountIfDestroyedFriendlyUnit = 0,
    int CreatedBaseEquipmentTokenCountIfDestroyedEnemyUnit = 0,
    bool DamageAmountFromFirstTargetManaCost = false,
    bool DamageAmountFromTargetMarkedDamage = false,
    bool DrawsControllerAndOtherPlayers = false,
    bool CallsRuneForControllerAndOtherPlayers = false,
    bool GrantsBoon = false,
    bool MovesTargetToBattlefield = false,
    bool GrantsBoonToAllFriendlyUnits = false,
    bool ConsumesTargetBoonsToReady = false,
    bool SwapsTargetLocations = false,
    bool SwapsSourceWithFirstTargetLocation = false,
    bool GainsControlOfTargetToBase = false,
    bool GainsControlOfTargetToBattlefield = false,
    bool ExhaustsControlledTarget = false,
    bool MovesTargetsSequentially = false,
    bool DamageAmountFromOptionalPowerCost = false,
    bool BanishesTargetThenPlaysToBase = false,
    bool BanishesAllFriendlyGraveyardUnits = false,
    bool PlaysSourceToBaseAsEquipment = false,
    string SourceEquipmentTags = "",
    bool SourceEquipmentIsExhausted = false,
    bool PlaysSourceToBaseAsUnit = false,
    int SourceUnitPower = 0,
    string SourceUnitTags = "",
    bool SourceUnitIsExhausted = true,
    int? SourceReadyWhenWinningScoreDistanceExceeds = null,
    bool AppliesPowerModifierToSourceUnit = false,
    bool GrantsBoonToSourceUnit = false,
    bool ModifiesAllEnemyUnits = false,
    bool RequiresDestroyFriendlyTraitUnitAdditionalCost = false,
    string CounteredStackItemDestination = CardCounteredStackItemDestinationZones.Graveyard,
    int CounterUnlessControllerPaysMana = 0,
    int? MaxTargetPowerCost = null,
    bool RequiresTargetCostsAtMostDestroyedAdditionalCostUnit = false,
    bool RequiresTargetStackItemControlledByEnemy = false,
    bool RequiresTargetStackItemTargetsFriendlyUnitOrEquipment = false,
    bool RequiresTargetStackItemTargetsFirstTarget = false,
    bool RequiresTargetStackItemTargetsNoOtherFriendlyUnits = false,
    bool AppliesPowerModifierToFirstTargetFromSecondStackSpellManaCost = false,
    int DamageTargetIndex = -1,
    int BanishPlayTargetIndex = -1,
    bool BanishesSourceOnResolution = false,
    bool SchedulesExtraTurnForController = false,
    bool PreventsAllSpellAndSkillDamageThisTurn = false,
    bool AttachesOrDetachesSecondTargetEquipmentToFirstTarget = false,
    bool GainsControlOfTargetStackSpell = false,
    bool MayPayPowerToGainControlAndRemakeSpellChoices = false,
    bool MayChooseNewTargetsForControlledSpell = false,
    bool RecyclesUnkeptSacredJudgmentCards = false,
    string DeckPlayKind = "",
    bool DeckPlayDrawsUnselected = false,
    bool CanPlayDuringSpellDuel = false,
    int GainExperienceOnPlay = 0,
    int GainExperienceAfterEffect = 0,
    int GainExperienceOnPlayPerFriendlyFieldUnit = 0,
    int OptionalExperienceCost = 0,
    int ManaReductionIfExperiencePaid = 0,
    bool AllowsAmbushToEnemyOccupiedBattlefield = false,
    bool AllowsPlayAtDestroyedAdditionalCostLocation = false,
    int HasteReadyManaCost = 0,
    int HasteReadyPowerCost = 0,
    string HasteReadyPowerTrait = "",
    int LevelExperienceThreshold = 0,
    int LevelSourceUnitPowerBonus = 0,
    string LevelSourceUnitTags = "",
    int LevelDrawOnPlayCount = 0,
    int SourceNextSpellCostReductionMana = 0,
    string SourceNextSpellCostReductionEffectKind = "",
    bool SourceNextSpellCostReductionAppliesToAllCards = false,
    int SourceNextSpellCostReductionGenericPower = 0,
    string SourceBoonConditionKind = CardSourceBoonConditionKinds.None,
    string SourceBoonRequiredControlledUnitTag = "",
    string TargetCountConditionKind = CardTargetCountConditionKinds.None,
    string CreatedBaseUnitTokenConditionKind = CardTokenCreationConditionKinds.None,
    int ManaReductionIfDiscardHandCardOptionalCost = 0,
    int SourceBoonAdditionalManaCost = 0,
    int SourceBoonDrawCount = 0,
    string SourceBoonDrawEffectKind = "",
    int SourceDrawAdditionalPowerCost = 0,
    string SourceDrawAdditionalPowerTrait = "",
    int SourceDrawCountIfOptionalPowerCostPaid = 0,
    int SourceReadyPowerModifierAdditionalPowerCost = 0,
    string SourceReadyPowerModifierAdditionalPowerTrait = "",
    int SourceReadyPowerModifierAmount = 0,
    int TargetEffectAdditionalManaCost = 0,
    int TargetEffectAdditionalPowerCost = 0,
    string TargetEffectAdditionalPowerTrait = "",
    int DefendTriggerMaxTargetCount = 0,
    string DefendTriggerTargetScope = "",
    int DefendTriggerMainDeckLookCount = 0,
    string DefendTriggerCountedTag = "",
    int DefendTriggerDamagePerCountedCard = 0,
    bool DefendTriggerRecyclesLookedMainDeckCards = false,
    int StandbyReactionMaxTargetCount = 0,
    int StandbyReactionMinTargetCount = 0,
    string StandbyReactionTargetScope = "",
    int StandbyReactionMainDeckLookCount = 0,
    string StandbyReactionCountedTag = "",
    int StandbyReactionDamagePerCountedCard = 0,
    bool StandbyReactionRecyclesLookedMainDeckCards = false,
    bool ExhaustsTarget = false,
    int SourceReadyAdditionalPowerCost = 0,
    string SourceReadyAdditionalPowerTrait = "",
    string SourceReadyConditionKind = CardSourceReadyConditionKinds.None,
    string SourceReadyOptionalCostPayloadKey = "",
    int SourceStealEnemyEquipmentAdditionalPowerCost = 0,
    string SourceStealEnemyEquipmentAdditionalPowerTrait = "",
    string SourceStealEnemyEquipmentOptionalCostPrefix = "",
    string SourceStealEnemyEquipmentReason = "",
    string ConditionalSourceUnitConditionKind = CardConditionalSourceUnitConditionKinds.None,
    int ConditionalSourceUnitPowerBonus = 0,
    string ConditionalSourceUnitTags = "",
    string SourceDrawConditionKind = CardSourceDrawConditionKinds.None,
    int SourceDrawCount = 0,
    int SourceDrawRequiredOtherControlledUnitPower = 0,
    int StaticUnitCostReductionMana = 0,
    string StaticUnitCostReductionRequiredUnitTag = "",
    int StaticUnitCostReductionMinimumManaCost = 0,
    int StaticSpellCostReductionMana = 0,
    int StaticSpellCostReductionMinimumManaCost = 0,
    bool SourceReadiesWhenControllerPlaysEquipment = false,
    string SourceReadyOnEquipmentPlayedEffectKind = "",
    bool SourceReadiesWhenControllerStunsEnemyUnit = false,
    int SourcePowerOnControllerStunsEnemyUnitAmount = 0,
    string SourceStunEnemyUnitTriggerEffectKind = "",
    int SourcePowerOnControllerStandbyHiddenAmount = 0,
    string SourcePowerOnControllerStandbyHiddenEffectKind = "",
    int SourceAttackDamageToFirstDefenderAmount = 0,
    string SourceAttackDamageToFirstDefenderEffectKind = "",
    string UnitHighCostSpellPowerModifierEffectKind = "",
    string UnitAttackPayPowerModifierEffectKind = "",
    bool GrantsFreeStandbyHidePermission = false,
    string EffectPlaySourceZone = "",
    bool EffectPlayIgnoreBaseMana = false,
    bool EffectPlayIgnoreBasePower = false,
    int EffectPlayManaReduction = 0,
    string EffectPlayDestination = "ANY",
    bool EffectPlayOptional = false,
    bool EffectPlayAllowsAnyCard = false,
    bool EffectPlayRevealsCards = false,
    bool IgnorePrintedPowerCost = false,
    string TargetTraitsManaReductionTags = "",
    string EffectPlayedUnitStatus = "",
    int TargetTraitsManaReduction = 0,
    bool CanCounterAbilities = false,
    SelectedBattlefieldEffectSpec? SelectedBattlefieldEffect = null,
    StandbyPlacement? StandbyPlayFrom = null,
    int? DeckPlayMaximumUnitMight = null,
    bool EffectPlayIgnoreAllCosts = false,
    bool CreatedBaseUnitTokensCopySource = false,
    bool GrantsHasteToFriendlyUnitsPlayedOutsideHand = false,
    IReadOnlyList<string>? GrantedHastePowerTraits = null,
    bool MaySacrificeUnitForPrintedCostReduction = false,
    bool DeckPlayOptionalReflexiveEmpower = false,
    bool GravesUnselectedMainDeckLookCards = false,
    FlowCostSpec? FlowCost = null,
    FlowCostSpec? AppliedFlowCost = null,
    bool MayReduceOtherCardOptionalAdditionalCostByOneManaOrPower = false,
    int DamageAmountIfDiscardHandCardOptionalCost = 0,
    int ExtraDamagePerSameNameInControllerGraveyard = 0,
    int? PowerModifierAmountIfTargetEmpowered = null)
{
    public bool PerformsInsight => InsightCount > 0;
}

public static class CardDamageConditionKinds
{
    public const string None = "NONE";
    public const string ControllerHasFaceDownCard = "CONTROLLER_HAS_FACE_DOWN_CARD";
    public const string TargetIsAttacking = "TARGET_IS_ATTACKING";
}

public static class CardTargetScopes
{
    public const string Battlefield = "BATTLEFIELD";
    public const string BattlefieldUnit = "BATTLEFIELD_UNIT";
    public const string BattlefieldUnitOrEquipment = "BATTLEFIELD_UNIT_OR_EQUIPMENT";
    public const string BaseUnit = "BASE_UNIT";
    public const string AnyUnit = "ANY_UNIT";
    public const string AnyEmpoweredUnit = "ANY_EMPOWERED_UNIT";
    public const string FriendlyUnit = "FRIENDLY_UNIT";
    public const string FriendlyUnitThenBattlefield = "FRIENDLY_UNIT_THEN_BATTLEFIELD";
    public const string FriendlyBaseUnitThenBattlefield = "FRIENDLY_BASE_UNIT_THEN_BATTLEFIELD";
    public const string BattlefieldThenEnemyUnit = "BATTLEFIELD_THEN_ENEMY_UNIT";
    public const string FriendlyOccupiedBattlefieldThenEnemyUnit = "FRIENDLY_OCCUPIED_BATTLEFIELD_THEN_ENEMY_UNIT";
    public const string FriendlyUnitThenFriendlyUnit = "FRIENDLY_UNIT_THEN_FRIENDLY_UNIT";
    public const string FriendlyThenEnemyUnits = "FRIENDLY_THEN_ENEMY_UNITS";
    public const string UnitThenItsControllersWeapon = "UNIT_THEN_ITS_CONTROLLERS_WEAPON";
    public const string FriendlyEquipmentThenEnemyEquipment = "FRIENDLY_EQUIPMENT_THEN_ENEMY_EQUIPMENT";
    public const string FriendlyThenEnemyBattlefieldUnits = "FRIENDLY_THEN_ENEMY_BATTLEFIELD_UNITS";
    public const string FriendlyBattlefieldThenEnemyBattlefieldUnits = "FRIENDLY_BATTLEFIELD_THEN_ENEMY_BATTLEFIELD_UNITS";
    public const string FriendlyBattlefieldUnitThenStackSpell = "FRIENDLY_BATTLEFIELD_UNIT_THEN_STACK_SPELL";
    public const string FriendlyUnitThenStackSpell = "FRIENDLY_UNIT_THEN_STACK_SPELL";
    public const string FriendlyBattlefieldUnit = "FRIENDLY_BATTLEFIELD_UNIT";
    public const string FriendlyHandCard = "FRIENDLY_HAND_CARD";
    public const string AnyHandCard = "ANY_HAND_CARD";
    public const string FriendlyHandCardThenBattlefieldUnit = "FRIENDLY_HAND_CARD_THEN_BATTLEFIELD_UNIT";
    public const string FriendlyMainDeckCard = "FRIENDLY_MAIN_DECK_CARD";
    public const string FriendlyGraveyardCard = "FRIENDLY_GRAVEYARD_CARD";
    public const string AnyGraveyardCard = "ANY_GRAVEYARD_CARD";
    public const string AttackingUnit = "ATTACKING_UNIT";
    public const string EnemyAttackingUnit = "ENEMY_ATTACKING_UNIT";
    public const string EnemyBattlefieldUnit = "ENEMY_BATTLEFIELD_UNIT";
    public const string EnemyBattlefieldUnitThenFriendlyUnit = "ENEMY_BATTLEFIELD_UNIT_THEN_FRIENDLY_UNIT";
    public const string FriendlyUnitThenFriendlyEquipment = "FRIENDLY_UNIT_THEN_FRIENDLY_EQUIPMENT";
    public const string EnemyUnitAtSourceBattlefield = "ENEMY_UNIT_AT_SOURCE_BATTLEFIELD";
    public const string EnemyUnitAtSourceLocation = "ENEMY_UNIT_AT_SOURCE_LOCATION";
    public const string EnemyUnit = "ENEMY_UNIT";
    public const string EnemyUnitThenEnemyUnit = "ENEMY_UNIT_THEN_ENEMY_UNIT";
    public const string OpponentHandCard = "OPPONENT_HAND_CARD";
    public const string OpponentGraveyardCard = "OPPONENT_GRAVEYARD_CARD";
    public const string OpponentMainDeckTopCard = "OPPONENT_MAIN_DECK_TOP_CARD";
    public const string AnyMainDeckTopFiveCard = "ANY_MAIN_DECK_TOP_FIVE_CARD";
    public const string FriendlyBaseUnit = "FRIENDLY_BASE_UNIT";
    public const string Equipment = "EQUIPMENT";
    public const string Legend = "LEGEND";
    public const string StackSpell = "STACK_SPELL";
    public const string SacredJudgmentKeepCard = "SACRED_JUDGMENT_KEEP_CARD";
}

public static class CardCounteredStackItemDestinationZones
{
    public const string Graveyard = "GRAVEYARD";
    public const string Hand = "HAND";
}

public static class CardObjectTags
{
    public const string UnitCard = "CARD_TYPE:UNIT";
    public const string EquipmentCard = "CARD_TYPE:EQUIPMENT";
    public const string RuneCard = "CARD_TYPE:RUNE";
    public const string SpellCard = "CARD_TYPE:SPELL";
    public const string MinionTokenFamily = "TOKEN_FAMILY:MINION";
    public const string Spellshield = "法盾";
    public const string Standby = "待命";
    public const string Ephemeral = "瞬息";
    public const string SandSoldier = "黄沙士兵";
    public const string Boon = "增益";
    public const string Bulwark = "壁垒";
}

public static class CardDrawConditionKinds
{
    public const string None = "NONE";
    public const string TargetDestroyedByThisEffect = "TARGET_DESTROYED_BY_THIS_EFFECT";
    public const string PlayedAfterAnotherCardThisTurn = "PLAYED_AFTER_ANOTHER_CARD_THIS_TURN";
    public const string PlayedFromHand = "PLAYED_FROM_HAND";
}

public static class CardDynamicDrawCountKinds
{
    public const string None = "NONE";
    public const string ControllerPowerfulUnits = "CONTROLLER_POWERFUL_UNITS";
}

public static class CardDrawRecipientKinds
{
    public const string Controller = "CONTROLLER";
    public const string TargetController = "TARGET_CONTROLLER";
}

public static class CardCostReductionConditionKinds
{
    public const string None = "NONE";
    public const string EnemyUnitDestroyedThisTurn = "ENEMY_UNIT_DESTROYED_THIS_TURN";
    public const string ControllerHighestUnitPower = "CONTROLLER_HIGHEST_UNIT_POWER";
    public const string OpponentWithinThreeOfWinningScore = "OPPONENT_WITHIN_THREE_OF_WINNING_SCORE";
    public const string ControllerControlsTaggedUnit = "CONTROLLER_CONTROLS_TAGGED_UNIT";
    public const string ControllerControlsEmpoweredObject = "CONTROLLER_CONTROLS_EMPOWERED_OBJECT";
    public const string ControllerPlayedAnotherCardThisTurn = "CONTROLLER_PLAYED_ANOTHER_CARD_THIS_TURN";
    public const string PlayedOutsideOwnHand = "PLAYED_OUTSIDE_OWN_HAND";
}

public static class CardSourceBoonConditionKinds
{
    public const string None = "NONE";
    public const string PlayedAfterAnotherCardThisTurn = "PLAYED_AFTER_ANOTHER_CARD_THIS_TURN";
    public const string ControllerControlsFaceUpUnitWithTag = "CONTROLLER_CONTROLS_FACE_UP_UNIT_WITH_TAG";
}

public static class CardSourceReadyConditionKinds
{
    public const string None = "NONE";
    public const string ControllerPlayedSpellThisTurn = "CONTROLLER_PLAYED_SPELL_THIS_TURN";
}

public static class CardSourceDrawConditionKinds
{
    public const string None = "NONE";
    public const string OtherControlledUnitPowerAtLeast = "OTHER_CONTROLLED_UNIT_POWER_AT_LEAST";
}

public static class CardConditionalSourceUnitConditionKinds
{
    public const string None = "NONE";
    public const string ControllerPlayedFourPlusCostSpellThisTurn = "CONTROLLER_PLAYED_FOUR_PLUS_COST_SPELL_THIS_TURN";
    public const string ControllerGainedExperienceThisTurn = "CONTROLLER_GAINED_EXPERIENCE_THIS_TURN";
    public const string ControllerDiscardedHandCardThisTurn = "CONTROLLER_DISCARDED_HAND_CARD_THIS_TURN";
}

public static class CardTargetCountConditionKinds
{
    public const string None = "NONE";
    public const string PlayedAfterAnotherCardThisTurn = "PLAYED_AFTER_ANOTHER_CARD_THIS_TURN";
}

public static class CardTokenCreationConditionKinds
{
    public const string None = "NONE";
    public const string PlayedAfterAnotherCardThisTurn = "PLAYED_AFTER_ANOTHER_CARD_THIS_TURN";
}

public static class CardPowerModifierConditionKinds
{
    public const string None = "NONE";
    public const string TargetIsAttacking = "TARGET_IS_ATTACKING";
    public const string TargetIsDefending = "TARGET_IS_DEFENDING";
}

public static class CardBehaviorRegistry
{
    private static readonly CardBehaviorDefinition[] RawDefinitions =
    [
        new("VEN·022", "无尽秘藏", 5, "EQUIPMENT_PLAY:VEN·022", 0, 0,
            PlaysSourceToBaseAsEquipment: true, UnitEventAbility: new("PLAY", "BANISH_HAND_GRAVEYARD_BURN", 7),
            GrantsNormalGraveyardPlay: true, SkipsDrawPhase: true, BanishesNonDeckGraveyardEntries: true),
        new("VEN·108", "遗落圣物", 5, "EQUIPMENT_PLAY:VEN·108", 0, 0,
            PlaysSourceToBaseAsEquipment: true, UnitEventAbility: new("PLAY_OR_START", "BURN_MIGHT_FRIENDLY")),
