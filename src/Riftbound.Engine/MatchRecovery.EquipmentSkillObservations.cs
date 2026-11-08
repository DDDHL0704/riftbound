using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateEquipmentSkillObservationHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var queued = events.Where(e => e.Event.Kind == "TRIGGER_QUEUED"
            && TryReadObjectString(e.Event.Payload, "equipmentSkillTriggerHash", out _)).ToArray();
        IEnumerable<(string Id, string Source, string Controller, UnitEventContext Context)> Scope(MatchState? value)
        {
            if (value is null) yield break;
            foreach (var trigger in (value.TriggerQueue ?? []).OfType<TriggerQueueItemState>())
                if (trigger.UnitEvent?.EquipmentSkillTriggerHash is not null)
                    yield return (trigger.TriggerId, trigger.SourceObjectId, trigger.ControllerId, trigger.UnitEvent);
            var items = (value.StackItems ?? []).OfType<StackItemState>().Concat(
                value.PendingEffectPlay?.Parent is { } parent ? [parent] : []);
            foreach (var item in items)
                if (item.UnitEvent?.EquipmentSkillTriggerHash is not null)
                    yield return (item.StackItemId.StartsWith("ordered-", StringComparison.Ordinal)
                        ? item.StackItemId[8..] : item.StackItemId, item.SourceObjectId, item.ControllerId, item.UnitEvent);
        }
        var current = Scope(state).Concat(Scope(state.PendingRuleChoice?.Origin)).ToArray();
        if (queued.Length == 0 && current.Length == 0) return;
        foreach (var scope in new MatchState?[] { state, state.PendingRuleChoice?.Origin })
            if (Scope(scope).GroupBy(value => value.Context.EquipmentSkillTriggerHash)
                .Any(group => group.Count() > 1))
                errors.Add("an equipment skill observation is duplicated across live pending locations");
        var retired = events.Where(e => e.Event.Kind is "STACK_ITEM_RESOLVED" or "STACK_ITEM_COUNTERED")
            .Select(e => TryReadObjectString(e.Event.Payload, "stackItemId", out var id) ? id : null)
            .OfType<string>().Where(id => id.StartsWith("ordered-", StringComparison.Ordinal))
            .Select(id => MatchStateHasher.HashValue(id[8..])).ToHashSet(StringComparer.Ordinal);
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in queued)
        {
            if (!TryReadObjectString(entry.Event.Payload, "equipmentSkillTriggerHash", out var key) || key is null
                || !TryReadObjectString(entry.Event.Payload, "equipmentSkillObservationHash", out _)
                || !commands.Any(command => command.Accepted && command.CompletedTick == entry.Tick
                    && command.StartedEventSequence < entry.Sequence && command.CompletedEventSequence >= entry.Sequence)
                || !expected.Add(key))
                errors.Add("equipment skill observer capture lacks a unique original accepted command");
        }
        expected.ExceptWith(retired);
        foreach (var value in current)
        {
            var key = value.Context.EquipmentSkillTriggerHash!;
            var proof = CoreRuleEngine.EquipmentSkillObservationHash(value.Context, value.Source, value.Controller);
            var original = queued.Where(entry => TryReadObjectString(entry.Event.Payload, "equipmentSkillTriggerHash", out var recorded)
                && recorded == key).ToArray();
            if (key != MatchStateHasher.HashValue(value.Id) || retired.Contains(key) || original.Length != 1
                || original[0].Tick != value.Context.CapturedTick
                || !TryReadObjectString(original[0].Event.Payload, "equipmentSkillObservationHash", out var recordedProof)
                || proof != recordedProof)
                errors.Add("equipment skill observer differs from its original accepted completion capture");
        }
        if (state.Status == MatchStatuses.InProgress
            && !expected.SetEquals(current.Select(value => value.Context.EquipmentSkillTriggerHash!)))
            errors.Add("pending equipment skill observers differ from original accepted queue and completion history");
    }
}
