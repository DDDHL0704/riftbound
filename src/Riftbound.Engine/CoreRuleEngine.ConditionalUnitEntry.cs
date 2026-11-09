namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool FriendlyStartPhaseUnitDestroyedThisTurn(string controller, int turnNumber)
    {
        if (RuleChoices.Value is not { } frame) return false;
        var ledger = frame.ResourceDeathLedger ?? frame.Origin.DeathLedger;
        return frame.StartPhaseDestroyedUnitControllers.Contains((turnNumber, controller))
            || ledger.TurnNumber == turnNumber && ledger.StartPhaseUnitControllers?.Contains(controller) == true;
    }

    // A public unit's current controller supplies the condition in quotes,
    // actual payment, normal entry and effect-driven entry alike.
    private static bool OpponentControlsStunnedUnit(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string player)
        => cards.Values.Any(card => IsFaceUpNonStandbyUnit(card) && IsObjectOnField(zones, card.ObjectId)
            && EffectiveFieldControllerId(zones, card.ObjectId, card) is { Length: > 0 } controller
            && controller != player && card.UntilEndOfTurnEffects.Contains("STUNNED", StringComparer.Ordinal));
}
