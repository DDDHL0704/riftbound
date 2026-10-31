using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Complete the ready instruction for all affected units before a following
    // modifier instruction. Readied observers capture each unit's earlier might.
    private static void ReadyFriendlyFieldUnits(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        Dictionary<string, CardObjectState> cardObjects,
        CardBehaviorDefinition behavior,
        StackItemState stackItem,
        List<GameEvent> events)
    {
        foreach (var targetObjectId in GetControlledPublicFieldUnitObjectIds(playerZones, cardObjects, stackItem.ControllerId))
        {
            var targetState = cardObjects.TryGetValue(targetObjectId, out var existingTarget)
                ? existingTarget : new CardObjectState(targetObjectId);
            cardObjects[targetObjectId] = ApplyReadyState(targetState, behavior, stackItem, targetObjectId,
                out var readyEvent, playerZones, cardObjects);
            if (readyEvent is not null) events.Add(readyEvent);
        }
    }
}
