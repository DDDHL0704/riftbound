using System.Collections.Immutable;

namespace Riftbound.CardCatalog;

public static class RadiantCardSources
{
    public const string SnapshotPath = VendettaCardSources.SnapshotPath;
    private static readonly Lazy<ImmutableArray<OfficialCard>> Cards = new(Load);

    public static IReadOnlyList<OfficialCard> GetAdmittedCards() => Cards.Value;

    private static ImmutableArray<OfficialCard> Load()
    {
        // Admit each implemented physical printing by both official identifiers.
        using var stream = typeof(RadiantCardSources).Assembly.GetManifestResourceStream(
            "Riftbound.CardCatalog.VendettaSourceSnapshot")
            ?? throw new InvalidOperationException("The official card gallery snapshot is missing.");
        var catalog = OfficialCardCatalogLoader.LoadFromStreamAsync(stream, SnapshotPath).GetAwaiter().GetResult();
        return new[] {
            (Id: 39493L, CardNo: "RAD·003/167"), (Id: 39498L, CardNo: "RAD·008/167"),
            (Id: 39499L, CardNo: "RAD·009/167"), (Id: 39503L, CardNo: "RAD·013/167"),
            (Id: 39510L, CardNo: "RAD·020/167"), (Id: 39511L, CardNo: "RAD·020a/167"),
            (Id: 39518L, CardNo: "RAD·025/167"), (Id: 39545L, CardNo: "RAD·049/167"),
            (Id: 39554L, CardNo: "RAD·058/167"), (Id: 39555L, CardNo: "RAD·059/167"),
            (Id: 39569L, CardNo: "RAD·070/167"), (Id: 39603L, CardNo: "RAD·101/167"),
            (Id: 39626L, CardNo: "RAD·121/167"),
            (Id: 39570L, CardNo: "RAD·071/167"), (Id: 39576L, CardNo: "RAD·077/167"),
            (Id: 39582L, CardNo: "RAD·083/167"), (Id: 39588L, CardNo: "RAD·088/167"),
            (Id: 39594L, CardNo: "RAD·092/167"), (Id: 39623L, CardNo: "RAD·118/167"),
            (Id: 39628L, CardNo: "RAD·123/167"), (Id: 39630L, CardNo: "RAD·125/167"),
            (Id: 39640L, CardNo: "RAD·134/167"),
            (Id: 39643L, CardNo: "RAD·137/167"), (Id: 39644L, CardNo: "RAD·137a/167"),
            (Id: 39651L, CardNo: "RAD·143/167"), (Id: 39680L, CardNo: "RAD·170*/167"),
            (Id: 39681L, CardNo: "RAD·170/167"), (Id: 39652L, CardNo: "RAD·144/167"),
            (Id: 39657L, CardNo: "RAD·149/167"), (Id: 39686L, CardNo: "RAD·173*/167"),
            (Id: 39687L, CardNo: "RAD·173/167"), (Id: 39697L, CardNo: "RAD·179*/167"),
            (Id: 39698L, CardNo: "RAD·179/167"), (Id: 39699L, CardNo: "RAD·180*/167"),
            (Id: 39700L, CardNo: "RAD·180/167"), (Id: 39701L, CardNo: "RAD·181*/167"),
            (Id: 39702L, CardNo: "RAD·181/167"), (Id: 39703L, CardNo: "RAD·182*/167"),
            (Id: 39704L, CardNo: "RAD·182/167"), (Id: 39705L, CardNo: "RAD·183*/167"),
            (Id: 39706L, CardNo: "RAD·183/167") }
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
