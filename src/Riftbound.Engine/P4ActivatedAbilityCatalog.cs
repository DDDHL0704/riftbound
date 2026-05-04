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
            "P4.389 keeps Xerath's verified one-target damage skill and spellshield target tax behind a registry entry.")
    ];

    public static IReadOnlyList<P4ActivatedAbilityDefinition> GetAll()
    {
        return Definitions;
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
