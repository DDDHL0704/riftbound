namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateSnapshotKeywordGrantHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        if (!state.CardObjects.Values.Any(SnapshotKeywordGrantRules.HasGrants)
            && !events.Any(e => e.Event.Kind == "SNAPSHOT_KEYWORDS_GRANTED")) return;
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var ledger = new Dictionary<string, (string Target, long Generation, long Tick, long Sequence)>();
        foreach (var entry in events.Where(e => e.Sequence > boundary && e.Event.Kind == "SNAPSHOT_KEYWORDS_GRANTED"))
        {
            var p = entry.Event.Payload;
            if (!TryReadObjectString(p, "targetObjectId", out var target) || target is null
                || !TryReadObjectLong(p, "targetGeneration", out var generation)
                || !TryReadObjectStringList(p, "effectIds", out var effects)
                || !TryReadObjectStringList(p, "keywords", out var keywords) || effects.Count != keywords.Count
                || !commands.Any(c => c.Accepted && c.CompletedTick == entry.Tick
                    && c.StartedEventSequence < entry.Sequence && c.CompletedEventSequence >= entry.Sequence))
            { errors.Add("snapshot keyword grant lacks an original accepted instruction"); continue; }
            for (var i = 0; i < effects.Count; i++)
                if (!SnapshotKeywordGrantRules.IsGrant(effects[i]) || !SnapshotKeywordGrantRules.IsValid(effects[i])
                    || SnapshotKeywordGrantRules.Keyword(effects[i]) != keywords[i]
                    || !ledger.TryAdd(effects[i], (target, generation, entry.Tick, entry.Sequence)))
                    errors.Add("snapshot keyword instruction is duplicated or malformed");
        }
        var expired = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in events.Where(e => e.Sequence > boundary && e.Event.Kind == "UNTIL_END_OF_TURN_EXPIRED"))
            if (TryReadObjectStringList(entry.Event.Payload, "effectIds", out var ids)) expired.UnionWith(ids);
        foreach (var frame in new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>())
        {
            var expected = ledger.Where(e => !expired.Contains(e.Key)
                && frame.CardObjects.TryGetValue(e.Value.Target, out var card) && card.ObjectGeneration == e.Value.Generation
                && frame.PlayerZones.Values.Any(z => z.Base.Contains(card.ObjectId) || z.Battlefields.Contains(card.ObjectId)))
                .Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
            var current = frame.CardObjects.Values.SelectMany(card => card.UntilEndOfTurnEffects
                .Where(SnapshotKeywordGrantRules.IsGrant).Select(effect => (card, effect))).ToArray();
            if (current.Select(e => e.effect).Distinct(StringComparer.Ordinal).Count() != current.Length
                || current.Any(e => !ledger.TryGetValue(e.effect, out var original)
                    || original.Target != e.card.ObjectId || original.Generation != e.card.ObjectGeneration)
                || frame.Status == MatchStatuses.InProgress && !expected.SetEquals(current.Select(e => e.effect)))
                errors.Add("temporary snapshot keywords differ from their original accepted instruction");
            var disarms = frame.StackItems.Select(i => (Id: i.StackItemId, Source: i.SourceObjectId, Controller: i.ControllerId, Context: i.FieldContext))
                .Concat(frame.TriggerQueue.Select(t => (Id: t.TriggerId, Source: t.SourceObjectId, Controller: t.ControllerId, Context: t.FieldContext)));
            foreach (var item in disarms.Where(i => i.Context?.Disarm is not null))
            {
                var original = events.Where(e => e.Sequence > boundary && e.Event.Kind == "TRIGGER_QUEUED"
                    && TryReadObjectString(e.Event.Payload, "triggerId", out var id)
                    && (item.Id == id || item.Id == "ordered-" + id)).ToArray();
                var hasSnapshots = item.Context!.Disarm!.Contributions.Any(c => c.SnapshotKeywordGrant is not null);
                if (!hasSnapshots && !original.Any(e => TryReadObjectString(e.Event.Payload, "snapshotDisarmCaptureHash", out _))) continue;
                if (original.Length != 1 || !TryReadObjectString(original[0].Event.Payload, "snapshotDisarmCaptureHash", out var hash)
                    || hash != CoreRuleEngine.SnapshotDisarmCaptureHash(item.Context, item.Source, item.Controller))
                    errors.Add("captured snapshot disarm differs from its original accepted capture");
                foreach (var contribution in item.Context.Disarm.Contributions.Where(c => c.SnapshotKeywordGrant is not null))
                    if (!ledger.TryGetValue(contribution.SnapshotKeywordGrant!, out var grant)
                        || grant.Target != contribution.Source.ObjectId || grant.Generation != contribution.Source.Generation
                        || grant.Tick > item.Context.Disarm.CapturedTick)
                        errors.Add("captured snapshot disarm lacks its original keyword instruction");
            }
        }
    }
}
