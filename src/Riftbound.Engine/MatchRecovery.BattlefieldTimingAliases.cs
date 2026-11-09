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
            if (!CoreRuleEngine.ValidTimingCopiedOrigin(state, CoreRuleEngine.TimingCaptureContext(item), item.SourceObjectId)
                || !CoreRuleEngine.ValidTimingAlias(state, item.TimingAlias, item.SourceObjectId,
                item.ControllerId, CoreRuleEngine.TimingCaptureContext(item))) errors.Add("invalid battlefield timing alias stack");
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidTimingCopiedOrigin(state, CoreRuleEngine.TimingCaptureContext(trigger), trigger.SourceObjectId)
                || !CoreRuleEngine.ValidTimingAlias(state, trigger.TimingAlias, trigger.SourceObjectId,
                trigger.ControllerId, CoreRuleEngine.TimingCaptureContext(trigger))) errors.Add("invalid battlefield timing alias queue");
        if (state.PendingEffectPlay?.Parent is { } parent
            && (!CoreRuleEngine.ValidTimingCopiedOrigin(state, CoreRuleEngine.TimingCaptureContext(parent), parent.SourceObjectId)
                || !CoreRuleEngine.ValidTimingAlias(state, parent.TimingAlias, parent.SourceObjectId,
                    parent.ControllerId, CoreRuleEngine.TimingCaptureContext(parent)))) errors.Add("invalid battlefield timing alias effect-play parent");
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
                || (item.Context is UnitEventContext { DelayedReceipt: not null }
                    ? !TryReadObjectString(queued[0].Event.Payload, "timingSourceHash", out var sourceHash)
                        || sourceHash != MatchStateHasher.HashValue(item.Source)
                    : !TryReadObjectString(queued[0].Event.Payload, "sourceObjectId", out var source) || source != item.Source)
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
            if (item.Context is UnitEventContext { TokenFactoryCardNo: not null } ceasedUnit
                && !state.CardObjects.ContainsKey(item.Source)
                && !events.Any(e => e.Event.Kind == "TOKEN_CEASED_TO_EXIST" && e.Tick >= ceasedUnit.CapturedTick
                    && TryReadObjectString(e.Event.Payload, "targetObjectId", out var target) && target == item.Source
                    && commands.Any(c => c.Accepted && c.CompletedTick == e.Tick
                        && c.StartedEventSequence < e.Sequence && c.CompletedEventSequence >= e.Sequence)))
                errors.Add("battlefield unit event source disappearance lacks its original accepted token cessation");
            if (item.Context is UnitEventContext unitEvent
                && unitEvent.Timing.Split("_OR_", StringSplitOptions.None).Contains("CONQUEST", StringComparer.Ordinal))
            {
                var modes = events.Where(e => e.Event.Kind == "TRIGGER_MODE_CONFIRMED"
                    && TryReadObjectString(e.Event.Payload, "stackItemId", out var id) && id == "ordered-" + item.Id
                    && commands.Any(c => c.Accepted && c.CompletedTick == e.Tick
                        && c.StartedEventSequence < e.Sequence && c.CompletedEventSequence >= e.Sequence)).ToArray();
                if (unitEvent.Mode is null ? modes.Length != 0 : modes.Length != 1
                    || !TryReadObjectString(modes[0].Event.Payload, "mode", out var mode) || mode != unitEvent.Mode)
                    errors.Add("conquest skill mode differs from its original accepted confirmation");
            }
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

    // This reference exception is restricted to captured battlefield UnitEvents.
    // ValidateBattlefieldTimingHistory still requires both the original capture
    // and an accepted token-cessation event; a missing registry row is not proof.
    private static bool HasValidatedCeasedBattlefieldUnitEventSource(MatchState? state, string? id,
        string? controller, string? source, string? effect)
    {
        if (state is null || string.IsNullOrWhiteSpace(source) || source == "HIDDEN"
            || state.CardObjects.ContainsKey(source)) return false;
        var stack = (state.PendingEffectPlay?.Parent is { } parent ? state.StackItems.Append(parent) : state.StackItems)
            .FirstOrDefault(i => i.StackItemId == id && i.ControllerId == controller
            && i.SourceObjectId == source && i.EffectKind == effect);
        if (stack?.UnitEvent is { TokenFactoryCardNo: { } token } context
            && P6TokenFactoryCatalog.IsTokenFactory(token) && CoreRuleEngine.TimingCaptureContext(stack) is UnitEventContext
            && CoreRuleEngine.ValidUnitEvent(state, context, stack.EffectKind, source, stack.CardNo, controller)
            && CoreRuleEngine.ValidUnitEventTargets(stack)) return true;
        var trigger = state.TriggerQueue.FirstOrDefault(i => i.TriggerId == id && i.ControllerId == controller
            && i.SourceObjectId == source && i.EffectKind == effect);
        return trigger?.UnitEvent is { TokenFactoryCardNo: { } factory } captured
            && P6TokenFactoryCatalog.IsTokenFactory(factory) && CoreRuleEngine.TimingCaptureContext(trigger) is UnitEventContext
            && CoreRuleEngine.ValidUnitEvent(state, captured, trigger.EffectKind, source, trigger.SourceCardNo, controller);
    }

    private static bool MatchesAcceptedTimingAlias(RecoveredEvent queued, BattlefieldTimingAlias alias,
        IReadOnlyList<RecoveredEvent> events)
        => queued.Tick == alias.CapturedTick && events.Any(e => e.Tick == queued.Tick && e.Sequence < queued.Sequence
            && (alias.Activation is { } activation ? MatchesNamedConquestActivationEvent(e.Event, activation, alias.Battlefield.ObjectId)
                : e.Event.Kind == alias.EventKind
                    && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == alias.ControllerId
                    && TryReadObjectString(e.Event.Payload, "battlefieldId", out var field) && field == alias.Battlefield.ObjectId));
}
