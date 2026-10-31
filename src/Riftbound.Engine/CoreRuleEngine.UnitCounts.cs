namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static int CountControlledExhaustedUnits(
        IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards,
        string controller)
        => GetControlledPublicFieldUnitObjectIds(zones, cards, controller)
            .Count(id => cards[id].IsExhausted);
}
