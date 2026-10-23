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
            var payments = new Dictionary<string, (int Amount, string? Trait)>(StringComparer.Ordinal);
            var options = new List<RuleChoiceOption>();
            for (var amount = 0; amount <= pool.TotalPower; amount++)
            {
                var id = "SPEND_POWER:" + amount;
                payments.Add(id, (amount, null));
                options.Add(new(id, $"支付 {amount} 点任意特性符能，造成等量伤害"));
            }
            foreach (var (trait, available) in pool.PowerByTrait.OrderBy(p => p.Key, StringComparer.Ordinal))
            for (var amount = 1; amount <= available; amount++)
            {
                var id = $"SPEND_POWER:{trait}:{amount}";
                payments.Add(id, (amount, trait));
                options.Add(new(id, $"支付 {amount} 点{RuneTraitLabel(trait)}符能，造成等量伤害"));
            }
            foreach (var rune in zones[player].Base.Where(id => cards.TryGetValue(id, out var card)
                && card.Tags.Contains(CardObjectTags.RuneCard) && !card.IsFaceDown
                && SourceObjectControlledByPlayerOrLegacyOwned(card, player) && TryGetRuneTrait(card, out _))
                .Order(StringComparer.Ordinal))
            {
                if (!cards[rune].IsExhausted)
                    options.Add(new("TAP_RUNE:" + rune, "横置符文获得 1 法力，之后继续选择", [rune]));
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
            var traits = payment.Trait is null ? new Dictionary<string, int>(StringComparer.Ordinal)
                : new Dictionary<string, int>(StringComparer.Ordinal) { [payment.Trait] = payment.Amount };
            var plan = new PaymentCostRules.PaymentPlan(
                PaymentCostRules.BuildPaymentId(state.Tick, InstructionPowerPaymentKind, player, item.SourceObjectId),
                InstructionPowerPaymentKind, player, genericPowerCost: payment.Trait is null ? payment.Amount : 0,
                totalPowerCost: payment.Amount, powerCostByTrait: traits,
                legalPaymentChoiceIds: payments.Keys.ToArray(), reason: item.EffectKind, sourceObjectId: item.SourceObjectId);
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
