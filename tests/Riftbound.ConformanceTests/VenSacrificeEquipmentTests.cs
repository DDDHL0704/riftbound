using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenSacrificeEquipmentTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MainPhaseSacrificePaysThreeOtherPermanentsBeforeResponsesAndCanScoreTheWinningPoint(bool win)
    {
        var initial = Position();
        if (win) initial = initial with { PlayerScores = new Dictionary<string, int> { ["P1"] = 7, ["P2"] = 0 } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·067", []));
        Assert.Equal(10, game.State.RunePools["P1"].Mana); Assert.Empty(game.State.StackItems);
        await game.Act("P1", new EndTurnCommand()); Assert.Null(game.State.PendingCardChoice);
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal(MatchPhases.Main, game.State.Phase); Assert.Equal(0, game.State.RunePools["P1"].Mana);
        var choice = game.State.PendingCardChoice!; Assert.Equal(0, choice.RequiredCount); Assert.Equal(3, choice.MaxCount);
        Assert.Equal(new[] { "ALLY", "EXTRA", "GEAR" }, choice.LegalObjectIds);
        var before = game.State.PlayerScores["P1"];
        foreach (var ids in new string[][] { ["ALLY"], ["ALLY", "GEAR"], ["ALLY", "GEAR", "UNIT"], ["ALLY", "GEAR", "ENEMY"] })
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY", "GEAR", "EXTRA"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY", "GEAR", "EXTRA"]));
        Assert.Equal(before, game.State.PlayerScores["P1"]);
        Assert.All(new[] { "ALLY", "GEAR", "EXTRA" }, id => Assert.Contains(id, game.State.PlayerZones["P1"].Graveyard));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(3, Assert.Single(game.State.StackItems).TriggerCost!.DestroyedPermanents!.Count);
        await game.Top(); Assert.Equal(before + 1, game.State.PlayerScores["P1"]);
        Assert.Equal(win ? "P1" : null, game.State.WinnerPlayerId); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TriggerCanBeDeclinedAndCannotPayWithFewerThanThreeOtherFriendlyPermanents(bool insufficient)
    {
        var initial = Position();
        if (insufficient) initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Base = ["ALLY", "GEAR"], Hand = ["UNIT", "EXTRA"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["EXTRA"] = new("P1", "HAND") } };
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·067", []));
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        var before = game.State.PlayerScores["P1"];
        if (!insufficient) {
            var choice = game.State.PendingCardChoice!;
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        }
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.Equal(before, game.State.PlayerScores["P1"]);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task AReplacementForTheDestructionStillPaysTheCost()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "HOURGLASS", "OGN·077/298", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·067", []));
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        var choice = game.State.PendingCardChoice!; var before = game.State.PlayerScores["P1"];
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY", "GEAR", "EXTRA"]));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Contains("HOURGLASS", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(3, Assert.Single(game.State.StackItems).TriggerCost!.DestroyedPermanents!.Count);
        await game.Top(); Assert.Equal(before + 1, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Fact]
    public async Task OrdinaryEquipmentPlayRejectsUnpaidPrintedManaAndPower()
    {
        var initial = Position();
        var poor = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty } };
        await Reject(poor, "P1", new PlayCardCommand("UNIT", "VEN·067", []));
    }

    private static MatchState Position() => HandDiscardObserverTests.Put(VenUnitEntryBasicTests.Position("VEN·067"),
        "EXTRA", "UNL-085/219", "P1", "BASE");
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-sacrifice-equipment", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
