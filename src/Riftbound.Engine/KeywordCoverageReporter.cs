using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record KeywordCoverageReport(
    int BehaviorSpecs,
    int CardsWithKeywordProfiles,
    IReadOnlyDictionary<string, int> StatusCounts,
    IReadOnlyList<KeywordFamilyCoverage> Families);

public sealed record KeywordFamilyCoverage(
    string Family,
    int CardsWithKeyword,
    IReadOnlyDictionary<string, int> StatusCounts,
    IReadOnlyList<KeywordCoverageRow> DeferredCards);

public sealed record KeywordCoverageRow(
    string CardNo,
    string CardName,
    IReadOnlyList<string> Keywords,
    string Status,
    string Reason);

public static class KeywordCoverageReporter
{
    private const string ImplementedRepresentative = "implemented-representative";
    private const string RecognizedDeferred = "recognized-deferred";
    private const string NotApplicable = "not-applicable";

    public static KeywordCoverageReport Build(IReadOnlyList<BehaviorSpec> specs)
    {
        ArgumentNullException.ThrowIfNull(specs);

        var families = new[]
        {
            BuildFamily("permission", specs, BuildPermissionRow),
            BuildFamily("combat", specs, BuildCombatRow),
            BuildFamily("resource", specs, BuildResourceRow),
            BuildFamily("equipment", specs, BuildEquipmentRow),
            BuildFamily("lifecycle", specs, BuildLifecycleRow),
            BuildFamily("interaction", specs, BuildInteractionRow)
        };

        return new KeywordCoverageReport(
            specs.Count,
            families.Sum(family => family.CardsWithKeyword),
            CountFamilyStatuses(families),
            families);
    }

    private static KeywordFamilyCoverage BuildFamily(
        string family,
        IReadOnlyList<BehaviorSpec> specs,
        Func<BehaviorSpec, KeywordCoverageRow?> buildRow)
    {
        var rows = specs
            .Select(buildRow)
            .Where(row => row is not null)
            .Cast<KeywordCoverageRow>()
            .OrderBy(row => row.CardNo, StringComparer.Ordinal)
            .ToArray();

        return new KeywordFamilyCoverage(
            family,
            rows.Length,
            CountStatuses(rows),
            rows
                .Where(row => string.Equals(row.Status, RecognizedDeferred, StringComparison.Ordinal))
                .ToArray());
    }

    private static KeywordCoverageRow? BuildPermissionRow(BehaviorSpec spec)
    {
        var hasPermissionKeyword = HasAnyKeyword(
            spec,
            CardPermissionKeywordNames.Swift,
            CardPermissionKeywordNames.Reaction,
            CardPermissionKeywordNames.Haste);
        if (!CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out var behavior))
        {
            return hasPermissionKeyword
                ? Row(
                    spec,
                    PermissionKeywords(spec),
                    RecognizedDeferred,
                    "Permission keyword appears in official text, but no executable CardBehaviorDefinition is available.")
                : null;
        }

        var profile = CardPermissionKeywordRules.BuildProfile(behavior);
        if (!profile.HasSwift && !profile.HasReaction && !profile.HasHaste && !hasPermissionKeyword)
        {
            return null;
        }

