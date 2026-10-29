using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record PlayedEquipmentWitness(ObjectBinding Source, string CardNo, long FaceRevision, string? TokenFactoryCardNo = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourcePlayDrawContext(string CardNo, long SourceGeneration, long FaceRevision, long CapturedTick,
    IReadOnlyList<PlayedEquipmentWitness> OtherEquipment,
    string? TokenFactoryCardNo = null, RuleTextInstanceBinding? Origin = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ControllerId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TriggerId { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string SourcePlayDrawEffect = "CAPTURED_SOURCE_PLAY_EQUIPMENT_DRAW";
    internal const string SourcePlayDiscardEffect = "CAPTURED_SOURCE_PLAY_DISCARD_DRAW";
    private sealed record SourcePlayDrawObservation(string SourceId, string Player, string Effect, SourcePlayDrawContext Context);
    private static TriggerSpec? SourcePlayDrawSpec(string? no) => SourceUnitPlayedTriggerSpecRules.TriggersForCard(no)
        .FirstOrDefault(t => t.Kind == TriggerKinds.SourceUnitPlayedOtherEquipmentDraw
            && t.Timing == TriggerTimings.SourceUnitPlayed && t.DrawCount is > 0 && t.RequiredOtherControlledEquipment is > 0);
    private static TriggerSpec? SourcePlayDiscardSpec(string? no) => SourceUnitPlayedTriggerSpecRules.TriggersForCard(no)
        .FirstOrDefault(t => t.Kind == TriggerKinds.SourceUnitPlayedDiscardDraw
            && t.Timing == TriggerTimings.SourceUnitPlayed && t.DiscardCount is > 0 && t.DrawCount is >= 0);

    private static GameEvent CaptureSourcePlayDraw(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, CardObjectState entered, string player)
    {
        if (entered.IsFaceDown || ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")) return ev;
        var witnesses = zones.Values.SelectMany(z => z.Base.Concat(z.Battlefields)).Distinct(StringComparer.Ordinal)
            .Where(id => id != entered.ObjectId && cards.TryGetValue(id, out var c) && !c.IsFaceDown
                && c.Tags.Contains(CardObjectTags.EquipmentCard) && EffectiveFieldControllerId(zones, id, c) == player)
            .Select(id => cards[id]).OrderBy(c => c.ObjectId, StringComparer.Ordinal)
            .Select(c => new PlayedEquipmentWitness(new(c.ObjectId, c.ObjectGeneration), c.CardNo!, c.CopyFaceTransitions?.Count ?? 0, c.TokenFactoryCardNo)).ToArray();
        var observations = new List<SourcePlayDrawObservation>();
        foreach (var instance in AttachedRuleTextRules.Instances(zones, cards, entered))
        {
            if (SourcePlayDrawSpec(instance.CardNo) is { } draw && witnesses.Length >= draw.RequiredOtherControlledEquipment)
                observations.Add(new(entered.ObjectId, player, SourcePlayDrawEffect,
                    new(instance.CardNo, entered.ObjectGeneration, entered.CopyFaceTransitions?.Count ?? 0, 0, witnesses,
                        entered.TokenFactoryCardNo, instance.Origin)));
            if (SourcePlayDiscardSpec(instance.CardNo) is not null)
                observations.Add(new(entered.ObjectId, player, SourcePlayDiscardEffect,
                    new(instance.CardNo, entered.ObjectGeneration, entered.CopyFaceTransitions?.Count ?? 0, 0, [],
                        entered.TokenFactoryCardNo, instance.Origin)));
        }
        return observations.Count == 0 ? ev : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            ["sourcePlayDrawObservation"] = observations.ToArray() } };
    }

    private static ResolutionResult QueueSourcePlayDraw(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var events = result.Events.ToList(); var queue = result.State.TriggerQueue.ToList();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            if (ev.Payload.GetValueOrDefault("sourcePlayDrawObservation") is not SourcePlayDrawObservation[] observations) continue;
            events[index] = ev with { Payload = ev.Payload.Where(p => p.Key != "sourcePlayDrawObservation").ToDictionary(p => p.Key, p => p.Value) };
            foreach (var (observation, instance) in observations.Select((o, i) => (o, i)))
            {
                var context = observation.Context; var generation = context.SourceGeneration;
                if (before.CardObjects.TryGetValue(observation.SourceId, out var old) && !IsObjectOnField(before.PlayerZones, observation.SourceId))
                    generation = Math.Max(generation, old.ObjectGeneration + 1);
                var triggerId = $"source-play-draw-{result.State.Tick}-{index}-{instance}";
                context = context with { SourceGeneration = generation, CapturedTick = result.State.Tick };
                if (observation.Effect == SourcePlayDiscardEffect)
                    context = context with { ControllerId = observation.Player, TriggerId = triggerId };
                var trigger = new TriggerQueueItemState(triggerId,
                    observation.Player, observation.SourceId, observation.Effect, ev.Kind) { SourcePlayDraw = context, SourceCardNo = context.CardNo };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        if (events.Count == result.Events.Count) return result;
        var next = result.State with { TriggerQueue = queue };
        return result with { State = next, Events = events, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidSourcePlayDraw(MatchState state, SourcePlayDrawContext? context, string effect,
        string source, string? no = null, string? controller = null, string? triggerId = null)
    {
        if (context is null) return effect is not (SourcePlayDrawEffect or SourcePlayDiscardEffect);
        if (no is not null && no != context.CardNo || context.CapturedTick <= 0 || context.CapturedTick > state.Tick
            || context.SourceGeneration < 0 || context.FaceRevision < 0 || context.OtherEquipment is null) return false;
        if (effect == SourcePlayDiscardEffect)
        {
            if (SourcePlayDiscardSpec(context.CardNo) is null || context.OtherEquipment.Count != 0
                || string.IsNullOrWhiteSpace(context.ControllerId) || !state.Seats.ContainsKey(context.ControllerId)
                || string.IsNullOrWhiteSpace(context.TriggerId)
                || !context.TriggerId.StartsWith($"source-play-draw-{context.CapturedTick}-", StringComparison.Ordinal)
                || controller is not null && context.ControllerId != controller
                || triggerId is not null && context.TriggerId != triggerId) return false;
        }
        else if (effect != SourcePlayDrawEffect || SourcePlayDrawSpec(context.CardNo) is not { } spec
            || context.ControllerId is not null || context.TriggerId is not null
            || context.OtherEquipment.Count < spec.RequiredOtherControlledEquipment
            || context.OtherEquipment.Any(w => w is null || w.Source is null || w.Source.ObjectId == source
                || w.Source.Generation < 0 || w.FaceRevision < 0 || string.IsNullOrWhiteSpace(w.CardNo)
                || !(PrintedPlayTypes.Value.TryGetValue(w.CardNo, out var types) && (types & 2) != 0
                    || P6TokenFactoryCatalog.TryGetByCardNo(w.CardNo, out var factory) && factory.Tags.Contains(CardObjectTags.EquipmentCard))
                || !PredictBindingMatches(state, w.Source, w.CardNo, w.FaceRevision, context.CapturedTick, w.TokenFactoryCardNo))
            || context.OtherEquipment.Select(w => w.Source.ObjectId).Distinct(StringComparer.Ordinal).Count() != context.OtherEquipment.Count) return false;
        return context.Origin is { } origin
            ? AttachedRuleTextRules.ValidCaptured(state, origin, source, context.SourceGeneration, context.CardNo)
            : PredictBindingMatches(state, new(source, context.SourceGeneration), context.CardNo,
                context.FaceRevision, context.CapturedTick, context.TokenFactoryCardNo);
    }

    private static StackResolutionResult ResolveSourcePlayDraw(MatchState state, StackItemState item)
    {
        if (item.EffectKind == SourcePlayDiscardEffect)
        {
            var id = item.StackItemId.StartsWith("ordered-", StringComparison.Ordinal)
                ? item.StackItemId["ordered-".Length..] : item.StackItemId;
            if (!ValidSourcePlayDraw(state, item.SourcePlayDraw, item.EffectKind, item.SourceObjectId, item.CardNo, item.ControllerId, id)
                || SourcePlayDiscardSpec(item.SourcePlayDraw!.CardNo) is not { } discard)
                throw new InvalidOperationException("Invalid captured on-play discard.");
            return ResolveDiscardDrawStackItem(state, item, discard.DiscardCount!.Value, discard.DrawCount!.Value,
                SourcePlayDiscardEffect, SourcePlayDiscardEffect, TriggerTimings.SourceUnitPlayed);
        }
        // CN 383.2.a.1: this additional clause was part of the trigger condition.
        // The captured skill survives departure and later equipment changes.
        var count = SourcePlayDrawSpec(item.SourcePlayDraw!.CardNo)!.DrawCount!.Value;
        var zones = NormalizeZonesForSeats(state); var events = new List<GameEvent> { new("TRIGGER_RESOLVED", "打出技能抽牌",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__"),
                ["sourceCardNo"] = item.SourcePlayDraw.CardNo, ["effectKind"] = SourcePlayDrawEffect, ["drawCount"] = count }) };
        var draw = ApplyDrawToPlayer(state, zones, NormalizeScoresForSeats(state), item.ControllerId, count, state.RngCursor, events, deferNormalVictory: true);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, PlayerScores = draw.PlayerScores,
            WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }

    internal static bool ValidSourcePlayDiscardChoice(MatchState state)
    {
        var choice = state.PendingHandChoice;
        if (choice is null) return true;
        if (choice.SourcePlayDraw is not { } context)
            return choice.EffectKind != SourcePlayDiscardEffect && choice.ChoiceWindow != SourcePlayDiscardEffect
                && choice.Reason != SourcePlayDiscardEffect;
        if (!ValidSourcePlayDraw(state, context, SourcePlayDiscardEffect, choice.SourceObjectId,
                controller: choice.PlayerId) || SourcePlayDiscardSpec(context.CardNo) is not { } spec
            || !state.PlayerZones.TryGetValue(choice.PlayerId, out var zones)) return false;
        var legal = zones.Hand.Where(id => IsCardObjectControlledByPlayerOrLegacyOwned(state.CardObjects, choice.PlayerId, id)).ToArray();
        return choice.ChoiceId == "CHOICE-ordered-" + context.TriggerId
            && choice.ChoiceWindow == SourcePlayDiscardEffect && choice.Reason == SourcePlayDiscardEffect
            && choice.EffectKind == SourcePlayDiscardEffect && choice.RequiredCount == spec.DiscardCount
            && choice.MaxCount == spec.DiscardCount && choice.DrawCount == spec.DrawCount
            && legal.Length >= spec.DiscardCount && choice.LegalObjectIds.SequenceEqual(legal)
            && choice.MovementObserver is null && choice.LastBreathDraw is null;
    }
}
