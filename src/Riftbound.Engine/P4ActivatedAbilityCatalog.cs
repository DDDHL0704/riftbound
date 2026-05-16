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
    IReadOnlyDictionary<string, int>? GeneratedPowerByTrait = null);

public sealed record P4DeferredActivatedAbilitySurface(
    string AbilityId,
    string SourceCardNo,
    string DisplayName,
    string OfficialTextAnchorKey,
    bool RequiresBattlefieldSource,
    bool IsTargetBearing,
    bool EnemySpellshieldTaxRisk,
    string Reason);

public sealed record P4SigilTypedResourceProfile(
    string AbilityId,
    string SourceCardNo,
    string EffectKind,
    string DisplayName,
    string Trait,
    string TraitLabel,
    string ResourceRestriction,
    string ResourceIdPrefix,
    bool IsOgnReprint);

public static class P4ActivatedAbilityCatalog
{
    public const string ViCardNo = "UNL-030/219";
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
    public const string MalzaharResourceAbilityId = "MALZAHAR_DESTROY_FRIENDLY_EXHAUST_GAIN_2_PAYMENT_POWER";
    public const string MalzaharResourceAbilityEffectKind = "MALZAHAR_RESOURCE_SKILL_GAIN_2_PAYMENT_ONLY_POWER";
    public const int MalzaharResourceGeneratedPower = 2;
    public const string MalzaharPaymentOnlyResourceRestriction = "PAY_RUNE_COSTS_ONLY_TEMPORARY_LEDGER_4D_03J";

    public const string DragonSoulSageCardNo = "UNL-093/219";
    public const string DragonSoulSageResourceAbilityId = "DRAGON_SOUL_SAGE_REACTION_EXHAUST_GAIN_1_MANA";
    public const string DragonSoulSageResourceAbilityEffectKind = "DRAGON_SOUL_SAGE_REACTION_RESOURCE_SKILL_GAIN_1_MANA";
    public const int DragonSoulSageGeneratedMana = 1;

    public const string JhinCardNo = "UNL-022/219";
    public const string JhinMoveResourceAbilityId = "JHIN_MOVE_TRIGGER_GAIN_1_MANA_1_POWER";
    public const string JhinMoveResourceAbilityEffectKind = "JHIN_MOVEMENT_RESOURCE_SKILL_GAIN_1_MANA_1_POWER";
    public const int JhinMoveResourceGeneratedMana = 1;
    public const int JhinMoveResourceGeneratedPower = 1;
    public const string JhinMoveResourceRestriction = "PAY_RUNE_COSTS_ONLY_JHIN_MOVE_TEMPORARY_LEDGER_4D_03CO";
    public const string JhinMoveTriggerOptionalCostPrefix = "JHIN_MOVE_TRIGGER:";

    public const string HoneyfruitCardNo = "UNL-049/219";
    public const string HoneyfruitResourceAbilityId = "HONEYFRUIT_REACTION_EXHAUST_GAIN_GENERIC_POWER";
    public const string HoneyfruitResourceAbilityEffectKind = "HONEYFRUIT_REACTION_RESOURCE_SKILL_GAIN_GENERIC_POWER";
    public const int HoneyfruitGeneratedPower = 1;
    public const int HoneyfruitUpgradedGeneratedMana = 1;
    public const int HoneyfruitLevelSixExperience = 6;
    public const string HoneyfruitLevelSixOptionalCostPrefix = "HONEYFRUIT_LEVEL_SIX:";
    public const string HoneyfruitPaymentOnlyResourceRestriction = "PAY_RUNE_COSTS_ONLY_HONEYFRUIT_TEMPORARY_LEDGER_4D_03CP";

    public const string BlueSentinelCardNo = "UNL-087/219";
    public const string BlueSentinelResourceAbilityId = "BLUE_SENTINEL_HELD_DELAYED_NEXT_MAIN_GAIN_GENERIC_POWER";
    public const string BlueSentinelResourceAbilityEffectKind = "BLUE_SENTINEL_HELD_DELAYED_NEXT_MAIN_RESOURCE_SKILL_GAIN_GENERIC_POWER";
    public const int BlueSentinelGeneratedPower = 1;
    public const string BlueSentinelPaymentOnlyResourceRestriction = "PAY_RUNE_COSTS_ONLY_BLUE_SENTINEL_DELAYED_TEMPORARY_LEDGER_4D_03CQ";
    public const string BlueSentinelDelayedResourceActionPrefix = "BLUE_SENTINEL_DELAYED_RESOURCE:";

