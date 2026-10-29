using Riftbound.Contracts;

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

        foreach (var item in state.StackItems.Where(i => i.UnitConquestReturn is not null && i.TriggerCost is not null))
        {
            var receipt = item.TriggerCost!;
            var paymentId = "TRIGGER-COST:" + item.StackItemId;
            var payments = events.Where(e => e.Event.Kind == "COST_PAID" && e.Tick >= item.UnitConquestReturn!.ConquestTick
                && TryReadObjectString(e.Event.Payload, "paymentId", out var id) && id == paymentId
                && TryReadObjectString(e.Event.Payload, "paymentWindow", out var window) && window == "TRIGGER_PAYMENT"
                && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == item.ControllerId
                && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var source) && source == item.SourceObjectId
                && TryReadObjectString(e.Event.Payload, "reason", out var reason) && reason == item.EffectKind
                && TryReadObjectLong(e.Event.Payload, "totalManaCost", out var mana) && mana == receipt.Mana
                && TryReadObjectLong(e.Event.Payload, "totalPowerCost", out var power) && power == receipt.Power
                && TryReadObjectLong(e.Event.Payload, "experienceCost", out var experience) && experience == 0)
                .Take(2).ToArray();
            if (payments.Length != 1 || !commands.Any(command => command.Accepted && command.PlayerId == item.ControllerId
                && command.CommandType == CommandTypes.PayCost && command.CompletedTick == payments[0].Tick
                && command.StartedEventSequence < payments[0].Sequence && command.CompletedEventSequence >= payments[0].Sequence
                && (command.RawCommand ?? command.TypedCommand?.Value) is { } input
                && TryReadObjectString(input, "paymentId", out var id) && id == paymentId
                && TryReadObjectString(input, "paymentWindow", out var window) && window == "TRIGGER_PAYMENT"
                && ValidUnitConquestPaymentResourceHistory(command, input, payments[0], receipt.Mana, events)))
                errors.Add("captured unit conquest return receipt is not bound to accepted payment history");
        }
    }

    private static bool ValidUnitConquestPaymentResourceHistory(RecoveredCommand command, object input,
        RecoveredEvent payment, int manaCost, IReadOnlyList<RecoveredEvent> events)
    {
        if (!TryReadObjectStringList(input, "paymentChoiceIds", out var choices)
            || !TryReadObjectStringList(payment.Event.Payload, "paymentChoiceIds", out var paidChoices)
            || !choices.SequenceEqual(paidChoices)
            || choices.Distinct(StringComparer.Ordinal).Count() != choices.Count) return false;
        var taps = new List<string>();
        var spendCount = 0;
        foreach (var choice in choices)
        {
            if (choice == "PAY" || choice == $"SPEND_MANA:{manaCost}") spendCount++;
            else if (choice.StartsWith("TAP_RUNE:", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(choice["TAP_RUNE:".Length..]))
                taps.Add(choice["TAP_RUNE:".Length..].Trim());
            else return false;
        }
        if (spendCount != 1 || taps.Distinct(StringComparer.Ordinal).Count() != taps.Count) return false;

        // Inline resource actions must occur inside this accepted command before its
        // receipt. An earlier standalone tap cannot authenticate a forged inline choice.
        var preceding = events.Where(e => e.Sequence > command.StartedEventSequence
            && e.Sequence < payment.Sequence).OrderBy(e => e.Sequence).ToArray();
        var resources = preceding.Where(e => e.Event.Kind is "RUNE_TAPPED" or "RUNE_RECYCLED").ToArray();
        var gains = preceding.Where(e => e.Event.Kind == "MANA_GAINED").ToArray();
        if (resources.Length != taps.Count || gains.Length != taps.Count) return false;
        for (var i = 0; i < taps.Count; i++)
        {
            if (resources[i].Event.Kind != "RUNE_TAPPED" || resources[i].Sequence >= gains[i].Sequence
                || !ValidUnitConquestInlineTapEvent(resources[i], taps[i], command, payment)
                || !ValidUnitConquestInlineTapEvent(gains[i], taps[i], command, payment)) return false;
        }
        return true;
    }

    private static bool ValidUnitConquestInlineTapEvent(RecoveredEvent resource, string rune,
        RecoveredCommand command, RecoveredEvent payment)
        => resource.Tick == command.CompletedTick
            && TryReadObjectString(resource.Event.Payload, "playerId", out var player) && player == command.PlayerId
            && TryReadObjectString(resource.Event.Payload, "sourceObjectId", out var source) && source == rune
            && TryReadObjectString(resource.Event.Payload, "paymentWindow", out var window) && window == "TRIGGER_PAYMENT"
            && TryReadObjectString(resource.Event.Payload, "paymentId", out var id)
            && TryReadObjectString(payment.Event.Payload, "paymentId", out var paymentId) && id == paymentId
            && TryReadObjectString(resource.Event.Payload, "abilityId", out var ability) && ability == "BASIC_RUNE_EXHAUST_GAIN_1_MANA"
            && TryReadObjectLong(resource.Event.Payload, "mana", out var mana) && mana == 1;
}
