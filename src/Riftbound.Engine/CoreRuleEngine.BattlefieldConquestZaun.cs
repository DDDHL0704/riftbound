using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestDiscardDrawContext(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] string ConquestSourceObjectId,
    [property: JsonRequired] long ConquestTick);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestDiscardDrawPublicContext(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] long ConquestTick);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestDiscardDrawContinuation(
    [property: JsonRequired] BattlefieldConquestDiscardDrawContext Context,
    [property: JsonRequired] string StackItemId);

public sealed partial class CoreRuleEngine
{
    private static IReadOnlyList<TriggerQueueItemState> CaptureBattlefieldConquestDiscardDraw(
        MatchState state, string controller, string battlefield, string conquestSourceObjectId, long tick)
    {
        if (!TryGetBattlefieldCardObject(state.PlayerZones, state.CardObjects, battlefield, out var id, out var card)
            || !BattlefieldTriggerSpecRules.TryGetTrigger(card.CardNo,
                BattlefieldTriggerSpecRules.IsBattlefieldConquerDiscardDrawTrigger, out var spec)) return [];
        var context = new BattlefieldConquestDiscardDrawContext(new(id, card.ObjectGeneration), card.CardNo!,
            controller, conquestSourceObjectId, tick);
        return [new($"CONQUEST-DISCARD-DRAW:{tick}:{id}", controller, id, spec.Kind, "BATTLEFIELD_CONQUERED")
        { SourceCardNo = card.CardNo, EnqueuedAtTick = tick, BattlefieldConquestDiscardDraw = context }];
    }

    internal static BattlefieldConquestDiscardDrawPublicContext? PublicBattlefieldConquestDiscardDraw(
        BattlefieldConquestDiscardDrawContext? context)
        => context is null ? null : new(context.Source, context.CardNo, context.ControllerId, context.ConquestTick);

    internal static bool ValidBattlefieldConquestDiscardDrawFrame(BattlefieldConquestDiscardDrawContext context)
        => context.Source is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(context.Source.ObjectId)
            && !string.IsNullOrWhiteSpace(context.CardNo) && !string.IsNullOrWhiteSpace(context.ControllerId)
            && !string.IsNullOrWhiteSpace(context.ConquestSourceObjectId) && context.ConquestTick >= 0
            && BattlefieldTriggerSpecRules.TryGetTrigger(context.CardNo,
                BattlefieldTriggerSpecRules.IsBattlefieldConquerDiscardDrawTrigger, out _);

    internal static bool ValidBattlefieldConquestDiscardDrawContext(MatchState state,
        BattlefieldConquestDiscardDrawContext context, string effect, string source, string controller, string? cardNo)
        => ValidBattlefieldConquestDiscardDrawFrame(context)
            && effect == TriggerKinds.BattlefieldConquerDiscardDraw && source == context.Source.ObjectId
            && controller == context.ControllerId && cardNo == context.CardNo && state.Seats.ContainsKey(controller)
            && context.ConquestTick <= state.Tick && state.CardObjects.TryGetValue(source, out var battlefield)
            && (battlefield.CardNo == context.CardNo || ReplacedBattlefieldCard(state, battlefield)?.CardNo == context.CardNo)
            && battlefield.ObjectGeneration >= context.Source.Generation
            && (state.BattlefieldResolutions.Any(r => r.Kind == "CONQUERED" && r.Tick == context.ConquestTick
                    && r.BattlefieldObjectId == source && r.PlayerId == controller
                    && r.SourceObjectId == context.ConquestSourceObjectId)
                || state.BattlefieldResolutions.Count == 12
                    && state.BattlefieldResolutions.All(r => r.Tick > context.ConquestTick));

    private static string VisibleZaunConquestSource(MatchState state, string objectId)
        => state.PlayerZones.Values.Any(z => z.Hand.Contains(objectId) || z.MainDeck.Contains(objectId) || z.RuneDeck.Contains(objectId))
            || state.CardObjects.TryGetValue(objectId, out var source) && source.IsFaceDown ? "HIDDEN" : objectId;

