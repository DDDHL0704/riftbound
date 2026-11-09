namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateArmamentPlayAttachmentConfirmationHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        foreach (var frame in new MatchState?[] { state, state.PendingRuleChoice?.Origin })
        {
            if (frame is null) continue;
            var items = frame.StackItems.Concat(frame.PendingEffectPlay is { } pending ? [pending.Parent] : []);
            foreach (var item in items.Where(item => CoreRuleEngine.HasArmamentPlayAttachment(item.UnitEvent)
                && item.UnitEvent!.ReflexiveTick is null))
            {
                var original = events.Where(entry => entry.Sequence > boundary && entry.Tick <= frame.Tick
                    && entry.Event.Kind == "TRIGGER_CONFIRMED"
                    && TryReadObjectString(entry.Event.Payload, "stackItemId", out var id) && id == item.StackItemId).ToArray();
                if (item.TriggerCost is null ? original.Length != 0
                    : original.Length != 1 || !CoreRuleEngine.ValidTriggerCostReceipt(frame, item)
                        || !commands.Any(command => command.Accepted && command.CompletedTick == original[0].Tick
                            && command.StartedEventSequence < original[0].Sequence
                            && command.CompletedEventSequence >= original[0].Sequence))
                    errors.Add("armament play permission differs from original optional confirmation");
            }
        }
    }
}
