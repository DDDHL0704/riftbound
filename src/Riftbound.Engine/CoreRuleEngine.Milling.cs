using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // A burn moves the available prefix, and the new graveyard objects already
    // have their identities before a following play instruction captures them.
    private static string[] ApplyMillToPlayer(Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, string player, int count, string source, List<GameEvent> events)
    {
        var milled = zones[player].MainDeck.Take(count).ToArray();
        zones[player] = zones[player] with { MainDeck = zones[player].MainDeck.Skip(milled.Length).ToArray(),
            Graveyard = zones[player].Graveyard.Concat(milled).ToArray() };
        foreach (var id in milled) {
            var previous = cards[id];
            cards[id] = (PrintedCardFactory.TryRestoreOutsidePlay(previous, player, out var printed) ? printed : previous)
                with { ObjectGeneration = checked(previous.ObjectGeneration + 1) };
        }
        events.Add(new("CARDS_MILLED", "燃烧主牌堆顶的卡牌", new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = source, ["cardIds"] = milled,
            ["count"] = milled.Length, ["sourceZone"] = "MAIN_DECK", ["destinationZone"] = "GRAVEYARD" }));
        return milled;
    }
}
