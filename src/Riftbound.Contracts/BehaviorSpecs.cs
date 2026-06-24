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

public static class StaticAuraKinds
{
    public const string FriendlyFieldEquipmentCountToSourceUnitPower =
        "FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER";
    public const string BattlefieldAllUnitsPowerPlusOne = "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE";
    public const string BattlefieldFilteredUnitsPower = "BATTLEFIELD_FILTERED_UNITS_POWER";
    public const string SameBattlefieldOtherFriendlyUnitsPowerPlusOne =
        "SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE";
    public const string SameBattlefieldOtherFriendlyFilteredUnitsPower =
        "SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_UNITS_POWER";
    public const string SameBattlefieldFriendlyFilteredUnitCountToSourcePower =
        "SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER";
    public const string OtherFriendlyUnitsPower = "OTHER_FRIENDLY_UNITS_POWER";
    public const string FriendlyFilteredUnitsPower = "FRIENDLY_FILTERED_UNITS_POWER";
}

public static class StaticAuraTargetScopes
{
    public const string SourceObject = "SOURCE_OBJECT";
    public const string SameBattlefieldUnits = "SAME_BATTLEFIELD_UNITS";
    public const string SameBattlefieldFilteredUnits = "SAME_BATTLEFIELD_FILTERED_UNITS";
    public const string SameBattlefieldOtherFriendlyUnits = "SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS";
    public const string SameBattlefieldOtherFriendlyFilteredUnits =
        "SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_UNITS";
    public const string OtherFriendlyUnits = "OTHER_FRIENDLY_UNITS";
    public const string FriendlyFilteredUnits = "FRIENDLY_FILTERED_UNITS";
}

public static class StaticAuraParticipantScopes
{
    public const string FriendlyPublicFieldEquipment = "FRIENDLY_PUBLIC_FIELD_EQUIPMENT";
    public const string SameBattlefieldPublicUnits = "SAME_BATTLEFIELD_PUBLIC_UNITS";
    public const string SameBattlefieldFilteredPublicUnits = "SAME_BATTLEFIELD_FILTERED_PUBLIC_UNITS";
    public const string SameBattlefieldFriendlyFilteredPublicUnits =
        "SAME_BATTLEFIELD_FRIENDLY_FILTERED_PUBLIC_UNITS";
    public const string SameBattlefieldOtherFriendlyPublicUnits = "SAME_BATTLEFIELD_OTHER_FRIENDLY_PUBLIC_UNITS";
    public const string SameBattlefieldOtherFriendlyFilteredPublicUnits =
        "SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_PUBLIC_UNITS";
    public const string OtherFriendlyPublicUnits = "OTHER_FRIENDLY_PUBLIC_UNITS";
    public const string FriendlyFilteredPublicUnits = "FRIENDLY_FILTERED_PUBLIC_UNITS";
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
    string Reason = "");

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
    string Reason);

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
    string? TargetFilter = null);

public sealed record EffectPhraseSpec(
    string TemplateId,
    string Phrase,
    string Status,
    string Reason);
