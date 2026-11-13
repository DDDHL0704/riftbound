using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveCommandOpponentMovement(MatchState state, StackItemState item,
        ObjectBinding sourceBinding, string displayName, string abilityId)
    {
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "所选玩家技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator),
            ["abilityId"] = abilityId }) };
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (cards.TryGetValue(item.SourceObjectId, out var commandedSource)
            && commandedSource.ObjectGeneration == sourceBinding.Generation
            && TryGetPreciseFieldLocation(zones, locations, commandedSource.ObjectId, out var sourceLocation)
            && sourceLocation.Zone == "BATTLEFIELD"
            && sourceLocation.BattlefieldObjectId is { } battlefieldId
            && BattlefieldLocalRules.Battlefield(state, battlefieldId) is not null)
        {
            var opponent = item.TargetObjectIds.Single();
            var current = state with {
                PlayerZones = zones,
                CardObjects = cards,
                ObjectLocations = locations,
                RunePools = pools
            };
            var legal = cards.Values
                .Where(card => IsFaceUpNonStandbyUnit(card)
                    && FindFieldObjectLocation(zones, card.ObjectId) is { } location
                    && location.PlayerId == opponent
                    && IsUnitMovementDestination(current, card.ObjectId, "BATTLEFIELD:" + battlefieldId)
                    && MovementRestrictionRules.CanMove(card, opponent))
                .OrderBy(card => card.ObjectId, StringComparer.Ordinal)
                .ToArray();
            if (legal.Length > 0)
            {
                var frame = RuleChoices.Value ?? throw new InvalidOperationException("移动技能需要对手选择单位。");
                var selected = frame.Choose(opponent, $"{displayName}：选择你控制的一名单位，移动到来源所在战场。",
                    legal.Select(card => new RuleChoiceOption(
                        "MOVE:" + card.ObjectId,
                        OfficialCardSourceIdentityGroups.DisplayName(card.CardNo ?? "") + "（" + card.ObjectId + "）",
                        [card.ObjectId])).ToArray(),
                    kind: "MOVEMENT_SELECTION", preview: current);
                TryMoveFieldUnitByEffect(current, zones, cards, locations, pools, selected[5..],
                    new(item.ControllerId, "BATTLEFIELD", battlefieldId), opponent, item, events);
            }
            else
            {
                events.Add(new("ABILITY_NO_EFFECT", "结算时对手没有可移动的单位",
                    new Dictionary<string, object?> {
                        ["sourceObjectId"] = item.SourceObjectId, ["opponentId"] = opponent,
                        ["abilityId"] = abilityId, ["stackItemId"] = item.StackItemId,
                        ["reason"] = "NO_LEGAL_OPPONENT_UNIT"
                    }));
            }
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations, RunePools = pools, Events = events };
    }

    private static StackResolutionResult ResolveTransferSourceAndRecall(MatchState state, StackItemState item,
        ObjectBinding sourceBinding, string abilityId)
    {
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "所选玩家技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator),
            ["abilityId"] = abilityId }) };
        if (cards.TryGetValue(item.SourceObjectId, out var transferred)
            && transferred.ObjectGeneration == sourceBinding.Generation && IsObjectOnField(zones, transferred.ObjectId)) {
            var controller = item.TargetObjectIds.Single();
            var previous = EffectiveFieldControllerId(zones, transferred.ObjectId, transferred);
            // A later indefinite transfer cannot expose any of the older grants again.
            cards[transferred.ObjectId] = transferred with { ControllerId = controller, AttachedToObjectId = null,
                AttachedRuleText = null, UnitControl = null, EquipmentControl = null };
            RecallAttachedGroupToBase(zones, cards, transferred.ObjectId, controller);
            events.Add(new("CONTROL_CHANGED", "所选玩家获得来源物体控制权并将其召回", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = item.SourceObjectId,
                ["previousControllerId"] = previous, ["controllerId"] = controller, ["destinationZone"] = "BASE" }));
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
    }
}
