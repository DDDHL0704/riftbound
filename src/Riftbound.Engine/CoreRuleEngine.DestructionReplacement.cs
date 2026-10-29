using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record BanishReplacementOrigin(string ControllerId, string SourceObjectId, string CardNo, long SourceGeneration, long AppliedTick, string StackItemId);

public sealed partial class CoreRuleEngine
{
    internal static bool ValidBanishReplacementOrigins(MatchState state, CardObjectState target)
        => target.BanishReplacementOrigins is not { } origins || origins.Count > 0
            && target.UntilEndOfTurnEffects.Contains(BanishIfDestroyedThisTurnEffectId, StringComparer.Ordinal)
            && IsObjectOnField(state.PlayerZones, target.ObjectId)
            && origins.All(origin => origin is not null && state.Seats.ContainsKey(origin.ControllerId)
                && !string.IsNullOrWhiteSpace(origin.SourceObjectId) && !string.IsNullOrWhiteSpace(origin.CardNo)
                && origin.SourceGeneration >= 0 && origin.AppliedTick >= 0 && origin.AppliedTick <= state.Tick
                && !string.IsNullOrWhiteSpace(origin.StackItemId)
                && state.CardObjects.TryGetValue(origin.SourceObjectId, out var source)
                && source.ObjectGeneration >= origin.SourceGeneration
                && (state.StackItems.Any(item => item.StackItemId == origin.StackItemId && item.SourceObjectId == origin.SourceObjectId
                        && item.ControllerId == origin.ControllerId && item.CardNo == origin.CardNo)
                    || state.CardPlays.Resolved.Any(play => play.PlayId == origin.StackItemId && play.Source.ObjectId == origin.SourceObjectId
                        && play.Source.Generation >= origin.SourceGeneration && play.PlayerId == origin.ControllerId
                        && play.CardNo == origin.CardNo && play.Tick >= origin.AppliedTick)));

    private const string SettRecallEffect = "SETT_BOON_UNIT_DESTROYED_RECALL_EXHAUSTED";
    private sealed record DestructionOption(RuleChoiceOption Choice, string Target, string Source, string Kind,
        string EffectKey, string SourceCardNo, bool Optional = false, string Payment = "", int Mana = 0, string? ResponsiblePlayerId = null);

