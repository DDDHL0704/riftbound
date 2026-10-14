using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record PrintedPredictEntryOrigin(long FaceRevision, long CapturedTick,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? TokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    private sealed record PrintedPredictObservation(string ObjectId, string CardNo, long Generation,
        string Controller, long FaceRevision, string? TokenFactoryCardNo);

    private static readonly Lazy<IReadOnlySet<string>> PrintedPredictFaces = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Where(card => card.CardEffect.Split('\n').Any(line => line.TrimStart().StartsWith("{{预知}}", StringComparison.Ordinal)))
            .Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal));

    internal static bool HasPrintedPredict(string? cardNo) => cardNo is not null && PrintedPredictFaces.Value.Contains(cardNo);

    private static bool ValidPrintedPredict(string cardNo, PrintedPredictEntryOrigin? origin)
        => HasPrintedPredict(cardNo) && origin is { FaceRevision: >= 0, CapturedTick: >= 0 }
            && (origin.TokenFactoryCardNo is null || P6TokenFactoryCatalog.IsTokenFactory(origin.TokenFactoryCardNo));

    internal static bool ValidPredictEntryBindings(MatchState state, InsightTriggerContext context, string source)
        => context.Kind == "PRINTED_PREDICT"
            ? ValidPrintedPredict(context.CardNo, context.PrintedPredict) && context.PrintedPredict is { } origin
                && origin.CapturedTick <= state.Tick && PredictBindingMatches(state, new(source, context.SourceGeneration),
                    context.CardNo, origin.FaceRevision, origin.CapturedTick, origin.TokenFactoryCardNo)
            : ValidGrantedPredictBindings(state, context, source);

    private static GameEvent CapturePrintedPredict(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones, CardObjectState entered)
    {
        if (entered.IsFaceDown || !HasPrintedPredict(entered.CardNo)
            || !entered.Tags.Any(t => t is CardObjectTags.UnitCard or CardObjectTags.EquipmentCard)
            || EffectiveFieldControllerId(zones, entered.ObjectId, entered) is not { Length: > 0 } controller) return ev;
        return ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            ["printedPredictObservation"] = new PrintedPredictObservation(entered.ObjectId, entered.CardNo!, entered.ObjectGeneration,
                controller, entered.CopyFaceTransitions?.Count ?? 0, entered.TokenFactoryCardNo) } };
    }

    private static ResolutionResult QueuePrintedPredict(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = result.Events.ToList();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i))) {
            if (ev.Payload.GetValueOrDefault("printedPredictObservation") is not PrintedPredictObservation observed) continue;
            events[index] = ev with { Payload = ev.Payload.Where(p => p.Key != "printedPredictObservation").ToDictionary(p => p.Key, p => p.Value) };
            var generation = observed.Generation;
            if (before.CardObjects.TryGetValue(observed.ObjectId, out var old) && !IsObjectOnField(before.PlayerZones, observed.ObjectId))
                generation = Math.Max(generation, old.ObjectGeneration + 1);
            var trigger = new TriggerQueueItemState($"printed-predict-entry-{result.State.Tick}-{index}", observed.Controller,
                observed.ObjectId, InsightTriggerEffect, ev.Kind) {
                InsightContext = new(observed.CardNo, observed.Controller, 1, generation, "PRINTED_PREDICT",
                    PrintedPredict: new(observed.FaceRevision, result.State.Tick, observed.TokenFactoryCardNo)) };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        if (events.Count == result.Events.Count) return result;
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}
