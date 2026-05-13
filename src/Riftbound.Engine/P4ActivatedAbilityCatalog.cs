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
    string ResourceRestriction = "");

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
    public const string MalzaharPaymentOnlyResourceRestriction = "PAY_RUNE_COSTS_ONLY_REPRESENTATIVE_4D_03I";

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
            "Stage 4D-03I opens only the open-main representative resource skill path; swift, spell-duel, reaction prohibition, and full payment-only lifecycle remain deferred.",
            IsResourceSkill: true,
            PaymentOnlyResource: true,
            GeneratedPower: MalzaharResourceGeneratedPower,
            UsesTargetAsCost: true,
            ResourceRestriction: MalzaharPaymentOnlyResourceRestriction)
    ];

    private static readonly P4DeferredActivatedAbilitySurface[] DeferredSurfaces =
    [
        new(
            "DEFERRED_TAP_REACTION_GAIN_1_MANA",
            "UNL-093/219",
            "Dragon Soul Sage reaction resource skill",
            "dragon-soul-sage-reaction-resource",
            RequiresBattlefieldSource: true,
            IsTargetBearing: false,
            EnemySpellshieldTaxRisk: false,
            "P4.391 keeps reaction-speed resource abilities deferred until the priority/resource reaction model is complete."),
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
            "DEFERRED_PAY_1_BLUE_DRAW_1",
            "SFD·088/221",
            "Renata Glasc draw skill",
            "renata-glasc-draw",
            RequiresBattlefieldSource: true,
            IsTargetBearing: false,
            EnemySpellshieldTaxRisk: false,
            "P4.391 keeps color-specific activated draw abilities deferred until colored resource costs are modeled."),
        new(
            "DEFERRED_PAY_4_BLUE4_EXHAUST_SCORE_1",
            "SFD·088/221",
            "Renata Glasc score skill",
            "renata-glasc-score",
            RequiresBattlefieldSource: true,
            IsTargetBearing: false,
            EnemySpellshieldTaxRisk: false,
            "P4.391 keeps activated scoring abilities deferred until scoring and color-specific costs are modeled."),
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
}
