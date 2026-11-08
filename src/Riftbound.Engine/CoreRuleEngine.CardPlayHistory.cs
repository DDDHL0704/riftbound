using Riftbound.CardCatalog;
using Riftbound.Contracts;
using System.Text.Json.Serialization;

namespace Riftbound.Engine;

// Confirmation belongs to the player who finalized the card. Successful
// resolution belongs to its controller at resolution (CN 420.4 / Vendetta FAQ).
public sealed record CardPlayHistoryEntry(string PlayId, string PlayerId, ObjectBinding Source,
    string CardNo, int Types, long Tick, long FaceRevision,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OriginHandPlayerId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool OriginOutsideHands = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ReturnedEquipmentManaCost = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ShowcaseReceipt? Showcase = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<OptionalPlayCostReductionUse>? OptionalPlayCostReductions = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? PaidMana = null);
public sealed record TurnCardPlayHistory(int TurnNumber,
    [property: JsonRequired] IReadOnlyList<CardPlayHistoryEntry> Finalized,
    [property: JsonRequired] IReadOnlyList<CardPlayHistoryEntry> Resolved);

public sealed partial class CoreRuleEngine
{
    // The common case (the finalizer's own hand) remains absent in serialized
    // histories. Other zones and another player's hand carry explicit provenance.
    private const string CardPlayHistoryObservationKey = "cardPlayHistoryObservation";
    private const string PlayedPermanentToBattlefieldPrefix = "PLAYED_PERMANENT_TO_BATTLEFIELD_THIS_TURN:";
    private static (string? OtherHandPlayerId, bool OutsideHands) CardPlayHandOrigin(MatchState state, string objectId, string player)
    {
        var handOwner = state.PlayerZones.FirstOrDefault(pair => pair.Value.Hand.Contains(objectId, StringComparer.Ordinal)).Key;
        return (handOwner == player ? null : handOwner, handOwner is null);
    }
    private static bool CompletedPlayFromOutsideHand(CardPlayHistoryEntry? confirmed, CardPlayHistoryEntry completed)
        => confirmed is not null && (confirmed.OriginOutsideHands
            || (confirmed.OriginHandPlayerId ?? confirmed.PlayerId) != completed.PlayerId);
    private sealed record CardPlayHistoryObservation(CardPlayHistoryEntry Entry, bool Resolved, string Zone,
        IReadOnlyList<UnitEventObservation> FirstCardObservers,
        OutsideHandPlayObserverCandidate[] Observers, HighCostPlayObserverCandidate[] HighCostObservers,
        UnitEventObservation[] CardPlayObservers);
    private static readonly Lazy<IReadOnlyDictionary<string, int>> PrintedPlayTypes = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo,
            c => (c.CardCategoryNameList.Any(n => n.Contains("单位", StringComparison.Ordinal)) ? 1 : 0)
                | (c.CardCategoryNameList.Any(n => n.Contains("装备", StringComparison.Ordinal)) ? 2 : 0)
                | (c.CardCategoryNameList.Any(n => n.Contains("法术", StringComparison.Ordinal)) ? 4 : 0), StringComparer.Ordinal));

    private static GameEvent CaptureCardPlayHistory(GameEvent ev, CardObjectState card, string player,
        string? playId, bool resolved, string zone, string? originHandPlayerId = null, bool originOutsideHands = false,
        IReadOnlyDictionary<string, PlayerZones>? zones = null, IReadOnlyDictionary<string, CardObjectState>? cards = null,
        ShowcaseReceipt? showcase = null)
    {
        var payload = ev.Payload.Where(p => p.Key != "historyPlayId").ToDictionary(p => p.Key, p => p.Value);
        if (card.CardNo is not { } no || TokenObjectRules.IsToken(card)
            || !PrintedPlayTypes.Value.TryGetValue(no, out var types) || types == 0)
            return ev with { Payload = payload };
        payload[CardPlayHistoryObservationKey] = new CardPlayHistoryObservation(
            new(playId ?? "", player, new(card.ObjectId, card.ObjectGeneration), no, types, 0,
                card.CopyFaceTransitions?.Count ?? 0, originHandPlayerId, originOutsideHands, Showcase: showcase), resolved, zone,
            CaptureFirstCardObservers(zones, cards, player),
            resolved && zones is not null && cards is not null ? CaptureOutsideHandPlayObservers(zones, cards, player) : [],
            resolved && zones is not null && cards is not null ? CaptureHighCostPlayObservers(zones, cards, player) : [],
            resolved && zones is not null && cards is not null
                ? CaptureCardPlayUnitEvents(zones, cards, player, card.ObjectId) : []);
        return ev with { Payload = payload };
    }

    // Capture at successful completion, before cleanup can remove an observer.
    // An entering unit can observe its own play (Origins judge FAQ 7.1).
    private static UnitEventObservation[] CaptureCardPlayUnitEvents(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        string player, string playedSource)
    {
        var friendly = cards.Values.Where(card => EffectiveFieldControllerId(zones, card.ObjectId, card) == player)
            .Select(card => card.ObjectId);
        return CaptureUnitEvents(zones, cards, friendly, "SECOND_CARD_PLAYED")
            .Concat(CaptureUnitEvents(zones, cards, [playedSource], "PLAY_AFTER_ANOTHER_CARD"))
            .Where(observation => observation.Controller == player).ToArray();
    }

    private static ResolutionResult RecordCardPlayHistory(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        result = RecordNextUnitPlayGrants(result);
        result = RecordActivatedNextUnitReady(result);
        result = RecordTimeAssassinNextUnitReductions(result);
        var history = result.State.CardPlays;
        var finalized = (history.TurnNumber == before.TurnNumber ? history.Finalized : []).ToList();
        var resolved = (history.TurnNumber == before.TurnNumber ? history.Resolved : []).ToList();
        var events = new List<GameEvent>();
        var queue = result.State.TriggerQueue.ToList();
        var observers = new List<UnitEventObservation>();
        var cardPlayObservers = new List<UnitEventObservation>();
        var effects = result.State.UntilEndOfTurnEffects;
        var locations = IdentityLocations(before);
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            if (ev.Payload.GetValueOrDefault(CardPlayHistoryObservationKey) is not CardPlayHistoryObservation observation)
            { if (ev.Kind != "INTERNAL_CARD_PLAY_COMPLETION") events.Add(ev); continue; }
            var publicEventIndex = events.Count;
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
            var confirmedItem = result.State.StackItems.Concat(before.StackItems).FirstOrDefault(item => item.StackItemId == entry.PlayId);
            var costReceipt = confirmedItem?.ReturnedEquipmentManaCost;
            var optionalPlayReductions = result.State.UntilEndOfTurnEffects.Select(ReadPlayCostReductionUse)
                .OfType<OptionalPlayCostReductionUse>().Where(use => use.PlayId == entry.PlayId).ToArray();
            entry = entry with { ReturnedEquipmentManaCost = costReceipt,
                PaidMana = !observation.Resolved && (entry.Types & 4) != 0 ? confirmedItem?.PlayCost?.PaidMana : null,
                OptionalPlayCostReductions = optionalPlayReductions.Length == 0 ? null : optionalPlayReductions,
                Showcase = entry.Showcase ?? result.State.StackItems.Concat(before.StackItems).FirstOrDefault(item => item.StackItemId == entry.PlayId)?.Showcase };
            var limited = entry.PlayId.Length == 0;
            if (limited) entry = entry with { OriginOutsideHands = true };
            entry = entry with { PlayId = limited ? $"LIMITED-{result.State.Tick}-{index}-{entry.Source.ObjectId}" : entry.PlayId,
                Tick = result.State.Tick, Source = entry.Source with { Generation = generation } };
            if (!observation.Resolved && entry.PaidMana is { } paidMana && ev.Kind == "CARD_PLAYED")
            {
                var publicEvent = events[publicEventIndex];
                events[publicEventIndex] = publicEvent with { Payload = new Dictionary<string, object?>(publicEvent.Payload) {
                    ["spellManaPayment"] = new ConfirmedSpellManaPayment(entry.Source, entry.CardNo, entry.PlayerId,
                        entry.PlayId, paidMana, before.TurnNumber, entry.Tick) } };
            }
            // Limited effect plays complete the play instruction directly. They
            // must not borrow a previous countered play of this physical card.
            if (!observation.Resolved || limited)
                if (!finalized.Any(p => p.PlayId == entry.PlayId)) {
                    finalized.Add(entry);
                    effects = BindActivatedNextSpellDamage(effects, entry);
                    foreach (var reduction in SourceNextSpellCostReductionEffects(result.State with { UntilEndOfTurnEffects = effects }, entry.PlayerId)
                        .Where(r => r.AppliesToAllCards)) effects = RemoveUntilEndOfTurnEffect(effects, reduction.EffectId);
                }
            if (observation.Resolved && !resolved.Any(p => p.PlayId == entry.PlayId))
            {
                var confirmed = finalized.FirstOrDefault(p => p.PlayId == entry.PlayId);
                entry = entry with { OriginHandPlayerId = confirmed?.OriginHandPlayerId,
                    PaidMana = confirmed?.PaidMana,
                    OriginOutsideHands = confirmed?.OriginOutsideHands ?? false,
                    ReturnedEquipmentManaCost = confirmed?.ReturnedEquipmentManaCost ?? entry.ReturnedEquipmentManaCost,
                    Showcase = confirmed?.Showcase ?? entry.Showcase };
                resolved.Add(entry);
                // CN 420.4.a: play triggers observe successful completion.
                // Confirmation still determines which card was first. The
                // completed-play guard also prevents a second observation.
                if (finalized.FirstOrDefault(p => p.PlayerId == entry.PlayerId)?.PlayId == entry.PlayId)
                    observers.AddRange(observation.FirstCardObservers);
                if (PrintedManaCost(entry.CardNo) is { } mana && result.State.Status == MatchStatuses.InProgress)
                {
                    foreach (var (observer, ordinal) in observation.HighCostObservers.Select((value, ordinal) => (value, ordinal)))
                    {
                        if (!HighCostObservesCard(observer.CardNo, entry.Types) || mana < HighCostPlaySpec(observer.CardNo)!.MinimumCardMana) continue;
                        var context = observer.ToContext(result.State.Tick, entry, null);
                        var trigger = new TriggerQueueItemState($"high-cost-play-{result.State.Tick}-{index}-{ordinal}",
                            entry.PlayerId, context.Source.ObjectId, HighCostEffectFor(context.CardNo), "CARD_PLAY_COMPLETED")
                            { HighCostPlayObserver = context, SourceCardNo = context.CardNo };
                        queue.Add(trigger);
                        var queued = BuildTriggerQueuedEvent(trigger);
                        events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, context.Source.ObjectId, ProjectionViewers.Spectator) } });
                    }
                }
                if (result.State.Status == MatchStatuses.InProgress)
                {
                    if (result.State.PlayerCardsPlayedThisTurn.GetValueOrDefault(entry.PlayerId) >= 2)
                    {
                        // Confirmation fixes the ordinal; a later reaction can resolve first
                        // and raise the counter before the second card completes (CN 420.4).
                        var isSecondCard = finalized.Where(play => play.PlayerId == entry.PlayerId)
                            .Skip(1).FirstOrDefault()?.PlayId == entry.PlayId;
                        var hasOtherCard = finalized.Any(play => play.PlayerId == entry.PlayerId
                            && play.Source.ObjectId != entry.Source.ObjectId);
                        cardPlayObservers.AddRange(observation.CardPlayObservers.Where(observer =>
                            observer.Context.Timing == "SECOND_CARD_PLAYED" ? isSecondCard : hasOtherCard));
                    }
                    foreach (var (observer, ordinal) in observation.Observers.Select((value, ordinal) => (value, ordinal)))
                    {
                        if (observer.Effect == OutsideHandPlayObserverEffect && !CompletedPlayFromOutsideHand(confirmed, entry)
                            || observer.Effect == OpponentTurnPlayTokenEffect
                                && (entry.PlayerId == before.TurnPlayerId || result.State.TurnNumber != before.TurnNumber)) continue;
                        var context = observer.ToContext(entry, result.State.Tick, before.TurnPlayerId);
                        var trigger = new TriggerQueueItemState($"outside-hand-play-{result.State.Tick}-{index}-{ordinal}",
                            entry.PlayerId, context.Source.ObjectId, observer.Effect, "CARD_PLAY_COMPLETED")
                            { OutsideHandPlayObserver = context, SourceCardNo = context.CardNo };
                        queue.Add(trigger);
                        var queued = BuildTriggerQueuedEvent(trigger);
                        events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, context.Source.ObjectId, ProjectionViewers.Spectator) } });
                    }
                }
            }
        }
        var nextHistory = result.State.TurnNumber == before.TurnNumber
            ? new TurnCardPlayHistory(before.TurnNumber, finalized, resolved)
            : new TurnCardPlayHistory(result.State.TurnNumber, [], []);
        if (result.State.TurnNumber == before.TurnNumber)
            foreach (var ev in result.Events)
                if (ev.Kind is "UNIT_PLAYED_TO_BATTLEFIELD" or "EQUIPMENT_PLAYED_TO_BATTLEFIELD"
                        or "UNIT_TOKEN_CREATED" or "EQUIPMENT_TOKEN_CREATED"
                    && Equals(ev.Payload.GetValueOrDefault("destinationZone"), "BATTLEFIELD")
                    && ev.Payload.GetValueOrDefault("playerId") is string player && result.State.Seats.ContainsKey(player))
                    effects = AddUntilEndOfTurnEffect(effects, PlayedPermanentToBattlefieldPrefix + player);
        foreach (var (observer, index) in observers.DistinctBy(o => (o.Source, o.Context.SourceGeneration, o.Context.CardNo, o.Context.Origin))
            .Select((o, i) => (o, i))) {
            var captured = observer;
            if (before.CardObjects.TryGetValue(observer.Source, out var prior) && !IsObjectOnField(before.PlayerZones, observer.Source))
                captured = observer with { Context = observer.Context with { SourceGeneration = Math.Max(observer.Context.SourceGeneration, prior.ObjectGeneration + 1) } };
            var trigger = UnitEventTrigger(captured, result.State.Tick, index, "FIRST_CARD_PLAYED");
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        foreach (var (observer, index) in cardPlayObservers
            .DistinctBy(o => (o.Source, o.Context.SourceGeneration, o.Context.CardNo, o.Context.Timing, o.Context.Origin))
            .Select((o, i) => (o, i))) {
            var captured = observer;
            if (before.CardObjects.TryGetValue(observer.Source, out var prior) && !IsObjectOnField(before.PlayerZones, observer.Source))
                captured = observer with { Context = observer.Context with {
                    SourceGeneration = Math.Max(observer.Context.SourceGeneration, prior.ObjectGeneration + 1) } };
            var trigger = UnitEventTrigger(captured, result.State.Tick, observers.Count + index, "CARD_PLAY_COMPLETED");
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        var next = result.State with { CardPlays = nextHistory, TriggerQueue = queue, UntilEndOfTurnEffects = effects };
        return RecordBattlefieldPermanentPlays(before, result with { State = next, Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) });
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
                || !ValidShowcaseReceipt(state, e.Showcase, e.CardNo, e.Source.ObjectId)
                || e.Showcase is { } shown && shown.Tick > e.Tick
                || e.ReturnedEquipmentManaCost is < 0
                || e.PaidMana is < 0 || e.PaidMana is not null && (e.Types & 4) == 0
                || e.ReturnedEquipmentManaCost is not null && (!CardBehaviorRegistry.TryGetByCardNo(e.CardNo, out var returned) || !returned.MayReturnFriendlyEquipmentAdditionalCost)
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
        if (history.Finalized.Any(e => e.Showcase is { } receipt && receipt.PlayerId != e.PlayerId)) return false;
        foreach (var completed in history.Resolved)
        {
            if (completed.PaidMana is not null && !history.Finalized.Any(e => e.PlayId == completed.PlayId && e.PaidMana == completed.PaidMana)) return false;
            if (history.Finalized.FirstOrDefault(e => e.PlayId == completed.PlayId) is { } confirmed
                && (confirmed.CardNo != completed.CardNo || confirmed.Types != completed.Types
                    || confirmed.Source.ObjectId != completed.Source.ObjectId || confirmed.Tick > completed.Tick
                    || confirmed.Source.Generation > completed.Source.Generation
                    || confirmed.OriginHandPlayerId != completed.OriginHandPlayerId
                    || confirmed.OriginOutsideHands != completed.OriginOutsideHands
                    || confirmed.Showcase != completed.Showcase
                    || confirmed.PaidMana != completed.PaidMana
                    || confirmed.ReturnedEquipmentManaCost != completed.ReturnedEquipmentManaCost)) return false;
        }
        return true;
    }
}
