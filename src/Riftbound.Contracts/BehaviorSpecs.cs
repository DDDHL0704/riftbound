namespace Riftbound.Contracts;

public static class BehaviorImplementationStatuses
{
    public const string Implemented = "implemented";
    public const string ManualRuleRequired = "manual-rule-required";
    public const string Unimplemented = "unimplemented";
}

public static class BehaviorTemplateIds
{
    public const string Draw = "draw";
    public const string Damage = "damage";
    public const string Destroy = "destroy";
    public const string Move = "move";
    public const string Recall = "recall";
    public const string Stun = "stun";
    public const string TempMight = "temp_might";
    public const string GainExperience = "gain_experience";
    public const string Assemble = "assemble";
    public const string Echo = "echo";
    public const string Ambush = "ambush";
}

public sealed record BehaviorSpec(
    string CardNo,
    string CardName,
    string CardCategoryName,
    string FunctionalUnitId,
    string Status,
    string Reason,
    string OfficialText,
    ParsedCostSpec Cost,
    IReadOnlyList<KeywordSpec> Keywords,
    IReadOnlyList<TargetSpec> Targets,
    IReadOnlyList<TriggerSpec> Triggers,
    IReadOnlyList<ReplacementSpec> Replacements,
    IReadOnlyList<ActivatedAbilitySpec> ActivatedAbilities,
    IReadOnlyList<StaticAbilitySpec> StaticAbilities,
    IReadOnlyList<EffectPhraseSpec> Effects,
    IReadOnlyList<string> TemplateIds,
    string? ImplementedEffectKind = null,
    string? ImplementedByCardNo = null);

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

public sealed record EffectPhraseSpec(
    string TemplateId,
    string Phrase,
    string Status,
    string Reason);
