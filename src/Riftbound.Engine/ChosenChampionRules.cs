using Riftbound.CardCatalog;

namespace Riftbound.Engine;

internal static class ChosenChampionRules
{
    // CN 103.2.a.3: every hero unit with the chosen full name qualifies,
    // including another physical copy or alternate art. This is not an object ID.
    private static readonly Lazy<IReadOnlyDictionary<string, (string Name, string Subtitle)>> HeroNames = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Where(c => c.CardCategoryName == "英雄单位")
            .ToDictionary(c => OfficialCardSourceIdentityGroups.NormalizeCardNo(c.CardNo),
                c => (c.CardName, c.SubTitle ?? ""), StringComparer.Ordinal));

    internal static bool Matches(MatchState state, string player, CardObjectState card)
        => card.Tags.Contains(CardObjectTags.UnitCard) && state.PlayerDecklists.TryGetValue(player, out var deck)
            && HeroNames.Value.TryGetValue(OfficialCardSourceIdentityGroups.NormalizeCardNo(deck.ChampionCardNo), out var chosen)
            && HeroNames.Value.TryGetValue(OfficialCardSourceIdentityGroups.NormalizeCardNo(card.CardNo), out var current)
            && chosen == current;

    internal static bool CanReturnToChampionZone(MatchState state, string player)
        => state.PlayerZones[player].ChampionZone.Count == 0;
}
