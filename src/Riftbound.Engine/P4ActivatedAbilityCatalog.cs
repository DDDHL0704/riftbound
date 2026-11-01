using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record P4ActivatedAbilityDefinition(
    string AbilityId,
    string SourceCardNo,
    string EffectKind,
    string DisplayName,
    int ManaCost,
    int PowerCost,
    int RequiredTargetCount,
    bool RequiresBattlefieldSource,
    bool ExhaustsSourceAsCost,
    int DamageAmount,
    bool AppliesSpellshieldTargetTax,
    string Reason,
    bool IsResourceSkill = false,
    bool PaymentOnlyResource = false,
    int GeneratedPower = 0,
    bool UsesTargetAsCost = false,
    string ResourceRestriction = "",
    bool ReactionSpeed = false,
    int GeneratedMana = 0,
    IReadOnlyDictionary<string, int>? PowerCostByTrait = null,
    int ExperienceCost = 0,
    bool RequiresBaseEquipmentSource = false,
    IReadOnlyDictionary<string, int>? GeneratedPowerByTrait = null,
    string Kind = "",
    string TargetScope = "",
    int MainDeckLookCount = 0,
    int PlayPowerDelta = 0,
    bool IgnorePlayManaCost = false,
    bool RecycleUnplayedLookedCards = false,
    string PlayCardFilter = "",
    bool SwiftSpeed = false,
    bool SupportsCopiedRuleText = false,
    int PowerModifierAmount = 0,
    int MinimumPowerAfterModifier = 0,
    string SourceRequiredTag = "",
    SimpleActivatedEffectSpec? SimpleEffect = null,
    int? GeneratedManaIfSourceEmpowered = null,
    int? PowerModifierAmountIfSourceEmpowered = null,
    string SourceScope = "",
    string RequiredControllerCardNoOnField = "");

public sealed record SimpleActivatedEffectSpec(bool RemovesSourceEmpowermentAsCost = false,
    int DrawCount = 0, int CreatedRobotCount = 0, bool EmpowersOtherEquipment = false,
    bool ReadiesSource = false, int SourcePowerBonus = 0, bool OncePerTurn = false,
    bool RequiresEnemyUnitTargetThisTurn = false, bool DiscardsEquipmentAsCost = false, int BattlefieldUnitDamage = 0,
    bool TransfersSourceToChosenPlayer = false,
    bool CommandsOpponentUnitToSourceBattlefield = false,
    bool ReturnsFriendlyUnitThenFreePlayUpToPower = false,
    bool GrantsBoonOrPower = false, int ExistingBoonPowerBonus = 0,
    bool StunsTarget = false);

public sealed record P4SigilTypedResourceProfile(
    string AbilityId,
    string SourceCardNo,
    string EffectKind,
    string DisplayName,
    string Trait,
    string TraitLabel,
    bool IsOgnReprint);

public static class P4ActivatedAbilityCatalog
{
    public const string ViCardNo = "UNL-030/219";
    public const string ViAltACardNo = "UNL-030a/219";
    public const string ViDoublePowerAbilityId = "PAY_2_RED_DOUBLE_POWER";
    public const string ViDoublePowerAbilityEffectKind = "VI_PAY_2_RED_DOUBLE_POWER_UNTIL_END_OF_TURN";
    public const int ViDoublePowerAbilityManaCost = 2;
    public const int ViDoublePowerAbilityPowerCost = 1;

    public const string XerathCardNo = "UNL-026/219";
    public const string XerathDamageAbilityId = "PAY_RED_EXHAUST_DAMAGE_3";
    public const string XerathDamageAbilityEffectKind = "XERATH_PAY_RED_EXHAUST_DAMAGE_3";
    public const int XerathDamageAbilityPowerCost = 1;
    public const int XerathDamageAbilityDamageAmount = 3;

    public const string MalzaharCardNo = "OGN·113/298";
    public const string MalzaharResourceAbilityId = "MALZAHAR_EXHAUST_DESTROY_FRIENDLY_GAIN_2_POWER";
    public const string MalzaharResourceAbilityEffectKind = "MALZAHAR_RESOURCE_GAIN_2_POWER";
    public const int MalzaharResourceGeneratedPower = 2;

    public const string DragonSoulSageCardNo = "UNL-093/219";
    public const string DragonSoulSageResourceAbilityId = "DRAGON_SOUL_SAGE_REACTION_EXHAUST_GAIN_1_MANA";
    public const string DragonSoulSageResourceAbilityEffectKind = "DRAGON_SOUL_SAGE_REACTION_RESOURCE_SKILL_GAIN_1_MANA";
    public const int DragonSoulSageGeneratedMana = 1;

    public const string JhinCardNo = "UNL-022/219";
    public const string JhinAltACardNo = "UNL-022a/219";
    public const string JhinMoveResourceAbilityId = "JHIN_MOVE_TRIGGER_GAIN_1_MANA_1_POWER";
    public const string JhinMoveResourceAbilityEffectKind = "JHIN_MOVEMENT_RESOURCE_SKILL_GAIN_1_MANA_1_POWER";
    public const int JhinMoveResourceGeneratedMana = 1;
    public const int JhinMoveResourceGeneratedPower = 1;

    public const string HoneyfruitCardNo = "UNL-049/219";
    public const string HoneyfruitResourceAbilityId = "HONEYFRUIT_REACTION_EXHAUST_GAIN_GENERIC_POWER";
    public const string HoneyfruitResourceAbilityEffectKind = "HONEYFRUIT_REACTION_RESOURCE_SKILL_GAIN_GENERIC_POWER";
    public const int HoneyfruitGeneratedPower = 1;
    public const int HoneyfruitUpgradedGeneratedMana = 1;
    public const int HoneyfruitLevelSixExperience = 6;
    public const string HoneyfruitLevelSixOptionalCostPrefix = "HONEYFRUIT_LEVEL_SIX:";

    public const string BlueSentinelCardNo = "UNL-087/219";
    public const string BlueSentinelAltACardNo = "UNL-087a/219";
    public const string BlueSentinelResourceAbilityId = "BLUE_SENTINEL_HELD_DELAYED_NEXT_MAIN_GAIN_GENERIC_POWER";
    public const string BlueSentinelResourceAbilityEffectKind = "BLUE_SENTINEL_HELD_DELAYED_NEXT_MAIN_RESOURCE_SKILL_GAIN_GENERIC_POWER";
    public const int BlueSentinelGeneratedPower = 1;
    public const string BlueSentinelPaymentOnlyResourceRestriction = "PAY_RUNE_COSTS_ONLY_BLUE_SENTINEL_DELAYED_TEMPORARY_LEDGER_4D_03CQ";
    public const string BlueSentinelDelayedResourceActionPrefix = "BLUE_SENTINEL_DELAYED_RESOURCE:";

    public const string LuxCardNo = "OGS·014/024";
    public const string LuxResourceAbilityId = "LUX_REACTION_EXHAUST_GAIN_2_SPELL_ONLY_MANA";
    public const string LuxResourceAbilityEffectKind = "LUX_REACTION_RESOURCE_SKILL_GAIN_2_SPELL_ONLY_MANA";
    public const int LuxGeneratedMana = 2;
    public const string LuxSpellOnlyResourceRestriction = "PLAY_SPELLS_ONLY";
    public const string LuxSpellOnlyResourceActionPrefix = "LUX_SPELL_ONLY_RESOURCE:";

