namespace Riftbound.Engine;

/// <summary>Battlefield ownership never grants control (CN core rules 190.2, 439.4.b).</summary>
internal static class CardControlRules
{
    internal static bool IsControlledByPlayerOrLegacyOwned(CardObjectState card, string playerId)
    {
        if (!string.IsNullOrWhiteSpace(card.ControllerId))
            return string.Equals(card.ControllerId, playerId, StringComparison.Ordinal);

        if (card.Tags.Contains("CARD_TYPE:BATTLEFIELD", StringComparer.Ordinal))
            return false;

        return string.IsNullOrWhiteSpace(card.OwnerId)
            || string.Equals(card.OwnerId, playerId, StringComparison.Ordinal);
    }
}