    private static StackResolutionResult ResolveBattlefieldConquestDiscardDraw(MatchState state, StackItemState item)
    {
        var context = item.BattlefieldConquestDiscardDraw!;
        if (!ValidBattlefieldConquestDiscardDrawContext(state, context, item.EffectKind, item.SourceObjectId,
                item.ControllerId, item.CardNo)
            || !BattlefieldTriggerSpecRules.TryGetTrigger(context.CardNo,
                BattlefieldTriggerSpecRules.IsBattlefieldConquerDiscardDrawTrigger, out var spec))
            throw new InvalidOperationException("Invalid captured battlefield conquest discard/draw.");

        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var legal = zones[item.ControllerId].Hand
            .Where(id => IsCardObjectControlledByPlayerOrLegacyOwned(cards, item.ControllerId, id)).ToArray();
        var discardCount = Math.Min(spec.DiscardCount.GetValueOrDefault(), legal.Length);
        var drawCount = spec.DrawCount.GetValueOrDefault();
        if (discardCount > 0 && legal.Length > discardCount)
        {
            var choice = new PendingHandChoiceState($"CONQUEST-HAND:{item.StackItemId}",
                TriggerKinds.BattlefieldConquerDiscardDraw, item.ControllerId, discardCount, discardCount, legal,
                TriggerKinds.BattlefieldConquerDiscardDraw, context.Source.ObjectId, item.EffectKind, drawCount)
                { BattlefieldConquestDiscardDraw = new(context, item.StackItemId) };
            var requested = new GameEvent("HAND_CHOICE_REQUESTED", "征服战场：选择要弃置的手牌，然后抽牌",
                new Dictionary<string, object?> { ["choiceId"] = choice.ChoiceId, ["choiceWindow"] = choice.ChoiceWindow,
                    ["playerId"] = item.ControllerId, ["sourceObjectId"] = context.Source.ObjectId,
                    ["effectKind"] = item.EffectKind, ["requiredCount"] = discardCount,
                    ["maxCount"] = discardCount, ["legalCount"] = legal.Length, ["drawCount"] = drawCount });
            return NoopStackResolutionResult(state) with { PendingHandChoice = choice, Events = [requested] };
        }

        var events = new List<GameEvent> { new("BATTLEFIELD_TRIGGER_RESOLVED", "征服战场：弃置手牌并抽牌",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["battlefieldId"] = context.Source.ObjectId,
                ["battlefieldObjectId"] = context.Source.ObjectId, ["battlefieldCardNo"] = context.CardNo,
                ["trigger"] = item.EffectKind, ["sourceObjectId"] = VisibleZaunConquestSource(state, context.ConquestSourceObjectId),
                ["drawCount"] = drawCount }) };
        var discarded = new List<string>();
        foreach (var id in legal.Take(discardCount))
        {
            if (!TryDiscardCardFromHand(zones, cards, item.ControllerId, id))
                throw new InvalidOperationException("A captured legal hand card could not be discarded.");
            events.Add(new GameEvent("CARD_DISCARDED", "征服战场：弃置手牌",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                    ["sourceObjectId"] = context.Source.ObjectId, ["targetObjectId"] = id,
                    ["reason"] = item.EffectKind, ["destinationZone"] = spec.DiscardDestinationZone }));
            discarded.Add(id);
        }
        if (discarded.Count > 0)
            ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, item.ControllerId, "CARD_DISCARDED",
                context.Source.ObjectId, discarded, events);
        var until = MarkPlayerDiscardedHandCardsThisTurn(state.UntilEndOfTurnEffects, item.ControllerId, discarded, zones);
        var draw = ApplyDrawToPlayer(state with { PlayerZones = zones, CardObjects = cards }, zones, cards,
            state.PlayerScores, item.ControllerId, drawCount, state.RngCursor, events, deferNormalVictory: true);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            PlayerScores = draw.PlayerScores, WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor,
            UntilEndOfTurnEffects = until, Events = events };
    }

    internal static bool ValidBattlefieldConquestDiscardHandChoice(MatchState state, PendingHandChoiceState choice)
    {
        var continuation = choice.BattlefieldConquestDiscardDraw;
        if (continuation is null || string.IsNullOrWhiteSpace(continuation.StackItemId)) return false;
        var context = continuation.Context;
        if (context is null || !ValidBattlefieldConquestDiscardDrawContext(state, context,
                choice.EffectKind, choice.SourceObjectId, choice.PlayerId, context.CardNo)
            || continuation.StackItemId != $"ordered-CONQUEST-DISCARD-DRAW:{context.ConquestTick}:{context.Source.ObjectId}"
            || !BattlefieldTriggerSpecRules.TryGetTrigger(context.CardNo,
                BattlefieldTriggerSpecRules.IsBattlefieldConquerDiscardDrawTrigger, out var spec)
            || !state.PlayerZones.TryGetValue(choice.PlayerId, out var zones)) return false;
        var legal = zones.Hand.Where(id => IsCardObjectControlledByPlayerOrLegacyOwned(state.CardObjects, choice.PlayerId, id)).ToArray();
        var count = Math.Min(spec.DiscardCount.GetValueOrDefault(), legal.Length);
        return count > 0 && legal.Length > count && choice.ChoiceId == $"CONQUEST-HAND:{continuation.StackItemId}"
            && choice.ChoiceWindow == TriggerKinds.BattlefieldConquerDiscardDraw && choice.Reason == choice.ChoiceWindow
            && choice.RequiredCount == count && choice.MaxCount == count && choice.DrawCount == spec.DrawCount.GetValueOrDefault()
            && choice.LegalObjectIds.SequenceEqual(legal);
    }
}
