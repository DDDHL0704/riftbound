namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal sealed record DamagePreventionSpend(string BudgetId, int Amount, int Remaining);
    private sealed record DamageReplacementResult(CardObjectState Target, int Damage, int Prevented, string EffectId,
        IReadOnlyList<DamagePreventionSpend> Spent);

    // CN 370.2 / 372: apply each replacement once to this damage event. The
    // affected unit's current controller orders competing replacements.
    private static DamageReplacementResult ReplaceDamage(CardObjectState target, Dictionary<string, CardObjectState> cards,
        int damage, bool preventAll, string preventionEffectId)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var prevented = 0; var effectIds = new List<string>(); var spent = new List<DamagePreventionSpend>();
        while (damage > 0)
        {
            var options = new List<RuleChoiceOption>();
            if (preventAll && !used.Contains("ALL")) options.Add(new("ALL", "抵挡本次全部伤害", [target.ObjectId]));
            if (target.UntilEndOfTurnEffects.Contains(PreventNextDamageThisTurnEffectId) && !used.Contains("NEXT"))
                options.Add(new("NEXT", "抵挡下一次伤害", [target.ObjectId]));
            if (target.UntilEndOfTurnEffects.Contains(DamageReceivedDoubledThisTurnEffectId) && !used.Contains("DOUBLE"))
                options.Add(new("DOUBLE", "将本次伤害翻倍", [target.ObjectId]));
            var budgets = TemporaryUnitEffectRules.PreventionBudgets(target);
            options.AddRange(budgets.Where(b => !used.Contains(b.Id)).Select(b =>
                new RuleChoiceOption(b.Id, $"抵挡伤害（剩余 {b.Remaining} 点）", [target.ObjectId])));
            if (options.Count == 0) break;
            var selected = options[0].Id;
            if (options.Count > 1 && RuleChoices.Value is { } frame)
            {
                var currentCards = new Dictionary<string, CardObjectState>(cards) { [target.ObjectId] = target };
                var preview = (frame.ResolutionState ?? frame.Origin) with { CardObjects = currentCards };
                var controller = EffectiveFieldControllerId(preview.PlayerZones, target.ObjectId, target)
                    ?? target.ControllerId ?? target.OwnerId ?? frame.Origin.TurnPlayerId;
                selected = frame.Choose(controller, "选择接下来生效的伤害替换效果。", options,
                    kind: "DAMAGE_REPLACEMENT_ORDER", preview: preview);
            }
            used.Add(selected);
            if (selected == "DOUBLE") { damage = checked(damage * 2); continue; }
            var amount = selected is "ALL" or "NEXT" ? damage : Math.Min(damage, budgets.Single(b => b.Id == selected).Remaining);
            if (selected == "NEXT") target = target with { UntilEndOfTurnEffects = target.UntilEndOfTurnEffects
                .Where(e => e != PreventNextDamageThisTurnEffectId).ToArray() };
            else if (selected != "ALL") {
                var budget = budgets.Single(b => b.Id == selected);
                target = TemporaryUnitEffectRules.SpendPrevention(target, budget, amount);
                spent.Add(new(budget.Id, amount, budget.Remaining - amount));
            }
            effectIds.Add(selected == "ALL" ? preventionEffectId : selected == "NEXT" ? PreventNextDamageThisTurnEffectId : TemporaryUnitEffectRules.DamagePreventionPrefix);
            prevented = checked(prevented + amount); damage -= amount;
        }
        return new(target, damage, prevented, string.Join("|", effectIds.Distinct(StringComparer.Ordinal)), spent);
    }
}
