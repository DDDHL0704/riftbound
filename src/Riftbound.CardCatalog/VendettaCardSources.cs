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
        return new[] { (Id: 37725L, CardNo: "VEN·037"), (Id: 37776L, CardNo: "VEN·082"),
            (Id: 37796L, CardNo: "VEN·099"), (Id: 37801L, CardNo: "VEN·104"), (Id: 37814L, CardNo: "VEN·114"), (Id: 37704L, CardNo: "VEN·019"), (Id: 37705L, CardNo: "VEN·019a"),
            (Id: 37842L, CardNo: "VEN·139"), (Id: 37892L, CardNo: "VEN·189"), (Id: 37893L, CardNo: "VEN·189*"),
            (Id: 37850L, CardNo: "VEN·147"), (Id: 37900L, CardNo: "VEN·193"), (Id: 37901L, CardNo: "VEN·193*") }
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