        var status = profile.HasHaste
            ? profile.HasteOptionalReadyBranchStatus
            : ImplementedRepresentative;
        return Row(
            spec,
            PermissionKeywords(spec),
            status,
            profile.HasHaste
                ? profile.HasteOptionalReadyBranchReason
                : "Swift/reaction permission surfaces are executable through the representative timing-window permission model.");
    }

    private static KeywordCoverageRow? BuildCombatRow(BehaviorSpec spec)
    {
        var hasCombatKeyword = HasAnyKeyword(
            spec,
            CardCombatKeywordNames.Assault,
            CardCombatKeywordNames.Steadfast,
            CardCombatKeywordNames.Bulwark,
            CardCombatKeywordNames.BackRow,
            CardCombatKeywordNames.Roam);
        if (!CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out var behavior))
        {
            return hasCombatKeyword
                ? Row(
                    spec,
                    CombatKeywords(spec),
                    RecognizedDeferred,
                    "Combat keyword appears in official text, but no executable CardBehaviorDefinition is available.")
                : null;
        }

        var profile = CardCombatKeywordRules.BuildProfile(behavior);
        return string.Equals(profile.Status, NotApplicable, StringComparison.Ordinal)
            ? null
            : Row(spec, CombatKeywords(spec), profile.Status, profile.Reason);
    }

    private static KeywordCoverageRow? BuildResourceRow(BehaviorSpec spec)
    {
        var behavior = CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out var definition)
            ? definition
            : null;
        var profile = CardResourceKeywordRules.BuildProfile(spec, behavior);
        return string.Equals(profile.Status, ResourceKeywordProfileStatuses.NotApplicable, StringComparison.Ordinal)
            ? null
            : Row(spec, ResourceKeywords(spec), profile.Status, profile.Reason);
    }

    private static KeywordCoverageRow? BuildEquipmentRow(BehaviorSpec spec)
    {
        var behavior = CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out var definition)
            ? definition
            : null;
        var profile = CardEquipmentKeywordRules.BuildProfile(spec, behavior);
        return string.Equals(profile.Status, EquipmentKeywordProfileStatuses.NotApplicable, StringComparison.Ordinal)
            ? null
            : Row(spec, EquipmentKeywords(spec), profile.Status, profile.Reason);
    }

    private static KeywordCoverageRow? BuildLifecycleRow(BehaviorSpec spec)
    {
        var behavior = CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out var definition)
            ? definition
            : null;
        var profile = CardLifecycleKeywordRules.BuildProfile(spec, behavior);
        return string.Equals(profile.Status, LifecycleKeywordProfileStatuses.NotApplicable, StringComparison.Ordinal)
            ? null
            : Row(spec, LifecycleKeywords(spec), profile.Status, profile.Reason);
    }

    private static KeywordCoverageRow? BuildInteractionRow(BehaviorSpec spec)
    {
        var behavior = CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out var definition)
            ? definition
            : null;
        var profile = CardInteractionKeywordRules.BuildProfile(spec, behavior);
        return string.Equals(profile.Status, InteractionKeywordProfileStatuses.NotApplicable, StringComparison.Ordinal)
            ? null
            : Row(spec, InteractionKeywords(spec), profile.Status, profile.Reason);
    }

    private static KeywordCoverageRow Row(
        BehaviorSpec spec,
        IReadOnlyList<string> keywords,
        string status,
        string reason)
    {
        return new KeywordCoverageRow(
            spec.CardNo,
            spec.CardName,
            keywords,
            status,
            reason);
    }

    private static IReadOnlyDictionary<string, int> CountStatuses(IEnumerable<KeywordCoverageRow> rows)
    {
        return rows
            .GroupBy(row => row.Status, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> CountFamilyStatuses(IEnumerable<KeywordFamilyCoverage> families)
    {
        return families
            .SelectMany(family => family.StatusCounts)
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Value), StringComparer.Ordinal);
    }

    private static bool HasAnyKeyword(BehaviorSpec spec, params string[] keywords)
    {
        return spec.Keywords.Any(candidate =>
            keywords.Any(keyword => string.Equals(candidate.Keyword, keyword, StringComparison.Ordinal)));
    }

    private static IReadOnlyList<string> PermissionKeywords(BehaviorSpec spec)
    {
        return Keywords(
            spec,
            CardPermissionKeywordNames.Swift,
            CardPermissionKeywordNames.Reaction,
            CardPermissionKeywordNames.Haste);
    }

    private static IReadOnlyList<string> CombatKeywords(BehaviorSpec spec)
    {
        return Keywords(
            spec,
            CardCombatKeywordNames.Assault,
            CardCombatKeywordNames.Steadfast,
            CardCombatKeywordNames.Bulwark,
            CardCombatKeywordNames.BackRow,
            CardCombatKeywordNames.Roam);
    }

    private static IReadOnlyList<string> ResourceKeywords(BehaviorSpec spec)
    {
        return Keywords(
            spec,
            CardResourceKeywordNames.Hunt,
            CardResourceKeywordNames.Level,
            CardResourceKeywordNames.Encourage,
            CardResourceKeywordNames.Spellshield);
    }

    private static IReadOnlyList<string> EquipmentKeywords(BehaviorSpec spec)
    {
        return Keywords(
            spec,
            CardEquipmentKeywordNames.Assemble,
            CardEquipmentKeywordNames.Agile,
            CardEquipmentKeywordNames.Tempered,
            CardEquipmentKeywordNames.Weapon);
    }

    private static IReadOnlyList<string> LifecycleKeywords(BehaviorSpec spec)
    {
        return Keywords(
            spec,
            CardLifecycleKeywordNames.Ephemeral,
            CardLifecycleKeywordNames.LastBreath,
            CardLifecycleKeywordNames.Predict);
    }

    private static IReadOnlyList<string> InteractionKeywords(BehaviorSpec spec)
    {
        return Keywords(
            spec,
            CardInteractionKeywordNames.Standby,
            CardInteractionKeywordNames.Echo,
            CardInteractionKeywordNames.Ambush);
    }

    private static IReadOnlyList<string> Keywords(BehaviorSpec spec, params string[] keywords)
    {
        return spec.Keywords
            .Where(candidate => keywords.Any(keyword => string.Equals(candidate.Keyword, keyword, StringComparison.Ordinal)))
            .Select(candidate => candidate.Keyword)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(keyword => keyword, StringComparer.Ordinal)
            .ToArray();
    }
}
