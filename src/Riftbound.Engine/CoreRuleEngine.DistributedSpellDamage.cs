namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static int DistributedSpellDamage(MatchState state, string player,
        CardBehaviorDefinition behavior, IReadOnlyList<string> targets)
        => checked(behavior.DamageDistributionAmount
            + DistributionBonuses(state, player, targets.Select(id => state.ObjectLocations.GetValueOrDefault(id))
                .FirstOrDefault(location => location is not null)).Sum(bonus => bonus.Amount));

    private static bool HasValidDistributedSpellTargets(MatchState state, string player,
        CardBehaviorDefinition behavior, IReadOnlyList<string> targets)
        => behavior.DamageDistributionAmount <= 0
            || targets.Count <= DistributedSpellDamage(state, player, behavior, targets);

    // CN 355.14: targets and their taxes are declared with the spell. Only
    // the positive integer allocation is chosen during its resolution.
    // Rule choices keep that allocation inside the ordinary resolution, so
    // source completion, repeats, cleanup, recovery and replay share one path.
    private static IReadOnlyDictionary<string, int> DistributedSpellAmounts(
        MatchState state, StackItemState item, CardBehaviorDefinition behavior)
    {
        var amounts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (behavior.DamageDistributionAmount <= 0) return amounts;
        var targets = item.TargetObjectIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        if (targets.Count == 0) return amounts;
        var remaining = DistributedSpellDamage(state, item.ControllerId, behavior, targets);
        while (targets.Count > remaining)
        {
            var removed = RuleChoices.Value!.Choose(item.ControllerId,
                "伤害量已减少，选择移除一个分摊目标。",
                targets.Select(id => new RuleChoiceOption(id, DeckChoiceLabel(state, id), [id])).ToArray(),
                kind: "DISTRIBUTED_DAMAGE_TARGETS");
            targets.Remove(removed);
        }
        for (var index = 0; index < targets.Count; index++)
        {
            var id = targets[index];
            var amount = ChooseBoundedQuantity(state, item.ControllerId, item.SourceObjectId,
                item.EffectKind, id, index == targets.Count - 1 ? remaining : 1,
                remaining - (targets.Count - index - 1), "DISTRIBUTED_DAMAGE_AMOUNT",
                $"为 {DeckChoiceLabel(state, id)} 分配伤害；剩余 {remaining} 点，每名目标至少 1 点。");
            amounts[id] = amount;
            remaining -= amount;
        }
        return amounts;
    }
}
