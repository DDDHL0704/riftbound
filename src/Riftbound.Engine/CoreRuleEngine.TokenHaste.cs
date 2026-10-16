using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN805 / DOC14p19: this is the unborn unit's optional cost, independent
    // of its parent instruction. Each token has its own decision and payment.
    private static bool ChooseTokenHaste(MatchState state, string player, string parentSource,
        CardObjectState token, Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, List<GameEvent> events)
    {
        if (!HasOutsideHandHasteGrant(ReplacementResourceContext(state, zones, cards, pools, player), player)) return false;
        var traits = PrintedPowerCostRules.ForCard(token.CardNo ?? "").Traits;
        while (true)
        {
            var context = ReplacementResourceContext(state, zones, cards, pools, player);
            var pool = pools.GetValueOrDefault(player) ?? RunePool.Empty;
            var options = new List<RuleChoiceOption> { new("DECLINE", "不支付急速费用，按原状态进场") };
            if (pool.Mana >= 1)
            {
                if (pool.Power > 0 && traits.Count == 0) options.Add(new("PAY:ANY", "支付急速：1 法力和 1 点通用符能"));
                foreach (var trait in pool.PowerByTrait.Where(t => t.Value > 0 && (traits.Count == 0 || traits.Contains(t.Key)))
                    .OrderBy(t => t.Key, StringComparer.Ordinal))
                    options.Add(new("PAY:TRAIT:" + trait.Key, "支付急速：1 法力和 1 点" + RuneTraitLabel(trait.Key) + "符能"));
            }
            var runes = zones[player].Base.Where(id => cards.TryGetValue(id, out var card)
                && card.Tags.Contains(CardObjectTags.RuneCard) && !card.IsFaceDown
                && SourceObjectControlledByPlayerOrLegacyOwned(card, player) && TryGetRuneTrait(card, out _))
                .Order(StringComparer.Ordinal).ToArray();
            foreach (var rune in runes)
            {
                if (!cards[rune].IsExhausted) options.Add(new("TAP_RUNE:" + rune, "横置符文，获得 1 法力，之后继续选择", [rune]));
                options.Add(new("RECYCLE_RUNE:" + rune, "回收符文，获得 1 符能，之后继续选择", [rune]));
            }
            var resources = ReplacementResourceActions(context, player);
            options.AddRange(resources.Select(r => r.Choice));
            var selected = RuleChoices.Value!.Choose(player,
                $"即将打出{OfficialCardSourceIdentityGroups.DisplayName(token.CardNo ?? "")}：可额外支付 1 法力和 1 符能，以活跃状态进场。",
                options, powerCost: 1, kind: "TOKEN_HASTE_PAYMENT", preview: context with { PendingPayment = null }, manaCost: 1);
            if (selected == "DECLINE") return false;
            if (resources.FirstOrDefault(r => r.Choice.Id == selected) is { } resource)
            {
                ApplyReplacementResourceAction(context, zones, cards, pools, player, resource, events); continue;
            }
            if (selected.StartsWith("TAP_RUNE:", StringComparison.Ordinal))
            {
                var result = ResolveTapRune(context, new("token-haste-resource", player, CommandTypes.TapRune), new(selected[9..]));
                if (!result.Accepted) throw new InvalidOperationException("Invalid token Haste rune resource: " + result.ErrorMessage);
                foreach (var pair in result.State.CardObjects) cards[pair.Key] = pair.Value;
                foreach (var pair in result.State.RunePools) pools[pair.Key] = pair.Value;
                events.AddRange(result.Events); continue;
            }
            if (selected.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal))
            {
                var paid = ApplyRecycleRunePaymentResourceActions(pools, zones, cards, new(), player, [selected[13..]], events, RuleChoiceWindow);
                foreach (var pair in paid) pools[pair.Key] = pair.Value;
                continue;
            }
            var spentTraits = selected == "PAY:ANY" ? new Dictionary<string, int>()
                : new Dictionary<string, int> { [selected[10..]] = 1 };
            pool = pools[player];
            var power = PayPowerCost(pool, selected == "PAY:ANY" ? 1 : 0, spentTraits);
            pools[player] = pool with { Mana = pool.Mana - 1, Power = power.AnyPower, PowerByTrait = power.PowerByTrait };
            events.Add(new("COST_PAID", "支付指示物急速费用", new Dictionary<string, object?> {
                ["playerId"] = player, ["sourceObjectId"] = token.ObjectId, ["parentSourceObjectId"] = parentSource,
                ["mana"] = 1, ["power"] = 1, ["powerByTrait"] = spentTraits,
                ["optionalCosts"] = new[] { HasteOptionalCostNames.HasteReady }, ["reason"] = "TOKEN_HASTE_PAYMENT" }));
            return true;
        }
    }

    private static void AddTokenHastePayload(Dictionary<string, object?> payload, bool paid)
    {
        if (!paid) return;
        payload["hasteReadyOptionalCostPaid"] = true;
        payload["isExhausted"] = false;
    }
}