    // Replacement instructions happen before the unreplaced simultaneous deaths (CN 373).
    private sealed class DestructionReplacementScope(RuleChoiceFrame frame) : IDisposable
    {
        private readonly HashSet<string> previous = frame.DestructionCandidates;
        private readonly HashSet<string> previousDeaths = frame.CurrentDestructions;
        private readonly int? previousBatch = frame.CurrentDestructionBatch;
        private readonly LastKnownMightFrame? previousMight = frame.CurrentDestructionMightFrame;
        private readonly LastKnownMightFrame? previousDraw = frame.CurrentDestructionDrawFrame;
        private readonly IReadOnlyDictionary<string, ObjectLocationState>? previousLocations = frame.CurrentDestructionLocations;
        public HashSet<string> Replaced { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, FieldRemovalResult> Outcomes { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Controllers { get; } = new(StringComparer.Ordinal);
        public List<GameEvent> Events { get; } = [];
        public void Dispose() {
            frame.DestructionCandidates = previous;
            frame.CurrentDestructions = previousDeaths;
            frame.CurrentDestructionBatch = previousBatch;
            frame.CurrentDestructionMightFrame = previousMight;
            frame.CurrentDestructionDrawFrame = previousDraw;
            frame.CurrentDestructionLocations = previousLocations;
        }
    }

    private static DestructionReplacementScope PrepareDestructionReplacements(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, IReadOnlyList<string> candidates,
        StackItemState? cause = null, string? battlefieldId = null,
        IReadOnlyDictionary<string, ObjectLocationState>? locations = null, Func<string, string>? destructionReason = null,
        IReadOnlyDictionary<string, StackItemState>? causesByTarget = null)
    {
        var frame = RuleChoices.Value ?? throw new InvalidOperationException("Destruction requires a rule command context.");
        var scope = new DestructionReplacementScope(frame);
        var remaining = candidates.Where(id => IsObjectOnField(zones, id)).ToHashSet(StringComparer.Ordinal);
        var localRecallSources = cards.Values.Where(source => source.Tags.Contains(CardObjectTags.UnitCard)
            && FieldObjectTypeRules.IsVisibleUnit(source) && IsObjectOnField(zones, source.ObjectId))
            .SelectMany(source => AttachedRuleTextRules.Instances(zones, cards, source)
                .Where(rule => CardReplacementSpecRules.TryGetReplacement(rule.CardNo,
                    CardReplacementSpecRules.IsOtherFriendlyLowerPowerUnitHereDestroyedRecallExhaustedReplacement, out _))
                .Select(rule => new CapturedLocalRecallSource(new(source.ObjectId, source.ObjectGeneration), rule)))
            .ToArray();
        var startedLocalSources = new HashSet<string>(StringComparer.Ordinal);
        frame.DestructionCandidates = frame.DestructionCandidates.Concat(candidates).ToHashSet(StringComparer.Ordinal);
        frame.CurrentDestructions = remaining.ToHashSet(StringComparer.Ordinal);
        frame.CurrentDestructionBatch = frame.DestructionBatchSequence++;
        frame.CurrentDestructionDrawFrame = null;
        frame.CurrentDestructionLocations = locations ?? state.ObjectLocations;
        frame.CurrentDestructionMightFrame = remaining.Any(id => cards.TryGetValue(id, out var unit)
            && AttachedRuleTextRules.Instances(zones, cards, unit).Any(i => UnitDestroyedTriggerSpecRules.TryGetTrigger(i.CardNo,
                UnitDestroyedTriggerSpecRules.IsLastBreathPowerfulDrawTrigger, out _)))
            ? CapturePublicMightFrame(state, zones, cards, locations) : null;
        try {
            foreach (var player in zones.Keys.OrderBy(p => p == state.TurnPlayerId ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal)) {
                var declinedOptional = false;
                IReadOnlyList<CapturedDestructionOption>? activeLocalGroup = null;
                while (true) {
                    remaining.RemoveWhere(id => !IsObjectOnField(zones, id));
                    // CN 373.2: evaluate an unstarted source where it is now.
                    // Once selected, its whole simultaneous group finishes in
                    // that context before another replacement can intervene.
                    var localRecalls = activeLocalGroup ?? CaptureLocalDestructionRecalls(state, zones, cards,
                        ReconcileObjectLocations(locations ?? state.ObjectLocations, zones), remaining, localRecallSources)
                        .Where(c => !startedLocalSources.Contains(c.Option.EffectKey)).ToArray();
                    var options = new List<DestructionOption>();
                    var canPayOptional = false;
                    foreach (var target in remaining.Order(StringComparer.Ordinal)) {
                        if (!cards.TryGetValue(target, out var unit) || EffectiveFieldControllerId(zones, target, unit) != player) continue;
                        IReadOnlyList<DestructionOption> candidatesForTarget = [];
                        if (activeLocalGroup is null) {
                            candidatesForTarget = DestructionOptions(state, zones, cards, pools, target, player,
                                causesByTarget?.GetValueOrDefault(target) ?? cause, battlefieldId, locations ?? state.ObjectLocations, declinedOptional, out var optionalAvailable);
                            canPayOptional |= optionalAvailable;
                        }
                        foreach (var candidate in candidatesForTarget.Concat(localRecalls
                            .Where(c => c.Option.Target == target && c.TargetGeneration == unit.ObjectGeneration).Select(c => c.Option)))
                            if (!frame.AppliedDestructionReplacements.Contains(candidate.EffectKey)) options.Add(candidate);
                    }
                    var resources = canPayOptional ? ReplacementResourceActions(ReplacementResourceContext(state, zones, cards, pools, player), player) : [];
                    if (options.Count + resources.Count == 0) {
                        if (activeLocalGroup is not null) { activeLocalGroup = null; continue; }
                        break;
                    }
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
                    if (option.Kind == "LOCAL_RECALL" && activeLocalGroup is null) {
                        startedLocalSources.Add(option.EffectKey);
                        activeLocalGroup = localRecalls.Where(c => c.Option.EffectKey == option.EffectKey).ToArray();
                    }
                    var unitBefore = cards[option.Target];
                    var previousChain = frame.AppliedDestructionReplacements;
                    frame.AppliedDestructionReplacements = new(previousChain, StringComparer.Ordinal) { option.EffectKey };
                    try {
                        ApplyDestructionOption(state, zones, cards, pools, player, option, scope.Events, causesByTarget?.GetValueOrDefault(option.Target) ?? cause, destructionReason?.Invoke(option.Target));
                    } finally { frame.AppliedDestructionReplacements = previousChain; }
                    scope.Replaced.Add(option.Target); remaining.Remove(option.Target); frame.CurrentDestructions.Remove(option.Target);
                    scope.Controllers[option.Target] = player;
                    scope.Outcomes[option.Target] = new(unitBefore.OwnerId ?? player, option.Kind == "BANISH" ? "BANISHED" : "BASE",
                        option.Kind == "BANISH", option.Kind != "BANISH", unitBefore.Tags.Contains(CardObjectTags.EquipmentCard), unitBefore.Tags.Contains(CardObjectTags.UnitCard), []);
                }
            }
            frame.CurrentDestructionDrawFrame = remaining.Any(id => cards.TryGetValue(id, out var unit)
                && AttachedRuleTextRules.Instances(zones, cards, unit).Any(i => UnitDestroyedTriggerSpecRules.SelfDrawTriggers(i.CardNo).Count > 0
                    || UnitDestroyedTriggerSpecRules.LastBreathBattlefieldDamageTriggers(i.CardNo).Count > 0))
                ? CapturePublicMightFrame(state, zones, cards, locations) : null;
            return scope;
        } catch { scope.Dispose(); throw; }
    }

    private sealed record CapturedDestructionOption(DestructionOption Option, long TargetGeneration);
    private sealed record CapturedLocalRecallSource(ObjectBinding Source, RuleTextInstance Rule);

    // CN 370.3–370.4/373.2: sources present before the event may apply while
    // awaiting their simultaneous deaths. A prior replacement can relocate a
    // source before its own group begins; freeze its targets only at that point.
    private static IReadOnlyList<CapturedDestructionOption> CaptureLocalDestructionRecalls(
        MatchState state, IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        IReadOnlyDictionary<string, ObjectLocationState> locations, IEnumerable<string> targets,
        IReadOnlyList<CapturedLocalRecallSource> sources)
    {
        var result = new List<CapturedDestructionOption>();
        var context = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
        foreach (var id in targets.Order(StringComparer.Ordinal)) {
            if (!cards.TryGetValue(id, out var target)) continue;
            if (!target.Tags.Contains(CardObjectTags.UnitCard) || !FieldObjectTypeRules.IsVisibleUnit(target)) continue;
            var controller = EffectiveFieldControllerId(zones, id, target);
            foreach (var captured in sources.OrderBy(c => c.Source.ObjectId, StringComparer.Ordinal)
                .ThenBy(c => c.Rule.Id, StringComparer.Ordinal)) {
                if (!cards.TryGetValue(captured.Source.ObjectId, out var source)
                    || source.ObjectGeneration != captured.Source.Generation || !IsObjectOnField(zones, source.ObjectId)
                    || source.ObjectId == id || !source.Tags.Contains(CardObjectTags.UnitCard)
                    || !FieldObjectTypeRules.IsVisibleUnit(source)
                    || EffectiveFieldControllerId(zones, source.ObjectId, source) != controller
                    || !IsSameFieldLocation(zones, locations, id, source.ObjectId)
                    || !AttachedRuleTextRules.Instances(zones, cards, source).Any(rule => rule == captured.Rule)
                    || ResolveCurrentFieldUnitPower(context, source) <= ResolveCurrentFieldUnitPower(context, target)) continue;
                var sourceName = CardBehaviorRegistry.TryGetByCardNo(captured.Rule.CardNo, out var card) ? card.DisplayName : captured.Rule.CardNo;
                var optionId = $"LOCAL_RECALL:{id}:{source.ObjectId}"
                    + (captured.Rule.Origin is null ? "" : ":" + captured.Rule.Id);
                var copiedLabel = captured.Rule.Origin is null ? "" : $"（复制文本：{captured.Rule.Origin.Equipment.ObjectId}）";
                result.Add(new(new(new(optionId,
                    $"{sourceName}（{source.ObjectId}）{copiedLabel}：移除 {id} 的伤害并休眠召回", [id, source.ObjectId]),
                    id, source.ObjectId, "LOCAL_RECALL", $"LOCAL_RECALL:{source.ObjectId}:{source.ObjectGeneration}:{captured.Rule.Id}",
                    captured.Rule.CardNo), target.ObjectGeneration));
            }
        }
        return result;
    }

    private static bool IsSameFieldLocation(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, ObjectLocationState> locations, string target, string source)
    {
        var a = FindFieldObjectLocation(zones, target); var b = FindFieldObjectLocation(zones, source);
        if (a is null || b is null || a.Value.Zone != b.Value.Zone) return false;
        if (a.Value.Zone == MoveUnitBaseZone) return a.Value.PlayerId == b.Value.PlayerId;
        return a.Value.Zone == MoveUnitBattlefieldZone
            && locations.TryGetValue(target, out var x) && locations.TryGetValue(source, out var y)
            && x.Zone == MoveUnitBattlefieldZone && y.Zone == MoveUnitBattlefieldZone
            && !string.IsNullOrWhiteSpace(x.BattlefieldObjectId) && x.BattlefieldObjectId == y.BattlefieldObjectId;
    }

    private static string? ResolveDestroyedSourceBattlefieldObjectId(
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        string sourceObjectId,
        string? fallbackBattlefieldObjectId = null)
    {
        if (objectLocations is not null
            && objectLocations.TryGetValue(sourceObjectId, out var location)
            && string.Equals(location.Zone, MoveUnitBattlefieldZone, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(location.BattlefieldObjectId)
            && !string.Equals(location.BattlefieldObjectId, sourceObjectId, StringComparison.Ordinal))
        {
            return location.BattlefieldObjectId;
        }

        return string.IsNullOrWhiteSpace(fallbackBattlefieldObjectId)
            ? null
            : fallbackBattlefieldObjectId;
    }

    private static IReadOnlyList<DestructionOption> DestructionOptions(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools,
        string target, string player, StackItemState? cause, string? battlefieldId,
        IReadOnlyDictionary<string, ObjectLocationState> locations, bool declinedOptional, out bool optionalAvailable, ISet<string>? appliedReplacements = null)
    {
        var unit = cards[target]; var pool = pools.GetValueOrDefault(player, RunePool.Empty);
        var options = new List<DestructionOption>(); optionalAvailable = false;
        var label = CardBehaviorRegistry.TryGetByCardNo(unit.CardNo ?? "", out var behavior) ? behavior.DisplayName : unit.CardNo ?? target;
        string Key(string kind, string source) => $"{kind}:{source}:{cards[source].ObjectGeneration}";
        void Add(string kind, string source, string text, bool optional = false, string payment = "", int mana = 0)
            => options.Add(new(new($"{kind}:{target}" + (source == target && kind is "BANISH" or "RECALL" ? "" : $":{source}")
                + (payment.Length > 0 ? ":" + payment : ""), $"{label}（{target}）：{text}", [target, source]),
                target, source, kind, Key(kind, source), cards[source].CardNo ?? "", optional, payment, mana));
        if (unit.UntilEndOfTurnEffects.Contains(RecallToBaseExhaustedIfDestroyedThisTurnEffectId))
            Add("RECALL", target, "应用本回合效果，移除伤害并休眠召回");
        if (unit.UntilEndOfTurnEffects.Contains(BanishIfDestroyedThisTurnEffectId))
        {
            if (unit.BanishReplacementOrigins is { Count: > 0 } origins)
                foreach (var (origin, index) in origins.Select((value, index) => (value, index)))
                    options.Add(new(new($"BANISH:{target}:{index}", $"{label}（{target}）：应用 {origin.ControllerId} 的{OfficialCardSourceIdentityGroups.DisplayName(origin.CardNo)}效果，改为放逐", [target]),
                        target, target, "BANISH", $"BANISH:{target}:{unit.ObjectGeneration}:{index}", unit.CardNo ?? "",
                        ResponsiblePlayerId: origin.ControllerId));
            else Add("BANISH", target, "应用本回合效果，改为放逐");
        }
        if (!unit.Tags.Contains(CardObjectTags.UnitCard) || unit.IsFaceDown) return options;
        foreach (var gear in cards.Values.Where(gear => gear.AttachedToObjectId == target && !gear.IsFaceDown
            && gear.Tags.Contains(CardObjectTags.EquipmentCard) && IsObjectOnField(zones, gear.ObjectId)
            && IsSameFieldLocation(zones, locations, target, gear.ObjectId)
            && OfficialArmamentCatalog.Find(gear.CardNo)?.ReplacesHostDestruction == true).OrderBy(gear => gear.ObjectId, StringComparer.Ordinal)) {
            var name = CardBehaviorRegistry.TryGetByCardNo(gear.CardNo ?? "", out var definition) ? definition.DisplayName : gear.CardNo;
            options.Add(new(new($"ATTACHED_GEAR:{target}:{gear.ObjectId}", $"{label}（{target}）：摧毁贴附的 {name}（{gear.ObjectId}），移除伤害并休眠召回", [target, gear.ObjectId]),
                target, gear.ObjectId, "ATTACHED_GEAR", $"ATTACHED_GEAR:{target}:{unit.ObjectGeneration}:{gear.ObjectId}:{gear.ObjectGeneration}:{gear.AttachmentRevision}", gear.CardNo!));
        }
        foreach (var source in zones[player].Base.Concat(zones[player].Battlefields).Distinct().Order(StringComparer.Ordinal)) {
            if (!cards.TryGetValue(source, out var gear) || gear.IsFaceDown
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
            && !(appliedReplacements ?? RuleChoices.Value!.AppliedDestructionReplacements).Contains(Key("ALTAR", fieldId))) {
            optionalAvailable = true;
            if (PaymentCostRules.AvailableMana(pool, ManaPaymentPurpose.Other, state) >= ability.Amount)
                Add("ALTAR", fieldId, $"支付 {ability.Amount} 法力，鲜血祭坛休眠召回", true, mana: ability.Amount);
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
        var attachedGear = option.Kind == "ATTACHED_GEAR";
        var effectSource = attachedGear ? option.Target : option.Source;
        var effectCardNo = attachedGear ? target.CardNo ?? target.TokenFactoryCardNo ?? option.SourceCardNo : option.SourceCardNo;
        var gearReplacementEffect = attachedGear ? ReplacementKinds.AttachedUnitDestroyedDestroySourceRecallExhausted : FriendlyUnitDestroyedEquipmentRecallEffectId;
        var stack = new StackItemState($"replacement-{state.Tick}-{effectSource}", player, effectSource,
            "DESTRUCTION_REPLACEMENT", effectCardNo, [], 0, 0, []);
        if (option.Kind is "GEAR" or "ATTACHED_GEAR") {
            // A replacement-generated destruction is a new event in the same replacement chain (CN 370.2).
            var nested = ResolveFieldDestructions(state, zones, cards, stack, new HashSet<string>(),
                state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(), pools, objectLocations: ReconcileObjectLocations(state.ObjectLocations, zones),
                explicitDestroyObjectIds: new HashSet<string> { option.Source });
            events.AddRange(nested.Events.Select(e => e.Kind is "UNIT_DESTROYED" or "EQUIPMENT_DESTROYED"
                && e.Payload.GetValueOrDefault("targetObjectId") as string == option.Source
                ? e with { Payload = new Dictionary<string, object?>(e.Payload) {
                    ["replacementTargetObjectId"] = option.Target, ["reason"] = gearReplacementEffect,
                    ["destroyReason"] = destructionReason } } : e));
            foreach (var pool in nested.RunePools) pools[pool.Key] = pool.Value;
            RuleChoices.Value!.NestedTriggers.AddRange(nested.TriggerQueue);
            RuleChoices.Value.NestedDestroyedOwners.UnionWith(nested.DestroyedUnitOwnerIds);
        }
        if (option.Kind == "ALTAR") {
            pools[player] = PaymentCostRules.SpendMana(pools.GetValueOrDefault(player, RunePool.Empty), option.Mana, state: state);
            events.Add(new("COST_PAID", "支付鲜血祭坛替换费用", new Dictionary<string, object?> {
                ["playerId"] = player, ["mana"] = option.Mana, ["power"] = 0, ["reason"] = BattlefieldDestroyedInBattleRecallEffectId }));
        }
        // Nested replacement instructions may already have removed this incarnation.
        if (!cards.TryGetValue(option.Target, out var current) || current.ObjectGeneration != target.ObjectGeneration
            || !IsObjectOnField(zones, option.Target)) return;
        if (option.Kind == "BANISH") {
            TryRemoveFieldTargetCore(zones, cards, option.Target, true, out var removal);
            var removalEvents = BuildFieldRemovalEvents("替换为放逐", cause ?? stack, option.Target, removal, destructionReason);
            events.AddRange(option.ResponsiblePlayerId is { } actor
                ? removalEvents.Select(ev => ev.Kind == "UNIT_BANISHED"
                    ? CaptureOwnedBanishObservers(ev, zones, cards, actor, [option.Target]) : ev)
                : removalEvents);
            return;
        }
        RecallAttachedGroupToBase(zones, cards, option.Target, player);
        cards[option.Target] = current with { Damage = 0, DamageMarkedByPlayerIds = null, IsExhausted = true, IsAttacking = false, IsDefending = false, ControllerId = player,
            UntilEndOfTurnEffects = option.Kind == "RECALL" ? current.UntilEndOfTurnEffects.Where(e => e != RecallToBaseExhaustedIfDestroyedThisTurnEffectId).ToArray() : current.UntilEndOfTurnEffects };
        events.Add(new("UNIT_RECALLED_TO_BASE", "移除伤害并休眠召回", new Dictionary<string, object?> {
            ["sourceObjectId"] = option.Kind == "RECALL" ? cause?.SourceObjectId ?? option.Source : effectSource,
            ["targetObjectId"] = option.Target, ["ownerPlayerId"] = current.OwnerId ?? player,
            ["controllerId"] = player, ["destinationZone"] = "BASE", ["isExhausted"] = true, ["damage"] = 0,
            ["destroyReason"] = destructionReason,
            ["replacementEffectId"] = option.Kind is "GEAR" or "ATTACHED_GEAR" ? gearReplacementEffect
                : option.Kind == "ALTAR" ? BattlefieldDestroyedInBattleRecallEffectId
                : option.Kind == "LOCAL_RECALL" ? ReplacementKinds.OtherFriendlyLowerPowerUnitHereDestroyedRecallExhausted
                : RecallToBaseExhaustedIfDestroyedThisTurnEffectId }));
    }

    private static void RecallAttachedGroupToBase(Dictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, string target, string controller)
    {
        // CN 456 / 719.3–719.4: recall changes the whole attached group's location,
        // without a move action, detaching cards, or changing their other states.
        var group = new List<string> { target };
        var seen = new HashSet<string>(group, StringComparer.Ordinal);
        for (var i = 0; i < group.Count; i++)
            foreach (var attached in cards.Values.Where(c => c.AttachedToObjectId == group[i]
                && IsObjectOnField(zones, c.ObjectId)).OrderBy(c => c.ObjectId, StringComparer.Ordinal))
                if (seen.Add(attached.ObjectId)) group.Add(attached.ObjectId);
        foreach (var (player, zone) in zones.ToArray())
            zones[player] = zone with { Base = zone.Base.Where(id => !seen.Contains(id)).ToArray(),
                Battlefields = zone.Battlefields.Where(id => !seen.Contains(id)).ToArray() };
        zones[controller] = zones[controller] with { Base = zones[controller].Base.Concat(group).ToArray() };
    }

    private static void ApplySettDestructionOption(Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, string player, DestructionOption option, List<GameEvent> events)
    {
        var unitState = cards[option.Target];
        if (option.Payment.StartsWith("RUNE:", StringComparison.Ordinal))
        {
            var context = ReplacementResourceContext(RuleChoices.Value!.Origin, zones, cards, pools, player);
            ApplyRuleChoiceRecycleRuneResource(context, zones, cards, pools, player, option.Payment[5..], events, RuleChoiceWindow);
        }
        var current = pools.GetValueOrDefault(player) ?? RunePool.Empty;
        // Selecting a colored pool preserves the player's other colors; recycling
        // spends that rune's newly generated power, even if another pool was available.
        var traitToSpend = option.Payment.StartsWith("TRAIT:", StringComparison.Ordinal) ? option.Payment[6..]
            : option.Payment.StartsWith("RUNE:", StringComparison.Ordinal) && TryGetRuneTrait(cards[option.Payment[5..]], out var t) ? t : null;
        var paymentTraits = traitToSpend is null ? new Dictionary<string, int>() : new Dictionary<string, int> { [traitToSpend] = 1 };
        var paidPower = PayPowerCost(current, traitToSpend is null ? 1 : 0, paymentTraits);
        pools[player] = current with { Power = paidPower.AnyPower, PowerByTrait = paidPower.PowerByTrait };
        RecallAttachedGroupToBase(zones, cards, option.Target, player);
        cards[option.Target] = unitState with { Damage = 0, DamageMarkedByPlayerIds = null, Power = unitState.Power - 1, IsExhausted = true,
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
