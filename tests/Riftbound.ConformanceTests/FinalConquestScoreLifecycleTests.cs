using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN effective 2026-10-16, 470.1 and 473.1.b–473.2:
// a scoring action, its point and its triggered skills are distinct.
public sealed class FinalConquestScoreLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeclareBattleChoicesRemainExactWhenZoneDictionaryOrderChanges(bool reversedZones)
    {
        var initial = OfficialHoldSequenceTests.AddUnit(Position(7), "DEFENDER", "SFD·125/221", "BF", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = initial.CardObjects["BF"] with { ControllerId = "P2" } } };
        var restored = initial with { PlayerZones = (reversedZones ? initial.PlayerZones.Reverse() : initial.PlayerZones)
            .ToDictionary(entry => entry.Key, entry => entry.Value) };
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(restored));
        var original = ResolutionResult.BuildPrompts(initial)["P1"];
        var actual = ResolutionResult.BuildPrompts(restored)["P1"];
        var declaration = Assert.Single(original.Candidates!, candidate => candidate.Action == CommandTypes.DeclareBattle);
        Assert.Equal(new[] { "BATTLEFIELD:P1-MAIN", "OTHER", "BF" }, declaration.Destinations!.Select(choice => choice.Id));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(original, json), JsonSerializer.SerializeToNode(actual, json)),
            "The entire original prompt, including destination and selection-step order, must survive JSONB dictionary reordering.");
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    public async Task TiedScoresAlreadyAboveTheThresholdStillReplaceAnIncompleteConquest(int score)
    {
        var initial = Position(score) with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = score, ["P2"] = score } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        var result = await Conquer(driver);
        Assert.Equal(score, driver.State.PlayerScores["P1"]);
        Assert.Equal(MatchStatuses.InProgress, driver.State.Status);
        Assert.Contains("DRAW1", driver.State.PlayerZones["P1"].Hand);
        Assert.Single(result.Events, e => e.Kind == "BATTLEFIELD_SCORE_REPLACED_BY_DRAW");
        Assert.DoesNotContain(result.Events, e => e.Kind is "SCORE_GAINED" or "MATCH_WON");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(7, false, 8, false)]
    [InlineData(8, false, 8, true)]
    [InlineData(8, true, 9, false)]
    public async Task HighroadUsesTheActualNinePointThreshold(int score, bool otherScored, int expectedScore, bool draws)
    {
        var initial = Position(score, otherScored);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER"] = initial.CardObjects["OTHER"] with { CardNo = "OGN·276/298", ControllerId = "P2" } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Conquer(driver);
        Assert.Equal(expectedScore, driver.State.PlayerScores["P1"]);
        Assert.Equal(draws, driver.State.PlayerZones["P1"].Hand.Contains("DRAW1"));
        Assert.Equal(expectedScore == 9 ? MatchStatuses.Finished : MatchStatuses.InProgress, driver.State.Status);
        Assert.Equal(expectedScore == 9 ? "P1" : null, driver.State.WinnerPlayerId);
        Assert.Equal(9, ResolutionResult.BuildSnapshots(driver.State)["P1"].Timing["winningScore"]);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task TheOtherPlayersScoreMarkerCannotSupplyMyMissingBattlefield()
    {
        var initial = Position(7) with {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.ScoreGainedThisTurn("OTHER", "P2")] };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Conquer(driver);
        Assert.Equal(7, driver.State.PlayerScores["P1"]);
        Assert.Contains("DRAW1", driver.State.PlayerZones["P1"].Hand);
        Assert.Contains(BattlefieldTaskMarkers.ScoreGainedThisTurn("OTHER", "P2"), driver.State.UntilEndOfTurnEffects);
        Assert.DoesNotContain(BattlefieldTaskMarkers.ScoreGainedThisTurn("OTHER", "P1"), driver.State.UntilEndOfTurnEffects);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ReplacementDrawFinishesBeforeZaunsConquestDiscardAndDraw()
    {
        var initial = Position(7, battlefield: "OGN·298/298");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        var result = await Conquer(driver);
        var choice = Assert.IsType<PendingHandChoiceState>(driver.State.PendingHandChoice);
        Assert.Contains("DRAW1", choice.LegalObjectIds);
        Assert.Equal(new[] { "H1", "H2", "H3", "DRAW1" }, driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(7, driver.State.PlayerScores["P1"]);
        Assert.Single(result.Events, e => e.Kind == "CARD_DRAWN");
        await driver.Act("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DRAW1"]));
        Assert.Equal(new[] { "H1", "H2", "H3", "DRAW2" }, driver.State.PlayerZones["P1"].Hand);
        Assert.Contains("DRAW1", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(7, driver.State.PlayerScores["P1"]);
        Assert.Null(driver.State.PendingHandChoice);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ActualCombatConquestUsesTheSameLastPointReplacement()
    {
        var initial = Position(7);
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Battlefields = ["BF", "DEFENDER"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["BF"] = initial.CardObjects["BF"] with { ControllerId = "P2" },
                ["DEFENDER"] = new("DEFENDER", cardNo: "SFD·125/221", power: 1,
                    tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState> {
                ["BF"] = new("P2", "BATTLEFIELD", "BF"), ["OTHER"] = new("P1", "BATTLEFIELD", "OTHER"),
                ["UNIT"] = new("P1", "BASE"), ["DEFENDER"] = new("P2", "BATTLEFIELD", "BF") } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        await driver.Act("P2", new PassFocusCommand());
        var result = await driver.Act("P1", new DeclareBattleCommand("BF", ["UNIT"], ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
        Assert.Equal(7, driver.State.PlayerScores["P1"]);
        Assert.Equal("P1", driver.State.CardObjects["BF"].ControllerId);
        Assert.Contains("DEFENDER", driver.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("DRAW1", driver.State.PlayerZones["P1"].Hand);
        Assert.Single(result.Events, e => e.Kind == "BATTLEFIELD_SCORE_REPLACED_BY_DRAW");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementDrawUsesRealBurnoutRecycleRngAndTheOpponentsPossibleWin(bool opponentWins)
    {
        var initial = Position(7);
        initial = initial with { RngCursor = 9,
            // CN 432.3.c.1 permits immediate victory on subsequent burnout.
            // The first, recyclable burnout remains an ordinary draw action.
            PlayerScores = new Dictionary<string, int> { ["P1"] = 7, ["P2"] = opponentWins ? 6 : 0 },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [], Graveyard = opponentWins ? [] : ["DRAW1", "DRAW2", "DRAW3"] } },
            CardObjects = opponentWins ? initial.CardObjects.Where(entry => entry.Key is not ("DRAW1" or "DRAW2" or "DRAW3"))
                .ToDictionary(entry => entry.Key, entry => entry.Value) : initial.CardObjects };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        var result = await Conquer(driver);
        Assert.Equal(7, driver.State.PlayerScores["P1"]);
        Assert.Equal(opponentWins ? 8 : 1, driver.State.PlayerScores["P2"]);
        Assert.Equal(opponentWins ? 9 : 10, driver.State.RngCursor);
        Assert.Empty(driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(opponentWins ? 0 : 2, driver.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(opponentWins ? 3 : 4, driver.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(opponentWins ? "P2" : null, driver.State.WinnerPlayerId);
        Assert.Equal(opponentWins ? MatchStatuses.Finished : MatchStatuses.InProgress, driver.State.Status);
        Assert.Equal(opponentWins ? 2 : 1, result.Events.Count(e => e.Kind == "BURNOUT_APPLIED"));
        Assert.DoesNotContain(result.Events, e => e.Kind == "SCORE_GAINED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AnAlreadyRecordedConquestAwardsNeitherAnotherPointNorAnotherReplacementDraw()
    {
        var initial = Position(7) with {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.ScoreGainedThisTurn("BF", "P1")] };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        var result = await Conquer(driver);
        Assert.Equal(7, driver.State.PlayerScores["P1"]);
        Assert.DoesNotContain("DRAW1", driver.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(result.Events, e => e.Kind is "SCORE_GAINED" or "CARD_DRAWN"
            or "BATTLEFIELD_CONQUERED" or "BATTLEFIELD_SCORE_REPLACED_BY_DRAW");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AReplacedConquestCanStillWinByHoldingOnTheNextTurn()
    {
        var initial = Position(7);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["P2-DRAW"] = new("P2-DRAW", cardNo: "OGN·004/298", tags: [CardObjectTags.SpellCard], ownerId: "P2", controllerId: "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { MainDeck = ["P2-DRAW"] } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Conquer(driver);
        Assert.Equal(7, driver.State.PlayerScores["P1"]);
        await driver.Act("P1", new EndTurnCommand());
        var result = await driver.Act("P2", new EndTurnCommand());
        Assert.Equal(8, driver.State.PlayerScores["P1"]);
        Assert.Equal("P1", driver.State.WinnerPlayerId);
        Assert.Equal(MatchStatuses.Finished, driver.State.Status);
        Assert.Contains(result.Events, e => e.Kind == "SCORE_GAINED"
            && Equals(e.Payload.GetValueOrDefault("reason"), "BATTLEFIELD_HELD_SCORE"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task DirectAbilityPointsDoNotRequireScoringEveryBattlefield()
    {
        var initial = OfficialHoldSequenceTests.AddUnit(Position(7), "RENATA", "SFD·088/221", "BF", "P1");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["BF"] = initial.CardObjects["BF"] with { ControllerId = "P1" } },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new(20, 0, new Dictionary<string, int> { ["blue"] = 4 }) } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new ActivateAbilityCommand("RENATA", P4ActivatedAbilityCatalog.RenataGlascScoreAbilityId, []));
        var result = await driver.Top();
        Assert.Equal(8, driver.State.PlayerScores["P1"]);
        Assert.Equal("P1", driver.State.WinnerPlayerId);
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_SCORE_REPLACED_BY_DRAW");
        Assert.DoesNotContain(BattlefieldTaskMarkers.ScoreGainedThisTurn("BF", "P1"), driver.State.UntilEndOfTurnEffects);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrushMayReturnAfterTheScoringActionEvenWhenItsPointBecameADraw(bool accept)
    {
        var initial = Position(7);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("UNL·T03", out var factory));
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Banished = ["BF-REPLACED"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["BF"] = factory.CreateObject("BF", "P2", "P2") with {
                    ControllerId = null, ReplacedBattlefieldCard = new("BF-REPLACED", 0) },
                ["BF-REPLACED"] = new("BF-REPLACED", cardNo: "OGN·282/298",
                    tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2", controllerId: "P2") { ReplacedAtBattlefieldId = "BF" },
                ["UNIT"] = initial.CardObjects["UNIT"] with { Tags = initial.CardObjects["UNIT"].Tags.Append("增益").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["BF"] = new("P2", "BATTLEFIELD", "BF"), ["BF-REPLACED"] = new("P2", "BANISHED") } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Conquer(driver);
        Assert.Equal(7, driver.State.PlayerScores["P1"]);
        Assert.Contains("DRAW1", driver.State.PlayerZones["P1"].Hand);
        var hand = driver.State.PlayerZones["P1"].Hand.ToArray();
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("TRIGGER_OPTIONAL_CONFIRMATION", choice.ChoiceWindow);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["BF"] : []));
        if (accept) await driver.Top();
        Assert.Equal(accept ? "OGN·282/298" : "UNL·T03", driver.State.CardObjects["BF"].CardNo);
        Assert.Equal(hand, driver.State.PlayerZones["P1"].Hand);
        Assert.Contains("增益", driver.State.CardObjects["UNIT"].Tags);
        Assert.Equal(7, driver.State.PlayerScores["P1"]);
        Assert.Equal(accept ? 0 : 1, driver.State.PlayerZones["P2"].Banished.Count);
        Assert.Contains(BattlefieldTaskMarkers.ScoreGainedThisTurn("BF", "P1"), driver.State.UntilEndOfTurnEffects);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static async Task<ResolutionResult> Conquer(StandbyDefendObserverTests.Driver driver)
    {
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand());
        return await driver.Act("P2", new PassFocusCommand());
    }

    private static MatchState Position(int score, bool otherScored = false, string battlefield = "OGN·296/298")
        => AddOther(ConquestLifecycleRegressionTests.State(battlefield)) with {
            PlayerScores = new Dictionary<string, int> { ["P1"] = score, ["P2"] = 0 },
            UntilEndOfTurnEffects = otherScored ? [BattlefieldTaskMarkers.ScoreGainedThisTurn("OTHER", "P1")] : [] };

    private static MatchState AddOther(MatchState state) => state with {
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Battlefields = state.PlayerZones["P1"].Battlefields.Append("OTHER").ToArray() } },
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = new("OTHER", cardNo: "OGN·280/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P1") },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["OTHER"] = new("P1", "BATTLEFIELD", "OTHER") } };
}
