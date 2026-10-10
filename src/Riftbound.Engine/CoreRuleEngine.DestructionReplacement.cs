using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private const string SettRecallEffect = "SETT_BOON_UNIT_DESTROYED_RECALL_EXHAUSTED";
    private sealed record DestructionOption(RuleChoiceOption Choice, string Target, string Source, string Kind,
        string EffectKey, bool Optional = false, string Payment = "", int Mana = 0);

    // Replacement instructions happen before the unreplaced simultaneous deaths (CN 373).
    private sealed class DestructionReplacementScope(RuleChoiceFrame frame) : IDisposable
    {
        private readonly HashSet<string> previous = frame.DestructionCandidates;
        private readonly HashSet<string> previousDeaths = frame.CurrentDestructions;
        private readonly int? previousBatch = frame.CurrentDestructionBatch;
        public HashSet<string> Replaced { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, FieldRemovalResult> Outcomes { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Controllers { get; } = new(StringComparer.Ordinal);
        public List<GameEvent> Events { get; } = [];
        public void Dispose() {
            frame.DestructionCandidates = previous;
            frame.CurrentDestructions = previousDeaths;
            frame.CurrentDestructionBatch = previousBatch;
        }
    }

    private static DestructionReplacementScope PrepareDestructionReplacements(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, IReadOnlyList<string> candidates,
        StackItemState? cause = null, string? battlefieldId = null,
        IReadOnlyDictionary<string, ObjectLocationState>? locations = null, Func<string, string>? destructionReason = null)
    {
        var frame = RuleChoices.Value ?? throw new InvalidOperationException("Destruction requires a rule command context.");
        var scope = new DestructionReplacementScope(frame);
        var remaining = candidates.Where(id => IsObjectOnField(zones, id)).ToHashSet(StringComparer.Ordinal);
        frame.DestructionCandidates = frame.DestructionCandidates.Concat(candidates).ToHashSet(StringComparer.Ordinal);
        frame.CurrentDestructions = remaining.ToHashSet(StringComparer.Ordinal);
        frame.CurrentDestructionBatch = frame.DestructionBatchSequence++;
        try {
            foreach (var player in zones.Keys.OrderBy(p => p == state.TurnPlayerId ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal)) {
                var declinedOptional = false;
                while (true) {
                    remaining.RemoveWhere(id => !IsObjectOnField(zones, id));
                    var options = new List<DestructionOption>();
                    var canPayOptional = false;
                    foreach (var target in remaining.Order(StringComparer.Ordinal)) {
                        if (!cards.TryGetValue(target, out var unit) || EffectiveFieldControllerId(zones, target, unit) != player) continue;
                        var candidatesForTarget = DestructionOptions(state, zones, cards, pools, target, player,
                            cause, battlefieldId, locations ?? state.ObjectLocations, declinedOptional, out var optionalAvailable);
                        foreach (var candidate in candidatesForTarget)
                            if (!frame.AppliedDestructionReplacements.Contains(candidate.EffectKey)) options.Add(candidate);
                        canPayOptional |= optionalAvailable;
                    }
                    var resources = canPayOptional ? ReplacementResourceActions(ReplacementResourceContext(state, zones, cards, pools, player), player) : [];
                    if (options.Count + resources.Count == 0) break;
                    var optional = options.Any(o => o.Optional) || resources.Count > 0;
                    string selected;
                    if (options.Count == 1 && !optional) selected = options[0].Choice.Id;
                    else {
                        var choices = options.Select(o => o.Choice).Concat(resources.Select(r => r.Choice)).ToList();
                        if (optional) choices.Add(new("DECLINE", "不使用可选替换；仍需执行适用的强制替换"));
                        selected = frame.Choose(player,
                            $"当前可用 {pools.GetValueOrDefault(player, RunePool.Empty).Mana} 法力、{pools.GetValueOrDefault(player, RunePool.Empty).TotalPower} 符能。选择先执行的摧毁替换及对象；可选支付可先发动资源技能。", choices,
                            powerCost: options.Any(o => o.Kind == "SETT") ? 1 : 0);
                    }
                    if (selected == "DECLINE") { declinedOptional = true; continue; }
                    if (resources.FirstOrDefault(r => r.Choice.Id == selected) is { } resource) {
                        ApplyReplacementResourceAction(ReplacementResourceContext(state, zones, cards, pools, player), zones, cards, pools, player, resource, scope.Events);
                        continue;
                    }
                    var option = options.Single(o => o.Choice.Id == selected);
                    var unitBefore = cards[option.Target];
                    var previousChain = frame.AppliedDestructionReplacements;
                    frame.AppliedDestructionReplacements = new(previousChain, StringComparer.Ordinal) { option.EffectKey };
                    try {
                        ApplyDestructionOption(state, zones, cards, pools, player, option, scope.Events, cause, destructionReason?.Invoke(option.Target));
                    } finally { frame.AppliedDestructionReplacements = previousChain; }
                    scope.Replaced.Add(option.Target); remaining.Remove(option.Target); frame.CurrentDestructions.Remove(option.Target);
                    scope.Controllers[option.Target] = player;
                    scope.Outcomes[option.Target] = new(unitBefore.OwnerId ?? player, option.Kind == "BANISH" ? "BANISHED" : "BASE",
                        option.Kind == "BANISH", option.Kind != "BANISH", unitBefore.Tags.Contains(CardObjectTags.EquipmentCard), unitBefore.Tags.Contains(CardObjectTags.UnitCard), []);
                }
            }
            return scope;
        } catch { scope.Dispose(); throw; }
    }

    private static IReadOnlyList<DestructionOption> DestructionOptions(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools,
        string target, string player, StackItemState? cause, string? battlefieldId,
        IReadOnlyDictionary<string, ObjectLocationState> locations, bool declinedOptional, out bool optionalAvailable)
    {
        var unit = cards[target]; var pool = pools.GetValueOrDefault(player, RunePool.Empty);
        var options = new List<DestructionOption>(); optionalAvailable = false;
        var label = CardBehaviorRegistry.TryGetByCardNo(unit.CardNo ?? "", out var behavior) ? behavior.DisplayName : unit.CardNo ?? target;
        string Key(string kind, string source) => $"{kind}:{source}:{cards[source].ObjectGeneration}";
        void Add(string kind, string source, string text, bool optional = false, string payment = "", int mana = 0)
            => options.Add(new(new($"{kind}:{target}" + (source == target && kind is "BANISH" or "RECALL" ? "" : $":{source}")
                + (payment.Length > 0 ? ":" + payment : ""), $"{label}（{target}）：{text}", [target, source]),
                target, source, kind, Key(kind, source), optional, payment, mana));
        if (unit.UntilEndOfTurnEffects.Contains(RecallToBaseExhaustedIfDestroyedThisTurnEffectId))
            Add("RECALL", target, "应用本回合效果，移除伤害并休眠召回");
        if (unit.UntilEndOfTurnEffects.Contains(BanishIfDestroyedThisTurnEffectId))
            Add("BANISH", target, "应用本回合效果，改为放逐");
        if (!unit.Tags.Contains(CardObjectTags.UnitCard) || unit.IsFaceDown || unit.Tags.Contains(CardObjectTags.Standby)) return options;
        foreach (var source in zones[player].Base.Concat(zones[player].Battlefields).Distinct().Order(StringComparer.Ordinal)) {
            if (!cards.TryGetValue(source, out var gear) || gear.IsFaceDown || gear.Tags.Contains(CardObjectTags.Standby)
                || !gear.Tags.Contains(CardObjectTags.EquipmentCard) || EffectiveFieldControllerId(zones, source, gear) != player
                || !CardReplacementSpecRules.TryGetReplacement(gear.CardNo, CardReplacementSpecRules.IsFriendlyUnitDestroyedDestroySourceRecallExhaustedReplacement, out _)) continue;
            Add("GEAR", source, $"由中娅沙漏（{source}）替换，摧毁沙漏并休眠召回");
        }
        if (declinedOptional) return options;
        var field = ResolveDestroyedSourceBattlefieldObjectId(locations, target, battlefieldId);
        var inBattle = cause?.EffectKind == "DECLARE_BATTLE_COMBAT_DAMAGE" && field == battlefieldId
            || state.BattleState.IsActive && field == state.BattleState.BattlefieldObjectId;
        if (inBattle && field is not null && FindFieldObjectLocation(zones, target)?.Zone == "BATTLEFIELD"
            && TryGetBattlefieldCardObject(zones, cards, field, out var fieldId, out var fieldState)
            && TryGetFieldControllerId(zones, fieldId, out var fieldController)
            && SourceObjectControlledByPlayerOrLegacyOwned(fieldState, fieldController)
            && BattlefieldStaticAbilitySpecRules.TryGetAbility(fieldState.CardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldDestroyedInBattlePayRecallReplacementAbility, out var ability) && ability.Amount > 0
            && !RuleChoices.Value!.AppliedDestructionReplacements.Contains(Key("ALTAR", fieldId))) {
            optionalAvailable = true;
            if (pool.Mana >= ability.Amount) Add("ALTAR", fieldId, $"支付 {ability.Amount} 法力，鲜血祭坛休眠召回", true, mana: ability.Amount);
        }
        if (!unit.Tags.Contains(CardObjectTags.Boon)) return options;
        foreach (var source in zones.SelectMany(z => z.Value.LegendZone).Order(StringComparer.Ordinal)) {
            if (!cards.TryGetValue(source, out var legend) || legend.IsFaceDown || legend.IsExhausted
                || !LegendCardHasIdentity(legend.CardNo, SettLegendIdentityId)
                || !SourceObjectControlledByPlayerOrLegacyOwned(legend, player)) continue;
            optionalAvailable = true;
            if (pool.Power > 0) Add("SETT", source, "支付 1 通用符能，休眠腕豪并消耗增益召回", true, "ANY");
            foreach (var trait in pool.PowerByTrait.Where(e => e.Value > 0).OrderBy(e => e.Key, StringComparer.Ordinal))
                Add("SETT", source, $"支付 1 {RuneTraitLabel(trait.Key)}符能，休眠腕豪并消耗增益召回", true, "TRAIT:" + trait.Key);
            foreach (var rune in zones[player].Base.Where(id => cards.TryGetValue(id, out var c) && c.Tags.Contains(CardObjectTags.RuneCard)
                && !c.IsFaceDown && SourceObjectControlledByPlayerOrLegacyOwned(c, player) && TryGetRuneTrait(c, out _)).Order(StringComparer.Ordinal))
                Add("SETT", source, $"回收符文 {rune} 支付，休眠腕豪并消耗增益召回", true, "RUNE:" + rune);
        }
        return options;
    }

    private static void ApplyDestructionOption(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools, string player,
        DestructionOption option, List<GameEvent> events, StackItemState? cause, string? destructionReason)
    {
        if (option.Kind == "SETT") { ApplySettDestructionOption(zones, cards, pools, player, option, events); return; }
        var target = cards[option.Target];
        var stack = new StackItemState($"replacement-{state.Tick}-{option.Source}", player, option.Source,
            "DESTRUCTION_REPLACEMENT", cards[option.Source].CardNo ?? "", [], 0, 0, []);
        if (option.Kind == "GEAR") {
            // A replacement-generated destruction is a new event in the same replacement chain (CN 370.2).
            var nested = ResolveFieldDestructions(state, zones, cards, stack, new HashSet<string>(),
                state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(), pools, objectLocations: ReconcileObjectLocations(state.ObjectLocations, zones),
                explicitDestroyObjectIds: new HashSet<string> { option.Source });
            events.AddRange(nested.Events.Select(e => e.Kind is "UNIT_DESTROYED" or "EQUIPMENT_DESTROYED"
                && e.Payload.GetValueOrDefault("targetObjectId") as string == option.Source
                ? e with { Payload = new Dictionary<string, object?>(e.Payload) {
                    ["replacementTargetObjectId"] = option.Target, ["reason"] = FriendlyUnitDestroyedEquipmentRecallEffectId,
                    ["destroyReason"] = destructionReason } } : e));
            foreach (var pool in nested.RunePools) pools[pool.Key] = pool.Value;
            RuleChoices.Value!.NestedTriggers.AddRange(nested.TriggerQueue);
            RuleChoices.Value.NestedDestroyedOwners.UnionWith(nested.DestroyedUnitOwnerIds);
        }
        if (option.Kind == "ALTAR") {
            pools[player] = pools.GetValueOrDefault(player, RunePool.Empty) with { Mana = pools[player].Mana - option.Mana };
            events.Add(new("COST_PAID", "支付鲜血祭坛替换费用", new Dictionary<string, object?> {
                ["playerId"] = player, ["mana"] = option.Mana, ["power"] = 0, ["reason"] = BattlefieldDestroyedInBattleRecallEffectId }));
        }
        // Nested replacement instructions may already have removed this incarnation.
        if (!cards.TryGetValue(option.Target, out var current) || current.ObjectGeneration != target.ObjectGeneration
            || !IsObjectOnField(zones, option.Target)) return;
        if (option.Kind == "BANISH") {
            TryRemoveFieldTargetCore(zones, cards, option.Target, true, out var removal);
            events.AddRange(BuildFieldRemovalEvents("替换为放逐", cause ?? stack, option.Target, removal, destructionReason));
            return;
        }
        foreach (var (owner, zone) in zones.ToArray())
            zones[owner] = zone with { Base = RemoveFromZone(zone.Base, option.Target), Battlefields = RemoveFromZone(zone.Battlefields, option.Target) };
        zones[player] = zones[player] with { Base = zones[player].Base.Append(option.Target).ToArray() };
        cards[option.Target] = current with { Damage = 0, IsExhausted = true, IsAttacking = false, IsDefending = false, ControllerId = player,
            UntilEndOfTurnEffects = option.Kind == "RECALL" ? current.UntilEndOfTurnEffects.Where(e => e != RecallToBaseExhaustedIfDestroyedThisTurnEffectId).ToArray() : current.UntilEndOfTurnEffects };
        events.Add(new("UNIT_RECALLED_TO_BASE", "移除伤害并休眠召回", new Dictionary<string, object?> {
            ["sourceObjectId"] = option.Kind == "RECALL" ? cause?.SourceObjectId ?? option.Source : option.Source,
            ["targetObjectId"] = option.Target, ["ownerPlayerId"] = current.OwnerId ?? player,
            ["controllerId"] = player, ["destinationZone"] = "BASE", ["isExhausted"] = true, ["damage"] = 0,
            ["destroyReason"] = destructionReason,
            ["replacementEffectId"] = option.Kind == "GEAR" ? FriendlyUnitDestroyedEquipmentRecallEffectId
                : option.Kind == "ALTAR" ? BattlefieldDestroyedInBattleRecallEffectId : RecallToBaseExhaustedIfDestroyedThisTurnEffectId }));
    }

    private static void ApplySettDestructionOption(Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, string player, DestructionOption option, List<GameEvent> events)
    {
        var unitState = cards[option.Target];
        if (option.Payment.StartsWith("RUNE:", StringComparison.Ordinal))
        {
            var paid = ApplyRecycleRunePaymentResourceActions(pools, zones, cards, new(), player,
                [option.Payment[5..]], events, RuleChoiceWindow);
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
        events.Add(new("COST_PAID", "支付腕豪替换费用：1 点任意符能", new Dictionary<string, object?> {
            ["playerId"] = player, ["mana"] = 0, ["power"] = 1, ["reason"] = SettRecallEffect }));
        events.Add(new("BOON_CONSUMED", "消耗增益", new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = option.Source, ["targetObjectId"] = option.Target,
            ["previousPower"] = unitState.Power, ["power"] = unitState.Power - 1 }));
        events.Add(new("LEGEND_EXHAUSTED", "腕豪变为休眠状态", new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = option.Source, ["reason"] = SettRecallEffect }));
        events.Add(new("UNIT_RECALLED_TO_BASE", "腕豪将单位改为休眠召回", new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = option.Source, ["targetObjectId"] = option.Target,
            ["ownerPlayerId"] = unitState.OwnerId, ["controllerId"] = player, ["destinationZone"] = "BASE",
            ["replacementEffectId"] = SettRecallEffect, ["isExhausted"] = true }));
    }
}
