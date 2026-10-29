using System.Collections.Immutable;

namespace Riftbound.CardCatalog;

// Admit only implemented printings from the official snapshot, not the whole set.
public static class VendettaCardSources
{
    public const string SnapshotPath = "data/official/upstream/2026-10-16/source-refresh-20261015T160523Z/card-catalog.zh-CN.json";
    private const string ResourceName = "Riftbound.CardCatalog.VendettaSourceSnapshot";
    private static readonly Lazy<ImmutableArray<OfficialCard>> Cards = new(Load);
    private static readonly Lazy<OfficialCardCatalog> Source = new(() => {
        using var stream = typeof(VendettaCardSources).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The official VEN snapshot is missing.");
        return OfficialCardCatalogLoader.LoadFromStreamAsync(stream, SnapshotPath).GetAwaiter().GetResult();
    });

    public static IReadOnlyList<OfficialCard> GetAdmittedCards() => Cards.Value;
    public static IReadOnlyList<OfficialCard> GetOfficialCards() => Source.Value.Cards;

    private static ImmutableArray<OfficialCard> Load()
    {
        var catalog = Source.Value;
        return new[] { (Id: 37696L, CardNo: "VEN·011"), (Id: 37715L, CardNo: "VEN·027"), (Id: 37767L, CardNo: "VEN·073"),
            (Id: 37704L, CardNo: "VEN·019"), (Id: 37705L, CardNo: "VEN·019a"), (Id: 37726L, CardNo: "VEN·038"),
            (Id: 37727L, CardNo: "VEN·038a"), (Id: 37842L, CardNo: "VEN·139"), (Id: 37892L, CardNo: "VEN·189"),
            (Id: 37893L, CardNo: "VEN·189*"), (Id: 37850L, CardNo: "VEN·147"), (Id: 37900L, CardNo: "VEN·193"),
            (Id: 37901L, CardNo: "VEN·193*"), (Id: 37854L, CardNo: "VEN·151"), (Id: 37942L, CardNo: "VEN·195"),
            (Id: 37905L, CardNo: "VEN·195*"), (Id: 37856L, CardNo: "VEN·153"), (Id: 37906L, CardNo: "VEN·196"),
            (Id: 37907L, CardNo: "VEN·196*"), (Id: 37735L, CardNo: "VEN·045"), (Id: 37756L, CardNo: "VEN·064"),
            (Id: 37793L, CardNo: "VEN·096"), (Id: 37795L, CardNo: "VEN·098"), (Id: 37798L, CardNo: "VEN·101"),
            (Id: 37819L, CardNo: "VEN·119"), (Id: 37820L, CardNo: "VEN·120"), (Id: 37832L, CardNo: "VEN·132"),
            (Id: 37689L, CardNo: "VEN·004"), (Id: 37713L, CardNo: "VEN·025"), (Id: 37717L, CardNo: "VEN·029"),
            (Id: 37724L, CardNo: "VEN·036"), (Id: 37744L, CardNo: "VEN·053"), (Id: 37703L, CardNo: "VEN·018"),
            (Id: 37770L, CardNo: "VEN·076"), (Id: 37936L, CardNo: "VEN·097"), (Id: 37861L, CardNo: "VEN·158"),
            (Id: 37863L, CardNo: "VEN·160"), (Id: 37866L, CardNo: "VEN·163"), (Id: 37867L, CardNo: "VEN·164"),
            (Id: 37864L, CardNo: "VEN·161"), (Id: 37745L, CardNo: "VEN·054"), (Id: 37751L, CardNo: "VEN·060"),
            (Id: 37753L, CardNo: "VEN·062"), (Id: 37769L, CardNo: "VEN·075"), (Id: 37771L, CardNo: "VEN·077"),
            (Id: 37782L, CardNo: "VEN·087"), (Id: 37825L, CardNo: "VEN·125"), (Id: 37833L, CardNo: "VEN·133") }
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
