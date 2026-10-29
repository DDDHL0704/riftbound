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
        return new[] {
            (Id: 37687L, CardNo: "VEN·002"), (Id: 37689L, CardNo: "VEN·004"), (Id: 37694L, CardNo: "VEN·009"),
            (Id: 37696L, CardNo: "VEN·011"), (Id: 37700L, CardNo: "VEN·015"), (Id: 37701L, CardNo: "VEN·016"),
            (Id: 37703L, CardNo: "VEN·018"), (Id: 37704L, CardNo: "VEN·019"), (Id: 37705L, CardNo: "VEN·019a"),
            (Id: 37706L, CardNo: "VEN·020"), (Id: 37707L, CardNo: "VEN·021"), (Id: 37708L, CardNo: "VEN·021a"),
            (Id: 37709L, CardNo: "VEN·022"), (Id: 37710L, CardNo: "VEN·023"), (Id: 37711L, CardNo: "VEN·023a"),
            (Id: 37712L, CardNo: "VEN·024"), (Id: 37713L, CardNo: "VEN·025"), (Id: 37715L, CardNo: "VEN·027"),
            (Id: 37716L, CardNo: "VEN·028"), (Id: 37717L, CardNo: "VEN·029"), (Id: 37719L, CardNo: "VEN·031"),
            (Id: 37986L, CardNo: "VEN·034"), (Id: 37724L, CardNo: "VEN·036"), (Id: 37725L, CardNo: "VEN·037"),
            (Id: 37726L, CardNo: "VEN·038"), (Id: 37727L, CardNo: "VEN·038a"), (Id: 37728L, CardNo: "VEN·039"),
            (Id: 37729L, CardNo: "VEN·040"), (Id: 37730L, CardNo: "VEN·041"), (Id: 37983L, CardNo: "VEN·044"),
            (Id: 37735L, CardNo: "VEN·045"), (Id: 37736L, CardNo: "VEN·046"), (Id: 37737L, CardNo: "VEN·046a"),
            (Id: 37743L, CardNo: "VEN·052"), (Id: 37744L, CardNo: "VEN·053"), (Id: 37745L, CardNo: "VEN·054"),
            (Id: 37751L, CardNo: "VEN·060"), (Id: 37752L, CardNo: "VEN·061"), (Id: 37753L, CardNo: "VEN·062"),
            (Id: 37754L, CardNo: "VEN·063"), (Id: 37755L, CardNo: "VEN·063a"), (Id: 37756L, CardNo: "VEN·064"),
            (Id: 37937L, CardNo: "VEN·065"), (Id: 37758L, CardNo: "VEN·066"), (Id: 37759L, CardNo: "VEN·067"),
            (Id: 37760L, CardNo: "VEN·068"), (Id: 37761L, CardNo: "VEN·068a"), (Id: 37765L, CardNo: "VEN·071"),
            (Id: 37767L, CardNo: "VEN·073"), (Id: 37769L, CardNo: "VEN·075"), (Id: 37770L, CardNo: "VEN·076"),
            (Id: 37771L, CardNo: "VEN·077"), (Id: 37773L, CardNo: "VEN·079"), (Id: 37774L, CardNo: "VEN·080"),
            (Id: 37776L, CardNo: "VEN·082"), (Id: 37777L, CardNo: "VEN·083"), (Id: 37778L, CardNo: "VEN·084"),
            (Id: 37779L, CardNo: "VEN·084a"), (Id: 37780L, CardNo: "VEN·085"), (Id: 37782L, CardNo: "VEN·087"),
            (Id: 37783L, CardNo: "VEN·088"), (Id: 37784L, CardNo: "VEN·088a"), (Id: 37786L, CardNo: "VEN·090"),
            (Id: 37787L, CardNo: "VEN·091"), (Id: 37788L, CardNo: "VEN·092"), (Id: 37789L, CardNo: "VEN·092a"),
            (Id: 37791L, CardNo: "VEN·094"), (Id: 37792L, CardNo: "VEN·095"), (Id: 37793L, CardNo: "VEN·096"),
            (Id: 37936L, CardNo: "VEN·097"), (Id: 37795L, CardNo: "VEN·098"), (Id: 37796L, CardNo: "VEN·099"),
            (Id: 37797L, CardNo: "VEN·100"), (Id: 37798L, CardNo: "VEN·101"), (Id: 37799L, CardNo: "VEN·102"),
            (Id: 37801L, CardNo: "VEN·104"), (Id: 37803L, CardNo: "VEN·106"), (Id: 37804L, CardNo: "VEN·107"),
            (Id: 37805L, CardNo: "VEN·108"), (Id: 37806L, CardNo: "VEN·109"), (Id: 37808L, CardNo: "VEN·110a"),
            (Id: 37809L, CardNo: "VEN·111"), (Id: 37810L, CardNo: "VEN·112"), (Id: 37811L, CardNo: "VEN·112a"),
            (Id: 37812L, CardNo: "VEN·113"), (Id: 37813L, CardNo: "VEN·113a"), (Id: 37814L, CardNo: "VEN·114"),
            (Id: 37815L, CardNo: "VEN·115"), (Id: 37816L, CardNo: "VEN·116"), (Id: 37819L, CardNo: "VEN·119"),
            (Id: 37820L, CardNo: "VEN·120"), (Id: 37821L, CardNo: "VEN·121"), (Id: 37825L, CardNo: "VEN·125"),
            (Id: 37826L, CardNo: "VEN·126"), (Id: 37827L, CardNo: "VEN·127"), (Id: 37831L, CardNo: "VEN·131"),
            (Id: 37832L, CardNo: "VEN·132"), (Id: 37833L, CardNo: "VEN·133"), (Id: 37834L, CardNo: "VEN·134"),
            (Id: 37835L, CardNo: "VEN·135"), (Id: 37836L, CardNo: "VEN·135a"), (Id: 37837L, CardNo: "VEN·136"),
            (Id: 37838L, CardNo: "VEN·136a"), (Id: 37842L, CardNo: "VEN·139"), (Id: 37843L, CardNo: "VEN·140"),
            (Id: 37845L, CardNo: "VEN·142"), (Id: 37846L, CardNo: "VEN·143"), (Id: 37847L, CardNo: "VEN·144"),
            (Id: 37848L, CardNo: "VEN·145"), (Id: 37849L, CardNo: "VEN·146"), (Id: 37850L, CardNo: "VEN·147"),
            (Id: 37851L, CardNo: "VEN·148"), (Id: 37852L, CardNo: "VEN·149"), (Id: 37853L, CardNo: "VEN·150"),
            (Id: 37854L, CardNo: "VEN·151"), (Id: 37856L, CardNo: "VEN·153"), (Id: 37857L, CardNo: "VEN·154"),
            (Id: 37858L, CardNo: "VEN·155"), (Id: 38299L, CardNo: "VEN·155a"), (Id: 37860L, CardNo: "VEN·157"),
            (Id: 37861L, CardNo: "VEN·158"), (Id: 37863L, CardNo: "VEN·160"), (Id: 37864L, CardNo: "VEN·161"),
            (Id: 37865L, CardNo: "VEN·162"), (Id: 37866L, CardNo: "VEN·163"), (Id: 37867L, CardNo: "VEN·164"),
            (Id: 37868L, CardNo: "VEN·165"), (Id: 37869L, CardNo: "VEN·166"), (Id: 37870L, CardNo: "VEN·167"),
            (Id: 37871L, CardNo: "VEN·168"), (Id: 37872L, CardNo: "VEN·169"), (Id: 37874L, CardNo: "VEN·171"),
            (Id: 37875L, CardNo: "VEN·172"), (Id: 37876L, CardNo: "VEN·173"), (Id: 37877L, CardNo: "VEN·174"),
            (Id: 37878L, CardNo: "VEN·175"), (Id: 37879L, CardNo: "VEN·176"), (Id: 37880L, CardNo: "VEN·177"),
            (Id: 37881L, CardNo: "VEN·178"), (Id: 37883L, CardNo: "VEN·180"), (Id: 37885L, CardNo: "VEN·182"),
            (Id: 37887L, CardNo: "VEN·184"), (Id: 37888L, CardNo: "VEN·185"), (Id: 37890L, CardNo: "VEN·187"),
            (Id: 37891L, CardNo: "VEN·188"), (Id: 37892L, CardNo: "VEN·189"), (Id: 37893L, CardNo: "VEN·189*"),
            (Id: 37896L, CardNo: "VEN·191"), (Id: 37897L, CardNo: "VEN·191*"), (Id: 37898L, CardNo: "VEN·192"),
            (Id: 37899L, CardNo: "VEN·192*"), (Id: 37900L, CardNo: "VEN·193"), (Id: 37901L, CardNo: "VEN·193*"),
            (Id: 37902L, CardNo: "VEN·194"), (Id: 37903L, CardNo: "VEN·194*"), (Id: 37942L, CardNo: "VEN·195"),
            (Id: 37905L, CardNo: "VEN·195*"), (Id: 37906L, CardNo: "VEN·196"), (Id: 37907L, CardNo: "VEN·196*"),
            (Id: 37908L, CardNo: "VEN·197"), (Id: 37909L, CardNo: "VEN·197*"), (Id: 37916L, CardNo: "VEN·SP1"),
            (Id: 37917L, CardNo: "VEN·SP2"), (Id: 37919L, CardNo: "VEN·SP4"), (Id: 37920L, CardNo: "VEN·SP5"),
            (Id: 37924L, CardNo: "VEN·T04"), (Id: 37925L, CardNo: "VEN·T05"), (Id: 37926L, CardNo: "VEN·T06") }
            .Select(identity => catalog.Cards.Single(card => card.Id == identity.Id && card.CardNo == identity.CardNo))
            // The upstream gallery labels VEN token printings as ordinary units.
            // CN 187.1 and 187.10 define their actual token category.
            .Select(card => card.CardNo.StartsWith("VEN·T", StringComparison.Ordinal)
                && card.CardCategory == "unit"
                ? card with { CardCategory = "indicator_unit", CardCategoryName = "指示物单位",
                    CardCategoryList = ["indicator_unit"], CardCategoryNameList = ["指示物单位"] }
                : card)
            .Select(card => card with {
                CardCategoryList = card.CardCategoryList.ToImmutableArray(),
                CardCategoryNameList = card.CardCategoryNameList.ToImmutableArray(),
                CardColorList = card.CardColorList.ToImmutableArray(),
                ProductCodeList = card.ProductCodeList.ToImmutableArray(),
                ProductNameList = card.ProductNameList.ToImmutableArray() })
            .ToImmutableArray();
    }
}
