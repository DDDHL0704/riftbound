namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN805: this is an optional cost on the unit being played. The grant is
    // evaluated before payment; its later departure cannot undo a paid cost.
    internal static CardBehaviorDefinition PlaySourceKeywordBehavior(
        MatchState state, string player, string source, CardBehaviorDefinition behavior)
    {
        behavior = FlowPlayBehavior(state, player, source, behavior);
        if (!behavior.PlaysSourceToBaseAsUnit
            || CardPermissionKeywordRules.HasSourceKeyword(behavior, CardPermissionKeywordNames.Haste)
            || !state.CardObjects.TryGetValue(source, out var card) || card.CardNo != behavior.CardNo
            || !state.PlayerZones.TryGetValue(player, out var zones) || zones.Hand.Contains(source, StringComparer.Ordinal)
            || !HasOutsideHandHasteGrant(state, player)) return behavior;

        var traits = PrintedPowerCostRules.ForCard(behavior.CardNo).Traits;
        return behavior with {
            SourceUnitTags = string.Join('|', ParseDelimitedValues(behavior.SourceUnitTags)
                .Append(CardPermissionKeywordNames.Haste).Distinct(StringComparer.Ordinal)),
            HasteReadyManaCost = 1, HasteReadyPowerCost = 1,
            HasteReadyPowerTrait = traits.Count == 1 ? traits[0] : "",
            GrantedHastePowerTraits = traits.Count > 1 ? traits : null
        };
    }

    private static bool HasOutsideHandHasteGrant(MatchState state, string player)
        => state.PlayerZones.TryGetValue(player, out var zones) && zones.Base.Concat(zones.Battlefields).Any(id =>
            IsPlayerControlledFieldUnitObject(state, player, id)
            && state.ObjectLocations.TryGetValue(id, out var location) && location.Zone is "BASE" or "BATTLEFIELD"
            && state.CardObjects.TryGetValue(id, out var unit) && unit.ControllerId == player
            && CardBehaviorRegistry.TryGetByCardNo(unit.CardNo ?? "", out var grant)
            && grant.GrantsHasteToFriendlyUnitsPlayedOutsideHand);
}
