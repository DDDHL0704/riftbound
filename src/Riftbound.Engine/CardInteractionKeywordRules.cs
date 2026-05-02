namespace Riftbound.Engine;

public static class CardInteractionKeywordNames
{
    public const string Echo = "回响";
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

public static class CardInteractionKeywordRules
{
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
}
