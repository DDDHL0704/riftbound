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
            if (ability.Spec.Kind == "RESOURCE" && !CoreRuleEngine.BorrowedResourceGainSupported(pool, ability.Spec, source)) continue;
            var targets = ability.Spec.Targets == 0 ? [] : state.CardObjects.Keys.Order(StringComparer.Ordinal)
                .Where(id => CoreRuleEngine.IsBorrowedExhaustTarget(state, player, id, ability.Spec))
                .Select(id => ObjectChoice(state, id, "技能目标")).ToArray();
            if (targets.Length < ability.Spec.Targets) continue;
            var choices = Enumerable.Range(0, ability.Spec.Targets).ToDictionary(i => i.ToString(),
                _ => (IReadOnlyList<ActionPromptChoiceDto>)targets);
            var effect = ability.Spec.Kind switch {
                "READY_EQUIPMENT" => $"让 {ability.Spec.Targets} 件装备活跃",
                "READY_UNIT" => "让一名单位活跃",
                "BOON_UNIT" => "给予一名单位增益",
                "KEYWORD_UNIT" => $"给予一名{(ability.Spec.TargetScope == "FRIENDLY_UNIT" ? "友方" : "")}单位本回合内{ability.Spec.Keyword}",
                "DAMAGE_UNIT" => $"对一名单位造成 {ability.Spec.Damage} 点伤害",
                "STUN_ENEMY" => "眩晕一名敌方单位",
                "RESOURCE" => BorrowedResourceLabel(ability.Spec, source.IsEmpowered),
                _ => $"让一名单位在本回合内战力 {ability.Spec.Might:+0;-0}" };
            var taxes = targets.ToDictionary(t => t.Id, t => CoreRuleEngine.SpellshieldPowerCostForTarget(state, player, t.Id), StringComparer.Ordinal);
            var payments = ability.Spec.Power + ability.Spec.TypedPower.Values.Sum() > 0 || taxes.Values.Any(tax => tax > 0)
                ? RenektonAwareRecycleRuneChoices(state, player, "支付获得的技能费用").ToArray() : [];
            var byTrait = ActivateAbilityPaymentResourcePowerByTrait(state, player, payments);
            yield return new(source.ObjectId, AttachedRuleTextRules.Face(source)!, ability.Id, "获得的横置技能",
                $"{(ability.Spec.Reaction ? "反应；" : ability.Spec.Swift ? "迅捷；" : "")}{(ability.Spec.RemovesSourceEmpowerment ? "解除我的强化；" : "")}横置：{effect}",
                ability.Spec.Mana, ability.Spec.Power, ability.Spec.Experience, ability.Spec.TypedPower, ability.Spec.Targets, ability.Spec.Targets,
                "技能目标", choices, [], payments, ActivateAbilityPaymentResourcePowerByChoice(state, player, payments),
                pool.TotalPower, PlayCardAvailablePowerByTrait(pool, new Dictionary<string, int>()),
                new RunePaymentReachability(pool, byTrait).MaximumTotal, PlayCardAvailablePowerByTrait(pool, byTrait),
                [], true, false, true, null, SpellshieldTaxPowerByTargetObjectId: taxes);
        }
    }

    private static string BorrowedResourceLabel(Riftbound.CardCatalog.BorrowedExhaustSpec spec, bool empowered)
    {
        var gains = new List<string>();
        var mana = empowered ? spec.EmpoweredMana ?? spec.GeneratedMana : spec.GeneratedMana;
        if (mana > 0) gains.Add($"{mana} 点法力");
        if (spec.GeneratedPower > 0) gains.Add($"{spec.GeneratedPower} 点任意符能");
        gains.AddRange(spec.GeneratedTraits.Select(p => $"{p.Value} 点{RuneTraitLabel(p.Key)}符能"));
        var restriction = spec.ResourceRestriction switch {
            "SPELL" => "；仅可用于打出法术", "UNIT" => "；仅可用于打出单位或使用单位技能",
            "EQUIPMENT" => "；仅可用于打出装备或使用装备技能", "DUEL" => "；仅可在法术对决中消耗", _ => "" };
        return "获得费用资源：" + string.Join("和", gains) + restriction;
    }
}
