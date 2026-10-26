using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string ImposedMovementPaymentKind = "IMPOSED_MOVEMENT_POWER_PAYMENT";
    private enum ImposedMovementPaymentResult { Paid, Declined, Unfunded, Unsupported }

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
            // Existing funded standard intents remain a single direct action.
            if (canPay && standardMove && !acquired) { Commit(); return ImposedMovementPaymentResult.Paid; }

            var resources = ReplacementResourceActions(context, player)
                .Where(r => !standardMove || !r.Ability.ExhaustsSourceAsCost || !movers.Contains(r.Command.SourceObjectId, StringComparer.Ordinal))
                .ToArray();
            var options = new List<RuleChoiceOption>();
            if (canPay) options.Add(new("PAY", $"支付 {amount} 点符能，执行此次群体移动"));
            options.Add(new("DECLINE", "不支付，放弃此次群体移动"));
            foreach (var id in zones[player].Base.Order(StringComparer.Ordinal))
            {
                if (!cards.TryGetValue(id, out var rune) || rune.IsFaceDown || !rune.Tags.Contains(CardObjectTags.RuneCard)
                    || !SourceObjectControlledByPlayerOrLegacyOwned(rune, player) || !TryGetRuneTrait(rune, out _)
                    || locations.GetValueOrDefault(id) is not { Zone: "BASE" } position || position.PlayerId != player) continue;
                if (!rune.IsExhausted && CanGainSpellOnlyMana(pool, 1))
                    options.Add(new("TAP_RUNE:" + id, "横置符文获得 1 点法力，之后继续选择", [id]));
                if (pool.HasSupportedAmounts && (long)pool.TotalPower + 1 <= int.MaxValue)
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
            if (selected == "PAY") { Commit(); return ImposedMovementPaymentResult.Paid; }
            ResolutionResult gained;
            if (resources.FirstOrDefault(r => r.Choice.Id == selected) is { } resource)
                gained = ApplyReplacementResourceAction(context, zones, cards, pools, player, resource, events);
            else
            {
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
            }
            ReplaceMovementPaymentState(locations, gained.State.ObjectLocations);
            acquired = true;

            void Commit()
            {
                var paid = PaymentCostRules.TryCommitPayment(plan, pools);
                if (!paid.Accepted) throw new InvalidOperationException("Invalid imposed movement payment: " + paid.ErrorMessage);
                ReplaceMovementPaymentState(pools, paid.RunePools);
                events.Add(new("COST_PAID", "支付群体移动的施加费用",
                    PaymentCostRules.BuildCostPaidPayload(plan, pools, null, new Dictionary<string, object?> {
                        ["sourceObjectIds"] = movers, ["battlefieldObjectId"] = battlefield,
                        ["manaCost"] = 0, ["powerCost"] = amount })));
            }
        }
    }

    private static void ReplaceMovementPaymentState<T>(Dictionary<string, T> target, IReadOnlyDictionary<string, T> source)
    {
        if (ReferenceEquals(target, source)) return;
        target.Clear(); foreach (var pair in source) target[pair.Key] = pair.Value;
    }
}