    public const string LuxCardNo = "OGS·014/024";
    public const string LuxResourceAbilityId = "LUX_REACTION_EXHAUST_GAIN_2_SPELL_ONLY_MANA";
    public const string LuxResourceAbilityEffectKind = "LUX_REACTION_RESOURCE_SKILL_GAIN_2_SPELL_ONLY_MANA";
    public const int LuxGeneratedMana = 2;
    public const string LuxSpellOnlyResourceRestriction = "PLAY_SPELLS_ONLY_LUX_TEMPORARY_MANA_4D_03CR";
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

    public static bool CardCannotBecomeActive(string? cardNo)
    {
        return string.Equals(cardNo, GatekeeperMaduliCardNo, StringComparison.Ordinal);
    }

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
    public const string WarhawkTokenCardNo = "UNL·T02";
    public const int FluftPoroWarhawkTokenCount = 2;

    public const string ShadowCardNo = "UNL-194/219";
    public const string ShadowStunAbilityId = "SHADOW_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER";
    public const string ShadowStunAbilityEffectKind = "SHADOW_ACTIVATED_STUN_ATTACKER";
    public const int ShadowStunManaCost = 1;
    public const int ShadowStunPowerCost = 1;

    public const string RageSigilCardNo = "SFD·222/221";
    public const string RageSigilResourceAbilityId = "RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER";
    public const string RageSigilResourceAbilityEffectKind = "RAGE_SIGIL_REACTION_TYPED_RESOURCE_GAIN_RED";
    public const string RageSigilTypedResourceRestriction = "PAY_RUNE_COSTS_ONLY_TYPED_RED_TEMPORARY_LEDGER_4D_03R";
    public const int RageSigilGeneratedRedPower = 1;

    public const string FocusSigilCardNo = "SFD·226/221";
    public const string FocusSigilResourceAbilityId = "FOCUS_SIGIL_REACTION_EXHAUST_GAIN_1_GREEN_POWER";
    public const string FocusSigilResourceAbilityEffectKind = "FOCUS_SIGIL_REACTION_TYPED_RESOURCE_GAIN_GREEN";
    public const string FocusSigilTypedResourceRestriction = "PAY_RUNE_COSTS_ONLY_TYPED_GREEN_TEMPORARY_LEDGER_4D_03S";

    public const string InsightSigilCardNo = "SFD·229/221";
    public const string InsightSigilResourceAbilityId = "INSIGHT_SIGIL_REACTION_EXHAUST_GAIN_1_BLUE_POWER";
    public const string InsightSigilResourceAbilityEffectKind = "INSIGHT_SIGIL_REACTION_TYPED_RESOURCE_GAIN_BLUE";
    public const string InsightSigilTypedResourceRestriction = "PAY_RUNE_COSTS_ONLY_TYPED_BLUE_TEMPORARY_LEDGER_4D_03S";

    public const string PowerSigilCardNo = "SFD·231/221";
    public const string PowerSigilResourceAbilityId = "POWER_SIGIL_REACTION_EXHAUST_GAIN_1_ORANGE_POWER";
    public const string PowerSigilResourceAbilityEffectKind = "POWER_SIGIL_REACTION_TYPED_RESOURCE_GAIN_ORANGE";
    public const string PowerSigilTypedResourceRestriction = "PAY_RUNE_COSTS_ONLY_TYPED_ORANGE_TEMPORARY_LEDGER_4D_03S";

    public const string DiscordSigilCardNo = "SFD·234/221";
    public const string DiscordSigilResourceAbilityId = "DISCORD_SIGIL_REACTION_EXHAUST_GAIN_1_PURPLE_POWER";
    public const string DiscordSigilResourceAbilityEffectKind = "DISCORD_SIGIL_REACTION_TYPED_RESOURCE_GAIN_PURPLE";
    public const string DiscordSigilTypedResourceRestriction = "PAY_RUNE_COSTS_ONLY_TYPED_PURPLE_TEMPORARY_LEDGER_4D_03S";

