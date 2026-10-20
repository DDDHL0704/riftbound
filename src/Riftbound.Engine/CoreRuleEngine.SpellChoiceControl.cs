using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveSpellControl(MatchState state, StackItemState item, CardBehaviorDefinition behavior)
    {
        var target = state.StackItems.FirstOrDefault(s => item.TargetObjectIds.Contains(s.StackItemId));
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var countered = new List<string>();
        var triggers = new List<TriggerQueueItemState>();
        var effects = state.UntilEndOfTurnEffects;
        IReadOnlyList<StackItemState>? stack = null;
        if (target is not null && !StackObjectRules.IsAbility(target)
            && IsLegalChosenCardTarget(state, item.ControllerId, target.StackItemId, 0, item.TargetObjectIds,
                CardTargetScopes.StackSpell, behavior)
            && (item.TargetStackSources is null || item.TargetStackSources.GetValueOrDefault(target.StackItemId) == target.SourceObjectId))
        {
            if (!behavior.MayPayPowerToGainControlAndRemakeSpellChoices
                || ChooseOptionalControlPayment(state, item, zones, cards, pools, events))
            {
                if (!TryGainControlOfTargetStackSpell(state.StackItems, behavior, item, out var transferred, out var controlEvent))
                    throw new InvalidOperationException("A confirmed spell target disappeared within its resolution.");
                var controlled = transferred.Single(s => s.StackItemId == target.StackItemId);
                var context = state with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
                    StackItems = state.StackItems.Select(s => s.StackItemId == target.StackItemId ? controlled : s).ToArray() };
                var remade = false;
                if (behavior.MayPayPowerToGainControlAndRemakeSpellChoices || behavior.MayChooseNewTargetsForControlledSpell)
                    controlled = RemakeSpellChoices(context, controlled, item.StackItemId,
                        behavior.MayChooseNewTargetsForControlledSpell, out remade);
                stack = transferred
                    .Select(s => s.StackItemId == target.StackItemId ? controlled : s).ToArray();
                events.Add(controlEvent);
                if (remade)
                {
                    events.Add(new("SPELL_CHOICES_REMADE", "为法术做出新的选择", new Dictionary<string, object?> {
                        ["stackItemId"] = target.StackItemId, ["effectKind"] = controlled.EffectKind,
                        ["targetObjectIds"] = controlled.TargetObjectIds, ["repeatExecutions"] = controlled.RepeatExecutions,
                        ["destination"] = controlled.Destination, ["movementDestinations"] = controlled.MovementDestinations }));
                    if (!CardBehaviorRegistry.TryGetByEffectKind(controlled.EffectKind, out var controlledBehavior))
                        throw new InvalidOperationException("A remade spell must retain a registered mode.");
                    var selected = CaptureSpellTriggers(context, controlled, controlledBehavior, out effects, targetSelection: true)
                        .Select(t => t with { TriggerId = $"spell-remade-{item.StackItemId}-{t.TriggerId}" }).ToArray();
                    triggers.AddRange(selected);
                    events.AddRange(selected.Select(BuildTriggerQueuedEvent));
                }
            }
            else if (TryCounterStackItem(state, zones, cards, target.StackItemId, item, behavior, out var counterEvent))
            {
                countered.Add(target.StackItemId); events.Add(counterEvent);
            }
        }
        CompleteSpellSource(zones, cards, item, behavior);
        return new(zones, cards, state.PlayerScores, state.PlayerExperience, pools, effects,
            null, events, [], stack, countered, null, triggers, state.RngCursor,
            ObjectLocations: ReconcileObjectLocations(state.ObjectLocations, zones));
    }

    private static bool ChooseOptionalControlPayment(MatchState state, StackItemState item,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, List<GameEvent> events)
    {
        var player = item.ControllerId;
        while (true)
        {
            var context = ReplacementResourceContext(state, zones, cards, pools, player);
            var pool = pools.GetValueOrDefault(player) ?? RunePool.Empty;
            var options = new List<RuleChoiceOption> { new("DECLINE", "不支付：尝试无效化该法术") };
            if (pool.Power > 0) options.Add(new("PAY:ANY", "支付 1 点通用符能：获得控制权"));
            foreach (var trait in pool.PowerByTrait.Where(t => t.Value > 0).OrderBy(t => t.Key, StringComparer.Ordinal))
                options.Add(new("PAY:TRAIT:" + trait.Key, "支付 1 点" + RuneTraitLabel(trait.Key) + "符能：获得控制权"));
            foreach (var rune in zones[player].Base.Where(id => cards.TryGetValue(id, out var card)
                && card.Tags.Contains(CardObjectTags.RuneCard) && !card.IsFaceDown
                && SourceObjectControlledByPlayerOrLegacyOwned(card, player) && TryGetRuneTrait(card, out _)).Order(StringComparer.Ordinal))
                options.Add(new("PAY:RUNE:" + rune, "回收符文 " + rune + "，支付 1 点符能：获得控制权", [rune]));
            var resources = ReplacementResourceActions(context, player);
            options.AddRange(resources.Select(r => r.Choice));
            var selected = RuleChoices.Value!.Choose(player, "结算时可支付 1 点任意符能，获得法术控制权并可重选；不支付则尝试无效化。",
                options, powerCost: 1, kind: "OPTIONAL_SPELL_CONTROL_PAYMENT", preview: context with { PendingPayment = null });
            if (selected == "DECLINE") return false;
            if (resources.FirstOrDefault(r => r.Choice.Id == selected) is { } resource)
            {
                ApplyReplacementResourceAction(context, zones, cards, pools, player, resource, events); continue;
            }
            string? traitToSpend = selected.StartsWith("PAY:TRAIT:", StringComparison.Ordinal) ? selected[10..] : null;
            if (selected.StartsWith("PAY:RUNE:", StringComparison.Ordinal))
            {
                var rune = selected[9..];
                var paid = ApplyRecycleRunePaymentResourceActions(pools, zones, cards, new(), player, [rune], events, RuleChoiceWindow);
                foreach (var pair in paid) pools[pair.Key] = pair.Value;
                TryGetRuneTrait(cards[rune], out traitToSpend!);
            }
            pool = pools[player];
            var traits = traitToSpend is null ? new Dictionary<string, int>() : new Dictionary<string, int> { [traitToSpend] = 1 };
            var power = PayPowerCost(pool, traitToSpend is null ? 1 : 0, traits);
            pools[player] = pool with { Power = power.AnyPower, PowerByTrait = power.PowerByTrait };
            events.Add(new("COST_PAID", "支付结算中的 1 点任意符能", new Dictionary<string, object?> {
                ["playerId"] = player, ["mana"] = 0, ["power"] = 1, ["powerByTrait"] = traits,
                ["sourceObjectId"] = item.SourceObjectId, ["reason"] = "OPTIONAL_SPELL_CONTROL_PAYMENT" }));
            return true;
        }
    }

    private static StackItemState RemakeSpellChoices(MatchState state, StackItemState spell, string resolvingItemId,
        bool targetsOnly, out bool remade)
    {
        // Editing one choice at a time represents partial remakes and arbitrary target counts
        // without materializing every combination. Confirmation validates the whole draft.
        var executions = (spell.RepeatExecutions ?? [new(spell.EffectKind, spell.TargetObjectIds,
            spell.TargetGenerations ?? new Dictionary<string, long>(), spell.MovementDestinations, spell.Destination)]).ToArray();
        remade = false;
        if (!executions.Any(e => HasRemakableSpellDeclaration(spell, e, targetsOnly))) return spell;
        if (HasNoLegalSingleTargetRemake(state, spell, resolvingItemId, executions, targetsOnly)) return spell;
        while (true)
        {
            var options = new List<RuleChoiceOption> { new("KEEP_ALL", "不做新的选择：保留所有原选择（包括已不合法的选择）") };
            var edits = new Dictionary<string, (int Index, SpellExecutionState Execution)>();
            if (executions.All(e => HasLegalRemadeExecution(state, spell, e)))
                options.Add(new("CONFIRM", "确认新的选择；所有目标与保留选择均合法"));
            for (var i = 0; i < executions.Length; i++)
            {
                var execution = executions[i];
                if (!CardBehaviorRegistry.TryGetByEffectKind(execution.EffectKind, out var definition)) continue;
                definition = definition with { StandbyPlayFrom = spell.PlayedFromStandby };
                if (!targetsOnly)
                    foreach (var mode in CardBehaviorRegistry.GetAll().Where(d => d.CardNo == spell.CardNo && d.EffectKind != execution.EffectKind))
                        Add(execution with { EffectKind = mode.EffectKind }, $"第 {i + 1} 次：模式 {mode.Mode}（{mode.DisplayName}）");
                var candidates = state.CardObjects.Keys.Concat(state.StackItems.Where(s => s.StackItemId != spell.StackItemId
                    && s.StackItemId != resolvingItemId).Select(s => s.StackItemId))
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                for (var j = 0; j <= execution.TargetObjectIds.Count; j++)
                {
                    var index = j;
                    foreach (var id in candidates)
                    {
                        var targets = execution.TargetObjectIds.ToList();
                        if (index == targets.Count) targets.Add(id); else targets[index] = id;
                        if (targets.Count > MaxTargetCount(state, spell.ControllerId, definition, TargetCountConditionApplies(state, spell.ControllerId, definition))
                            && !IsStandbyReactionStackItem(definition, spell)) continue;
                        if (index < execution.TargetObjectIds.Count && id == execution.TargetObjectIds[index]
                            && CurrentChosenIncarnation(state, execution, id)) continue;
                        var scope = IsStandbyReactionStackItem(definition, spell) ? definition.StandbyReactionTargetScope : PlayCardTargetScopeForBehavior(definition);
                        if (!IsLegalChosenCardTarget(state, spell.ControllerId, id, index, targets, scope, definition)
                            || !PermanentPlayHereTargetAllowed(state, spell, definition, id)) continue;
                        var generations = execution.TargetGenerations.Where(p => targets.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
                        if (state.CardObjects.TryGetValue(id, out var card)) generations[id] = card.ObjectGeneration;
                        Add(execution with { TargetObjectIds = targets.ToArray(), TargetGenerations = generations },
                            $"第 {i + 1} 次：{(index == execution.TargetObjectIds.Count ? "添加" : "重选")}目标 {index + 1} 为 {ChoiceObjectLabel(state, id)}", [id]);
                    }
                    if (index < execution.TargetObjectIds.Count)
                    {
                        var targets = execution.TargetObjectIds.Where((_, k) => k != index).ToArray();
                        Add(execution with { TargetObjectIds = targets, TargetGenerations = execution.TargetGenerations.Where(p => targets.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value) },
                            $"第 {i + 1} 次：移除目标 {index + 1}");
                    }
                }
                var destinations = targetsOnly ? [] : definition.CommandedMovement is { } move ? CommandedMovementDestinations(state, spell.ControllerId, move)
                    : definition.MovesTargetsToChosenLocation ? ChosenMovementDestinations(state, spell.ControllerId) : [];
                foreach (var destination in destinations.Where(d => d.Id != execution.Destination))
                    Add(execution with { Destination = destination.Id }, $"第 {i + 1} 次：目的地 {destination.Label}");
                if (!targetsOnly && definition.MovesTargetsSequentially)
                    for (var j = 0; j < execution.TargetObjectIds.Count; j++)
                        foreach (var destination in SequentialMovementDestinations(state, spell.ControllerId, j))
                        {
                            var locations = (execution.MovementDestinations ?? Enumerable.Repeat("", execution.TargetObjectIds.Count).ToArray()).ToArray();
                            if (locations.Length != execution.TargetObjectIds.Count || locations[j] == destination.Id) continue;
                            locations[j] = destination.Id;
                            Add(execution with { MovementDestinations = locations }, $"第 {i + 1} 次：目标 {j + 1} 的目的地 {destination.Label}");
                        }
                void Add(SpellExecutionState draft, string label, IReadOnlyList<string>? ids = null)
                {
                    draft = draft with { MovementDeclarationPlayerId = CardBehaviorRegistry.TryGetByEffectKind(draft.EffectKind, out var movement)
                        && movement.MovesTargetsToChosenLocation ? spell.ControllerId : null };
                    var key = "EDIT:" + i + ":" + MatchStateHasher.HashValue(draft);
                    if (edits.TryAdd(key, (i, draft))) options.Add(new(key, label, ids));
                }
            }
            var selected = RuleChoices.Value!.Choose(spell.ControllerId,
                (targetsOnly ? "可重新指定目标；原模式、终点和已支付的额外费用保持不变。" : "可为任意目标或模式做出新的选择；")
                    + "确认时，保留的选择也必须合法。也可保留全部原选择。当前选择："
                    + string.Join("；", executions.Select((e, i) => $"第 {i + 1} 次 {e.EffectKind}：{string.Join("、", e.TargetObjectIds.Select(id => ChoiceObjectLabel(state, id)))} {e.Destination}")),
                options, kind: "SPELL_CHOICE_REMAKE", preview: state);
            if (selected == "KEEP_ALL") return spell;
            if (selected == "CONFIRM") { remade = true; return ApplyRemadeExecutions(state, spell, executions); }
            var edit = edits[selected]; executions[edit.Index] = edit.Execution;
        }
    }

    private static bool HasRemakableSpellDeclaration(StackItemState spell, SpellExecutionState execution, bool targetsOnly)
    {
        if (!CardBehaviorRegistry.TryGetByEffectKind(execution.EffectKind, out var behavior)) return false;
        return behavior.RequiredTargetCount > 0 || behavior.AnyNumberOfTargets
            || behavior.UsesFriendlyBattlefieldUnitCountAsMaxTargetCount
            || IsStandbyReactionStackItem(behavior, spell)
            || !targetsOnly && (behavior.CommandedMovement is not null || behavior.MovesTargetsToChosenLocation
                || behavior.MovesTargetsSequentially
                || CardBehaviorRegistry.GetAll().Any(other => other.CardNo == spell.CardNo && other.EffectKind != execution.EffectKind));
    }

    private static bool HasNoLegalSingleTargetRemake(MatchState state, StackItemState spell, string resolvingItemId,
        IReadOnlyList<SpellExecutionState> executions, bool targetsOnly)
    {
        // A friendly unit declaration uses card objects. With one target slot
        // and other declarations fixed, zero and each unit exhaust its choices.
        if (executions.Count != 1 || spell.EffectRepeatCount != 1 || spell.PlayedFromStandby is not null
            || spell.OptionalCosts.Count != 0) return false;
        var execution = executions[0];
        if (!CardBehaviorRegistry.TryGetByEffectKind(execution.EffectKind, out var behavior)
            || behavior.TargetScope != CardTargetScopes.FriendlyUnit
            || behavior.RequiredTargetCount != 1 || behavior.AnyNumberOfTargets
            || behavior.UsesFriendlyBattlefieldUnitCountAsMaxTargetCount
            || behavior.CommandedMovement is not null || behavior.MovesTargetsToChosenLocation || behavior.MovesTargetsSequentially
            || !string.IsNullOrEmpty(execution.Destination) || execution.MovementDestinations is { Count: > 0 }
            || !targetsOnly && CardBehaviorRegistry.GetAll().Any(b => b.CardNo == spell.CardNo && b.EffectKind != execution.EffectKind)
            || MaxTargetCount(state, spell.ControllerId, behavior, TargetCountConditionApplies(state, spell.ControllerId, behavior)) > 1)
            return false;
        if (HasLegalRemadeExecution(state, spell, execution)
            || HasLegalRemadeExecution(state, spell, execution with { TargetObjectIds = [], TargetGenerations = new Dictionary<string, long>() }))
            return false;
        foreach (var id in state.CardObjects.Keys.Concat(state.StackItems
            .Where(s => s.StackItemId != spell.StackItemId && s.StackItemId != resolvingItemId).Select(s => s.StackItemId)))
        {
            var generations = new Dictionary<string, long>();
            if (state.CardObjects.TryGetValue(id, out var card)) generations[id] = card.ObjectGeneration;
            if (HasLegalRemadeExecution(state, spell, execution with { TargetObjectIds = [id], TargetGenerations = generations }))
                return false;
        }
        return true;
    }

    private static string ChoiceObjectLabel(MatchState state, string id)
        => state.CardObjects.TryGetValue(id, out var card) ? OfficialCardSourceIdentityGroups.DisplayName(card.CardNo ?? "") + "（" + id + "）" : id;

    private static StackItemState ApplyRemadeExecutions(MatchState state, StackItemState spell, IReadOnlyList<SpellExecutionState> executions)
    {
        var first = executions[0];
        var chosen = executions.SelectMany(e => e.TargetObjectIds).ToHashSet(StringComparer.Ordinal);
        return spell with { EffectKind = first.EffectKind, TargetObjectIds = first.TargetObjectIds,
            TargetGenerations = first.TargetGenerations, Destination = first.Destination, MovementDestinations = first.MovementDestinations,
            RepeatExecutions = spell.RepeatExecutions is null ? null : executions.Select(e => e with {
                MovementDeclarationPlayerId = CardBehaviorRegistry.TryGetByEffectKind(e.EffectKind, out var movement)
                    && movement.MovesTargetsToChosenLocation ? spell.ControllerId : null }).ToArray(),
            TargetStackSources = state.StackItems.Where(s => chosen.Contains(s.StackItemId)).ToDictionary(s => s.StackItemId, s => s.SourceObjectId, StringComparer.Ordinal) };
    }
}
