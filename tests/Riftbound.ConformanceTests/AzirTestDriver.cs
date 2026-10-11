using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class AzirTestDriver
{
    internal static async Task<ResolutionResult> Finish(ResolutionResult result, bool attach,
        Func<string, GameCommand, ValueTask<ResolutionResult>>? submit = null)
    {
        if (result.State.PendingCardChoice is not { ChoiceWindow: "AZIR_ARMAMENT_ATTACHMENT" } choice) return result;
        var before = result.State;
        var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, attach ? [choice.LegalObjectIds.Single()] : []);
        var final = submit is null ? await new CoreRuleEngine().ResolveAsync(before,
            new("azir-after-movements-" + before.Tick, choice.PlayerId, CommandTypes.ChooseCards), command, default)
            : await submit(choice.PlayerId, command);
        Assert.True(final.Accepted, final.ErrorMessage); Assert.Equal(before.Tick + 1, final.State.Tick);
        foreach (var card in before.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.UnitCard)))
            Assert.Equal(card.MovementCountThisTurn, final.State.CardObjects[card.ObjectId].MovementCountThisTurn);
        Assert.DoesNotContain(final.Events, e => e.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
        var recoveryErrors = OfficialInsightAndSpellLockTests.Errors(final.State);
        Assert.True(recoveryErrors.Count == 0, string.Join("; ", recoveryErrors));
        OfficialGraveyardRecastTests.Restore(final.State); ArmamentMightTests.PublicRecovery(final.State);
        return final with { Events = result.Events.Concat(final.Events).ToArray() };
    }
}
