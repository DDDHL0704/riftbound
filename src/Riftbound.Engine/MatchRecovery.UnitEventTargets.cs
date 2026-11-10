namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateUnitEventTargetHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        foreach (var frame in new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>())
        {
            var items = (frame.StackItems ?? []).OfType<StackItemState>().Concat(
                frame.PendingEffectPlay?.Parent is { } parent ? [parent] : []);
            foreach (var item in items.Where(item => (item.UnitEvent is not null || CoreRuleEngine.IsAgilePermanentPlaySkill(item))
                && item.TargetObjectIds is not null))
            {
                var error = item.UnitEvent is null ? "equipment play skill targets differ from original accepted confirmation"
                    : "unit event targets differ from original accepted confirmation";
                if (item.UnitEvent is null && events.Any(record => record.Sequence > boundary && record.Tick <= frame.Tick
                    && record.Event.Kind is "STACK_ITEM_RESOLVED" or "STACK_ITEM_COUNTERED" or "TRIGGER_NOT_CONFIRMED"
                    && TryReadObjectString(record.Event.Payload, "stackItemId", out var retired) && retired == item.StackItemId
                    && commands.Any(command => command.Accepted && command.CompletedTick == record.Tick
                        && command.StartedEventSequence < record.Sequence && command.CompletedEventSequence >= record.Sequence)))
                    errors.Add("equipment play skill was already retired in original accepted history");
                if (item.TargetGenerations is null)
                {
                    // An original confirmation locks this skill's targets.
                    // A later snapshot cannot reopen that already accepted choice.
                    if (events.Any(record => record.Sequence > boundary && record.Tick <= frame.Tick
                        && record.Event.Kind is "PAYMENT_WINDOW_OPENED" or "TRIGGER_CONFIRMED"
                        && TryReadObjectString(record.Event.Payload, "stackItemId", out var id) && id == item.StackItemId
                        && TryReadObjectValue(record.Event.Payload, "targetGenerations", out var generations) && generations is not null
                        && commands.Any(command => command.Accepted && command.PlayerId == item.ControllerId
                            && command.CompletedTick == record.Tick && command.StartedEventSequence < record.Sequence
                            && command.CompletedEventSequence >= record.Sequence)))
                        errors.Add(error);
                    continue;
                }
                // Ward already locks the targets before payment. A suspended
                // parent or movement-payment origin keeps its original choice.
                var kind = frame.PendingPayment is { } payment
                    && payment.ResolvingStackItemId == item.StackItemId
                    && payment.PaymentWindow == CoreRuleEngine.TriggerTargetCostWindow
                    ? "PAYMENT_WINDOW_OPENED" : "TRIGGER_CONFIRMED";
                var confirmations = events.Where(record => record.Sequence > boundary && record.Tick <= frame.Tick && record.Event.Kind == kind
                    && TryReadObjectString(record.Event.Payload, "stackItemId", out var id) && id == item.StackItemId
                    && TryReadObjectStringList(record.Event.Payload, "targetObjectIds", out var targets)
                        && targets.SequenceEqual(item.TargetObjectIds)
                    && TryReadObjectValue(record.Event.Payload, "targetGenerations", out var generations)
                        && MatchStateHasher.HashValue(generations) == MatchStateHasher.HashValue(item.TargetGenerations))
                    .Take(2).ToArray();
                if (confirmations.Length != 1 || !commands.Any(command => command.Accepted
                    && command.PlayerId == item.ControllerId && command.CompletedTick == confirmations[0].Tick
                    && command.StartedEventSequence < confirmations[0].Sequence
                    && command.CompletedEventSequence >= confirmations[0].Sequence))
                    errors.Add(error);
            }
        }
    }

    private static void ValidateUnitEventPlayTargetHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var confirmations = events.Where(record => record.Sequence > boundary
            && record.Event.Kind == "TRIGGER_CONFIRMED"
            && TryReadObjectString(record.Event.Payload, "unitEventTargetsHash", out _)).ToArray();
        foreach (var frame in new MatchState?[] { state, state.PendingRuleChoice?.Origin })
        {
            if (frame is null) continue;
            var items = frame.PendingEffectPlay?.Parent is { } parent ? frame.StackItems.Append(parent) : frame.StackItems;
            foreach (var item in items.Where(CoreRuleEngine.IsUnitEventGraveyardPlay))
            {
                var originals = confirmations.Where(record => TryReadObjectString(record.Event.Payload,
                    "stackItemId", out var stack) && stack == item.StackItemId).ToArray();
                if (item.TargetGenerations is null)
                {
                    if (originals.Length != 0) errors.Add("unit event public target confirmation was rewound");
                    continue;
                }
                if (originals.Length != 1
                    || !TryReadObjectString(originals[0].Event.Payload, "unitEventTargetsHash", out var hash)
                    || hash != CoreRuleEngine.UnitEventPlayTargetsHash(item)
                    || !HasAcceptedBattlefieldChoice(commands, originals[0], item.ControllerId,
                        "UNIT-EVENT:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.TargetObjectIds))
                    errors.Add("unit event public targets differ from their original accepted confirmation");
            }
        }
    }
}
