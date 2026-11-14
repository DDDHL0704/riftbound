using System.Text;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string CombatDamageOrderPrefix = "BATTLE_DAMAGE_REPLACEMENT_ORDER:";
    private sealed record CombatDamageTargetOrder(ObjectBinding Target, IReadOnlyList<string> Effects);
    private sealed record CombatDamageOrder(string BattleId, string BattlefieldId, IReadOnlyList<CombatDamageTargetOrder> Targets);

    private static bool HasCompetingCombatDamageModifiers(CardObjectState target)
    {
        var prevention = TemporaryUnitEffectRules.PreventionBudgets(target).Length
            + (target.UntilEndOfTurnEffects.Contains(PreventNextDamageThisTurnEffectId) ? 1 : 0);
        return prevention > 1 || prevention > 0 && DamageDoublingApplications.Count(target) > 0;
    }

    private static string[] CombatDamageOrderTargets(MatchState state, BattleState battle)
    {
        var possible = battle.AttackerObjectIds.Concat(battle.DefenderObjectIds)
            .Where(id => state.CardObjects.TryGetValue(id, out var unit) && HasCompetingCombatDamageModifiers(unit)
                && !DamageImmunityRules.CannotTakeDamage(state, unit)).ToHashSet(StringComparer.Ordinal);
        if (possible.Count == 0) return [];
        var pool = BuildCombatDamagePool(state, battle);
        return (battle.AttackerObjectIds.Any(id => pool.GetValueOrDefault(id) > 0) ? battle.DefenderObjectIds : [])
            .Concat(battle.DefenderObjectIds.Any(id => pool.GetValueOrDefault(id) > 0) ? battle.AttackerObjectIds : [])
            .Where(possible.Contains).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    // CN 467.2.c.4.a/c.5 and 372: the recipient's controller fixes competing
    // replacements during allocation, before the allocator spends the raw pool.
    private static MatchState PrepareCombatDamageOrders(MatchState state, string battlefield,
        IReadOnlyList<string> attackers, IReadOnlyList<string> defenders)
    {
        var targets = new List<CombatDamageTargetOrder>();
        var battle = new BattleState(true, BattleLifecycleIds.BattleIdForBattlefield(battlefield), battlefield, attackers, defenders,
            attackers.Concat(defenders).Distinct(StringComparer.Ordinal).ToDictionary(id => id,
                id => EffectiveFieldControllerId(state.PlayerZones, id, state.CardObjects[id])!, StringComparer.Ordinal));
        foreach (var id in CombatDamageOrderTargets(state, battle))
        {
            if (!state.CardObjects.TryGetValue(id, out var target) || !HasCompetingCombatDamageModifiers(target)
                || DamageImmunityRules.CannotTakeDamage(state, target)) continue;
            var used = new HashSet<string>(StringComparer.Ordinal); var order = new List<string>();
            while (DamageReplacementOptions(target, used, false) is { Count: > 0 } options)
            {
                var selected = options[0].Id;
                if (options.Count > 1 && RuleChoices.Value is { } frame)
                    selected = frame.Choose(EffectiveFieldControllerId(state.PlayerZones, id, target)!,
                        "在分配战斗伤害前，选择此单位接下来生效的伤害替换效果。", options,
                        kind: "DAMAGE_REPLACEMENT_ORDER", preview: state);
                used.Add(selected); order.Add(selected);
            }
            targets.Add(new(new(id, target.ObjectGeneration), order));
        }
        var effects = state.UntilEndOfTurnEffects.Where(e => !e.StartsWith(CombatDamageOrderPrefix, StringComparison.Ordinal));
        if (targets.Count > 0)
            effects = effects.Append(EncodeCombatDamageOrder(new(BattleLifecycleIds.BattleIdForBattlefield(battlefield)
                ?? throw new InvalidOperationException("A combat replacement order requires a battlefield."), battlefield, targets)));
        return state with { UntilEndOfTurnEffects = effects.Order(StringComparer.Ordinal).ToArray() };
    }

    private static string EncodeCombatDamageOrder(CombatDamageOrder order)
        => CombatDamageOrderPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order)));

    private static CombatDamageOrder? ReadCombatDamageOrder(MatchState state)
    {
        var markers = state.UntilEndOfTurnEffects.Where(e => e.StartsWith(CombatDamageOrderPrefix, StringComparison.Ordinal)).ToArray();
        if (markers.Length != 1) return null;
        try {
            var order = JsonSerializer.Deserialize<CombatDamageOrder>(Convert.FromBase64String(markers[0][CombatDamageOrderPrefix.Length..]));
            return order is { Targets: not null } && order.Targets.All(t => t is { Target: not null, Effects: not null }
                && t.Effects.All(e => e is not null)) ? order : null;
        }
        catch (Exception error) when (error is JsonException or FormatException) { return null; }
    }

    internal static bool ValidCombatDamageOrder(MatchState state)
    {
        if (state.UntilEndOfTurnEffects is null || state.UntilEndOfTurnEffects.Any(e => e is null)) return false;
        var markers = state.UntilEndOfTurnEffects.Where(e => e.StartsWith(CombatDamageOrderPrefix, StringComparison.Ordinal)).ToArray();
        if (markers.Length == 0) return true;
        if (markers.Length != 1 || ReadCombatDamageOrder(state) is not { Targets: { } targets } order
            || !HasOpenBattleDamageAssignmentWindow(state) || order.BattleId != state.BattleState.BattleId
            || order.BattlefieldId != state.BattleState.BattlefieldObjectId || EncodeCombatDamageOrder(order) != markers[0]) return false;
        var expected = CombatDamageOrderTargets(state, state.BattleState);
        if (targets.Any(t => t?.Target is null || t.Effects is null)
            || !targets.Select(t => t.Target.ObjectId).SequenceEqual(expected)) return false;
        foreach (var target in targets)
        {
            var unit = state.CardObjects[target.Target.ObjectId];
            if (target.Target.Generation != unit.ObjectGeneration) return false;
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var effect in target.Effects)
                if (!DamageReplacementOptions(unit, used, false).Any(o => o.Id == effect) || !used.Add(effect)) return false;
            if (DamageReplacementOptions(unit, used, false).Count != 0) return false;
        }
        return true;
    }

    private static IReadOnlyList<string>? CombatDamageOrderFor(MatchState state, CardObjectState target)
        => ReadCombatDamageOrder(state)?.Targets.FirstOrDefault(t => t.Target.ObjectId == target.ObjectId
            && t.Target.Generation == target.ObjectGeneration)?.Effects;

    private static int OriginalCombatDamageNeeded(MatchState state, CardObjectState target, int markedDamageNeeded)
    {
        if (markedDamageNeeded == 0) return 0;
        var budgets = TemporaryUnitEffectRules.PreventionBudgets(target).ToDictionary(b => b.Id, StringComparer.Ordinal);
        var order = CombatDamageOrderFor(state, target);
        if (order is null)
        {
            // No competing choice is needed for pure doublers or pure finite
            // prevention. Uncommitted previews conservatively put prevention first.
            var used = new HashSet<string>(StringComparer.Ordinal); var defaults = new List<string>();
            defaults.AddRange(budgets.Keys); used.UnionWith(budgets.Keys);
            if (target.UntilEndOfTurnEffects.Contains(PreventNextDamageThisTurnEffectId)) { defaults.Add("NEXT"); used.Add("NEXT"); }
            while (DamageReplacementOptions(target, used, false) is { Count: > 0 } remaining)
            { defaults.Add(remaining[0].Id); used.Add(remaining[0].Id); }
            order = defaults;
        }
        long needed = markedDamageNeeded;
        foreach (var effect in order.Reverse())
        {
            if (effect == "DOUBLE" || effect.StartsWith("DOUBLE:", StringComparison.Ordinal)) needed = (needed + 1) / 2;
            else if (effect == "NEXT") return int.MaxValue;
            else if (budgets.TryGetValue(effect, out var budget)) needed += budget.Remaining;
        }
        return checked((int)needed);
    }

    // One side allocates one pool (467.2.a-c). Apply replacements once to the
    // recipient's pooled event, retaining each submitted source's ledger row.
    private static IReadOnlyList<DamageApplicationResult> ApplyPooledCombatDamage(MatchState state,
        Dictionary<string, CardObjectState> cards, IReadOnlyList<CombatDamageAssignmentDto> assignments, ISet<string> lethal)
    {
        var results = new DamageApplicationResult[assignments.Count];
        foreach (var group in assignments.Select((a, index) => (Assignment: a, Index: index)).GroupBy(e => e.Assignment.TargetObjectId))
        {
            var target = cards[group.Key]; var entries = group.ToArray();
            var total = entries.Sum(e => (long)e.Assignment.Damage);
            if (total > int.MaxValue) throw new DamageNumericLimitException("战斗伤害原始分配量");
            var controller = EffectiveFieldControllerId(state.PlayerZones, entries[0].Assignment.SourceObjectId,
                state.CardObjects[entries[0].Assignment.SourceObjectId])!;
            var applied = ApplyDamageToCardObject(state, cards, controller, group.Key, (int)total, lethal,
                fromUnit: true, replacementOrder: CombatDamageOrderFor(state, target));
            if (entries.Length == 1) { results[entries[0].Index] = applied; continue; }
            var raw = 0; var previousDamage = 0; var previousPrevented = 0;
            var previousSpent = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                raw = checked(raw + entry.Assignment.Damage);
                var prefix = applied.ReplacementOrder is null ? null
                    : ReplaceDamage(target, cards, raw, false, "", applied.ReplacementOrder);
                var damage = prefix?.Damage ?? 0;
                var prevented = prefix?.Prevented ?? raw;
                var spent = new List<DamagePreventionSpend>();
                foreach (var budget in prefix?.Spent ?? [])
                {
                    var delta = budget.Amount - previousSpent.GetValueOrDefault(budget.BudgetId);
                    if (delta > 0) spent.Add(new(budget.BudgetId, delta, budget.Remaining));
                    previousSpent[budget.BudgetId] = budget.Amount;
                }
                results[entry.Index] = applied with { DamageAmount = damage - previousDamage,
                    OriginalDamageAmount = entry.Assignment.Damage, Prevented = prevented > previousPrevented,
                    PreventedDamageAmount = prevented - previousPrevented, PreventionSpent = spent };
                previousDamage = damage; previousPrevented = prevented;
            }
        }
        return results;
    }
}
