using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private sealed record NamedConquestContinuation(string TriggerId, string SourceObjectId,
        string ControllerId, string EffectKind, NamedConquestCaptureInfo Capture);

    private static IEnumerable<NamedConquestContinuation> NamedConquestContinuations(MatchState state)
    {
        foreach (var trigger in state.TriggerQueue)
            if (trigger.TimingAlias is null && CoreRuleEngine.NamedConquestCaptureOf(trigger.ConquestToken, trigger.UnitConquest, trigger.RecastContext) is { } capture)
                yield return new(trigger.TriggerId, trigger.SourceObjectId, trigger.ControllerId, trigger.EffectKind, capture);
        var items = state.PendingEffectPlay is { } pending
            ? state.StackItems.Append(pending.Parent) : state.StackItems;
        foreach (var item in items)
            if (item.TimingAlias is null && CoreRuleEngine.NamedConquestCaptureOf(item.ConquestToken, item.UnitConquest, item.RecastContext) is { } capture)
                yield return new(item.StackItemId.StartsWith("ordered-", StringComparison.Ordinal)
                    ? item.StackItemId["ordered-".Length..] : item.StackItemId,
                    item.SourceObjectId, item.ControllerId, item.EffectKind, capture);
    }

    private static void ValidateNamedConquestActivationHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        foreach (var entry in NamedConquestContinuations(state))
        {
            var queued = events.Where(e => e.Event.Kind == "TRIGGER_QUEUED"
                && TryReadObjectString(e.Event.Payload, "triggerId", out var id) && id == entry.TriggerId).ToArray();
            var capture = entry.Capture;
            var origin = capture.Activation;
            // Every continuation needs its accepted queue entry. Dropping the
            // origin and renaming the stack item cannot invent a real conquest.
            var parents = events.Where(e => queued.Length == 1 && e.Tick == queued[0].Tick
                && (origin is not null ? MatchesNamedConquestActivationEvent(e.Event, origin, capture.BattlefieldId)
                    : e.Event.Kind == "BATTLEFIELD_CONQUERED"
                        && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == entry.ControllerId
                        && TryReadObjectString(e.Event.Payload, "battlefieldId", out var field) && field == capture.BattlefieldId))
                .Take(2).ToArray();
            if (origin is not null && !CoreRuleEngine.ValidNamedConquestActivation(state, origin, capture.BattlefieldId)
                || parents.Length != 1 || queued.Length != 1
                || queued[0].Tick != parents[0].Tick || queued[0].Sequence <= parents[0].Sequence
                || capture.CapturedTick is { } tick && tick != parents[0].Tick
                || !MatchesNamedConquestCapturePayload(queued[0].Event.Payload, entry)
                || !commands.Any(command => command.Accepted && command.CompletedTick == parents[0].Tick
                    && command.StartedEventSequence < parents[0].Sequence && command.CompletedEventSequence >= queued[0].Sequence))
                errors.Add("captured conquest skill is not bound to accepted actual or named conquest activation history");
        }
    }

    private static bool MatchesNamedConquestCapturePayload(object? payload, NamedConquestContinuation entry)
        => TryReadObjectString(payload, "sourceObjectId", out var source) && source == entry.SourceObjectId
            && TryReadObjectString(payload, "controllerId", out var controller) && controller == entry.ControllerId
            && TryReadObjectString(payload, "effectKind", out var effect) && effect == entry.EffectKind
            && TryReadObjectString(payload, "triggeredByEventKind", out var reason)
                && reason == CoreRuleEngine.ConquestCaptureReason(entry.Capture.Activation)
            && TryReadObjectLong(payload, "sourceGeneration", out var generation) && generation == entry.Capture.SourceGeneration
            && TryReadObjectString(payload, "sourceCardNo", out var card) && card == entry.Capture.CardNo
            && TryReadObjectString(payload, "battlefieldObjectId", out var field) && field == entry.Capture.BattlefieldId
            && TryReadObjectString(payload, "conquestContextHash", out var hash) && hash == entry.Capture.ContextHash
            && (entry.Capture.Activation is { } origin ? MatchesNamedConquestActivationPayload(payload, origin)
                : !TryReadObjectValue(payload, "activationStackItemId", out _));

    private static bool MatchesNamedConquestActivationEvent(GameEvent ev, NamedConquestActivationOrigin origin, string field)
        => origin.Source is not null && ev.Kind == "TRIGGER_RESOLVED"
            && TryReadObjectString(ev.Payload, "triggerId", out var id) && id == origin.ParentStackItemId
            && TryReadObjectString(ev.Payload, "controllerId", out var controller) && controller == origin.ControllerId
            && TryReadObjectString(ev.Payload, "sourceObjectId", out var source) && source == origin.Source.ObjectId
            && TryReadObjectLong(ev.Payload, "sourceGeneration", out var gen) && gen == origin.Source.Generation
            && TryReadObjectString(ev.Payload, "sourceCardNo", out var card) && card == origin.CardNo
            && TryReadObjectString(ev.Payload, "effectKind", out var effect) && effect == "HOLD_ACTIVATE_CONQUEST"
            && TryReadObjectString(ev.Payload, "battlefieldObjectId", out var at) && at == field;

    private static bool MatchesNamedConquestActivationPayload(object? payload, NamedConquestActivationOrigin origin)
        => origin.Source is not null
            && TryReadObjectString(payload, "activationStackItemId", out var id) && id == origin.ParentStackItemId
            && TryReadObjectString(payload, "activationSourceObjectId", out var source) && source == origin.Source.ObjectId
            && TryReadObjectLong(payload, "activationSourceGeneration", out var gen) && gen == origin.Source.Generation
            && TryReadObjectString(payload, "activationSourceCardNo", out var card) && card == origin.CardNo
            && TryReadObjectString(payload, "activationControllerId", out var controller) && controller == origin.ControllerId;
}
