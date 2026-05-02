using System.Text.RegularExpressions;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static class CardResourceKeywordNames
{
    public const string Hunt = "狩猎";
    public const string Level = "等级";
    public const string Encourage = "鼓舞";
    public const string Spellshield = "法盾";
}

public static class ResourceKeywordProfileStatuses
{
    public const string RecognizedDeferred = "recognized-deferred";
    public const string NotApplicable = "not-applicable";
}

public sealed record CardResourceKeywordProfile(
    bool HasHunt,
    int HuntAmount,
    bool HasLevel,
    IReadOnlyList<int> LevelThresholds,
    bool HasEncourage,
    bool HasSpellshield,
    int SpellshieldTax,
    string Status,
    string Reason);

public static class CardResourceKeywordRules
{
    private static readonly Regex LevelThresholdRegex = new(
        @"等级(?<amount>[0-9]+)>",
        RegexOptions.Compiled);

    public static CardResourceKeywordProfile BuildProfile(
        BehaviorSpec spec,
        CardBehaviorDefinition? behavior)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var tags = behavior is null ? [] : SourceTags(behavior);
        var huntAmount = KeywordAmount(tags, CardResourceKeywordNames.Hunt);
        if (huntAmount == 0 && HasKeyword(spec, CardResourceKeywordNames.Hunt))
        {
            huntAmount = KeywordAmountFromOfficialText(spec.OfficialText, CardResourceKeywordNames.Hunt);
        }

        var spellshieldTax = KeywordAmount(tags, CardResourceKeywordNames.Spellshield);
        if (spellshieldTax == 0 && HasKeyword(spec, CardResourceKeywordNames.Spellshield))
        {
            spellshieldTax = KeywordAmountFromOfficialText(spec.OfficialText, CardResourceKeywordNames.Spellshield);
        }

        var levelThresholds = LevelThresholdRegex
            .Matches(spec.OfficialText)
            .Select(match => int.Parse(match.Groups["amount"].Value))
            .Distinct()
            .Order()
            .ToArray();
        var hasEncourage = HasKeyword(spec, CardResourceKeywordNames.Encourage);
        var hasAnyResourceKeyword = huntAmount > 0
            || levelThresholds.Length > 0
            || hasEncourage
            || spellshieldTax > 0;

        return new CardResourceKeywordProfile(
            huntAmount > 0,
            huntAmount,
            levelThresholds.Length > 0,
            levelThresholds,
            hasEncourage,
            spellshieldTax > 0,
            spellshieldTax,
            hasAnyResourceKeyword
                ? ResourceKeywordProfileStatuses.RecognizedDeferred
                : ResourceKeywordProfileStatuses.NotApplicable,
            hasAnyResourceKeyword
                ? "P4.7 recognizes resource keyword surfaces from P3 BehaviorSpec and P2 tags; P4.12/P4.14/P4.15/P4.16/P4.17/P4.21/P4.22/P4.23/P4.24/P4.28 cover narrow spellshield target tax, encourage cost-reduction/self-boon/target temporary might/discard-draw/minion creation, and level-threshold unit/draw/tag representatives while broader experience, level, encourage, and spellshield branches remain deferred unless covered by a separate P2 path."
                : "Card does not expose resource keywords through P3 BehaviorSpec or the P2 source-object tag path.");
    }

    public static int SpellshieldTaxFromTags(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return KeywordAmount(tags, CardResourceKeywordNames.Spellshield);
    }

    private static bool HasKeyword(
        BehaviorSpec spec,
        string keyword)
    {
        return spec.Keywords.Any(candidate => string.Equals(candidate.Keyword, keyword, StringComparison.Ordinal));
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

    private static int KeywordAmountFromOfficialText(
        string text,
        string keyword)
    {
        var marker = $"{{{{{keyword}";
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return 0;
        }

        var amountStart = index + marker.Length;
        var amountEnd = text.IndexOf("}}", amountStart, StringComparison.Ordinal);
        if (amountEnd <= amountStart)
        {
            return 1;
        }

        return int.TryParse(text[amountStart..amountEnd], out var amount) && amount > 0
            ? amount
            : 1;
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
