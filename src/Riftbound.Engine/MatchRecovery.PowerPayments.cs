namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidatePowerPaymentHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        if (state.Seats is null || state.RunePools is null) return;
        var totals = state.Seats.Keys.ToDictionary(player => player, _ => 0, StringComparer.Ordinal);
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        foreach (var record in events.Where(record => record.Sequence > boundary
            && record.Event.Kind == CoreRuleEngine.PowerSpentEvent))
        {
            if (!TryReadObjectInt(record.Event.Payload, "turnNumber", out var turn) || turn != state.TurnNumber) continue;
            if (!TryReadObjectString(record.Event.Payload, "playerId", out var player) || player is null
                || !totals.TryGetValue(player, out var previous)
                || !TryReadObjectInt(record.Event.Payload, "amount", out var amount) || amount <= 0
                || !TryReadObjectInt(record.Event.Payload, "paidPowerTotal", out var total)
                || (long)previous + amount != total
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
    }
}
