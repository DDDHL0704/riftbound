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
            && (e.Event.Kind == "CARD_DISCARDED"
                    && TryReadObjectString(e.Event.Payload, "destinationZone", out var zone) && zone == "GRAVEYARD"
                || e.Event.Kind == "COST_PAID"
                    && TryReadObjectBool(e.Event.Payload, "discardReplaced", out var replaced) && replaced)
            && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == item.ControllerId
            && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var source) && source == receipt.Source.ObjectId
            && TryReadObjectLong(e.Event.Payload, "sourceGeneration", out var sourceGen) && sourceGen == receipt.Source.Generation
            && TryReadObjectString(e.Event.Payload, "targetObjectId", out var target) && target == receipt.Discarded?.ObjectId
            && TryReadObjectLong(e.Event.Payload, "targetGeneration", out var targetGen) && targetGen == receipt.Discarded?.Generation
            && TryReadObjectString(e.Event.Payload, "stackItemId", out var stack) && stack == item.StackItemId
            && TryReadObjectString(e.Event.Payload, "choiceId", out var choice) && choice == choiceId
            && TryReadObjectString(e.Event.Payload, "choiceWindow", out var window) && window == CoreRuleEngine.TriggerCostWindow
            && TryReadObjectString(e.Event.Payload, "reason", out var reason) && reason == CoreRuleEngine.TriggerCostWindow)
            .Take(2).ToArray();
        var confirmed = discarded.Length == 1 ? events.Where(e => e.Tick == discarded[0].Tick
            && e.Sequence > discarded[0].Sequence && e.Event.Kind == "TRIGGER_CONFIRMED"
            && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == item.ControllerId
            && TryReadObjectString(e.Event.Payload, "stackItemId", out var stack) && stack == item.StackItemId).Take(2).ToArray() : [];
        if (discarded.Length != 1 || confirmed.Length != 1 || !commands.Any(command => command.Accepted
            && command.PlayerId == item.ControllerId && command.CommandType == CommandTypes.ChooseCards
            && command.StartedEventSequence < discarded[0].Sequence
            && (command.RawCommand ?? command.TypedCommand?.Value) is { } input
            && TryReadObjectString(input, "choiceId", out var id) && id?.Trim() == choiceId
            && TryReadObjectString(input, "choiceWindow", out var window) && window?.Trim() == CoreRuleEngine.TriggerCostWindow
            && TryReadObjectStringList(input, "chosenObjectIds", out var choices) && choices.Count == 1
            && choices[0].Trim() == receipt.Discarded?.ObjectId
            && (discarded[0].Event.Kind == "CARD_DISCARDED"
                ? command.CompletedTick == discarded[0].Tick && command.CompletedEventSequence >= confirmed[0].Sequence
                : ValidConquestDiscardReplacementHistory(item.ControllerId, choices[0].Trim(), command,
                    discarded[0], confirmed[0], commands, events))))
            errors.Add("captured conquest return discard receipt is not bound to accepted payment history");
    }

    // The card was selected by CHOOSE_CARDS and the suspended cost completed
    // under the accepted PAY_COST answer. Keep both journal bindings.
    private static bool ValidConquestDiscardReplacementHistory(string player, string card, RecoveredCommand selection,
        RecoveredEvent receipt, RecoveredEvent confirmed, IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events)
    {
        var prompt = events.FirstOrDefault(e => e.Sequence == selection.CompletedEventSequence
            && e.Event.Kind == "RULE_CHOICE_REQUESTED"
            && TryReadObjectString(e.Event.Payload, "playerId", out var payer) && payer == player);
        return prompt is not null && TryReadObjectString(prompt.Event.Payload, "choiceId", out var choiceId)
            && commands.Any(command => command.Accepted && command.PlayerId == player
                && command.CommandType == CommandTypes.PayCost && command.CompletedTick == receipt.Tick
                && command.StartedEventSequence >= prompt.Sequence && command.StartedEventSequence < receipt.Sequence
                && command.CompletedEventSequence >= confirmed.Sequence
                && (command.RawCommand ?? command.TypedCommand?.Value) is { } input
                && TryReadObjectString(input, "paymentId", out var id) && id == choiceId
                && TryReadObjectString(input, "paymentWindow", out var window) && window == CoreRuleEngine.RuleChoiceWindow
                && events.Any(e => e.Sequence > command.StartedEventSequence && e.Sequence < receipt.Sequence
                    && e.Event.Kind == "CARD_PLAYED"
                    && TryReadObjectString(e.Event.Payload, "mode", out var mode) && mode == CoreRuleEngine.DiscardReplacementPlayMode
                    && TryReadObjectString(e.Event.Payload, "playerId", out var controller) && controller == player
                    && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var source) && source == card));
    }
}
