using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Riftbound.CardCatalog;

// Exact reviewed legendary faces for future shared-resource consumers. Their
// presence here does not register a legend action or certify event legality.
public static class ReviewedResourceLegendSources
{
    public const string SnapshotPath =
        "data/official/upstream/2026-10-28/reviewed-resource-legend-sources/card-catalog.zh-CN.json";
    private const string ResourceName = "Riftbound.CardCatalog.ReviewedResourceLegendSnapshot";
    private static readonly byte[] Snapshot = ReadSnapshot();
    private static readonly ImmutableArray<OfficialCard> Cards = Load();
    private static readonly FrozenDictionary<string, OfficialCard> ByCardNo =
        Cards.ToFrozenDictionary(card => card.CardNo, StringComparer.Ordinal);

    public static string SnapshotSha256 { get; } = Convert.ToHexString(SHA256.HashData(Snapshot)).ToLowerInvariant();
    public static IReadOnlyList<OfficialCard> GetAll() => Cards;
    public static IReadOnlyList<OfficialCard> GetPhysicalPrints() => Cards;
    public static bool TryGet(string? cardNo, out OfficialCard card)
    {
        card = default!;
        return cardNo is not null && ByCardNo.TryGetValue(cardNo, out card!);
    }

    private static byte[] ReadSnapshot()
    {
        using var stream = typeof(ReviewedResourceLegendSources).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The reviewed resource legend snapshot is missing.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static ImmutableArray<OfficialCard> Load()
    {
        using var stream = new MemoryStream(Snapshot, writable: false);
        var catalog = OfficialCardCatalogLoader.LoadFromStreamAsync(stream, SnapshotPath).GetAwaiter().GetResult();
        using var document = JsonDocument.Parse(Snapshot);
        var identities = document.RootElement.GetProperty("reviewedPhysicalPrints").EnumerateArray()
            .Select(row => (row.GetProperty("id").GetInt64(), row.GetProperty("cardNo").GetString()!)).ToFrozenSet();
        var text = document.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(
            row => row.GetProperty("cardNo").GetString()!, row =>
                row.TryGetProperty("errata", out var errata) && !string.IsNullOrWhiteSpace(errata.GetString())
                    ? errata.GetString()! : row.GetProperty("cardEffect").GetString()!, StringComparer.Ordinal);
        var cards = catalog.Cards.Select(card => card with {
            CardEffect = text[card.CardNo],
            CardCategoryList = card.CardCategoryList.ToImmutableArray(),
            CardCategoryNameList = card.CardCategoryNameList.ToImmutableArray(),
            CardColorList = card.CardColorList.ToImmutableArray(),
            ProductCodeList = card.ProductCodeList.ToImmutableArray(),
            ProductNameList = card.ProductNameList.ToImmutableArray()
        }).ToImmutableArray();
        if (cards.Length != identities.Count || cards.Any(card => !identities.Contains((card.Id, card.CardNo))))
            throw new InvalidDataException("A reviewed resource legend print does not match its source identity.");
        return cards;
    }
}
