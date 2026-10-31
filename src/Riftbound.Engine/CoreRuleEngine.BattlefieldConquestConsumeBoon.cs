using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestConsumeBoonContext(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] string ConquestSourceObjectId,
    [property: JsonRequired] long ConquestTick);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldConquestConsumeBoonPublicContext(
    [property: JsonRequired] ObjectBinding Source,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] long ConquestTick);

public sealed partial class CoreRuleEngine
{
    internal static TriggerSpec? BattlefieldConquestConsumeBoonDefinition(string? cardNo)
        => BattlefieldTriggerSpecRules.TryGetTrigger(cardNo,
            spec => BattlefieldTriggerSpecRules.IsBattlefieldConquerConsumeBoonDrawTrigger(spec)
                && spec.ConsumedBoonCount == 1, out var spec) ? spec : null;

    internal static BattlefieldConquestConsumeBoonPublicContext? PublicBattlefieldConquestConsumeBoon(
        BattlefieldConquestConsumeBoonContext? context)
        => context is null ? null : new(context.Source, context.CardNo, context.ControllerId, context.ConquestTick);

    internal static bool ValidBattlefieldConquestConsumeBoonContext(MatchState state,
        BattlefieldConquestConsumeBoonContext context, string effect, string source, string controller, string? cardNo)
        => context.Source is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(context.Source.ObjectId)
            && !string.IsNullOrWhiteSpace(context.ControllerId) && !string.IsNullOrWhiteSpace(context.ConquestSourceObjectId)
            && context.ConquestTick >= 0 && context.ConquestTick <= state.Tick
            && BattlefieldConquestConsumeBoonDefinition(context.CardNo) is { } spec && effect == spec.Kind
            && source == context.Source.ObjectId && controller == context.ControllerId && cardNo == context.CardNo
            && state.Seats.ContainsKey(controller) && state.CardObjects.TryGetValue(source, out var battlefield)
            && (battlefield.CardNo == context.CardNo || ReplacedBattlefieldCard(state, battlefield)?.CardNo == context.CardNo)
            && battlefield.ObjectGeneration >= context.Source.Generation
            && (state.BattlefieldResolutions.Any(r => r.Kind == "CONQUERED" && r.Tick == context.ConquestTick
                    && r.BattlefieldObjectId == source && r.PlayerId == controller
                    && r.SourceObjectId == context.ConquestSourceObjectId)
                || state.BattlefieldResolutions.Count == 12
                    && state.BattlefieldResolutions.All(r => r.Tick > context.ConquestTick));

    private static IReadOnlyList<TriggerQueueItemState> CaptureBattlefieldConquestConsumeBoon(
        MatchState state, string controller, string battlefield, string conquestSourceObjectId, long tick)
    {
        if (!TryGetBattlefieldCardObject(state.PlayerZones, state.CardObjects, battlefield, out var id, out var card)
            || BattlefieldConquestConsumeBoonDefinition(card.CardNo) is not { } spec) return [];
        var context = new BattlefieldConquestConsumeBoonContext(new(id, card.ObjectGeneration), card.CardNo!,
            controller, conquestSourceObjectId, tick);
        return [new($"CONQUEST-BOON-DRAW:{tick}:{id}", controller, id, spec.Kind, "BATTLEFIELD_CONQUERED") {
            SourceCardNo = card.CardNo, EnqueuedAtTick = tick, BattlefieldConquestConsumeBoon = context }];
    }

    private static string VisibleCapturedConquestReference(MatchState state, string objectId)
        => state.PlayerZones.Values.Any(z => z.Hand.Contains(objectId) || z.MainDeck.Contains(objectId) || z.RuneDeck.Contains(objectId))
            || state.CardObjects.TryGetValue(objectId, out var card) && card.IsFaceDown ? "HIDDEN" : objectId;

    private static StackResolutionResult ResolveBattlefieldConquestConsumeBoon(MatchState state, StackItemState item)
    {
        var context = item.BattlefieldConquestConsumeBoon!;
        if (!ValidBattlefieldConquestConsumeBoonContext(state, context, item.EffectKind, item.SourceObjectId,
                item.ControllerId, item.CardNo)
            || BattlefieldConquestConsumeBoonDefinition(context.CardNo) is not { } spec
            || item.TriggerCost?.ConsumedBoon is not { } paid || !ValidTriggerCostReceipt(state, item))
            throw new InvalidOperationException("Invalid confirmed battlefield conquest boon cost.");
        var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent> { new("BATTLEFIELD_TRIGGER_RESOLVED", "征服战场的增益费用技能结算抽牌",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["battlefieldId"] = context.Source.ObjectId,
                ["battlefieldObjectId"] = context.Source.ObjectId, ["battlefieldCardNo"] = context.CardNo,
                ["trigger"] = item.EffectKind, ["sourceObjectId"] = VisibleCapturedConquestReference(state, context.ConquestSourceObjectId),
                ["targetObjectId"] = VisibleCapturedConquestReference(state, paid.ObjectId), ["drawCount"] = spec.DrawCount!.Value }) };
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var draw = ApplyDrawToPlayer(state, zones, cards, state.PlayerScores, item.ControllerId,
            spec.DrawCount!.Value, state.RngCursor, events, deferNormalVictory: true);
        return new(zones, cards, draw.PlayerScores, NormalizeExperienceForSeats(state), state.RunePools,
            state.UntilEndOfTurnEffects, draw.WinnerPlayerId, events, [], null, [], null, [], draw.RngCursor);
    }
}
