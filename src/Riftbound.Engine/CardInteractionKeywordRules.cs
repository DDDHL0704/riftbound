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
        var echoManaCost = behavior?.EchoManaCost ?? 0;
        var hasEcho = hasEchoKeyword || echoManaCost > 0;
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
                    "P4.4 implements mana-only Echo through the existing P2 optional cost repeat path.",
                InteractionKeywordProfileStatuses.RecognizedDeferred =>
                    "P4.9 recognizes interaction keyword surfaces; P4.70/P4.71/P4.76/P4.386 cover narrow Standby hide/reveal/reaction and one reaction resolution trigger, while Standby target damage, Ambush reaction battlefield play, and complex Echo costs remain deferred unless a separate P2 path covers the ordinary play effect.",
                _ =>
                    hasAnyInteractionKeyword
                        ? "Interaction keyword surface is recognized but has no P4 execution status."
                        : "Card does not expose interaction keywords through P3 BehaviorSpec or the P2 source-object tag path."
            });
    }

    public static CardEchoKeywordProfile BuildEchoProfile(CardBehaviorDefinition behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);

        if (behavior.EchoManaCost <= 0)
        {
            return new CardEchoKeywordProfile(
                false,
                0,
                EchoKeywordProfileStatuses.NotApplicable,
                "Card does not expose a P2 mana-only Echo optional cost.");
        }

        return new CardEchoKeywordProfile(
            true,
            behavior.EchoManaCost,
            EchoKeywordProfileStatuses.Implemented,
            "P4.4 mana-only Echo is implemented through the existing P2 optional cost repeat path.");
    }

    public static bool TryBuildEchoOptionalCost(
        IReadOnlyList<string> normalizedOptionalCosts,
        CardBehaviorDefinition behavior,
        out int extraManaCost,
        out int effectRepeatCount)
    {
        ArgumentNullException.ThrowIfNull(normalizedOptionalCosts);
        ArgumentNullException.ThrowIfNull(behavior);

        extraManaCost = 0;
        effectRepeatCount = 1;
        var profile = BuildEchoProfile(behavior);
        if (normalizedOptionalCosts.Count == 1
            && string.Equals(normalizedOptionalCosts[0], EchoOptionalCostNames.Echo, StringComparison.Ordinal)
            && profile.HasEcho)
        {
            extraManaCost = profile.EchoManaCost;
            effectRepeatCount = 2;
            return true;
        }

        return false;
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
