namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateInstructionCostHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var captured = state.StackItems.Select(i => (Id: i.StackItemId, Source: i.SourceObjectId,
                Controller: i.ControllerId, Context: i.UnitEvent, Receipt: i.TriggerCost, IsStack: true))
            .Concat(state.TriggerQueue.Select(i => (Id: i.TriggerId, Source: i.SourceObjectId,
                Controller: i.ControllerId, Context: i.UnitEvent, Receipt: (TriggerCostReceipt?)null, IsStack: false)));
        if (state.PendingEffectPlay?.Parent is { } parent)
            captured = captured.Append((parent.StackItemId, parent.SourceObjectId, parent.ControllerId,
                parent.UnitEvent, parent.TriggerCost, true));
        foreach (var item in captured)
        {
            var queued = events.Where(e => e.Event.Kind == "TRIGGER_QUEUED"
                && TryReadObjectString(e.Event.Payload, "triggerId", out var id)
                && (item.IsStack ? "ordered-" + id : id) == item.Id).ToArray();
            if (!CoreRuleEngine.RequiresInstructionCostHistory(item.Context)
                && !queued.Any(e => TryReadObjectString(e.Event.Payload, "instructionCostCaptureHash", out _))) continue;
            if (item.Context is not { } context || queued.Length != 1
                || !TryReadObjectString(queued[0].Event.Payload, "instructionCostCaptureHash", out var captureHash)
                || captureHash != CoreRuleEngine.InstructionCostCaptureHash(context, item.Source, item.Controller)
                || queued[0].Tick != context.CapturedTick
                || !commands.Any(c => c.Accepted && c.CompletedTick == queued[0].Tick
                    && c.StartedEventSequence < queued[0].Sequence && queued[0].Sequence <= c.CompletedEventSequence))
                errors.Add("instruction cost skill differs from its original accepted capture");

            var paid = events.Where(e => e.Event.Kind == "TRIGGER_CONFIRMED"
                && TryReadObjectString(e.Event.Payload, "stackItemId", out var id) && id == item.Id
                && TryReadObjectString(e.Event.Payload, "destructionCostReceiptHash", out _)).ToArray();
            if (item.Receipt?.DestroyedPermanentManaCosts is null && paid.Length == 0) continue;
            if (item.Receipt?.DestroyedPermanentManaCosts is null || paid.Length != 1
                || !TryReadObjectString(paid[0].Event.Payload, "destructionCostReceiptHash", out var receiptHash)
                || receiptHash != CoreRuleEngine.DestructionCostReceiptHash(item.Receipt)
                || !commands.Any(c => c.Accepted && c.PlayerId == item.Controller && c.CompletedTick == paid[0].Tick
                    && c.StartedEventSequence < paid[0].Sequence && paid[0].Sequence <= c.CompletedEventSequence))
                errors.Add("destruction cost receipt differs from its original accepted payment");
        }
    }
}
