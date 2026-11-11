using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CapturedHandExile(string OwnerId, ObjectBinding Card, string CardNo,
    long FaceRevision, long BanishedTick);

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextOpponentHoldEffect { get; init; }
}

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CapturedHandExile? HandExile { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string BanishChosenOpponentHandEffect = "BANISH_CHOSEN_OPPONENT_HAND_UNTIL_HOLD";
    internal const string ReturnCapturedHandExileEffect = "RETURN_CAPTURED_HAND_EXILE";

    private static string? RevealedHandOwner(MatchState state, StackItemState item)
    {
        if (item.UnitEvent is { } context && UnitEventRequiresOpponent(UnitEventSpecForContext(context)))
            return context.ChosenOpponentId;
        var opponents = state.Seats.Keys.Where(id => id != item.ControllerId).Take(2).ToArray();
        return opponents.Length == 1 ? opponents[0] : null;
    }

    internal static bool IsUnitEventHandChoice(StackItemState item)
        => item.UnitEvent is { DelayedReceipt: null } context
            && UnitEventSpecForContext(context)?.Effect == BanishChosenOpponentHandEffect;

    private static bool ValidCapturedHandExile(MatchState state, UnitEventContext context, UnitEventAbilitySpec spec)
        => spec.NextOpponentHoldEffect != ReturnCapturedHandExileEffect ? context.HandExile is null
            : context.HandExile is not { } exile ? context.DelayedReceipt is null
            : exile is { OwnerId.Length: > 0, Card: { ObjectId.Length: > 0, Generation: > 0 },
                    CardNo.Length: > 0, FaceRevision: >= 0 }
                && exile.OwnerId == context.ChosenOpponentId && state.Seats.ContainsKey(exile.OwnerId)
                && exile.BanishedTick >= context.OpponentConfirmedAtTick
                && exile.BanishedTick >= context.CapturedTick && exile.BanishedTick <= state.Tick
                && PredictBindingMatches(state, exile.Card, exile.CardNo, exile.FaceRevision, exile.BanishedTick, null);

    private static ScheduledUnitEvent? BanishRevealedHandCard(MatchState state, StackItemState item,
        string owner, string id, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        var before = cards[id];
        zones[owner] = zones[owner] with { Hand = RemoveFromZone(zones[owner].Hand, id),
            Banished = zones[owner].Banished.Append(id).ToArray() };
        ResetCardOutsidePlay(zones, cards, id, before, owner);
        events.Add(CaptureOwnedBanishObservers(new("CARD_BANISHED", "放逐对手展示的卡牌", new Dictionary<string, object?> {
            ["playerId"] = owner, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id,
            ["fromZone"] = "HAND", ["destinationZone"] = "BANISHED" }), zones, cards, owner, [id]));
        if (!IsUnitEventHandChoice(item) || !cards.TryGetValue(id, out var banished)
            || TokenObjectRules.IsToken(before)) return null;
        // ResetCardOutsidePlay performed the real nonfield crossing already.
        // The earned delay binds that incarnation, never the old hand object.
        var context = item.UnitEvent! with { HandExile = new(owner,
            new(id, banished.ObjectGeneration), banished.CardNo!, banished.CopyFaceTransitions?.Count ?? 0,
            RuleChoices.Value?.CompletionTick ?? state.Tick + 1) };
        var schedule = CaptureScheduledUnitEvent(state, item, context);
        events.Add(ScheduledUnitEventCreated(schedule));
        return schedule;
    }

    private static StackResolutionResult ReturnCapturedHandExile(MatchState state, StackItemState item)
    {
        if (item.UnitEvent is not { DelayedReceipt: not null, HandExile: { } exile }
            || !state.PlayerZones.TryGetValue(exile.OwnerId, out var own)
            || !own.Banished.Contains(exile.Card.ObjectId)
            || !state.CardObjects.TryGetValue(exile.Card.ObjectId, out var card)
            || card.ObjectGeneration != exile.Card.Generation) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        zones[exile.OwnerId] = own with { Banished = RemoveFromZone(own.Banished, card.ObjectId),
            Hand = own.Hand.Append(card.ObjectId).ToArray() };
        ResetCardOutsidePlay(zones, cards, card.ObjectId, card, exile.OwnerId);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
            Events = [new("CARD_RETURNED_TO_HAND", "所选对手据守后，关联放逐的卡牌返回其手牌",
                new Dictionary<string, object?> { ["playerId"] = exile.OwnerId,
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = card.ObjectId,
                    ["fromZone"] = "BANISHED", ["destinationZone"] = "HAND" })] };
    }

    private static bool ScheduledUnitEventLinkIsCurrent(MatchState state, ScheduledUnitEvent schedule)
        => schedule.Context.HandExile is not { } exile
            || state.CardObjects.TryGetValue(exile.Card.ObjectId, out var card)
                && card.ObjectGeneration == exile.Card.Generation
                && state.PlayerZones.TryGetValue(exile.OwnerId, out var zones)
                && zones.Banished.Contains(exile.Card.ObjectId);

    private static ResolutionResult RetireDepartedScheduledUnitEvents(ResolutionResult result)
    {
        if (!result.Accepted || result.State.ScheduledUnitEvents is not { } schedules) return result;
        var retired = schedules.Where(schedule => !ScheduledUnitEventLinkIsCurrent(result.State, schedule)).ToArray();
        if (retired.Length == 0) return result;
        var remaining = schedules.Except(retired).ToArray();
        var next = result.State with { ScheduledUnitEvents = remaining.Length == 0 ? null : remaining };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next),
            Events = result.Events.Concat(retired.Select(schedule => new GameEvent("DELAYED_UNIT_EVENT_RETIRED",
                "关联卡牌已经离开原区域，延迟技能结束", new Dictionary<string, object?> {
                    ["scheduleId"] = schedule.Receipt.Id }))).ToArray() };
    }

}
