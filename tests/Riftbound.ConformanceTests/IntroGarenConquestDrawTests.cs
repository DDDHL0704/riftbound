using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed class IntroGarenConquestDrawTests
{
    private static async Task Conquer(Driver game, int count = 4)
    {
        var ids = Enumerable.Range(1, count).Select(i => "U" + i).ToArray();
        await game.Act("P1", new MoveUnitCommand(ids[0], "BASE", "BATTLEFIELD:TARGET", [], ids));
        await game.Act("P1", new PassFocusCommand());
        await game.Act("P2", new PassFocusCommand());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FourUnitsQueueAnIndependentResponseWindowEvenWhenTheLegendIsExhausted(bool exhausted)
    {
        var state = Position();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GAREN"] = state.CardObjects["GAREN"] with { IsExhausted = exhausted } } };
        var game = new Driver(state); await Conquer(game);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        var item = Assert.Single(game.State.StackItems);
        Assert.Empty(item.TargetObjectIds); Assert.Null(item.TriggerCost);
        Assert.Equal(4, item.LegendConquest!.Draw!.CountedUnits.Count);
        await game.Top();
        Assert.Equal(new[] { "DRAW1", "DRAW2" }, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(exhausted, game.State.CardObjects["GAREN"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task ARealResponseDroppingTheCountBelowFourDoesNotRecheckTheTriggerCondition()
    {
        var state = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20) } };
        var game = new Driver(state); await Conquer(game);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["U4"])); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations["U4"].Zone);
        Assert.Equal(3, Enumerable.Range(1, 4).Count(i => game.State.ObjectLocations["U" + i].Zone == "BATTLEFIELD"));
        Assert.Single(game.State.StackItems); Assert.DoesNotContain("DRAW1", game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(new[] { "U4", "DRAW1", "DRAW2" }, game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task ThreeAtConquestDoNotTriggerWhenAFourthUnitArrivesLater()
    {
        var state = LocalDestructionRecallTests.At(Position(3), "U4", "P1", "BASE");
        var game = new Driver(state); await Conquer(game, 3);
        Assert.Empty(game.State.StackItems);
        await game.Act("P1", new MoveUnitCommand("U4", "BASE", "BATTLEFIELD:TARGET", []));
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task EmptyDeckBurnoutOccursAtResolutionAndNotAtConquest()
    {
        var state = Position();
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { MainDeck = [], Graveyard = ["DRAW1", "DRAW2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["DRAW1"] = new("P1", "GRAVEYARD"), ["DRAW2"] = new("P1", "GRAVEYARD") } };
        var game = new Driver(state); await Conquer(game);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BURNOUT_APPLIED");
        Assert.Single(game.State.StackItems);
        await game.Top();
        Assert.Contains(game.Events(), e => e.Event.Kind == "BURNOUT_APPLIED");
        await game.Replay();
    }

    [Fact]
    public async Task WinningConquestEndsTheGameBeforeTheDrawTriggerCanResolve()
    {
        var state = Position();
        state = state with { PlayerScores = new Dictionary<string, int>(state.PlayerScores) { ["P1"] = 7 },
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.ScoreGainedThisTurn("OTHER", "P1")] };
        var game = new Driver(state); await Conquer(game);
        Assert.Equal("P1", game.State.WinnerPlayerId);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await game.Replay();
    }

    [Theory]
    [InlineData("missing-origin")] [InlineData("threshold")] [InlineData("duplicate-unit")]
    [InlineData("unknown-unit")] [InlineData("unit-generation")] [InlineData("unit-face")]
    [InlineData("field-generation")] [InlineData("controller")] [InlineData("future-tick")]
    [InlineData("source-generation")] [InlineData("source-face")]
    [InlineData("target")] [InlineData("amount")] [InlineData("repeat")] [InlineData("cost")]
    public async Task RecoveryRejectsForgedCapturedConditionsAndStackInstructions(string branch)
    {
        var game = new Driver(Position()); await Conquer(game);
        var state = game.State; var item = Assert.Single(state.StackItems);
        var context = item.LegendConquest!; var draw = context.Draw!;
        var units = draw.CountedUnits.ToArray();
        if (branch == "missing-origin") context = context with { Draw = null };
        if (branch == "threshold") draw = draw with { CountedUnits = units.Take(3).ToArray() };
        if (branch == "duplicate-unit") { units[3] = units[0]; draw = draw with { CountedUnits = units }; }
        if (branch == "unknown-unit") { units[0] = units[0] with { Unit = new("UNKNOWN", 0) }; draw = draw with { CountedUnits = units }; }
        if (branch == "unit-generation") { units[0] = units[0] with { Unit = units[0].Unit with { Generation = 99 } }; draw = draw with { CountedUnits = units }; }
        if (branch == "unit-face") { units[0] = units[0] with { CardNo = "OGN·219/298" }; draw = draw with { CountedUnits = units }; }
        if (branch == "field-generation") draw = draw with { Battlefield = draw.Battlefield with { Generation = 99 } };
        if (branch == "controller") draw = draw with { ControllerId = "P2" };
        if (branch == "future-tick") draw = draw with { CapturedTick = state.Tick + 1 };
        if (branch == "source-generation") context = context with { SourceGeneration = 99 };
        if (branch == "source-face") draw = draw with { FaceRevision = 99 };
        if (branch != "missing-origin") context = context with { Draw = draw };
        item = item with { LegendConquest = context };
        if (branch == "target") item = item with { TargetObjectIds = ["U1"] };
        if (branch == "amount") item = item with { DamageAmount = 99 };
        if (branch == "repeat") item = item with { EffectRepeatCount = 2 };
        if (branch == "cost") item = item with { OptionalCosts = ["SPEND_MANA:1"] };
        var forged = state with { StackItems = [item] };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("legend conquest"));
    }
}
