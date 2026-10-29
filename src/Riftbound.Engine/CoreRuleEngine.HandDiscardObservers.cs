using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HandDiscardObserverContext(string CardNo, string Kind, ObjectBinding Source,
    string ControllerId, string InstanceId, long FaceRevision, long CapturedTick,
    string TriggeredByEventKind, string DiscardSourceObjectId, IReadOnlyList<ObjectBinding> DiscardedCards,
    RuleTextInstanceBinding? Origin = null, string? TokenFactoryCardNo = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HandDiscardObserverView(string CardNo, string Kind, string ControllerId, int DiscardedCount);

public sealed partial class CoreRuleEngine
{
    internal const string HandDiscardObserverEffect = "CAPTURED_HAND_DISCARD_OBSERVER";
    private const string CapturedHandDiscardEvent = "INTERNAL_HAND_DISCARD_OBSERVERS";
    private sealed record CapturedHandDiscardBatch(string Id, IReadOnlyList<HandDiscardObserverContext> Observers);

    internal static bool ValidHandDiscardObserverFrame(HandDiscardObserverContext c, string effect,
        string source, string controller, string? cardNo = null)
        => effect == HandDiscardObserverEffect && c.Source is { Generation: >= 0 }
            && c.Source.ObjectId == source && !string.IsNullOrWhiteSpace(source)
            && c.ControllerId == controller && !string.IsNullOrWhiteSpace(controller)
            && (cardNo is null || cardNo == c.CardNo) && c.FaceRevision >= 0 && c.CapturedTick > 0
            && HandDiscardTriggerSpecRules.TryGetTrigger(c.CardNo,
                HandDiscardTriggerSpecRules.IsHandCardsDiscardedReadySourcePowerTrigger, out var spec)
            && c.Kind == spec.Kind && c.TriggeredByEventKind is "CARD_DISCARDED" or "CARDS_DISCARDED"
            && !string.IsNullOrWhiteSpace(c.DiscardSourceObjectId)
            && c.DiscardedCards is { Count: > 0 }
            && c.DiscardedCards.All(d => d is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(d.ObjectId))
            && c.DiscardedCards.Select(d => d.ObjectId).Distinct(StringComparer.Ordinal).Count() == c.DiscardedCards.Count
            && (c.Origin is { Equipment: not null } origin
                ? origin.Unit == c.Source && origin.CardNo == c.CardNo
                    && c.InstanceId == $"ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}"
                : c.InstanceId == $"PRINTED:{source}:{c.Source.Generation}");

    internal static bool ValidHandDiscardObserver(MatchState state, HandDiscardObserverContext c,
        string effect, string source, string controller, string? cardNo = null)
        => ValidHandDiscardObserverFrame(c, effect, source, controller, cardNo)
            && state.Seats.ContainsKey(controller) && c.CapturedTick <= state.Tick
            && c.DiscardedCards.All(d => state.CardObjects.TryGetValue(d.ObjectId, out var card)
                && d.Generation <= card.ObjectGeneration)
            && (c.Origin is { } origin
                ? AttachedRuleTextRules.ValidCaptured(state, origin, source, c.Source.Generation, c.CardNo)
                : PredictBindingMatches(state, c.Source, c.CardNo, c.FaceRevision, c.CapturedTick, c.TokenFactoryCardNo));

    internal static string HandDiscardObserverLabel(HandDiscardObserverContext c)
    {
        HandDiscardTriggerSpecRules.TryGetTrigger(c.CardNo,
            HandDiscardTriggerSpecRules.IsHandCardsDiscardedReadySourcePowerTrigger, out var spec);
        return OfficialCardSourceIdentityGroups.DisplayName(c.CardNo) + $"：弃牌后活跃，本回合战力 +{spec.PowerDelta}";
    }

    // A discarded card can be shuffled and drawn again in the same instruction.
    // Public ability text needs the batch count, never current private bindings.
    internal static HandDiscardObserverView? PublicHandDiscardObserver(HandDiscardObserverContext? c)
        => c is null ? null : new(c.CardNo, c.Kind, c.ControllerId, c.DiscardedCards.Count);

    private static ResolutionResult RecordHandDiscardObservers(ResolutionResult result)
    {
        if (!result.Accepted || !result.Events.Any(e => e.Kind == CapturedHandDiscardEvent)) return result;
        var events = new List<GameEvent>(); var queue = result.State.TriggerQueue.ToList();
        foreach (var ev in result.Events)
        {
            if (ev.Kind != CapturedHandDiscardEvent) { events.Add(ev); continue; }
            if (result.State.Status != MatchStatuses.InProgress) continue;
            var batch = (CapturedHandDiscardBatch)ev.Payload["batch"]!;
            foreach (var captured in batch.Observers)
            {
                var c = captured with { CapturedTick = RuleChoices.Value?.CompletionTick ?? result.State.Tick };
                var trigger = new TriggerQueueItemState($"{batch.Id}:{c.InstanceId}", c.ControllerId,
                    c.Source.ObjectId, HandDiscardObserverEffect, c.TriggeredByEventKind)
                    { HandDiscardObserver = c, SourceCardNo = c.CardNo };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state),
            Prompts = BuildCorePrompts(state) };
    }

    private static StackResolutionResult ResolveHandDiscardObserver(MatchState state, StackItemState item)
    {
        var c = item.HandDiscardObserver!;
        if (!ValidHandDiscardObserver(state, c, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo))
            throw new InvalidOperationException("Invalid captured hand discard observer.");
        var events = new List<GameEvent> { new("TRIGGER_RESOLVED", "弃牌触发技能结算", new Dictionary<string, object?> {
            ["triggerId"] = item.StackItemId, ["playerId"] = c.ControllerId, ["controllerId"] = c.ControllerId,
            ["sourceObjectId"] = c.Source.ObjectId, ["effectKind"] = c.Kind,
            ["triggeredByEventKind"] = c.TriggeredByEventKind,
            ["discardSourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, c.DiscardSourceObjectId, "__spectator__"),
            ["discardedObjectIds"] = c.DiscardedCards.Select(d => d.ObjectId).ToArray(), ["discardedCount"] = c.DiscardedCards.Count } ) };
        // An earned skill survives its text or controller changing. "Me" still
        // refers to the original field object, never a later incarnation.
        if (!state.CardObjects.TryGetValue(c.Source.ObjectId, out var source)
            || source.ObjectGeneration != c.Source.Generation || !IsObjectOnField(state.PlayerZones, source.ObjectId))
            return NoopStackResolutionResult(state) with { Events = events };
        HandDiscardTriggerSpecRules.TryGetTrigger(c.CardNo,
            HandDiscardTriggerSpecRules.IsHandCardsDiscardedReadySourcePowerTrigger, out var spec);
        var behavior = new CardBehaviorDefinition(c.CardNo, "弃牌触发", 0, c.Kind, 0, 0,
            PowerModifierAmount: spec.PowerDelta.GetValueOrDefault());
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        source = ApplyReadyState(source, behavior, item, source.ObjectId, out var ready);
        if (ready is not null) events.Add(ready);
        source = ApplyPowerModifier(state, source, behavior, item, source.ObjectId,
            spec.PowerDelta.GetValueOrDefault(), out var power);
        events.Add(power); cards[source.ObjectId] = source;
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
