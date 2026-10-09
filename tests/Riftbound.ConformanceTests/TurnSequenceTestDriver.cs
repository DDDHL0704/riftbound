using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;
internal static class TurnSequenceTestDriver
{
    internal static async Task<ResolutionResult> Complete(ResolutionResult current,
        Func<string, GameCommand, ValueTask<ResolutionResult>>? submit = null)
    {
        var events = current.Events.ToList();
        for (var i = 0; i < 60 && current.State.TurnStartStep is not null
            && current.State.Status == MatchStatuses.InProgress; i++)
        {
            var state = current.State;
            string player; GameCommand command;
            if (state.PendingCardChoice is { } choice) {
                player = choice.PlayerId; command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                    choice.LegalObjectIds.Take(choice.RequiredCount).ToArray());
            } else if (state.PendingPayment is { } payment) {
                player = payment.PlayerId; command = new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]);
            } else if (state.StackItems.Count > 0) {
                player = state.PriorityPlayerId!; command = new PassPriorityCommand();
            } else {
                var prompt = current.Prompts.First(e => e.Value.Actions.Contains("ORDER_TRIGGERS"));
                player = prompt.Key;
                var candidate = prompt.Value.Candidates!.Single(c => c.Action == "ORDER_TRIGGERS");
                var ids = JsonSerializer.SerializeToElement(candidate.Metadata).GetProperty("orderedTriggerIds")
                    .EnumerateArray().Select(v => v.GetString()!).ToArray();
                command = new OrderTriggersCommand(OrderedTriggerIds: ids);
            }
            current = submit is null ? await new CoreRuleEngine().ResolveAsync(state,
                new(Guid.NewGuid().ToString(), player, command.CmdType), command, default) : await submit(player, command);
            Assert.True(current.Accepted, current.ErrorMessage); events.AddRange(current.Events);
        }
        Assert.True(current.State.TurnStartStep is null || current.State.Status == MatchStatuses.Finished);
        return current with { Events = events };
    }
}
