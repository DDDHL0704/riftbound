using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public sealed record OfficialCardCatalog(
    string Source,
    string Api,
    string FetchedAt,
    int Total,
    IReadOnlyList<OfficialCard> Cards)
{
    public static async Task<OfficialCardCatalog> LoadDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await OfficialCardCatalogLoader.LoadFromFileAsync(DefaultCatalogPath(), cancellationToken)
            .ConfigureAwait(false);
    }

    public static string DefaultCatalogPath()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "data", "official", "card-catalog.zh-CN.json");
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "data", "official", "card-catalog.zh-CN.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Official card catalog snapshot is missing.");
    }
}

public sealed record OfficialCard(
    long Id,
    string CardCategory,
    string CardCategoryName,
    string CardNo,
    string CardName,
    string SubTitle,
    string? ExtendType,
    string ExtendTypeName,
    IReadOnlyList<string> CardColorList,
    string Hero,
    string Region,
    string Tag,
    string Artist,
    string CardEffect,
    string FlavorText,
    int? Energy,
    int? ReturnEnergy,
    int? Power,
    IReadOnlyList<string> ProductCodeList,
    IReadOnlyList<string> ProductNameList,
    string Rarity,
    string RarityName,
    string ExtendRarity,
    string ExtendRarityName,
    string FrontImage,
    string BackImage,
    int? ListSort,
    int? Status,
    int? CardGroupLimit);

public sealed record FunctionalUnit(
    string Id,
    string Signature,
    string RepresentativeNo,
    string Name,
    string Category,
    IReadOnlyList<OfficialCard> Cards)
{
    public int Size => Cards.Count;
}

public sealed record FunctionalUnitSummary(
    int OfficialEntries,
    int FunctionalUnits,
    int DuplicateGroups,
    int DuplicateEntries,
    int SavedLogicImplementations);

public static class OfficialCardCatalogLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static async Task<OfficialCardCatalog> LoadFromFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var raw = await JsonSerializer.DeserializeAsync<RawCatalog>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (raw is null)
        {
            throw new InvalidOperationException($"Failed to load official card catalog from {path}.");
        }

        var cards = raw.Cards
            .Select(Normalize)
            .OrderByDescending(card => card.ListSort ?? int.MinValue)
            .ThenBy(card => card.Id)
            .ToArray();

        return new OfficialCardCatalog(
            raw.Source ?? string.Empty,
            raw.Api ?? string.Empty,
            raw.FetchedAt ?? string.Empty,
            raw.Total,
            cards);
    }

    private static OfficialCard Normalize(RawCard raw)
    {
        return new OfficialCard(
            raw.Id,
            Value(raw.CardCategory),
            Value(raw.CardCategoryName),
            Value(raw.CardNo),
            Value(raw.CardName),
            Value(raw.SubTitle),
            raw.ExtendType,
            Value(raw.ExtendTypeName),
            raw.CardColorList ?? [],
            Value(raw.Hero),
            Value(raw.Region),
            Value(raw.Tag),
            Value(raw.Artist),
            Value(raw.CardEffect),
            Value(raw.FlavorText),
            raw.Energy,
            raw.ReturnEnergy,
            raw.Power,
            raw.ProductCodeList ?? [],
            raw.ProductNameList ?? [],
            Value(raw.Rarity),
            Value(raw.RarityName),
            Value(raw.ExtendRarity),
            Value(raw.ExtendRarityName),
            Value(raw.FrontImage),
            Value(raw.BackImage),
            raw.ListSort,
            raw.Status,
            raw.CardGroupLimit);
    }

    private static string Value(string? raw)
    {
        return raw ?? string.Empty;
    }

    private sealed record RawCatalog(
        string? Source,
        string? Api,
        string? FetchedAt,
        int Total,
        IReadOnlyList<RawCard> Cards);

    private sealed record RawCard(
        long Id,
        string? CardCategory,
        string? CardCategoryName,
        string? CardNo,
        string? CardName,
        string? SubTitle,
        string? ExtendType,
        string? ExtendTypeName,
        IReadOnlyList<string>? CardColorList,
        string? Hero,
        string? Region,
        string? Tag,
        string? Artist,
        string? CardEffect,
        string? FlavorText,
        int? Energy,
        int? ReturnEnergy,
        int? Power,
        IReadOnlyList<string>? ProductCodeList,
        IReadOnlyList<string>? ProductNameList,
        string? Rarity,
        string? RarityName,
        string? ExtendRarity,
        string? ExtendRarityName,
        string? FrontImage,
        string? BackImage,
        int? ListSort,
        int? Status,
        int? CardGroupLimit);
}

