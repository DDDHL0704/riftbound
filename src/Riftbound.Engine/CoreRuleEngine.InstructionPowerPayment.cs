using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 204.3.a / 740.4.a.1: choose and pay this instruction while resolving.
    // The synchronous journal keeps each Echo execution and resource decision
    // separate, without charging the play or introducing another continuation.
    internal const string InstructionPowerPaymentKind = "INSTRUCTION_POWER_PAYMENT";

    private static int ChooseInstructionPowerPayment(MatchState state, StackItemState item,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, List<GameEvent> events)
    {
        var player = item.ControllerId;
        while (true)
        {
            var context = ReplacementResourceContext(state, zones, cards, pools, player);
            var pool = pools.GetValueOrDefault(player) ?? RunePool.Empty;
            var payments = new Dictionary<string, (int Amount, string? Trait, int? Maximum)>(StringComparer.Ordinal);
            var options = new List<RuleChoiceOption>();
            AddAmounts(null, 0, pool.TotalPower);
            foreach (var (trait, available) in pool.PowerByTrait.OrderBy(p => p.Key, StringComparer.Ordinal))
                AddAmounts(trait, 1, available);
            void AddAmounts(string? trait, int minimum, int maximum)
            {
                if (maximum < minimum) return;
                var prefix = trait is null ? "SPEND_POWER:" : $"SPEND_POWER:{trait}:";
                var label = trait is null ? "任意特性" : RuneTraitLabel(trait);
                if ((long)maximum - minimum + 1 > ResourceQuantityRules.DirectChoiceLimit)
                {
                    if (trait is null) { payments.Add(prefix + "0", (0, null, null)); options.Add(new(prefix + "0", "支付 0 点符能，造成 0 点伤害")); }
                    var id = prefix + ResourceQuantityRules.ChooseAmountSuffix;
                    payments.Add(id, (minimum, trait, maximum)); options.Add(new(id, $"选择 {minimum}–{maximum} 点{label}符能"));
                }
                else for (var amount = minimum; amount <= maximum; amount++)
                {
                    var id = prefix + amount;
                    payments.Add(id, (amount, trait, null)); options.Add(new(id, $"支付 {amount} 点{label}符能，造成等量伤害"));
                }
            }
            foreach (var rune in zones[player].Base.Where(id => cards.TryGetValue(id, out var card)
                && card.Tags.Contains(CardObjectTags.RuneCard) && !card.IsFaceDown
                && SourceObjectControlledByPlayerOrLegacyOwned(card, player) && TryGetRuneTrait(card, out _))
                .Order(StringComparer.Ordinal))
            {
                if (!cards[rune].IsExhausted && CanGainSpellOnlyMana(pool, 1))
                    options.Add(new("TAP_RUNE:" + rune, "横置符文获得 1 法力，之后继续选择", [rune]));
                if (pool.HasSupportedAmounts && (long)pool.TotalPower + 1 <= int.MaxValue)
                    options.Add(new("RECYCLE_RUNE:" + rune, "回收符文获得 1 符能，之后继续选择", [rune]));
            }
            var resources = ReplacementResourceActions(context, player);
            options.AddRange(resources.Select(r => r.Choice));
            var selected = RuleChoices.Value!.Choose(player,
                "结算此指令：支付任意数量的符能，随后造成等量伤害；可支付 0 或先获得资源。",
                options, kind: InstructionPowerPaymentKind, preview: context with { PendingPayment = null });
            if (resources.FirstOrDefault(r => r.Choice.Id == selected) is { } resource)
            {
                ApplyReplacementResourceAction(context, zones, cards, pools, player, resource, events);
                continue;
            }
            if (selected.StartsWith("TAP_RUNE:", StringComparison.Ordinal))
            {
                var tapped = ResolveTapRune(context, new("instruction-power-resource", player, CommandTypes.TapRune),
                    new(selected[9..]));
                if (!tapped.Accepted) throw new InvalidOperationException("Invalid instruction rune resource: " + tapped.ErrorMessage);
                foreach (var pair in tapped.State.CardObjects) cards[pair.Key] = pair.Value;
                foreach (var pair in tapped.State.RunePools) pools[pair.Key] = pair.Value;
                events.AddRange(tapped.Events);
                continue;
            }
            if (selected.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal))
            {
                var generated = ApplyRecycleRunePaymentResourceActions(pools, zones, cards, new(), player,
                    [selected[13..]], events, InstructionPowerPaymentKind);
                foreach (var pair in generated) pools[pair.Key] = pair.Value;
                continue;
            }

            var payment = payments[selected];
            if (payment.Maximum is { } maximum)
            {
                var amount = ChooseBoundedQuantity(context, player, item.SourceObjectId, item.EffectKind,
                    payment.Trait is null ? "INSTRUCTION_POWER_ANY" : "INSTRUCTION_POWER_TRAIT:" + payment.Trait,
                    payment.Amount, maximum, InstructionPowerPaymentKind, "选择此次结算指令实际支付的符能数量，确认前不扣资源。");
                payment = (amount, payment.Trait, null);
                selected = payment.Trait is null ? "SPEND_POWER:" + amount : $"SPEND_POWER:{payment.Trait}:{amount}";
            }
            var traits = payment.Trait is null ? new Dictionary<string, int>(StringComparer.Ordinal)
                : new Dictionary<string, int>(StringComparer.Ordinal) { [payment.Trait] = payment.Amount };
            var plan = new PaymentCostRules.PaymentPlan(
                PaymentCostRules.BuildPaymentId(state.Tick, InstructionPowerPaymentKind, player, item.SourceObjectId),
                InstructionPowerPaymentKind, player, genericPowerCost: payment.Trait is null ? payment.Amount : 0,
                totalPowerCost: payment.Amount, powerCostByTrait: traits,
                legalPaymentChoiceIds: payments.Keys.Append(selected).Distinct(StringComparer.Ordinal).ToArray(), reason: item.EffectKind, sourceObjectId: item.SourceObjectId);
            var paid = PaymentCostRules.TryCommitPayment(plan, pools);
            if (!paid.Accepted) throw new InvalidOperationException("Invalid generated instruction payment: " + paid.ErrorMessage);
            foreach (var pair in paid.RunePools) pools[pair.Key] = pair.Value;
            events.Add(new("COST_PAID", $"支付结算指令中的 {payment.Amount} 点符能",
                PaymentCostRules.BuildCostPaidPayload(plan, pools, null, new Dictionary<string, object?> {
                    ["stackItemId"] = item.StackItemId, ["mana"] = 0, ["power"] = payment.Amount,
                    ["paymentChoiceIds"] = new[] { selected }, ["targetObjectIds"] = item.TargetObjectIds.ToArray(),
                    ["sourceGeneration"] = cards.GetValueOrDefault(item.SourceObjectId)?.ObjectGeneration })));
            return payment.Amount;
        }
    }
}
