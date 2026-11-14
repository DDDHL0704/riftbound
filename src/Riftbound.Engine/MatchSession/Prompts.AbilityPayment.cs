using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static partial class ActionPromptBuilder
{
    private static IReadOnlyList<ActionPromptChoiceDto> AbilityTapRunePaymentChoices(MatchState state, string playerId)
        => CoreRuleEngine.TapRunePaymentResourceActionIds(state, playerId).Select(id =>
        {
            var source = ObjectChoice(state, id[TapRunePaymentOptionalCostPrefix.Length..], "技能付款资源");
            return new ActionPromptChoiceDto(id, $"横置符文支付：{source.Label}", "获得 1 点法力并继续支付技能费用");
        }).ToArray();

    private static int AbilityAvailableManaWithRuneTaps(MatchState state, string playerId,
        RunePool pool, ManaPaymentPurpose purpose)
    {
        var available = PaymentCostRules.AvailableMana(pool, purpose, state);
        var gain = Math.Min(CoreRuleEngine.TapRunePaymentResourceActionIds(state, playerId).Count,
            Math.Max(0, int.MaxValue - pool.TotalStoredMana));
        return checked(available + gain);
    }

    private sealed record AbilityPaymentWallet(int Mana, RunePaymentReachability Power);

    private static IReadOnlyList<AbilityPaymentWallet> SharedAbilityPaymentWallets(
        MatchState state, string playerId, string sourceObjectId, P4ActivatedAbilityDefinition ability)
    {
        var manaPurpose = CoreRuleEngine.ManaPurposeForActivatedAbility(state, sourceObjectId);
        var powerPurpose = CoreRuleEngine.PowerPurposeForActivatedAbility(ability);
        var pool = state.RunePools.GetValueOrDefault(playerId, RunePool.Empty);
        var wallets = new List<AbilityPaymentWallet> {
            new(AbilityAvailableManaWithRuneTaps(state, playerId, pool, manaPurpose),
                new(PaymentCostRules.PowerPoolForPurpose(pool, powerPurpose),
                    ActivateAbilityPaymentResourcePowerByTrait(state, playerId, ability))) };
        if (ability.ManaCost == 0 && ability.PowerCost == 0
            && P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(ability).Values.All(amount => amount == 0)) return wallets;
        // Consider the legend even when ordinary mana is sufficient: paying
        // its own cost may free rune-pool capacity for a later recycle.
        foreach (var choice in PromptRenektonPaymentChoices(state, playerId))
        {
            var remaining = choice.RemainingPool with {
                UnitPlayOrUnitAbilityOnlyMana = choice.RemainingPool.UnitPlayOrUnitAbilityOnlyMana + 2 };
            wallets.Add(new(AbilityAvailableManaWithRuneTaps(state, playerId, remaining, manaPurpose),
                new(PaymentCostRules.PowerPoolForPurpose(remaining, powerPurpose),
                    choice.RemainingRunePower, choice.RemainingRuneCapacity)));
        }
        return wallets;
    }

    private static bool CanPaySharedAbilityFee(MatchState state, string playerId, string sourceObjectId,
        P4ActivatedAbilityDefinition ability, int genericPower, IReadOnlyDictionary<string, int> typedPower)
        => SharedAbilityPaymentWallets(state, playerId, sourceObjectId, ability)
            .Any(wallet => wallet.Mana >= ability.ManaCost && wallet.Power.CanPay(genericPower, typedPower));

    private static ActivateAbilityPromptRequirement WithAbilityRunePaymentResources(
        MatchState state, string playerId, ActivateAbilityPromptRequirement requirement)
    {
        var pool = state.RunePools.GetValueOrDefault(playerId, RunePool.Empty);
        var purpose = CoreRuleEngine.ManaPurposeForActivatedAbility(state, requirement.SourceObjectId);
        var availableMana = PaymentCostRules.AvailableMana(pool, purpose, state);
        var taps = requirement.ManaCost > availableMana ? AbilityTapRunePaymentChoices(state, playerId) : [];
        var taxed = requirement.SpellshieldTaxPowerByTargetObjectId?.Values.Any(amount => amount > 0) == true;
        var recycles = taxed ? RecycleRunePaymentResourceChoices(state, playerId, "支付目标的法盾费用") : [];
        var choices = requirement.PaymentResourceChoices.Concat(taps).Concat(recycles)
            .DistinctBy(choice => choice.Id, StringComparer.Ordinal).ToArray();
        var powerPool = P4ActivatedAbilityCatalog.TryGetByAbilityId(requirement.AbilityId, out var ability)
            ? PaymentCostRules.PowerPoolForPurpose(pool, CoreRuleEngine.PowerPurposeForActivatedAbility(ability))
            : PaymentCostRules.PowerPoolForPurpose(pool, PowerPaymentPurpose.Other);
        var powerByTrait = ActivateAbilityPaymentResourcePowerByTrait(state, playerId, choices);
        var result = requirement with {
            OptionalCostChoices = requirement.OptionalCostChoices.Concat(taps).Concat(recycles)
                .DistinctBy(choice => choice.Id, StringComparer.Ordinal).ToArray(),
            PaymentResourceChoices = choices,
            PaymentResourcePowerByChoice = ActivateAbilityPaymentResourcePowerByChoice(state, playerId, choices),
            AvailablePowerWithPaymentResources = new RunePaymentReachability(powerPool, powerByTrait).MaximumTotal,
            AvailablePowerByTraitWithPaymentResources = PlayCardAvailablePowerByTrait(powerPool, powerByTrait),
            AvailableMana = PaymentCostRules.AvailableMana(pool, purpose, state),
            AvailableManaWithPaymentResources = AbilityAvailableManaWithRuneTaps(state, playerId, pool, purpose)
        };
        if (ability is not { UsesSharedPayment: true }) return result;
        ability = CoreRuleEngine.BattlefieldAdjustedAbility(state, playerId, requirement.SourceObjectId, ability);
        var wallets = SharedAbilityPaymentWallets(state, playerId, requirement.SourceObjectId, ability);
        var payable = wallets.Where(wallet => wallet.Mana >= requirement.ManaCost).ToArray();
        return result with {
            AvailableManaWithPaymentResources = wallets.Max(wallet => wallet.Mana),
            AvailablePowerWithPaymentResources = payable.Select(wallet => wallet.Power.MaximumTotal).DefaultIfEmpty(0).Max(),
            AvailablePowerByTraitWithPaymentResources = payable.SelectMany(wallet => wallet.Power.TraitUpperBounds())
                .GroupBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Max(pair => pair.Value), StringComparer.Ordinal)
        };
    }

    private static LegendActionPromptRequirement WithLegendRunePaymentResources(
        MatchState state, string playerId, LegendActionPromptRequirement requirement)
    {
        var pool = state.RunePools.GetValueOrDefault(playerId, RunePool.Empty);
        var availableMana = PaymentCostRules.AvailableMana(pool, ManaPaymentPurpose.Other, state);
        var taps = requirement.ManaCost > availableMana ? AbilityTapRunePaymentChoices(state, playerId) : [];
        var recycles = requirement.PowerCost > 0
            || requirement.SpellshieldTaxPowerByTargetObjectId?.Values.Any(amount => amount > 0) == true
                ? RecycleRunePaymentResourceChoices(state, playerId, "支付传奇技能费用") : [];
        var choices = taps.Concat(recycles).ToArray();
        var powerPool = PaymentCostRules.PowerPoolForPurpose(pool, PowerPaymentPurpose.Other);
        return requirement with {
            PaymentResourceChoices = choices,
            PaymentResourcePowerByChoice = ActivateAbilityPaymentResourcePowerByChoice(state, choices),
            AvailablePower = powerPool.TotalPower,
            AvailablePowerWithPaymentResources = new RunePaymentReachability(powerPool,
                ActivateAbilityPaymentResourcePowerByTrait(state, playerId, choices)).MaximumTotal,
            AvailableMana = PaymentCostRules.AvailableMana(pool, ManaPaymentPurpose.Other, state),
            AvailableManaWithPaymentResources = AbilityAvailableManaWithRuneTaps(state, playerId, pool, ManaPaymentPurpose.Other)
        };
    }
}
