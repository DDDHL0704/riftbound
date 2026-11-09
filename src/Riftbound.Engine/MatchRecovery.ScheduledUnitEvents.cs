namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateScheduledUnitEventHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var pending = new Dictionary<string, (string Hash, long Tick)>(StringComparer.Ordinal);
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        foreach (var record in events.Where(record => record.Sequence > boundary))
        {
            if (record.Event.Kind is not ("DELAYED_UNIT_EVENT_CREATED" or "DELAYED_UNIT_EVENT_TRIGGERED")) continue;
            if (!commands.Any(command => command.Accepted && command.CompletedTick == record.Tick
                    && command.StartedEventSequence < record.Sequence && command.CompletedEventSequence >= record.Sequence))
            { errors.Add("scheduled unit event lacks original accepted creation or occurrence"); continue; }
            if (!TryReadObjectString(record.Event.Payload, "scheduleId", out var id) || string.IsNullOrWhiteSpace(id))
            { errors.Add("missing original scheduled unit-event identity"); continue; }
            if (record.Event.Kind == "DELAYED_UNIT_EVENT_TRIGGERED")
            {
                if (!pending.Remove(id)) errors.Add("scheduled unit event fired without its original creation");
                continue;
            }
            if (!TryReadObjectString(record.Event.Payload, "scheduledUnitEventHash", out var hash)
                || string.IsNullOrWhiteSpace(hash) || !pending.TryAdd(id, (hash, record.Tick)))
                errors.Add("invalid or duplicated original scheduled unit-event record");
        }
        var actual = state.ScheduledUnitEvents ?? [];
        if (actual.Count != pending.Count || actual.Any(schedule => schedule?.Receipt is not { } receipt
                || !pending.TryGetValue(receipt.Id, out var original) || receipt.CreatedTick != original.Tick
                || MatchStateHasher.HashValue(schedule) != original.Hash))
            errors.Add("pending scheduled unit events differ from original accepted history");
    }
}
