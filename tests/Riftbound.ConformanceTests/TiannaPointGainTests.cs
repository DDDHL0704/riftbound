using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class TiannaPointGainTests
{
    private const string Tianna = "SFD·060/221";

    private static MatchState WithOpponentTianna(MatchState state, bool battlefield = true, int copies = 1)
    {
        for (var i = 0; i < copies; i++)
            state = RadGearTestPosition.Put(state, i == 0 ? "TIANNA" : "TIANNA" + i, Tianna, "P2",
                battlefield ? "BATTLEFIELD" : "BASE", battlefield ? "OTHER" : null);
        return state;
    }

    private static async Task CloseDuel(Driver game)
    {
        for (var passes = 0; game.State.SpellDuelState.IsActive; passes++)
        {
            Assert.InRange(passes, 0, 8);
            Assert.Empty(game.State.StackItems);
            if (game.State.PriorityPlayerId is { } priority) await game.Act(priority, new PassPriorityCommand());
            else await game.Act(game.State.SpellDuelState.FocusPlayerId!, new PassFocusCommand());
        }
    }

    [Theory]
    [InlineData(false, 1)] [InlineData(true, 1)] [InlineData(true, 2)]
    public async Task NaturalHoldKeepsItsDrawSkillAndGiantDiscountEvenWhenPointsArePrevented(bool battlefield, int copies)
    {
        var initial = WithOpponentTianna(HeldBattlefieldPlayCostTests.Position(1), battlefield, copies);
        initial = RadGearTestPosition.Put(initial, "DRAW", "SFD·027/221", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await game.Act("P2", new EndTurnCommand());
        Assert.Equal(battlefield ? 0 : 1, game.State.PlayerScores["P1"]);
        Assert.Single(game.State.BattlefieldHolds!.Entries, entry => entry.PlayerId == "P1");
        Assert.Equal("DRAW", Assert.Single(game.State.StackItems).HeldContext!.Kind);
        if (battlefield)
        {
            var prevented = Assert.Single(game.Events(), e => e.Event.Kind == "SCORE_GAIN_PREVENTED");
            Assert.Equal(copies, JsonSerializer.SerializeToElement(prevented.Event.Payload["preventingSourceObjectIds"]).GetArrayLength());
        }
        else Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "SCORE_GAIN_PREVENTED");
        await game.Top();
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.Equal(4, game.State.PlayerZones["P1"].Hand.Count);
        await game.TapRunes("P1", Enumerable.Range(0, 8).Select(i => "GREEN" + i));
        var command = new PlayCardCommand("GIANT", "SFD·055/221", [], OptionalCosts: ["RECYCLE_RUNE:GREEN0", "RECYCLE_RUNE:GREEN1"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(8, quote.Cost!.Mana);
        Assert.Equal(2, quote.Cost.PowerByTrait[RuneTrait.Green]);
        await game.Act("P1", command);
        Assert.Contains("GIANT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Fact]
    public async Task TheCurrentControllerKeepsItsOwnPointsAndPreventsTheOwnersOpposingPoints()
    {
        var initial = WithOpponentTianna(HeldBattlefieldPlayCostTests.Position(1));
        initial = RadUnitEffectTestFixture.Place(initial, "TIANNA", "P1", "BATTLEFIELD", "BF");
        initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        initial = initial with { PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 } };
        var game = new Driver(initial); await game.Act("P2", new EndTurnCommand());
        Assert.Equal("P2", game.State.CardObjects["TIANNA"].OwnerId);
        Assert.Equal("P1", game.State.CardObjects["TIANNA"].ControllerId);
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(7, game.State.PlayerScores["P2"]); Assert.Null(game.State.WinnerPlayerId);
        Assert.Equal(MatchStatuses.InProgress, game.State.Status); await game.Replay();
    }

    private static MatchState ConquestPosition(string host = "OGN·039/298", bool lastPoint = false)
    {
        var state = WithOpponentTianna(HeldBattlefieldPlayCostTests.Position(0, opponentTurn: false));
        state = RadGearTestPosition.Put(state, "HOST", host, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "TARGET", "OGN·296/298", "P2", "BATTLEFIELD", "TARGET");
        return state with { PlayerScores = new Dictionary<string, int> { ["P1"] = lastPoint ? 7 : 0, ["P2"] = 0 } };
    }

    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:TARGET", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
    }

    [Fact]
    public async Task APreventedConquestPointStillEarnsItsSkillAndConsumesTheScoringAction()
    {
        var game = new Driver(ConquestPosition()); await Conquer(game);
        Assert.Equal(0, game.State.PlayerScores["P1"]);
        Assert.Single(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        Assert.Contains(BattlefieldTaskMarkers.ScoreGainedThisTurn("TARGET", "P1"), game.State.UntilEndOfTurnEffects);
        Assert.Single(game.State.BattlefieldConquests!.Entries, entry => entry.PlayerId == "P1" && entry.Battlefield.ObjectId == "TARGET");
        Assert.Null(game.State.BattlefieldHolds);
        Assert.Single(game.State.StackItems); await game.Top(); await CloseDuel(game);
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(0, game.State.PlayerScores["P1"]);
        var command = new PlayCardCommand("GIANT", "SFD·055/221", []);
        Assert.Equal(10, new CoreRuleEngine().PreviewPlayCard(game.State, "P1",
            PlayCostPreviewTests.Request(game.State, command)).Cost!.Mana);
        await game.Replay();
    }

    [Fact]
    public async Task APreventedConquestPointStillEarnsTheFourUnitLegendDraw()
    {
        var initial = WithOpponentTianna(OfficialGarenConquestBattlefieldCountTests.Position());
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("U1", "BASE", "BATTLEFIELD:TARGET", [], ["U1", "U2", "U3", "U4"]));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Equal(0, game.State.PlayerScores["P1"]);
        Assert.Single(game.Events(), e => e.Event.Kind == "SCORE_GAIN_PREVENTED");
        Assert.Single(game.Events(), e => e.Event.Kind == "BATTLEFIELD_CONQUERED");
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("OGS·023/024", skill.LegendConquest!.CardNo);
        Assert.Equal(4, skill.LegendConquest.Draw!.CountedUnits.Count);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Equal(new[] { "DRAW1", "DRAW2" }, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Fact]
    public async Task ALastPointReplacedByDrawingStillDrawsWhileThePointRestrictionIsPresent()
    {
        var game = new Driver(ConquestPosition("OGN·219/298", lastPoint: true)); await Conquer(game); await CloseDuel(game);
        Assert.Equal(7, game.State.PlayerScores["P1"]); Assert.Null(game.State.WinnerPlayerId);
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Single(game.Events(), e => e.Event.Kind == "BATTLEFIELD_SCORE_REPLACED_BY_DRAW");
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "SCORE_GAIN_PREVENTED"); await game.Replay();
    }

    [Fact]
    public async Task APreventedHoldingPointStillEarnsTheUnitsScoringActionToken()
    {
        var initial = WithOpponentTianna(HeldBattlefieldPlayCostTests.Position(1));
        initial = RadGearTestPosition.Put(initial, "ILLAOI", "VEN·109", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await game.Act("P2", new EndTurnCommand());
        Assert.Equal(0, game.State.PlayerScores["P1"]);
        Assert.Equal("SCORE", Assert.Single(game.State.StackItems).SourceUnitTokens!.Timing);
        await game.Top();
        Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo is { } no
            && P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory) && factory.TokenFamilyName == "触手");
        Assert.Equal(0, game.State.PlayerScores["P1"]); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ThePaidPointSkillChecksTheRestrictionAfterActualReturnResponses(bool returnTianna)
    {
        var initial = WithOpponentTianna(HeldBattlefieldPlayCostTests.Position(0, opponentTurn: false));
        initial = RadGearTestPosition.Put(initial, "RENATA", P4ActivatedAbilityCatalog.RenataGlascCardNo, "P1", "BATTLEFIELD", "BF");
        initial = RadGearTestPosition.Put(initial, "RETURN", "OGN·104/298", "P2", "HAND");
        initial = initial with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 4 }), ["P2"] = new(5, 0) },
            PlayerScores = new Dictionary<string, int> { ["P1"] = 7, ["P2"] = 0 }
        };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("RENATA", P4ActivatedAbilityCatalog.RenataGlascScoreAbilityId, []));
        Assert.True(game.State.CardObjects["RENATA"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        if (returnTianna)
        {
            await game.Act("P1", new PassPriorityCommand());
            await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["TIANNA"])); await game.Top();
            Assert.Contains("TIANNA", game.State.PlayerZones["P2"].Hand);
        }
        await game.Top();
        Assert.Equal(returnTianna ? 8 : 7, game.State.PlayerScores["P1"]);
        Assert.Equal(returnTianna ? "P1" : null, game.State.WinnerPlayerId);
        Assert.Equal(returnTianna ? 0 : 1, game.Events().Count(e => e.Event.Kind == "SCORE_GAIN_PREVENTED"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheFirstBurnoutIsPreventedButCyclicBurnoutContinuesToVictory(bool cyclic)
    {
        var initial = HeldBattlefieldPlayCostTests.Position(0, opponentTurn: false);
        initial = RadGearTestPosition.Put(initial, "TIANNA", Tianna, "P1", "BATTLEFIELD", "BF");
        initial = RadGearTestPosition.Put(initial, "RENATA", P4ActivatedAbilityCatalog.RenataGlascCardNo, "P1", "BATTLEFIELD", "BF");
        foreach (var id in initial.PlayerZones["P1"].MainDeck.ToArray())
            initial = RadUnitEffectTestFixture.Place(initial, id, "P1", "BANISHED");
        if (!cyclic) initial = RadGearTestPosition.Put(initial, "RECYCLE", "OGN·219/298", "P1", "GRAVEYARD");
        initial = initial with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(1, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }), ["P2"] = RunePool.Empty },
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 }
        };
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("RENATA", P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId, []));
        await game.Top();
        Assert.Equal(cyclic ? 8 : 7, game.State.PlayerScores["P2"]);
        Assert.Equal(cyclic ? "P2" : null, game.State.WinnerPlayerId);
        Assert.Equal(cyclic ? 2 : 1, game.Events().Count(e => e.Event.Kind == "BURNOUT_APPLIED"));
        Assert.Single(game.Events(), e => e.Event.Kind == "SCORE_GAIN_PREVENTED");
        if (!cyclic) Assert.Contains("RECYCLE", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }
}
