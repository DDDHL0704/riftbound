using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private static MatchState ExplorerReactionPosition(string donor, bool equipment = false)
    {
        var state = BorrowedBombPosition(donor);
        state = HandDiscardObserverTests.Put(state, "ENEMY-GEAR", "SFD·022/221", "P2", "BASE");
        state = AttackDamageDistributionTests.Spell(state, "EXPLORER-TARGET-1", equipment ? "UNL-070/219" : "OGN·004/298", "P1");
        state = AttackDamageDistributionTests.Spell(state, "EXPLORER-TARGET-2", "OGN·004/298", "P1");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 20, new Dictionary<string, int> { ["red"] = 20, ["blue"] = 20, ["orange"] = 20,
                ["yellow"] = 20, ["purple"] = 20, ["green"] = 20 }) } };
    }

    private static async Task CloseExplorerDuel(Driver game)
    {
        for (var i = 0; game.State.TimingState == TimingStates.SpellDuelOpen; i++)
        {
            Assert.True(i < 3);
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        }
    }

    private static async Task<(Driver Game, MatchState OneTarget)> ExplorerSecondTarget(string donor, bool equipment = false)
    {
        var game = new Driver(ExplorerReactionPosition(donor, equipment));
        await game.Act("P1", new PlayCardCommand("EXPLORER-TARGET-1", equipment ? "UNL-070/219" : "OGN·004/298",
            [equipment ? "ENEMY-GEAR" : "ENEMY"]));
        await game.Top();
        await CloseExplorerDuel(game);
        var oneTarget = game.State;
        Assert.Contains("EZREAL_ENEMY_TARGETS_THIS_TURN:P1:1", oneTarget.UntilEndOfTurnEffects);
        await game.Act("P1", new PlayCardCommand("EXPLORER-TARGET-2", "OGN·004/298", ["ENEMY"]));
        Assert.Contains("EZREAL_ENEMY_TARGETS_THIS_TURN:P1:2", game.State.UntilEndOfTurnEffects);
        return (game, oneTarget);
    }

    // SFD 199/248; VEN FAQ p. 15: inherit the complete condition and
    // reaction timing. The non-resource draw still enters the ordinary chain.
    [Theory]
    [InlineData("SFD·199/221", false)]
    [InlineData("SFD·248/221", false)]
    [InlineData("SFD·199/221", true)]
    [InlineData("SFD·248/221", true)]
    public async Task BorrowedExplorerReactionRequiresTwoActualTargetEventsAndDrawsOnlyAtResolution(string donor, bool equipment)
    {
        var (game, oneTarget) = await ExplorerSecondTarget(donor, equipment);
        var ability = TemplateId(game.State, "两次");
        await RadGearTestPosition.Reject(ExplorerReactionPosition(donor), "P1", new ActivateAbilityCommand("HOST", ability, []));
        await RadGearTestPosition.Reject(oneTarget, "P1", new ActivateAbilityCommand("HOST", ability, []));
        await RadGearTestPosition.Reject(game.State, "P2", new ActivateAbilityCommand("HOST", ability, []));
        await RadGearTestPosition.Reject(game.State, "P1", new ActivateAbilityCommand("HOST", ability, ["ENEMY"]));
        var secondSpell = Assert.Single(game.State.StackItems).StackItemId;
        var before = game.State;
        await game.Act("P1", new ActivateAbilityCommand("HOST", ability, []));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(before.CardObjects["UNIT"], game.State.CardObjects["UNIT"]);
        Assert.Equal(before.PlayerZones["P1"].MainDeck, game.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(before.PlayerZones["P1"].Hand, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, game.State.StackItems.Count);
        var captured = game.State.StackItems.Single(i => i.BorrowedExhaust is not null);
        Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
            UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Where(e => !e.StartsWith("EZREAL_ENEMY_TARGETS_THIS_TURN:", StringComparison.Ordinal)).ToArray() }));
        Assert.Contains("invalid captured borrowed skill", OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = game.State.StackItems.Select(i => i == captured ? i with { TimingContext = "FORGED" } : i).ToArray() }));
        await game.Top();
        Assert.Contains(before.PlayerZones["P1"].MainDeck[0], game.State.PlayerZones["P1"].Hand);
        Assert.Equal(before.PlayerZones["P1"].MainDeck.Count - 1, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(secondSpell, Assert.Single(game.State.StackItems).StackItemId);
        await game.Top();
        await CloseExplorerDuel(game);
        await game.Replay();
    }

    [Theory]
    [InlineData("SFD·199/221")]
    [InlineData("SFD·248/221")]
    public async Task BorrowedExplorerReactionRetainsItsEarnedDrawWhenTheBorrowerReturnsToHand(string donor)
    {
        var (game, _) = await ExplorerSecondTarget(donor);
        var topCard = game.State.PlayerZones["P1"].MainDeck[0];
        await game.Act("P1", new ActivateAbilityCommand("HOST", TemplateId(game.State, "两次"), []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(topCard, game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Contains(topCard, game.State.PlayerZones["P1"].Hand);
        await game.Top();
        await CloseExplorerDuel(game);
        await game.Replay();
    }
}
