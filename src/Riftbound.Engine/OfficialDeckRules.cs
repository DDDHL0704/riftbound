using Riftbound.CardCatalog;

namespace Riftbound.Engine;

public sealed record OfficialDecklist(
    string LegendCardNo,
    string ChampionCardNo,
    IReadOnlyList<string> MainDeck,
    IReadOnlyList<string> RuneDeck,
    IReadOnlyList<string> Battlefields);

public sealed record OfficialDeckValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static OfficialDeckValidationResult Valid { get; } = new(true, []);
}

public static class OfficialDeckValidator
{
    public const int MinimumMainDeckCount = 40;
    public const int RuneDeckCount = 12;
    public const int BattlefieldCount = 3;
    public const int OpeningHandCount = 4;
    public const int MaximumMulliganCount = 2;
    public const int DefaultMaxCopiesByName = 3;
    public const int MaximumExclusiveCardsForLegend = 3;

    private static readonly HashSet<string> MainDeckCategories = new(StringComparer.Ordinal)
    {
        "单位",
        "英雄单位",
        "装备",
        "法术",
        "专属单位",
        "专属装备",
        "专属法术"
    };

    public static OfficialDeckValidationResult Validate(
        OfficialDecklist decklist,
        OfficialCardCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(decklist);
        ArgumentNullException.ThrowIfNull(catalog);

        var errors = new List<string>();
        var cardsByNo = catalog.Cards
            .Where(card => !string.IsNullOrWhiteSpace(card.CardNo))
            .ToDictionary(card => card.CardNo, StringComparer.Ordinal);

        var legendCardNo = Normalize(decklist.LegendCardNo);
        var championCardNo = Normalize(decklist.ChampionCardNo);
        var mainDeck = NormalizeList(decklist.MainDeck);
        var runeDeck = NormalizeList(decklist.RuneDeck);
        var battlefields = NormalizeList(decklist.Battlefields);

        var legend = RequireCard(cardsByNo, legendCardNo, "legendCardNo", errors);
        var champion = RequireCard(cardsByNo, championCardNo, "championCardNo", errors);

        if (legend is not null && !string.Equals(legend.CardCategoryName, "传奇", StringComparison.Ordinal))
        {
            errors.Add("legendCardNo must reference a 传奇 card.");
        }

        if (champion is not null && !string.Equals(champion.CardCategoryName, "英雄单位", StringComparison.Ordinal))
        {
            errors.Add("championCardNo must reference a 英雄单位 card.");
        }

        if (legend is not null
            && champion is not null
            && !string.Equals(legend.Hero, champion.Hero, StringComparison.Ordinal))
        {
            errors.Add("champion hero tag must match the selected legend hero tag.");
        }

        if (mainDeck.Count < MinimumMainDeckCount)
        {
            errors.Add($"mainDeck must contain at least {MinimumMainDeckCount} cards.");
        }

        if (!mainDeck.Contains(championCardNo, StringComparer.Ordinal))
        {
            errors.Add("mainDeck must contain one copy of championCardNo; the selected copy starts in the champion zone.");
        }

        if (runeDeck.Count != RuneDeckCount)
        {
            errors.Add($"runeDeck must contain exactly {RuneDeckCount} rune cards.");
        }

        if (battlefields.Count != BattlefieldCount)
        {
            errors.Add($"battlefields must contain exactly {BattlefieldCount} battlefield cards.");
        }

        ValidateMainDeck(cardsByNo, mainDeck, legend, errors);
        ValidateRuneDeck(cardsByNo, runeDeck, legend, errors);
        ValidateBattlefields(cardsByNo, battlefields, legend, errors);

        return errors.Count == 0
            ? OfficialDeckValidationResult.Valid
            : new OfficialDeckValidationResult(false, errors.Distinct(StringComparer.Ordinal).ToArray());
    }

    public static OfficialDecklist Normalize(OfficialDecklist decklist)
    {
        ArgumentNullException.ThrowIfNull(decklist);
        return new OfficialDecklist(
            Normalize(decklist.LegendCardNo),
            Normalize(decklist.ChampionCardNo),
            NormalizeList(decklist.MainDeck),
            NormalizeList(decklist.RuneDeck),
            NormalizeList(decklist.Battlefields));
    }

