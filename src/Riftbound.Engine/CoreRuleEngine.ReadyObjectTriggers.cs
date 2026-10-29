using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReadyObjectTriggerContext(string CardNo, ObjectBinding Source, long CapturedTick,
    long FaceRevision, string Timing, string? EquipmentPlayId = null,
    RuleTextInstanceBinding? Origin = null, string? SourceTokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    internal const string ReadyOtherObjectEffect = "SOURCE_PLAY_OR_FIRST_EQUIPMENT_READY_OTHER_OBJECT";
    private const string ReadyObjectObservationKey = "readyObjectTriggerObservations";
    private static readonly Lazy<IReadOnlyDictionary<string, TriggerSpec>> ReadyOtherObjectSpecs = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Select(c => (c.CardNo, Spec: RuleTextParser.Parse(c).Triggers.FirstOrDefault(t =>
                t.Kind == TriggerKinds.SourcePlayedOrFirstEquipmentReadyOtherObject)))
            .Where(c => c.Spec is not null).ToDictionary(c => c.CardNo, c => c.Spec!, StringComparer.Ordinal));

    private static ReadyObjectTriggerContext[] CaptureReadyOtherObjects(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, IEnumerable<CardObjectState> sources, string timing, string? playId = null)
        => sources.Where(c => !c.IsFaceDown).SelectMany(c => AttachedRuleTextRules.Instances(zones, cards, c)
            .Where(i => ReadyOtherObjectSpecs.Value.ContainsKey(i.CardNo))
            .Select(i => new ReadyObjectTriggerContext(i.CardNo, new(c.ObjectId, c.ObjectGeneration), 0,
                c.CopyFaceTransitions?.Count ?? 0, timing, playId, i.Origin, c.TokenFactoryCardNo))).ToArray();

    private static GameEvent CaptureSourcePlayReadyObject(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState entered)
        => ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED") ? ev
            : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
                [ReadyObjectObservationKey] = CaptureReadyOtherObjects(zones, cards, [entered], "SOURCE_PLAY") } };

    private static GameEvent CaptureEquipmentPlayReadyObjects(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState equipment, string player, string playId)
    {
        if (TokenObjectRules.IsToken(equipment)) return ev;
        var sources = zones.Values.SelectMany(z => z.Base.Concat(z.Battlefields)).Distinct(StringComparer.Ordinal)
            .Where(cards.ContainsKey).Select(id => cards[id])
            .Where(c => c.Tags.Contains(CardObjectTags.UnitCard) && EffectiveFieldControllerId(zones, c.ObjectId, c) == player);
        return ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            [ReadyObjectObservationKey] = CaptureReadyOtherObjects(zones, cards, sources, "FIRST_EQUIPMENT", playId) } };
    }

    private static ResolutionResult QueueReadyOtherObjects(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state = result.State; var events = result.Events.ToList(); var queue = state.TriggerQueue.ToList();
        var priorEquipment = (before.CardPlays.TurnNumber == state.TurnNumber ? before.CardPlays.Resolved : [])
            .Where(p => (p.Types & 2) != 0).Select(p => p.PlayerId).ToHashSet(StringComparer.Ordinal);
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            var firstEquipment = ev.Payload.GetValueOrDefault(CardPlayHistoryObservationKey) is CardPlayHistoryObservation
                { Resolved: true, Entry.Types: var types } history && (types & 2) != 0 && priorEquipment.Add(history.Entry.PlayerId);
            if (ev.Payload.GetValueOrDefault(ReadyObjectObservationKey) is not ReadyObjectTriggerContext[] observations) continue;
            events[index] = ev with { Payload = ev.Payload.Where(p => p.Key != ReadyObjectObservationKey).ToDictionary(p => p.Key, p => p.Value) };
            foreach (var (captured, instance) in observations.Select((c, i) => (c, i)))
            {
                if (captured.Timing == "FIRST_EQUIPMENT" && !firstEquipment) continue;
                var source = captured.Source;
                if (captured.Timing == "SOURCE_PLAY" && before.CardObjects.TryGetValue(source.ObjectId, out var old)
                    && !IsObjectOnField(before.PlayerZones, source.ObjectId))
                    source = source with { Generation = Math.Max(source.Generation, old.ObjectGeneration + 1) };
                var controller = ev.Payload.GetValueOrDefault("playerId") as string;
                if (controller is null) continue;
                var context = captured with { Source = source, CapturedTick = state.Tick };
                var trigger = new TriggerQueueItemState($"ready-other-{state.Tick}-{index}-{instance}", controller,
                    source.ObjectId, ReadyOtherObjectEffect, ev.Kind) { ReadyObjectTrigger = context, SourceCardNo = context.CardNo };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        return result with { State = state with { TriggerQueue = queue }, Events = events };
    }

    internal static bool ValidReadyObjectTrigger(MatchState state, ReadyObjectTriggerContext? context, string effect,
        string source, string controller, string? cardNo = null)
    {
        if (context is null) return effect != ReadyOtherObjectEffect;
        if (effect != ReadyOtherObjectEffect || cardNo is not null && cardNo != context.CardNo
            || !ReadyOtherObjectSpecs.Value.ContainsKey(context.CardNo) || context.Source is not { Generation: >= 0 }
            || context.Source.ObjectId != source || context.CapturedTick <= 0 || context.CapturedTick > state.Tick
            || context.FaceRevision < 0 || !state.Seats.ContainsKey(controller)
            || context.Timing is not ("SOURCE_PLAY" or "FIRST_EQUIPMENT")) return false;
        if (context.Timing == "SOURCE_PLAY" ? context.EquipmentPlayId is not null
            : string.IsNullOrWhiteSpace(context.EquipmentPlayId)
                || state.CardPlays.TurnNumber != state.TurnNumber
                || state.CardPlays.Resolved.FirstOrDefault(p => p.PlayerId == controller && (p.Types & 2) != 0)?.PlayId != context.EquipmentPlayId)
            return false;
        return context.Origin is { } origin
            ? AttachedRuleTextRules.ValidCaptured(state, origin, source, context.Source.Generation, context.CardNo)
            : PredictBindingMatches(state, context.Source, context.CardNo, context.FaceRevision, context.CapturedTick, context.SourceTokenFactoryCardNo);
    }

    private static string[] ReadyOtherObjectTargets(MatchState state, StackItemState item)
        => state.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.LegendZone))
            .Distinct(StringComparer.Ordinal).Where(id => id != item.SourceObjectId
                && state.CardObjects.TryGetValue(id, out var c) && !c.IsFaceDown && c.IsExhausted
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, id))
            .Order(StringComparer.Ordinal).ToArray();

    internal static bool ValidReadyOtherObjectStack(MatchState state, StackItemState item)
    {
        if (item.ReadyObjectTrigger is null) return item.EffectKind != ReadyOtherObjectEffect;
        if (!ValidReadyObjectTrigger(state, item.ReadyObjectTrigger, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)
            || item.SourceConfirmed || item.DamageAmount != 0 || item.EffectRepeatCount != 1 || item.OptionalCosts.Count != 0) return false;
        return item.TargetGenerations is null ? item.TargetObjectIds.Count == 0
            : item.TargetObjectIds.Count == 1 && item.TargetObjectIds[0] != item.SourceObjectId
                && item.TargetGenerations.Count == 1 && item.TargetGenerations.TryGetValue(item.TargetObjectIds[0], out var generation) && generation >= 0;
    }

    private static PendingCardChoiceState ReadyOtherObjectChoice(MatchState state, StackItemState item)
    {
        var legal = ReadyOtherObjectTargets(state, item);
        return new("READY-OTHER:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId, 0, 1, legal, legal,
            "选择除来源外的一个休眠物体变为活跃；不选则放弃。", item.SourceObjectId, item.EffectKind)
            { ResolvingStackItemId = item.StackItemId };
    }

    private static ResolutionResult PrepareReadyOtherObject(ResolutionResult result, StackItemState item)
    {
        var choice = ReadyOtherObjectChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var next = result.State with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidReadyObjectChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.ReadyObjectTrigger is null || item.TargetGenerations is not null
            || !ValidReadyObjectTrigger(state, item.ReadyObjectTrigger, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)) return false;
        var expected = ReadyOtherObjectChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId
            && choice.EffectKind == expected.EffectKind && choice.RequiredCount == 0 && choice.MaxCount == 1
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static StackResolutionResult ResolveReadyOtherObject(MatchState state, StackItemState item)
    {
        if (!ValidReadyOtherObjectStack(state, item) || item.TargetGenerations is null)
            throw new InvalidOperationException("Invalid ready other object ability.");
        var id = item.TargetObjectIds.Single();
        if (!StackObjectRules.ReferencesCurrentObject(state, item, id) || !ReadyOtherObjectTargets(state, item).Contains(id))
            return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var behavior = new CardBehaviorDefinition(item.CardNo, "活跃其他物体", 0, item.EffectKind, 0, 1);
        cards[id] = ApplyReadyState(cards[id], behavior, item, id, out var ready);
        if (ready is not null) ready = ready with { Payload = new Dictionary<string, object?>(ready.Payload) {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__") } };
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = ready is null ? [] : [ready] };
    }
}
