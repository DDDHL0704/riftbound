using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private sealed record KeepCategory(string Kind, string Label, IReadOnlyList<string> Objects, bool Private = false);

    // These are resolution choices, not targets. Collect every category before
    // changing any zone; one multi-type permanent can be kept in either category
    // (CN 133.3), and a short category keeps everything (CN 055/359.3.e.11).
    private static RecycleResult ResolveKeepAndRecycle(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        StackItemState item, long rngCursor)
    {
        var events = new List<GameEvent>();
        for (var repeat = 0; repeat < item.EffectRepeatCount; repeat++)
        {
            var current = state with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), RngCursor = rngCursor };
            var kept = new HashSet<string>(StringComparer.Ordinal);
            var candidates = new Dictionary<string, string>(StringComparer.Ordinal);
            var players = new[] { state.TurnPlayerId }.Concat(PlayerIdsStartingAfter(state, state.TurnPlayerId))
                .Distinct(StringComparer.Ordinal).Where(zones.ContainsKey);
            foreach (var player in players)
                foreach (var category in KeepAndRecycleCategories(current, player))
                {
                    foreach (var id in category.Objects) candidates.TryAdd(id, player);
                    if (category.Objects.Count <= 2) { kept.UnionWith(category.Objects); continue; }
                    var remaining = category.Objects.ToList();
                    for (var pick = 0; pick < 2; pick++)
                    {
                        var options = remaining.Select((id, index) => new RuleChoiceOption(
                            category.Private ? "HAND:" + index : id, DeckChoiceLabel(current, id), [id])).ToArray();
                        var selected = RuleChoices.Value!.Choose(player, $"选择第 {pick + 1} 张要留在原位的{category.Label}。",
                            options, kind: "KEEP_TWO_" + category.Kind, preview: current);
                        var chosen = options.Single(option => option.Id == selected).ObjectIds![0];
                        kept.Add(chosen); remaining.Remove(chosen);
                    }
                }
            var recycled = RecycleCardsToOwnersDecks(current, zones, cards, item.SourceObjectId,
                candidates.Where(entry => !kept.Contains(entry.Key)).ToDictionary(), rngCursor);
            events.AddRange(recycled.Events); rngCursor = recycled.RngCursor;
        }
        return new(events, rngCursor);
    }

    private static IReadOnlyList<KeepCategory> KeepAndRecycleCategories(MatchState state, string player)
    {
        var field = state.PlayerZones.Values.SelectMany(zone => zone.Base.Concat(zone.Battlefields))
            .Distinct(StringComparer.Ordinal).Where(id => state.CardObjects.TryGetValue(id, out var card)
                && !card.IsFaceDown && EffectiveFieldControllerId(state.PlayerZones, id, card) == player)
            .Order(StringComparer.Ordinal).ToArray();
        return [
            new("UNIT", "单位", field.Where(id => IsFaceUpNonStandbyUnit(state.CardObjects[id])).ToArray()),
            new("EQUIPMENT", "装备", field.Where(id => CardObjectHasTag(state.CardObjects, id, CardObjectTags.EquipmentCard)).ToArray()),
            new("RUNE", "符文", field.Where(id => IsRuneObject(id, state.CardObjects[id])).ToArray()),
            new("HAND", "手牌", state.PlayerZones[player].Hand.Where(id =>
                IsPrivateZoneObjectControlledByPlayerOrLegacyOwned(state, player, id)).Order(StringComparer.Ordinal).ToArray(), true)
        ];
    }

    // A single recycling instruction can include objects controlled by several
    // players. Group destinations by owner, not physical field placement; main
    // cards are randomized together, whereas rune order belongs to their owner
    // (CN 056/417.1/417.5). This also shares ordinary departure/token cleanup.
    private static RecycleResult RecycleCardsToOwnersDecks(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        string source, IReadOnlyDictionary<string, string> recyclingPlayers, long rngCursor)
    {
        if (recyclingPlayers.Count == 0) return new([], rngCursor);
        var beforeZones = zones.ToDictionary(); var beforeCards = cards.ToDictionary();
        var batches = recyclingPlayers.Keys.Order(StringComparer.Ordinal).Where(cards.ContainsKey)
            .GroupBy(id => (Owner: NonFieldDestinationOwner(zones, cards[id], recyclingPlayers[id]),
                Rune: IsRuneObject(id, cards[id])))
            .OrderBy(group => group.Key.Owner, StringComparer.Ordinal).ThenBy(group => group.Key.Rune)
            .Select(group => (group.Key.Owner, group.Key.Rune, Ids: group.ToArray())).ToArray();
        var destinations = new List<(string Owner, bool Rune, string[] Ids)>();
        foreach (var batch in batches)
        {
            var ids = batch.Ids;
            if (batch.Rune)
            {
                var remaining = ids.ToList(); var ordered = new List<string>();
                while (remaining.Count > 1)
                {
                    var next = RuleChoices.Value!.Choose(batch.Owner, "选择下一枚放到符文牌堆底部的符文。",
                        remaining.Select(id => new RuleChoiceOption(id, DeckChoiceLabel(state, id), [id])).ToArray(),
                        kind: "RUNE_RECYCLE_ORDER", preview: state);
                    ordered.Add(next); remaining.Remove(next);
                }
                ordered.AddRange(remaining); ids = ordered.ToArray();
            }
            else
            {
                ids = RandomizeForMainDeckBottom(ids, state.Seed, rngCursor, source).ToArray();
                if (ids.Length > 1) rngCursor++;
            }
            destinations.Add((batch.Owner, batch.Rune, ids));
        }

        var allIds = destinations.SelectMany(batch => batch.Ids).ToHashSet(StringComparer.Ordinal);
        foreach (var id in allIds)
        {
            CaptureFieldDeparture(zones, cards, id);
            DetachEquipmentFromRemovedHost(cards, id);
        }
        foreach (var player in zones.Keys.ToArray())
            zones[player] = zones[player] with {
                Base = zones[player].Base.Where(id => !allIds.Contains(id)).ToArray(),
                Battlefields = zones[player].Battlefields.Where(id => !allIds.Contains(id)).ToArray(),
                Hand = zones[player].Hand.Where(id => !allIds.Contains(id)).ToArray() };
        foreach (var batch in destinations)
            zones[batch.Owner] = batch.Rune
                ? zones[batch.Owner] with { RuneDeck = zones[batch.Owner].RuneDeck.Concat(batch.Ids).ToArray() }
                : zones[batch.Owner] with { MainDeck = zones[batch.Owner].MainDeck.Concat(batch.Ids).ToArray() };

        var events = new List<GameEvent>();
        foreach (var batch in destinations)
            foreach (var actor in batch.Ids.GroupBy(id => recyclingPlayers[id]).OrderBy(group => group.Key, StringComparer.Ordinal))
                events.Add(CaptureRecycleObservers(new("CARDS_RECYCLED", "回收其余卡牌到所属牌堆底部",
                    new Dictionary<string, object?> {
                        ["sourceObjectId"] = source, ["playerId"] = actor.Key, ["ownerPlayerId"] = batch.Owner,
                        ["count"] = actor.Count(), ["destinationZone"] = batch.Rune ? "RUNE_DECK" : "MAIN_DECK"
                    }), zones, beforeCards, actor.ToArray(), beforeZones, beforeCards));
        foreach (var batch in destinations)
            foreach (var id in batch.Ids)
                ResetCardOutsidePlay(zones, cards, id, beforeCards[id], batch.Owner);
        return new(events, rngCursor);
    }
}
