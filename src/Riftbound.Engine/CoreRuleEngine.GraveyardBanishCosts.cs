using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static void PayGraveyardPermanentBanishCost(Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, string player, string sourceId, string abilityId,
        string costId, List<GameEvent> events, string message)
    {
        var own = zones[player];
        zones[player] = own with {
            Graveyard = own.Graveyard.Where(id => id != costId).ToArray(),
            Banished = own.Banished.Append(costId).ToArray()
        };
        ResetCardOutsidePlay(zones, cards, costId, cards[costId], player);
        events.Add(CaptureOwnedBanishObservers(new("CARD_BANISHED", message, new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = sourceId, ["targetObjectId"] = costId,
            ["fromZone"] = "GRAVEYARD", ["destinationZone"] = "BANISHED", ["isCost"] = true,
            ["abilityId"] = abilityId
        }), zones, cards, player, [costId]));
    }
}
