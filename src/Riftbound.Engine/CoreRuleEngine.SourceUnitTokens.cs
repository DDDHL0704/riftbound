using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourceUnitTokenContext(string CardNo, ObjectBinding Source, string ControllerId,
    long CapturedTick, long FaceRevision, string Timing, string? BattlefieldId = null,
    RuleTextInstanceBinding? Origin = null, string? TokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    internal const string SourceUnitTokenEffect = "SOURCE_PLAY_OR_SCORE_UNIT_TOKEN";
    private const string SourceUnitTokenObservationKey = "sourceUnitTokenObservations";
    private static readonly Lazy<IReadOnlyDictionary<string, TriggerSpec>> SourceUnitTokenSpecs = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Select(c => (c.CardNo, Spec: RuleTextParser.Parse(c).Triggers.FirstOrDefault(t =>
                t.Kind is TriggerKinds.SourceUnitPlayedOrScoredCreateUnit or TriggerKinds.SourceUnitPlayedOrExhaustedCreateUnit)))
            .Where(c => c.Spec is not null).ToDictionary(c => c.CardNo, c => c.Spec!, StringComparer.Ordinal));

    private static P6TokenFactoryDefinition? SourceUnitTokenFactory(string no)
        => SourceUnitTokenSpecs.Value.TryGetValue(no, out var spec)
            ? P6TokenFactoryCatalog.GetAll().FirstOrDefault(f => f.TokenFamilyName == spec.CreatedTokenName
                && f.DefaultPower == spec.CreatedTokenPower && f.Tags.Contains(CardObjectTags.UnitCard)
                && (spec.CreatedTokenKeywords ?? []).All(f.Tags.Contains)) : null;

    private static SourceUnitTokenContext[] CaptureSourceUnitTokens(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState source, string player, string timing, string? field = null)
        => source.IsFaceDown ? [] : AttachedRuleTextRules.Instances(zones, cards, source)
            .Where(i => SourceUnitTokenFactory(i.CardNo) is not null && SourceUnitTokenTiming(i.CardNo, timing))
            .Select(i => new SourceUnitTokenContext(i.CardNo, new(source.ObjectId, source.ObjectGeneration), player,
                0, source.CopyFaceTransitions?.Count ?? 0, timing, field, i.Origin, source.TokenFactoryCardNo)).ToArray();

    private static bool SourceUnitTokenTiming(string no, string timing)
        => SourceUnitTokenSpecs.Value.TryGetValue(no, out var spec) && (timing == "PLAY"
            || timing == "SCORE" && spec.Kind == TriggerKinds.SourceUnitPlayedOrScoredCreateUnit
            || timing == "EXHAUST" && spec.Kind == TriggerKinds.SourceUnitPlayedOrExhaustedCreateUnit);

    private static SourceUnitTokenContext[] CaptureSourceExhaustionTokens(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState unit)
        => unit.IsExhausted || !IsFaceUpNonStandbyUnit(unit) || !IsObjectOnField(zones, unit.ObjectId) ? []
            : CaptureSourceUnitTokens(zones, cards, unit, EffectiveFieldControllerId(zones, unit.ObjectId, unit), "EXHAUST");

    private static GameEvent CaptureSourceExhaustionTokens(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, IEnumerable<CardObjectState> units, bool captureOtherObservers = true)
    {
        var originals = units.ToArray();
        if (captureOtherObservers) ev = CaptureUnitExhaustionObservers(ev, zones, cards, originals);
        var captures = originals.SelectMany(unit => CaptureSourceExhaustionTokens(zones, cards, unit)).ToArray();
        return captures.Length == 0 ? ev : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            [SourceUnitTokenObservationKey] = captures } };
    }

    private static GameEvent CaptureSourcePlayTokens(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState entered, string player)
        => ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED") ? ev
            : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
                [SourceUnitTokenObservationKey] = CaptureSourceUnitTokens(zones, cards, entered, player, "PLAY") } };

    // CN 470-473: scoring remains an action when the last point becomes a draw.
    private static SourceUnitTokenContext[] CaptureScoringUnitTokens(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        string player, string battlefield)
    {
        var atScore = state with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        return zones.Values.SelectMany(z => z.Battlefields).Distinct(StringComparer.Ordinal)
            .Where(id => cards.TryGetValue(id, out var c) && !c.IsFaceDown && c.Tags.Contains(CardObjectTags.UnitCard)
                && EffectiveFieldControllerId(zones, id, c) == player && BattlefieldLocalRules.AtUnit(atScore, id)?.ObjectId == battlefield)
            .Order(StringComparer.Ordinal).SelectMany(id => CaptureSourceUnitTokens(zones, cards, cards[id], player, "SCORE", battlefield)).ToArray();
    }

    private static ResolutionResult QueueSourceUnitTokens(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        // Costs can exhaust a unit without emitting the ordinary exhaust event.
        // Entry in an exhausted state is excluded by the pre-existing field binding.
        // Explicit instruction captures take precedence, including when a later
        // instruction readies, transforms or removes their source.
        var observed = result.Events.SelectMany(ev => ev.Payload.GetValueOrDefault(SourceUnitTokenObservationKey)
                as SourceUnitTokenContext[] ?? [])
            .Concat(result.State.TriggerQueue.Select(t => t.SourceUnitTokens).OfType<SourceUnitTokenContext>())
            .Concat(result.State.StackItems.Select(i => i.SourceUnitTokens).OfType<SourceUnitTokenContext>())
            .Where(c => c.Timing == "EXHAUST" && (c.CapturedTick == 0 || c.CapturedTick == result.State.Tick))
            .Select(c => c.Source).ToHashSet();
        var unrecorded = before.CardObjects.Values.Where(unit => !observed.Contains(new(unit.ObjectId, unit.ObjectGeneration))
            && result.State.CardObjects.TryGetValue(unit.ObjectId, out var after)
            && after.ObjectGeneration == unit.ObjectGeneration && after.CardNo == unit.CardNo
            && (after.CopyFaceTransitions?.Count ?? 0) == (unit.CopyFaceTransitions?.Count ?? 0) && after.IsExhausted)
            .OrderBy(unit => unit.ObjectId, StringComparer.Ordinal).ToArray();
        var costObservation = CaptureSourceExhaustionTokens(new("INTERNAL_SOURCE_EXHAUSTION", "", new Dictionary<string, object?>()),
            before.PlayerZones, before.CardObjects, unrecorded, captureOtherObservers: false);
        if (costObservation.Payload.ContainsKey(SourceUnitTokenObservationKey))
            result = result with { Events = result.Events.Append(costObservation).ToArray() };
        var events = result.Events.ToList(); var queue = result.State.TriggerQueue.ToList();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i))) {
            if (ev.Payload.GetValueOrDefault(SourceUnitTokenObservationKey) is not SourceUnitTokenContext[] observations) continue;
            events[index] = ev with { Payload = ev.Payload.Where(p => p.Key != SourceUnitTokenObservationKey).ToDictionary(p => p.Key, p => p.Value) };
            foreach (var (captured, instance) in observations.Select((c, i) => (c, i))) {
                var generation = captured.Source.Generation;
                if (captured.Timing == "PLAY" && before.CardObjects.TryGetValue(captured.Source.ObjectId, out var old)
                    && !IsObjectOnField(before.PlayerZones, old.ObjectId)) generation = Math.Max(generation, old.ObjectGeneration + 1);
                var context = captured with { Source = captured.Source with { Generation = generation }, CapturedTick = result.State.Tick };
                if (result.State.Status != MatchStatuses.InProgress) continue;
                var trigger = new TriggerQueueItemState($"source-token-{result.State.Tick}-{index}-{instance}",
                    context.ControllerId, context.Source.ObjectId, SourceUnitTokenEffect,
                    context.Timing == "EXHAUST" ? "UNIT_EXHAUSTED" : ev.Kind) {
                    SourceCardNo = context.CardNo, SourceUnitTokens = context };
                queue.Add(trigger);
                var queued = BuildTriggerQueuedEvent(trigger);
                events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, context.Source.ObjectId, "__spectator__") } });
            }
        }
        events.RemoveAll(ev => ev.Kind == "INTERNAL_SOURCE_EXHAUSTION");
        return result with { State = result.State with { TriggerQueue = queue }, Events = events };
    }

    internal static bool ValidSourceUnitTokens(MatchState state, SourceUnitTokenContext? context,
        string effect, string source, string controller, string? no = null)
    {
        if (context is null) return effect != SourceUnitTokenEffect;
        if (effect != SourceUnitTokenEffect || context.Source is not { Generation: >= 0 } || context.Source.ObjectId != source
            || context.ControllerId != controller || !state.Seats.ContainsKey(controller) || no is not null && no != context.CardNo
            || context.CapturedTick <= 0 || context.CapturedTick > state.Tick || context.FaceRevision < 0
            || SourceUnitTokenFactory(context.CardNo) is null || !SourceUnitTokenTiming(context.CardNo, context.Timing)
            || (context.Timing == "SCORE"
                ? context.BattlefieldId is null || !state.BattlefieldStates.ContainsKey(context.BattlefieldId)
                : context.BattlefieldId is not null)) return false;
        return context.Origin is { } origin
            ? AttachedRuleTextRules.ValidCaptured(state, origin, source, context.Source.Generation, context.CardNo)
            : PredictBindingMatches(state, context.Source, context.CardNo, context.FaceRevision, context.CapturedTick, context.TokenFactoryCardNo);
    }

    private static StackResolutionResult ResolveSourceUnitTokens(MatchState state, StackItemState item)
    {
        if (!ValidSourceUnitTokens(state, item.SourceUnitTokens, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo))
            throw new InvalidOperationException("Invalid source unit token trigger.");
        if (BeginTokenReplacement(state, item) is { } replacement)
            return replacement;
        var spec = SourceUnitTokenSpecs.Value[item.SourceUnitTokens!.CardNo];
        return CreateUnitTokenBatch(state, item, SourceUnitTokenFactory(item.SourceUnitTokens.CardNo)!.CardNo,
            spec.CreatedTokenCount!.Value + AdditionalUnitTokens(item), entersReady: spec.CreatedTokenExhausted != true);
    }
}
