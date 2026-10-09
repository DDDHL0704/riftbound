using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool TryBuildRepeatExecutions(MatchState state, PlayerIntent intent, PlayCardCommand command,
        CardBehaviorDefinition behavior, IReadOnlyList<string> targets, int count,
        out IReadOnlyList<SpellExecutionState>? executions, out string error)
    {
        executions = null;
        error = "回响的逐次选择数量、模式或目标不合法。";
        if (count < 2 || !SupportsSeparateExecution(behavior))
            return command.RepeatChoices is not { Count: > 0 };
        var choices = command.RepeatChoices is { Count: > 0 } supplied ? supplied
            : Enumerable.Range(1, count - 1).Select(_ => new SpellRepeatChoice(command.Mode, targets)).ToArray();
        if (choices.Count != count - 1) return false;
        SpellExecutionState Capture(CardBehaviorDefinition definition, IReadOnlyList<string> ids)
            => new(definition.EffectKind, ids, ids.Distinct(StringComparer.Ordinal)
                .Where(state.CardObjects.ContainsKey).ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration));
        var result = new List<SpellExecutionState> { Capture(behavior, targets) };
        foreach (var choice in choices)
        {
            if (choice is null || choice.TargetObjectIds is null) return false;
            var repeatCommand = command with { Mode = choice.Mode, TargetObjectIds = choice.TargetObjectIds, RepeatChoices = null };
            // Validate with the same authoritative target rules as the initial play.
            // A quote may be short of resources; resource authorization belongs to
            // the combined plan after all target taxes have been counted.
            if (!TryBuildPlayCardPlan(state, intent, repeatCommand, out var repeatPlan, out var rejection,
                    includeRejectionProjections: false, validateRepeats: false)
                && (repeatPlan is null || rejection.ErrorCode != ErrorCodes.InsufficientCost)) return false;
            if (!SupportsSeparateExecution(repeatPlan.Behavior)) return false;
            result.Add(Capture(repeatPlan.Behavior, repeatPlan.TargetObjectIds));
        }
        executions = result;
        return true;
    }

    internal static bool SupportsSeparateExecution(CardBehaviorDefinition behavior)
        => !behavior.PlaysSourceToBaseAsUnit && !behavior.PlaysSourceToBaseAsEquipment
            && behavior.HandChoice is null && behavior.MainDeckLookCount == 0 && string.IsNullOrEmpty(behavior.EffectPlaySourceZone)
            && !behavior.GainsControlOfTargetStackSpell && !behavior.PerformsInsight
            // Pure counters and Nightfall's controller restriction have completed the repeat audit.
            // Other counters still need their follow-up choices, restrictions,
            // ability targets or optional payments checked before charging Echo.
            && (!behavior.CountersTargetStackSpell || behavior.EffectKind is "WIND_WALL_COUNTER_SPELL" or "NIGHTFALL_LULLABY_COUNTER_SPELL_AND_SPELL_LOCK");

    private static StackResolutionResult ResolveSeparateSpellExecutions(MatchState state, StackItemState original)
    {
        var current = state;
        var events = new List<GameEvent>();
        var triggers = new List<TriggerQueueItemState>();
        var destroyed = new List<string>();
        var countered = new List<string>();
        StackResolutionResult result = null!;
        var executions = original.RepeatExecutions!;
        var damageDestroyTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var index = original.CompletedRepeatExecutions; index < executions.Count; index++)
        {
            var execution = executions[index];
            var item = original with { EffectKind = execution.EffectKind, TargetObjectIds = execution.TargetObjectIds,
                TargetGenerations = execution.TargetGenerations, EffectRepeatCount = 1, RepeatExecutions = null, PlayCost = null, AfterPlayRecycle = null, CompletedRepeatExecutions = 0,
                TokenEntryPlan = index == original.CompletedRepeatExecutions ? original.TokenEntryPlan : null };
            result = ResolveStackItemEffect(current, item, deferCompletion: index < executions.Count - 1, repeatDamageDestroyTargets: damageDestroyTargets);
            events.AddRange(result.Events);
            triggers.AddRange(result.TriggerQueue);
            destroyed.AddRange(result.DestroyedUnitOwnerIds);
            countered.AddRange(result.CounteredStackItemIds);
            if (result.PendingCardChoice?.ChoiceWindow == TokenReplacementWindow)
            {
                var suspended = original with { CompletedRepeatExecutions = index, TokenEntryPlan = result.StackItems![^1].TokenEntryPlan };
                return result with { Events = events, TriggerQueue = triggers,
                    PendingCardChoice = TokenReplacementChoice(current, suspended, suspended.TokenEntryPlan!),
                    DestroyedUnitOwnerIds = destroyed.Distinct(StringComparer.Ordinal).ToArray(), CounteredStackItemIds = countered,
                    StackItems = current.StackItems.Where(i => i.StackItemId != original.StackItemId).Append(suspended).ToArray() };
            }
            events.Add(new("SPELL_EXECUTION_COMPLETED", $"第 {index + 1} 次法术效果完成", new Dictionary<string, object?>
            {
                ["stackItemId"] = original.StackItemId, ["executionIndex"] = index,
                ["effectKind"] = execution.EffectKind, ["targetObjectIds"] = execution.TargetObjectIds
            }));
            current = current with
            {
                PlayerZones = result.PlayerZones, CardObjects = result.CardObjects, RunePools = result.RunePools,
                PlayerScores = result.PlayerScores, PlayerExperience = result.PlayerExperience,
                UntilEndOfTurnEffects = result.UntilEndOfTurnEffects, RngCursor = result.RngCursor,
                StackItems = RemoveCounteredStackItems(result.StackItems ?? current.StackItems, result.CounteredStackItemIds),
                ObjectLocations = result.ObjectLocations ?? ReconcileObjectLocations(current.ObjectLocations, result.PlayerZones),
                DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(current.DestroyedUnitOwnerIdsThisTurn, result.DestroyedUnitOwnerIds)
            };
            if (result.WinnerPlayerId is not null) break;
        }
        return result with { Events = events, TriggerQueue = triggers,
            DestroyedUnitOwnerIds = destroyed.Distinct(StringComparer.Ordinal).ToArray(), CounteredStackItemIds = countered,
            StackItems = current.StackItems.Where(item => item.StackItemId != original.StackItemId).ToArray() };
    }
}
