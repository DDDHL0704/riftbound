using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record SivirRuneRecycleContext(string CardNo, long SourceGeneration);

public sealed partial class CoreRuleEngine
{
    internal const string SivirRuneRecycleEffect = "SIVIR_RUNE_RECYCLE_GOLD";
    private sealed record SivirRuneObservation(string ObjectId, string Controller, string CardNo, long Generation, int Count);

    internal static bool ValidSivirRuneContext(SivirRuneRecycleContext context, string effect, string? cardNo = null)
        => effect == SivirRuneRecycleEffect && context.SourceGeneration >= 0
            && LegendCardHasIdentity(context.CardNo, SivirLegendIdentityId) && (cardNo is null || cardNo == context.CardNo);

    internal static bool ValidSivirRuneSource(MatchState state, SivirRuneRecycleContext context, string source)
        => state.CardObjects.TryGetValue(source, out var card) && card.ObjectGeneration >= context.SourceGeneration
            && (card.ObjectGeneration > context.SourceGeneration || card.CardNo == context.CardNo);

    // Capture at the recycling instruction, before private identities are removed
    // or later effects change the legend. This metadata never enters the journal.
    private static GameEvent CaptureRecycleObservers(GameEvent ev, Dictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, IReadOnlyList<string>? recycledIds = null)
    {
        ev = CaptureRecycledCardBoons(ev, zones, cards, recycledIds);
        var count = ev.Kind == "RUNE_RECYCLED" ? 1
            : (recycledIds ?? PayloadStringValues(ev, "cardIds")).Count(id =>
                cards.TryGetValue(id, out var card) && card.Tags.Contains(CardObjectTags.RuneCard));
        if (count == 0 || ev.Payload.GetValueOrDefault("playerId") is not string player
            || !TryGetSivirLegend(zones, cards, player, out var id, out var legend) || legend.IsFaceDown) return ev;
        return ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            ["sivirRuneObservation"] = new SivirRuneObservation(id, player, legend.CardNo!, legend.ObjectGeneration, count) } };
    }

    private static ResolutionResult QueueSivirRuneRecycles(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = new List<GameEvent>();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i))) {
            var observation = ev.Payload.GetValueOrDefault("sivirRuneObservation") as SivirRuneObservation;
            events.Add(ev with {
                Payload = ev.Payload.Where(p => p.Key != "sivirRuneObservation").ToDictionary(p => p.Key, p => p.Value) });
            if (observation is null) continue;
            for (var ordinal = 0; ordinal < observation.Count; ordinal++) {
                var trigger = new TriggerQueueItemState($"sivir-rune-{result.State.Tick}-{index}-{ordinal}", observation.Controller,
                    observation.ObjectId, SivirRuneRecycleEffect, "RUNE_RECYCLED") {
                    SivirRuneRecycle = new(observation.CardNo, observation.Generation) };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        if (queue.Count == result.State.TriggerQueue.Count) return result;
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static StackResolutionResult ResolveSivirRuneRecycle(MatchState state, StackItemState item)
    {
        if (item.TriggerCost is null) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent> { new("LEGEND_TRIGGER_RESOLVED", "已支付希维尔休眠费用，打出休眠金币", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["legendObjectId"] = item.SourceObjectId, ["legendCardNo"] = item.CardNo,
            ["trigger"] = "RUNE_RECYCLED_CREATE_GOLD" }) };
        CreateLegendEquipmentToken(zones, cards, item.ControllerId, item.SourceObjectId,
            "LEGEND_TRIGGER_RUNE_RECYCLED_CREATE_GOLD", "金币", [CardObjectTags.EquipmentCard], true, events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
