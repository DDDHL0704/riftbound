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
        return new[] { (Id: 39557L, CardNo: "RAD·061/167"), (Id: 39558L, CardNo: "RAD·061a/167"),
            (Id: 39510L, CardNo: "RAD·020/167"), (Id: 39511L, CardNo: "RAD·020a/167"),
            (Id: 39643L, CardNo: "RAD·137/167"), (Id: 39644L, CardNo: "RAD·137a/167"),
            (Id: 39658L, CardNo: "RAD·150/167"), (Id: 39651L, CardNo: "RAD·143/167"),
            (Id: 39680L, CardNo: "RAD·170*/167"), (Id: 39681L, CardNo: "RAD·170/167"),
            (Id: 39652L, CardNo: "RAD·144/167"), (Id: 39657L, CardNo: "RAD·149/167"),
            (Id: 39686L, CardNo: "RAD·173*/167"), (Id: 39687L, CardNo: "RAD·173/167"),
            (Id: 39697L, CardNo: "RAD·179*/167"), (Id: 39698L, CardNo: "RAD·179/167"),
            (Id: 39699L, CardNo: "RAD·180*/167"), (Id: 39700L, CardNo: "RAD·180/167"),
            (Id: 39701L, CardNo: "RAD·181*/167"), (Id: 39702L, CardNo: "RAD·181/167"),
            (Id: 39703L, CardNo: "RAD·182*/167"), (Id: 39704L, CardNo: "RAD·182/167"),
            (Id: 39705L, CardNo: "RAD·183*/167"), (Id: 39706L, CardNo: "RAD·183/167"),
            (Id: 39495L, CardNo: "RAD·005/167"), (Id: 39504L, CardNo: "RAD·014/167"), (Id: 39519L, CardNo: "RAD·026/167"),
            (Id: 39564L, CardNo: "RAD·066/167"), (Id: 39571L, CardNo: "RAD·072/167"), (Id: 39574L, CardNo: "RAD·075/167"),
            (Id: 39524L, CardNo: "RAD·031/167"), (Id: 39580L, CardNo: "RAD·081/167"),
            (Id: 39597L, CardNo: "RAD·095/167"), (Id: 39601L, CardNo: "RAD·099/167"),
            (Id: 39634L, CardNo: "RAD·129/167"), (Id: 39573L, CardNo: "RAD·074/167"),
            (Id: 39577L, CardNo: "RAD·078/167"), (Id: 39575L, CardNo: "RAD·076/167"),
            (Id: 39579L, CardNo: "RAD·080/167"), (Id: 39523L, CardNo: "RAD·030/167"),
            (Id: 39508L, CardNo: "RAD·018/167") }
            .Select(identity => catalog.Cards.Single(card => card.Id == identity.Id && card.CardNo == identity.CardNo))
            .Concat(ScopedOfficialCardSources.GetPhysicalPrints().Where(card =>
                card.CardNo is "RAD·050/167" or "RAD·054/167" or "RAD·055/167" or "RAD·093/167" or "RAD·097/167"))
            .Select(card => card with {
                CardCategoryList = card.CardCategoryList.ToImmutableArray(),
                CardCategoryNameList = card.CardCategoryNameList.ToImmutableArray(),
                CardColorList = card.CardColorList.ToImmutableArray(),
                ProductCodeList = card.ProductCodeList.ToImmutableArray(),
                ProductNameList = card.ProductNameList.ToImmutableArray() })
            .ToImmutableArray();
    }
}
