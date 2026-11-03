using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData("OGN·267/298", "ALLY", "none")]
    [InlineData("OGN·267/298", "ENEMY", "none")]
    [InlineData("OGN·309/298", "ALLY", "none")]
    [InlineData("OGN·309/298", "ENEMY", "none")]
    [InlineData("OGN·309*/298", "ALLY", "none")]
    [InlineData("OGN·309*/298", "ENEMY", "none")]
    [InlineData("OGN·267/298", "ALLY", "source-return")]
    [InlineData("OGN·267/298", "ALLY", "target-return")]
    public async Task BorrowedRoamUsesAnyUnitAndExpiresWithItsCapturedTarget(string donor, string target, string response)
    {
        var game = await WithTemplate(donor, "LEGEND", adjust: state =>
            AttackDamageDistributionTests.Spell(state, "ROAM-RETURN", "OGN·104/298", "P1"));
        var id = TemplateId(game.State, "游走");
        await RejectBorrowedResource(game.State, "HOST", id, ["GEAR"]);
        await RejectBorrowedResource(game.State, "HOST", id, []);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [target]));
        Assert.DoesNotContain("ROAM", game.State.CardObjects[target].UntilEndOfTurnEffects);
        await RejectBorrowedResource(game.State, "HOST", id, [target]);
        if (response != "none")
        {
            await game.Act("P1", new PlayCardCommand("ROAM-RETURN", "OGN·104/298",
                [response == "source-return" ? "HOST" : target]));
            await game.Top();
        }
        await game.Top();
        Assert.Equal(response != "target-return", game.State.CardObjects[target].UntilEndOfTurnEffects.Contains("ROAM"));
        Assert.DoesNotContain("ROAM", game.State.CardObjects["DONOR"].UntilEndOfTurnEffects);
        async ValueTask<ResolutionResult> Submit(string actor, GameCommand command)
        {
            await game.Act(actor, command);
            return new(true, null, game.State, [], ResolutionResult.BuildSnapshots(game.State), ResolutionResult.BuildPrompts(game.State));
        }
        await TurnSequenceTestDriver.Complete(await Submit("P1", new EndTurnCommand()), Submit);
        Assert.DoesNotContain("ROAM", game.State.CardObjects[target].UntilEndOfTurnEffects);
        await game.Replay();
    }
}
