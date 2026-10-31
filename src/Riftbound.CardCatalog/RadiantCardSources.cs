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
        return new[] { (Id: 39491L, CardNo: "RAD·001/167"), (Id: 39607L, CardNo: "RAD·105/167"), (Id: 39549L, CardNo: "RAD·053/167"), (Id: 39506L, CardNo: "RAD·016/167"),
            (Id: 39553L, CardNo: "RAD·057/167"), (Id: 39625L, CardNo: "RAD·120/167"),
            (Id: 39537L, CardNo: "RAD·043/167"), (Id: 39538L, CardNo: "RAD·043a/167"),
            (Id: 39638L, CardNo: "RAD·133/167"), (Id: 39639L, CardNo: "RAD·133a/167"),
            (Id: 39618L, CardNo: "RAD·114/167"), (Id: 39619L, CardNo: "RAD·114a/167"),
            (Id: 39535L, CardNo: "RAD·042/167"), (Id: 39536L, CardNo: "RAD·042a/167"),
            (Id: 39540L, CardNo: "RAD·045/167"), (Id: 39541L, CardNo: "RAD·045a/167"),
            (Id: 39560L, CardNo: "RAD·063/167"),
            (Id: 39562L, CardNo: "RAD·065/167"), (Id: 39563L, CardNo: "RAD·065a/167"),
            (Id: 39590L, CardNo: "RAD·090/167"), (Id: 39591L, CardNo: "RAD·090a/167"),
            (Id: 39584L, CardNo: "RAD·085/167"), (Id: 39655L, CardNo: "RAD·147/167"),
            (Id: 39656L, CardNo: "RAD·148/167"),
            (Id: 39653L, CardNo: "RAD·145/167"),
            (Id: 39682L, CardNo: "RAD·171*/167"), (Id: 39683L, CardNo: "RAD·171/167"),
            (Id: 39684L, CardNo: "RAD·172*/167"), (Id: 39685L, CardNo: "RAD·172/167"),
            (Id: 39613L, CardNo: "RAD·110/167"),
            (Id: 39510L, CardNo: "RAD·020/167"), (Id: 39511L, CardNo: "RAD·020a/167"),
            (Id: 39645L, CardNo: "RAD·138/167"), (Id: 39646L, CardNo: "RAD·138a/167"),
            (Id: 39724L, CardNo: "RAD·SP5/005"),
            (Id: 39566L, CardNo: "RAD·068/167"), (Id: 39567L, CardNo: "RAD·068a/167"),
            (Id: 39659L, CardNo: "RAD·151/167"),
            (Id: 39688L, CardNo: "RAD·174*/167"), (Id: 39689L, CardNo: "RAD·174/167"),
            (Id: 39725L, CardNo: "RAD·174a*/167"), (Id: 39726L, CardNo: "RAD·174a/167"),
            (Id: 39557L, CardNo: "RAD·061/167"), (Id: 39558L, CardNo: "RAD·061a/167"),
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
            (Id: 39585L, CardNo: "RAD·086/167"), (Id: 39586L, CardNo: "RAD·086a/167"),
            (Id: 39695L, CardNo: "RAD·178*/167"), (Id: 39696L, CardNo: "RAD·178/167"),
            (Id: 39611L, CardNo: "RAD·109/167"), (Id: 39612L, CardNo: "RAD·109a/167"),
            (Id: 39722L, CardNo: "RAD·SP3/005"),
            (Id: 39661L, CardNo: "RAD·153/167"), (Id: 39690L, CardNo: "RAD·175*/167"),
            (Id: 39691L, CardNo: "RAD·175/167"),
            (Id: 39495L, CardNo: "RAD·005/167"), (Id: 39504L, CardNo: "RAD·014/167"), (Id: 39519L, CardNo: "RAD·026/167"),
            (Id: 39564L, CardNo: "RAD·066/167"), (Id: 39571L, CardNo: "RAD·072/167"), (Id: 39574L, CardNo: "RAD·075/167"),
            (Id: 39524L, CardNo: "RAD·031/167"), (Id: 39580L, CardNo: "RAD·081/167"),
            (Id: 39597L, CardNo: "RAD·095/167"), (Id: 39601L, CardNo: "RAD·099/167"),
            (Id: 39634L, CardNo: "RAD·129/167"), (Id: 39573L, CardNo: "RAD·074/167"),
            (Id: 39577L, CardNo: "RAD·078/167"), (Id: 39575L, CardNo: "RAD·076/167"),
            (Id: 39579L, CardNo: "RAD·080/167"), (Id: 39523L, CardNo: "RAD·030/167"),
            (Id: 39529L, CardNo: "RAD·036/167"), (Id: 39636L, CardNo: "RAD·131/167"),
            (Id: 39631L, CardNo: "RAD·126/167"), (Id: 39635L, CardNo: "RAD·130/167"), (Id: 39565L, CardNo: "RAD·067/167"),
            (Id: 39508L, CardNo: "RAD·018/167"), (Id: 39501L, CardNo: "RAD·011/167"),
            (Id: 39556L, CardNo: "RAD·060/167"), (Id: 39605L, CardNo: "RAD·103/167"), (Id: 39609L, CardNo: "RAD·107/167") }
            .Select(identity => catalog.Cards.Single(card => card.Id == identity.Id && card.CardNo == identity.CardNo))
            .Concat(ScopedOfficialCardSources.GetPhysicalPrints().Where(card =>
                card.CardNo is "RAD·038/167" or "RAD·050/167" or "RAD·054/167" or "RAD·055/167" or "RAD·093/167" or "RAD·097/167"))
            .Select(card => card with {
                CardCategoryList = card.CardCategoryList.ToImmutableArray(),
                CardCategoryNameList = card.CardCategoryNameList.ToImmutableArray(),
                CardColorList = card.CardColorList.ToImmutableArray(),
                ProductCodeList = card.ProductCodeList.ToImmutableArray(),
                ProductNameList = card.ProductNameList.ToImmutableArray() })
            .ToImmutableArray();
    }
}
