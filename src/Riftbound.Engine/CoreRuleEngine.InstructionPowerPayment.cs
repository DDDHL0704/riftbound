using System.Globalization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 204.3.a / 740.4.a.1: choose and pay this instruction while resolving.
    // The synchronous journal keeps each Echo execution and resource decision
    // separate, without charging the play or introducing another continuation.
    internal const string InstructionPowerPaymentKind = "INSTRUCTION_POWER_PAYMENT";
    private const string AllocateInstructionPowerChoice = "SPEND_POWER:ALLOCATE";
    private const string ConfirmInstructionPowerAllocationChoice = "CONFIRM_INSTRUCTION_POWER_ALLOCATION";
    private const string CancelInstructionPowerAllocationChoice = "CANCEL_INSTRUCTION_POWER_ALLOCATION";
    private sealed record InstructionPowerAllocation(int Amount, int Wildcard, IReadOnlyDictionary<string, int> PowerByTrait);

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
            if (pool.HasSupportedAmounts && pool.PowerByTrait.Values.Count(value => value > 0) + (pool.Power > 0 ? 1 : 0) > 1)
                options.Add(new(AllocateInstructionPowerChoice, "自行分配此次支付的符能：选择总量及各特性的数量"));
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
                if (pool.HasSupportedAmounts && (long)pool.TotalStoredPower + 1 <= int.MaxValue)
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
                ApplyRuleChoiceTapRuneResource(context, zones, cards, pools, player, selected[9..], events);
                continue;
            }
            if (selected.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal))
            {
                ApplyRuleChoiceRecycleRuneResource(context, zones, cards, pools, player, selected[13..], events, InstructionPowerPaymentKind);
                continue;
            }

            var allocation = selected == AllocateInstructionPowerChoice
                ? ChooseInstructionPowerAllocation(context, player, item, pool) : null;
            if (selected == AllocateInstructionPowerChoice && allocation is null) continue;
            var payment = allocation is null ? payments[selected]
                : (Amount: allocation.Amount, Trait: (string?)null, Maximum: (int?)null);
            if (payment.Maximum is { } maximum)
            {
                var amount = ChooseBoundedQuantity(context, player, item.SourceObjectId, item.EffectKind,
                    payment.Trait is null ? "INSTRUCTION_POWER_ANY" : "INSTRUCTION_POWER_TRAIT:" + payment.Trait,
                    payment.Amount, maximum, InstructionPowerPaymentKind, "选择此次结算指令实际支付的符能数量，确认前不扣资源。");
                payment = (amount, payment.Trait, null);
                selected = payment.Trait is null ? "SPEND_POWER:" + amount : $"SPEND_POWER:{payment.Trait}:{amount}";
            }
            var traits = allocation?.PowerByTrait ?? (payment.Trait is null ? new Dictionary<string, int>(StringComparer.Ordinal)
                : new Dictionary<string, int>(StringComparer.Ordinal) { [payment.Trait] = payment.Amount });
            var plan = new PaymentCostRules.PaymentPlan(
                PaymentCostRules.BuildPaymentId(state.Tick, InstructionPowerPaymentKind, player, item.SourceObjectId),
                InstructionPowerPaymentKind, player, genericPowerCost: allocation?.Wildcard ?? (payment.Trait is null ? payment.Amount : 0),
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

    private static InstructionPowerAllocation? ChooseInstructionPowerAllocation(MatchState context,
        string player, StackItemState item, RunePool pool)
    {
        var amount = Quantity("TOTAL", 0, pool.TotalPower, 0, "选择此次支付的符能总量；确认分配前不扣除资源。");
        if (amount is null) return null;
        var buckets = new List<(string Trait, int Available)>();
        if (pool.Power > 0) buckets.Add((string.Empty, pool.Power));
        buckets.AddRange(pool.PowerByTrait.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (pair.Key, pair.Value)));
        var remaining = amount.Value;
        var laterCapacity = pool.TotalPower;
        var wildcard = 0;
        var typed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (trait, available) in buckets)
        {
            laterCapacity -= available;
            var lower = Math.Max(0, remaining - laterCapacity);
            var upper = Math.Min(available, remaining);
            var label = trait.Length == 0 ? "通用符能" : RuneTraitLabel(trait) + "符能";
            var selected = Quantity(trait.Length == 0 ? "ANY" : "TRAIT:" + trait, lower, upper, amount.Value,
                $"此次共支付 {amount.Value} 点符能：选择其中使用的{label}数量；确认分配前不扣除资源。");
            if (selected is null) return null;
            if (trait.Length == 0) wildcard = selected.Value;
            else if (selected.Value > 0) typed.Add(trait, selected.Value);
            remaining -= selected.Value;
        }
        if (remaining != 0) throw new InvalidOperationException("Incomplete instruction power allocation.");
        var allocation = new InstructionPowerAllocation(amount.Value, wildcard, typed);
        var parts = new List<string>();
        if (wildcard > 0) parts.Add($"通用符能 {wildcard}");
        parts.AddRange(typed.Select(pair => $"{RuneTraitLabel(pair.Key)}符能 {pair.Value}"));
        var detail = parts.Count == 0 ? "不消耗符能" : string.Join("、", parts);
        var confirmation = RuleChoices.Value!.Choose(player, $"确认支付 {amount.Value} 点符能：{detail}。",
            [new(ConfirmInstructionPowerAllocationChoice, "确认分配并支付"),
             new(CancelInstructionPowerAllocationChoice, "返回支付菜单，不扣除资源")],
            powerCost: amount.Value, kind: InstructionPowerPaymentKind, preview: context with { PendingPayment = null });
        return confirmation == CancelInstructionPowerAllocationChoice ? null : allocation;

        int? Quantity(string bucket, int minimum, int maximum, int total, string reason)
        {
            var generation = context.CardObjects.GetValueOrDefault(item.SourceObjectId)?.ObjectGeneration
                .ToString(CultureInfo.InvariantCulture) ?? "NONE";
            var purpose = "INSTRUCTION_POWER_ALLOCATION:" + bucket;
            var authority = $"QUANTITY:{Uri.EscapeDataString(item.SourceObjectId)}:{generation}:{Uri.EscapeDataString(item.EffectKind)}:{Uri.EscapeDataString(purpose)}";
            while (minimum < maximum)
            {
                var midpoint = (int)((long)minimum + ((long)maximum - minimum) / 2);
                var left = $"{authority}:{minimum}:{maximum}:{minimum}:{midpoint}";
                var right = $"{authority}:{minimum}:{maximum}:{midpoint + 1}:{maximum}";
                var selected = RuleChoices.Value!.Choose(player, reason,
                    [new(left, $"{minimum}–{midpoint}", [item.SourceObjectId]),
                     new(right, $"{midpoint + 1}–{maximum}", [item.SourceObjectId]),
                     new(CancelInstructionPowerAllocationChoice, "返回支付菜单，不扣除资源")],
                    powerCost: total, kind: InstructionPowerPaymentKind, preview: context with { PendingPayment = null });
                if (selected == CancelInstructionPowerAllocationChoice) return null;
                if (selected == left) maximum = midpoint;
                else minimum = midpoint + 1;
            }
            return minimum;
        }
    }
}
