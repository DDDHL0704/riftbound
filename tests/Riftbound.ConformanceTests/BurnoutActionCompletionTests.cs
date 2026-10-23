using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN core effective 2026-10-16: 303.1, 414.4.c, 432.2.d,
// 432.3.c.1 and 474 distinguish completing a first burnout from immediate subsequent victory.
public sealed class BurnoutActionCompletionTests
{
    [Fact]
    public async Task NormalVictoryAfterTheCompletedTopParentPrecedesAnyLowerChainResponse()
    {
        var initial = HweiMoveDiscardTypesTests.Position("OGN·083/298", deckCount: 0, runeCount: 0);
        initial = AddRecyclableCards(initial, ["REFILL0", "REFILL1", "REFILL2"], "P1") with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 } };
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["LOWER"] = HweiMoveDiscardTypesTests.Card("LOWER", "OGN·083/298") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["LOWER"] = new("P1", "HAND") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = ["LOWER", "HAND"] } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("LOWER", "OGN·083/298", []));
        var lowerId = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PlayCardCommand("HAND", "OGN·083/298", []));
        Assert.Equal(2, driver.State.StackItems.Count);
        await driver.Top();
        Assert.Equal(2, driver.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(8, driver.State.PlayerScores["P2"]);
        Assert.Equal(MatchStatuses.Finished, driver.State.Status);
        Assert.Equal("P2", driver.State.WinnerPlayerId);
        Assert.Equal(lowerId, Assert.Single(driver.State.StackItems).StackItemId);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "STACK_ITEM_RESOLVED"
            && Equals(e.Event.Payload.GetValueOrDefault("stackItemId"), lowerId));
        Assert.All(ResolutionResult.BuildPrompts(driver.State).Values, prompt => Assert.False(prompt.Actionable));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    // Only the existing preselected CARDS execution branch is under test;
    // this does not certify who selects the printed card's mode.
    [Fact]
    public async Task BothPlayersFirstBurnoutsCanCreateATieWithoutAnyIntermediateWinner()
    {
        var initial = HweiMoveDiscardTypesTests.Position("OGN·071/298", deckCount: 0, runeCount: 0);
        initial = AddRecyclableCards(initial, ["P1-REFILL"], "P1");
        initial = AddRecyclableCards(initial, ["P2-REFILL"], "P2") with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = 7, ["P2"] = 7 } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("HAND", "OGN·071/298", [], "OTHER_PLAYERS_CHOOSE_CARDS"));
        await driver.Top();
        Assert.Equal(new[] { "P1-REFILL" }, driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(new[] { "P2-REFILL" }, driver.State.PlayerZones["P2"].Hand);
        Assert.Equal(8, driver.State.PlayerScores["P1"]);
        Assert.Equal(8, driver.State.PlayerScores["P2"]);
        Assert.Equal(MatchStatuses.InProgress, driver.State.Status);
        Assert.Null(driver.State.WinnerPlayerId);
        Assert.Equal(2, driver.Events().Count(e => e.Event.Kind == "BURNOUT_APPLIED"));
        Assert.Equal(2, driver.Events().Count(e => e.Event.Kind == "CARD_DRAWN"));
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "MATCH_WON");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(1, 9, 1, 2)]
    [InlineData(3, 8, 2, 1)]
    public async Task OnePrintedDrawTwoCompletesUntilItsOwnSubsequentBurnoutWinsImmediately(
        int recyclableCards, int opponentScore, int cardsDrawn, int burnoutCount)
    {
        var initial = HweiMoveDiscardTypesTests.Position("OGN·083/298", deckCount: 0, runeCount: 0);
        var ids = Enumerable.Range(0, recyclableCards).Select(i => "REFILL"+i).ToArray();
        initial = AddRecyclableCards(initial, ids, "P1") with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("HAND", "OGN·083/298", []));
        await driver.Top();
        Assert.Equal(cardsDrawn, driver.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(recyclableCards-cardsDrawn, driver.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(opponentScore, driver.State.PlayerScores["P2"]);
        Assert.Equal("P2", driver.State.WinnerPlayerId);
        Assert.Equal(MatchStatuses.Finished, driver.State.Status);
        Assert.Equal(burnoutCount, driver.Events().Count(e => e.Event.Kind == "BURNOUT_APPLIED"));
        Assert.Equal(cardsDrawn, Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_DRAWN").Event.Payload["count"]);
        Assert.Equal(1, driver.Events().Count(e => e.Event.Kind == "MATCH_WON"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task AFirstBurnoutAtEightWaitsForTheParentsRequiredDiscard(int recyclableCards)
    {
        var initial = HweiMoveDiscardTypesTests.Position(deckCount: 0);
        var ids = Enumerable.Range(0, recyclableCards).Select(i => "REFILL"+i).ToArray();
        initial = AddRecyclableCards(initial, ids, "P1") with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await HweiMoveDiscardTypesTests.Parent(driver);
        var pending = Assert.IsType<PendingHandChoiceState>(driver.State.PendingHandChoice);
        Assert.Equal(8, driver.State.PlayerScores["P2"]);
        Assert.Equal(MatchStatuses.InProgress, driver.State.Status);
        Assert.Null(driver.State.WinnerPlayerId);
        Assert.Equal(2, driver.State.PlayerZones["P1"].Hand.Count);
        var drawn = Assert.Single(driver.State.PlayerZones["P1"].Hand, id => id != "HAND");
        Assert.Contains(drawn, pending.LegalObjectIds);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(driver.State));
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "MATCH_WON");
        await HweiMoveDiscardTypesTests.Discard(driver, drawn);
        Assert.Equal(new[] { "HAND" }, driver.State.PlayerZones["P1"].Hand);
        Assert.Contains(drawn, driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(8, driver.State.PlayerScores["P2"]);
        Assert.Equal("P2", driver.State.WinnerPlayerId);
        Assert.Equal(MatchStatuses.Finished, driver.State.Status);
        Assert.Null(driver.State.PendingHandChoice);
        var kinds = driver.Events().Select(e => e.Event.Kind).ToList();
        Assert.Single(driver.Events(), e => e.Event.Kind == "HAND_CHOICE_RESOLVED");
        Assert.Single(driver.Events(), e => e.Event.Kind == "MATCH_WON");
        Assert.True(kinds.IndexOf("HAND_CHOICE_RESOLVED") < kinds.IndexOf("MATCH_WON"));
        Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static MatchState AddRecyclableCards(MatchState state, string[] ids, string owner)
    {
        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        foreach (var id in ids)
        {
            cards[id] = HweiMoveDiscardTypesTests.Card(id, "OGN·083/298", owner);
            locations[id] = new(owner, "GRAVEYARD");
        }
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                [owner] = state.PlayerZones[owner] with { Graveyard = ids } } };
    }

    [Fact]
    public async Task AConquestBelowTheFinalPointDoesNotAttemptTheReplacementDrawOrBurnout()
    {
        var initial = ConquestLifecycleRegressionTests.State("OGN·296/298") with {
            RngCursor = 9,
            PlayerScores = new Dictionary<string, int> { ["P1"] = 6, ["P2"] = 6 } };
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [], Graveyard = ["DRAW1", "DRAW2", "DRAW3"] } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        var result = await driver.Act("P2", new PassFocusCommand());
        Assert.Equal(7, driver.State.PlayerScores["P1"]);
        Assert.Equal(6, driver.State.PlayerScores["P2"]);
        Assert.Equal(3, driver.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(driver.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(3, driver.State.PlayerZones["P1"].Graveyard.Count);
        Assert.Equal(9, driver.State.RngCursor);
        Assert.Null(driver.State.WinnerPlayerId);
        Assert.DoesNotContain(result.Events, e => e.Kind is "BURNOUT_APPLIED" or "CARD_DRAWN" or "BATTLEFIELD_SCORE_REPLACED_BY_DRAW");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(7, 7, true)]
    [InlineData(8, 8, true)]
    [InlineData(7, 0, false)]
    [InlineData(7, 6, false)]
    public async Task AFirstRecyclableBurnoutCompletesTheOriginalDrawBeforeNormalVictory(
        int ownScore, int opponentScore, bool opponentWins)
    {
        var baseState = ConquestLifecycleRegressionTests.State("OGN·296/298");
        var initial = baseState with {
            RngCursor = 9,
            PlayerScores = new Dictionary<string, int> { ["P1"] = ownScore, ["P2"] = opponentScore },
            PlayerZones = new Dictionary<string, PlayerZones>(baseState.PlayerZones) {
                ["P1"] = baseState.PlayerZones["P1"] with {
                    MainDeck = [], Graveyard = ["DRAW1", "DRAW2", "DRAW3"],
                    Battlefields = baseState.PlayerZones["P1"].Battlefields.Append("OTHER").ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(baseState.CardObjects) {
                ["OTHER"] = new("OTHER", cardNo: "OGN·280/298", ownerId: "P1",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(baseState.ObjectLocations) {
                ["OTHER"] = new("P1", "BATTLEFIELD", "OTHER") } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        var result = await driver.Act("P2", new PassFocusCommand());

        Assert.Equal(4, driver.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(initial.PlayerZones["P1"].Hand, driver.State.PlayerZones["P1"].Hand.Take(3));
        Assert.Contains(driver.State.PlayerZones["P1"].Hand[^1], new[] { "DRAW1", "DRAW2", "DRAW3" });
        Assert.Equal(2, driver.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Empty(driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(10, driver.State.RngCursor);
        Assert.Equal(ownScore, driver.State.PlayerScores["P1"]);
        Assert.Equal(opponentScore+1, driver.State.PlayerScores["P2"]);
        Assert.Equal(opponentWins ? "P2" : null, driver.State.WinnerPlayerId);
        Assert.Equal(opponentWins ? MatchStatuses.Finished : MatchStatuses.InProgress, driver.State.Status);
        Assert.Equal("P1", driver.State.CardObjects["BF"].ControllerId);
        Assert.Contains(BattlefieldTaskMarkers.ScoreGainedThisTurn("BF", "P1"), driver.State.UntilEndOfTurnEffects);
        Assert.Single(result.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.Equal(1, Assert.Single(result.Events, e => e.Kind == "CARD_DRAWN").Payload["count"]);
        var kinds = result.Events.Select(e => e.Kind).ToList();
        Assert.True(kinds.IndexOf("BURNOUT_APPLIED") < kinds.IndexOf("CARD_DRAWN"));
        if (opponentWins)
        {
            Assert.Single(result.Events, e => e.Kind == "MATCH_WON");
            Assert.True(kinds.IndexOf("CARD_DRAWN") < kinds.IndexOf("MATCH_WON"));
        }
        else Assert.DoesNotContain(result.Events, e => e.Kind == "MATCH_WON");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }
}
