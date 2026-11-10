namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool HasDistributedDamage(CardBehaviorDefinition behavior)
        => behavior.DamageDistributionAmount > 0 || behavior.DamageDistributionUsesFirstTargetMight;

    private static IEnumerable<string> DistributedDamageTargets(CardBehaviorDefinition behavior, IReadOnlyList<string> targets)
        => behavior.DamageDistributionUsesFirstTargetMight ? targets.Skip(1) : targets;

    private static int? DistributedUnitDamageMight(MatchState state, string player, IReadOnlyList<string> targets,
        StackItemState? item)
        => targets.FirstOrDefault() is { Length: > 0 } id && state.CardObjects.TryGetValue(id, out var source)
            && IsFaceUpNonStandbyUnit(source) && IsObjectOnField(state.PlayerZones, id)
            && EffectiveFieldControllerId(state.PlayerZones, id, source) == player
            && (item is null || StackObjectRules.ReferencesCurrentObject(state, item, id))
                ? Math.Max(0, ResolveCurrentFieldUnitPower(state, source)) : null;

    private static int DistributedSpellDamage(MatchState state, string player,
        CardBehaviorDefinition behavior, IReadOnlyList<string> targets, StackItemState? item = null)
        => behavior.DamageDistributionUsesFirstTargetMight ? DistributedUnitDamageMight(state, player, targets, item) ?? 0
            : checked(behavior.DamageDistributionAmount
            + (item is null ? PendingActivatedSpellDamageBonus(state, player) : ActivatedSpellDamageBonus(state, item))
            + DistributionBonuses(state, player, targets.Select(id => state.ObjectLocations.GetValueOrDefault(id))
                .FirstOrDefault(location => location is not null)).Sum(bonus => bonus.Amount));

    internal static bool HasValidDistributedSpellTargets(MatchState state, string player,
        CardBehaviorDefinition behavior, IReadOnlyList<string> targets, StackItemState? item = null)
        => !HasDistributedDamage(behavior)
            || DistributedDamageTargets(behavior, targets).Count() <= DistributedSpellDamage(state, player, behavior, targets, item);

    // CN 355.14: targets and their taxes are declared with the spell. Only
    // the positive integer allocation is chosen during its resolution.
    // Rule choices keep that allocation inside the ordinary resolution, so
    // source completion, repeats, cleanup, recovery and replay share one path.
    private static IReadOnlyDictionary<string, int> DistributedSpellAmounts(
        MatchState state, StackItemState item, CardBehaviorDefinition behavior)
    {
        var amounts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!HasDistributedDamage(behavior)
            || behavior.DamageDistributionUsesFirstTargetMight
                && DistributedUnitDamageMight(state, item.ControllerId, item.TargetObjectIds, item) is null) return amounts;
        var targets = DistributedDamageTargets(behavior, item.TargetObjectIds).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        if (targets.Count == 0) return amounts;
        var remaining = DistributedSpellDamage(state, item.ControllerId, behavior, item.TargetObjectIds, item);
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
