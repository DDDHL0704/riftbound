namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private sealed record TimingContinuation(string Id, string Source, string Controller, string Effect,
        object Context, BattlefieldTimingAlias? Alias, bool IsStack);

    private static IEnumerable<TimingContinuation> BattlefieldTimingContinuations(MatchState state)
    {
        foreach (var trigger in state.TriggerQueue)
            if (CoreRuleEngine.TimingCaptureContext(trigger) is { } context)
                yield return new(trigger.TriggerId, trigger.SourceObjectId, trigger.ControllerId, trigger.EffectKind,
                    context, trigger.TimingAlias, false);
        foreach (var item in state.PendingEffectPlay?.Parent is { } parent ? state.StackItems.Append(parent) : state.StackItems)
            if (CoreRuleEngine.TimingCaptureContext(item) is { } context)
                yield return new(item.StackItemId.StartsWith("ordered-", StringComparison.Ordinal)
                    ? item.StackItemId["ordered-".Length..] : item.StackItemId,
                    item.SourceObjectId, item.ControllerId, item.EffectKind, context, item.TimingAlias, true);
    }

    private static void ValidateBattlefieldTimingAliases(MatchState state, List<string> errors)
    {
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidTimingAlias(state, item.TimingAlias, item.SourceObjectId,
                item.ControllerId, CoreRuleEngine.TimingCaptureContext(item))) errors.Add("invalid battlefield timing alias stack");
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidTimingAlias(state, trigger.TimingAlias, trigger.SourceObjectId,
                trigger.ControllerId, CoreRuleEngine.TimingCaptureContext(trigger))) errors.Add("invalid battlefield timing alias queue");
        if (state.PendingEffectPlay?.Parent is { } parent
            && !CoreRuleEngine.ValidTimingAlias(state, parent.TimingAlias, parent.SourceObjectId,
                parent.ControllerId, CoreRuleEngine.TimingCaptureContext(parent))) errors.Add("invalid battlefield timing alias effect-play parent");
        foreach (var gain in state.DelayedResourceGains)
            if (!CoreRuleEngine.ValidTimingAlias(state, gain.TimingAlias, gain.SourceObjectId, gain.ControllerId, gain))
                errors.Add("invalid battlefield timing alias delayed ability");
    }

    private static void ValidateBattlefieldTimingHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        foreach (var item in BattlefieldTimingContinuations(state))
        {
            var queued = events.Where(e => e.Event.Kind == "TRIGGER_QUEUED"
                && TryReadObjectString(e.Event.Payload, "triggerId", out var id) && id == item.Id).ToArray();
            if (queued.Length != 1
                || !TryReadObjectString(queued[0].Event.Payload, "sourceObjectId", out var source) || source != item.Source
                || !TryReadObjectString(queued[0].Event.Payload, "controllerId", out var controller) || controller != item.Controller
                || !TryReadObjectString(queued[0].Event.Payload, "effectKind", out var effect) || effect != item.Effect
                || !TryReadObjectString(queued[0].Event.Payload, "timingCaptureHash", out var hash)
                    || hash != CoreRuleEngine.TimingCaptureHash(item.Context, item.Alias)
                || !commands.Any(c => c.Accepted && c.CompletedTick == queued[0].Tick
                    && c.StartedEventSequence < queued[0].Sequence && c.CompletedEventSequence >= queued[0].Sequence)
                || item.IsStack && !events.Any(e => e.Sequence > queued[0].Sequence && e.Event.Kind == "TRIGGERS_MOVED_TO_STACK"
                    && TryReadObjectStringList(e.Event.Payload, "stackItemIds", out var ids) && ids.Contains("ordered-" + item.Id))
                || item.Alias is { } alias && !MatchesAcceptedTimingAlias(queued[0], alias, events))
                errors.Add("battlefield timing skill differs from its original accepted capture");
        }
        foreach (var gain in state.DelayedResourceGains)
        {
            var registered = events.Where(e => e.Event.Kind == "DELAYED_ABILITY_CREATED"
                && TryReadObjectString(e.Event.Payload, "triggerId", out var id) && id == gain.Id).ToArray();
            if (registered.Length != 1
                || !TryReadObjectString(registered[0].Event.Payload, "timingCaptureHash", out var hash)
                || hash != CoreRuleEngine.DelayedTimingCaptureHash(gain)
                || !commands.Any(c => c.Accepted && c.CompletedTick == registered[0].Tick
                    && c.StartedEventSequence < registered[0].Sequence && c.CompletedEventSequence >= registered[0].Sequence)
                || gain.TimingAlias is { } alias && !MatchesAcceptedTimingAlias(registered[0], alias, events))
                errors.Add("delayed battlefield timing skill differs from its original accepted registration");
        }
    }

    private static bool MatchesAcceptedTimingAlias(RecoveredEvent queued, BattlefieldTimingAlias alias,
        IReadOnlyList<RecoveredEvent> events)
        => queued.Tick == alias.CapturedTick && events.Any(e => e.Tick == queued.Tick && e.Sequence < queued.Sequence
            && (alias.Activation is { } activation ? MatchesNamedConquestActivationEvent(e.Event, activation, alias.Battlefield.ObjectId)
                : e.Event.Kind == alias.EventKind
                    && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == alias.ControllerId
                    && TryReadObjectString(e.Event.Payload, "battlefieldId", out var field) && field == alias.Battlefield.ObjectId));
}
