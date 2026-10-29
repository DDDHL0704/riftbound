using System.Collections.Immutable;

namespace Riftbound.CardCatalog;

public static class RadiantCardSources
{
    public const string SnapshotPath = VendettaCardSources.SnapshotPath;
    private static readonly Lazy<ImmutableArray<OfficialCard>> Cards = new(Load);

    public static IReadOnlyList<OfficialCard> GetAdmittedCards() => Cards.Value;

    private static ImmutableArray<OfficialCard> Load()
    {
        // The gallery snapshot contains all sets; admit only implemented RAD printings.
        using var stream = typeof(RadiantCardSources).Assembly.GetManifestResourceStream(
            "Riftbound.CardCatalog.VendettaSourceSnapshot")
            ?? throw new InvalidOperationException("The official card gallery snapshot is missing.");
        var catalog = OfficialCardCatalogLoader.LoadFromStreamAsync(stream, SnapshotPath).GetAwaiter().GetResult();
        return new[] { (Id: 39493L, CardNo: "RAD·003/167"), (Id: 39498L, CardNo: "RAD·008/167"),
            (Id: 39555L, CardNo: "RAD·059/167"), (Id: 39576L, CardNo: "RAD·077/167"),
            (Id: 39588L, CardNo: "RAD·088/167") }
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
