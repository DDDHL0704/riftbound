using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveBorrowedReturnSkill(MatchState state, StackItemState item, BorrowedExhaustSpec spec)
        => ResolveUnitReturnInstructions(state, item,
            id => StackObjectRules.ReferencesCurrentObject(state, item, id)
                && IsBorrowedExhaustTarget(state, item.ControllerId, id, spec, item.SourceObjectId),
            spec.TargetScope == "OWNED_TEEMO_UNIT", spec.Kind == "RETURN_AND_CREATE_GOLD", item.BorrowedExhaust!.Donor.CardNo);

    private static StackResolutionResult ResolveUnitReturnInstructions(MatchState state, StackItemState item,
        Func<string, bool> legalTarget, bool includeChampionZone, bool createsGold, string tokenSourceCardNo)
    {
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var pools = state.RunePools;
        var events = new List<GameEvent>();
        foreach (var id in item.TargetObjectIds)
        {
            if (!legalTarget(id)) continue;
            var target = cards[id];
            if (TryReplaceEmpoweredTargetEvent(state, item, target, "RETURN_TO_HAND", out var replacement, out var replaced))
            {
                cards[id] = replacement;
                events.Add(replaced);
                continue;
            }
            var originZone = zones.Values.Any(zone => zone.ChampionZone.Contains(id)) ? "CHAMPION"
                : FindFieldObjectLocation(zones, id)?.Zone ?? string.Empty;
            var battlefieldReturn = !target.IsFaceDown && TryGetBattlefieldUnitReturnContext(zones, cards, id, out _);
            if (!TryReturnTargetToHand(zones, cards, id, out var owner, out var equipment,
                    includeChampionZone: includeChampionZone)) continue;
            GameEvent returned = target.IsFaceDown
                ? new("CARD_RETURNED_TO_HAND", "正面朝下的卡牌返回手牌", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id,
                    ["ownerPlayerId"] = owner, ["destinationZone"] = "HAND" })
                : BuildReturnedToHandEvent("单位返手技能", item, id, owner, equipment);
            var payload = new Dictionary<string, object?>(returned.Payload) {
                ["originZone"] = originZone, ["destinationZone"] = "HAND" };
            if (item.LegendFieldEffect is { } legend) {
                payload["abilityId"] = legend.AbilityId; payload["playerId"] = item.ControllerId;
            }
            events.Add(returned with { Payload = payload });
            if (battlefieldReturn && TryResolveBattlefieldUnitReturnedCallRuneTrigger(state, zones, cards, pools,
                owner, id, item.SourceObjectId, events, out var calledPools)) pools = calledPools;
        }
        // CN 359.3.e.1/5/6: creating the coin is independent of the return target.
        if (createsGold)
            CreateBaseEquipmentTokens(zones, cards,
                new(tokenSourceCardNo, "单位返手的后续指示物", 0, item.EffectKind, 0, 0,
                    CreatedBaseEquipmentTokenCount: 1, CreatedBaseEquipmentTokenName: "金币",
                    CreatedBaseEquipmentTokenTags: CardObjectTags.EquipmentCard, CreatedBaseEquipmentTokenIsExhausted: true), item, events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