    public const string RenataGlascCardNo = "SFD·088/221";
    public const string RenataGlascAltCardNo = "SFD·088a/221";
    public const string RenataGlascDrawAbilityId = "RENATA_GLASC_PAY_1_BLUE_DRAW_1";
    public const string RenataGlascDrawAbilityEffectKind = "RENATA_GLASC_ACTIVATED_DRAW_1";
    public const int RenataGlascDrawManaCost = 1;
    public const int RenataGlascDrawBluePowerCost = 1;
    public const string RenataGlascScoreAbilityId = "RENATA_GLASC_PAY_4_BLUE4_EXHAUST_SCORE_1";
    public const string RenataGlascScoreAbilityEffectKind = "RENATA_GLASC_ACTIVATED_SCORE_1";
    public const int RenataGlascScoreManaCost = 4;
    public const int RenataGlascScoreBluePowerCost = 4;

    public const string AzirCardNo = "SFD·050/221";
    public const string AzirAltCardNo = "SFD·050a/221";
    public const string AzirSwiftSwapAbilityId = "AZIR_SWIFT_PAY_GREEN_SWAP_WITH_CONTROLLED_UNIT";
    public const string AzirSwiftSwapAbilityEffectKind = "AZIR_ACTIVATED_SWIFT_SWAP_WITH_CONTROLLED_UNIT";
    public const int AzirSwiftSwapGreenPowerCost = 1;
    public const string AzirSwiftSwapUsedThisTurnEffectPrefix = "AZIR_SWIFT_SWAP_USED_THIS_TURN:";
    public const string AzirArmamentReattachOptionalCostPrefix = "AZIR_REATTACH_ARMAMENT:";
    public const string AzirArmamentReattachPolicy = "implemented";

    public const string GatekeeperMaduliCardNo = "UNL-144/219";
    public const string GatekeeperMaduliMoveAbilityId = "GATEKEEPER_MADULI_PAY_PURPLE_MOVE_TO_WEAKER_ENEMY_BATTLEFIELD";
    public const string GatekeeperMaduliMoveAbilityEffectKind = "GATEKEEPER_MADULI_ACTIVATED_MOVE_TO_WEAKER_ENEMY_BATTLEFIELD";
    public const int GatekeeperMaduliMovePurplePowerCost = 1;

    public const string EzrealBlueSwiftCardNo = "SFD·082/221";
    public const string EzrealBlueSwiftAltCardNo = "SFD·082a/221";
    public const string EzrealBlueSwiftPromoCardNo = "SFD·082b/221·P";
    public const string EzrealBlueSwiftMoveAbilityId = "EZREAL_PAY_BLUE_SWIFT_MOVE_TO_BASE";
    public const string EzrealBlueSwiftMoveAbilityEffectKind = "EZREAL_ACTIVATED_SWIFT_MOVE_SELF_TO_BASE";
    public const int EzrealBlueSwiftMoveBluePowerCost = 1;

    public const string CrimsonRoseCardNo = "UNL-109/219";
    public const string CrimsonRoseReadyAbilityId = "CRIMSON_ROSE_EXPERIENCE3_EXHAUST_READY_UNIT";
    public const string CrimsonRoseReadyAbilityEffectKind = "CRIMSON_ROSE_ACTIVATED_READY_UNIT";
    public const int CrimsonRoseReadyExperienceCost = 3;

    public const string FluftPoroCardNo = "UNL-160/219";
    public const string FluftPoroWarhawkAbilityId = "FLUFT_PORO_EXHAUST_CREATE_TWO_SPELLSHIELD_WARHAWKS";
    public const string FluftPoroWarhawkAbilityEffectKind = "FLUFT_PORO_ACTIVATED_CREATE_TWO_WARHAWKS";
    public const string WarhawkTokenCardNo = P6TokenFactoryCatalog.WarhawkTokenCardNo;
    public const int FluftPoroWarhawkTokenCount = 2;

    public const string ShadowCardNo = "UNL-194/219";
    public const string ShadowStunAbilityId = "SHADOW_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER";
    public const string ShadowStunAbilityEffectKind = "SHADOW_ACTIVATED_STUN_ATTACKER";
    public const int ShadowStunManaCost = 1;
    public const int ShadowStunPowerCost = 1;

    public const string RageSigilCardNo = "SFD·222/221";
    public const string RageSigilResourceAbilityId = "RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER";
    public const string RageSigilResourceAbilityEffectKind = "RAGE_SIGIL_REACTION_TYPED_RESOURCE_GAIN_RED";
    public const int RageSigilGeneratedRedPower = 1;

    public const string FocusSigilCardNo = "SFD·226/221";
    public const string FocusSigilResourceAbilityId = "FOCUS_SIGIL_REACTION_EXHAUST_GAIN_1_GREEN_POWER";
    public const string FocusSigilResourceAbilityEffectKind = "FOCUS_SIGIL_REACTION_TYPED_RESOURCE_GAIN_GREEN";

    public const string InsightSigilCardNo = "SFD·229/221";
    public const string InsightSigilResourceAbilityId = "INSIGHT_SIGIL_REACTION_EXHAUST_GAIN_1_BLUE_POWER";
    public const string InsightSigilResourceAbilityEffectKind = "INSIGHT_SIGIL_REACTION_TYPED_RESOURCE_GAIN_BLUE";

    public const string PowerSigilCardNo = "SFD·231/221";
    public const string PowerSigilResourceAbilityId = "POWER_SIGIL_REACTION_EXHAUST_GAIN_1_ORANGE_POWER";
    public const string PowerSigilResourceAbilityEffectKind = "POWER_SIGIL_REACTION_TYPED_RESOURCE_GAIN_ORANGE";

    public const string DiscordSigilCardNo = "SFD·234/221";
    public const string DiscordSigilResourceAbilityId = "DISCORD_SIGIL_REACTION_EXHAUST_GAIN_1_PURPLE_POWER";
    public const string DiscordSigilResourceAbilityEffectKind = "DISCORD_SIGIL_REACTION_TYPED_RESOURCE_GAIN_PURPLE";

    public const string UnitySigilCardNo = "SFD·238/221";
    public const string UnitySigilResourceAbilityId = "UNITY_SIGIL_REACTION_EXHAUST_GAIN_1_YELLOW_POWER";
    public const string UnitySigilResourceAbilityEffectKind = "UNITY_SIGIL_REACTION_TYPED_RESOURCE_GAIN_YELLOW";

    public const string OgnRageSigilCardNo = "OGN·040/298";
    public const string OgnRageSigilResourceAbilityId = "OGN_RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER";
    public const string OgnRageSigilResourceAbilityEffectKind = "OGN_RAGE_SIGIL_REACTION_TYPED_RESOURCE_GAIN_RED";

    public const string OgnFocusSigilCardNo = "OGN·081/298";
    public const string OgnFocusSigilResourceAbilityId = "OGN_FOCUS_SIGIL_REACTION_EXHAUST_GAIN_1_GREEN_POWER";
    public const string OgnFocusSigilResourceAbilityEffectKind = "OGN_FOCUS_SIGIL_REACTION_TYPED_RESOURCE_GAIN_GREEN";

    public const string OgnInsightSigilCardNo = "OGN·120/298";
    public const string OgnInsightSigilResourceAbilityId = "OGN_INSIGHT_SIGIL_REACTION_EXHAUST_GAIN_1_BLUE_POWER";
    public const string OgnInsightSigilResourceAbilityEffectKind = "OGN_INSIGHT_SIGIL_REACTION_TYPED_RESOURCE_GAIN_BLUE";

