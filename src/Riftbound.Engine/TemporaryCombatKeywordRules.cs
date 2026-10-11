namespace Riftbound.Engine;

// Each application is a distinct grant: repeated Shield grants add (CN 814.2).
// The ordinary turn cleanup and outside-play reconstruction remove these effects.
internal static class TemporaryCombatKeywordRules
{
    internal const string Prefix = "COMBAT_KEYWORD:";
    internal static string AppliedEffectId(CardObjectState card, string definition)
        => definition.StartsWith(Prefix, StringComparison.Ordinal)
            ? definition + ":" + (card.UntilEndOfTurnEffects.Count(e => e.StartsWith(definition + ":", StringComparison.Ordinal)) + 1)
            : definition;
    private static IEnumerable<string> Keywords(CardObjectState card)
        => card.UntilEndOfTurnEffects.Where(e => e.StartsWith(Prefix, StringComparison.Ordinal) && IsValid(e))
            .Select(e => e[Prefix.Length..e.LastIndexOf(':')]);
    internal static int Amount(CardObjectState card, string keyword)
        => Keywords(card).Sum(tag => CardCombatKeywordRules.KeywordAmount([tag], keyword));
    internal static bool Has(CardObjectState card, string keyword)
        => Keywords(card).Contains(keyword, StringComparer.Ordinal);
    internal static bool IsValid(string effect)
    {
        if (!effect.StartsWith(Prefix, StringComparison.Ordinal)) return true;
        var separator = effect.LastIndexOf(':');
        if (separator <= Prefix.Length || !int.TryParse(effect[(separator + 1)..], out var instance) || instance <= 0) return false;
        var keyword = effect[Prefix.Length..separator];
        return keyword is CardCombatKeywordNames.Bulwark or CardCombatKeywordNames.BackRow
            || CardCombatKeywordRules.KeywordAmount([keyword], CardCombatKeywordNames.Steadfast) > 0
            || CardCombatKeywordRules.KeywordAmount([keyword], CardCombatKeywordNames.Assault) > 0;
    }
}
