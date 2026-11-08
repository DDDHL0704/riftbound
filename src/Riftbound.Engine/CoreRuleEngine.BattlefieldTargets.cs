namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN170.11.a: occupation is the presence of units, independent of the
    // battlefield card's physical owner or its retained controller metadata.
    internal static bool IsEnemyOccupiedBattlefield(MatchState state, string player, string field)
        => BattlefieldLocalRules.Battlefield(state, field) is not null
            && state.CardObjects.Values.Any(unit => IsFaceUpNonStandbyUnit(unit)
                && BattlefieldLocalRules.AtUnit(state, unit.ObjectId)?.ObjectId == field
                && EffectiveFieldControllerId(state.PlayerZones, unit.ObjectId, unit) is { } controller
                && controller != player && state.Seats.ContainsKey(controller));
}
