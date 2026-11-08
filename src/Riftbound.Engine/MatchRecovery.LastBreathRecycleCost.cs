using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateLastBreathRecycleCostHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        foreach (var item in state.StackItems.Where(i => CoreRuleEngine.HasLastBreathSourceRecycleCost(i)
            && i.TriggerCost is not null))
        {
            var receipt = item.TriggerCost!;
            if (receipt.Source is null) { errors.Add("last-breath recycle cost has no source"); continue; }
            var choiceId = "TRIGGER-COST:" + item.StackItemId;
            var recycled = events.Where(e => e.Tick >= item.LastBreathRuneCall!.CreatedAtTick
                && e.Event.Kind == "CARDS_RECYCLED"
                && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == item.ControllerId
                && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var source) && source == receipt.Source.ObjectId
                && TryReadObjectLong(e.Event.Payload, "sourceGeneration", out var generation) && generation == receipt.Source.Generation
                && TryReadObjectString(e.Event.Payload, "ownerPlayerId", out var owner)
                    && state.CardObjects.TryGetValue(receipt.Source.ObjectId, out var current) && owner == current.OwnerId
                && TryReadObjectString(e.Event.Payload, "stackItemId", out var stack) && stack == item.StackItemId
                && TryReadObjectString(e.Event.Payload, "deathId", out var death) && death == item.LastBreathRuneCall!.DeathId
                && TryReadObjectString(e.Event.Payload, "choiceId", out var choice) && choice == choiceId
                && TryReadObjectString(e.Event.Payload, "choiceWindow", out var window) && window == CoreRuleEngine.TriggerCostWindow
                && TryReadObjectString(e.Event.Payload, "sourceZone", out var origin) && origin == TriggerZones.Graveyard
                && TryReadObjectString(e.Event.Payload, "destinationZone", out var destination) && destination == TriggerZones.MainDeck
                && TryReadObjectString(e.Event.Payload, "reason", out var reason) && reason == CoreRuleEngine.TriggerCostWindow
                && TryReadObjectStringList(e.Event.Payload, "cardIds", out var ids)
                    && ids.Count == 1 && ids[0] == receipt.Source.ObjectId).Take(2).ToArray();
            var confirmed = recycled.Length == 1 ? events.Where(e => e.Tick == recycled[0].Tick
                && e.Sequence > recycled[0].Sequence && e.Event.Kind == "TRIGGER_CONFIRMED"
                && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == item.ControllerId
                && TryReadObjectString(e.Event.Payload, "stackItemId", out var stack) && stack == item.StackItemId).Take(2).ToArray() : [];
            if (recycled.Length != 1 || confirmed.Length != 1 || !commands.Any(command => command.Accepted
                && command.PlayerId == item.ControllerId && command.CommandType == CommandTypes.ChooseCards
                && command.CompletedTick == recycled[0].Tick && command.StartedEventSequence < recycled[0].Sequence
                && command.CompletedEventSequence >= confirmed[0].Sequence
                && (command.RawCommand ?? command.TypedCommand?.Value) is { } input
                && TryReadObjectString(input, "choiceId", out var id) && id?.Trim() == choiceId
                && TryReadObjectString(input, "choiceWindow", out var window) && window?.Trim() == CoreRuleEngine.TriggerCostWindow
                && TryReadObjectStringList(input, "chosenObjectIds", out var choices) && choices.Count == 1
                && choices[0].Trim() == receipt.Source.ObjectId))
                errors.Add("last-breath recycle receipt is not bound to accepted payment history");
        }
    }
}
