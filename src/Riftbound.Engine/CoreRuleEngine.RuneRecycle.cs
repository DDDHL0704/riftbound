using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 417.5.a: multiple runes go to their owner's deck bottom in an
    // order chosen by that owner. The ordinary command checkpoint persists
    // each decision before any part of this recycling instruction commits.
    private static void RecycleFieldRunes(MatchState state, string player, string source,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        var runes = PublicStaticAuraSourceObjectIds(zones).Where(id => cards.TryGetValue(id, out var card)
            && !card.IsFaceDown && IsRuneObject(id, card) && SourceObjectControlledByPlayerOrLegacyOwned(card, player)).ToArray();
        foreach (var group in runes.GroupBy(id => cards[id].OwnerId ?? player).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var remaining = group.Order(StringComparer.Ordinal).ToList(); var ordered = new List<string>();
            while (remaining.Count > 1)
            {
                var next = RuleChoices.Value!.Choose(group.Key, "选择下一枚放到符文牌堆底部的符文。",
                    remaining.Select(id => new RuleChoiceOption(id, "回收 " + DeckChoiceLabel(state, id), [id])).ToArray(),
                    kind: "RUNE_RECYCLE_ORDER", preview: state with { PlayerZones = zones, CardObjects = cards,
                        ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) });
                ordered.Add(next); remaining.Remove(next);
            }
            ordered.AddRange(remaining);
            var ids = ordered.ToHashSet(StringComparer.Ordinal);
            foreach (var seat in zones.Keys.ToArray())
                zones[seat] = zones[seat] with { Base = zones[seat].Base.Where(id => !ids.Contains(id)).ToArray(),
                    Battlefields = zones[seat].Battlefields.Where(id => !ids.Contains(id)).ToArray(),
                    LegendZone = zones[seat].LegendZone.Where(id => !ids.Contains(id)).ToArray() };
            zones[group.Key] = zones[group.Key] with { RuneDeck = zones[group.Key].RuneDeck.Concat(ordered).ToArray() };
            foreach (var id in ordered) ResetCardOutsidePlay(zones, cards, id, cards[id], group.Key);
            events.Add(CaptureRecycleObservers(new("CARDS_RECYCLED", "回收符文到拥有者的符文牌堆底部", new Dictionary<string, object?> {
                ["playerId"] = player, ["sourceObjectId"] = source, ["cardIds"] = ids.Order(StringComparer.Ordinal).ToArray(),
                ["count"] = ids.Count, ["sourceZone"] = "FIELD", ["destinationZone"] = "RUNE_DECK" }), zones, cards));
        }
    }

    private static StackResolutionResult ResolveEndRuneRecycleCall(MatchState state, StackItemState item)
    {
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var events = new List<GameEvent>();
        RecycleFieldRunes(state, item.ControllerId, item.SourceObjectId, zones, cards, events);
        var called = CallRunes(zones, cards, item.ControllerId, item.EndAbility!.RuneCount);
        // A recycled rune can immediately reenter during this same instruction.
        // Both zone transitions create a new incarnation (CN 129.4).
        foreach (var id in called.CalledRuneObjectIds)
            cards[id] = cards[id] with { ObjectGeneration = checked(cards[id].ObjectGeneration + 1) };
        if (called.CalledRuneObjectIds.Count > 0)
            events.Add(new("RUNES_CALLED", "召出休眠符文", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["count"] = called.CalledRuneObjectIds.Count, ["runeObjectIds"] = called.CalledRuneObjectIds }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
