namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateLegendPlayCostHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var paid = events.Where(record => record.Sequence > boundary
            && record.Event.Kind == "LEGEND_PLAY_ADDITIONAL_COST_PAID").ToArray();
        bool Original(LegendPlayCostReceipt receipt, string source, long tick)
        {
            var matching = paid.Where(record => record.Tick <= tick && record.Tick == receipt.PaidTick
                && TryReadObjectString(record.Event.Payload, "playId", out var play) && play == receipt.PlayId
                && TryReadObjectString(record.Event.Payload, "playedSourceObjectId", out var originalSource) && originalSource == source
                && TryReadObjectString(record.Event.Payload, "legendPlayCostHash", out var hash)
                    && hash == MatchStateHasher.HashValue(receipt)).ToArray();
            return CoreRuleEngine.ValidLegendPlayCostReceipt(state, receipt) && matching.Length == 1
                && commands.Any(command => command.Accepted && command.PlayerId == receipt.PlayerId
                    && command.CommandType == Riftbound.Contracts.CommandTypes.PlayCard
                    && command.CompletedTick == matching[0].Tick
                    && command.StartedEventSequence < matching[0].Sequence
                    && command.CompletedEventSequence >= matching[0].Sequence);
        }
        foreach (var frame in new MatchState?[] { state, state.PendingRuleChoice?.Origin })
        {
            if (frame is null) continue;
            var items = frame.PendingEffectPlay?.Parent is { } parent ? frame.StackItems.Append(parent) : frame.StackItems;
            foreach (var item in items)
            {
                if (item.UnitEventMovementBattlefield is not null
                    && !CoreRuleEngine.ValidUnitEventMovementDeclaration(frame, item))
                    errors.Add("unit event battlefield binding lacks its matching movement skill");
                var originalPaid = paid.Any(record => record.Tick <= frame.Tick
                    && TryReadObjectString(record.Event.Payload, "playId", out var play) && play == item.StackItemId);
                if (item.LegendPlayCost is { } receipt
                    ? receipt.PlayId != item.StackItemId || !Original(receipt, item.SourceObjectId, frame.Tick)
                    : originalPaid)
                    errors.Add("legend play additional cost differs from original accepted payment");
                if (item.UnitEvent?.PaidLegendPlayCost is { } captured
                    && !Original(captured, item.SourceObjectId, frame.Tick))
                    errors.Add("unit play condition lacks its original accepted legend payment");
            }
            foreach (var trigger in frame.TriggerQueue)
                if (trigger.UnitEvent?.PaidLegendPlayCost is { } captured
                    && !Original(captured, trigger.SourceObjectId, frame.Tick))
                    errors.Add("queued unit play condition lacks its original accepted legend payment");
        }
    }
}