    public const string UnitySigilCardNo = "SFD·238/221";
    public const string UnitySigilResourceAbilityId = "UNITY_SIGIL_REACTION_EXHAUST_GAIN_1_YELLOW_POWER";
    public const string UnitySigilResourceAbilityEffectKind = "UNITY_SIGIL_REACTION_TYPED_RESOURCE_GAIN_YELLOW";
    public const string UnitySigilTypedResourceRestriction = "PAY_RUNE_COSTS_ONLY_TYPED_YELLOW_TEMPORARY_LEDGER_4D_03S";

    public const string OgnRageSigilCardNo = "OGN·040/298";
    public const string OgnRageSigilResourceAbilityId = "OGN_RAGE_SIGIL_REACTION_EXHAUST_GAIN_1_RED_POWER";
    public const string OgnRageSigilResourceAbilityEffectKind = "OGN_RAGE_SIGIL_REACTION_TYPED_RESOURCE_GAIN_RED";
    public const string OgnRageSigilTypedResourceRestriction = "PAY_RUNE_COSTS_ONLY_TYPED_RED_TEMPORARY_LEDGER_4D_03T";

    public const string OgnFocusSigilCardNo = "OGN·081/298";
    public const string OgnFocusSigilResourceAbilityId = "OGN_FOCUS_SIGIL_REACTION_EXHAUST_GAIN_1_GREEN_POWER";
    public const string OgnFocusSigilResourceAbilityEffectKind = "OGN_FOCUS_SIGIL_REACTION_TYPED_RESOURCE_GAIN_GREEN";
    public const string OgnFocusSigilTypedResourceRestriction = "PAY_RUNE_COSTS_ONLY_TYPED_GREEN_TEMPORARY_LEDGER_4D_03T";

    public const string OgnInsightSigilCardNo = "OGN·120/298";
    public const string OgnInsightSigilResourceAbilityId = "OGN_INSIGHT_SIGIL_REACTION_EXHAUST_GAIN_1_BLUE_POWER";
    public const string OgnInsightSigilResourceAbilityEffectKind = "OGN_INSIGHT_SIGIL_REACTION_TYPED_RESOURCE_GAIN_BLUE";
    public const string OgnInsightSigilTypedResourceRestriction = "PAY_RUNE_COSTS_ONLY_TYPED_BLUE_TEMPORARY_LEDGER_4D_03T";

    public const string OgnPowerSigilCardNo = "OGN·163/298";
    public const string OgnPowerSigilResourceAbilityId = "OGN_POWER_SIGIL_REACTION_EXHAUST_GAIN_1_ORANGE_POWER";
    public const string OgnPowerSigilResourceAbilityEffectKind = "OGN_POWER_SIGIL_REACTION_TYPED_RESOURCE_GAIN_ORANGE";
    public const string OgnPowerSigilTypedResourceRestriction = "PAY_RUNE_COSTS_ONLY_TYPED_ORANGE_TEMPORARY_LEDGER_4D_03T";

    public const string OgnDiscordSigilCardNo = "OGN·204/298";
    public const string OgnDiscordSigilResourceAbilityId = "OGN_DISCORD_SIGIL_REACTION_EXHAUST_GAIN_1_PURPLE_POWER";
    public const string OgnDiscordSigilResourceAbilityEffectKind = "OGN_DISCORD_SIGIL_REACTION_TYPED_RESOURCE_GAIN_PURPLE";
    public const string OgnDiscordSigilTypedResourceRestriction = "PAY_RUNE_COSTS_ONLY_TYPED_PURPLE_TEMPORARY_LEDGER_4D_03T";

    public const string OgnUnitySigilCardNo = "OGN·245/298";
    public const string OgnUnitySigilResourceAbilityId = "OGN_UNITY_SIGIL_REACTION_EXHAUST_GAIN_1_YELLOW_POWER";
    public const string OgnUnitySigilResourceAbilityEffectKind = "OGN_UNITY_SIGIL_REACTION_TYPED_RESOURCE_GAIN_YELLOW";
    public const string OgnUnitySigilTypedResourceRestriction = "PAY_RUNE_COSTS_ONLY_TYPED_YELLOW_TEMPORARY_LEDGER_4D_03T";

    public const string EnergyChannelCardNo = "OGN·098/298";
    public const string EnergyChannelResourceAbilityId = "ENERGY_CHANNEL_REACTION_EXHAUST_GAIN_1_MANA";
    public const string EnergyChannelResourceAbilityEffectKind = "ENERGY_CHANNEL_REACTION_RESOURCE_SKILL_GAIN_1_MANA";
    public const int EnergyChannelGeneratedMana = 1;

