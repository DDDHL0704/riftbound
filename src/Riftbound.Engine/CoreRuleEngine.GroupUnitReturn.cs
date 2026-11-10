using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static IReadOnlyDictionary<string, RunePool> ReturnFieldUnitGroup(MatchState state, StackItemState item,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        IReadOnlyDictionary<string, RunePool> pools, List<GameEvent> events, IReadOnlyList<string> units)
    {
        // Eligibility was determined once, before any unit or continuous source
        // leaves. No spellshield or target selection applies to this group.
        foreach (var id in units)
        {
            var observesReturn = TryGetBattlefieldUnitReturnContext(zones, cards, id, out var controller);
            if (!TryReturnTargetToHand(zones, cards, id, out var owner, out var equipment)) continue;
            events.Add(BuildReturnedToHandEvent("群体指示让单位返回其所属手牌", item, id, owner, equipment));
            if (observesReturn && TryResolveBattlefieldUnitReturnedCallRuneTrigger(state, zones, cards, pools,
                controller, id, item.SourceObjectId, events, out var nextPools)) pools = nextPools;
        }
        return pools;
    }

    private static StackResolutionResult ResolveLowMightGroupReturn(MatchState state, StackItemState item, int maximum)
    {
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var units = GetFieldUnitObjectIds(zones, cards)
            .Where(id => ResolveCurrentFieldUnitPower(state, cards[id]) <= maximum).ToArray();
        var events = new List<GameEvent>();
        var pools = ReturnFieldUnitGroup(state, item, zones, cards, state.RunePools, events, units);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
