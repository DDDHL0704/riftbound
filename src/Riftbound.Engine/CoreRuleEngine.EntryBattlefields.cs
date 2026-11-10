using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN355.1 / 369.3: creating the missing battlefield and replacing entry
    // belong to the play task. Neither is a separate play trigger or movement.
    private static StackItemState ApplyEntryBattlefieldReplacement(MatchState state,
        CardBehaviorDefinition behavior, StackItemState item,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        ref IReadOnlyDictionary<string, ObjectLocationState>? locations, List<GameEvent> events)
    {
        if (item.SourceConfirmed || string.IsNullOrEmpty(behavior.EntryBattlefieldTokenCardNo)) return item;
        if (!P6TokenFactoryCatalog.TryGetByCardNo(behavior.EntryBattlefieldTokenCardNo, out var factory)
            || !factory.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag))
            throw new InvalidOperationException("Entry replacement requires a registered battlefield token.");
        var current = state with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations ?? state.ObjectLocations };
        if (cards.Values.Any(card => card.CardNo == factory.CardNo
            && BattlefieldLocalRules.Battlefield(current, card.ObjectId) is not null)) return item;

        // The battlefield outlives this public source incarnation. Keep its ID
        // opaque so returning the unit does not rewrite public field-history IDs.
        var identity = MatchStateHasher.HashValue(new { item.StackItemId, factory.CardNo });
        var id = NextTokenObjectId(zones, cards, "BATTLEFIELD-" + identity, 1);
        // CN190 has its own control rules: the physical creator is the owner;
        // normal contest/showdown cleanup establishes battlefield control.
        cards[id] = factory.CreateObject(id, item.ControllerId, item.ControllerId) with { ControllerId = null };
        zones[item.ControllerId] = zones[item.ControllerId] with {
            Battlefields = zones[item.ControllerId].Battlefields.Append(id).ToArray() };
        var nextLocations = (locations ?? state.ObjectLocations).ToDictionary(pair => pair.Key, pair => pair.Value);
        nextLocations[id] = new(item.ControllerId, "BATTLEFIELD", id);
        nextLocations[item.SourceObjectId] = new(item.ControllerId, "BATTLEFIELD", id);
        locations = nextLocations;
        events.Add(new("BATTLEFIELD_TOKEN_CREATED", "打出时添置缺少的战场指示物并替换单位进场位置",
            new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["sourcePlayId"] = item.StackItemId, ["sourceCardNo"] = item.CardNo,
                ["tokenObjectId"] = id, ["tokenCardNo"] = factory.CardNo,
                ["battlefieldObjectId"] = id, ["destinationZone"] = "BATTLEFIELD",
                ["requestedDestination"] = item.Destination }));
        return item with { Destination = "BATTLEFIELD:" + id };
    }
}
