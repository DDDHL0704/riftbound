using Riftbound.Contracts;

namespace Riftbound.Engine;

public static class CardEquipmentKeywordNames
{
    public const string Assemble = "装配";
    public const string Agile = "灵便";
    public const string Tempered = "百炼";
    public const string Weapon = "武装";
}

public static class EquipmentKeywordProfileStatuses
{
    public const string RecognizedDeferred = "recognized-deferred";
    public const string NotApplicable = "not-applicable";
}

public sealed record CardEquipmentKeywordProfile(
    bool HasAssemble,
    bool HasAgile,
    bool HasTempered,
    bool HasWeapon,
    string Status,
    string Reason);

public static class CardEquipmentKeywordRules
{
    public static CardEquipmentKeywordProfile BuildProfile(
        BehaviorSpec spec,
        CardBehaviorDefinition? behavior)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var sourceUnitTags = behavior is null ? [] : ParseDelimitedValues(behavior.SourceUnitTags);
        var sourceEquipmentTags = behavior is null ? [] : ParseDelimitedValues(behavior.SourceEquipmentTags);
        var isEquipmentCard = string.Equals(spec.CardCategoryName, "装备", StringComparison.Ordinal);
        var hasAssemble = isEquipmentCard
            && (HasOwnKeywordLine(spec, CardEquipmentKeywordNames.Assemble)
                || HasKeyword(spec, CardEquipmentKeywordNames.Assemble));
        var hasAgile = HasExactKeyword(sourceEquipmentTags, CardEquipmentKeywordNames.Agile)
            || (isEquipmentCard && HasOwnKeywordLine(spec, CardEquipmentKeywordNames.Agile));
        var hasTempered = HasExactKeyword(sourceUnitTags, CardEquipmentKeywordNames.Tempered)
            || HasOwnKeywordLine(spec, CardEquipmentKeywordNames.Tempered);
        var hasWeapon = HasExactKeyword(sourceEquipmentTags, CardEquipmentKeywordNames.Weapon);
        var hasAnyEquipmentKeyword = hasAssemble
            || hasAgile
            || hasTempered
            || hasWeapon;

        return new CardEquipmentKeywordProfile(
            hasAssemble,
            hasAgile,
            hasTempered,
            hasWeapon,
            hasAnyEquipmentKeyword
                ? EquipmentKeywordProfileStatuses.RecognizedDeferred
                : EquipmentKeywordProfileStatuses.NotApplicable,
            hasAnyEquipmentKeyword
                ? "P4.8 recognizes equipment keyword surfaces from P3 BehaviorSpec and P2 source-object tags; attach/detach, assemble costs, agile reaction attachment, tempered optional attachment, and owner/controller execution remain deferred."
                : "Card does not expose equipment keyword surfaces through P3 BehaviorSpec or the P2 source-object tag path.");
    }

    private static bool HasKeyword(
        BehaviorSpec spec,
        string keyword)
    {
        return spec.Keywords.Any(candidate => string.Equals(candidate.Keyword, keyword, StringComparison.Ordinal));
    }

    private static bool HasOwnKeywordLine(
        BehaviorSpec spec,
        string keyword)
    {
        var marker = $"{{{{{keyword}";
        return spec.OfficialText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => line.StartsWith(marker, StringComparison.Ordinal));
    }

    private static bool HasExactKeyword(
        IReadOnlyList<string> tags,
        string keyword)
    {
        return tags.Any(tag => string.Equals(tag, keyword, StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> ParseDelimitedValues(string value)
    {
        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
    }
}
