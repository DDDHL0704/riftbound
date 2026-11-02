using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadHeimerdingerTokenReplacementTests
{
    private static MatchState Position(string card = "UNL-069/219", bool zilean = false)
    {
        var s = OfficialTokenReplacementTests.Position(zilean ? 1 : 0, card);
        return s with { Tick = 0,
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["HEIMER"] = new("HEIMER", cardNo: "RAD·132/167", ownerId: "P1", controllerId: "P1",
                    power: 5, tags: [CardObjectTags.UnitCard, "约德尔人"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["HEIMER"] = new("P1", "BATTLEFIELD", "BFZ") },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Battlefields = [..s.PlayerZones["P1"].Battlefields, "HEIMER"] } }
        };
    }
    private static Task Choose(Driver g, params string[] ids)
    {
        var p = Assert.IsType<PendingCardChoiceState>(g.State.PendingCardChoice);
        return g.Act(p.PlayerId, new ChooseCardsCommand(p.ChoiceId, p.ChoiceWindow, ids));
    }
    private static CardObjectState[] Tokens(MatchState s) => OfficialTokenReplacementTests.Tokens(s);

    [Fact]
    public async Task OfficialHeroPaysSixAndYellowAndDoesNotReplaceFromBase()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == "RAD·132/167");
        Assert.Equal(39637L, card.Id); Assert.Equal("学院导师", card.SubTitle);
        var g = new Driver(VenUnitEntryBasicTests.Position(card.CardNo));
        await g.Act("P1", new PlayCardCommand("UNIT", card.CardNo, []));
        Assert.Equal(14, g.State.RunePools["P1"].Mana); Assert.Equal(19, g.State.RunePools["P1"].TotalPower);
        Assert.Equal(5, g.State.CardObjects["UNIT"].Power); Assert.True(g.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains("约德尔人", g.State.CardObjects["UNIT"].Tags); await g.Replay();
    }

    [Fact]
    public async Task EachTokenCanBeReplacedIndependentlyAndTheHeroHasNoTurnLimit()
    {
        var g = new Driver(Position());
        await g.Act("P1", new PlayCardCommand("C", "UNL-069/219", [])); await g.Top();
        Assert.Empty(Tokens(g.State)); await Choose(g, "HEIMER");
        Assert.Empty(Tokens(g.State)); Assert.Contains("HEIMER", g.State.PendingCardChoice!.LegalObjectIds); await Choose(g);
        var tokens = Tokens(g.State); Assert.Equal(2, tokens.Length);
        var robot = Assert.Single(tokens, t => t.TokenFactoryCardNo == "SFD·T01");
        Assert.Equal(3, robot.Power); Assert.Contains("机械", robot.Tags); Assert.DoesNotContain(CardObjectTags.Ephemeral, robot.Tags);
        Assert.Contains(robot.ObjectId, g.State.PlayerZones["P1"].Base); Assert.True(robot.IsExhausted);
        Assert.Single(tokens, t => t.Tags.Contains(CardObjectTags.Ephemeral));
        Assert.Empty(g.State.CardObjects["HEIMER"].UntilEndOfTurnEffects); await g.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task BothReplacementOrdersAccountForEveryAdditionalToken(bool robotFirst)
    {
        var g = new Driver(Position("SFD·076/221", true));
        await g.Act("P1", new PlayCardCommand("C", "SFD·076/221", [])); await g.Top();
        if (robotFirst) { await Choose(g, "HEIMER"); await Choose(g, "Z1"); }
        else { await Choose(g, "Z1"); await Choose(g, "HEIMER"); await Choose(g, "HEIMER"); }
        Assert.Null(g.State.PendingCardChoice); Assert.Equal(2, Tokens(g.State).Length);
        Assert.All(Tokens(g.State), t => Assert.Equal("SFD·T01", t.TokenFactoryCardNo)); await g.Replay();
    }

    [Fact]
    public async Task AnAdditionalTokenMayKeepItsOriginalFactoryAfterTheFirstBecomesARobot()
    {
        var g = new Driver(Position("UNL-200/219", true));
        await g.Act("P1", new PlayCardCommand("C", "UNL-200/219", ["TARGET"])); await g.Top();
        await Choose(g, "Z1"); await Choose(g, "HEIMER"); await Choose(g);
        Assert.Equal(2, Tokens(g.State).Length);
        Assert.Single(Tokens(g.State), t => t.TokenFactoryCardNo == "SFD·T01");
        Assert.Single(Tokens(g.State), t => t.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        await g.Top();
        Assert.All(Tokens(g.State), t => { Assert.Equal("SFD·068/221", t.CardNo); Assert.Equal(3, t.Power); Assert.Contains(CardObjectTags.Ephemeral, t.Tags); });
        await g.Replay();
    }

    [Theory]
    [InlineData("base")] [InlineData("hidden")] [InlineData("opponent")] [InlineData("unlocated")]
    public async Task OnlyAVisibleFriendlySourceAtARealBattlefieldCanOfferTheReplacement(string mode)
    {
        var s = Position();
        if (mode == "hidden") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["HEIMER"] = s.CardObjects["HEIMER"] with { IsFaceDown = true } } };
        if (mode == "opponent") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HEIMER"] = s.CardObjects["HEIMER"] with { ControllerId = "P2" }, ["BFZ"] = s.CardObjects["BFZ"] with { ControllerId = "P2" } },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Battlefields = ["BFZ"] },
                ["P2"] = s.PlayerZones["P2"] with { Battlefields = ["HEIMER"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["HEIMER"] = new("P2", "BATTLEFIELD", "BFZ") } };
        if (mode == "base") s = s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P1"] = s.PlayerZones["P1"] with { Base = [..s.PlayerZones["P1"].Base, "HEIMER"], Battlefields = s.PlayerZones["P1"].Battlefields.Where(id => id != "HEIMER").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["HEIMER"] = new("P1", "BASE") } };
        if (mode == "unlocated") s = s with { ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["HEIMER"] = new("P1", "BATTLEFIELD", "missing") } };
        if (mode == "unlocated")
        {
            var cast = await OfficialGraveyardRecastTests.Act(s, "P1", new PlayCardCommand("C", "UNL-069/219", []));
            var done = await OfficialGraveyardRecastTests.Top(cast.State);
            Assert.Null(done.State.PendingCardChoice); Assert.Equal(2, Tokens(done.State).Length); return;
        }
        var g = new Driver(s); await g.Act("P1", new PlayCardCommand("C", "UNL-069/219", [])); await g.Top();
        Assert.Null(g.State.PendingCardChoice); Assert.Equal(2, Tokens(g.State).Length);
    }

    [Fact]
    public async Task AHereTokenReplacementKeepsItsOriginalBattlefield()
    {
        var s = VenUnitEntryBasicTests.Position("RAD·049/167");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HEIMER"] = new("HEIMER", cardNo: "RAD·132/167", ownerId: "P1", controllerId: "P1", power: 5, tags: [CardObjectTags.UnitCard, "约德尔人"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["HEIMER"] = new("P1", "BATTLEFIELD", "BF") },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Battlefields = [..s.PlayerZones["P1"].Battlefields, "HEIMER"] } } };
        var g = new Driver(s);
        await g.Act("P1", new PlayCardCommand("UNIT", "RAD·049/167", [], Destination: "BATTLEFIELD:BF"));
        await g.Top(); await Choose(g, "HEIMER");
        var robot = Assert.Single(Tokens(g.State)); Assert.Equal("SFD·T01", robot.TokenFactoryCardNo);
        Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF"), g.State.ObjectLocations[robot.ObjectId]); await g.Replay();
    }

    [Fact]
    public async Task ReplacementChoiceCannotBeStolenAndRecoveryRejectsForgedFactories()
    {
        var g = new Driver(Position(zilean: true));
        await g.Act("P1", new PlayCardCommand("C", "UNL-069/219", [])); await g.Top();
        var p = g.State.PendingCardChoice!; var hash = MatchStateHasher.Hash(g.State);
        var r = await new CoreRuleEngine().ResolveAsync(g.State, new("steal", "P2", CommandTypes.ChooseCards),
            new ChooseCardsCommand(p.ChoiceId, p.ChoiceWindow, ["HEIMER"]), default);
        Assert.False(r.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(r.State));
        await Choose(g, "HEIMER"); var parent = g.State.StackItems.Single(); var plan = parent.TokenEntryPlan!;
        foreach (var app in new[] { plan.Applied[0] with { FactoryCardNo = P6TokenFactoryCatalog.FaerieTokenCardNo },
            plan.Applied[0] with { Source = new("HEIMER", 999) }, plan.Applied[0] with { TokenIndex = 999 } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(g.State with { StackItems = [parent with { TokenEntryPlan = plan with { Applied = [app] } }] }));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(g.State with {
            StackItems = [parent with { TokenEntryPlan = plan with { Applied = [null!] } }] }));
        for (var i = 0; g.State.PendingCardChoice is not null; i++) { Assert.True(i < 6); await Choose(g); }
        await g.Replay();
    }
}
