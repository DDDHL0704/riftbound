using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateConquestDiscardReceiptHistory(StackItemState item,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var receipt = item.TriggerCost!;
        var choiceId = "TRIGGER-COST:" + item.StackItemId;
        var discarded = events.Where(e => e.Tick >= item.UnitConquestReturn!.ConquestTick
            && e.Event.Kind == "CARD_DISCARDED"
            && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == item.ControllerId
            && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var source) && source == receipt.Source.ObjectId
            && TryReadObjectLong(e.Event.Payload, "sourceGeneration", out var sourceGen) && sourceGen == receipt.Source.Generation
            && TryReadObjectString(e.Event.Payload, "targetObjectId", out var target) && target == receipt.Discarded?.ObjectId
            && TryReadObjectLong(e.Event.Payload, "targetGeneration", out var targetGen) && targetGen == receipt.Discarded?.Generation
            && TryReadObjectString(e.Event.Payload, "stackItemId", out var stack) && stack == item.StackItemId
            && TryReadObjectString(e.Event.Payload, "choiceId", out var choice) && choice == choiceId
            && TryReadObjectString(e.Event.Payload, "choiceWindow", out var window) && window == CoreRuleEngine.TriggerCostWindow
            && TryReadObjectString(e.Event.Payload, "destinationZone", out var zone) && zone == "GRAVEYARD"
            && TryReadObjectString(e.Event.Payload, "reason", out var reason) && reason == CoreRuleEngine.TriggerCostWindow)
            .Take(2).ToArray();
        var confirmed = discarded.Length == 1 ? events.Where(e => e.Tick == discarded[0].Tick
            && e.Sequence > discarded[0].Sequence && e.Event.Kind == "TRIGGER_CONFIRMED"
            && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == item.ControllerId
            && TryReadObjectString(e.Event.Payload, "stackItemId", out var stack) && stack == item.StackItemId).Take(2).ToArray() : [];
        if (discarded.Length != 1 || confirmed.Length != 1 || !commands.Any(command => command.Accepted
            && command.PlayerId == item.ControllerId && command.CommandType == CommandTypes.ChooseCards
            && command.CompletedTick == discarded[0].Tick && command.StartedEventSequence < discarded[0].Sequence
            && command.CompletedEventSequence >= confirmed[0].Sequence
            && (command.RawCommand ?? command.TypedCommand?.Value) is { } input
            && TryReadObjectString(input, "choiceId", out var id) && id?.Trim() == choiceId
            && TryReadObjectString(input, "choiceWindow", out var window) && window?.Trim() == CoreRuleEngine.TriggerCostWindow
            && TryReadObjectStringList(input, "chosenObjectIds", out var choices) && choices.Count == 1
            && choices[0].Trim() == receipt.Discarded?.ObjectId))
            errors.Add("captured conquest return discard receipt is not bound to accepted payment history");
    }
}
