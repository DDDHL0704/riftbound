using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<string> TriggerResolutionOrderIds(MatchState state)
        => BuildLegalResolutionTriggerControllerBlocks(state).SelectMany(block => block.TriggerIds).ToArray();

    internal static IReadOnlyList<IReadOnlyDictionary<string, object?>> TriggerInsertionControllerBlocks(MatchState state)
        => TriggerControllerBlockViews(BuildApnapTriggerControllerBlocks(state));

    internal static string? SoleTriggerChoiceControllerId(MatchState state)
    {
        var controllers = BuildApnapTriggerControllerBlocks(state).Where(block => block.TriggerIds.Count > 1)
            .Select(block => block.ControllerId).Distinct(StringComparer.Ordinal).ToArray();
        return controllers.Length == 1 ? controllers[0] : null;
    }

    // A later command cannot make an earlier earned trigger simultaneous with it.
    // This records command chronology; finer ordering inside one command remains
    // the responsibility of the rule that produces its simultaneous group.
    private static ResolutionResult StampNewTriggerQueueTimes(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var prior = before.TriggerQueue.Select(t => t.TriggerId).ToHashSet(StringComparer.Ordinal);
        var added = result.State.TriggerQueue.Where(t => t.EnqueuedAtTick is null && !prior.Contains(t.TriggerId))
            .Select(t => t.TriggerId).ToHashSet(StringComparer.Ordinal);
        if (added.Count == 0) return result;
        var tick = result.State.Tick;
        var state = result.State with {
            TriggerQueue = result.State.TriggerQueue.Select(t => added.Contains(t.TriggerId)
                ? t with { EnqueuedAtTick = tick } : t).ToArray()
        };
        var events = result.Events.Select(e => e.Kind == "TRIGGER_QUEUED"
            && e.Payload.GetValueOrDefault("triggerId") is string id && added.Contains(id)
                ? e with { Payload = new Dictionary<string, object?>(e.Payload) { ["enqueuedAtTick"] = tick } } : e).ToArray();
        return result with { State = state, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool AreDistinctCommandTriggers(IReadOnlyList<TriggerQueueItemState> triggers)
        => triggers.All(t => t.EnqueuedAtTick is not null)
            && triggers.Select(t => t.EnqueuedAtTick).Distinct().Count() == triggers.Count;
}
