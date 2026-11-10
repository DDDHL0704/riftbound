namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidatePowerPaymentHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        if (state.Seats is null || state.RunePools is null) return;
        var totals = state.Seats.Keys.ToDictionary(player => player, _ => 0L, StringComparer.Ordinal);
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        foreach (var record in events.Where(record => record.Sequence > boundary
            && record.Event.Payload?.ContainsKey(CoreRuleEngine.PowerPaymentReceiptKey) == true))
        {
            if (record.Event.Kind is not ("COST_PAID" or "RESOURCE_SKILL_RESOLVED" or CoreRuleEngine.PowerSpentEvent)
                || !TryReadObjectValue(record.Event.Payload, CoreRuleEngine.PowerPaymentReceiptKey, out var payload)
                || !TryReadObjectInt(payload, "turnNumber", out var turn))
            {
                errors.Add("power payment counter lacks its original accepted debit");
                continue;
            }
            if (turn != state.TurnNumber) continue;
            if (!TryReadObjectString(payload, "playerId", out var player) || player is null
                || !TryReadObjectString(record.Event.Payload, "playerId", out var actor) || actor != player
                || !totals.TryGetValue(player, out var previous)
                || !TryReadObjectLong(payload, "amount", out var amount) || amount <= 0
                || !TryReadObjectLong(payload, "paidPowerTotal", out var total)
                || amount > long.MaxValue - previous || previous + amount != total
                || !commands.Any(command => command.Accepted && command.CompletedTick == record.Tick
                    && command.StartedEventSequence < record.Sequence && record.Sequence <= command.CompletedEventSequence))
            {
                errors.Add("power payment counter lacks its original accepted debit");
                continue;
            }
            totals[player] = total;
        }
        foreach (var (player, pool) in state.RunePools)
            if (pool is null || !totals.TryGetValue(player, out var expected) || pool.PaidPowerThisTurn != expected)
                errors.Add("power payment counter differs from original accepted debit history");
        ValidateMightyPowerPaymentHistory(state, commands, events, boundary, errors);
    }

    private static void ValidateMightyPowerPaymentHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, long boundary, List<string> errors)
    {
        static IEnumerable<(string Id, MightySkillContext Context)> Scope(MatchState? frame)
        {
            if (frame is null) yield break;
            foreach (var trigger in frame.TriggerQueue)
                if (CoreRuleEngine.RequiresMightyPowerPaymentHistory(trigger.MightySkill))
                    yield return (trigger.TriggerId, trigger.MightySkill!);
            foreach (var item in frame.StackItems.Concat(frame.PendingEffectPlay is { } pending ? [pending.Parent] : []))
                if (CoreRuleEngine.RequiresMightyPowerPaymentHistory(item.MightySkill))
                    yield return (item.StackItemId.StartsWith("ordered-", StringComparison.Ordinal)
                        ? item.StackItemId[8..] : item.StackItemId, item.MightySkill!);
        }
        foreach (var capture in Scope(state).Concat(Scope(state.PendingRuleChoice?.Origin)))
        {
            var original = events.Where(record => record.Sequence > boundary && record.Event.Kind == "TRIGGER_QUEUED"
                && TryReadObjectString(record.Event.Payload, "triggerId", out var id) && id == capture.Id).ToArray();
            if (original.Length != 1
                || !TryReadObjectString(original[0].Event.Payload, "mightyPowerPaymentHash", out var hash)
                || hash != CoreRuleEngine.MightyPowerPaymentHistoryHash(capture.Context)
                || !commands.Any(command => command.Accepted && command.CompletedTick == original[0].Tick
                    && command.StartedEventSequence < original[0].Sequence
                    && original[0].Sequence <= command.CompletedEventSequence))
                errors.Add("Mighty payment inputs differ from original accepted capture");
        }
    }
}
