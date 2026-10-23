using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record DuelChainFocusState(string FocusPlayerId, IReadOnlyList<string> PassedFocusPlayerIds);

public sealed partial class CoreRuleEngine
{
    private static ResolutionResult CaptureDuelChainFocus(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted || result.State.DuelChainFocus is not null || before.StackItems.Count > 0
            || before.TimingState != TimingStates.SpellDuelOpen || before.FocusPlayerId is not { } focus
            || !result.Events.Any(e => e.Kind == "STACK_ITEM_ADDED")) return result;
        // A card or non-resource skill opens this chain: focus passes only after
        // the complete chain, including earned triggers and choices, has finished.
        return result with { State = result.State with { DuelChainFocus = new(NextPlayerIdAfter(before, focus), []) } };
    }

    // Resource skills remain available for payment. Other pending triggers must reach
    // the response/ordering window before the next ordinary action or battlefield task.
    internal static bool IsImmediateTrigger(TriggerQueueItemState trigger) =>
        !IsJhinMovementResourceTrigger(trigger)
        && !string.Equals(trigger.EffectKind, P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityEffectKind, StringComparison.Ordinal);

    private static ResolutionResult CaptureDeathTriggerSources(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var triggers = result.State.TriggerQueue.Select(trigger => CaptureDeathTriggerSource(
            trigger, before.CardObjects, result.State.CardObjects)).ToArray();
        return result with { State = result.State with { TriggerQueue = triggers } };
    }

    private static TriggerQueueItemState CaptureDeathTriggerSource(TriggerQueueItemState trigger,
        IReadOnlyDictionary<string, CardObjectState> before, IReadOnlyDictionary<string, CardObjectState> after)
        => trigger.TriggeredByEventKind == "UNIT_DESTROYED" && trigger.SourceCardNo is null
            ? trigger with { SourceCardNo = before.GetValueOrDefault(trigger.SourceObjectId)?.CardNo
                ?? after.GetValueOrDefault(trigger.SourceObjectId)?.CardNo }
            : trigger;

    private static ResolutionResult PublishPendingTriggers(ResolutionResult result)
    {
        var state = result.State;
        if (!result.Accepted || state.Status != MatchStatuses.InProgress
            || HasObservedConfirmationBeforeTriggers(state)
            || state.PendingPayment is not null || state.PendingHandChoice is not null
            || state.PendingCardChoice is not null || state.PendingEffectPlay is not null)
            return result;

        var pending = state.TriggerQueue.Where(IsImmediateTrigger).ToArray();
        if (pending.Length == 0) return result;

        // CN 340.2.a / 346.1: a trigger-opened chain keeps the existing focus,
        // including when resolution pauses for a choice or a response is added.
        if (state.DuelChainFocus is null && state.StackItems.Count == 0
            && state.SpellDuelState.IsActive && state.FocusPlayerId is { } focus)
            state = state with { DuelChainFocus = new(focus, state.PassedFocusPlayerIds.ToArray()) };

        var events = result.Events.ToList();
        if (pending.Length == 1 || AreDistinctCommandTriggers(pending))
        {
            var admitted = pending.OrderBy(t => t.EnqueuedAtTick).ThenBy(TriggerInstructionOrder).ToArray();
            var items = admitted.Select(t => BuildStackItemForOrderedTrigger(state, t)).ToArray();
            var item = items[^1];
            state = state with {
                TriggerQueue = state.TriggerQueue.Where(t => !IsImmediateTrigger(t)).ToArray(),
                StackItems = state.StackItems.Where(i => !admitted.Any(t => t.PermanentPlayStackItemId == i.StackItemId)).Concat(items).ToArray(),
                PriorityPlayerId = item.ControllerId, ActivePlayerId = item.ControllerId,
                FocusPlayerId = null, PassedPriorityPlayerIds = [],
                TimingState = state.SpellDuelState.IsActive ? TimingStates.SpellDuelClosed : TimingStates.NeutralClosed
            };
            events.Add(new GameEvent("TRIGGERS_MOVED_TO_STACK", "单一触发能力自动加入结算链",
                new Dictionary<string, object?> {
                    ["orderedTriggerIds"] = admitted.Reverse().Select(t => t.TriggerId).ToArray(),
                    ["confirmationTriggerIds"] = admitted.Select(t => t.TriggerId).ToArray(),
                    ["stackItemIds"] = items.Select(i => i.StackItemId).ToArray(), ["topStackItemId"] = item.StackItemId,
                    ["nextPriorityPlayerId"] = item.ControllerId, ["orderingPolicy"] = pending.Length == 1
                        ? "SINGLE_TRIGGER_AUTO_STACK" : "DISTINCT_COMMAND_TRIGGERS_CHRONOLOGICAL_AUTO_STACK"
                }));
        }
        else
        {
            state = state with { PriorityPlayerId = null, FocusPlayerId = null,
                PassedPriorityPlayerIds = [],
                TimingState = state.SpellDuelState.IsActive ? TimingStates.SpellDuelClosed : TimingStates.NeutralClosed };
        }
        return result with { State = state, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static ResolutionResult ResumeDuelChainFocus(ResolutionResult result)
    {
        var state = result.State;
        if (!result.Accepted || state.DuelChainFocus is not { } continuation) return result;
        if (state.Status != MatchStatuses.InProgress || result.Events.Any(e => e.Kind is "SPELL_DUEL_CLOSED" or "BATTLE_CLOSED"))
            state = state with { DuelChainFocus = null };
        else
        {
            var resolving = state.StackItems.Count > 0 || state.TriggerQueue.Any(IsImmediateTrigger)
                || state.PendingHandChoice is not null || state.PendingCardChoice is not null
                || state.PendingPayment is not null || state.PendingEffectPlay is not null
                || state.PendingRuleChoice is not null || HasObservedConfirmationBeforeTriggers(state);
            state = state with {
                TimingState = resolving ? TimingStates.SpellDuelClosed : TimingStates.SpellDuelOpen,
                ActivePlayerId = resolving ? state.ActivePlayerId : continuation.FocusPlayerId,
                FocusPlayerId = resolving ? null : continuation.FocusPlayerId,
                PassedFocusPlayerIds = continuation.PassedFocusPlayerIds,
                PriorityPlayerId = resolving ? state.PriorityPlayerId : null,
                PassedPriorityPlayerIds = resolving ? state.PassedPriorityPlayerIds : [],
                DuelChainFocus = resolving ? continuation : null
            };
        }
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}
