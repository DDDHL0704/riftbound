namespace Riftbound.Engine;

// Each application is a distinct grant: repeated Shield grants add (CN 814.2).
// The ordinary turn cleanup and outside-play reconstruction remove these effects.
internal static class TemporaryCombatKeywordRules
{
    internal const string Prefix = "COMBAT_KEYWORD:";
    internal const string ValueSourcePrefix = "COMBAT_KEYWORD_FROM:";
    internal static string AppliedEffectId(CardObjectState card, string definition)
        => definition.StartsWith(Prefix, StringComparison.Ordinal) || definition.StartsWith(ValueSourcePrefix, StringComparison.Ordinal)
            ? definition + ":" + (card.UntilEndOfTurnEffects.Count(e => e.StartsWith(definition + ":", StringComparison.Ordinal)) + 1)
            : definition;
    private static IEnumerable<string> Keywords(CardObjectState card)
        => card.UntilEndOfTurnEffects.Where(e => e.StartsWith(Prefix, StringComparison.Ordinal) && IsValid(e))
            .Select(e => e[Prefix.Length..e.LastIndexOf(':')]);
    internal static int Amount(CardObjectState card, string keyword)
        => Keywords(card).Sum(tag => CardCombatKeywordRules.KeywordAmount([tag], keyword));
    internal static bool Has(CardObjectState card, string keyword)
        => Keywords(card).Contains(keyword, StringComparer.Ordinal);
    internal static IEnumerable<string> ValueSources(CardObjectState card, string keyword)
        => card.UntilEndOfTurnEffects.Where(e => e.StartsWith(ValueSourcePrefix, StringComparison.Ordinal) && IsValid(e))
            .Select(e => e.Split(':')).Where(p => p[1] == keyword).Select(p => p[2]);
    internal static bool IsValid(string effect)
    {
        if (effect.StartsWith(ValueSourcePrefix, StringComparison.Ordinal)) {
            var parts = effect.Split(':');
            return parts.Length == 4 && parts[1] != parts[2]
                && parts[1] is CardCombatKeywordNames.Assault or CardCombatKeywordNames.Steadfast
                && parts[2] is CardCombatKeywordNames.Assault or CardCombatKeywordNames.Steadfast
                && int.TryParse(parts[3], out var ordinal) && ordinal > 0;
        }
        if (!effect.StartsWith(Prefix, StringComparison.Ordinal)) return true;
        var separator = effect.LastIndexOf(':');
        if (separator <= Prefix.Length || !int.TryParse(effect[(separator + 1)..], out var instance) || instance <= 0) return false;
        var keyword = effect[Prefix.Length..separator];
        return keyword is CardCombatKeywordNames.Bulwark or CardCombatKeywordNames.BackRow
            || CardCombatKeywordRules.KeywordAmount([keyword], CardCombatKeywordNames.Steadfast) > 0
            || CardCombatKeywordRules.KeywordAmount([keyword], CardCombatKeywordNames.Assault) > 0
            || CardResourceKeywordRules.SpellshieldTaxFromTags([keyword]) > 0;
    }
}
