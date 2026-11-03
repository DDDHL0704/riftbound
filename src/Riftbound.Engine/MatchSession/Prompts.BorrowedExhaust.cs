using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static partial class ActionPromptBuilder
{
    private static IEnumerable<ActivateAbilityPromptRequirement> BorrowedExhaustRequirements(
        MatchState state, string player, CardObjectState source)
    {
        var pool = state.RunePools.GetValueOrDefault(player, RunePool.Empty);
        foreach (var ability in BorrowedExhaustRules.Abilities(state, player, source))
        {
            if (!CoreRuleEngine.CanActivateBorrowedExhaust(state, player, ability.Spec)) continue;
            var targets = state.CardObjects.Keys.Order(StringComparer.Ordinal)
                .Where(id => CoreRuleEngine.IsBorrowedExhaustTarget(state, player, id, ability.Spec))
                .Select(id => ObjectChoice(state, id, "技能目标")).ToArray();
            if (targets.Length < ability.Spec.Targets) continue;
            var choices = Enumerable.Range(0, ability.Spec.Targets).ToDictionary(i => i.ToString(),
                _ => (IReadOnlyList<ActionPromptChoiceDto>)targets);
            var effect = ability.Spec.Kind switch {
                "READY_EQUIPMENT" => $"让 {ability.Spec.Targets} 件装备活跃",
                "READY_UNIT" => "让一名单位活跃",
                "BOON_UNIT" => "给予一名单位增益",
                "KEYWORD_UNIT" => $"给予一名友方单位本回合内{ability.Spec.Keyword}",
                "DAMAGE_UNIT" => $"对一名单位造成 {ability.Spec.Damage} 点伤害",
                "STUN_ENEMY" => "眩晕一名敌方单位",
                _ => $"让一名单位在本回合内战力 {ability.Spec.Might:+0;-0}" };
            var taxes = targets.ToDictionary(t => t.Id, t => CoreRuleEngine.SpellshieldPowerCostForTarget(state, player, t.Id), StringComparer.Ordinal);
            var payments = ability.Spec.Power + ability.Spec.TypedPower.Values.Sum() > 0 || taxes.Values.Any(tax => tax > 0)
                ? RenektonAwareRecycleRuneChoices(state, player, "支付获得的技能费用").ToArray() : [];
            var byTrait = ActivateAbilityPaymentResourcePowerByTrait(state, player, payments);
            yield return new(source.ObjectId, AttachedRuleTextRules.Face(source)!, ability.Id, "获得的横置技能",
                $"{(ability.Spec.Swift ? "迅捷；" : "")}横置：{effect}",
                ability.Spec.Mana, ability.Spec.Power, ability.Spec.Experience, ability.Spec.TypedPower, ability.Spec.Targets, ability.Spec.Targets,
                "技能目标", choices, [], payments, ActivateAbilityPaymentResourcePowerByChoice(state, player, payments),
                pool.TotalPower, PlayCardAvailablePowerByTrait(pool, new Dictionary<string, int>()),
                new RunePaymentReachability(pool, byTrait).MaximumTotal, PlayCardAvailablePowerByTrait(pool, byTrait),
                [], true, false, true, null, SpellshieldTaxPowerByTargetObjectId: taxes);
        }
    }
}