    public const string OgnPowerSigilCardNo = "OGN·163/298";
    public const string OgnPowerSigilResourceAbilityId = "OGN_POWER_SIGIL_REACTION_EXHAUST_GAIN_1_ORANGE_POWER";
    public const string OgnPowerSigilResourceAbilityEffectKind = "OGN_POWER_SIGIL_REACTION_TYPED_RESOURCE_GAIN_ORANGE";

    public const string OgnDiscordSigilCardNo = "OGN·204/298";
    public const string OgnDiscordSigilResourceAbilityId = "OGN_DISCORD_SIGIL_REACTION_EXHAUST_GAIN_1_PURPLE_POWER";
    public const string OgnDiscordSigilResourceAbilityEffectKind = "OGN_DISCORD_SIGIL_REACTION_TYPED_RESOURCE_GAIN_PURPLE";

    public const string OgnUnitySigilCardNo = "OGN·245/298";
    public const string OgnUnitySigilResourceAbilityId = "OGN_UNITY_SIGIL_REACTION_EXHAUST_GAIN_1_YELLOW_POWER";
    public const string OgnUnitySigilResourceAbilityEffectKind = "OGN_UNITY_SIGIL_REACTION_TYPED_RESOURCE_GAIN_YELLOW";

    public const string EnergyChannelCardNo = "OGN·098/298";
    public const string EnergyChannelResourceAbilityId = "ENERGY_CHANNEL_REACTION_EXHAUST_GAIN_1_MANA";
    public const string EnergyChannelResourceAbilityEffectKind = "ENERGY_CHANNEL_REACTION_RESOURCE_SKILL_GAIN_1_MANA";
    public const int EnergyChannelGeneratedMana = 1;

    public const string AncientSteleCardNo = "SFD·117/221";
    public const string AncientSteleResourceAbilityId = "ANCIENT_STELE_REACTION_PAY_MANA_GAIN_GENERIC_POWER";
    public const string AncientSteleResourceAbilityEffectKind = "ANCIENT_STELE_REACTION_RESOURCE_CONVERT_MANA_TO_GENERIC_POWER";
    public const string AncientSteleConversionOptionalCostPrefix = "CONVERT_MANA_TO_GENERIC_POWER:";

    public const string HextechAnomalyCardNo = "SFD·083/221";
    public const string HextechAnomalyResourceAbilityId = "HEXTECH_ANOMALY_REACTION_PAY_GENERIC_POWER_GAIN_MANA";
    public const string HextechAnomalyResourceAbilityEffectKind = "HEXTECH_ANOMALY_REACTION_RESOURCE_CONVERT_GENERIC_POWER_TO_MANA";
    public const string HextechAnomalyConversionOptionalCostPrefix = "CONVERT_GENERIC_POWER_TO_MANA:";

    public const string GoldTokenUnlCardNo = "UNL·T05";
    public const string GoldTokenSfdCardNo = "SFD·T03";
    public const string GoldTokenUnlResourceAbilityId = "GOLD_TOKEN_UNL_REACTION_DESTROY_EXHAUST_GAIN_GENERIC_POWER";
    public const string GoldTokenSfdResourceAbilityId = "GOLD_TOKEN_SFD_REACTION_DESTROY_EXHAUST_GAIN_GENERIC_POWER";
    public const string GoldTokenUnlResourceAbilityEffectKind = "GOLD_TOKEN_UNL_REACTION_RESOURCE_DESTROY_GAIN_GENERIC_POWER";
    public const string GoldTokenSfdResourceAbilityEffectKind = "GOLD_TOKEN_SFD_REACTION_RESOURCE_DESTROY_GAIN_GENERIC_POWER";
    public const int GoldTokenGeneratedPower = 1;
    public const string GoldTokenRenataBonusTag = "RENATA_GOLD_EXTRA_1_MANA";
    public const int GoldTokenRenataBonusMana = 1;

    private static readonly Lazy<IReadOnlyList<P4SigilTypedResourceProfile>> SigilTypedResourceProfiles =
        new(BuildSigilTypedResourceProfiles, LazyThreadSafetyMode.ExecutionAndPublication);

    public const string ScryingBlossomAbilityId = "SCRYING_BLOSSOM_DESTROY_INSIGHT_DRAW_EXPERIENCE";

    public const string NextSpellEchoAbilityId = "NEXT_SPELL_ECHO";
    public const string NextSpellEchoEffectKind = "GRANT_NEXT_SPELL_BASE_COST_ECHO";

    public const string SoulOfInspirationCardNo = "RAD·040/167";
    public const string SoulOfInspirationAbilityId = "RAD_040_FRIENDLY_LEGEND_POWER";
    public const string SoulOfInspirationAbilityEffectKind = "RAD_040_FRIENDLY_LEGEND_POWER";

    public const string LanternDragonCardNo = "RAD·113/167";
    public const string LanternDragonAbilityId = "RAD_113_COMMAND_OPPONENT_MOVE_TO_SOURCE";
    public const string LanternDragonAbilityEffectKind = "RAD_113_COMMAND_OPPONENT_MOVE_TO_SOURCE";

    public const string TranspositionMageCardNo = "RAD·115/167";
    public const string TranspositionMageAbilityId = "RAD_115_RETURN_FRIENDLY_PLAY_LOWER_POWER";
    public const string TranspositionMageAbilityEffectKind = "RAD_115_RETURN_FRIENDLY_PLAY_LOWER_POWER";

    public const string PrismwingFlockCardNo = "RAD·027/167";
    public const string PrismwingFlockAbilityId = "RAD_027_GRANT_BOON_OR_POWER";
    public const string PrismwingFlockAbilityEffectKind = "RAD_027_GRANT_BOON_OR_POWER";

    public const string RelentlessStalkerCardNo = "RAD·104/167";
    public const string RelentlessStalkerAbilityId = "RAD_104_EXHAUST_STUN_ENEMY_UNIT";
    public const string RelentlessStalkerAbilityEffectKind = "RAD_104_EXHAUST_STUN_ENEMY_UNIT";

