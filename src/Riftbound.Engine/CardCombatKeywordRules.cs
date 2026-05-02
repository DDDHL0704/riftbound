namespace Riftbound.Engine;

public static class CardCombatKeywordNames
{
    public const string Assault = "强攻";
    public const string Steadfast = "坚守";
    public const string Bulwark = "壁垒";
    public const string BackRow = "后排";
    public const string Roam = "游走";
}

public static class CombatKeywordProfileStatuses
{
    public const string RecognizedDeferred = "recognized-deferred";
    public const string NotApplicable = "not-applicable";
}

public sealed record CardCombatKeywordProfile(
    bool HasAssault,
    int AssaultAmount,
    bool HasSteadfast,
    int SteadfastAmount,
    bool HasBulwark,
    bool HasBackRow,
    bool HasRoam,
    string Status,
    string Reason);

public static class CardCombatKeywordRules
{
    public static CardCombatKeywordProfile BuildProfile(CardBehaviorDefinition behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        var tags = SourceTags(behavior);
        var assaultAmount = KeywordAmount(tags, CardCombatKeywordNames.Assault);
        var steadfastAmount = KeywordAmount(tags, CardCombatKeywordNames.Steadfast);
        var hasBulwark = HasExactKeyword(tags, CardCombatKeywordNames.Bulwark);
        var hasBackRow = HasExactKeyword(tags, CardCombatKeywordNames.BackRow);
        var hasRoam = HasExactKeyword(tags, CardCombatKeywordNames.Roam);
        var hasAnyCombatKeyword = assaultAmount > 0
            || steadfastAmount > 0
            || hasBulwark
            || hasBackRow
            || hasRoam;

        return new CardCombatKeywordProfile(
            assaultAmount > 0,
            assaultAmount,
            steadfastAmount > 0,
            steadfastAmount,
            hasBulwark,
            hasBackRow,
            hasRoam,
            hasAnyCombatKeyword
                ? CombatKeywordProfileStatuses.RecognizedDeferred
                : CombatKeywordProfileStatuses.NotApplicable,
            hasAnyCombatKeyword
                ? "P4.6 recognizes combat keyword tags and values from the P2 source-object path; combat damage, assignment order, and roam movement execution remain deferred."
                : "Card does not expose combat keywords through the P2 source-object tag path.");
    }

    private static int KeywordAmount(
        IReadOnlyList<string> tags,
        string keyword)
    {
        var exactMatchFound = false;
        foreach (var tag in tags)
        {
            if (string.Equals(tag, keyword, StringComparison.Ordinal))
            {
                exactMatchFound = true;
                continue;
            }

            if (tag.StartsWith(keyword, StringComparison.Ordinal)
                && int.TryParse(tag[keyword.Length..], out var amount)
                && amount > 0)
            {
                return amount;
            }
        }

        return exactMatchFound ? 1 : 0;
    }

    private static bool HasExactKeyword(
        IReadOnlyList<string> tags,
        string keyword)
    {
        return tags.Any(tag => string.Equals(tag, keyword, StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> SourceTags(CardBehaviorDefinition behavior)
    {
        return ParseDelimitedValues(behavior.SourceUnitTags)
            .Concat(ParseDelimitedValues(behavior.SourceEquipmentTags))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ParseDelimitedValues(string value)
    {
        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
    }
}
