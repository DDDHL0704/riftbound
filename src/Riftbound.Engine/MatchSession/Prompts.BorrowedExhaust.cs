using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static partial class ActionPromptBuilder
{
    private static IEnumerable<ActivateAbilityPromptRequirement> BorrowedExhaustRequirements(
        MatchState state, string player, CardObjectState source)
    {
        if (!CoreRuleEngine.CanActivateOrdinaryPowerAbility(state, player)) yield break;
        var pool = state.RunePools.GetValueOrDefault(player, RunePool.Empty);
        foreach (var ability in BorrowedExhaustRules.Abilities(state, player, source))
        {
            var targets = state.CardObjects.Keys.Order(StringComparer.Ordinal)
                .Where(id => CoreRuleEngine.IsBorrowedExhaustTarget(state, player, id, ability.Spec))
                .Select(id => ObjectChoice(state, id, "技能目标")).ToArray();
            if (targets.Length < ability.Spec.Targets) continue;
            var choices = Enumerable.Range(0, ability.Spec.Targets).ToDictionary(i => i.ToString(),
                _ => (IReadOnlyList<ActionPromptChoiceDto>)targets);
            var effect = ability.Spec.Kind switch {
                "READY_EQUIPMENT" => $"让 {ability.Spec.Targets} 件装备活跃",
                "STUN_ENEMY" => "眩晕一名敌方单位",
                _ => $"让一名单位在本回合内战力 {ability.Spec.Might:+0;-0}" };
            var taxes = targets.ToDictionary(t => t.Id, t => CoreRuleEngine.SpellshieldPowerCostForTarget(state, player, t.Id), StringComparer.Ordinal);
            yield return new(source.ObjectId, AttachedRuleTextRules.Face(source)!, ability.Id, "获得的横置技能",
                $"支付 {ability.Spec.Mana} 法力并横置：{effect}",
                ability.Spec.Mana, 0, 0, new Dictionary<string, int>(), ability.Spec.Targets, ability.Spec.Targets,
                "技能目标", choices, [], [], new Dictionary<string, IReadOnlyDictionary<string, object?>>(),
                pool.TotalPower, PlayCardAvailablePowerByTrait(pool, new Dictionary<string, int>()),
                pool.TotalPower, PlayCardAvailablePowerByTrait(pool, new Dictionary<string, int>()),
                [], true, false, true, null, SpellshieldTaxPowerByTargetObjectId: taxes);
        }
    }
}
