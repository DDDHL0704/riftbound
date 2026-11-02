using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 383.2, 417, 425, 432.1.c: reveal the existing tops together. An
    // empty deck contributes no card and revealing never causes burnout.
    private static StackResolutionResult ResolveBeginningDeckComparison(MatchState state, StackItemState item)
    {
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var revealed = state.Seats.Keys.OrderBy(player => player == state.TurnPlayerId ? 0 : 1)
            .ThenBy(player => player, StringComparer.Ordinal)
            .Select(player => (Player: player, Card: zones[player].MainDeck.FirstOrDefault()))
            .Where(top => top.Card is not null && cards.ContainsKey(top.Card))
            .Select(top => (top.Player, Card: top.Card!, Mana: cards[top.Card!].ManaCost)).ToArray();
        foreach (var top in revealed)
            events.Add(new("MAIN_DECK_CARDS_REVEALED", "展示每名玩家主牌堆顶部的一张牌", new Dictionary<string, object?> {
                ["playerId"] = top.Player, ["sourceObjectId"] = item.SourceObjectId,
                ["cardIds"] = new[] { top.Card }, ["count"] = 1 }));
        var bindings = revealed.ToDictionary(top => top.Card, top => cards[top.Card].ObjectGeneration, StringComparer.Ordinal);
        var remaining = revealed.ToDictionary(top => top.Player,
            top => ObserveDeckCards(state, zones, cards, item, top.Player, [top.Card], "REVEAL", 0, events,
                simultaneousReveals: bindings), StringComparer.Ordinal);
        var scores = state.PlayerScores;
        string? winner = null;
        var cursor = state.RngCursor;
        foreach (var top in revealed)
        {
            // Equal minima qualify. Draw only the card actually revealed; a
            // reveal replacement removing it cannot cause the next card to draw.
            if (top.Player == item.ControllerId && top.Mana == revealed.Min(card => card.Mana)
                && remaining[top.Player].Contains(top.Card)
                && zones[top.Player].MainDeck.FirstOrDefault() == top.Card)
            {
                var draw = ApplyDrawToPlayer(state, zones, cards, scores, top.Player, 1, cursor, events, deferNormalVictory: true);
                scores = draw.PlayerScores; winner = draw.WinnerPlayerId; cursor = draw.RngCursor;
            }
            else
            {
                var recycled = RecycleLookedMainDeckCards(state, zones, cards, top.Player, item.SourceObjectId,
                    remaining[top.Player], cursor);
                events.AddRange(recycled.Events); cursor = recycled.RngCursor;
            }
        }
        events.Add(new("TRIGGER_RESOLVED", "开始阶段牌顶费用比较完成", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
            ["sourceCardNo"] = item.CardNo, ["trigger"] = item.BeginningDraw!.Kind }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, PlayerScores = scores,
            WinnerPlayerId = winner, RngCursor = cursor, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}
