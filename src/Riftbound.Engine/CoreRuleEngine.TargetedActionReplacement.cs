namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN VEN FAQ p13 / 423.1.a.1: an already-stunned object cannot
    // execute another stun action. Independent instructions still resolve.
    // Callers establish field/target legality before this action predicate.
    private static bool CanExecuteStun(CardObjectState target)
        => !target.UntilEndOfTurnEffects.Contains("STUNNED", StringComparer.Ordinal);
}
