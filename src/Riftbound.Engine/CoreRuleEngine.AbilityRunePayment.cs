using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool TryExtractAbilityRunePaymentResourceActions(
        MatchState state, string playerId, IReadOnlyList<string> choices,
        out IReadOnlyList<string> behaviorCosts, out IReadOnlyList<string> resources,
        out IReadOnlyList<string> recycledRunes)
    {
        behaviorCosts = [];
        resources = [];
        recycledRunes = [];
        if (!TryExtractTapRunePaymentResourceActions(state, playerId, choices,
                out var withoutTaps, out _, out _)
            || !TryExtractRecycleRunePaymentResourceActions(state, playerId, withoutTaps,
                out behaviorCosts, out _, out recycledRunes)) return false;
        resources = choices.Where(choice => IsTapRunePaymentResourceActionId(choice)
            || IsRecycleRunePaymentResourceActionId(choice)).ToArray();
        return true;
    }
}
