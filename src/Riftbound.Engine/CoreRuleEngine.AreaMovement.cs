using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static void MoveWeakerEnemiesAtSelectedBattlefieldToBase(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        // Only the chosen unit and battlefield are targets. CN 355.5.a:
        // units affected by the condition neither pay Ward nor get retargeted.
        if (stackItem.TargetObjectIds.Count != 2
            || !cardObjects.TryGetValue(stackItem.TargetObjectIds[0], out var reference)
            || BattlefieldLocalRules.Battlefield(state, stackItem.TargetObjectIds[1]) is not { } battlefield)
            return;

        var current = state with { PlayerZones = playerZones, CardObjects = cardObjects };
        var threshold = ResolveCurrentFieldUnitPower(current, reference);
        // Evaluate the entire group before relocating any aura source (CN 143/446).
        var affected = cardObjects.Values
            .Where(card => FieldObjectTypeRules.IsVisibleUnit(card)
                && IsEnemyBattlefieldUnitObject(current, stackItem.ControllerId, card.ObjectId)
                && BattlefieldLocalRules.AtUnit(current, card.ObjectId)?.ObjectId == battlefield.ObjectId
                && ResolveCurrentFieldUnitPower(current, card) < threshold)
            .Select(card => card.ObjectId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        foreach (var id in affected)
            MoveFieldUnitToControllerBase(current, playerZones, cardObjects, stackItem, id, events);
    }

    private static void MoveFieldUnitToControllerBase(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        IReadOnlyDictionary<string, CardObjectState> cardObjects,
        StackItemState stackItem,
        string unitId,
        List<GameEvent> events)
    {
        if (BattlefieldLocalRules.PreventsMoveToBase(state, unitId)
            || !TryMoveTargetToOwnerBase(playerZones, cardObjects, stackItem.ControllerId, unitId, out var controllerId))
            return;

        events.Add(new GameEvent("UNIT_MOVED_TO_BASE", "单位移动到其控制者的基地",
            new Dictionary<string, object?> {
                ["sourceObjectId"] = stackItem.SourceObjectId,
                ["targetObjectId"] = unitId,
                ["unitObjectId"] = unitId,
                ["playerId"] = controllerId,
                ["ownerPlayerId"] = controllerId,
                ["originZone"] = "BATTLEFIELD",
                ["originBattlefieldObjectId"] = state.ObjectLocations.GetValueOrDefault(unitId)?.BattlefieldObjectId,
                ["destinationZone"] = "BASE"
            }));
        events.AddRange(MoveAttachedEquipmentWithHost(playerZones,
            AttachedEquipmentObjectIds(cardObjects, unitId), controllerId, unitId, "BASE"));
    }
}
