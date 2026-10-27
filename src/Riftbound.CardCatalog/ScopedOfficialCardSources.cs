using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace Riftbound.CardCatalog;

// Retained exact source prints may have physical identities before their whole
// behavior is implemented. This metadata does not register gameplay or admit decks.
public static class ScopedOfficialCardSources
{
    public const string SnapshotPath =
        "data/official/upstream/2026-10-28/radiance-disarm/card-catalog.zh-CN.json";
    private const string ResourceName = "Riftbound.CardCatalog.DisarmSourceSnapshot";
    private static readonly byte[] Snapshot = ReadSnapshot();
    private static readonly ImmutableArray<OfficialCard> Cards = Load();
    private static readonly FrozenDictionary<string, OfficialCard> ByCardNo =
        Cards.ToFrozenDictionary(card => card.CardNo, StringComparer.Ordinal);

    public static string SnapshotSha256 { get; } =
        Convert.ToHexString(SHA256.HashData(Snapshot)).ToLowerInvariant();

    public static IReadOnlyList<OfficialCard> GetAll() => Cards;

    public static bool TryGet(string? cardNo, out OfficialCard card)
    {
        card = default!;
        return cardNo is not null && ByCardNo.TryGetValue(cardNo, out card!);
    }

    private static byte[] ReadSnapshot()
    {
        using var stream = typeof(ScopedOfficialCardSources).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The retained scoped card source snapshot is missing.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static ImmutableArray<OfficialCard> Load()
    {
        using var stream = new MemoryStream(Snapshot, writable: false);
        var catalog = OfficialCardCatalogLoader.LoadFromStreamAsync(stream, SnapshotPath)
            .GetAwaiter().GetResult();
        using var document = JsonDocument.Parse(Snapshot);
        var effectiveText = document.RootElement.GetProperty("cards").EnumerateArray()
            .ToDictionary(card => card.GetProperty("cardNo").GetString()!, card =>
                card.TryGetProperty("errata", out var errata) && !string.IsNullOrWhiteSpace(errata.GetString())
                    ? errata.GetString()! : card.GetProperty("cardEffect").GetString()!, StringComparer.Ordinal);
        return catalog.Cards.Select(card => card with {
            CardEffect = effectiveText[card.CardNo],
            CardCategoryList = card.CardCategoryList.ToImmutableArray(),
            CardCategoryNameList = card.CardCategoryNameList.ToImmutableArray(),
            CardColorList = card.CardColorList.ToImmutableArray(),
            ProductCodeList = card.ProductCodeList.ToImmutableArray(),
            ProductNameList = card.ProductNameList.ToImmutableArray()
        }).ToImmutableArray();
    }
}
