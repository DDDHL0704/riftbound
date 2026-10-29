using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class IllaoiTentacleTests
{
    private static CardObjectState[] Tentacles(MatchState state)
        => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo == "VEN·T06").ToArray();
    private static CardObjectState Source(string id, string no)
        => new(id, power: 4, cardNo: no, manaCost: 6, tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1");

    [Theory]
    [InlineData("VEN·109", 37806)] [InlineData("VEN·182", 37885)]
    public async Task OfficialPrintingsAndTokenKeepTheirIdentityAndPrintedCharacteristics(string no, long id)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(6, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(4, card.Power);
        var spec = RuleTextParser.Parse(card);
        Assert.Contains(spec.Triggers, t => t.Kind == TriggerKinds.SourceUnitPlayedOrScoredCreateUnit && t.CreatedTokenName == "触手");
        Assert.Contains(spec.StaticAuras, a => a.Kind == StaticAuraKinds.FriendlyFilteredUnitCountToSourcePower);
        var token = Assert.Single(catalog.Cards, c => c.CardNo == "VEN·T06");
        Assert.Equal(37926, token.Id); Assert.True(OfficialRuleDomainBehaviorCatalog.IsTokenCard(token));
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(token.CardNo, out _));
    }

    [Theory]
    [InlineData("VEN·109")] [InlineData("VEN·182")]
    public async Task PaidPlayQueuesARespondableTentacleAndCountsItOnlyAfterResolution(string no)
    {
        var game = new Driver(EmpowerTests.Position(no));
        await game.Act("P1", new PlayCardCommand("HOST", no, []));
        Assert.Equal(14, game.State.RunePools["P1"].Mana); Assert.Empty(Tentacles(game.State));
        Assert.Equal(4, ArmamentMightTests.Might(game.State));
        Assert.Equal("PLAY", Assert.Single(game.State.StackItems).SourceUnitTokens!.Timing);
        await game.Top(); var token = Assert.Single(Tentacles(game.State));
        Assert.Equal(1, token.Power); Assert.True(token.IsExhausted); Assert.Contains("比尔吉沃特", token.Tags);
        Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base); Assert.Equal(5, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·109", "mana")] [InlineData("VEN·182", "mana")]
    [InlineData("VEN·109", "target")] [InlineData("VEN·182", "opponent")]
    public async Task IllegalPlayCannotSpendResourcesOrCreateAToken(string no, string failure)
    {
        var state = EmpowerTests.Position(no);
        if (failure == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(5, 0) } };
        var hash = MatchStateHasher.Hash(state);
        var command = new PlayCardCommand("HOST", no, failure == "target" ? ["HOST"] : []);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-illaoi", failure == "opponent" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("VEN·109")] [InlineData("VEN·182")]
    public async Task AReturnResponseRetainsTheEarnedTokenWithoutExposingTheNewHandInstance(string no)
    {
        var state = AttackDamageDistributionTests.Spell(EmpowerTests.Position(no), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("HOST", no, []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand); Assert.Empty(Tentacles(game.State));
        await game.Top(); Assert.Single(Tentacles(game.State)); Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·109")] [InlineData("VEN·182")]
    public async Task HoldingTheBattlefieldCreatesOneTentacleBeforeChannelAndDraw(string no)
    {
        var state = OfficialHoldSequenceTests.AddUnit(OfficialHoldSequenceTests.State("OGN·278/298"), "HOST", no, "F", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["HOST"] = Source("HOST", no) } };
        var game = new Driver(state); await game.Act("P1", new PassPriorityCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]); Assert.Equal("CHANNEL", game.State.TurnStartStep);
        Assert.Equal("SCORE", Assert.Single(game.State.StackItems).SourceUnitTokens!.Timing); Assert.Empty(Tentacles(game.State));
        await game.Top(); Assert.Single(Tentacles(game.State)); Assert.Equal(5, ArmamentMightTests.Might(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·109", false, false)] [InlineData("VEN·182", false, false)]
    [InlineData("VEN·109", true, false)] [InlineData("VEN·182", true, false)]
    [InlineData("VEN·109", false, true)] [InlineData("VEN·182", false, true)]
    public async Task ConquestScoringTriggersEvenWhenThePointBecomesADrawButNotWhenAlreadyScored(string no, bool finalPoint, bool alreadyScored)
    {
        var state = ConquestLifecycleRegressionTests.State("OGN·296/298", handCount: 0);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = Source("UNIT", no), ["OTHER"] = new("OTHER", cardNo: "OGN·294/298", tags: [P6TokenFactoryCatalog.BattlefieldCardTag], ownerId: "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Battlefields = ["BF", "OTHER"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["BF"] = new("P2", "BATTLEFIELD", "BF"), ["OTHER"] = new("P2", "BATTLEFIELD", "OTHER") },
            PlayerScores = new Dictionary<string, int> { ["P1"] = finalPoint ? 7 : 0, ["P2"] = 0 },
            UntilEndOfTurnEffects = alreadyScored ? [BattlefieldTaskMarkers.ScoreGainedThisTurn("BF", "P1")] : [] };
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        if (alreadyScored) { Assert.Empty(game.State.StackItems); Assert.Empty(Tentacles(game.State)); }
        else
        {
            Assert.Equal("SCORE", Assert.Single(game.State.StackItems).SourceUnitTokens!.Timing);
            await game.Top();
            Assert.Single(Tentacles(game.State));
        }
        Assert.Equal(finalPoint ? 7 : alreadyScored ? 0 : 1, game.State.PlayerScores["P1"]);
        if (finalPoint) Assert.Single(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Fact]
    public async Task DestroyingTheTokenImmediatelyRemovesItsMightContribution()
    {
        var state = AttackDamageDistributionTests.Spell(EmpowerTests.Position("VEN·109"), "KILL", "OGN·229/298", "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("HOST", "VEN·109", [])); await game.Top();
        var token = Assert.Single(Tentacles(game.State));
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", [token.ObjectId])); await game.Top();
        Assert.Empty(Tentacles(game.State)); Assert.Equal(4, ArmamentMightTests.Might(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData("P1", "BASE", "VEN·T06", false, 5)]
    [InlineData("P1", "BATTLEFIELD", "VEN·T06", false, 5)]
    [InlineData("P2", "BASE", "VEN·T06", false, 4)]
    [InlineData("P1", "GRAVEYARD", "VEN·T06", false, 4)]
    [InlineData("P1", "HAND", "VEN·T06", false, 4)]
    [InlineData("P1", "BATTLEFIELD", "VEN·T06", true, 4)]
    [InlineData("P1", "BASE", "UNL·T05", false, 4)]
    public void MightCountsOnlyControlledPublicUnitTokensAtAnyFieldLocation(string controller, string zone, string no, bool hidden, int expected)
    {
        var state = ArmamentMightTests.Position(unit: "VEN·182", power: 4);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(no, out var factory));
        var token = factory.CreateObject("TOKEN", "P1", controller) with { IsFaceDown = hidden };
        var zones = state.PlayerZones[controller];
        zones = zone switch { "BASE" => zones with { Base = [..zones.Base, "TOKEN"] },
            "BATTLEFIELD" => zones with { Battlefields = [..zones.Battlefields, "TOKEN"] },
            "HAND" => zones with { Hand = [..zones.Hand, "TOKEN"] },
            _ => zones with { Graveyard = [..zones.Graveyard, "TOKEN"] } };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["TOKEN"] = token },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [controller] = zones },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["TOKEN"] = new(controller, zone, zone == "BATTLEFIELD" ? "F" : null) } };
        Assert.Equal(expected, ArmamentMightTests.Might(state));
        if (!hidden && zone is "BASE" or "BATTLEFIELD") ArmamentMightTests.PublicRecovery(state);
    }

    [Theory]
    [InlineData("generation")] [InlineData("tick")] [InlineData("controller")] [InlineData("timing")] [InlineData("battlefield")]
    public async Task RecoveryRejectsForgedCapturedTokenTriggers(string mutation)
    {
        var game = new Driver(EmpowerTests.Position("VEN·109")); await game.Act("P1", new PlayCardCommand("HOST", "VEN·109", []));
        var item = Assert.Single(game.State.StackItems); var c = item.SourceUnitTokens!;
        var changed = c with { Source = mutation == "generation" ? c.Source with { Generation = c.Source.Generation + 1 } : c.Source,
            CapturedTick = mutation == "tick" ? game.State.Tick + 1 : c.CapturedTick,
            ControllerId = mutation == "controller" ? "P2" : c.ControllerId,
            Timing = mutation == "timing" ? "FORGED" : c.Timing, BattlefieldId = mutation == "battlefield" ? "UNKNOWN" : c.BattlefieldId };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { SourceUnitTokens = changed }] }),
            e => e.Contains("source unit token"));
    }
}
