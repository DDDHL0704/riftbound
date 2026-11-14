using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private sealed record AbilityPaymentSelection(
        IReadOnlyList<string> Resources,
        IReadOnlyList<string> RecycledRunes,
        string? LegendResource,
        PlayPowerSpendSelectionRules.Selection? PowerSpend);

    // CN 445.2.c: resource reactions resolve inside the fee being paid. Keep
    // their declared order, and select the skill's power separately from any
    // resource skill's own fee (135.2.e.5.a/b).
    private static bool TryExtractSharedAbilityPayment(
        MatchState state, string playerId, P4ActivatedAbilityDefinition ability,
        IReadOnlyList<string> costs, out AbilityPaymentSelection payment)
    {
        payment = default!;
        if (!ability.UsesSharedPayment
            || ability.ManaCost == 0 && ability.PowerCost == 0
                && P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(ability).Values.All(amount => amount == 0)
                && NormalizeOptionalCosts(costs).Count > 0
            || !PlayPowerSpendSelectionRules.TryExtract(costs, out var withoutPower, out var powerSpend)
            || !TryExtractRenektonPlayPayment(NormalizeOptionalCosts(withoutPower),
                out var withoutLegend, out var legend)
            || !TryExtractAbilityRunePaymentResourceActions(state, playerId, withoutLegend,
                out var behavior, out var resources, out var runes)
            || behavior.Count != 0) return false;
        payment = new(OrderedRenektonPaymentResources(NormalizeOptionalCosts(withoutPower), resources, legend),
            runes, legend, powerSpend);
        return true;
    }

    private static bool TrySelectAbilityPowerDebitPlan(
        PaymentCostRules.PaymentPlan fee, RunePool pool,
        PlayPowerSpendSelectionRules.Selection? selection, out PaymentCostRules.PaymentPlan debit)
    {
        debit = fee;
        if (selection is null) return true;
        if (!PlayPowerSpendSelectionRules.CanPayWithSources(selection,
                PaymentCostRules.PowerPoolForPurpose(pool, fee.PowerPurpose), fee.GenericPowerCost,
                fee.PowerCostByTrait, pool, fee.PowerPurpose)) return false;
        debit = PlayPowerSpendSelectionRules.DebitPlan(fee, selection);
        return true;
    }

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
