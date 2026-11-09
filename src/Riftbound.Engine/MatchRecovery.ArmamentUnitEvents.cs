namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateArmamentUnitEventHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var captured = state.StackItems.Select(i => (Id: i.StackItemId, Source: i.SourceObjectId, Controller: i.ControllerId, Context: i.UnitEvent, IsStack: true))
            .Concat(state.TriggerQueue.Select(i => (Id: i.TriggerId, Source: i.SourceObjectId, Controller: i.ControllerId, Context: i.UnitEvent, IsStack: false)));
        if (state.PendingEffectPlay?.Parent is { } parent)
            captured = captured.Append((Id: parent.StackItemId, Source: parent.SourceObjectId, Controller: parent.ControllerId, Context: parent.UnitEvent, IsStack: true));
        foreach (var item in captured) {
            var queued = events.Where(e => e.Event.Kind == "TRIGGER_QUEUED"
                && TryReadObjectString(e.Event.Payload, "triggerId", out var id)
                && (item.IsStack ? "ordered-" + id : id) == item.Id).ToArray();
            var isArmament = item.Context?.Origin?.ArmamentGranted == true
                || item.Context is not null && ArmamentUnitEventRules.Spec(item.Context.CardNo) is not null
                || queued.Any(e => TryReadObjectString(e.Event.Payload, "armamentGrantHash", out _));
            if (!isArmament) continue;
            if (item.Context is not { Origin.ArmamentGranted: true } context || queued.Length != 1
                || !TryReadObjectString(queued[0].Event.Payload, "armamentGrantHash", out var hash)
                || hash != ArmamentUnitEventRules.CaptureHash(context)
                || !TryReadObjectString(queued[0].Event.Payload, "sourceObjectId", out var source) || source != item.Source
                || !TryReadObjectString(queued[0].Event.Payload, "controllerId", out var controller) || controller != item.Controller
                || item.IsStack && !events.Any(e => e.Sequence > queued[0].Sequence && e.Event.Kind == "TRIGGERS_MOVED_TO_STACK"
                    && TryReadObjectStringList(e.Event.Payload, "stackItemIds", out var ids) && ids.Contains(item.Id))
                || queued[0].Tick != CoreRuleEngine.ObservedUnitEventOccurrenceTick(context)
                || !commands.Any(c => c.Accepted && c.CompletedTick == queued[0].Tick
                    && c.StartedEventSequence < queued[0].Sequence && queued[0].Sequence <= c.CompletedEventSequence))
            {
                var hasHash = queued.Length == 1 && TryReadObjectString(queued[0].Event.Payload, "armamentGrantHash", out var storedHash)
                    && item.Context is not null && storedHash == ArmamentUnitEventRules.CaptureHash(item.Context);
                errors.Add($"captured armament unit skill differs from its original accepted grant event (queue={queued.Length}, hash={hasHash}, captured={item.Context?.CapturedTick}, queued={queued.FirstOrDefault()?.Tick})");
            }
        }
    }
}
