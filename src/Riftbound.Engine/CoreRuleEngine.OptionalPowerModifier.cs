namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static string OptionalPowerModifierCostId(OptionalPowerModifierSpec spec)
        => $"SPEND_POWER:{spec.Trait}:{spec.PowerCost}";

    private static int OptionalPowerModifierAmount(CardBehaviorDefinition behavior, StackItemState item)
        => behavior.OptionalPowerModifier is { } spec && item.OptionalCosts.Contains(OptionalPowerModifierCostId(spec))
            ? spec.Amount : 0;
}
