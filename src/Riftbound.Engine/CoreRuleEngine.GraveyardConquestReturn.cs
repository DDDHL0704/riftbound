using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 385.2 explicitly uses this graveyard-return wording as its example:
    // only sources already in the graveyard observe the actual conquest.
    private static IEnumerable<TriggerQueueItemState> CaptureGraveyardConquestReturns(
        MatchState state, string controller, string battlefield, long tick)
    {
        if (!state.PlayerZones.TryGetValue(controller, out var own)) yield break;
        foreach (var id in own.Graveyard.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || source.OwnerId != controller
                || IsTokenObject(source) || UnitConquestReturnSpec(source.CardNo) is not { } spec
                || !UnitConquestTriggerSpecRules.IsGraveyardConquestDiscardReturnSelfTrigger(spec)) continue;
            var repeats = ResolveUnitConquestEffectRepeatCount(state.PlayerZones, state.CardObjects,
                state.ObjectLocations, controller, battlefield, "BATTLEFIELD_CONQUERED");
            for (var occurrence = 0; occurrence < repeats; occurrence++)
            {
                var context = new UnitConquestReturnContext(new(id, source.ObjectGeneration), source.CardNo!,
                    controller, battlefield, tick, occurrence, source.CopyFaceTransitions?.Count ?? 0) {
                    OriginZone = TriggerZones.Graveyard };
                yield return new(UnitConquestReturnTriggerId(context), controller, id,
                    RuntimeTriggerEffectKind(spec), "BATTLEFIELD_CONQUERED") {
                    SourceCardNo = source.CardNo, EnqueuedAtTick = tick, UnitConquestReturn = context };
            }
        }
    }

    private static StackResolutionResult ResolveGraveyardConquestReturn(MatchState state, StackItemState item)
    {
        var context = item.UnitConquestReturn!;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var returned = cards.TryGetValue(context.Source.ObjectId, out var current)
            && current.ObjectGeneration == context.Source.Generation && current.OwnerId == context.ControllerId
            && TryReturnGraveyardCardToHand(zones, cards, context.ControllerId, context.Source.ObjectId);
        var events = new List<GameEvent>();
        if (returned)
            events.Add(new("CARD_RETURNED_TO_HAND", "征服技能将原废牌堆卡牌返回所属手牌",
                new Dictionary<string, object?> { ["playerId"] = context.ControllerId,
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = item.SourceObjectId,
                    ["sourceZone"] = "GRAVEYARD", ["destinationZone"] = "HAND",
                    ["ownerPlayerId"] = context.ControllerId, ["reason"] = item.EffectKind }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
