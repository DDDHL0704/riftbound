namespace Riftbound.Engine;

// A graveyard choice pays an activation cost. It is not a target and does
// not create a response window or a spellshield payment.
internal static class LegendGraveyardBanishCostRules
{
    internal const string Prefix = "BANISH_GRAVEYARD_CARD:";

    internal static string[] Choices(MatchState state, string player)
        => state.PlayerZones.TryGetValue(player, out var zones)
            ? zones.Graveyard.Where(id => IsEligible(state, player, id))
                .Order(StringComparer.Ordinal).Select(id => Prefix + id).ToArray() : [];

    internal static bool TryRead(MatchState state, string player, IReadOnlyList<string> costs, out string id)
    {
        id = costs.Count == 1 && costs[0].StartsWith(Prefix, StringComparison.Ordinal)
            ? costs[0][Prefix.Length..] : string.Empty;
        return id.Length > 0 && costs[0] == Prefix + id && IsEligible(state, player, id);
    }

    private static bool IsEligible(MatchState state, string player, string id)
        => state.PlayerZones.TryGetValue(player, out var zones) && zones.Graveyard.Contains(id, StringComparer.Ordinal)
            && state.CardObjects.TryGetValue(id, out var card) && card.ObjectId == id && !card.IsFaceDown
            && !TokenObjectRules.IsToken(card)
            && (card.Tags.Contains(CardObjectTags.UnitCard) || card.Tags.Contains(CardObjectTags.EquipmentCard))
            && state.ObjectLocations.TryGetValue(id, out var location) && location.Zone == "GRAVEYARD"
            && location.PlayerId == player;
}
