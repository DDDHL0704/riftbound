using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateStandbyUnitObserverHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var pending = state.TriggerQueue.Where(item => item.StandbyObserver?.Unit is not null)
            .Select(item => (Id: item.TriggerId, Effect: item.EffectKind, Context: item.StandbyObserver!, Controller: item.ControllerId))
            .Concat(state.StackItems.Where(item => item.StandbyObserver?.Unit is not null)
                .Select(item => (Id: item.StackItemId.StartsWith("ordered-", StringComparison.Ordinal)
                    ? item.StackItemId["ordered-".Length..] : "", Effect: item.EffectKind,
                    Context: item.StandbyObserver!, Controller: item.ControllerId)));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in pending)
        {
            var context = entry.Context;
            var birth = context.Unit!;
            if (entry.Id.Length == 0 || !seen.Add(entry.Id)
                || !CoreRuleEngine.ValidStandbyObserver(state, context, entry.Effect, context.Source.ObjectId,
                    context.CardNo, entry.Controller))
            {
                errors.Add("invalid or duplicated standby unit observer origin");
                continue;
            }
            var hash = CoreRuleEngine.StandbyUnitCaptureHash(context);
            var queued = events.Where(ev => ev.Event.Kind == "TRIGGER_QUEUED" && ev.Tick >= birth.CapturedTick
                && TryReadObjectString(ev.Event.Payload, "triggerId", out var id) && id == entry.Id
                && TryReadObjectString(ev.Event.Payload, "controllerId", out var controller) && controller == entry.Controller
                && TryReadObjectString(ev.Event.Payload, "effectKind", out var effect) && effect == entry.Effect
                && TryReadObjectString(ev.Event.Payload, "triggeredByEventKind", out var cause)
                    && cause == CoreRuleEngine.StandbyObserverEventKind(entry.Effect)
                && TryReadObjectString(ev.Event.Payload, "standbyUnitCaptureHash", out var capture) && capture == hash)
                .Take(2).ToArray();
            if (queued.Length != 1 || !commands.Any(command => command.Accepted
                && command.CompletedTick == queued[0].Tick
                && command.StartedEventSequence < queued[0].Sequence && command.CompletedEventSequence >= queued[0].Sequence
                && events.Any(action => action.Sequence > command.StartedEventSequence && action.Sequence < queued[0].Sequence
                    && action.Event.Kind == birth.ActionEventKind
                    && CoreRuleEngine.StandbyUnitActionHash(action.Event) == birth.ActionHash
                    && (TryReadObjectString(action.Event.Payload, "playedByPlayerId", out var actor)
                        || TryReadObjectString(action.Event.Payload, "playerId", out actor)) && actor == entry.Controller)))
                errors.Add("standby unit observer is not bound to its accepted action and source capture");
            if (birth.CeasedSource is { } ceased && !events.Any(ev => ev.Tick == ceased.Tick
                && ev.Event.Kind == "TOKEN_CEASED_TO_EXIST"
                && TryReadObjectString(ev.Event.Payload, "targetObjectId", out var source) && source == context.Source.ObjectId
                && commands.Any(command => command.Accepted && command.CompletedTick == ev.Tick
                    && command.StartedEventSequence < ev.Sequence && command.CompletedEventSequence >= ev.Sequence)))
                errors.Add("standby unit observer ceased source is not bound to accepted token departure history");
        }
        foreach (var item in state.StackItems.Where(item => item.EffectKind == CoreRuleEngine.StandbyPlayedEnemyDamageEffect
            && item.StandbyObserver?.Unit is not null && item.TargetGenerations is not null))
        {
            // Preserve the original target both while Ward payment is pending
            // and after confirmation. A consistent projection alone cannot
            // substitute a different legal enemy or its new generation.
            var kind = state.PendingPayment?.ResolvingStackItemId == item.StackItemId
                ? "PAYMENT_WINDOW_OPENED" : "TRIGGER_CONFIRMED";
            var confirmed = events.Where(ev => ev.Event.Kind == kind
                && TryReadObjectString(ev.Event.Payload, "stackItemId", out var stack) && stack == item.StackItemId
                && TryReadObjectStringList(ev.Event.Payload, "targetObjectIds", out var targets)
                    && targets.SequenceEqual(item.TargetObjectIds)
                && TryReadObjectValue(ev.Event.Payload, "targetGenerations", out var generations)
                    && MatchStateHasher.HashValue(generations) == MatchStateHasher.HashValue(item.TargetGenerations))
                .Take(2).ToArray();
            if (confirmed.Length != 1 || !commands.Any(command => command.Accepted && command.PlayerId == item.ControllerId
                && command.CompletedTick == confirmed[0].Tick && command.StartedEventSequence < confirmed[0].Sequence
                && command.CompletedEventSequence >= confirmed[0].Sequence
                && command.CommandType is CommandTypes.ChooseCards or CommandTypes.PayCost))
                errors.Add("standby unit damage target is not bound to accepted confirmation history");
        }
    }
}
