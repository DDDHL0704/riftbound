namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool IsUnitConquestReturnEffect(string? effect)
        => UnitConquestTriggerSpecRules.TryGetTriggerByEffectKind(effect,
            UnitConquestTriggerSpecRules.IsUnitConquestPayReturnSelfToHandTrigger, out _);

    private static bool ValidUnitConquestReturnQueue(MatchState state, TriggerQueueItemState trigger)
    {
        if (trigger.UnitConquestReturn is not { } context
            || !CoreRuleEngine.ValidUnitConquestReturnContext(state, context, trigger.EffectKind,
                trigger.SourceObjectId, trigger.ControllerId, trigger.SourceCardNo)
            || trigger.EnqueuedAtTick != context.ConquestTick || trigger.TriggeredByEventKind != "BATTLEFIELD_CONQUERED"
            || trigger.TriggerId != CoreRuleEngine.UnitConquestReturnTriggerId(context)) return false;
        return MatchStateHasher.HashValue(trigger) == MatchStateHasher.HashValue(new TriggerQueueItemState(
            trigger.TriggerId, trigger.ControllerId, trigger.SourceObjectId, trigger.EffectKind,
            trigger.TriggeredByEventKind, trigger.TimingContext) { EnqueuedAtTick = trigger.EnqueuedAtTick,
            ControllerOrderConfirmed = trigger.ControllerOrderConfirmed, SourceCardNo = trigger.SourceCardNo,
            UnitConquestReturn = context });
    }

    private static bool ValidUnitConquestReturnStack(MatchState state, StackItemState item)
    {
        if (item.UnitConquestReturn is not { } context
            || !CoreRuleEngine.ValidUnitConquestReturnContext(state, context, item.EffectKind,
                item.SourceObjectId, item.ControllerId, item.CardNo)
            || item.StackItemId != "ordered-" + CoreRuleEngine.UnitConquestReturnTriggerId(context)) return false;
        return MatchStateHasher.HashValue(item) == MatchStateHasher.HashValue(new StackItemState(
            item.StackItemId, item.ControllerId, item.SourceObjectId, item.EffectKind, item.CardNo,
            timingContext: item.TimingContext) { UnitConquestReturn = context, TriggerCost = item.TriggerCost });
    }

    private static void ValidateUnitConquestReturnContinuations(MatchState state, List<string> errors)
    {
        foreach (var trigger in state.TriggerQueue)
            if (trigger.UnitConquestReturn is not null ? !ValidUnitConquestReturnQueue(state, trigger)
                : IsUnitConquestReturnEffect(trigger.EffectKind))
                errors.Add("invalid captured unit conquest return queue");
        foreach (var item in state.StackItems)
            if (item.UnitConquestReturn is not null ? !ValidUnitConquestReturnStack(state, item)
                : IsUnitConquestReturnEffect(item.EffectKind))
                errors.Add("invalid captured unit conquest return stack");
    }

    private static bool IsUnitConquestReturnPayment(MatchState state, PendingPaymentState payment)
        => payment.PaymentWindow == "TRIGGER_PAYMENT" && payment.ResolvingStackItemId is { } id
            && state.StackItems.Any(item => item.StackItemId == id && item.UnitConquestReturn is not null);

    private static bool HasValidatedPublicUnitConquestReturnEffect(MatchState? state, string? triggerId, string? effect)
        => state is not null && state.TriggerQueue.Any(trigger => trigger.TriggerId == triggerId
            && trigger.EffectKind == effect && ValidUnitConquestReturnQueue(state, trigger));

    private static bool HasValidatedUnitConquestReturnQueueSource(MatchState? state, string? triggerId,
        string? controller, string? source, string? effect)
        => state is not null && state.TriggerQueue.Any(trigger => trigger.TriggerId == triggerId
            && trigger.ControllerId == controller && trigger.SourceObjectId == source && trigger.EffectKind == effect
            && ValidUnitConquestReturnQueue(state, trigger));

    // The authoritative binding may identify the object's later private incarnation.
    private static void ValidateUnitConquestReturnPrivateContextAbsent(object? payload, string label, List<string> errors)
    {
        if (EnumerateSnapshotPayloadObjectPropertyNames(payload)
            .Any(name => string.Equals(name, "unitConquestReturn", StringComparison.OrdinalIgnoreCase)))
            errors.Add($"{label} must not expose the private unit conquest return binding");
    }

    private static void ValidateUnitConquestReturnHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var pending = state.TriggerQueue.Select(t => (Context: t.UnitConquestReturn, t.EffectKind))
            .Concat(state.StackItems.Select(i => (Context: i.UnitConquestReturn, i.EffectKind)))
            .Where(entry => entry.Context is not null);
        foreach (var entry in pending)
        {
            var context = entry.Context!;
            if (context.Source is null)
            {
                errors.Add("captured unit conquest return has no source binding");
                continue;
            }
            var triggerId = CoreRuleEngine.UnitConquestReturnTriggerId(context);
            var conquests = events.Where(e => e.Tick == context.ConquestTick && e.Event.Kind == "BATTLEFIELD_CONQUERED"
                && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == context.ControllerId
                && TryReadObjectString(e.Event.Payload, "battlefieldId", out var battlefield) && battlefield == context.BattlefieldObjectId)
                .Take(2).ToArray();
            var queued = events.Where(e => e.Tick == context.ConquestTick && e.Event.Kind == "TRIGGER_QUEUED"
                && TryReadObjectString(e.Event.Payload, "triggerId", out var id) && id == triggerId
                && TryReadObjectString(e.Event.Payload, "controllerId", out var controller) && controller == context.ControllerId
                && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var source) && source == context.Source.ObjectId
                && TryReadObjectLong(e.Event.Payload, "sourceGeneration", out var generation) && generation == context.Source.Generation
                && TryReadObjectLong(e.Event.Payload, "sourceFaceRevision", out var revision) && revision == context.FaceRevision
                && TryReadObjectString(e.Event.Payload, "sourceCardNo", out var cardNo) && cardNo == context.CardNo
                && TryReadObjectString(e.Event.Payload, "battlefieldObjectId", out var battlefield) && battlefield == context.BattlefieldObjectId
                && TryReadObjectLong(e.Event.Payload, "occurrence", out var occurrence) && occurrence == context.Occurrence
                && (context.TokenFactoryCardNo is null ? !TryReadObjectValue(e.Event.Payload, "tokenFactoryCardNo", out _)
                    : TryReadObjectString(e.Event.Payload, "tokenFactoryCardNo", out var factory) && factory == context.TokenFactoryCardNo)
                && TryReadObjectString(e.Event.Payload, "effectKind", out var effect) && effect == entry.EffectKind
                && TryReadObjectString(e.Event.Payload, "triggeredByEventKind", out var origin) && origin == "BATTLEFIELD_CONQUERED")
                .Take(2).ToArray();
            if (conquests.Length != 1 || queued.Length != 1 || queued[0].Sequence <= conquests[0].Sequence
                || !commands.Any(cmd => cmd.Accepted && cmd.CompletedTick == context.ConquestTick
                    && cmd.StartedEventSequence < conquests[0].Sequence && cmd.CompletedEventSequence >= queued[0].Sequence))
                errors.Add("captured unit conquest return is not bound to accepted conquest history");
        }
    }
}
