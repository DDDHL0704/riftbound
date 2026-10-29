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
        return new[] { (Id: 37687L, CardNo: "VEN·002"), (Id: 37700L, CardNo: "VEN·015"), (Id: 37701L, CardNo: "VEN·016"),
            (Id: 37704L, CardNo: "VEN·019"), (Id: 37705L, CardNo: "VEN·019a"), (Id: 37707L, CardNo: "VEN·021"),
            (Id: 37708L, CardNo: "VEN·021a"), (Id: 37719L, CardNo: "VEN·031"), (Id: 37986L, CardNo: "VEN·034"),
            (Id: 37725L, CardNo: "VEN·037"), (Id: 37726L, CardNo: "VEN·038"), (Id: 37727L, CardNo: "VEN·038a"),
            (Id: 37728L, CardNo: "VEN·039"), (Id: 37729L, CardNo: "VEN·040"), (Id: 37730L, CardNo: "VEN·041"),
            (Id: 37736L, CardNo: "VEN·046"), (Id: 37737L, CardNo: "VEN·046a"), (Id: 37743L, CardNo: "VEN·052"),
            (Id: 37752L, CardNo: "VEN·061"), (Id: 37754L, CardNo: "VEN·063"), (Id: 37755L, CardNo: "VEN·063a"),
            (Id: 37937L, CardNo: "VEN·065"), (Id: 37758L, CardNo: "VEN·066"), (Id: 37760L, CardNo: "VEN·068"),
            (Id: 37761L, CardNo: "VEN·068a"), (Id: 37776L, CardNo: "VEN·082"), (Id: 37777L, CardNo: "VEN·083"),
            (Id: 37778L, CardNo: "VEN·084"), (Id: 37779L, CardNo: "VEN·084a"), (Id: 37780L, CardNo: "VEN·085"),
            (Id: 37783L, CardNo: "VEN·088"), (Id: 37784L, CardNo: "VEN·088a"), (Id: 37786L, CardNo: "VEN·090"),
            (Id: 37788L, CardNo: "VEN·092"), (Id: 37792L, CardNo: "VEN·095"), (Id: 37796L, CardNo: "VEN·099"),
            (Id: 37797L, CardNo: "VEN·100"), (Id: 37801L, CardNo: "VEN·104"), (Id: 37803L, CardNo: "VEN·106"),
            (Id: 37804L, CardNo: "VEN·107"), (Id: 37808L, CardNo: "VEN·110a"), (Id: 37809L, CardNo: "VEN·111"),
            (Id: 37814L, CardNo: "VEN·114"), (Id: 37815L, CardNo: "VEN·115"), (Id: 37816L, CardNo: "VEN·116"),
            (Id: 37826L, CardNo: "VEN·126"), (Id: 37827L, CardNo: "VEN·127"), (Id: 37831L, CardNo: "VEN·131"),
            (Id: 37834L, CardNo: "VEN·134"), (Id: 37835L, CardNo: "VEN·135"), (Id: 37836L, CardNo: "VEN·135a"),
            (Id: 37837L, CardNo: "VEN·136"), (Id: 37838L, CardNo: "VEN·136a"), (Id: 37842L, CardNo: "VEN·139"),
            (Id: 37846L, CardNo: "VEN·143"), (Id: 37848L, CardNo: "VEN·145"), (Id: 37850L, CardNo: "VEN·147"),
            (Id: 37852L, CardNo: "VEN·149"), (Id: 37853L, CardNo: "VEN·150"), (Id: 37854L, CardNo: "VEN·151"),
            (Id: 37856L, CardNo: "VEN·153"), (Id: 37858L, CardNo: "VEN·155"), (Id: 38299L, CardNo: "VEN·155a"),
            (Id: 37870L, CardNo: "VEN·167"), (Id: 37871L, CardNo: "VEN·168"), (Id: 37874L, CardNo: "VEN·171"), (Id: 37875L, CardNo: "VEN·172"), (Id: 37876L, CardNo: "VEN·173"),
            (Id: 37877L, CardNo: "VEN·174"), (Id: 37878L, CardNo: "VEN·175"), (Id: 37879L, CardNo: "VEN·176"),
            (Id: 37880L, CardNo: "VEN·177"), (Id: 37881L, CardNo: "VEN·178"), (Id: 37883L, CardNo: "VEN·180"),
            (Id: 37887L, CardNo: "VEN·184"), (Id: 37888L, CardNo: "VEN·185"), (Id: 37890L, CardNo: "VEN·187"), (Id: 37891L, CardNo: "VEN·188"),
            (Id: 37892L, CardNo: "VEN·189"), (Id: 37893L, CardNo: "VEN·189*"), (Id: 37896L, CardNo: "VEN·191"),
            (Id: 37897L, CardNo: "VEN·191*"), (Id: 37898L, CardNo: "VEN·192"), (Id: 37899L, CardNo: "VEN·192*"),
            (Id: 37900L, CardNo: "VEN·193"), (Id: 37901L, CardNo: "VEN·193*"), (Id: 37902L, CardNo: "VEN·194"),
            (Id: 37903L, CardNo: "VEN·194*"), (Id: 37942L, CardNo: "VEN·195"), (Id: 37905L, CardNo: "VEN·195*"),
            (Id: 37906L, CardNo: "VEN·196"), (Id: 37907L, CardNo: "VEN·196*"), (Id: 37908L, CardNo: "VEN·197"),
            (Id: 37909L, CardNo: "VEN·197*"), (Id: 37916L, CardNo: "VEN·SP1"), (Id: 37917L, CardNo: "VEN·SP2"), (Id: 37919L, CardNo: "VEN·SP4"), (Id: 37920L, CardNo: "VEN·SP5"),
            (Id: 37924L, CardNo: "VEN·T04"), (Id: 37926L, CardNo: "VEN·T06") }
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
