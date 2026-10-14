using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record RecycledCardBoonOrigin(string PlayerId, int CardCount, long CapturedTick, long FaceRevision);

public sealed partial class CoreRuleEngine
{
    private static readonly Lazy<IReadOnlySet<string>> RecycledCardBoonFaces = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Where(c => c.CardEffect.Contains("每当你将任意数量的卡牌回收到自己的主牌堆时，给予一名友方单位增益", StringComparison.Ordinal))
            .Select(c => c.CardNo).ToHashSet(StringComparer.Ordinal));
    private static bool HasRecycledCardBoon(string? cardNo) => cardNo is not null && RecycledCardBoonFaces.Value.Contains(cardNo);
    private sealed record CapturedRecycleBoon(string SourceId, string Controller, FieldTriggerContext Context);

    private static GameEvent CaptureRecycledCardBoons(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, IReadOnlyList<string>? recycledIds)
    {
        if (ev.Kind != "CARDS_RECYCLED" || ev.Payload.GetValueOrDefault("playerId") is not string player
            || !zones.TryGetValue(player, out var own)) return ev;
        var count = (recycledIds ?? PayloadStringValues(ev, "cardIds")).Distinct(StringComparer.Ordinal).Count(id =>
            own.MainDeck.Contains(id) && cards.TryGetValue(id, out var card) && card.OwnerId == player
                && !card.Tags.Contains(CardObjectTags.RuneCard) && !IsTokenObject(card));
        if (count == 0) return ev;
        var sources = cards.Values.Where(c => !c.IsFaceDown && c.Tags.Contains(CardObjectTags.UnitCard)
            && c.ControllerId == player && IsObjectOnField(zones, c.ObjectId))
            .OrderBy(c => c.ObjectId, StringComparer.Ordinal)
            .SelectMany(c => AttachedRuleTextRules.Instances(zones, cards, c).Where(i => HasRecycledCardBoon(i.CardNo))
                .Select(i => new CapturedRecycleBoon(c.ObjectId, player,
                    new(i.CardNo, "RECYCLE_BOON", c.ObjectGeneration) { Origin = i.Origin,
                        SourceTokenFactoryCardNo = c.TokenFactoryCardNo,
                        RecycledBoon = new(player, count, 0, c.CopyFaceTransitions?.Count ?? 0) }))).ToArray();
        return sources.Length == 0 ? ev : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            ["recycledCardBoonSources"] = sources } };
    }

    private static ResolutionResult QueueRecycledCardBoons(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = new List<GameEvent>();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            events.Add(ev with { Payload = ev.Payload.Where(p => p.Key != "recycledCardBoonSources").ToDictionary(p => p.Key, p => p.Value) });
            if (ev.Payload.GetValueOrDefault("recycledCardBoonSources") is not CapturedRecycleBoon[] sources) continue;
            foreach (var (source, ordinal) in sources.Select((s, i) => (s, i)))
            {
                var trigger = new TriggerQueueItemState($"recycle-boon-{result.State.Tick}-{index}-{ordinal}",
                    source.Controller, source.SourceId, FieldTriggerEffect, "CARDS_RECYCLED") {
                    FieldContext = source.Context with { RecycledBoon = source.Context.RecycledBoon! with { CapturedTick = result.State.Tick } } };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        if (queue.Count == result.State.TriggerQueue.Count) return result;
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool ValidRecycledBoonSource(MatchState state, FieldTriggerContext context, string id, string controller)
        => context.RecycledBoon is { CardCount: > 0, CapturedTick: >= 0, FaceRevision: >= 0 } origin
            && origin.PlayerId == controller && state.Seats.ContainsKey(controller) && origin.CapturedTick <= state.Tick
            && (!state.CardObjects.TryGetValue(id, out var source) || source.TokenFactoryCardNo == context.SourceTokenFactoryCardNo)
            && (context.Origin is null
                ? PredictBindingMatches(state, new(id, context.SourceGeneration), context.CardNo, origin.FaceRevision,
                    origin.CapturedTick, context.SourceTokenFactoryCardNo)
                : AttachedRuleTextRules.ValidCaptured(state, context.Origin, id, context.SourceGeneration, context.CardNo));
}
