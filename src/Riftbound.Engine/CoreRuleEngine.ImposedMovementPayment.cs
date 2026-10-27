using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string ImposedMovementPaymentKind = "IMPOSED_MOVEMENT_POWER_PAYMENT";
    private enum ImposedMovementPaymentResult { Paid, Declined, Unfunded, Unsupported }
    private const string AllocateImposedMovementPowerChoice = "ALLOCATE_IMPOSED_MOVEMENT_POWER";
    private const string ConfirmImposedMovementPowerChoice = "CONFIRM_IMPOSED_MOVEMENT_POWER";
    private const string CancelImposedMovementPowerChoice = "CANCEL_IMPOSED_MOVEMENT_POWER";
    private sealed record ImposedMovementPowerAllocation(int Wildcard, IReadOnlyDictionary<string, int> PowerByTrait);

    // CN 204.4.b / 430.3: pause this movement instruction, not the ordinary
    // priority flow. Re-execution uses the existing authoritative choice journal.
    private static ImposedMovementPaymentResult PayImposedMovementCost(MatchState state, string player,
        string sourceId, string? battlefield, bool standardMove,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, ObjectLocationState> locations, Dictionary<string, RunePool> pools,
        Func<MatchState, string[]?> currentMovers, List<GameEvent> events)
    {
        var acquired = false;
        while (true)
        {
            var context = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations,
                RunePools = pools, PendingRuleChoice = null,
                PendingPayment = new("imposed-movement-resource", RuleChoiceWindow, player) };
            var movers = currentMovers(context);
            if (movers is null) return ImposedMovementPaymentResult.Declined;
            if (!StandardMovementCostRules.TryPowerCost(context, player, battlefield, movers.Length, out var amount))
                return ImposedMovementPaymentResult.Unsupported;
            if (amount == 0) return ImposedMovementPaymentResult.Paid;
            var pool = pools.GetValueOrDefault(player, RunePool.Empty);
            var plan = new PaymentCostRules.PaymentPlan(
                PaymentCostRules.BuildPaymentId(state.Tick, ImposedMovementPaymentKind, player, sourceId),
                ImposedMovementPaymentKind, player, genericPowerCost: amount, totalPowerCost: amount,
                reason: "MAGESEEKER_PATROL_MOVE_TAX_STATIC", sourceObjectId: sourceId);
            var canPay = PaymentCostRules.AuthorizePayment(plan, pool).Accepted;
            var multipleAllocations = canPay && amount < pool.TotalPower
                && pool.PowerByTrait.Values.Count(value => value > 0) + (pool.Power > 0 ? 1 : 0) > 1;
            // A uniquely funded standard intent still completes directly.
            if (canPay && standardMove && !acquired && !multipleAllocations) { Commit(plan); return ImposedMovementPaymentResult.Paid; }

            var resources = ReplacementResourceActions(context, player)
                .Where(r => !standardMove || r.Ability?.ExhaustsSourceAsCost != true
                    || r.Command is not ActivateAbilityCommand activated || !movers.Contains(activated.SourceObjectId, StringComparer.Ordinal))
                .ToArray();
            var options = new List<RuleChoiceOption>();
            if (canPay) options.Add(new("PAY", $"支付 {amount} 点符能，执行此次群体移动"));
            if (multipleAllocations)
                options.Add(new(AllocateImposedMovementPowerChoice, $"自行分配此次 {amount} 点符能的各特性数量"));
            options.Add(new("DECLINE", "不支付，放弃此次群体移动"));
            foreach (var id in zones[player].Base.Order(StringComparer.Ordinal))
            {
                if (!cards.TryGetValue(id, out var rune) || rune.IsFaceDown || !rune.Tags.Contains(CardObjectTags.RuneCard)
                    || !SourceObjectControlledByPlayerOrLegacyOwned(rune, player) || !TryGetRuneTrait(rune, out _)
                    || locations.GetValueOrDefault(id) is not { Zone: "BASE" } position || position.PlayerId != player) continue;
                if (!rune.IsExhausted && CanGainSpellOnlyMana(pool, 1))
                    options.Add(new("TAP_RUNE:" + id, "横置符文获得 1 点法力，之后继续选择", [id]));
                if (pool.HasSupportedAmounts && (long)pool.TotalStoredPower + 1 <= int.MaxValue)
                    options.Add(new("RECYCLE_RUNE:" + id, "回收符文获得 1 点符能，之后继续选择", [id]));
            }
            options.AddRange(resources.Select(r => r.Choice));
            if (!canPay && options.Count == 1 && standardMove && !acquired) return ImposedMovementPaymentResult.Unfunded;
            var selected = RuleChoices.Value!.Choose(player,
                $"此次同时移动需额外支付 {amount} 点符能；可先获得资源或放弃此次移动。",
                options, amount, ImposedMovementPaymentKind, context with { PendingPayment = null });
            if (selected == "DECLINE")
            {
                events.Add(new("MOVEMENT_COST_DECLINED", "未支付施加费用，此次群体移动不执行",
                    new Dictionary<string, object?> { ["playerId"] = player, ["sourceObjectId"] = sourceId,
                        ["sourceObjectIds"] = movers, ["battlefieldObjectId"] = battlefield, ["powerCost"] = amount }));
                return ImposedMovementPaymentResult.Declined;
            }
            if (selected == "PAY") { Commit(plan); return ImposedMovementPaymentResult.Paid; }
            if (selected == AllocateImposedMovementPowerChoice)
            {
                var allocation = ChooseImposedMovementPowerAllocation(context, player, movers[0], pool, amount);
                if (allocation is null) continue;
                var exactPlan = new PaymentCostRules.PaymentPlan(plan.PaymentId, plan.PaymentWindow, player,
                    genericPowerCost: allocation.Wildcard, totalPowerCost: amount, powerCostByTrait: allocation.PowerByTrait,
                    reason: plan.Reason, sourceObjectId: sourceId);
                Commit(exactPlan);
                return ImposedMovementPaymentResult.Paid;
            }
            ResolutionResult gained;
            if (resources.FirstOrDefault(r => r.Choice.Id == selected) is { } resource)
                gained = ApplyReplacementResourceAction(context, zones, cards, pools, player, resource, events);
            else
            {
                var capture = BeginRuleChoiceResourceStep(context, player, selected);
                var intent = new PlayerIntent("imposed-movement-resource", player,
                    selected.StartsWith("TAP_RUNE:", StringComparison.Ordinal) ? CommandTypes.TapRune : CommandTypes.RecycleRune);
                gained = intent.CommandType == CommandTypes.TapRune
                    ? ResolveTapRune(context, intent, new(selected[9..]))
                    : ResolveRecycleRune(context, intent, new(selected[13..]));
                if (!gained.Accepted) throw new InvalidOperationException("Invalid imposed movement resource: " + gained.ErrorMessage);
                ReplaceMovementPaymentState(zones, gained.State.PlayerZones);
                ReplaceMovementPaymentState(cards, gained.State.CardObjects);
                ReplaceMovementPaymentState(pools, gained.State.RunePools);
                events.AddRange(gained.Events);
                CompleteRuleChoiceResourceStep(capture, gained.State, gained.Events);
            }
            ReplaceMovementPaymentState(locations, gained.State.ObjectLocations);
            acquired = true;

            void Commit(PaymentCostRules.PaymentPlan selectedPlan)
            {
                var paid = PaymentCostRules.TryCommitPayment(selectedPlan, pools);
                if (!paid.Accepted) throw new InvalidOperationException("Invalid imposed movement payment: " + paid.ErrorMessage);
                ReplaceMovementPaymentState(pools, paid.RunePools);
                events.Add(new("COST_PAID", "支付群体移动的施加费用",
                    PaymentCostRules.BuildCostPaidPayload(selectedPlan, pools, null, new Dictionary<string, object?> {
                        ["sourceObjectIds"] = movers, ["battlefieldObjectId"] = battlefield,
                        ["manaCost"] = 0, ["powerCost"] = amount })));
            }
        }
    }

    private static ImposedMovementPowerAllocation? ChooseImposedMovementPowerAllocation(MatchState context,
        string player, string anchorId, RunePool pool, int amount)
    {
        var buckets = new List<(string Trait, int Available)>();
        if (pool.Power > 0) buckets.Add((string.Empty, pool.Power));
        buckets.AddRange(pool.PowerByTrait.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (pair.Key, pair.Value)));
        var remaining = amount;
        var laterCapacity = pool.TotalPower;
        var wildcard = 0;
        var typed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (trait, available) in buckets)
        {
            laterCapacity -= available;
            var lower = Math.Max(0, remaining - laterCapacity);
            var upper = Math.Min(available, remaining);
            var label = trait.Length == 0 ? "通用符能" : RuneTraitLabel(trait) + "符能";
            var selected = Quantity(trait.Length == 0 ? "ANY" : "TRAIT:" + trait, lower, upper,
                $"此次固定支付 {amount} 点符能：选择其中使用的{label}数量；确认前不扣除资源。");
            if (selected is null) return null;
            if (trait.Length == 0) wildcard = selected.Value;
            else if (selected.Value > 0) typed.Add(trait, selected.Value);
            remaining -= selected.Value;
        }
        if (remaining != 0) throw new InvalidOperationException("Incomplete imposed movement power allocation.");
        var parts = new List<string>();
        if (wildcard > 0) parts.Add($"通用符能 {wildcard}");
        parts.AddRange(typed.Select(pair => $"{RuneTraitLabel(pair.Key)}符能 {pair.Value}"));
        var confirmation = RuleChoices.Value!.Choose(player, $"确认支付 {amount} 点符能：{string.Join("、", parts)}。",
            [new(ConfirmImposedMovementPowerChoice, "确认分配并支付"),
             new(CancelImposedMovementPowerChoice, "返回支付菜单，不扣除符能")],
            powerCost: amount, kind: ImposedMovementPaymentKind, preview: context with { PendingPayment = null });
        return confirmation == CancelImposedMovementPowerChoice ? null : new(wildcard, typed);

        int? Quantity(string bucket, int minimum, int maximum, string reason)
        {
            // The moving unit is public; do not expose an instruction's possibly hidden physical source.
            var generation = context.CardObjects[anchorId].ObjectGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var purpose = "IMPOSED_MOVEMENT_POWER_ALLOCATION:" + amount + ":" + bucket;
            var authority = $"QUANTITY:{Uri.EscapeDataString(anchorId)}:{generation}:{ImposedMovementPaymentKind}:{Uri.EscapeDataString(purpose)}";
            while (minimum < maximum)
            {
                var midpoint = (int)((long)minimum + ((long)maximum - minimum) / 2);
                var left = $"{authority}:{minimum}:{maximum}:{minimum}:{midpoint}";
                var right = $"{authority}:{minimum}:{maximum}:{midpoint + 1}:{maximum}";
                var selected = RuleChoices.Value!.Choose(player, reason,
                    [new(left, $"{minimum}–{midpoint}", [anchorId]),
                     new(right, $"{midpoint + 1}–{maximum}", [anchorId]),
                     new(CancelImposedMovementPowerChoice, "返回支付菜单，不扣除符能")],
                    powerCost: amount, kind: ImposedMovementPaymentKind, preview: context with { PendingPayment = null });
                if (selected == CancelImposedMovementPowerChoice) return null;
                if (selected == left) maximum = midpoint;
                else minimum = midpoint + 1;
            }
            return minimum;
        }
    }

    private static void ReplaceMovementPaymentState<T>(Dictionary<string, T> target, IReadOnlyDictionary<string, T> source)
    {
        if (ReferenceEquals(target, source)) return;
        target.Clear(); foreach (var pair in source) target[pair.Key] = pair.Value;
    }
}
