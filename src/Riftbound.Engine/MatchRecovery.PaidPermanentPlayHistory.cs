namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidatePaidPermanentPlayHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var current = CoreRuleEngine.PaidPermanentPlaySkills(state).ToArray();
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var receipts = events.Where(record => record.Sequence > boundary
            && record.Event.Kind is CoreRuleEngine.PaidPermanentPlayCaptured or CoreRuleEngine.PaidPermanentPlayRetired).ToArray();
        if (current.Length == 0 && receipts.Length == 0) return;
        var captured = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = new HashSet<string>(StringComparer.Ordinal);
        static bool Hash(string? value) => value is { Length: 64 }
            && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        foreach (var record in receipts)
        {
            if (!TryReadObjectString(record.Event.Payload, CoreRuleEngine.PaidPermanentPlaySkillKey, out var key)
                || !Hash(key) || !commands.Any(command => command.Accepted && command.CompletedTick == record.Tick
                    && command.StartedEventSequence < record.Sequence && command.CompletedEventSequence >= record.Sequence))
            {
                errors.Add("paid permanent play skill receipt lacks its original accepted command");
                continue;
            }
            if (record.Event.Kind == CoreRuleEngine.PaidPermanentPlayCaptured)
            {
                if (!TryReadObjectString(record.Event.Payload, CoreRuleEngine.PaidPermanentPlayChoicesKey, out var proof)
                    || !Hash(proof) || !captured.TryAdd(key!, proof!) || !pending.Add(key!))
                    errors.Add("paid permanent play skill capture is invalid or duplicated");
            }
            else if (!pending.Remove(key!))
                errors.Add("paid permanent play skill retirement lacks a pending original capture");
        }
        foreach (var item in current)
        {
            var key = MatchStateHasher.HashValue(item.StackItemId);
            if (!CoreRuleEngine.ValidPaidPermanentPlayChoices(item) || !pending.Contains(key)
                || !captured.TryGetValue(key, out var proof) || proof != CoreRuleEngine.PaidPermanentPlayChoicesHash(item))
                errors.Add("paid permanent play skill differs from original accepted payment choices");
        }
        if (state.Status == MatchStatuses.InProgress
            && !pending.SetEquals(current.Select(item => MatchStateHasher.HashValue(item.StackItemId))))
            errors.Add("pending paid permanent play skills differ from original accepted capture and retirement history");
    }
}
