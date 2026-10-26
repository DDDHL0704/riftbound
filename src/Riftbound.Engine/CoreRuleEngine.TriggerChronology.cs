using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<string> TriggerResolutionOrderIds(MatchState state)
        => BuildLegalResolutionTriggerControllerBlocks(state).SelectMany(block => block.TriggerIds).ToArray();

    internal static IReadOnlyList<IReadOnlyDictionary<string, object?>> TriggerInsertionControllerBlocks(MatchState state)
        => TriggerControllerBlockViews(BuildApnapTriggerControllerBlocks(state));

    internal static IReadOnlyList<string> TriggerChoiceControllerIds(MatchState state)
    {
        var controllers = BuildApnapTriggerControllerBlocks(state).Where(block => block.TriggerIds.Count > 1)
            .Select(block => block.ControllerId).Distinct(StringComparer.Ordinal).ToArray();
        return OrderTriggerApnapControllerIds(state, controllers);
    }

    internal static string? NextTriggerChoiceControllerId(MatchState state)
        => TriggerChoiceControllerIds(state).FirstOrDefault(controller => state.TriggerQueue
            .Any(t => t.ControllerId == controller && !t.ControllerOrderConfirmed));

    // A later command cannot make an earlier earned trigger simultaneous with it.
    // This records command chronology; finer ordering inside one command remains
    // the responsibility of the rule that produces its simultaneous group.
    private static ResolutionResult StampNewTriggerQueueTimes(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var prior = before.TriggerQueue.Select(t => t.TriggerId).ToHashSet(StringComparer.Ordinal);
        var added = result.State.TriggerQueue.Where(t => t.EnqueuedAtTick is null && !prior.Contains(t.TriggerId))
            .Select(t => t.TriggerId).ToHashSet(StringComparer.Ordinal);
        // Replaying a suspended parent still evaluates its original state;
        // newly earned skills belong to this accepted completion, while
        // confirmed resource children keep their earlier recorded ticks.
        var tick = RuleChoices.Value?.CompletionTick ?? result.State.Tick;
        var queue = result.State.TriggerQueue.Select(t => added.Contains(t.TriggerId)
                ? t with { EnqueuedAtTick = tick } : t)
            .OrderBy(t => t.EnqueuedAtTick).ToArray();
        if (added.Count == 0 && result.State.TriggerQueue.SequenceEqual(queue)) return result;
        var state = result.State with {
            TriggerQueue = queue
        };
        var events = result.Events.Select(e => e.Kind == "TRIGGER_QUEUED"
            && e.Payload.GetValueOrDefault("triggerId") is string id && added.Contains(id)
                ? e with { Payload = new Dictionary<string, object?>(e.Payload) { ["enqueuedAtTick"] = tick } } : e).ToArray();
        return result with { State = state, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    // Combat deaths occur before the later result-determination instruction
    // (CN 467.3, 468.1, 468.3). Sharing a command tick does not make them
    // simultaneous. Within either instruction the ordinary APNAP rule applies.
    private static int TriggerInstructionOrder(TriggerQueueItemState trigger)
        => trigger.TriggeredByEventKind == "BATTLE_RESULT_DETERMINED" ? 1 : 0;

    private static bool AreDistinctCommandTriggers(IReadOnlyList<TriggerQueueItemState> triggers)
        => triggers.All(t => t.EnqueuedAtTick is not null)
            && triggers.Select(t => (t.EnqueuedAtTick, Instruction: TriggerInstructionOrder(t))).Distinct().Count() == triggers.Count;
}
