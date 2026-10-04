using Riftbound.CardCatalog;

namespace Riftbound.Engine;

internal static class OfficialPrintedTimingRules
{
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
