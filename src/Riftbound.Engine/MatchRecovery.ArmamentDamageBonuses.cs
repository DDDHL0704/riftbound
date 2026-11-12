namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateArmamentDamageBonusHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        static string Normalize(string? id) => id is not null && id.StartsWith("ordered-", StringComparison.Ordinal) ? id[8..] : id ?? "";
        static bool Sha(string? value) => value is { Length: 64 }
            && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        static IEnumerable<(string Id, string Source, string Controller, DamageDistributionContext Context, long Tick)> Scope(MatchState frame)
        {
            foreach (var trigger in frame.TriggerQueue ?? [])
                if (trigger?.DamageDistribution is { } context)
                    yield return (Normalize(trigger.TriggerId), trigger.SourceObjectId, trigger.ControllerId, context, frame.Tick);
            foreach (var item in (frame.StackItems ?? []).Concat(frame.PendingEffectPlay is { Parent: { } } pending ? [pending.Parent] : []))
                if (item?.DamageDistribution is { } context)
                    yield return (Normalize(item.StackItemId), item.SourceObjectId, item.ControllerId, context, frame.Tick);
        }
        var frames = new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>().ToArray();
        var all = frames.SelectMany(Scope).ToArray();
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var captures = events.Where(entry => entry.Sequence > boundary
            && entry.Event.Payload?.ContainsKey(CoreRuleEngine.ArmamentDamageCaptureHashKey) == true).ToArray();
        if (captures.Length == 0 && !all.Any(item => CoreRuleEngine.RequiresArmamentDamageHistory(item.Context))) return;
        bool Accepted(RecoveredEvent entry) => commands.Any(command => command.Accepted && command.CompletedTick == entry.Tick
            && command.StartedEventSequence < entry.Sequence && command.CompletedEventSequence >= entry.Sequence);
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in captures)
            if (!Accepted(entry) || entry.Event.Kind != "TRIGGER_QUEUED"
                || !TryReadObjectString(entry.Event.Payload, "triggerId", out var id) || string.IsNullOrWhiteSpace(id)
                || !expected.Add(Normalize(id))
                || !TryReadObjectString(entry.Event.Payload, CoreRuleEngine.ArmamentDamageCaptureHashKey, out var hash) || !Sha(hash))
                errors.Add("armament damage bonus lacks a unique original accepted capture");
        var capturedIds = expected.ToHashSet(StringComparer.Ordinal);
        var retired = events.Where(entry => entry.Sequence > boundary && Accepted(entry)
                && entry.Event.Kind is "STACK_ITEM_RESOLVED" or "STACK_ITEM_COUNTERED" or "TRIGGER_PAYMENT_DECLINED")
            .Select(entry => TryReadObjectString(entry.Event.Payload, "stackItemId", out var id) ? id : null)
            .OfType<string>().Select(Normalize).ToHashSet(StringComparer.Ordinal);
        var discarded = events.Where(entry => entry.Sequence > boundary && Accepted(entry)
                && entry.Event.Kind == "TRIGGER_NOT_CONFIRMED")
            .Select(entry => TryReadObjectString(entry.Event.Payload, CoreRuleEngine.DiscardedArmamentDamageHashKey, out var hash) ? hash : null)
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        retired.UnionWith(expected.Where(id => discarded.Contains(MatchStateHasher.HashValue(id))
            || discarded.Contains(MatchStateHasher.HashValue("ordered-" + id))));
        expected.ExceptWith(retired);
        var current = all.Where(item => capturedIds.Contains(item.Id) || CoreRuleEngine.RequiresArmamentDamageHistory(item.Context)).ToArray();
        foreach (var frame in frames)
            if (Scope(frame).Where(item => capturedIds.Contains(item.Id) || CoreRuleEngine.RequiresArmamentDamageHistory(item.Context))
                .GroupBy(item => item.Id).Any(group => group.Count() > 1))
                errors.Add("armament damage bonus skill is duplicated across pending locations");
        foreach (var item in current)
        {
            var original = captures.Where(entry => TryReadObjectString(entry.Event.Payload, "triggerId", out var id)
                && id is not null && Normalize(id) == item.Id).ToArray();
            if (retired.Contains(item.Id) || original.Length != 1 || original[0].Tick > item.Tick
                || !TryReadObjectString(original[0].Event.Payload, CoreRuleEngine.ArmamentDamageCaptureHashKey, out var hash)
                || hash != CoreRuleEngine.ArmamentDamageCaptureHash(item.Context, item.Source, item.Controller))
                errors.Add("armament damage bonuses differ from the original accepted capture");
        }
        if (state.Status == MatchStatuses.InProgress && !expected.SetEquals(current.Select(item => item.Id)))
            errors.Add("pending armament damage bonuses differ from original completion history");
    }
}
