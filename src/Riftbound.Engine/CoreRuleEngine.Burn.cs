using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 441/432: burned cards enter the graveyard before any subsequent
    // burnout recycles it. Taking a top card does not fire draw observers.
    private static DrawApplicationResult ApplyBurnToPlayer(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, IReadOnlyDictionary<string, int> scores, string player,
        int amount, long rng, StackItemState item, List<GameEvent> events)
    {
        var burnouts = 0;
        string? winner = null;
        for (var i = 0; i < amount && winner is null; i++)
        {
            var before = zones[player];
            var top = TakeTopCardWithBurnout(state with { CardObjects = cards, PlayerScores = scores, RngCursor = rng },
                player, before, burnouts);
            scores = top.PlayerScores; rng = top.RngCursor; winner = top.WinnerPlayerId;
            foreach (var burnout in top.Burnouts)
                events.Add(new("BURNOUT_APPLIED", "燃烧时执行燃尽", new Dictionary<string, object?> {
                    ["playerId"] = player, ["scoredPlayerId"] = burnout.ScoredPlayerId,
                    ["scoredPlayerScore"] = burnout.ScoredPlayerScore, ["burnoutIndex"] = ++burnouts }));
            zones[player] = before with { MainDeck = top.MainDeck, Graveyard = top.Graveyard.Concat(top.DrawnCards).ToArray() };
            // Record each nonfield crossing, including a card burned twice in
            // the same instruction. The final continuity pass preserves these generations.
            foreach (var recycled in before.Graveyard.Except(top.Graveyard, StringComparer.Ordinal)) Advance(recycled);
            foreach (var burned in top.DrawnCards) Advance(burned);
            if (top.DrawnCards.Count > 0)
                events.Add(new("CARDS_BURNED", "将主牌堆顶部卡牌放入废牌堆", new Dictionary<string, object?> {
                    ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId, ["cardIds"] = top.DrawnCards,
                    ["count"] = top.DrawnCards.Count, ["sourceZone"] = "MAIN_DECK", ["destinationZone"] = "GRAVEYARD" }));
        }
        if (winner is not null) events.Add(BuildScoreVictoryEvent(winner, EffectiveWinningScore(state)));
        return new(scores, winner, rng);

        void Advance(string id)
        {
            if (!cards.TryGetValue(id, out var card)) return;
            ResetCardOutsidePlay(zones, cards, id, card, player);
            cards[id] = cards[id] with { ObjectGeneration = checked(card.ObjectGeneration + 1) };
        }
    }
}
