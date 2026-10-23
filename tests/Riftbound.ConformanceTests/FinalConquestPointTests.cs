using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN current core 473.1.b.1: only conquest points need every battlefield this turn.
public sealed class FinalConquestPointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConquestAtSevenDrawsInsteadUnlessEveryOtherBattlefieldAlreadyScoredThisTurn(bool otherScored)
    {
        var initial = Position(7, otherScored);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        await driver.Act("P2", new PassFocusCommand());
        Assert.Equal(otherScored ? 8 : 7, driver.State.PlayerScores["P1"]);
        Assert.Equal(otherScored ? MatchStatuses.Finished : MatchStatuses.InProgress, driver.State.Status);
        Assert.Equal(otherScored ? "P1" : null, driver.State.WinnerPlayerId);
        Assert.Equal(!otherScored, driver.State.PlayerZones["P1"].Hand.Contains("DRAW1"));
        Assert.Equal(otherScored ? 3 : 2, driver.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Contains(BattlefieldTaskMarkers.ScoreGainedThisTurn("BF", "P1"), driver.State.UntilEndOfTurnEffects);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AConquestBelowTheFinalPointStillGainsTheOrdinaryPoint()
    {
        var initial = Position(6, false);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        await driver.Act("P2", new PassFocusCommand());
        Assert.Equal(7, driver.State.PlayerScores["P1"]);
        Assert.Equal(MatchStatuses.InProgress, driver.State.Status);
        Assert.DoesNotContain("DRAW1", driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(3, driver.State.PlayerZones["P1"].MainDeck.Count);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static MatchState Position(int score, bool otherScored)
    {
        var state = ConquestLifecycleRegressionTests.State("OGN·296/298");
        return state with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = score, ["P2"] = 0 },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Battlefields = ["OTHER"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["OTHER"] = new("OTHER", cardNo: "OGN·280/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P1") },
            UntilEndOfTurnEffects = otherScored ? [BattlefieldTaskMarkers.ScoreGainedThisTurn("OTHER", "P1")] : [] };
    }
}