    public const string AncientSteleCardNo = "SFD·117/221";
    public const string AncientSteleResourceAbilityId = "ANCIENT_STELE_REACTION_PAY_MANA_GAIN_GENERIC_POWER";
    public const string AncientSteleResourceAbilityEffectKind = "ANCIENT_STELE_REACTION_RESOURCE_CONVERT_MANA_TO_GENERIC_POWER";
    public const string AncientSteleConversionOptionalCostPrefix = "CONVERT_MANA_TO_GENERIC_POWER:";
    public const string AncientStelePaymentOnlyResourceRestriction = "PAY_RUNE_COSTS_ONLY_GENERIC_TEMPORARY_LEDGER_4D_03U";

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
    public const string GoldTokenPaymentOnlyResourceRestriction = "PAY_RUNE_COSTS_ONLY_GOLD_TEMPORARY_LEDGER_4D_03V";

    private static readonly P4SigilTypedResourceProfile[] SigilTypedResourceProfiles =
    [
        new(
            RageSigilResourceAbilityId,
            RageSigilCardNo,
            RageSigilResourceAbilityEffectKind,
            "暴怒之印",
            RuneTrait.Red,
            "红色",
            RageSigilTypedResourceRestriction,
            "RAGE_SIGIL",
            IsOgnReprint: false),
        new(
            FocusSigilResourceAbilityId,
            FocusSigilCardNo,
            FocusSigilResourceAbilityEffectKind,
            "专注之印",
            RuneTrait.Green,
            "绿色",
            FocusSigilTypedResourceRestriction,
            "FOCUS_SIGIL",
            IsOgnReprint: false),
        new(
            InsightSigilResourceAbilityId,
            InsightSigilCardNo,
            InsightSigilResourceAbilityEffectKind,
            "洞察之印",
            RuneTrait.Blue,
            "蓝色",
            InsightSigilTypedResourceRestriction,
            "INSIGHT_SIGIL",
            IsOgnReprint: false),
        new(
            PowerSigilResourceAbilityId,
            PowerSigilCardNo,
            PowerSigilResourceAbilityEffectKind,
            "力量之印",
            RuneTrait.Orange,
            "橙色",
            PowerSigilTypedResourceRestriction,
            "POWER_SIGIL",
            IsOgnReprint: false),
        new(
            DiscordSigilResourceAbilityId,
            DiscordSigilCardNo,
            DiscordSigilResourceAbilityEffectKind,
            "不和之印",
            RuneTrait.Purple,
            "紫色",
            DiscordSigilTypedResourceRestriction,
            "DISCORD_SIGIL",
            IsOgnReprint: false),
        new(
            UnitySigilResourceAbilityId,
            UnitySigilCardNo,
            UnitySigilResourceAbilityEffectKind,
            "团结之印",
            RuneTrait.Yellow,
            "黄色",
            UnitySigilTypedResourceRestriction,
            "UNITY_SIGIL",
            IsOgnReprint: false),
        new(
            OgnRageSigilResourceAbilityId,
            OgnRageSigilCardNo,
            OgnRageSigilResourceAbilityEffectKind,
            "暴怒之印",
            RuneTrait.Red,
            "红色",
            OgnRageSigilTypedResourceRestriction,
            "OGN_RAGE_SIGIL",
            IsOgnReprint: true),
        new(
            OgnFocusSigilResourceAbilityId,
            OgnFocusSigilCardNo,
            OgnFocusSigilResourceAbilityEffectKind,
            "专注之印",
            RuneTrait.Green,
            "绿色",
            OgnFocusSigilTypedResourceRestriction,
            "OGN_FOCUS_SIGIL",
            IsOgnReprint: true),
        new(
            OgnInsightSigilResourceAbilityId,
            OgnInsightSigilCardNo,
            OgnInsightSigilResourceAbilityEffectKind,
            "洞察之印",
            RuneTrait.Blue,
            "蓝色",
            OgnInsightSigilTypedResourceRestriction,
            "OGN_INSIGHT_SIGIL",
            IsOgnReprint: true),
        new(
            OgnPowerSigilResourceAbilityId,
            OgnPowerSigilCardNo,
            OgnPowerSigilResourceAbilityEffectKind,
            "力量之印",
            RuneTrait.Orange,
            "橙色",
            OgnPowerSigilTypedResourceRestriction,
            "OGN_POWER_SIGIL",
            IsOgnReprint: true),
        new(
            OgnDiscordSigilResourceAbilityId,
            OgnDiscordSigilCardNo,
            OgnDiscordSigilResourceAbilityEffectKind,
            "不和之印",
            RuneTrait.Purple,
            "紫色",
            OgnDiscordSigilTypedResourceRestriction,
            "OGN_DISCORD_SIGIL",
            IsOgnReprint: true),
        new(
            OgnUnitySigilResourceAbilityId,
            OgnUnitySigilCardNo,
            OgnUnitySigilResourceAbilityEffectKind,
            "团结之印",
            RuneTrait.Yellow,
            "黄色",
            OgnUnitySigilTypedResourceRestriction,
            "OGN_UNITY_SIGIL",
            IsOgnReprint: true)
    ];

