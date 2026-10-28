using System.Collections.Immutable;

namespace Riftbound.CardCatalog;

// Admit only implemented printings from the official snapshot, not the whole set.
public static class VendettaCardSources
{
    public const string SnapshotPath = "data/official/upstream/2026-10-16/source-refresh-20261015T160523Z/card-catalog.zh-CN.json";
    private const string ResourceName = "Riftbound.CardCatalog.VendettaSourceSnapshot";
    private static readonly Lazy<ImmutableArray<OfficialCard>> Cards = new(Load);

    public static IReadOnlyList<OfficialCard> GetAdmittedCards() => Cards.Value;

    private static ImmutableArray<OfficialCard> Load()
    {
        using var stream = typeof(VendettaCardSources).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The official VEN snapshot is missing.");
        var catalog = OfficialCardCatalogLoader.LoadFromStreamAsync(stream, SnapshotPath).GetAwaiter().GetResult();
        return new[] { (Id: 37726L, CardNo: "VEN·038"), (Id: 37727L, CardNo: "VEN·038a") }
            .Select(identity => catalog.Cards.Single(card => card.Id == identity.Id && card.CardNo == identity.CardNo))
            .Select(card => card with {
                CardCategoryList = card.CardCategoryList.ToImmutableArray(),
                CardCategoryNameList = card.CardCategoryNameList.ToImmutableArray(),
                CardColorList = card.CardColorList.ToImmutableArray(),
                ProductCodeList = card.ProductCodeList.ToImmutableArray(),
                ProductNameList = card.ProductNameList.ToImmutableArray() })
            .ToImmutableArray();
    }
}
