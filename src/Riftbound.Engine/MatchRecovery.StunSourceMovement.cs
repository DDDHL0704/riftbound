using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateStunSourceMovementHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var pending = state.TriggerQueue.Where(item => item.FieldContext?.Kind == CoreRuleEngine.StunMoveSourceKind)
            .Select(item => (Id: item.TriggerId, Context: item.FieldContext!, Controller: item.ControllerId))
            .Concat(state.StackItems.Where(item => item.FieldContext?.Kind == CoreRuleEngine.StunMoveSourceKind)
                .Select(item => (Id: item.StackItemId.StartsWith("ordered-", StringComparison.Ordinal)
                    ? item.StackItemId["ordered-".Length..] : "", Context: item.FieldContext!, Controller: item.ControllerId)));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in pending)
        {
            if (entry.Context.StunObserver is not { Source: not null, Stunned: not null, Battlefield: not null } origin
                || string.IsNullOrWhiteSpace(entry.Id) || !seen.Add(entry.Id))
            {
                errors.Add("invalid or duplicated stun source movement origin");
                continue;
            }
            var hash = CoreRuleEngine.StunSourceMovementCaptureHash(entry.Context);
            var queued = events.Where(ev => ev.Tick == origin.CapturedTick && ev.Event.Kind == "TRIGGER_QUEUED"
                && TryReadObjectString(ev.Event.Payload, "triggerId", out var id) && id == entry.Id
                && TryReadObjectString(ev.Event.Payload, "controllerId", out var controller) && controller == entry.Controller
                && TryReadObjectString(ev.Event.Payload, "effectKind", out var effect) && effect == CoreRuleEngine.FieldTriggerEffect
                && TryReadObjectString(ev.Event.Payload, "triggeredByEventKind", out var cause) && cause == "STATUS_EFFECT_APPLIED"
                && TryReadObjectString(ev.Event.Payload, "stunSourceMovementCaptureHash", out var captured) && captured == hash)
                .Take(2).ToArray();
            if (queued.Length != 1 || !events.Any(ev => ev.Tick == origin.CapturedTick
                    && ev.Sequence < queued[0].Sequence && ev.Event.Kind == "STATUS_EFFECT_APPLIED"
                    && TryReadObjectString(ev.Event.Payload, "targetObjectId", out var target) && target == origin.Stunned.ObjectId
                    && TryReadObjectString(ev.Event.Payload, "effectId", out var status) && status == "STUNNED")
                || !commands.Any(command => command.Accepted && command.CompletedTick == origin.CapturedTick
                    && command.StartedEventSequence < queued[0].Sequence && command.CompletedEventSequence >= queued[0].Sequence))
                errors.Add("stun source movement is not bound to accepted stun history");
        }
        foreach (var item in state.StackItems.Where(item => item.FieldContext?.Kind == CoreRuleEngine.StunMoveSourceKind
            && item.TriggerCost is not null))
        {
            var confirmed = events.Where(ev => ev.Event.Kind == "TRIGGER_CONFIRMED"
                && TryReadObjectString(ev.Event.Payload, "stackItemId", out var stack) && stack == item.StackItemId
                && TryReadObjectString(ev.Event.Payload, "playerId", out var player) && player == item.ControllerId)
                .Take(2).ToArray();
            if (confirmed.Length != 1 || !commands.Any(command => command.Accepted && command.PlayerId == item.ControllerId
                && command.CommandType == CommandTypes.ChooseCards && command.CompletedTick == confirmed[0].Tick
                && command.StartedEventSequence < confirmed[0].Sequence && command.CompletedEventSequence >= confirmed[0].Sequence
                && (command.RawCommand ?? command.TypedCommand?.Value) is { } input
                && TryReadObjectString(input, "choiceId", out var id) && id == "TRIGGER-COST:" + item.StackItemId
                && TryReadObjectString(input, "choiceWindow", out var window) && window == CoreRuleEngine.OptionalTriggerWindow
                && TryReadObjectStringList(input, "chosenObjectIds", out var selected)
                && selected.SequenceEqual(new[] { item.SourceObjectId })))
                errors.Add("stun source movement confirmation is not bound to accepted choice history");
        }
    }
}
