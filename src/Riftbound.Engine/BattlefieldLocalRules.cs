using Riftbound.Contracts;

namespace Riftbound.Engine;

// A battlefield's physical owner/zone is not its controller, nor an effect's scope.
// "Here" always follows the authoritative location of the affected unit.
internal static class BattlefieldLocalRules
{
    public static CardObjectState? Battlefield(MatchState state, string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !state.CardObjects.TryGetValue(id, out var card)
            || card.IsFaceDown || !card.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)
            || !state.PlayerZones.Values.Any(z => z.Battlefields.Contains(id, StringComparer.Ordinal))
            || (state.ObjectLocations.TryGetValue(id, out var location)
                && (location.Zone != "BATTLEFIELD" || location.BattlefieldObjectId != id))) return null;
        return card;
    }

    public static CardObjectState? AtUnit(MatchState state, string unitId)
    {
        if (!state.CardObjects.TryGetValue(unitId, out var unit) || unit.IsFaceDown
            || !unit.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            || unit.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)
            || !state.ObjectLocations.TryGetValue(unitId, out var location) || location.Zone != "BATTLEFIELD"
            || !state.PlayerZones.TryGetValue(location.PlayerId, out var zones)
            || !zones.Battlefields.Contains(unitId, StringComparer.Ordinal)) return null;
        return Battlefield(state, location.BattlefieldObjectId);
    }

    public static bool GrantsKeyword(MatchState state, string unitId, string keyword) =>
        AtUnit(state, unitId) is { } battlefield && StaticAuraSpecRules.GetStaticAuras(battlefield.CardNo)
            .Where(StaticAuraSpecRules.IsBattlefieldKeywordStaticAura)
            .Where(a => a.TargetScope == StaticAuraTargetScopes.SameBattlefieldUnits
                || (a.TargetScope == StaticAuraTargetScopes.SameBattlefieldFilteredUnits && StaticAuraSpecRules.TargetMatchesFilter(a, state.CardObjects[unitId])))
            .Any(a => a.GrantedKeyword is { Length: > 0 } granted && CardCombatKeywordRules.KeywordAmount([granted], keyword) > 0);

    public static bool HasAbility(CardObjectState? battlefield, Func<StaticAbilitySpec, bool> predicate) =>
        battlefield is not null && BattlefieldStaticAbilitySpecRules.TryGetAbility(battlefield.CardNo, predicate, out _);

    public static bool PreventsMoveToBase(MatchState state, string unitId) =>
        HasAbility(AtUnit(state, unitId), BattlefieldStaticAbilitySpecRules.IsBattlefieldPreventMoveToBaseAbility);

    public static bool PreventsUnitPlay(MatchState state, string destination) =>
        destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
        && HasAbility(Battlefield(state, destination[12..]), BattlefieldStaticAbilitySpecRules.IsBattlefieldPreventUnitPlayAbility);

    public static int WinningScoreIncrease(IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards) =>
        zones.Values.SelectMany(z => z.Battlefields).Distinct(StringComparer.Ordinal)
            .Select(id => cards.TryGetValue(id, out var card) && !card.IsFaceDown
                && card.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag, StringComparer.Ordinal)
                && BattlefieldStaticAbilitySpecRules.TryGetAbility(card.CardNo, BattlefieldStaticAbilitySpecRules.IsBattlefieldWinningScoreIncreaseAbility, out var ability)
                ? ability.Amount : 0).Sum();

    public static IEnumerable<CardObjectState> ControlledBy(MatchState state, string playerId) =>
        state.BattlefieldStates.Values.Where(b => b.ControllerId == playerId)
            .Select(b => Battlefield(state, b.BattlefieldObjectId)).OfType<CardObjectState>();
}
