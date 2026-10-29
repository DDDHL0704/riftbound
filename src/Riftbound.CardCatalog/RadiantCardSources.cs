using System.Collections.Immutable;

namespace Riftbound.CardCatalog;

public static class RadiantCardSources
{
    public const string SnapshotPath = VendettaCardSources.SnapshotPath;
    private static readonly Lazy<ImmutableArray<OfficialCard>> Cards = new(Load);

    public static IReadOnlyList<OfficialCard> GetAdmittedCards() => Cards.Value;

    private static ImmutableArray<OfficialCard> Load()
    {
        using var stream = typeof(RadiantCardSources).Assembly.GetManifestResourceStream(
            "Riftbound.CardCatalog.VendettaSourceSnapshot")
            ?? throw new InvalidOperationException("The official card gallery snapshot is missing.");
        var catalog = OfficialCardCatalogLoader.LoadFromStreamAsync(stream, SnapshotPath).GetAwaiter().GetResult();
        // Admit each implemented physical printing by both official identifiers.
        return new[] { (Id: 39535L, CardNo: "RAD·042/167"), (Id: 39536L, CardNo: "RAD·042a/167"),
            (Id: 39540L, CardNo: "RAD·045/167"), (Id: 39541L, CardNo: "RAD·045a/167"),
            (Id: 39560L, CardNo: "RAD·063/167"),
            (Id: 39590L, CardNo: "RAD·090/167"), (Id: 39591L, CardNo: "RAD·090a/167"),
            (Id: 39584L, CardNo: "RAD·085/167"), (Id: 39655L, CardNo: "RAD·147/167"),
            (Id: 39656L, CardNo: "RAD·148/167"),
            (Id: 39684L, CardNo: "RAD·172*/167"), (Id: 39685L, CardNo: "RAD·172/167"),
            (Id: 39651L, CardNo: "RAD·143/167"),
            (Id: 39680L, CardNo: "RAD·170*/167"), (Id: 39681L, CardNo: "RAD·170/167"),
            (Id: 39652L, CardNo: "RAD·144/167"), (Id: 39657L, CardNo: "RAD·149/167"),
            (Id: 39686L, CardNo: "RAD·173*/167"), (Id: 39687L, CardNo: "RAD·173/167"),
            (Id: 39697L, CardNo: "RAD·179*/167"), (Id: 39698L, CardNo: "RAD·179/167"),
            (Id: 39699L, CardNo: "RAD·180*/167"), (Id: 39700L, CardNo: "RAD·180/167"),
            (Id: 39701L, CardNo: "RAD·181*/167"), (Id: 39702L, CardNo: "RAD·181/167"),
            (Id: 39703L, CardNo: "RAD·182*/167"), (Id: 39704L, CardNo: "RAD·182/167"),
            (Id: 39705L, CardNo: "RAD·183*/167"), (Id: 39706L, CardNo: "RAD·183/167"),
            (Id: 39585L, CardNo: "RAD·086/167"), (Id: 39586L, CardNo: "RAD·086a/167"),
            (Id: 39695L, CardNo: "RAD·178*/167"), (Id: 39696L, CardNo: "RAD·178/167"),
            (Id: 39611L, CardNo: "RAD·109/167"), (Id: 39612L, CardNo: "RAD·109a/167"),
            (Id: 39722L, CardNo: "RAD·SP3/005") }
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
