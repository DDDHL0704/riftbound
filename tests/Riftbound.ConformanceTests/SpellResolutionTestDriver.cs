using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class SpellResolutionTestDriver
{
    public static async Task<ResolutionResult> Top(ResolutionResult before)
    {
        var after = await OfficialInsightAndSpellLockTests.ResolveTop(before.State);
        return after with { Events = before.Events.Concat(after.Events).ToArray() };
    }

    public static async Task<ResolutionResult> Finish(ResolutionResult result, bool accept = true)
    {
        for (var step = 0; step < 40; step++)
        {
            var state = result.State;
            GameCommand? command = null;
            string player;
            if (state.PendingCardChoice is { } choice)
            {
                Assert.Contains(choice.ChoiceWindow, new[] { "SPELL_TRIGGER_CONFIRMATION", "INSIGHT" });
                command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                    choice.ChoiceWindow == "SPELL_TRIGGER_CONFIRMATION" && accept ? choice.LegalObjectIds.Take(1).ToArray() : []);
                player = choice.PlayerId;
            }
            else if (state.TriggerQueue.Count > 0)
            {
                command = new OrderTriggersCommand(OrderedTriggerIds: state.TriggerQueue.Select(t => t.TriggerId).ToArray());
                player = state.TriggerQueue[0].ControllerId;
            }
            else if (state.StackItems.Count > 0) { result = await Top(result); continue; }
            else return result;
            var after = await OfficialInsightAndSpellLockTests.Act(state, player, command);
            result = after with { Events = result.Events.Concat(after.Events).ToArray() };
        }
        throw new InvalidOperationException("Spell/trigger test did not finish within 40 actions.");
    }
}
