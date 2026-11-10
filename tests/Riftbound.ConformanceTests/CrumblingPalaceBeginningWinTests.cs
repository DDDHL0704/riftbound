using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class CrumblingPalaceBeginningWinTests
{
    private const string Palace = "UNL-088/219";
    private static MatchState Position(int hand = 4, int units = 4, string controller = "P1", string owner = "P1")
    {
        var state = RadGearTestPosition.Put(OfficialHoldSequenceTests.State("OGN·294/298"), "PALACE", Palace, controller, "BASE");
        state = RadGearTestPosition.Put(state, "F2", "OGN·294/298", "P1", "BATTLEFIELD", "F2");
        for (var i = 0; i < hand; i++) state = RadGearTestPosition.Put(state, "H" + i, "OGN·219/298", "P1", "HAND");
        for (var i = 0; i < units; i++) state = RadGearTestPosition.Put(state, "U" + i, "OGN·219/298", "P1", "BATTLEFIELD", i % 2 == 0 ? "F" : "F2");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["PALACE"] = state.CardObjects["PALACE"] with { OwnerId = owner } } };
    }
    private static async Task Open(Driver game) => await game.Act("P1", new PassPriorityCommand());
    private static async Task Win(Driver game)
    {
        var skill = Assert.Single(game.State.StackItems); Assert.Equal(Palace, skill.CardNo); Assert.Empty(skill.TargetObjectIds);
        Assert.Equal(0, game.State.PlayerScores.GetValueOrDefault("P1")); Assert.Equal("SCORE", game.State.TurnStartStep);
        await game.Top(); Assert.Equal("P1", game.State.WinnerPlayerId); Assert.Equal(MatchStatuses.Finished, game.State.Status);
        Assert.Equal(0, game.State.PlayerScores.GetValueOrDefault("P1")); await game.Replay();
    }

    [Theory]
    [InlineData(3, 3)] [InlineData(3, 4)] [InlineData(3, 5)]
    [InlineData(4, 3)] [InlineData(4, 4)] [InlineData(4, 5)]
    [InlineData(5, 3)] [InlineData(5, 4)] [InlineData(5, 5)]
    public async Task OnlyExactlyFourHandCardsAndFourUnitsAcrossAllBattlefieldsEarnTheBeforeScoringWin(int hand, int units)
    {
        var game = new Driver(Position(hand, units)); await Open(game);
        if (hand == 4 && units == 4) await Win(game);
        else { Assert.Empty(game.State.StackItems); Assert.Null(game.State.WinnerPlayerId); await game.Replay(); }
    }

    [Theory]
    [InlineData("P1", "P2", true)] [InlineData("P2", "P1", false)]
    public async Task TheStartingPlayerMustCurrentlyControlTheEquipmentRegardlessOfItsOwner(string controller, string owner, bool earns)
    {
        var game = new Driver(Position(controller: controller, owner: owner)); await Open(game);
        if (earns) await Win(game);
        else { Assert.Empty(game.State.StackItems); Assert.Null(game.State.WinnerPlayerId); await game.Replay(); }
    }

    [Fact]
    public async Task AUnitTokenCountsButABaseUnitAndOrdinaryEquipmentDoNot()
    {
        var state = RadGearTestPosition.Put(Position(), "BASE-UNIT", "OGN·219/298", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "EXTRA-GEAR", "OGN·017/298", "P1", "BASE");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("OGN·271/298", out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["U3"] = factory.CreateObject("U3", "P1", "P1", isExhausted: false) } };
        var game = new Driver(state); await Open(game); await Win(game);
    }

    [Fact]
    public async Task ARealBlinkChangingBothCountsCannotCancelTheAlreadyEarnedWin()
    {
        var game = new Driver(RadGearTestPosition.Put(Position(), "H0", "OGS·011/024", "P1", "HAND")); await Open(game);
        await game.Act("P1", new PlayCardCommand("H0", "OGS·011/024", ["U0"])); await game.Top();
        Assert.Equal(3, game.State.PlayerZones["P1"].Hand.Count); Assert.Contains("U0", game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.WinnerPlayerId); await Win(game);
    }

    [Theory]
    [InlineData("erase")] [InlineData("controller")]
    public async Task RecoveryRequiresTheOriginalAcceptedBeginningWinCapture(string change)
    {
        var game = new Driver(Position()); await Open(game); var skill = Assert.Single(game.State.StackItems);
        var forged = game.State with { StackItems = change == "erase" ? [] : [skill with { ControllerId = "P2" }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await Win(game);
    }
}
