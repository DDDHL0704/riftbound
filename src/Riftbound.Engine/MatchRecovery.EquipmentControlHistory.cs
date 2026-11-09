namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateEquipmentControlHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        if (!CoreRuleEngine.ValidEquipmentControlLeases(state)) {
            errors.Add("equipment control lease has an invalid source, equipment or controller");
            return;
        }
        var current = CoreRuleEngine.EquipmentControlReceipts(state).Distinct().ToArray();
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var receipts = events.Where(record => record.Sequence > boundary
            && record.Event.Kind is CoreRuleEngine.EquipmentControlCaptured or CoreRuleEngine.EquipmentControlRetired).ToArray();
        if (current.Length == 0 && receipts.Length == 0) return;
        var captured = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = new HashSet<string>(StringComparer.Ordinal);
        static bool Hash(string? value) => value is { Length: 64 }
            && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        foreach (var record in receipts)
        {
            if (!TryReadObjectString(record.Event.Payload, CoreRuleEngine.EquipmentControlKey, out var key)
                || !Hash(key) || !commands.Any(command => command.Accepted && command.CompletedTick == record.Tick
                    && command.StartedEventSequence < record.Sequence && command.CompletedEventSequence >= record.Sequence)) {
                errors.Add("equipment control receipt lacks its original accepted command");
                continue;
            }
            if (record.Event.Kind == CoreRuleEngine.EquipmentControlCaptured) {
                if (!TryReadObjectString(record.Event.Payload, CoreRuleEngine.EquipmentControlProof, out var proof)
                    || !Hash(proof) || !captured.TryAdd(key!, proof!) || !pending.Add(key!))
                    errors.Add("equipment control capture is invalid or duplicated");
            }
            else if (!pending.Remove(key!)) errors.Add("equipment control retirement lacks its original capture");
        }
        foreach (var row in current)
            if (!pending.Contains(row.Key) || !captured.TryGetValue(row.Key, out var proof) || proof != row.Proof)
                errors.Add("equipment control differs from its original accepted effect");
        if (state.Status == MatchStatuses.InProgress && !pending.SetEquals(current.Select(row => row.Key)))
            errors.Add("equipment control effects differ from original capture and retirement history");
    }
}
