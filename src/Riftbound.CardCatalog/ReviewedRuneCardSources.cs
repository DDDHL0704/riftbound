using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Riftbound.CardCatalog;

// These exact reviewed prints provide source facts to shared rune mechanisms.
// They do not add card behaviors or establish whole-card/format certification.
public static class ReviewedRuneCardSources
{
    public const string SnapshotPath =
        "data/official/upstream/2026-10-28/reviewed-rune-source-universe/card-catalog.zh-CN.json";
    private const string ResourceName = "Riftbound.CardCatalog.ReviewedRuneSourceSnapshot";
    private static readonly byte[] Snapshot = ReadSnapshot();
    private static readonly ImmutableArray<OfficialCard> Cards = Load();
    private static readonly ImmutableArray<OfficialCard> Physical = ReviewedCards("reviewedPhysicalPrints");
    private static readonly ImmutableArray<OfficialCard> ResourceCosts = ReviewedCards("reviewedResourceCostPrints");
    private static readonly FrozenDictionary<string, OfficialCard> ByCardNo =
        Cards.ToFrozenDictionary(card => card.CardNo, StringComparer.Ordinal);

    public static string SnapshotSha256 { get; } = Convert.ToHexString(SHA256.HashData(Snapshot)).ToLowerInvariant();
    public static IReadOnlyList<OfficialCard> GetAll() => Cards;
    public static IReadOnlyList<OfficialCard> GetPhysicalPrints() => Physical;
    public static IReadOnlyList<OfficialCard> GetPrintedResourceCostPrints() => ResourceCosts;
    public static bool TryGet(string? cardNo, out OfficialCard card)
    {
        card = default!;
        return cardNo is not null && ByCardNo.TryGetValue(cardNo, out card!);
    }

    private static byte[] ReadSnapshot()
    {
        using var stream = typeof(ReviewedRuneCardSources).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The reviewed rune card source snapshot is missing.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static ImmutableArray<OfficialCard> ReviewedCards(string field)
    {
        using var document = JsonDocument.Parse(Snapshot);
        var identities = document.RootElement.GetProperty(field).EnumerateArray()
            .Select(identity => (identity.GetProperty("id").GetInt64(), identity.GetProperty("cardNo").GetString()!))
            .ToFrozenSet();
        var result = Cards.Where(card => identities.Contains((card.Id, card.CardNo))).ToImmutableArray();
        if (result.Length != identities.Count) throw new InvalidDataException("A reviewed rune print is missing from its source snapshot.");
        return result;
    }

    private static ImmutableArray<OfficialCard> Load()
    {
        using var stream = new MemoryStream(Snapshot, writable: false);
        var catalog = OfficialCardCatalogLoader.LoadFromStreamAsync(stream, SnapshotPath).GetAwaiter().GetResult();
        using var document = JsonDocument.Parse(Snapshot);
        var text = document.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(
            card => card.GetProperty("cardNo").GetString()!, card =>
                card.TryGetProperty("errata", out var errata) && !string.IsNullOrWhiteSpace(errata.GetString())
                    ? errata.GetString()! : card.GetProperty("cardEffect").GetString()!, StringComparer.Ordinal);
        return catalog.Cards.Select(card => card with {
            CardEffect = text[card.CardNo],
            CardCategoryList = card.CardCategoryList.ToImmutableArray(),
            CardCategoryNameList = card.CardCategoryNameList.ToImmutableArray(),
            CardColorList = card.CardColorList.ToImmutableArray(),
            ProductCodeList = card.ProductCodeList.ToImmutableArray(),
            ProductNameList = card.ProductNameList.ToImmutableArray()
        }).ToImmutableArray();
    }
}