    private static readonly P4ActivatedAbilityDefinition[] Definitions =
    [
        new(LanternDragonAbilityId, LanternDragonCardNo, LanternDragonAbilityEffectKind, "提灯亚龙", 0, 0, 1,
            RequiresBattlefieldSource: false, ExhaustsSourceAsCost: true, DamageAmount: 0,
            AppliesSpellshieldTargetTax: false,
            Reason: "官方中文：横置，选择一名对手；该对手必须将其控制的一名单位移动到提灯亚龙所在战场。",
            SourceRequiredTag: CardObjectTags.UnitCard,
            SimpleEffect: new(CommandsOpponentUnitToSourceBattlefield: true)),
        new(TranspositionMageAbilityId, TranspositionMageCardNo, TranspositionMageAbilityEffectKind, "易位法师", 0, 0, 1,
            RequiresBattlefieldSource: false, ExhaustsSourceAsCost: true, DamageAmount: 0,
            AppliesSpellshieldTargetTax: false,
            Reason: "官方中文：支付1紫色符能并横置，让一名友方单位返回其所属的手牌；你可以选择打出一名战力不高于该单位的单位，无视其费用。",
            PowerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal) { [RuneTrait.Purple] = 1 },
            SourceRequiredTag: CardObjectTags.UnitCard,
            SimpleEffect: new(ReturnsFriendlyUnitThenFreePlayUpToPower: true)),
        new(PrismwingFlockAbilityId, PrismwingFlockCardNo, PrismwingFlockAbilityEffectKind, "幻彩飞禽", 0, 0, 1,
            RequiresBattlefieldSource: false, ExhaustsSourceAsCost: true, DamageAmount: 0,
            AppliesSpellshieldTargetTax: true,
            Reason: "官方中文：横置，给予一名单位增益；如果该单位已拥有增益，则改为给予其本回合内战力+2。",
            SourceRequiredTag: CardObjectTags.UnitCard,
            SimpleEffect: new(GrantsBoonOrPower: true, ExistingBoonPowerBonus: 2)),
        new(RelentlessStalkerAbilityId, RelentlessStalkerCardNo, RelentlessStalkerAbilityEffectKind, "无情潜伏者", 0, 0, 1,
            RequiresBattlefieldSource: false, ExhaustsSourceAsCost: true, DamageAmount: 0,
            AppliesSpellshieldTargetTax: true,
            Reason: "官方中文：横置，眩晕一名敌方单位。",
            SourceRequiredTag: CardObjectTags.UnitCard,
            SimpleEffect: new(StunsTarget: true)),
        new("GLOWING_STONE_TRANSFER", "VEN·133", "GLOWING_STONE_TRANSFER", "发光石", 0, 0, 1,
            false, true, 0, false, "解除强化并横置：选择一名玩家，该玩家获得此牌的控制权并将其召回。",
            SourceRequiredTag: CardObjectTags.EquipmentCard,
            SimpleEffect: new(RemovesSourceEmpowermentAsCost: true, TransfersSourceToChosenPlayer: true)),
        new("SKY_STRIDER_DISCARD_DAMAGE", "VEN·060", "SKY_STRIDER_DISCARD_DAMAGE", "天际漫游者", 1, 0, 1,
            false, true, 4, true, "官方中文：弃置装备、支付1并横置，对战场上的一名单位造成4点伤害。",
            SourceRequiredTag: CardObjectTags.UnitCard, SimpleEffect: new(DiscardsEquipmentAsCost: true, BattlefieldUnitDamage: 4)),
        new("TUNDRA_HUNTER_READY", "VEN·125", "TUNDRA_HUNTER_READY", "冰原饿狼", 0, 0, 0,
            false, false, 0, false, "官方中文：本回合已选择过敌方单位后，支付黄色，活跃并本回合+1；每回合一次。",
            PowerCostByTrait: new Dictionary<string, int> { ["yellow"] = 1 }, SourceRequiredTag: CardObjectTags.UnitCard,
            SimpleEffect: new(ReadiesSource: true, SourcePowerBonus: 1, OncePerTurn: true, RequiresEnemyUnitTargetThisTurn: true)),
        new("IMPERIAL_TOOL_POWER", "VEN·077", "IMPERIAL_TOOL_POWER", "帝国工具", 0, 0, 1,
            false, true, 0, true, "官方中文：横置，单位本回合+2；来源已强化时改为+4。",
            Kind: ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn, TargetScope: CardTargetScopes.AnyUnit,
            PowerModifierAmount: 2, SourceRequiredTag: CardObjectTags.EquipmentCard, PowerModifierAmountIfSourceEmpowered: 4),
        new("BOOK_REMOVE_EMPOWER_DRAW", "VEN·054", "BOOK_REMOVE_EMPOWER_DRAW", "可疑之书", 1, 0, 0,
            false, true, 0, false, "官方中文：解除强化、支付1并横置，抽一张牌。",
            RequiresBaseEquipmentSource: true, SourceRequiredTag: CardObjectTags.EquipmentCard,
            SimpleEffect: new(RemovesSourceEmpowermentAsCost: true, DrawCount: 1)),
        new("HEXTECH_DISC_CREATE_ROBOT", "VEN·087", "HEXTECH_DISC_CREATE_ROBOT", "海克斯圆盘", 1, 0, 0,
            false, true, 0, false, "官方中文：解除强化、支付1并横置，打出3战力机器人。",
            RequiresBaseEquipmentSource: true, SourceRequiredTag: CardObjectTags.EquipmentCard,
            SimpleEffect: new(RemovesSourceEmpowermentAsCost: true, CreatedRobotCount: 1)),
        new("HEXTECH_EQUATION_EMPOWER_EQUIPMENT", "VEN·062", "HEXTECH_EQUATION_EMPOWER_EQUIPMENT", "海克斯方程式", 0, 0, 1,
            false, true, 0, true, "官方中文：横置，强化另一件装备。",
            RequiresBaseEquipmentSource: true, SourceRequiredTag: CardObjectTags.EquipmentCard,
            SimpleEffect: new(EmpowersOtherEquipment: true)),
        new("EGG_REACTION_GAIN_MANA", "VEN·075", "EGG_REACTION_GAIN_MANA", "剑头蛟的卵", 0, 0, 0,
            false, true, 0, false, "官方中文：反应横置，获得1法力，已强化改为2。",
            IsResourceSkill: true, ReactionSpeed: true, GeneratedMana: 1,
            RequiresBaseEquipmentSource: true, SourceRequiredTag: CardObjectTags.EquipmentCard,
            GeneratedManaIfSourceEmpowered: 2),
        new(ScryingBlossomAbilityId, "UNL-136/219", "INSIGHT_TRIGGER", "占卜花朵 · 洞察 2 后抽牌",
            1, 0, 0, RequiresBattlefieldSource: false, ExhaustsSourceAsCost: true, 0,
            AppliesSpellshieldTargetTax: false, "支付 1 法力、横置并摧毁此牌：洞察 2，然后抽一张牌并获得 1 经验", RequiresBaseEquipmentSource: true),
        new(NextSpellEchoAbilityId, "SFD·078/221", NextSpellEchoEffectKind, "预时之门 · 下个法术获得回响",
            0, 1, 0, RequiresBattlefieldSource: false, ExhaustsSourceAsCost: true, 0,
            AppliesSpellshieldTargetTax: false, "支付任意符能并横置，结算后授予本回合下个法术回响", RequiresBaseEquipmentSource: true),
        new(
            ViDoublePowerAbilityId,
            ViCardNo,
            ViDoublePowerAbilityEffectKind,
            "Vi double-power skill",
            ViDoublePowerAbilityManaCost,
            ViDoublePowerAbilityPowerCost,
            0,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: false,
            0,
            AppliesSpellshieldTargetTax: false,
            "P4.389 keeps Vi's already verified no-target paid skill behind a registry entry before broader skill migration."),
        new(
            XerathDamageAbilityId,
            XerathCardNo,
            XerathDamageAbilityEffectKind,
            "Xerath damage skill",
            0,
            XerathDamageAbilityPowerCost,
            1,
            RequiresBattlefieldSource: true,
            ExhaustsSourceAsCost: true,
            XerathDamageAbilityDamageAmount,
            AppliesSpellshieldTargetTax: true,
            "P4.389 keeps Xerath's verified one-target damage skill and spellshield target tax behind a registry entry."),
        new(
            MalzaharResourceAbilityId,
            MalzaharCardNo,
            MalzaharResourceAbilityEffectKind,
            "Malzahar payment resource skill",
            0,
            0,
            1,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "CN 357.2 and 429: destroy a friendly permanent and exhaust; gain two wildcard power immediately.",
            IsResourceSkill: true,
            GeneratedPower: MalzaharResourceGeneratedPower,
            UsesTargetAsCost: true),
        new(
            DragonSoulSageResourceAbilityId,
            DragonSoulSageCardNo,
            DragonSoulSageResourceAbilityEffectKind,
            "Dragon Soul Sage reaction resource skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "Stage 4D-03L opens only Dragon Soul Sage's reaction-speed no-target resource skill representative; the broader reaction resource skill family remains deferred.",
            IsResourceSkill: true,
            ReactionSpeed: true,
            GeneratedMana: DragonSoulSageGeneratedMana),
        new(
            JhinMoveResourceAbilityId,
            JhinCardNo,
            JhinMoveResourceAbilityEffectKind,
            "Jhin movement-triggered resource skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: false,
            0,
            AppliesSpellshieldTargetTax: false,
            "CN 429.2: movement-triggered resources resolve automatically into the rune pool.",
            IsResourceSkill: true,
            GeneratedPower: JhinMoveResourceGeneratedPower,
            GeneratedMana: JhinMoveResourceGeneratedMana, Kind: "TRIGGERED_RESOURCE"),
        new(
            HoneyfruitResourceAbilityId,
            HoneyfruitCardNo,
            HoneyfruitResourceAbilityEffectKind,
            "Honeyfruit reaction resource skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "CN 166-168 and 429: resources enter the rune pool with normal timing and lifetime.",
            IsResourceSkill: true,
            GeneratedPower: HoneyfruitGeneratedPower,
            ReactionSpeed: true,
            RequiresBaseEquipmentSource: true),
        new(
            BlueSentinelResourceAbilityId,
            BlueSentinelCardNo,
            BlueSentinelResourceAbilityEffectKind,
            "Blue Sentinel held-battlefield delayed resource skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: true,
            ExhaustsSourceAsCost: false,
            0,
            AppliesSpellshieldTargetTax: false,
            "Stage 4D-03CQ opens Blue Sentinel's server-captured held-battlefield delayed next-main payment-only resource representative.",
            IsResourceSkill: true,
            PaymentOnlyResource: true,
            GeneratedPower: BlueSentinelGeneratedPower,
            ResourceRestriction: BlueSentinelPaymentOnlyResourceRestriction),
        new(
            LuxResourceAbilityId,
            LuxCardNo,
            LuxResourceAbilityEffectKind,
            "Lux spell-only reaction resource skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "CN166-168/430: reaction resource mana retains its spell-play restriction until normal rune-pool clearing.",
            IsResourceSkill: true,
            PaymentOnlyResource: false,
            ReactionSpeed: true,
            GeneratedMana: LuxGeneratedMana,
            ResourceRestriction: LuxSpellOnlyResourceRestriction),
        new(
            RenataGlascDrawAbilityId,
            RenataGlascCardNo,
            RenataGlascDrawAbilityEffectKind,
            "Renata Glasc draw skill",
            RenataGlascDrawManaCost,
            0,
            0,
            RequiresBattlefieldSource: true,
            ExhaustsSourceAsCost: false,
            0,
            AppliesSpellshieldTargetTax: false,
            "Stage 4D-03M opens only Renata Glasc's pay 1 and blue draw representative; the score skill and broader colored activated family remain deferred.",
            PowerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Blue] = RenataGlascDrawBluePowerCost
            }),
        new(
            RenataGlascScoreAbilityId,
            RenataGlascCardNo,
            RenataGlascScoreAbilityEffectKind,
            "Renata Glasc score skill",
            RenataGlascScoreManaCost,
            0,
            0,
            RequiresBattlefieldSource: true,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "Stage 4D-03N opens only Renata Glasc's pay 4 and four blue score representative; the broader colored activated family remains deferred.",
            PowerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Blue] = RenataGlascScoreBluePowerCost
            }),
        new(
            AzirSwiftSwapAbilityId,
            AzirCardNo,
            AzirSwiftSwapAbilityEffectKind,
            "Azir swift swap skill",
            0,
            0,
            1,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: false,
            0,
            AppliesSpellshieldTargetTax: false,
            "Printed Azir Swift movements use shared actions; the conditional armament target is confirmed before responses, with optional attachment decided after movements. Copied instances retain independent use and captured origins.",
            PowerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Green] = AzirSwiftSwapGreenPowerCost
            }, SwiftSpeed: true, SupportsCopiedRuleText: true),
        new(
            GatekeeperMaduliMoveAbilityId,
            GatekeeperMaduliCardNo,
            GatekeeperMaduliMoveAbilityEffectKind,
            "Gatekeeper Maduli purple battlefield move skill",
            0,
            0,
            1,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: false,
            0,
            AppliesSpellshieldTargetTax: false,
            "Stage 4D-03AR keeps Gatekeeper Maduli's pay purple move representative and implements its cannot-become-active static representative; the broader movement family remains deferred.",
            PowerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Purple] = GatekeeperMaduliMovePurplePowerCost
            }),
        new(
            EzrealBlueSwiftMoveAbilityId,
            EzrealBlueSwiftCardNo,
            EzrealBlueSwiftMoveAbilityEffectKind,
            "Ezreal blue swift move-to-base skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: true,
            ExhaustsSourceAsCost: false,
            0,
            AppliesSpellshieldTargetTax: false,
            "Swift self movement uses the shared action window and captured source identity.",
            PowerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Blue] = EzrealBlueSwiftMoveBluePowerCost
            }, SwiftSpeed: true, SupportsCopiedRuleText: true, Kind: ActivatedAbilityKinds.SwiftMoveSelfToBase),
        new(
            CrimsonRoseReadyAbilityId,
            CrimsonRoseCardNo,
            CrimsonRoseReadyAbilityEffectKind,
            "Crimson Rose ready-unit skill",
            0,
            0,
            1,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: true,
            "Stage 4D-03O opens only Crimson Rose's spend 3 experience, exhaust, ready a unit representative; the unit-play experience trigger and broader target-bearing family remain deferred.",
            ExperienceCost: CrimsonRoseReadyExperienceCost,
            RequiresBaseEquipmentSource: true),
        new(
            FluftPoroWarhawkAbilityId,
            FluftPoroCardNo,
            FluftPoroWarhawkAbilityEffectKind,
            "Fluft Poro Warhawk skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: true,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "Stage 4D-03P opens only Fluft Poro's battlefield-only no-target Warhawk token representative; broader token-play semantics remain deferred.",
            ExperienceCost: 0),
        new(
            ShadowStunAbilityId,
            ShadowCardNo,
            ShadowStunAbilityEffectKind,
            "Shadow swift stun skill",
            ShadowStunManaCost,
            ShadowStunPowerCost,
            1,
            RequiresBattlefieldSource: true,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: true,
            "Shadow's printed Swift skill chooses an enemy attacking here through an ordinary or spell-duel focus window.",
            SwiftSpeed: true),
        new(
            EnergyChannelResourceAbilityId,
            EnergyChannelCardNo,
            EnergyChannelResourceAbilityEffectKind,
            "Energy Channel reaction mana resource skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "Stage 4D-03U opens only Energy Channel's base-equipment reaction-speed gain 1 mana representative; broader equipment resource conversion remains deferred.",
            IsResourceSkill: true,
            ReactionSpeed: true,
            GeneratedMana: EnergyChannelGeneratedMana,
            RequiresBaseEquipmentSource: true),
        new(
            AncientSteleResourceAbilityId,
            AncientSteleCardNo,
            AncientSteleResourceAbilityEffectKind,
            "Ancient Stele reaction mana-to-power conversion resource skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "CN 166-168 and 429: resources enter the rune pool with normal timing and lifetime.",
            IsResourceSkill: true,
            ReactionSpeed: true,
            RequiresBaseEquipmentSource: true),
        new(
            HextechAnomalyResourceAbilityId,
            HextechAnomalyCardNo,
            HextechAnomalyResourceAbilityEffectKind,
            "Hextech Anomaly reaction power-to-mana conversion resource skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "Stage 4D-03U opens only Hextech Anomaly's base-equipment reaction-speed ordinary generic power-to-mana conversion representative.",
            IsResourceSkill: true,
            ReactionSpeed: true,
            RequiresBaseEquipmentSource: true),
        new(
            GoldTokenUnlResourceAbilityId,
            GoldTokenUnlCardNo,
            GoldTokenUnlResourceAbilityEffectKind,
            "UNL Gold token reaction resource skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "CN 166-168 and 429: resources enter the rune pool with normal timing and lifetime.",
            IsResourceSkill: true,
            GeneratedPower: GoldTokenGeneratedPower,
            ReactionSpeed: true,
            RequiresBaseEquipmentSource: true),
        new(
            GoldTokenSfdResourceAbilityId,
            GoldTokenSfdCardNo,
            GoldTokenSfdResourceAbilityEffectKind,
            "SFD Gold token reaction resource skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "CN 166-168 and 429: resources enter the rune pool with normal timing and lifetime.",
            IsResourceSkill: true,
            GeneratedPower: GoldTokenGeneratedPower,
            ReactionSpeed: true,
            RequiresBaseEquipmentSource: true),
        new(
            SoulOfInspirationAbilityId,
            SoulOfInspirationCardNo,
            SoulOfInspirationAbilityEffectKind,
            "鼓舞之魂：传奇技能",
            0,
            0,
            1,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "官方中文：所有友方传奇具有“横置：给予一名单位在本回合内战力+3。”",
            Kind: ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn,
            TargetScope: CardTargetScopes.AnyUnit,
            PowerModifierAmount: 3,
            SourceScope: ActivatedAbilitySourceScopes.FriendlyLegend,
            RequiredControllerCardNoOnField: SoulOfInspirationCardNo),
        .. SigilTypedResourceProfiles.Value.Select(SigilTypedResourceDefinition)
    ];

    private static readonly Lazy<IReadOnlyList<P4ActivatedAbilityDefinition>> BehaviorSpecActivatedAbilityDefinitions =
        new(BuildBehaviorSpecActivatedAbilityDefinitions, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> SourceCardNosByRepresentativeCardNo =
        new(BuildSourceCardNosByRepresentativeCardNo, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<P4ActivatedAbilityDefinition> GetAll()
    {
        return Definitions
            .Concat(BehaviorSpecActivatedAbilityDefinitions.Value)
            .ToArray();
    }

    public static bool TryGetByAbilityId(
        string abilityId,
        out P4ActivatedAbilityDefinition definition)
    {
        definition = GetAll().FirstOrDefault(candidate => string.Equals(
            candidate.AbilityId,
            abilityId,
            StringComparison.Ordinal))!;
        return definition is not null;
    }

    public static bool TryGetByEffectKind(
        string effectKind,
        out P4ActivatedAbilityDefinition definition)
    {
        definition = GetAll().FirstOrDefault(candidate => string.Equals(
            candidate.EffectKind,
            effectKind,
            StringComparison.Ordinal))!;
        return definition is not null;
    }

    public static bool IsSourceCardNoForAbility(
        P4ActivatedAbilityDefinition definition,
        string? cardNo)
    {
        return SourceCardNosForAbility(definition)
            .Contains(cardNo ?? string.Empty, StringComparer.Ordinal);
    }

    public static bool IsSourceCardNoForAbilityId(
        string abilityId,
        string? cardNo)
    {
        return TryGetByAbilityId(abilityId, out var definition)
            && IsSourceCardNoForAbility(definition, cardNo);
    }

    public static IReadOnlyList<string> SourceCardNosForAbility(P4ActivatedAbilityDefinition definition)
    {
        var sourceCardNo = OfficialCardSourceIdentityGroups.NormalizeCardNo(definition.SourceCardNo);
        // Both exact current CN prints were source-checked. Their punctuation
        // and reminder text differ, so whole-text identity cannot map this skill.
        if (definition.AbilityId == LuxResourceAbilityId && sourceCardNo == LuxCardNo)
            return [LuxCardNo, "VEN·SP6"];
        var sources = SourceCardNosByRepresentativeCardNo.Value.TryGetValue(sourceCardNo, out var sourceCardNos)
            ? sourceCardNos
            : [sourceCardNo];
        return sources;
    }

    public static string AzirSwiftSwapUsedThisTurnEffectId(string playerId, string sourceObjectId, long generation = 0, string? instanceId = null)
    {
        // The printed ability belongs to this continuous object, regardless of its controller.
        return $"{AzirSwiftSwapUsedThisTurnEffectPrefix}{sourceObjectId}:{generation}" + (instanceId is null ? "" : ":" + instanceId);
    }

    public static string AzirArmamentReattachOptionalCostId(string equipmentObjectId)
    {
        return $"{AzirArmamentReattachOptionalCostPrefix}{equipmentObjectId}";
    }

    public static bool TryParseAzirArmamentReattachOptionalCost(string optionalCost, out string equipmentObjectId)
    {
        equipmentObjectId = string.Empty;
        if (string.IsNullOrWhiteSpace(optionalCost)
            || !optionalCost.StartsWith(AzirArmamentReattachOptionalCostPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        equipmentObjectId = optionalCost[AzirArmamentReattachOptionalCostPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(equipmentObjectId);
    }

    public static IReadOnlyDictionary<string, int> PowerCostByTraitForAbility(P4ActivatedAbilityDefinition definition)
    {
        return PaymentCostRules.NormalizePowerCostByTrait(
            definition.PowerCostByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal));
    }

    public static IReadOnlyDictionary<string, int> GeneratedPowerByTraitForAbility(P4ActivatedAbilityDefinition definition)
    {
        return PaymentCostRules.NormalizePowerCostByTrait(
            definition.GeneratedPowerByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal));
    }

    public static bool IsSigilTypedResourceAbility(string? abilityId)
    {
        return SigilTypedResourceProfiles.Value.Any(profile => string.Equals(profile.AbilityId, abilityId, StringComparison.Ordinal));
    }

    public static bool IsResourceConversionEquipmentAbility(string? abilityId)
    {
        return string.Equals(abilityId, EnergyChannelResourceAbilityId, StringComparison.Ordinal)
            || string.Equals(abilityId, AncientSteleResourceAbilityId, StringComparison.Ordinal)
            || string.Equals(abilityId, HextechAnomalyResourceAbilityId, StringComparison.Ordinal);
    }

    public static bool IsGoldTokenResourceAbility(string? abilityId)
    {
        return string.Equals(abilityId, GoldTokenUnlResourceAbilityId, StringComparison.Ordinal)
            || string.Equals(abilityId, GoldTokenSfdResourceAbilityId, StringComparison.Ordinal);
    }

    public static bool IsHoneyfruitResourceAbility(string? abilityId)
    {
        return string.Equals(abilityId, HoneyfruitResourceAbilityId, StringComparison.Ordinal);
    }

    public static bool IsBlueSentinelResourceAbility(string? abilityId)
    {
        return string.Equals(abilityId, BlueSentinelResourceAbilityId, StringComparison.Ordinal);
    }

    public static bool IsLuxResourceAbility(string? abilityId)
    {
        return string.Equals(abilityId, LuxResourceAbilityId, StringComparison.Ordinal);
    }

    public static bool TryGetSigilTypedResourceProfile(
        string? abilityId,
        out P4SigilTypedResourceProfile profile)
    {
        profile = SigilTypedResourceProfiles.Value.FirstOrDefault(candidate =>
            string.Equals(candidate.AbilityId, abilityId, StringComparison.Ordinal))!;
        return profile is not null;
    }

    public static bool IsSfdSigilTypedResourceAbility(string? abilityId)
    {
        return SigilTypedResourceProfiles.Value.Any(profile =>
            !profile.IsOgnReprint
            && string.Equals(profile.AbilityId, abilityId, StringComparison.Ordinal));
    }

    public static bool TryGetSfdSigilTypedResourceProfile(
        string? abilityId,
        out P4SigilTypedResourceProfile profile)
    {
        profile = SigilTypedResourceProfiles.Value.FirstOrDefault(candidate =>
            !candidate.IsOgnReprint
            && string.Equals(candidate.AbilityId, abilityId, StringComparison.Ordinal))!;
        return profile is not null;
    }

    public static IReadOnlyList<P4SigilTypedResourceProfile> GetSigilTypedResourceProfiles()
    {
        return SigilTypedResourceProfiles.Value;
    }

    public static IReadOnlyList<P4SigilTypedResourceProfile> GetSfdSigilTypedResourceProfiles()
    {
        return SigilTypedResourceProfiles.Value
            .Where(profile => !profile.IsOgnReprint)
            .ToArray();
    }

    public static IReadOnlyList<P4SigilTypedResourceProfile> GetOgnSigilTypedResourceProfiles()
    {
        return SigilTypedResourceProfiles.Value
            .Where(profile => profile.IsOgnReprint)
            .ToArray();
    }

    private static P4ActivatedAbilityDefinition SigilTypedResourceDefinition(P4SigilTypedResourceProfile profile)
    {
        return new P4ActivatedAbilityDefinition(
            profile.AbilityId,
            profile.SourceCardNo,
            profile.EffectKind,
            $"{profile.DisplayName} reaction typed resource skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: false,
            ExhaustsSourceAsCost: true,
            0,
            AppliesSpellshieldTargetTax: false,
            "Stage 4D typed Sigil slices open the SFD/OGN base-equipment reaction-speed typed payment-only resource representatives; the broader Sigil family remains deferred.",
            IsResourceSkill: true,
            ReactionSpeed: true,
            RequiresBaseEquipmentSource: true,
            GeneratedPowerByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [profile.Trait] = 1
            });
    }

    private static IReadOnlyList<P4SigilTypedResourceProfile> BuildSigilTypedResourceProfiles()
    {
        var catalog = OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var behaviors = CardBehaviorRegistry.GetAll()
            .Select(behavior => new ImplementedCardBehavior(
                behavior.CardNo,
                behavior.EffectKind,
                behavior.DisplayName))
            .ToArray();
        var implementedBehaviors = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(
            catalog.Cards,
            behaviors);

        return BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, implementedBehaviors)
            .SelectMany(spec => spec.ActivatedAbilities
                .Where(IsSigilTypedResourceActivatedAbility)
                .Select(ability => SigilTypedResourceProfile(spec, ability)))
            .OrderBy(profile => profile.IsOgnReprint ? 1 : 0)
            .ThenBy(profile => profile.SourceCardNo, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<P4ActivatedAbilityDefinition> BuildBehaviorSpecActivatedAbilityDefinitions()
    {
        var catalog = OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var behaviors = CardBehaviorRegistry.GetAll()
            .Select(behavior => new ImplementedCardBehavior(
                behavior.CardNo,
                behavior.EffectKind,
                behavior.DisplayName))
            .ToArray();
        var implementedBehaviors = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(
            catalog.Cards,
            behaviors);

        return BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, implementedBehaviors)
            .SelectMany(spec => spec.ActivatedAbilities
                .Where(ability => IsDestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRestActivatedAbility(ability)
                    || IsTargetedUnitExhaustSpec(ability) && spec.CardCategoryName.Contains("单位", StringComparison.Ordinal)
                    || IsTargetedPowerModifier(ability) && (spec.CardCategoryName.Contains("装备", StringComparison.Ordinal)
                        || spec.CardCategoryName.Contains("单位", StringComparison.Ordinal)))
                .Select(ability => IsTargetedUnitExhaustSpec(ability) ? TargetedUnitExhaustDefinition(spec, ability)
                    : IsTargetedPowerModifier(ability)
                    ? TargetedPowerModifierDefinition(spec, ability)
                    : DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRestDefinition(spec, ability)))
            // Some reviewed cards have a hand-written definition because their shared
            // ability carries card-specific source constraints. Keep that definition
            // authoritative and avoid registering the equivalent generated one twice.
            .Where(definition => !Definitions.Any(existing =>
                string.Equals(existing.SourceCardNo, definition.SourceCardNo, StringComparison.Ordinal)
                && string.Equals(existing.Kind, definition.Kind, StringComparison.Ordinal)))
            .OrderBy(definition => definition.SourceCardNo, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool IsOrdinaryTargetedExhaustAbility(P4ActivatedAbilityDefinition ability)
        => ability.Kind is ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn
            or ActivatedAbilityKinds.StunEnemyUnit or ActivatedAbilityKinds.GrantUnitBoonOrTemporaryPower;

    private static bool IsTargetedUnitExhaustSpec(ActivatedAbilitySpec ability)
        => ability.ExhaustsSourceAsCost == true && ability.RequiredTargetCount == 1
            && ability.ManaCost == 0 && ability.PowerCost == 0
            && (ability.Kind == ActivatedAbilityKinds.StunEnemyUnit && ability.TargetScope == CardTargetScopes.EnemyUnit
                || ability.Kind == ActivatedAbilityKinds.GrantUnitBoonOrTemporaryPower
                    && ability.TargetScope == CardTargetScopes.AnyUnit && ability.PowerModifierAmount is > 0);

    private static P4ActivatedAbilityDefinition TargetedUnitExhaustDefinition(BehaviorSpec spec, ActivatedAbilitySpec ability)
    {
        var id = BuildBehaviorSpecAbilityId(spec.CardNo, ability.Kind!);
        return new(id, spec.CardNo, id, spec.CardName, 0, 0, 1, false, true, 0, true,
            ability.Reason, Kind: ability.Kind!, TargetScope: ability.TargetScope!,
            PowerModifierAmount: ability.PowerModifierAmount.GetValueOrDefault(), SourceRequiredTag: CardObjectTags.UnitCard);
    }

    private static bool IsTargetedPowerModifier(ActivatedAbilitySpec ability)
        => ability.Kind == ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn
            && ability.ExhaustsSourceAsCost == true && ability.RequiredTargetCount == 1
            && ability.TargetScope == CardTargetScopes.AnyUnit
            && ability.PowerModifierAmount is not null and not 0
            && (ability.PowerModifierAmount < 0 && ability.MinimumPowerAfterModifier is > 0
                || ability.PowerModifierAmount > 0 && ability.MinimumPowerAfterModifier.GetValueOrDefault() == 0)
            && ability.ManaCost.GetValueOrDefault() == 0 && ability.PowerCost.GetValueOrDefault() == 0;

    private static P4ActivatedAbilityDefinition TargetedPowerModifierDefinition(BehaviorSpec spec, ActivatedAbilitySpec ability)
    {
        var id = BuildBehaviorSpecAbilityId(spec.CardNo, ability.Kind);
        return new(id, spec.CardNo, id, spec.CardName, 0, 0, 1, false, true, 0, true,
            "Printed targeted exhaust ability with ordinary timing, Ward payment and captured Might reduction.",
            Kind: ActivatedAbilityKinds.UnitPowerModifierUntilEndOfTurn, TargetScope: CardTargetScopes.AnyUnit,
            PowerModifierAmount: ability.PowerModifierAmount!.Value,
            MinimumPowerAfterModifier: ability.MinimumPowerAfterModifier.GetValueOrDefault(),
            SourceRequiredTag: spec.CardCategoryName.Contains("装备", StringComparison.Ordinal)
                ? CardObjectTags.EquipmentCard : CardObjectTags.UnitCard,
            SourceScope: ability.SourceScope ?? string.Empty,
            RequiredControllerCardNoOnField: ability.RequiredControllerCardNoOnField ?? string.Empty);
    }

    private static bool IsDestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRestActivatedAbility(
        ActivatedAbilitySpec ability)
    {
        return string.Equals(
                ability.Kind,
                ActivatedAbilityKinds.DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRest,
                StringComparison.Ordinal)
            && ability.ExhaustsSourceAsCost == true
            && ability.ManaCost.GetValueOrDefault() == 1
            && ability.PowerCost.GetValueOrDefault() == 1
            && string.Equals(ability.PowerCostTrait, RuneTrait.Yellow, StringComparison.Ordinal)
            && ability.RequiredTargetCount.GetValueOrDefault() == 1
            && string.Equals(ability.TargetScope, CardTargetScopes.FriendlyUnit, StringComparison.Ordinal)
            && ability.RequiresBaseEquipmentSource == true
            && ability.MainDeckLookCount.GetValueOrDefault() == 5
            && ability.PlayPowerDelta.GetValueOrDefault() == 1
            && ability.IgnorePlayManaCost == true
            && ability.RecycleUnplayedLookedCards == true
            && string.Equals(ability.PlayCardFilter, CardObjectTags.UnitCard, StringComparison.Ordinal);
    }

    private static P4ActivatedAbilityDefinition DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRestDefinition(
        BehaviorSpec spec,
        ActivatedAbilitySpec ability)
    {
        return new P4ActivatedAbilityDefinition(
            BuildBehaviorSpecAbilityId(spec.CardNo, ability.Kind),
            spec.CardNo,
            ability.Kind ?? ActivatedAbilityKinds.DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRest,
            spec.CardName,
            ability.ManaCost.GetValueOrDefault(),
            0,
            ability.RequiredTargetCount.GetValueOrDefault(),
            RequiresBattlefieldSource: ability.RequiresBattlefieldSource == true,
            ExhaustsSourceAsCost: ability.ExhaustsSourceAsCost == true,
            0,
            AppliesSpellshieldTargetTax: false,
            "BehaviorSpec-driven destroy-friendly-unit, look-top-five, optional free unit play, recycle-rest activated ability representative.",
            PowerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ability.PowerCostTrait ?? string.Empty] = ability.PowerCost.GetValueOrDefault()
            },
            RequiresBaseEquipmentSource: ability.RequiresBaseEquipmentSource == true,
            Kind: ability.Kind ?? string.Empty,
            TargetScope: ability.TargetScope ?? string.Empty,
            MainDeckLookCount: ability.MainDeckLookCount.GetValueOrDefault(),
            PlayPowerDelta: ability.PlayPowerDelta.GetValueOrDefault(),
            IgnorePlayManaCost: ability.IgnorePlayManaCost == true,
            RecycleUnplayedLookedCards: ability.RecycleUnplayedLookedCards == true,
            PlayCardFilter: ability.PlayCardFilter ?? string.Empty);
    }

    private static string BuildBehaviorSpecAbilityId(string cardNo, string? kind)
    {
        var normalizedCardNo = OfficialCardSourceIdentityGroups.NormalizeCardNo(cardNo);
        var prefix = new string(normalizedCardNo
            .Select(character => char.IsLetterOrDigit(character)
                ? char.ToUpperInvariant(character)
                : '_')
            .ToArray());
        return $"{prefix}_{kind}";
    }

    private static bool IsSigilTypedResourceActivatedAbility(ActivatedAbilitySpec ability)
    {
        return string.Equals(ability.Kind, ActivatedAbilityKinds.TypedResourceSkill, StringComparison.Ordinal)
            && ability.ExhaustsSourceAsCost == true
            && ability.ReactionSpeed == true
            && ability.IsResourceSkill == true
            && ability.PaymentOnlyResource == true
            && !string.IsNullOrWhiteSpace(ability.GeneratedPowerTrait)
            && ability.GeneratedPower.GetValueOrDefault() == 1;
    }

    private static P4SigilTypedResourceProfile SigilTypedResourceProfile(
        BehaviorSpec spec,
        ActivatedAbilitySpec ability)
    {
        var trait = ability.GeneratedPowerTrait ?? string.Empty;
        var isOgnReprint = spec.CardNo.StartsWith("OGN·", StringComparison.Ordinal);
        var prefix = SigilResourcePrefix(trait, isOgnReprint);
        var traitUpper = SigilResourceTraitUpper(trait);
        return new P4SigilTypedResourceProfile(
            $"{prefix}_REACTION_EXHAUST_GAIN_1_{traitUpper}_POWER",
            spec.CardNo,
            $"{prefix}_REACTION_TYPED_RESOURCE_GAIN_{traitUpper}",
            spec.CardName,
            trait,
            SigilResourceTraitLabel(trait),
            isOgnReprint);
    }

    private static string SigilResourcePrefix(string trait, bool isOgnReprint)
    {
        var basePrefix = trait switch
        {
            RuneTrait.Red => "RAGE_SIGIL",
            RuneTrait.Green => "FOCUS_SIGIL",
            RuneTrait.Blue => "INSIGHT_SIGIL",
            RuneTrait.Orange => "POWER_SIGIL",
            RuneTrait.Purple => "DISCORD_SIGIL",
            RuneTrait.Yellow => "UNITY_SIGIL",
            _ => throw new InvalidOperationException($"Unsupported typed resource trait '{trait}'.")
        };
        return isOgnReprint ? $"OGN_{basePrefix}" : basePrefix;
    }

    private static string SigilResourceTraitUpper(string trait)
    {
        return trait switch
        {
            RuneTrait.Red => "RED",
            RuneTrait.Green => "GREEN",
            RuneTrait.Blue => "BLUE",
            RuneTrait.Orange => "ORANGE",
            RuneTrait.Purple => "PURPLE",
            RuneTrait.Yellow => "YELLOW",
            _ => throw new InvalidOperationException($"Unsupported typed resource trait '{trait}'.")
        };
    }

    private static string SigilResourceTraitLabel(string trait)
    {
        return trait switch
        {
            RuneTrait.Red => "红色",
            RuneTrait.Green => "绿色",
            RuneTrait.Blue => "蓝色",
            RuneTrait.Orange => "橙色",
            RuneTrait.Purple => "紫色",
            RuneTrait.Yellow => "黄色",
            _ => throw new InvalidOperationException($"Unsupported typed resource trait '{trait}'.")
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildSourceCardNosByRepresentativeCardNo()
    {
        return OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(
            GetAll().Select(definition => definition.SourceCardNo));
    }
}
