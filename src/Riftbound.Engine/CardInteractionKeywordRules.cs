using Riftbound.Contracts;

namespace Riftbound.Engine;

public static class CardInteractionKeywordNames
{
    public const string Standby = "待命";
    public const string Echo = "回响";
    public const string Ambush = "伏击";
}

public static class EchoOptionalCostNames
{
    public const string Echo = "ECHO";
}

public static class EchoKeywordProfileStatuses
{
    public const string Implemented = "implemented";
    public const string NotApplicable = "not-applicable";
}

public sealed record CardEchoKeywordProfile(
    bool HasEcho,
    int EchoManaCost,
    string Status,
    string Reason);

public static class InteractionKeywordProfileStatuses
{
    public const string Implemented = "implemented";
    public const string RecognizedDeferred = "recognized-deferred";
    public const string NotApplicable = "not-applicable";
}

public sealed record CardInteractionKeywordProfile(
    bool HasStandby,
    bool HasEcho,
    int EchoManaCost,
    bool HasAmbush,
    string Status,
    string Reason);

public static class CardInteractionKeywordRules
{
    public static CardInteractionKeywordProfile BuildProfile(
        BehaviorSpec spec,
        CardBehaviorDefinition? behavior)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var tags = behavior is null ? [] : SourceTags(behavior);
        var hasStandby = HasKeyword(spec, CardInteractionKeywordNames.Standby)
            || HasExactKeyword(tags, CardInteractionKeywordNames.Standby);
        var hasEchoKeyword = HasKeyword(spec, CardInteractionKeywordNames.Echo);
        var echoCost = behavior is null ? null : EchoCostRules.PrintedFor(behavior.CardNo);
        var echoManaCost = echoCost?.Mana ?? 0;
        var hasEcho = hasEchoKeyword || echoCost is not null;
        var hasAmbush = HasKeyword(spec, CardInteractionKeywordNames.Ambush);
        var hasAnyInteractionKeyword = hasStandby
            || hasEcho
            || hasAmbush;
        var status = ResolveProfileStatus(hasStandby, hasEcho, echoManaCost, hasAmbush);

        return new CardInteractionKeywordProfile(
            hasStandby,
            hasEcho,
            echoManaCost,
            hasAmbush,
            status,
            status switch
            {
                InteractionKeywordProfileStatuses.Implemented =>
                    "Resource Echo costs are available; independent repeat targets and modes require separate verification.",
                InteractionKeywordProfileStatuses.RecognizedDeferred =>
                    "P4.9 recognizes interaction keyword surfaces; P4.70/P4.71/P4.76/P4.386 cover narrow Standby hide/reveal/reaction and one reaction resolution trigger, the B0 battlefield-source Teemo slice covers one Standby target-damage representative, and P4.387 covers one Ambush battlefield reaction play representative, while base-context/broader Standby target damage, broader Ambush targets/cards, and complex Echo costs remain deferred unless a separate P2 path covers the ordinary play effect.",
                _ =>
                    hasAnyInteractionKeyword
                        ? "Interaction keyword surface is recognized but has no P4 execution status."
                        : "Card does not expose interaction keywords through P3 BehaviorSpec or the P2 source-object tag path."
            });
    }

    public static CardEchoKeywordProfile BuildEchoProfile(CardBehaviorDefinition behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        var cost = EchoCostRules.PrintedFor(behavior.CardNo);
        return new CardEchoKeywordProfile(cost is not null, cost?.Mana ?? 0,
            cost is null ? EchoKeywordProfileStatuses.NotApplicable : EchoKeywordProfileStatuses.Implemented,
            cost is null ? "No resource-only printed Echo cost." : "Resource Echo cost recognized; repeat choices are audited separately.");
    }

    private static string ResolveProfileStatus(
        bool hasStandby,
        bool hasEcho,
        int echoManaCost,
        bool hasAmbush)
    {
        if (!hasStandby && !hasEcho && !hasAmbush)
        {
            return InteractionKeywordProfileStatuses.NotApplicable;
        }

        if (hasStandby || hasAmbush)
        {
            return InteractionKeywordProfileStatuses.RecognizedDeferred;
        }

        return echoManaCost > 0
            ? InteractionKeywordProfileStatuses.Implemented
            : InteractionKeywordProfileStatuses.RecognizedDeferred;
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
