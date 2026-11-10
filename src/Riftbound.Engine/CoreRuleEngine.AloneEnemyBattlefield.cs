using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 740.2.a: an enemy is alone when no other unit friendly to it is
    // here. Our own units and ordinary equipment do not change that count.
    private static bool HasAloneEnemyAtBattlefield(MatchState state, string player, string battlefield)
        => BattlefieldLocalRules.Battlefield(state, battlefield) is not null
            && state.CardObjects.Values.Where(unit => IsVisibleFieldUnitObject(state.CardObjects, unit.ObjectId)
                && state.ObjectLocations.TryGetValue(unit.ObjectId, out var at) && at.Zone == "BATTLEFIELD"
                && at.BattlefieldObjectId == battlefield && state.PlayerZones.TryGetValue(at.PlayerId, out var zones)
                && zones.Battlefields.Contains(unit.ObjectId))
                .GroupBy(unit => EffectiveFieldControllerId(state.PlayerZones, unit.ObjectId, unit))
                .Any(group => group.Key is { } controller && state.Seats.ContainsKey(controller)
                    && controller != player && group.Count() == 1);

    private static bool FriendlyUnitPlayToAloneEnemyBattlefieldGranted(MatchState state, string player)
        => state.CardObjects.Values.Any(source => IsVisibleFieldUnitObject(state.CardObjects, source.ObjectId)
            && IsObjectOnField(state.PlayerZones, source.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source) == player
            && AttachedRuleTextRules.Instances(state, source).Any(instance =>
                CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var spec)
                && spec.FriendlyUnitsMayPlayToBattlefieldWithAloneEnemy));

    private static bool AloneEnemyBattlefieldPlayAllowed(MatchState state, string player,
        CardBehaviorDefinition? incoming, string destination)
        => (incoming is null || incoming.PlaysSourceToBaseAsUnit)
            && destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
            && HasAloneEnemyAtBattlefield(state, player, destination[12..])
            && (incoming?.AllowsPlayToBattlefieldWithAloneEnemy == true
                || FriendlyUnitPlayToAloneEnemyBattlefieldGranted(state, player));
}
