using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmpowerOtherObserverContext(ObjectBinding Source, ObjectBinding Observed, string CardNo,
    string ControllerId, long CapturedTick, long FaceRevision, string RuleTextInstanceId,
    string? SourceTokenFactoryCardNo = null, string? ObservedTokenFactoryCardNo = null,
    RuleTextInstanceBinding? Origin = null);

public sealed partial class CoreRuleEngine
{
    internal const string EmpowerOtherObserverEffect = "CONTROLLER_EMPOWERED_OTHER_EMPOWER_SOURCE";
    private const string EmpowerOtherCaptureKey = "internalEmpowerOtherObservers";
    private sealed record CapturedEmpowerOtherObserver(EmpowerOtherObserverContext Context);

    private static readonly Lazy<HashSet<string>> EmpowerOtherObserverFaces = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Where(card => RuleTextParser.Parse(card).Triggers.Any(t => t.Kind == TriggerKinds.ControllerEmpoweredOtherEmpowerSource))
            .Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal));

    private static CapturedEmpowerOtherObserver[] CaptureEmpowerOtherObservers(MatchState state,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState observed, string actor)
    {
        if (!state.PlayerZones.TryGetValue(actor, out var zones)) return [];
        return zones.Base.Concat(zones.Battlefields).Concat(zones.LegendZone).Distinct(StringComparer.Ordinal)
            .Where(id => id != observed.ObjectId && cards.TryGetValue(id, out var source) && !source.IsFaceDown
                && SourceObjectControlledByPlayerOrLegacyOwned(source, actor))
            .Order(StringComparer.Ordinal)
            .SelectMany(id => AttachedRuleTextRules.Instances(state.PlayerZones, cards, cards[id])
                .Where(instance => EmpowerOtherObserverFaces.Value.Contains(instance.CardNo))
                .Select(instance => new CapturedEmpowerOtherObserver(new(new(id, cards[id].ObjectGeneration),
                    new(observed.ObjectId, observed.ObjectGeneration), instance.CardNo, actor, 0,
                    instance.Origin?.FaceRevision ?? cards[id].CopyFaceTransitions?.Count ?? 0, instance.Id,
                    cards[id].TokenFactoryCardNo, observed.TokenFactoryCardNo, instance.Origin))))
            .ToArray();
    }

    private static ResolutionResult QueueEmpowerOtherObservers(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList();
        var events = new List<GameEvent>();
        var found = false;
        foreach (var (ev, index) in result.Events.Select((value, index) => (value, index)))
        {
            found |= ev.Payload.ContainsKey(EmpowerOtherCaptureKey);
            events.Add(ev.Payload.ContainsKey(EmpowerOtherCaptureKey)
                ? ev with { Payload = ev.Payload.Where(pair => pair.Key != EmpowerOtherCaptureKey).ToDictionary(pair => pair.Key, pair => pair.Value) }
                : ev);
            if (result.State.Status != MatchStatuses.InProgress || ev.Kind != "OBJECT_EMPOWERED"
                || ev.Payload.GetValueOrDefault(EmpowerOtherCaptureKey) is not CapturedEmpowerOtherObserver[] captured) continue;
            foreach (var (observation, ordinal) in captured.Select((value, ordinal) => (value, ordinal)))
            {
                var context = observation.Context with { CapturedTick = RuleChoices.Value?.CompletionTick ?? result.State.Tick };
                var trigger = new TriggerQueueItemState($"empower-other-{context.CapturedTick}-{index}-{ordinal}",
                    context.ControllerId, context.Source.ObjectId, EmpowerOtherObserverEffect, "OBJECT_EMPOWERED")
                { EmpowerOtherObserver = context, SourceCardNo = context.CardNo };
                queue.Add(trigger);
                var queued = BuildTriggerQueuedEvent(trigger);
                events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, context.Source.ObjectId, "__spectator__") } });
            }
        }
        if (!found) return result;
        var next = result.State with { TriggerQueue = queue };
        return result with { State = next, Events = events, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidEmpowerOtherObserver(MatchState state, EmpowerOtherObserverContext? context,
        string effect, string sourceId, string controller, string? cardNo)
    {
        if (context is null) return effect != EmpowerOtherObserverEffect;
        if (effect != EmpowerOtherObserverEffect || context.CardNo != cardNo || !EmpowerOtherObserverFaces.Value.Contains(context.CardNo)
            || context.Source is null || context.Observed is null || context.Source.ObjectId != sourceId
            || context.Source.ObjectId == context.Observed.ObjectId || context.Source.Generation < 0 || context.Observed.Generation < 0
            || string.IsNullOrWhiteSpace(context.Observed.ObjectId) || context.ControllerId != controller || !state.Seats.ContainsKey(controller)
            || context.CapturedTick < 0 || context.CapturedTick > state.Tick || context.FaceRevision < 0
            || (state.CardObjects.TryGetValue(context.Observed.ObjectId, out var observed)
                ? observed.ObjectGeneration < context.Observed.Generation || observed.TokenFactoryCardNo != context.ObservedTokenFactoryCardNo
                : context.ObservedTokenFactoryCardNo is null || !P6TokenFactoryCatalog.IsTokenFactory(context.ObservedTokenFactoryCardNo))) return false;
        return context.Origin is null
            ? context.RuleTextInstanceId == $"PRINTED:{context.Source.ObjectId}:{context.Source.Generation}"
                && PredictBindingMatches(state, context.Source, context.CardNo, context.FaceRevision, context.CapturedTick, context.SourceTokenFactoryCardNo)
            : context.Origin.Equipment is { } equipment
                && context.RuleTextInstanceId == $"ATTACHED:{equipment.ObjectId}:{equipment.Generation}:{context.Origin.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, context.Origin, sourceId, context.Source.Generation, context.CardNo);
    }

    internal static bool ValidEmpowerOtherTrigger(MatchState state, TriggerQueueItemState item)
        => ValidEmpowerOtherObserver(state, item.EmpowerOtherObserver, item.EffectKind, item.SourceObjectId, item.ControllerId, item.SourceCardNo)
            && (item.EmpowerOtherObserver is null || item.TriggeredByEventKind == "OBJECT_EMPOWERED"
                && item.TriggerId.StartsWith($"empower-other-{item.EmpowerOtherObserver.CapturedTick}-", StringComparison.Ordinal));

    internal static bool ValidEmpowerOtherStack(MatchState state, StackItemState item)
        => ValidEmpowerOtherObserver(state, item.EmpowerOtherObserver, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)
            && (item.EmpowerOtherObserver is null || item.StackItemId.StartsWith($"ordered-empower-other-{item.EmpowerOtherObserver.CapturedTick}-", StringComparison.Ordinal)
                && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.OptionalCosts.Count == 0
                && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && !item.SourceConfirmed);

    private static StackResolutionResult ResolveEmpowerOtherObserver(MatchState state, StackItemState item)
    {
        if (!ValidEmpowerOtherStack(state, item)) throw new InvalidOperationException("Invalid captured empower observer.");
        return ApplyEmpowerAction(state, item.EmpowerOtherObserver!.Source, item.ControllerId);
    }
}