public static partial class FunctionalUnitBuilder
{
    private static readonly JsonSerializerOptions SignatureJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static IReadOnlyList<FunctionalUnit> Build(IReadOnlyList<OfficialCard> cards)
    {
        var groupsBySignature = new Dictionary<string, List<OfficialCard>>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            var signature = FunctionalSignature(card);
            if (!groupsBySignature.TryGetValue(signature, out var group))
            {
                group = [];
                groupsBySignature.Add(signature, group);
            }

            group.Add(card);
        }

        return groupsBySignature
            .Select(entry =>
            {
                var groupCards = entry.Value
                    .OrderBy(card => card.CardNo, StringComparer.Ordinal)
                    .ToArray();
                var representative = groupCards[0];
                return new FunctionalUnit(
                    $"FU-{Sha1(entry.Key)[..10]}",
                    entry.Key,
                    representative.CardNo,
                    representative.CardName,
                    !string.IsNullOrWhiteSpace(representative.CardCategoryName)
                        ? representative.CardCategoryName
                        : representative.CardCategory,
                    groupCards);
            })
            .OrderByDescending(unit => unit.Size)
            .ThenBy(unit => unit.Name, StringComparer.Ordinal)
            .ThenBy(unit => unit.RepresentativeNo, StringComparer.Ordinal)
            .ToArray();
    }

    public static FunctionalUnitSummary Summarize(IReadOnlyList<FunctionalUnit> units)
    {
        var officialEntries = units.Sum(unit => unit.Size);
        var duplicateGroups = units.Count(unit => unit.Size > 1);
        var duplicateEntries = units.Where(unit => unit.Size > 1).Sum(unit => unit.Size);
        return new FunctionalUnitSummary(
            officialEntries,
            units.Count,
            duplicateGroups,
            duplicateEntries,
            officialEntries - units.Count);
    }

    private static string FunctionalSignature(OfficialCard card)
    {
        var signature = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cardCategory"] = NormalizeSignatureValue(card.CardCategory),
            ["cardCategoryName"] = NormalizeSignatureValue(card.CardCategoryName),
            ["cardName"] = NormalizeSignatureValue(card.CardName),
            ["subTitle"] = NormalizeSignatureValue(card.SubTitle),
            ["extendType"] = NormalizeSignatureValue(card.ExtendType),
            ["extendTypeName"] = NormalizeSignatureValue(card.ExtendTypeName),
            ["cardColorList"] = NormalizeSignatureArray(card.CardColorList),
            ["hero"] = NormalizeSignatureValue(card.Hero),
            ["region"] = NormalizeSignatureValue(card.Region),
            ["tag"] = NormalizeSignatureValue(card.Tag),
            ["cardEffect"] = NormalizeRulesText(card.CardEffect),
            ["energy"] = NormalizeSignatureValue(card.Energy),
            ["returnEnergy"] = NormalizeSignatureValue(card.ReturnEnergy),
            ["power"] = NormalizeSignatureValue(card.Power),
            ["cardGroupLimit"] = NormalizeSignatureValue(card.CardGroupLimit)
        };
        return JsonSerializer.Serialize(signature, SignatureJsonOptions);
    }

    private static IReadOnlyList<string> NormalizeSignatureArray(IReadOnlyList<string> values)
    {
        return values
            .Select(NormalizeSignatureValue)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeRulesText(string? value)
    {
        return NormalizeSignatureValue(value)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n ", "\n", StringComparison.Ordinal)
            .Replace(" \n", "\n", StringComparison.Ordinal)
            .Trim();
    }

    private static string NormalizeSignatureValue(object? value)
    {
        var normalized = (Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
            .Normalize(NormalizationForm.FormKC)
            .Trim();
        return WhitespaceRegex()
            .Replace(normalized, " ")
            .Trim();
    }

    private static string Sha1(string value)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
