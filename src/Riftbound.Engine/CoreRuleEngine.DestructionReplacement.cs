using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private const string SettRecallEffect = "SETT_BOON_UNIT_DESTROYED_RECALL_EXHAUSTED";

    // One instruction may destroy several units at once (CN 370.1, 373). Make all
    // replacement decisions before removing any of those units; never choose by ID.
    private sealed class DestructionReplacementScope(RuleChoiceFrame? frame, HashSet<string> previous, HashSet<string> previousDeaths, int? previousBatch) : IDisposable
    {
        public HashSet<string> Replaced { get; } = new(StringComparer.Ordinal);
        public List<GameEvent> Events { get; } = [];
        public void Dispose() { if (frame is not null) { frame.DestructionCandidates = previous; frame.CurrentDestructions = previousDeaths; frame.CurrentDestructionBatch = previousBatch; } }
    }

    private static DestructionReplacementScope PrepareDestructionReplacements(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, IReadOnlyList<string> candidates)
    {
        var frame = RuleChoices.Value;
        var previous = frame?.DestructionCandidates ?? [];
        var scope = new DestructionReplacementScope(frame, previous, frame?.CurrentDestructions ?? [], frame?.CurrentDestructionBatch);
        if (frame is null) throw new InvalidOperationException("Destruction requires a rule command context.");
        var remaining = candidates.Where(id => !previous.Contains(id)).Distinct().ToHashSet(StringComparer.Ordinal);
        frame.DestructionCandidates = previous.Concat(candidates).ToHashSet(StringComparer.Ordinal);
        frame.CurrentDestructions = candidates.ToHashSet(StringComparer.Ordinal);
        frame.CurrentDestructionBatch = frame.DestructionBatchSequence++;
        try
        {
            foreach (var player in zones.Keys.OrderBy(p => p == frame.Origin.TurnPlayerId ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal))
            {
                while (true)
                {
                    var options = new List<(RuleChoiceOption Choice, string Target, string Source, string Payment)>();
                    var sources = zones.SelectMany(z => z.Value.LegendZone.Select(id => (Id: id, ZonePlayer: z.Key)))
                        .Where(entry => cards.TryGetValue(entry.Id, out var source)
                        && !source.IsFaceDown && !source.IsExhausted && LegendCardHasIdentity(source.CardNo, SettLegendIdentityId)
                        && (string.IsNullOrWhiteSpace(source.ControllerId) ? string.IsNullOrWhiteSpace(source.OwnerId) ? entry.ZonePlayer : source.OwnerId : source.ControllerId) == player)
                        .Select(entry => entry.Id).Order(StringComparer.Ordinal).ToArray();
                    var eligibleTarget = false;
                    foreach (var target in remaining.Order(StringComparer.Ordinal))
                    {
                        if (!cards.TryGetValue(target, out var unit) || unit.IsFaceDown || !IsObjectOnField(zones, target)
                            || !unit.Tags.Contains(CardObjectTags.UnitCard) || !unit.Tags.Contains(CardObjectTags.Boon)
                            || EffectiveFieldControllerId(zones, target, unit) != player) continue;
                        eligibleTarget = sources.Length > 0;
                        foreach (var source in sources)
                        {
                            var pool = pools.GetValueOrDefault(player) ?? RunePool.Empty;
                            if (pool.Power > 0) Add("ANY", "支付 1 点通用符能");
                            foreach (var trait in pool.PowerByTrait.Where(e => e.Value > 0).OrderBy(e => e.Key, StringComparer.Ordinal))
                                Add("TRAIT:" + trait.Key, $"支付 1 点{RuneTraitLabel(trait.Key)}符能");
                            foreach (var rune in zones[player].Base.Where(id => cards.TryGetValue(id, out var c)
                                && c.Tags.Contains(CardObjectTags.RuneCard) && !c.IsFaceDown && SourceObjectControlledByPlayerOrLegacyOwned(c, player) && TryGetRuneTrait(c, out _)).Order(StringComparer.Ordinal))
                                Add("RUNE:" + rune, $"回收符文 {rune} 支付");
                            void Add(string payment, string label) => options.Add((new($"SETT:{target}:{source}:{payment}",
                                $"腕豪召回 {(CardBehaviorRegistry.TryGetByCardNo(unit.CardNo ?? "", out var behavior) ? behavior.DisplayName : unit.CardNo ?? target)}（{target}）：{label}，休眠腕豪并消耗增益", [target, source]), target, source, payment));
                        }
                    }
                    var context = ReplacementResourceContext(state, zones, cards, pools, player);
                    var resources = eligibleTarget ? ReplacementResourceActions(context, player) : [];
                    if (options.Count + resources.Count == 0) break;
                    var selected = frame.Choose(player, $"当前支付可用 {pools.GetValueOrDefault(player, RunePool.Empty).Mana} 法力、{pools.GetValueOrDefault(player, RunePool.Empty).TotalPower} 符能。可先发动资源技能。\n这些单位即将被摧毁。可支付 1 点任意符能并休眠腕豪，消耗一名单位的增益，改为休眠召回；也可不替换。",
                        options.Select(o => o.Choice).Concat(resources.Select(r => r.Choice)).Append(new("DECLINE", "不使用腕豪替换，继续摧毁结算")).ToArray(), powerCost: 1);
                    if (selected == "DECLINE") break;
                    if (resources.FirstOrDefault(r => r.Choice.Id == selected) is { } resource)
                    {
                        ApplyReplacementResourceAction(context, zones, cards, pools, player, resource, scope.Events);
                        continue;
                    }
                    var option = options.Single(o => o.Choice.Id == selected);
                    var unitState = cards[option.Target];
                    if (option.Payment.StartsWith("RUNE:", StringComparison.Ordinal))
                    {
                        var paid = ApplyRecycleRunePaymentResourceActions(pools, zones, cards, new(), player,
                            [option.Payment[5..]], scope.Events, RuleChoiceWindow);
                        foreach (var pair in paid) pools[pair.Key] = pair.Value;
                    }
                    var current = pools.GetValueOrDefault(player) ?? RunePool.Empty;
                    // Selecting a colored pool preserves the player's other colors; recycling
                    // spends that rune's newly generated power, even if another pool was available.
                    var traitToSpend = option.Payment.StartsWith("TRAIT:", StringComparison.Ordinal) ? option.Payment[6..]
                        : option.Payment.StartsWith("RUNE:", StringComparison.Ordinal) && TryGetRuneTrait(cards[option.Payment[5..]], out var t) ? t : null;
                    var paymentTraits = traitToSpend is null ? new Dictionary<string, int>() : new Dictionary<string, int> { [traitToSpend] = 1 };
                    var paidPower = PayPowerCost(current, traitToSpend is null ? 1 : 0, paymentTraits);
                    pools[player] = current with { Power = paidPower.AnyPower, PowerByTrait = paidPower.PowerByTrait };
                    foreach (var (owner, zone) in zones.ToArray())
                        zones[owner] = zone with { Base = RemoveFromZone(zone.Base, option.Target), Battlefields = RemoveFromZone(zone.Battlefields, option.Target) };
                    zones[player] = zones[player] with { Base = zones[player].Base.Append(option.Target).ToArray() };
                    cards[option.Target] = unitState with { Damage = 0, Power = unitState.Power - 1, IsExhausted = true,
                        IsAttacking = false, IsDefending = false, ControllerId = player,
                        Tags = unitState.Tags.Where(t => t != CardObjectTags.Boon).ToArray() };
                    cards[option.Source] = cards[option.Source] with { IsExhausted = true };
                    scope.Events.Add(new("COST_PAID", "支付腕豪替换费用：1 点任意符能", new Dictionary<string, object?> {
                        ["playerId"] = player, ["mana"] = 0, ["power"] = 1, ["reason"] = SettRecallEffect }));
                    scope.Events.Add(new("BOON_CONSUMED", "消耗增益", new Dictionary<string, object?> {
                        ["playerId"] = player, ["sourceObjectId"] = option.Source, ["targetObjectId"] = option.Target,
                        ["previousPower"] = unitState.Power, ["power"] = unitState.Power - 1 }));
                    scope.Events.Add(new("LEGEND_EXHAUSTED", "腕豪变为休眠状态", new Dictionary<string, object?> {
                        ["playerId"] = player, ["sourceObjectId"] = option.Source, ["reason"] = SettRecallEffect }));
                    scope.Events.Add(new("UNIT_RECALLED_TO_BASE", "腕豪将单位改为休眠召回", new Dictionary<string, object?> {
                        ["playerId"] = player, ["sourceObjectId"] = option.Source, ["targetObjectId"] = option.Target,
                        ["ownerPlayerId"] = unitState.OwnerId, ["controllerId"] = player, ["destinationZone"] = "BASE",
                        ["replacementEffectId"] = SettRecallEffect, ["isExhausted"] = true }));
                    scope.Replaced.Add(option.Target); remaining.Remove(option.Target); frame.CurrentDestructions.Remove(option.Target);
                }
            }
            return scope;
        }
        catch { scope.Dispose(); throw; }
    }

}
