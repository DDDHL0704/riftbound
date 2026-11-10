using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static bool HasOptionalExperiencePayment(CardBehaviorDefinition behavior)
        => behavior.OptionalExperienceCost > 0;

    private static bool OptionalExperiencePaymentSelected(CardBehaviorDefinition behavior, IReadOnlyList<string>? costs)
        => HasOptionalExperiencePayment(behavior) && NormalizeOptionalCosts(costs).Any(cost =>
            TryParseSpendExperienceOptionalCost(cost, out var amount) && amount == behavior.OptionalExperienceCost);

    // This reads the confirmed selection, rather than the player's later XP.
    // The ordinary payment planner still validates and pays the entire cost.
    private static CardBehaviorDefinition OptionalExperienceTargetDefinition(CardBehaviorDefinition behavior,
        IReadOnlyList<string>? costs)
        => behavior.RemovesTargetMightLimitIfExperiencePaid && OptionalExperiencePaymentSelected(behavior, costs)
            ? behavior with { MaxTargetPower = 0 } : behavior;
}
