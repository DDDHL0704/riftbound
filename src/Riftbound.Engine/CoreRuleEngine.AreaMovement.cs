using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static void MoveWeakerEnemiesAtSelectedBattlefieldToBase(
        MatchState state,
        Dictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        Dictionary<string, ObjectLocationState> locations,
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
        foreach (var group in affected.GroupBy(id => FindFieldObjectLocation(playerZones, id)!.Value.PlayerId))
            MoveFieldUnitsToChosenLocation(current, playerZones, cardObjects, locations,
                stackItem, group.ToArray(), "BASE:" + group.Key, events);
    }
}
