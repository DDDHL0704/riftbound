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
        SpellExecutionState Capture(CardBehaviorDefinition definition, IReadOnlyList<string> ids, IReadOnlyList<string>? destinations, string? destination)
            => new(definition.EffectKind, ids, ids.Distinct(StringComparer.Ordinal)
                .Where(state.CardObjects.ContainsKey).ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration), destinations, NormalizeRepeatDestination(destination),
                definition.MovesTargetsToChosenLocation ? intent.PlayerId : null);
        var result = new List<SpellExecutionState> { Capture(behavior, targets, command.MovementDestinations, command.Destination) };
        foreach (var choice in choices)
        {
            if (choice is null || choice.TargetObjectIds is null) return false;
            var repeatCommand = command with { Mode = choice.Mode, TargetObjectIds = choice.TargetObjectIds, Destination = choice.Destination ?? command.Destination, MovementDestinations = choice.MovementDestinations ?? command.MovementDestinations, RepeatChoices = null };
            // Validate with the same authoritative target rules as the initial play.
            // A quote may be short of resources; resource authorization belongs to
            // the combined plan after all target taxes have been counted.
            if (!TryBuildPlayCardPlan(state, intent, repeatCommand, out var repeatPlan, out var rejection,
                    includeRejectionProjections: false, validateRepeats: false)
                && (repeatPlan is null || rejection.ErrorCode != ErrorCodes.InsufficientCost)) return false;
            if (!SupportsSeparateExecution(repeatPlan.Behavior)) return false;
            result.Add(Capture(repeatPlan.Behavior, repeatPlan.TargetObjectIds, repeatCommand.MovementDestinations, repeatCommand.Destination));
        }
        executions = result;
        return true;
    }

    private static string NormalizeRepeatDestination(string? destination)
        => destination?.Trim() is { Length: > 0 } value && value != "BASE" ? value : string.Empty;

    // A response can change control, location or target legality. Recovery checks
    // the declaration structure, not the current legality of executing the move.
    internal static bool ValidRepeatDestination(MatchState state, string controller, SpellExecutionState execution)
    {
        if (!CardBehaviorRegistry.TryGetByEffectKind(execution.EffectKind, out var behavior)) return false;
        var destination = execution.Destination;
        if (destination is null || destination != NormalizeRepeatDestination(destination)) return false;
        if (!behavior.MovesTargetsToChosenLocation && behavior.CommandedMovement is null) return destination.Length == 0;
        if (behavior.MovesTargetsToChosenLocation && (execution.MovementDeclarationPlayerId is null
            || !state.Seats.ContainsKey(execution.MovementDeclarationPlayerId))) return false;
        if (destination.Length == 0) return behavior.MinTargetCount == 0 && execution.TargetObjectIds.Count == 0;
        if (destination.StartsWith("BASE:", StringComparison.Ordinal))
            return behavior.MovesTargetsToChosenLocation && state.Seats.ContainsKey(destination[5..])
                && (execution.TargetObjectIds.Count == 0 || behavior.TargetScope == CardTargetScopes.AnyUnit
                    || (behavior.TargetScope is CardTargetScopes.FriendlyUnit
                    or CardTargetScopes.FriendlyUnitThenFriendlyEquipment
                    ? destination[5..] == execution.MovementDeclarationPlayerId : destination[5..] != execution.MovementDeclarationPlayerId));
        return destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(destination[12..], out var battlefield)
            && battlefield.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag);
    }

    internal static bool SupportsSeparateExecution(CardBehaviorDefinition behavior)
        => !behavior.PlaysSourceToBaseAsUnit && !behavior.PlaysSourceToBaseAsEquipment
            && behavior.HandChoice is null && behavior.MainDeckLookCount == 0 && string.IsNullOrEmpty(behavior.EffectPlaySourceZone)
            && !behavior.GainsControlOfTargetStackSpell && (!behavior.PerformsInsight || SupportsTargetedInsightRepeat(behavior))
            // Pure counters and Nightfall's controller restriction have completed the repeat audit.
            // Other counters still need their follow-up choices, restrictions,
            // ability targets or optional payments checked before charging Echo.
            && (!behavior.CountersTargetStackSpell || SupportsTargetedInsightRepeat(behavior)
                || behavior.CounterUnlessControllerPaysMana > 0
                || behavior.EffectKind is "WIND_WALL_COUNTER_SPELL" or "NIGHTFALL_LULLABY_COUNTER_SPELL_AND_SPELL_LOCK");

    private static StackResolutionResult ResolveSeparateSpellExecutions(MatchState state, StackItemState original)
    {
        var current = state;
        var events = new List<GameEvent>();
        var triggers = new List<TriggerQueueItemState>();
        var destroyed = new List<string>();
        var countered = new List<string>();
        StackResolutionResult result = null!;
        var executions = original.RepeatExecutions!;
        if (original.SourceReplayDecisionCompleted && original.CompletedRepeatExecutions >= executions.Count)
            return CompleteSourceReplayDecision(state, original);
        var damageDestroyTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var index = original.CompletedRepeatExecutions; index < executions.Count; index++)
        {
            var execution = executions[index];
            var item = original with { EffectKind = execution.EffectKind, TargetObjectIds = execution.TargetObjectIds,
                TargetGenerations = execution.TargetGenerations, Destination = execution.Destination, MovementDestinations = execution.MovementDestinations, EffectRepeatCount = 1, RepeatExecutions = null, PlayCost = null, AfterPlayRecycle = null, CompletedRepeatExecutions = CardBehaviorRegistry.TryGetByEffectKind(execution.EffectKind, out var repeatedBehavior)
                    && (repeatedBehavior.OptionalEquipmentAfterMovement || repeatedBehavior.CreatesGoldWhenTargetDestroyedThisTurn || repeatedBehavior.FriendlyBoonBonusUntilEndOfTurn > 0 || repeatedBehavior.DamageAlternativeDrawCount > 0 || repeatedBehavior.TurnUnitPlayGrantKind.Length > 0) ? index : 0,
                ResolutionDamageChoice = original.ResolutionDamageChoice?.ExecutionIndex == index ? original.ResolutionDamageChoice : null,
                MovementFollowUp = index == original.CompletedRepeatExecutions ? original.MovementFollowUp : null,
                SourceReplayDecisionCompleted = false,
                TokenEntryPlan = index == original.CompletedRepeatExecutions ? original.TokenEntryPlan : null };
            result = ResolveStackItemEffect(current, item, deferCompletion: index < executions.Count - 1, repeatDamageDestroyTargets: damageDestroyTargets);
            events.AddRange(result.Events);
            triggers.AddRange(result.TriggerQueue);
            destroyed.AddRange(result.DestroyedUnitOwnerIds);
            countered.AddRange(result.CounteredStackItemIds);
            if (result.PendingEffectPlay is { SourceReplay: { } c } replay)
            {
                var suspended = original with { CompletedRepeatExecutions = index + 1, SourceReplay = c.Progress, SourceReplayDecisionCompleted = false };
                return result with { Events = events, TriggerQueue = triggers, PendingEffectPlay = replay with { Parent = suspended },
                    DestroyedUnitOwnerIds = destroyed.Distinct(StringComparer.Ordinal).ToArray(), CounteredStackItemIds = countered };
            }
            if (result.PendingCardChoice?.ChoiceWindow == MovementAttachmentWindow)
            {
                var suspended = original with { CompletedRepeatExecutions = index, MovementFollowUp = result.StackItems![^1].MovementFollowUp };
                return result with { Events = events, TriggerQueue = triggers,
                    DestroyedUnitOwnerIds = destroyed.Distinct(StringComparer.Ordinal).ToArray(), CounteredStackItemIds = countered,
                    StackItems = current.StackItems.Where(i => i.StackItemId != original.StackItemId).Append(suspended).ToArray() };
            }
            if (result.PendingCardChoice?.ChoiceWindow == ResolutionDamageChoiceWindow)
            {
                var suspended = original with { CompletedRepeatExecutions = index,
                    ResolutionDamageChoice = result.StackItems![^1].ResolutionDamageChoice };
                return result with { Events = events, TriggerQueue = triggers,
                    DestroyedUnitOwnerIds = destroyed.Distinct(StringComparer.Ordinal).ToArray(), CounteredStackItemIds = countered,
                    StackItems = current.StackItems.Where(i => i.StackItemId != original.StackItemId).Append(suspended).ToArray() };
            }
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
