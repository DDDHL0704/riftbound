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
    IReadOnlyDictionary<string, int>? PowerCostByTrait = null);

public sealed record P4DeferredActivatedAbilitySurface(
    string AbilityId,
    string SourceCardNo,
    string DisplayName,
    string OfficialTextAnchorKey,
    bool RequiresBattlefieldSource,
    bool IsTargetBearing,
    bool EnemySpellshieldTaxRisk,
    string Reason);

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
            })
    ];

    private static readonly P4DeferredActivatedAbilitySurface[] DeferredSurfaces =
    [
        new(
            "DEFERRED_TAP_CREATE_TWO_SPELLSHIELD_WARHAWKS",
            "UNL-160/219",
            "Fluft Poro Warhawk skill",
            "fluft-poro-warhawk-token",
            RequiresBattlefieldSource: true,
            IsTargetBearing: false,
            EnemySpellshieldTaxRisk: false,
            "P4.391 keeps token creation with Spellshield deferred until token and battlefield-only skill execution are complete."),
        new(
            "DEFERRED_EXPERIENCE_EXHAUST_READY_UNIT",
            "UNL-109/219",
            "Crimson Rose ready-unit skill",
            "crimson-rose-ready-unit",
            RequiresBattlefieldSource: true,
            IsTargetBearing: true,
            EnemySpellshieldTaxRisk: true,
            "P4.391 keeps target-bearing activated skills deferred until experience costs and skill target taxes are generalized."),
        new(
            "DEFERRED_SWIFT_PAY_1_A_EXHAUST_STUN_ATTACKER",
            "UNL-194/219",
            "Shadow swift stun skill",
            "shadow-swift-stun-attacker",
            RequiresBattlefieldSource: true,
            IsTargetBearing: true,
            EnemySpellshieldTaxRisk: true,
            "P4.391 keeps combat-window target skills deferred until swift timing, battlefield locality, stun, and skill target taxes are generalized.")
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
            : [definition.SourceCardNo];
    }

    public static IReadOnlyDictionary<string, int> PowerCostByTraitForAbility(P4ActivatedAbilityDefinition definition)
    {
        return PaymentCostRules.NormalizePowerCostByTrait(
            definition.PowerCostByTrait ?? new Dictionary<string, int>(StringComparer.Ordinal));
    }
}
