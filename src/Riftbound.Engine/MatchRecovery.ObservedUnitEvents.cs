namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateObservedUnitEventHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        ValidateUnitEventPlayTargetHistory(state, commands, events, errors);
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var queued = events.Where(entry => entry.Sequence > boundary && entry.Event.Kind == "TRIGGER_QUEUED"
            && TryReadObjectString(entry.Event.Payload, "observedUnitEventCaptureHash", out _)).ToArray();
        static IEnumerable<(string Id, string Source, string Controller, UnitEventContext Context)> Scope(MatchState? frame)
        {
            if (frame is null) yield break;
            foreach (var trigger in frame.TriggerQueue)
                if (CoreRuleEngine.RequiresObservedUnitEventHistory(trigger.UnitEvent))
                    yield return (trigger.TriggerId, trigger.SourceObjectId, trigger.ControllerId, trigger.UnitEvent!);
            var items = frame.StackItems.Concat(frame.PendingEffectPlay is { } pending ? [pending.Parent] : []);
            foreach (var item in items)
                if (CoreRuleEngine.RequiresObservedUnitEventHistory(item.UnitEvent))
                    yield return (item.StackItemId.StartsWith("ordered-", StringComparison.Ordinal)
                        ? item.StackItemId[8..] : item.StackItemId, item.SourceObjectId, item.ControllerId, item.UnitEvent!);
        }
        var current = Scope(state).Concat(Scope(state.PendingRuleChoice?.Origin)).ToArray();
        if (queued.Length == 0 && current.Length == 0) return;
        foreach (var frame in new MatchState?[] { state, state.PendingRuleChoice?.Origin })
            if (Scope(frame).GroupBy(item => item.Id).Any(group => group.Count() > 1))
                errors.Add("an observed unit event is duplicated across pending locations");
        var retired = events.Where(entry => entry.Sequence > boundary
                && entry.Event.Kind is "STACK_ITEM_RESOLVED" or "STACK_ITEM_COUNTERED" or "TRIGGER_PAYMENT_DECLINED")
            .Select(entry => TryReadObjectString(entry.Event.Payload, "stackItemId", out var id) ? id : null)
            .OfType<string>().Where(id => id.StartsWith("ordered-", StringComparison.Ordinal))
            .Select(id => id[8..]).ToHashSet(StringComparer.Ordinal);
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in queued)
            if (!TryReadObjectString(entry.Event.Payload, "triggerId", out var id) || id is null || !expected.Add(id)
                || !commands.Any(command => command.Accepted && command.CompletedTick == entry.Tick
                    && command.StartedEventSequence < entry.Sequence && command.CompletedEventSequence >= entry.Sequence))
                errors.Add("observed unit event lacks a unique original accepted capture");
        var unconfirmed = events.Where(entry => entry.Sequence > boundary && entry.Event.Kind == "TRIGGER_NOT_CONFIRMED")
            .Select(entry => TryReadObjectString(entry.Event.Payload, "discardedObservedUnitEventHash", out var hash) ? hash : null)
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        retired.UnionWith(expected.Where(id => unconfirmed.Contains(MatchStateHasher.HashValue("ordered-" + id))));
        expected.ExceptWith(retired);
        foreach (var item in current)
        {
            var original = queued.Where(entry => TryReadObjectString(entry.Event.Payload, "triggerId", out var id)
                && id == item.Id).ToArray();
            if (retired.Contains(item.Id) || original.Length != 1 || original[0].Tick != CoreRuleEngine.ObservedUnitEventOccurrenceTick(item.Context)
                || !TryReadObjectString(original[0].Event.Payload, "observedUnitEventCaptureHash", out var hash)
                || hash != CoreRuleEngine.ObservedUnitEventCaptureHash(item.Context, item.Source, item.Controller))
                errors.Add("observed unit event differs from original accepted capture");
        }
        if (state.Status == MatchStatuses.InProgress && !expected.SetEquals(current.Select(item => item.Id)))
            errors.Add("pending observed unit events differ from original accepted completion history");
    }
}
