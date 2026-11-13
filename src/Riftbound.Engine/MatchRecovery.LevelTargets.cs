namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateLevelTargetHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var pending = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in events.Where(record => record.Sequence > boundary
            && record.Event.Kind is CoreRuleEngine.LevelTargetsConfirmed or CoreRuleEngine.LevelTargetsRetired))
        {
            if (!TryReadObjectString(record.Event.Payload, "levelTargetKey", out var key) || key is not { Length: 64 }
                || !commands.Any(command => command.Accepted && command.CompletedTick == record.Tick
                    && command.StartedEventSequence < record.Sequence && command.CompletedEventSequence >= record.Sequence))
            { errors.Add("dependent target receipt lacks its original accepted command"); continue; }
            if (record.Event.Kind == CoreRuleEngine.LevelTargetsConfirmed)
            {
                if (!TryReadObjectString(record.Event.Payload, "levelTargetProof", out var proof) || proof is not { Length: 64 }
                    || pending.GetValueOrDefault(key!) == proof)
                    errors.Add("dependent target confirmation is invalid or duplicated");
                else pending[key!] = proof!;
            }
            else if (!pending.Remove(key!)) errors.Add("dependent target retirement lacks its original confirmation");
        }
        var current = CoreRuleEngine.LevelTargetReceipts(state).Distinct().ToArray();
        foreach (var row in current)
            if (pending.GetValueOrDefault(row.Key) != row.Proof)
                errors.Add("dependent targets differ from their original confirmation");
        if (state.Status == MatchStatuses.InProgress && !pending.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(current.Select(row => row.Key)))
            errors.Add("dependent target declarations differ from original confirmation and retirement history");
    }
}