    private static readonly P4ActivatedAbilityDefinition[] Definitions =
    [
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
            "Stage 4D-03J opens the open-main and spell-duel focus representative resource skill path with an auditable temporary payment-only ledger; the broader resource skill family remains deferred.",
            IsResourceSkill: true,
            PaymentOnlyResource: true,
            GeneratedPower: MalzaharResourceGeneratedPower,
            UsesTargetAsCost: true,
            ResourceRestriction: MalzaharPaymentOnlyResourceRestriction),
        new(
            DragonSoulSageResourceAbilityId,
            DragonSoulSageCardNo,
            DragonSoulSageResourceAbilityEffectKind,
            "Dragon Soul Sage reaction resource skill",
            0,
            0,
            0,
            RequiresBattlefieldSource: true,
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
            "Stage 4D-03CO opens only Jhin's movement-triggered non-legend resource-skill lane with server-captured movement context and payment-only generated power.",
            IsResourceSkill: true,
            PaymentOnlyResource: true,
            GeneratedPower: JhinMoveResourceGeneratedPower,
            ResourceRestriction: JhinMoveResourceRestriction,
            GeneratedMana: JhinMoveResourceGeneratedMana),
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
            "Stage 4D-03CP opens Honeyfruit's base-equipment reaction-speed payment-only resource skill plus its level-six upgraded mana branch.",
            IsResourceSkill: true,
            PaymentOnlyResource: true,
            GeneratedPower: HoneyfruitGeneratedPower,
            ResourceRestriction: HoneyfruitPaymentOnlyResourceRestriction,
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
            "Stage 4D-03CR opens Lux's base-or-battlefield spell-only reaction payment resource representative with inline play-card cleanup.",
            IsResourceSkill: true,
            PaymentOnlyResource: true,
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
            "Stage 4D-03AS opens Azir's pay green swift controlled-unit position-swap representative plus optional target armament reattach; the broader swift target-bearing family remains deferred.",
            PowerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Green] = AzirSwiftSwapGreenPowerCost
            }),
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
            "Stage 4D-03AO opens only Ezreal's pay blue swift self move-to-base representative; attack/defense damage trigger, cannot-combat-damage static, and full swift timing remain deferred.",
            PowerCostByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [RuneTrait.Blue] = EzrealBlueSwiftMoveBluePowerCost
            }),
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
            "Stage 4D-03Q opens only Shadow's swift battle-response stun-attacker representative; the broader swift combat response and target-bearing skill family remains deferred.",
            ReactionSpeed: true),
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
            "Stage 4D-03U opens only Ancient Stele's base-equipment reaction-speed mana-to-generic-temporary-power conversion representative.",
            IsResourceSkill: true,
            PaymentOnlyResource: true,
            ResourceRestriction: AncientStelePaymentOnlyResourceRestriction,
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
            "Stage 4D-03V opens the UNL Gold token's base-equipment reaction-speed destroy self resource representative; Stage 4D-03W applies the Renata marker bonus while non-Gold token surfaces remain deferred.",
            IsResourceSkill: true,
            PaymentOnlyResource: true,
            GeneratedPower: GoldTokenGeneratedPower,
            ResourceRestriction: GoldTokenPaymentOnlyResourceRestriction,
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
            "Stage 4D-03V opens the SFD Gold token's base-equipment reaction-speed destroy self resource representative; Stage 4D-03W applies the Renata marker bonus while non-Gold token surfaces remain deferred.",
            IsResourceSkill: true,
            PaymentOnlyResource: true,
            GeneratedPower: GoldTokenGeneratedPower,
            ResourceRestriction: GoldTokenPaymentOnlyResourceRestriction,
            ReactionSpeed: true,
            RequiresBaseEquipmentSource: true),
        .. SigilTypedResourceProfiles.Select(SigilTypedResourceDefinition)
    ];

    private static readonly P4DeferredActivatedAbilitySurface[] DeferredSurfaces =
    [
    ];

    public static IReadOnlyList<P4ActivatedAbilityDefinition> GetAll()
    {
        return Definitions;
    }

    public static IReadOnlyList<P4DeferredActivatedAbilitySurface> GetDeferredSurfaces()
    {
        return DeferredSurfaces;
    }

    public static bool TryGetByAbilityId(
        string abilityId,
        out P4ActivatedAbilityDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate => string.Equals(
            candidate.AbilityId,
            abilityId,
            StringComparison.Ordinal))!;
        return definition is not null;
    }

    public static bool TryGetByEffectKind(
        string effectKind,
        out P4ActivatedAbilityDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate => string.Equals(
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

    public static IReadOnlyList<string> SourceCardNosForAbility(P4ActivatedAbilityDefinition definition)
    {
        return string.Equals(definition.AbilityId, RenataGlascDrawAbilityId, StringComparison.Ordinal)
            || string.Equals(definition.AbilityId, RenataGlascScoreAbilityId, StringComparison.Ordinal)
                ? [RenataGlascCardNo, RenataGlascAltCardNo]
            : string.Equals(definition.AbilityId, AzirSwiftSwapAbilityId, StringComparison.Ordinal)
                ? [AzirCardNo, AzirAltCardNo]
            : string.Equals(definition.AbilityId, EzrealBlueSwiftMoveAbilityId, StringComparison.Ordinal)
                ? [EzrealBlueSwiftCardNo, EzrealBlueSwiftAltCardNo, EzrealBlueSwiftPromoCardNo]
                : [definition.SourceCardNo];
    }

    public static string AzirSwiftSwapUsedThisTurnEffectId(string playerId, string sourceObjectId)
    {
        return $"{AzirSwiftSwapUsedThisTurnEffectPrefix}{playerId}:{sourceObjectId}";
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
        return SigilTypedResourceProfiles.Any(profile => string.Equals(profile.AbilityId, abilityId, StringComparison.Ordinal));
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
        profile = SigilTypedResourceProfiles.FirstOrDefault(candidate =>
            string.Equals(candidate.AbilityId, abilityId, StringComparison.Ordinal))!;
        return profile is not null;
    }

    public static bool IsSfdSigilTypedResourceAbility(string? abilityId)
    {
        return SigilTypedResourceProfiles.Any(profile =>
            !profile.IsOgnReprint
            && string.Equals(profile.AbilityId, abilityId, StringComparison.Ordinal));
    }

    public static bool TryGetSfdSigilTypedResourceProfile(
        string? abilityId,
        out P4SigilTypedResourceProfile profile)
    {
        profile = SigilTypedResourceProfiles.FirstOrDefault(candidate =>
            !candidate.IsOgnReprint
            && string.Equals(candidate.AbilityId, abilityId, StringComparison.Ordinal))!;
        return profile is not null;
    }

    public static IReadOnlyList<P4SigilTypedResourceProfile> GetSigilTypedResourceProfiles()
    {
        return SigilTypedResourceProfiles;
    }

    public static IReadOnlyList<P4SigilTypedResourceProfile> GetSfdSigilTypedResourceProfiles()
    {
        return SigilTypedResourceProfiles
            .Where(profile => !profile.IsOgnReprint)
            .ToArray();
    }

    public static IReadOnlyList<P4SigilTypedResourceProfile> GetOgnSigilTypedResourceProfiles()
    {
        return SigilTypedResourceProfiles
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
            PaymentOnlyResource: true,
            ResourceRestriction: profile.ResourceRestriction,
            ReactionSpeed: true,
            RequiresBaseEquipmentSource: true,
            GeneratedPowerByTrait: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [profile.Trait] = 1
            });
    }
}
