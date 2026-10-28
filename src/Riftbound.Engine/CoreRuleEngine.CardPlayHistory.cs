using Riftbound.CardCatalog;
using Riftbound.Contracts;
using System.Text.Json.Serialization;

namespace Riftbound.Engine;

// Confirmation belongs to the player who finalized the card. Successful
// resolution belongs to its controller at resolution (CN 420.4 / Vendetta FAQ).
public sealed record CardPlayHistoryEntry(string PlayId, string PlayerId, ObjectBinding Source,
    string CardNo, int Types, long Tick, long FaceRevision,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OriginHandPlayerId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool OriginOutsideHands = false);
public sealed record TurnCardPlayHistory(int TurnNumber,
    [property: JsonRequired] IReadOnlyList<CardPlayHistoryEntry> Finalized,
    [property: JsonRequired] IReadOnlyList<CardPlayHistoryEntry> Resolved);

public sealed partial class CoreRuleEngine
{
    // The common case (the finalizer's own hand) remains absent in serialized
    // histories. Other zones and another player's hand carry explicit provenance.
    private const string CardPlayHistoryObservationKey = "cardPlayHistoryObservation";
    private static (string? OtherHandPlayerId, bool OutsideHands) CardPlayHandOrigin(MatchState state, string objectId, string player)
    {
        var handOwner = state.PlayerZones.FirstOrDefault(pair => pair.Value.Hand.Contains(objectId, StringComparer.Ordinal)).Key;
        return (handOwner == player ? null : handOwner, handOwner is null);
    }
    private static bool CompletedPlayFromOutsideHand(CardPlayHistoryEntry? confirmed, CardPlayHistoryEntry completed)
        => confirmed is not null && (confirmed.OriginOutsideHands
            || (confirmed.OriginHandPlayerId ?? confirmed.PlayerId) != completed.PlayerId);
    private sealed record CardPlayHistoryObservation(CardPlayHistoryEntry Entry, bool Resolved, string Zone,
        OutsideHandPlayObserverCandidate[] Observers, HighCostPlayObserverCandidate[] HighCostObservers);
    private static readonly Lazy<IReadOnlyDictionary<string, int>> PrintedPlayTypes = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo,
            c => (c.CardCategoryNameList.Any(n => n.Contains("单位", StringComparison.Ordinal)) ? 1 : 0)
                | (c.CardCategoryNameList.Any(n => n.Contains("装备", StringComparison.Ordinal)) ? 2 : 0)
                | (c.CardCategoryNameList.Any(n => n.Contains("法术", StringComparison.Ordinal)) ? 4 : 0), StringComparer.Ordinal));

    private static GameEvent CaptureCardPlayHistory(GameEvent ev, CardObjectState card, string player,
        string? playId, bool resolved, string zone, string? originHandPlayerId = null, bool originOutsideHands = false,
        IReadOnlyDictionary<string, PlayerZones>? zones = null, IReadOnlyDictionary<string, CardObjectState>? cards = null)
    {
        var payload = ev.Payload.Where(p => p.Key != "historyPlayId").ToDictionary(p => p.Key, p => p.Value);
        if (card.CardNo is not { } no || TokenObjectRules.IsToken(card)
            || !PrintedPlayTypes.Value.TryGetValue(no, out var types) || types == 0)
            return ev with { Payload = payload };
        payload[CardPlayHistoryObservationKey] = new CardPlayHistoryObservation(
            new(playId ?? "", player, new(card.ObjectId, card.ObjectGeneration), no, types, 0,
                card.CopyFaceTransitions?.Count ?? 0, originHandPlayerId, originOutsideHands), resolved, zone,
            resolved && zones is not null && cards is not null ? CaptureOutsideHandPlayObservers(zones, cards, player) : [],
            resolved && zones is not null && cards is not null ? CaptureHighCostPlayObservers(zones, cards, player) : []);
        return ev with { Payload = payload };
    }

    private static ResolutionResult RecordCardPlayHistory(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var history = result.State.CardPlays;
        var finalized = (history.TurnNumber == before.TurnNumber ? history.Finalized : []).ToList();
        var resolved = (history.TurnNumber == before.TurnNumber ? history.Resolved : []).ToList();
        var events = new List<GameEvent>();
        var queue = result.State.TriggerQueue.ToList();
        var locations = IdentityLocations(before);
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            if (ev.Payload.GetValueOrDefault(CardPlayHistoryObservationKey) is not CardPlayHistoryObservation observation)
            { if (ev.Kind != "INTERNAL_CARD_PLAY_COMPLETION") events.Add(ev); continue; }
            if (ev.Kind != "INTERNAL_CARD_PLAY_COMPLETION")
                events.Add(ev with { Payload = ev.Payload.Where(p => p.Key != CardPlayHistoryObservationKey)
                    .ToDictionary(p => p.Key, p => p.Value) });
            var entry = observation.Entry;
            var generation = entry.Source.Generation;
            if (before.CardObjects.TryGetValue(entry.Source.ObjectId, out var old)
                && locations.TryGetValue(entry.Source.ObjectId, out var oldLocation)
                && oldLocation.Zone != observation.Zone
                && (!IsIdentityFieldZone(oldLocation.Zone) || !IsIdentityFieldZone(observation.Zone)))
                generation = Math.Max(generation, old.ObjectGeneration + 1);
            var limited = entry.PlayId.Length == 0;
            if (limited) entry = entry with { OriginOutsideHands = true };
            entry = entry with { PlayId = limited ? $"LIMITED-{result.State.Tick}-{index}-{entry.Source.ObjectId}" : entry.PlayId,
                Tick = result.State.Tick, Source = entry.Source with { Generation = generation } };
            // Limited effect plays complete the play instruction directly. They
            // must not borrow a previous countered play of this physical card.
            if (!observation.Resolved || limited)
                if (!finalized.Any(p => p.PlayId == entry.PlayId)) finalized.Add(entry);
            if (observation.Resolved && !resolved.Any(p => p.PlayId == entry.PlayId))
            {
                var confirmed = finalized.FirstOrDefault(p => p.PlayId == entry.PlayId);
                entry = entry with { OriginHandPlayerId = confirmed?.OriginHandPlayerId,
                    OriginOutsideHands = confirmed?.OriginOutsideHands ?? false };
                resolved.Add(entry);
                if ((entry.Types & 3) != 0 && PrintedManaCost(entry.CardNo) is { } mana && result.State.Status == MatchStatuses.InProgress)
                {
                    foreach (var (observer, ordinal) in observation.HighCostObservers.Select((value, ordinal) => (value, ordinal)))
                    {
                        if (mana < HighCostPlaySpec(observer.CardNo)!.MinimumCardMana) continue;
                        var context = observer.ToContext(result.State.Tick, entry, null);
                        var trigger = new TriggerQueueItemState($"high-cost-play-{result.State.Tick}-{index}-{ordinal}",
                            entry.PlayerId, context.Source.ObjectId, HighCostPlayEffect, "CARD_PLAY_COMPLETED")
                            { HighCostPlayObserver = context, SourceCardNo = context.CardNo };
                        queue.Add(trigger);
                        var queued = BuildTriggerQueuedEvent(trigger);
                        events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, context.Source.ObjectId, "__spectator__") } });
                    }
                }
                if (CompletedPlayFromOutsideHand(confirmed, entry) && result.State.Status == MatchStatuses.InProgress)
                {
                    foreach (var (observer, ordinal) in observation.Observers.Select((value, ordinal) => (value, ordinal)))
                    {
                        var context = observer.ToContext(entry, result.State.Tick);
                        var trigger = new TriggerQueueItemState($"outside-hand-play-{result.State.Tick}-{index}-{ordinal}",
                            entry.PlayerId, context.Source.ObjectId, OutsideHandPlayObserverEffect, "CARD_PLAY_COMPLETED")
                            { OutsideHandPlayObserver = context, SourceCardNo = context.CardNo };
                        queue.Add(trigger);
                        var queued = BuildTriggerQueuedEvent(trigger);
                        events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, context.Source.ObjectId, "__spectator__") } });
                    }
                }
            }
        }
        var nextHistory = result.State.TurnNumber == before.TurnNumber
            ? new TurnCardPlayHistory(before.TurnNumber, finalized, resolved)
            : new TurnCardPlayHistory(result.State.TurnNumber, [], []);
        var next = result.State with { CardPlays = nextHistory, TriggerQueue = queue };
        return result with { State = next, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static bool ValidCardPlayHistory(MatchState state)
    {
        var history = state.CardPlays;
        if (history is null || history.Finalized is null || history.Resolved is null
            || history.TurnNumber < 0 || history.TurnNumber > state.TurnNumber
            || history.TurnNumber != state.TurnNumber && (history.Finalized.Count != 0 || history.Resolved.Count != 0)) return false;
        bool Valid(CardPlayHistoryEntry? e)
        {
            if (e is null || e.Source is not { Generation: >= 0 }
                || string.IsNullOrWhiteSpace(e.PlayerId) || string.IsNullOrWhiteSpace(e.Source.ObjectId)
                || string.IsNullOrWhiteSpace(e.CardNo) || !state.Seats.ContainsKey(e.PlayerId)
                || e.OriginHandPlayerId is not null && (!state.Seats.ContainsKey(e.OriginHandPlayerId) || e.OriginOutsideHands)
                || e.Tick <= 0 || e.Tick > state.Tick || e.FaceRevision < 0
                || !PrintedPlayTypes.Value.TryGetValue(e.CardNo, out var types) || types == 0 || e.Types != types
                || !state.CardObjects.TryGetValue(e.Source.ObjectId, out var card)
                || card.TokenFactoryCardNo is not null || card.ObjectGeneration < e.Source.Generation
                || string.IsNullOrWhiteSpace(e.PlayId)
                || !(e.PlayId.StartsWith("STACK-", StringComparison.Ordinal) || e.PlayId.StartsWith("LIMITED-", StringComparison.Ordinal))
                || !e.PlayId.EndsWith("-" + e.Source.ObjectId, StringComparison.Ordinal)) return false;
            if (e.PlayId.StartsWith("STACK-", StringComparison.Ordinal))
            {
                if (e.PlayId.Length <= e.Source.ObjectId.Length + 7
                    || !long.TryParse(e.PlayId[6..^(e.Source.ObjectId.Length + 1)], out var tick)
                    || tick <= 0 || tick > e.Tick || e.PlayId != $"STACK-{tick}-{e.Source.ObjectId}") return false;
            }
            else
            {
                if (e.PlayId.Length <= e.Source.ObjectId.Length + 9) return false;
                var parts = e.PlayId[8..^(e.Source.ObjectId.Length + 1)].Split('-');
                if (parts.Length != 2 || !long.TryParse(parts[0], out var tick) || tick != e.Tick
                    || !int.TryParse(parts[1], out var index) || index < 0
                    || e.PlayId != $"LIMITED-{tick}-{index}-{e.Source.ObjectId}") return false;
            }
            return card.ObjectGeneration != e.Source.Generation
                || CopyFaceTimeline.Valid(state, card) && CopyFaceTimeline.FaceAt(card, e.FaceRevision, e.Tick) == e.CardNo;
        }
        foreach (var entries in new[] { history.Finalized, history.Resolved })
            if (entries.Any(e => !Valid(e)) || entries.Select(e => e.PlayId).Distinct(StringComparer.Ordinal).Count() != entries.Count) return false;
        foreach (var completed in history.Resolved)
            if (history.Finalized.FirstOrDefault(e => e.PlayId == completed.PlayId) is { } confirmed
                && (confirmed.CardNo != completed.CardNo || confirmed.Types != completed.Types
                    || confirmed.Source.ObjectId != completed.Source.ObjectId || confirmed.Tick > completed.Tick
                    || confirmed.Source.Generation > completed.Source.Generation
                    || confirmed.OriginHandPlayerId != completed.OriginHandPlayerId
                    || confirmed.OriginOutsideHands != completed.OriginOutsideHands)) return false;
        return true;
    }
}
