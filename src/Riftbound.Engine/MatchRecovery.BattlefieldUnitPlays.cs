namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    // Original accepted events remain immutable. Malformed payloads are
    // rejected by the frame validator before they can certify any capture.
    private static void ValidateBattlefieldUnitPlayHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        static string NormalizeId(string id) => id.StartsWith("ordered-", StringComparison.Ordinal) ? id[8..] : id;
        static bool Sha(string? value) => value is { Length: 64 }
            && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        static IEnumerable<(string Id, string Source, string Controller, FieldTriggerContext Context,
            StackItemState? Item, long FrameTick)> Scope(MatchState? frame)
        {
            if (frame is null) yield break;
            foreach (var trigger in frame.TriggerQueue)
                if (trigger.FieldContext is { } context && (CoreRuleEngine.IsBattlefieldUnitPlayKind(context.Kind)
                    || context.BattlefieldUnitPlay is not null))
                    yield return (NormalizeId(trigger.TriggerId), trigger.SourceObjectId, trigger.ControllerId, context, null, frame.Tick);
            foreach (var item in frame.StackItems.Concat(frame.PendingEffectPlay is { } pending ? [pending.Parent] : []))
                if (item.FieldContext is { } context && (CoreRuleEngine.IsBattlefieldUnitPlayKind(context.Kind)
                    || context.BattlefieldUnitPlay is not null))
                    yield return (NormalizeId(item.StackItemId), item.SourceObjectId, item.ControllerId, context, item, frame.Tick);
        }
        var frames = new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>().ToArray();
        if (frames.Any(frame => frame.UntilEndOfTurnEffects is null
            || frame.UntilEndOfTurnEffects.Any(value => value is null)))
        {
            errors.Add("battlefield unit-play history contains a null effect list or value");
            return;
        }
        var current = frames.SelectMany(Scope).ToArray();
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var captures = events.Where(entry => entry.Sequence > boundary
            && entry.Event.Payload?.ContainsKey(CoreRuleEngine.BattlefieldUnitPlayCaptureHashKey) == true).ToArray();
        if (captures.Length == 0 && current.Length == 0 && !frames.Any(frame => frame.UntilEndOfTurnEffects.Any(value =>
            value.StartsWith(CoreRuleEngine.BattlefieldFirstEntryUsePrefix, StringComparison.Ordinal)))) return;
        bool Accepted(RecoveredEvent entry) => commands.Any(command => command.Accepted && command.CompletedTick == entry.Tick
            && command.StartedEventSequence < entry.Sequence && command.CompletedEventSequence >= entry.Sequence);
        foreach (var frame in frames)
            if (Scope(frame).GroupBy(item => item.Id).Any(group => group.Count() > 1))
                errors.Add("battlefield unit-play skill is duplicated across pending locations");
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in captures)
            if (entry.Event.Kind != "TRIGGER_QUEUED" || !Accepted(entry)
                || !TryReadObjectString(entry.Event.Payload, "triggerId", out var id) || string.IsNullOrWhiteSpace(id)
                || !expected.Add(NormalizeId(id))
                || !TryReadObjectString(entry.Event.Payload, CoreRuleEngine.BattlefieldUnitPlayCaptureHashKey, out var hash) || !Sha(hash)
                || !TryReadObjectInt(entry.Event.Payload, CoreRuleEngine.BattlefieldUnitPlayTurnKey, out var turn) || turn < 0)
                errors.Add("battlefield unit-play skill lacks a unique original accepted capture");
        var retired = events.Where(entry => entry.Sequence > boundary && Accepted(entry)
                && entry.Event.Kind is "STACK_ITEM_RESOLVED" or "STACK_ITEM_COUNTERED" or "TRIGGER_PAYMENT_DECLINED")
            .Select(entry => TryReadObjectString(entry.Event.Payload, "stackItemId", out var id) ? id : null)
            .OfType<string>().Select(NormalizeId).ToHashSet(StringComparer.Ordinal);
        var declined = events.Where(entry => entry.Sequence > boundary && Accepted(entry)
                && entry.Event.Kind == "TRIGGER_NOT_CONFIRMED")
            .Select(entry => TryReadObjectString(entry.Event.Payload, "discardedBattlefieldUnitPlayHash", out var hash) ? hash : null)
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        retired.UnionWith(expected.Where(id => declined.Contains(MatchStateHasher.HashValue(id))
            || declined.Contains(MatchStateHasher.HashValue("ordered-" + id))));
        expected.ExceptWith(retired);
        foreach (var item in current)
        {
            var original = captures.Where(entry => TryReadObjectString(entry.Event.Payload, "triggerId", out var id)
                && id is not null && NormalizeId(id) == item.Id).ToArray();
            if (retired.Contains(item.Id) || item.Context.BattlefieldUnitPlay is not { } origin
                || original.Length != 1 || original[0].Tick != origin.CapturedTick
                || !TryReadObjectString(original[0].Event.Payload, CoreRuleEngine.BattlefieldUnitPlayCaptureHashKey, out var hash)
                || hash != CoreRuleEngine.BattlefieldUnitPlayCaptureHash(item.Context, item.Source, item.Controller))
                errors.Add("battlefield unit-play skill differs from its original accepted capture");
            if (item.Context.Kind != CoreRuleEngine.BattlefieldFirstEntryMoveKind) continue;
            var confirmations = events.Where(entry => entry.Sequence > boundary && Accepted(entry) && entry.Tick <= item.FrameTick
                && entry.Event.Kind == "TRIGGER_CONFIRMED"
                && TryReadObjectString(entry.Event.Payload, "stackItemId", out var id)
                && id is not null && NormalizeId(id) == item.Id).ToArray();
            if (item.Item is { TargetGenerations: not null } selected)
            {
                if (confirmations.Length != 1 || !TryReadObjectString(confirmations[0].Event.Payload,
                    CoreRuleEngine.BattlefieldUnitPlayTargetsHashKey, out var targetHash)
                    || targetHash != CoreRuleEngine.BattlefieldUnitPlayTargetHash(selected))
                    errors.Add("battlefield unit-play targets differ from their original accepted confirmation");
            }
            else if (confirmations.Length != 0)
                errors.Add("battlefield unit-play target confirmation was erased or returned to its queue");
        }
        if (state.Status == MatchStatuses.InProgress && !expected.SetEquals(current.Select(item => item.Id)))
            errors.Add("pending battlefield unit-play skills differ from original completion history");
        foreach (var frame in frames)
        {
            var frameBoundary = CurrentGameAcceptedEventBoundary(frame, commands, events);
            var uses = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in events.Where(entry => entry.Sequence > frameBoundary && entry.Tick <= frame.Tick)
                .OrderBy(entry => entry.Sequence))
            {
                if (entry.Event.Kind == "TURN_END_CLEANUP_STARTED") { uses.Clear(); continue; }
                if (entry.Event.Payload?.ContainsKey(CoreRuleEngine.BattlefieldFirstEntryUseKey) != true) continue;
                if (!Accepted(entry) || entry.Event.Kind != "TRIGGER_QUEUED"
                    || entry.Event.Payload?.ContainsKey(CoreRuleEngine.BattlefieldUnitPlayCaptureHashKey) != true
                    || !TryReadObjectInt(entry.Event.Payload, CoreRuleEngine.BattlefieldUnitPlayTurnKey, out var turn))
                {
                    errors.Add("battlefield first-entry use lacks its original accepted capture");
                    continue;
                }
                if (turn != frame.TurnNumber) continue;
                if (!TryReadObjectString(entry.Event.Payload, CoreRuleEngine.BattlefieldFirstEntryUseKey, out var marker)
                    || marker is null || !marker.StartsWith(CoreRuleEngine.BattlefieldFirstEntryUsePrefix, StringComparison.Ordinal)
                    || !Sha(marker[CoreRuleEngine.BattlefieldFirstEntryUsePrefix.Length..]) || !uses.Add(marker))
                    errors.Add("battlefield first-entry use is invalid or repeated in its original turn");
            }
            var actual = frame.UntilEndOfTurnEffects.Where(value =>
                value.StartsWith(CoreRuleEngine.BattlefieldFirstEntryUsePrefix, StringComparison.Ordinal)).ToArray();
            if (actual.Length != uses.Count || !uses.SetEquals(actual))
                errors.Add("battlefield first-entry uses differ from original accepted turn history");
        }
    }
}
