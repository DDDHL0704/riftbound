using System.Collections.Immutable;

namespace Riftbound.CardCatalog;

// CN tournament 601.2.a admits same-name prints of legal-set cards. Keep
// their physical identity and product metadata from the official snapshot.
public static class TournamentChampionReprintSources
{
    private static readonly Lazy<ImmutableArray<OfficialCard>> Cards = new(() =>
        new[] { (Id: 38003L, CardNo: "T1S·001/005") }
            .Select(identity => VendettaCardSources.GetOfficialCards().Single(card =>
                card.Id == identity.Id && card.CardNo == identity.CardNo))
            .Select(card => card with {
                CardCategoryList = card.CardCategoryList.ToImmutableArray(),
                CardCategoryNameList = card.CardCategoryNameList.ToImmutableArray(),
                CardColorList = card.CardColorList.ToImmutableArray(),
                ProductCodeList = card.ProductCodeList.ToImmutableArray(),
                ProductNameList = card.ProductNameList.ToImmutableArray()
            }).ToImmutableArray());

    public static IReadOnlyList<OfficialCard> GetAdmittedCards() => Cards.Value;
}
