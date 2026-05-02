using Riftbound.Contracts;

namespace Riftbound.Engine;

public static class CardLifecycleKeywordNames
{
    public const string Ephemeral = "瞬息";
    public const string LastBreath = "绝念";
    public const string Predict = "预知";
}

public static class LifecycleKeywordProfileStatuses
{
    public const string Implemented = "implemented";
    public const string RecognizedDelegated = "recognized-delegated";
    public const string RecognizedDeferred = "recognized-deferred";
    public const string NotApplicable = "not-applicable";
}

public sealed record CardLifecycleKeywordProfile(
    bool HasEphemeral,
    bool HasLastBreath,
    bool HasPredict,
    bool HasPredictRecyclePath,
    string Status,
    string Reason);

public static class CardLifecycleKeywordRules
{
    public static CardLifecycleKeywordProfile BuildProfile(
        BehaviorSpec spec,
        CardBehaviorDefinition? behavior)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var tags = behavior is null ? [] : SourceTags(behavior);
        var hasEphemeral = HasKeyword(spec, CardLifecycleKeywordNames.Ephemeral)
            || HasExactKeyword(tags, CardLifecycleKeywordNames.Ephemeral);
        var hasLastBreath = HasKeyword(spec, CardLifecycleKeywordNames.LastBreath);
        var hasPredict = HasKeyword(spec, CardLifecycleKeywordNames.Predict)
            || HasExactKeyword(tags, CardLifecycleKeywordNames.Predict);
        var hasPredictRecyclePath = behavior is not null
            && behavior.MainDeckLookCount > 0
            && behavior.RecyclesSelectedMainDeckTargets;
        var hasAnyLifecycleKeyword = hasEphemeral
            || hasLastBreath
            || hasPredict;

        var status = ResolveStatus(hasEphemeral, hasLastBreath, hasPredict, hasPredictRecyclePath);
        return new CardLifecycleKeywordProfile(
            hasEphemeral,
            hasLastBreath,
            hasPredict,
            hasPredictRecyclePath,
            status,
            status switch
            {
                LifecycleKeywordProfileStatuses.Implemented =>
                    "P4.3 implements Ephemeral turn-start cleanup for controlled base/battlefield objects; no additional lifecycle keyword is present on this profile.",
                LifecycleKeywordProfileStatuses.RecognizedDelegated =>
                    "P4.9 recognizes Predict and delegates the audited top-card recycle/no-recycle path to existing P2 behavior; broader static grants remain deferred.",
                LifecycleKeywordProfileStatuses.RecognizedDeferred =>
                    "P4.9 recognizes lifecycle keyword surfaces, but Last Breath trigger queues, broad Predict grants, and non-audited lifecycle branches remain deferred.",
                _ =>
                    hasAnyLifecycleKeyword
                        ? "Lifecycle keyword surface is recognized but has no P4 execution status."
                        : "Card does not expose lifecycle keywords through P3 BehaviorSpec or the P2 source-object tag path."
            });
    }

    private static string ResolveStatus(
        bool hasEphemeral,
        bool hasLastBreath,
        bool hasPredict,
        bool hasPredictRecyclePath)
    {
        if (!hasEphemeral && !hasLastBreath && !hasPredict)
        {
            return LifecycleKeywordProfileStatuses.NotApplicable;
        }

        if (hasLastBreath)
        {
            return LifecycleKeywordProfileStatuses.RecognizedDeferred;
        }

        if (hasPredict)
        {
            return hasPredictRecyclePath
                ? LifecycleKeywordProfileStatuses.RecognizedDelegated
                : LifecycleKeywordProfileStatuses.RecognizedDeferred;
        }

        return LifecycleKeywordProfileStatuses.Implemented;
    }

    private static bool HasKeyword(
        BehaviorSpec spec,
        string keyword)
    {
        return spec.Keywords.Any(candidate => string.Equals(candidate.Keyword, keyword, StringComparison.Ordinal));
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
