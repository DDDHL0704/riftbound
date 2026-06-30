using Riftbound.CardCatalog;

namespace Riftbound.Engine;

internal static class OfficialCardSourceIdentityGroups
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildByRepresentativeCardNo(
        IEnumerable<string?> representativeCardNos)
    {
        var catalog = OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult();
        var representatives = representativeCardNos
            .Select(NormalizeCardNo)
            .Where(cardNo => !string.IsNullOrWhiteSpace(cardNo))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        var sourceCardNosByRepresentative = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var group in catalog.Cards.GroupBy(SourceCardIdentitySignature, StringComparer.Ordinal))
        {
            var groupCardNos = group
                .Select(card => NormalizeCardNo(card.CardNo))
                .Where(cardNo => !string.IsNullOrWhiteSpace(cardNo))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var groupRepresentatives = groupCardNos
                .Where(representatives.Contains)
                .ToArray();

            if (groupRepresentatives.Length == 1)
            {
                sourceCardNosByRepresentative[groupRepresentatives[0]] = groupCardNos;
            }
            else
            {
                foreach (var representative in groupRepresentatives)
                {
                    sourceCardNosByRepresentative[representative] = [representative];
                }
            }
        }

        return sourceCardNosByRepresentative;
    }

    public static string NormalizeCardNo(string? value)
    {
        return NormalizeSourceIdentityValue(value);
    }

    private static string SourceCardIdentitySignature(OfficialCard card)
    {
        return string.Join(
            "\u001F",
            NormalizeSourceIdentityValue(card.CardCategoryName),
            NormalizeSourceIdentityValue(card.CardName),
            NormalizeSourceIdentityValue(card.SubTitle),
            string.Join(",", card.CardColorList.Select(NormalizeSourceIdentityValue).Order(StringComparer.Ordinal)),
            NormalizeSourceIdentityValue(card.Hero),
            NormalizeSourceIdentityValue(card.Tag),
            NormalizeSourceRulesText(card.CardEffect),
            NormalizeSourceIdentityValue(card.Energy),
            NormalizeSourceIdentityValue(card.ReturnEnergy),
            NormalizeSourceIdentityValue(card.Power),
            NormalizeSourceIdentityValue(card.CardGroupLimit));
    }

    private static string NormalizeSourceRulesText(string? value)
    {
        var normalized = NormalizeSourceIdentityValue(value)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n ", "\n", StringComparison.Ordinal)
            .Replace(" \n", "\n", StringComparison.Ordinal);
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            "[（(][^）)]*[）)]",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        normalized = normalized
            .Replace(" — ", "—", StringComparison.Ordinal)
            .Replace(" —", "—", StringComparison.Ordinal)
            .Replace("— ", "—", StringComparison.Ordinal);
        return System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @"\s+",
                " ",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Trim();
    }

    private static string NormalizeSourceIdentityValue(object? value)
    {
        return System.Text.RegularExpressions.Regex.Replace(
                (Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
                    .Normalize(System.Text.NormalizationForm.FormKC)
                    .Trim(),
                @"\s+",
                " ",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Trim();
    }
}
