using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record SpellModeSetSpec(string CardNo, int RequiredModes,
    IReadOnlyList<CardBehaviorDefinition> Modes);

public sealed record DeclaredSpellMode(string Mode, SpellExecutionState Execution);

public sealed partial class CoreRuleEngine
{
    // CN 355.3 / 355.5: declare every mode and its targets before paying.
    // This only captures choices; the ordinary play plan still owns permission,
    // timing, costs, one spell source, and the eventual resolution sequence.
    internal static bool TryCaptureSpellModes(MatchState state, string player, string source,
        SpellModeSetSpec spec, IReadOnlyList<SpellModeChoice>? choices,
        out IReadOnlyList<DeclaredSpellMode> captured, out string error)
    {
        captured = [];
        error = "所选模式数量、模式或各模式目标不合法。";
        if (!state.Seats.ContainsKey(player)
            || !state.CardObjects.TryGetValue(source, out var card)
            || card.CardNo != spec.CardNo
            || !SourceObjectControlledByPlayerOrLegacyOwned(card, player)
            || spec.RequiredModes <= 0 || spec.Modes is null
            || spec.RequiredModes > spec.Modes.Count
            || spec.Modes.Any(mode => mode is null || mode.CardNo != spec.CardNo
                || string.IsNullOrWhiteSpace(mode.Mode))
            || spec.Modes.Select(mode => mode.Mode).Distinct(StringComparer.Ordinal).Count() != spec.Modes.Count
            || choices is null || choices.Count != spec.RequiredModes
            || choices.Any(choice => choice is null || string.IsNullOrWhiteSpace(choice.Mode)
                || choice.TargetObjectIds is null || choice.TargetObjectIds.Any(string.IsNullOrWhiteSpace))
            || choices.Select(choice => choice.Mode).Distinct(StringComparer.Ordinal).Count() != choices.Count)
            return false;

        var selected = choices.ToDictionary(choice => choice.Mode, StringComparer.Ordinal);
        if (selected.Keys.Any(id => !spec.Modes.Any(mode => mode.Mode == id))) return false;
        var result = new List<DeclaredSpellMode>();
        // CN 158.1 / 359.3.d: the printed order determines resolution order, including when the
        // caller lists the selected modes in a different order.
        foreach (var mode in spec.Modes)
        {
            if (!selected.TryGetValue(mode.Mode, out var choice)) continue;
            var targets = choice.TargetObjectIds.ToArray();
            // Looking at a hidden deck is an instruction during resolution.
            // No private card identity is accepted as an upfront target.
            if (IsDeferredDeckChoice(mode) ? targets.Length != 0
                : !HasLegalDeclaredTargets(state, player, source, mode, targets, PlayCardTargetScopeForBehavior(mode)))
                return false;
            var generations = targets.Distinct(StringComparer.Ordinal).Where(state.CardObjects.ContainsKey)
                .ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration, StringComparer.Ordinal);
            result.Add(new(mode.Mode, new(mode.EffectKind, targets, generations)));
        }
        captured = result.ToArray();
        error = string.Empty;
        return true;
    }

    private static bool TryBuildModeExecutions(MatchState state, string player, PlayCardCommand command,
        SpellModeSetSpec spec, IReadOnlyList<DeclaredSpellMode> first, int repeatCount,
        out IReadOnlyList<DeclaredSpellMode>? executions, out string error)
    {
        executions = null;
        error = "回响的多模式声明数量或目标不合法。";
        if (command.RepeatChoices is { } supplied && supplied.Count != repeatCount - 1) return false;
        var result = first.ToList();
        for (var i = 1; i < repeatCount; i++)
        {
            var repeat = command.RepeatChoices?[i - 1];
            if (repeat is not null && (repeat.Mode.Length > 0 || !string.IsNullOrEmpty(repeat.Destination)
                || repeat.MovementDestinations is not null)) return false;
            if (!TryCaptureSpellModes(state, player, command.SourceObjectId, spec,
                    repeat?.ModeChoices ?? command.ModeChoices, out var modes, out error)) return false;
            if (repeat is not null && repeat.TargetObjectIds.Count > 0
                && !repeat.TargetObjectIds.SequenceEqual(modes.SelectMany(m => m.Execution.TargetObjectIds))) return false;
            result.AddRange(modes);
        }
        executions = result.ToArray();
        error = string.Empty;
        return true;
    }

    private static StackItemState ActiveSpellMode(StackItemState parent)
        => parent.ModeExecutions is { } modes && parent.CompletedModeExecutions >= 0
            && parent.CompletedModeExecutions < modes.Count
            ? parent with { EffectKind = modes[parent.CompletedModeExecutions].Execution.EffectKind,
                TargetObjectIds = modes[parent.CompletedModeExecutions].Execution.TargetObjectIds,
                TargetGenerations = modes[parent.CompletedModeExecutions].Execution.TargetGenerations,
                EffectRepeatCount = 1, ModeExecutions = null } : parent;

    internal static bool ValidSpellModeExecutions(MatchState state, StackItemState item)
    {
        var spec = CardBehaviorRegistry.TryGetByCardNo(item.CardNo ?? "", out var root) ? root.SpellModes : null;
        if (spec is null || StackObjectRules.IsAbility(item)) return item.ModeExecutions is null && item.CompletedModeExecutions == 0;
        if (item.ModeExecutions is not { } modes || item.RepeatExecutions is not null
            || item.EffectRepeatCount < 1 || modes.Count != (long)item.EffectRepeatCount * spec.RequiredModes
            || item.CompletedModeExecutions < 0 || item.CompletedModeExecutions >= modes.Count) return false;
        for (var group = 0; group < item.EffectRepeatCount; group++)
        {
            var last = -1;
            foreach (var mode in modes.Skip(group * spec.RequiredModes).Take(spec.RequiredModes))
            {
                if (mode is null || mode.Execution is not { } execution || execution.TargetObjectIds is not { } targets
                    || execution.TargetGenerations is not { } bindings || execution.Destination != ""
                    || execution.MovementDestinations is not null || execution.MovementDeclarationPlayerId is not null) return false;
                var index = spec.Modes.ToList().FindIndex(b => b.Mode == mode.Mode);
                if (index <= last || spec.Modes[index].EffectKind != execution.EffectKind) return false;
                last = index;
                var definition = spec.Modes[index];
                if (IsDeferredDeckChoice(definition) ? targets.Count != 0
                    : !HasValidTargetCount(state, item.ControllerId, definition, targets)) return false;
                var ids = targets.Distinct(StringComparer.Ordinal).ToArray();
                if (targets.Any(string.IsNullOrWhiteSpace) || bindings.Count != ids.Length
                    || ids.Any(id => !bindings.TryGetValue(id, out var generation) || generation < 0
                        || state.CardObjects.TryGetValue(id, out var card) && generation > card.ObjectGeneration)) return false;
            }
        }
        var active = item.EffectKind == root.EffectKind ? modes.Take(spec.RequiredModes) : modes.Skip(item.CompletedModeExecutions).Take(1);
        var bindingsForItem = active.SelectMany(m => m.Execution.TargetGenerations).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.First().Value);
        var actual = item.TargetGenerations ?? new Dictionary<string, long>();
        if (actual.Count != bindingsForItem.Count || actual.Any(p => !bindingsForItem.TryGetValue(p.Key, out var expected) || p.Value != expected)) return false;
        return item.EffectKind == root.EffectKind
            ? item.CompletedModeExecutions == 0 && item.TargetObjectIds.SequenceEqual(modes.Take(spec.RequiredModes).SelectMany(m => m.Execution.TargetObjectIds))
            : item.EffectKind == modes[item.CompletedModeExecutions].Execution.EffectKind
                && item.TargetObjectIds.SequenceEqual(modes[item.CompletedModeExecutions].Execution.TargetObjectIds);
    }

    private static StackResolutionResult ResolveSpellModes(MatchState state, StackItemState original)
    {
        var current = state;
        var events = new List<GameEvent>();
        var triggers = new List<TriggerQueueItemState>();
        var destroyed = new HashSet<string>(StringComparer.Ordinal);
        var damageTargets = new HashSet<string>(StringComparer.Ordinal);
        StackResolutionResult result = null!;
        var modes = original.ModeExecutions!;
        for (var index = original.CompletedModeExecutions; index < modes.Count; index++)
        {
            var execution = modes[index].Execution;
            var child = ActiveSpellMode(original with { CompletedModeExecutions = index }) with {
                DamageAmount = CardBehaviorRegistry.TryGetByEffectKind(execution.EffectKind, out var definition) ? definition.DamageAmount : 0,
                TokenEntryPlan = index == original.CompletedModeExecutions ? original.TokenEntryPlan : null,
                CompletedDeckExecutions = index == original.CompletedModeExecutions ? original.CompletedDeckExecutions : 0,
                DeckChoiceCompleted = index == original.CompletedModeExecutions && original.DeckChoiceCompleted
            };
            // An empty look instruction needs no private choice. Complete it
            // inside this sequence so it cannot finish the source early.
            if (definition is not null && IsDeferredDeckChoice(definition)
                && current.PlayerZones[original.ControllerId].MainDeck.Count == 0)
                child = child with { DeckChoiceCompleted = true, CompletedDeckExecutions = 1 };
            var step = current with { StackItems = current.StackItems.Select(i =>
                i.StackItemId == original.StackItemId ? child : i).ToArray() };
            result = ResolveStackItemEffect(step, child, deferCompletion: index < modes.Count - 1,
                repeatDamageDestroyTargets: damageTargets);
            events.AddRange(result.Events); triggers.AddRange(result.TriggerQueue);
            destroyed.UnionWith(result.DestroyedUnitOwnerIds);
            if (result.PendingCardChoice is not null)
            {
                var suspended = result.StackItems!.Last(i => i.StackItemId == original.StackItemId) with {
                    ModeExecutions = modes, CompletedModeExecutions = index, EffectRepeatCount = original.EffectRepeatCount
                };
                return result with { Events = events, TriggerQueue = triggers, DestroyedUnitOwnerIds = destroyed.ToArray(),
                    StackItems = result.StackItems!.Select(i => i.StackItemId == original.StackItemId ? suspended : i).ToArray() };
            }
            events.Add(new("SPELL_MODE_COMPLETED", "已完成所选模式", new Dictionary<string, object?> {
                ["stackItemId"] = original.StackItemId, ["mode"] = modes[index].Mode, ["executionIndex"] = index }));
            current = current with { PlayerZones = result.PlayerZones, CardObjects = result.CardObjects,
                RunePools = result.RunePools, PlayerScores = result.PlayerScores, PlayerExperience = result.PlayerExperience,
                UntilEndOfTurnEffects = result.UntilEndOfTurnEffects, RngCursor = result.RngCursor,
                StackItems = result.StackItems ?? current.StackItems,
                ObjectLocations = result.ObjectLocations ?? ReconcileObjectLocations(current.ObjectLocations, result.PlayerZones),
                DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(current.DestroyedUnitOwnerIdsThisTurn, result.DestroyedUnitOwnerIds) };
            if (result.WinnerPlayerId is not null) break;
        }
        return result with { Events = events, TriggerQueue = triggers, DestroyedUnitOwnerIds = destroyed.ToArray(),
            StackItems = current.StackItems.Where(i => i.StackItemId != original.StackItemId).ToArray() };
    }
}
