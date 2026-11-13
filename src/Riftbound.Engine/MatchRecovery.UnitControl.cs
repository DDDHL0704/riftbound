namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool HasValidatedPublicControlLossEffect(MatchState? state, string? triggerId, string? effectKind)
        => state is not null && effectKind == CoreRuleEngine.DelayedControlLossEffect
            && state.TriggerQueue.Any(item => item.TriggerId == triggerId && CoreRuleEngine.ValidControlLossTrigger(state, item));

    private static void ValidateControlLossState(MatchState state, List<string> errors)
    {
        foreach (var frame in CoreRuleEngine.UnitControlFrames(state))
        {
            if (!HasUnitControlEffectList(frame))
            {
                errors.Add("unit control history contains a null effect list or value");
                continue;
            }
            var markers = frame.UntilEndOfTurnEffects.Where(e => e.StartsWith(CoreRuleEngine.DelayedControlLossPrefix, StringComparison.Ordinal)).ToArray();
            if (markers.Any(marker => !CoreRuleEngine.ValidControlLoss(frame, CoreRuleEngine.ReadControlLoss(marker)))
                || markers.Length > 0 && (frame.EndStep is not null || frame.Phase == MatchPhases.TurnEnd))
                errors.Add("invalid delayed control loss instruction");
            var all = CoreRuleEngine.CurrentControlLosses(frame).ToArray();
            if (all.Select(c => c.Grant.InstanceId).Distinct(StringComparer.Ordinal).Count() != all.Length)
                errors.Add("duplicate delayed control loss instruction");
            foreach (var item in frame.StackItems)
                if (item.DelayedControlLoss is not null ? !CoreRuleEngine.ValidControlLossStack(frame, item)
                    : item.EffectKind == CoreRuleEngine.DelayedControlLossEffect) errors.Add("invalid captured control loss stack");
            foreach (var item in frame.TriggerQueue)
                if (item.DelayedControlLoss is not null ? !CoreRuleEngine.ValidControlLossTrigger(frame, item)
                    : item.EffectKind == CoreRuleEngine.DelayedControlLossEffect) errors.Add("invalid captured control loss trigger");
        }
    }

    private static void ValidateControlLossViews(IReadOnlyList<object?> payloads, List<string> errors)
    {
        foreach (var payload in payloads)
        {
            var json = System.Text.Json.JsonSerializer.SerializeToElement(payload);
            if (json.ValueKind == System.Text.Json.JsonValueKind.Object && json.TryGetProperty("delayedControlLoss", out _))
                errors.Add("public control loss view exposes a private effect receipt");
        }
    }

    private static void ValidateUnitControlHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        if (CoreRuleEngine.UnitControlFrames(state).Any(frame => !HasUnitControlEffectList(frame)))
        {
            errors.Add("unit control history contains a null effect list or value");
            return;
        }
        if (!CoreRuleEngine.ValidUnitControlLeases(state)) {
            errors.Add("unit control lease has an invalid source, unit or controller");
            return;
        }
        var current = CoreRuleEngine.UnitControlReceipts(state).Distinct().ToArray();
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var receipts = events.Where(record => record.Sequence > boundary
            && record.Event.Kind is CoreRuleEngine.UnitControlCaptured or CoreRuleEngine.UnitControlRetired).ToArray();
        if (current.Length == 0 && receipts.Length == 0) return;
        var captured = new Dictionary<string, string>(StringComparer.Ordinal);
        var pending = new HashSet<string>(StringComparer.Ordinal);
        static bool Hash(string? value) => value is { Length: 64 }
            && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        foreach (var record in receipts)
        {
            if (!TryReadObjectString(record.Event.Payload, CoreRuleEngine.UnitControlKey, out var key)
                || !Hash(key) || !commands.Any(command => command.Accepted && command.CompletedTick == record.Tick
                    && command.StartedEventSequence < record.Sequence && command.CompletedEventSequence >= record.Sequence)) {
                errors.Add("unit control receipt lacks its original accepted command");
                continue;
            }
            if (record.Event.Kind == CoreRuleEngine.UnitControlCaptured) {
                if (!TryReadObjectString(record.Event.Payload, CoreRuleEngine.UnitControlProof, out var proof)
                    || !Hash(proof) || !captured.TryAdd(key!, proof!) || !pending.Add(key!))
                    errors.Add("unit control capture is invalid or duplicated");
            }
            else if (!pending.Remove(key!)) errors.Add("unit control retirement lacks its original capture");
        }
        foreach (var row in current)
            if (!pending.Contains(row.Key) || !captured.TryGetValue(row.Key, out var proof) || proof != row.Proof)
                errors.Add("unit control differs from its original accepted effect");
        var order = receipts.Where(r => r.Event.Kind == CoreRuleEngine.UnitControlCaptured)
            .Select((record, index) => (Key: record.Event.Payload?.GetValueOrDefault(CoreRuleEngine.UnitControlKey)?.ToString(), Index: index))
            .Where(row => row.Key is not null).DistinctBy(row => row.Key).ToDictionary(row => row.Key!, row => row.Index, StringComparer.Ordinal);
        foreach (var lease in CoreRuleEngine.UnitControlFrames(state).SelectMany(frame => frame.CardObjects.Values)
            .Select(card => card.UnitControl).OfType<UnitControlLease>())
        {
            var indices = lease.Grants.Select(grant => order.GetValueOrDefault(MatchStateHasher.HashValue("CONTROL:" + grant.InstanceId), -1)).ToArray();
            if (indices.Zip(indices.Skip(1)).Any(pair => pair.First >= pair.Second))
                errors.Add("unit control effects differ from their original timestamp order");
        }
        if (state.Status == MatchStatuses.InProgress && !pending.SetEquals(current.Select(row => row.Key)))
            errors.Add("unit control effects differ from original capture and retirement history");
    }

    private static bool HasUnitControlEffectList(MatchState state)
        => state.UntilEndOfTurnEffects is { } effects && effects.All(effect => effect is not null);
}