    private static void ValidateMainDeck(
        IReadOnlyDictionary<string, OfficialCard> cardsByNo,
        IReadOnlyList<string> mainDeck,
        OfficialCard? legend,
        List<string> errors)
    {
        var cards = new List<OfficialCard>();
        foreach (var cardNo in mainDeck)
        {
            var card = RequireCard(cardsByNo, cardNo, "mainDeck", errors);
            if (card is null)
            {
                continue;
            }

            cards.Add(card);
            if (!MainDeckCategories.Contains(card.CardCategoryName))
            {
                errors.Add($"mainDeck card {card.CardNo} has illegal category {card.CardCategoryName}.");
            }

            if (IsExclusive(card)
                && legend is not null
                && !string.Equals(card.Hero, legend.Hero, StringComparison.Ordinal))
            {
                errors.Add($"exclusive card {card.CardNo} does not match the selected legend hero tag.");
            }

            ValidateLegendTraits(card, legend, $"mainDeck card {card.CardNo}", errors);
        }

        ValidateCopyLimits(cards, errors);

        if (legend is not null)
        {
            var exclusiveCount = cards.Count(card => IsExclusive(card)
                && string.Equals(card.Hero, legend.Hero, StringComparison.Ordinal));
            if (exclusiveCount > MaximumExclusiveCardsForLegend)
            {
                errors.Add($"mainDeck can contain at most {MaximumExclusiveCardsForLegend} exclusive cards for the selected legend hero tag.");
            }
        }
    }

    private static void ValidateRuneDeck(
        IReadOnlyDictionary<string, OfficialCard> cardsByNo,
        IReadOnlyList<string> runeDeck,
        OfficialCard? legend,
        List<string> errors)
    {
        foreach (var cardNo in runeDeck)
        {
            var card = RequireCard(cardsByNo, cardNo, "runeDeck", errors);
            if (card is null)
            {
                continue;
            }

            if (!string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal))
            {
                errors.Add($"runeDeck card {card.CardNo} must be a 符文 card.");
            }

            ValidateLegendTraits(card, legend, $"runeDeck card {card.CardNo}", errors);
        }
    }

    private static void ValidateBattlefields(
        IReadOnlyDictionary<string, OfficialCard> cardsByNo,
        IReadOnlyList<string> battlefields,
        OfficialCard? legend,
        List<string> errors)
    {
        var battlefieldCards = new List<OfficialCard>();
        foreach (var cardNo in battlefields)
        {
            var card = RequireCard(cardsByNo, cardNo, "battlefields", errors);
            if (card is null)
            {
                continue;
            }

            battlefieldCards.Add(card);
            if (!string.Equals(card.CardCategoryName, "战场", StringComparison.Ordinal))
            {
                errors.Add($"battlefield card {card.CardNo} must be a 战场 card.");
            }

            ValidateLegendTraits(card, legend, $"battlefield card {card.CardNo}", errors);
        }

        var duplicateBattlefields = battlefieldCards
            .GroupBy(card => card.CardName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        foreach (var duplicate in duplicateBattlefields)
        {
            errors.Add($"battlefields cannot contain duplicate battlefield name {duplicate}.");
        }
    }

    private static void ValidateCopyLimits(IReadOnlyList<OfficialCard> cards, List<string> errors)
    {
        foreach (var group in cards.GroupBy(card => card.CardName, StringComparer.Ordinal))
        {
            var maxCopies = group.Any(IsUniqueCard) ? 1 : DefaultMaxCopiesByName;
            if (group.Count() > maxCopies)
            {
                errors.Add($"mainDeck contains {group.Count()} copies of {group.Key}; maximum is {maxCopies}.");
            }
        }
    }

    private static void ValidateLegendTraits(
        OfficialCard card,
        OfficialCard? legend,
        string label,
        List<string> errors)
    {
        if (legend is null)
        {
            return;
        }

        var allowedColors = ColorSet(legend);
        var cardColors = ColorSet(card);
        var illegalColors = cardColors
            .Where(color => !string.Equals(color, "colorless", StringComparison.Ordinal)
                && !allowedColors.Contains(color))
            .ToArray();
        if (illegalColors.Length > 0)
        {
            errors.Add($"{label} has traits outside the selected legend traits: {string.Join(", ", illegalColors)}.");
        }
    }

    private static OfficialCard? RequireCard(
        IReadOnlyDictionary<string, OfficialCard> cardsByNo,
        string cardNo,
        string field,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            errors.Add($"{field} contains a blank card number.");
            return null;
        }

        if (cardsByNo.TryGetValue(cardNo, out var card))
        {
            return card;
        }

        errors.Add($"{field} references unknown card {cardNo}.");
        return null;
    }

    private static bool IsExclusive(OfficialCard card)
    {
        return card.CardCategoryName.StartsWith("专属", StringComparison.Ordinal);
    }

    private static bool IsUniqueCard(OfficialCard card)
    {
        return card.CardGroupLimit == 1
            || card.CardEffect.Contains("{{唯我}}", StringComparison.Ordinal);
    }

    private static HashSet<string> ColorSet(OfficialCard card)
    {
        return card.CardColorList
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .Select(color => color.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
    }
}
