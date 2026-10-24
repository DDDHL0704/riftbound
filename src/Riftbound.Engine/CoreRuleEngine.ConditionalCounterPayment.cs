using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN DOC01 445.2.c / 740.4 / 820: each execution asks the current target
    // controller while resolving, using the existing synchronous choice journal.
    private static bool TargetSpellControllerPaysCounterCost(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, string targetId, StackItemState counter, int manaCost,
        List<GameEvent> events)
    {
        var target = state.StackItems.FirstOrDefault(s => s.StackItemId == targetId);
        if (target is null || !StackObjectRules.IsSpell(target) || !zones.ContainsKey(target.ControllerId)) return false;
        var player = target.ControllerId;
        while (true)
        {
            var context = ReplacementResourceContext(state, zones, cards, pools, player);
            var pool = pools.GetValueOrDefault(player) ?? RunePool.Empty;
            var options = new List<RuleChoiceOption> { new(DeclinePaymentChoiceId, "不支付，尝试无效化该法术") };
            var spend = BuildSpendManaPaymentChoiceId(manaCost);
            if (pool.Mana >= manaCost) options.Add(new(spend, $"支付 {manaCost} 法力，保留该法术"));
            foreach (var rune in zones[player].Base.Where(id => cards.TryGetValue(id, out var card)
                && card.Tags.Contains(CardObjectTags.RuneCard) && !card.IsFaceDown
                && SourceObjectControlledByPlayerOrLegacyOwned(card, player) && TryGetRuneTrait(card, out _))
                .Order(StringComparer.Ordinal))
            {
                if (!cards[rune].IsExhausted && CanGainSpellOnlyMana(pool, 1)) options.Add(new("TAP_RUNE:" + rune, "横置符文获得 1 法力，之后继续选择", [rune]));
                if (pool.HasSupportedAmounts && (long)pool.TotalPower + 1 <= int.MaxValue)
                    options.Add(new("RECYCLE_RUNE:" + rune, "回收符文获得 1 符能，之后继续选择", [rune]));
            }
            var resources = ReplacementResourceActions(context, player);
            options.AddRange(resources.Select(r => r.Choice));
            if (options.Count == 1) return false;
            var selected = RuleChoices.Value!.Choose(player,
                $"你可以支付 {manaCost} 法力，使该法术不被无效化。",
                options, kind: "COUNTER_TARGET_MANA_PAYMENT", preview: context with { PendingPayment = null }, manaCost: manaCost);
            if (selected == DeclinePaymentChoiceId) return false;
            if (resources.FirstOrDefault(r => r.Choice.Id == selected) is { } resource)
            {
                ApplyReplacementResourceAction(context, zones, cards, pools, player, resource, events);
                continue;
            }
            if (selected.StartsWith("TAP_RUNE:", StringComparison.Ordinal))
            {
                var result = ResolveTapRune(context, new("conditional-counter-resource", player, CommandTypes.TapRune), new(selected[9..]));
                if (!result.Accepted) throw new InvalidOperationException("Invalid conditional counter rune resource: " + result.ErrorMessage);
                foreach (var pair in result.State.CardObjects) cards[pair.Key] = pair.Value;
                foreach (var pair in result.State.RunePools) pools[pair.Key] = pair.Value;
                events.AddRange(result.Events);
                continue;
            }
            if (selected.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal))
            {
                var paid = ApplyRecycleRunePaymentResourceActions(pools, zones, cards, new(), player, [selected[13..]], events, RuleChoiceWindow);
                foreach (var pair in paid) pools[pair.Key] = pair.Value;
                continue;
            }
            pools[player] = pool with { Mana = pool.Mana - manaCost };
            events.Add(new("COST_PAID", $"支付 {manaCost} 法力，保留法术", new Dictionary<string, object?> {
                ["playerId"] = player, ["mana"] = manaCost, ["power"] = 0,
                ["sourceObjectId"] = counter.SourceObjectId, ["targetStackItemId"] = targetId,
                ["reason"] = "COUNTER_TARGET_MANA_PAYMENT" }));
            return true;
        }
    }
}
