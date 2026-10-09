using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
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
            || state.PendingPayment is not null || state.PendingHandChoice is not null
            || state.PendingCardChoice is not null || state.PendingEffectPlay is not null)
            return result;

        var pending = state.TriggerQueue.Where(IsImmediateTrigger).ToArray();
        if (pending.Length == 0) return result;

        var events = result.Events.ToList();
        if (pending.Length == 1)
        {
            var item = BuildStackItemForOrderedTrigger(state, pending[0]);
            state = state with {
                TriggerQueue = state.TriggerQueue.Where(t => !IsImmediateTrigger(t)).ToArray(),
                StackItems = state.StackItems.Concat([item]).ToArray(),
                PriorityPlayerId = item.ControllerId, ActivePlayerId = item.ControllerId,
                FocusPlayerId = null, PassedPriorityPlayerIds = [],
                TimingState = state.SpellDuelState.IsActive ? TimingStates.SpellDuelClosed : TimingStates.NeutralClosed
            };
            events.Add(new GameEvent("TRIGGERS_MOVED_TO_STACK", "单一触发能力自动加入结算链",
                new Dictionary<string, object?> {
                    ["orderedTriggerIds"] = new[] { pending[0].TriggerId },
                    ["stackItemIds"] = new[] { item.StackItemId }, ["topStackItemId"] = item.StackItemId,
                    ["nextPriorityPlayerId"] = item.ControllerId, ["orderingPolicy"] = "SINGLE_TRIGGER_AUTO_STACK"
                }));
        }
        else
        {
            state = state with { PriorityPlayerId = null, FocusPlayerId = null,
                PassedPriorityPlayerIds = [], TimingState = TimingStates.NeutralClosed };
        }
        return result with { State = state, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}
