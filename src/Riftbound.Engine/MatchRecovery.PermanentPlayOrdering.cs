namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    // A well-formed surviving companion cannot prove that its deferred
    // receipt was not deleted. Compare the pending set with the original
    // accepted captures and admissions, including a suspended origin.
    private static void ValidatePermanentPlayOrderingHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var frames = new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>().ToArray();
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var records = events.Where(record => record.Sequence > boundary
            && record.Event.Kind is "TRIGGER_ORDERING_DEFERRED" or "TRIGGERS_MOVED_TO_STACK")
            .OrderBy(record => record.Sequence).ToArray();
        if (!frames.Any(frame => frame.TriggerQueue.Any(trigger => trigger.PermanentPlayStackItemId is not null))
            && !records.Any(record => record.Event.Kind == "TRIGGER_ORDERING_DEFERRED")) return;
        bool Accepted(RecoveredEvent record) => commands.Any(command => command.Accepted
            && command.CompletedTick == record.Tick && command.StartedEventSequence < record.Sequence
            && command.CompletedEventSequence >= record.Sequence);
        foreach (var frame in frames)
        {
            var captures = new Dictionary<string, (string StackId, string SourceId, long Tick)>(StringComparer.Ordinal);
            var pending = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records.Where(record => record.Tick <= frame.Tick))
            {
                if (record.Event.Kind == "TRIGGER_ORDERING_DEFERRED")
                {
                    if (!Accepted(record)
                        || !TryReadObjectString(record.Event.Payload, "triggerId", out var id) || id is null
                        || !TryReadObjectString(record.Event.Payload, "stackItemId", out var stackId) || string.IsNullOrWhiteSpace(stackId)
                        || !TryReadObjectString(record.Event.Payload, "sourceObjectId", out var sourceId) || string.IsNullOrWhiteSpace(sourceId)
                        || id != "permanent-play-order-" + stackId
                        || !captures.TryAdd(id, (stackId, sourceId, record.Tick)) || !pending.Add(id))
                        errors.Add("permanent play ordering receipt lacks its original accepted capture");
                }
                else if (TryReadObjectStringList(record.Event.Payload, "orderedTriggerIds", out var ids))
                {
                    foreach (var id in ids.Where(id => id.StartsWith("permanent-play-order-", StringComparison.Ordinal)))
                        if (!Accepted(record) || !pending.Remove(id))
                            errors.Add("permanent play ordering admission lacks a pending original capture");
                }
            }
            var queued = frame.TriggerQueue.Where(trigger => trigger.PermanentPlayStackItemId is not null).ToArray();
            foreach (var trigger in queued)
                if (!captures.TryGetValue(trigger.TriggerId, out var capture)
                    || capture.StackId != trigger.PermanentPlayStackItemId || capture.SourceId != trigger.SourceObjectId
                    || capture.Tick != trigger.EnqueuedAtTick)
                    errors.Add("deferred permanent play ordering differs from its original accepted capture");
            if (frame.Status == MatchStatuses.InProgress
                && (!pending.SetEquals(queued.Select(trigger => trigger.TriggerId)) || pending.Count != queued.Length))
                errors.Add("pending permanent play ordering differs from original capture and admission history");
        }
    }
}
