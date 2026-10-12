using Riftbound.CardCatalog;
using System.Text.RegularExpressions;

namespace Riftbound.Engine;

internal static class OfficialPrintedTimingRules
{
    private static readonly Lazy<IReadOnlySet<string>> PlayTriggers = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            // Weaponmaster has an independently captured trigger. Its reminder
            // must not also create the legacy permanent play-ability item.
            .Where(card => Regex.Replace(card.CardEffect, @"\{\{百炼\}\}[（(][^）)]*[）)]", "{{百炼}}").Split('。', '\n').Any(clause =>
                !clause.Contains("作为额外费用", StringComparison.Ordinal)
                && (clause.Contains("当你打出我", StringComparison.Ordinal)
                    || clause.Contains("当你打出此牌", StringComparison.Ordinal)
                    || clause.Contains("当你打出此装备", StringComparison.Ordinal)
                    || clause.Contains("当此牌被打出", StringComparison.Ordinal)
                    || clause.Contains("当你将我打出", StringComparison.Ordinal))))
            .Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal));
    internal static bool HasPlayTrigger(string cardNo) => PlayTriggers.Value.Contains(cardNo);
    private static readonly Lazy<IReadOnlySet<string>> OtherPlayTargets = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Where(card => Regex.IsMatch(card.CardEffect,
                @"(?:当你打出我|当你打出此牌|当你打出此装备|当此牌被打出|当你将我打出)[^。\n]*(?:另一名|其他[一二两三四五六七八九十0-9]+名)",
                RegexOptions.CultureInvariant)).Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal));
    internal static bool PlayTargetsExcludeSource(string cardNo) => OtherPlayTargets.Value.Contains(cardNo);
    private static readonly Lazy<IReadOnlyDictionary<string, (bool Swift, bool Reaction)>> Permissions = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.ToDictionary(card => card.CardNo,
            card => (HasPrintedKeyword(card.CardEffect, "迅捷"),
                HasPrintedKeyword(card.CardEffect, "反应") || HasPrintedKeyword(card.CardEffect, "灵便")), StringComparer.Ordinal));

    internal static CardBehaviorDefinition Apply(CardBehaviorDefinition definition)
        => Permissions.Value.TryGetValue(definition.CardNo, out var printed)
            && (definition.CanPlayDuringSpellDuel != printed.Swift || definition.CanPlayDuringPriority != printed.Reaction)
            ? definition with { CanPlayDuringSpellDuel = printed.Swift, CanPlayDuringPriority = printed.Reaction }
            : definition;

    // A keyword in an ability, a conditional grant, or reminder text is not an
    // unconditional permission to play this card (notably Ambush and rune skills).
    private static bool HasPrintedKeyword(string text, string keyword)
        => text.Split('\n').Any(line => line.TrimStart().StartsWith($"{{{{{keyword}}}}}", StringComparison.Ordinal));
}
