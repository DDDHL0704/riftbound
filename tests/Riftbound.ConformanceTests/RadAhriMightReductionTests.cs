using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadAhriMightReductionTests
{
    private static MatchState Position(string no = "RAD·038/167")
    {
        var s = RadDisarmUnitTests.Board(no);
        s = LocalDestructionRecallTests.At(s, "F", "P2", "BATTLEFIELD", "F");
        var cards = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["HOST"] = s.CardObjects["HOST"] with { Power = 4 },
            ["ENEMY2"] = s.CardObjects["ENEMY2"] with { Tags = [..s.CardObjects["ENEMY2"].Tags, "法盾2"] } };
        var locations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations);
        var deck = Enumerable.Range(1, 4).Select(i => "P2-DRAW-" + i).ToArray();
        foreach (var id in deck) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·219/298", "P2");
            locations[id] = new("P2", "MAIN_DECK");
        }
        return s with { Tick = 0, CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P2"] = s.PlayerZones["P2"] with { MainDeck = deck } } };
    }

    private static async Task OpenBattle(Driver g)
    {
        await g.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = g.State.PendingCardChoice!;
        await g.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY2"]));
        var payment = g.State.PendingPayment!;
        await g.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        Assert.Empty(g.State.StackItems); Assert.Empty(g.State.TriggerQueue);
        Assert.True(g.State.SpellDuelState.IsActive);
    }

    private static async Task TakeFocus(Driver g, string actor)
    {
        if (g.State.FocusPlayerId is { } other && other != actor)
            await g.Act(other, new PassFocusCommand());
    }

    [Theory]
    [InlineData("RAD·038/167", 40107L)] [InlineData("RAD·SP1/005", 40296L)]
    public async Task BothPrintingsPayFourManaAndOneGreenAndEnterExhausted(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("自信闪耀", card.SubTitle);
        var g = new Driver(VenUnitEntryBasicTests.Position(no));
        await g.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(16, g.State.RunePools["P1"].Mana); Assert.Equal(19, g.State.RunePools["P1"].TotalPower);
        Assert.Equal(4, g.State.CardObjects["UNIT"].Power); Assert.True(g.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains("缴械", g.State.CardObjects["UNIT"].Tags); await g.Replay();
    }

    [Theory]
    [InlineData("RAD·038/167")] [InlineData("RAD·SP1/005")]
    public async Task PrintedDisarmMakesItsOwnSeparateMightTrigger(string no)
    {
        var s = RadDisarmUnitTests.Board(no); s = s with { Tick = 0,
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["HOST"] = s.CardObjects["HOST"] with { Power = 4 } } };
        var g = new Driver(s); var power = s.CardObjects["ENEMY1"].Power;
        await g.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var choice = g.State.PendingCardChoice!;
        await g.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"])); await g.Top();
        Assert.Equal(power - 1, g.State.CardObjects["ENEMY1"].Power);
        Assert.Equal(4, g.State.CardObjects["HOST"].Power); Assert.Single(g.State.StackItems);
        await g.Top(); Assert.Equal(5, g.State.CardObjects["HOST"].Power); await g.Replay();
    }

    [Theory]
    [InlineData("local", true)] [InlineData("other-location", false)] [InlineData("base", false)]
    [InlineData("opponent", false)] [InlineData("friendly", false)] [InlineData("zero", false)]
    public async Task OnlyANonzeroReductionByYouOnALocalEnemyTriggers(string branch, bool expected)
    {
        var s = Position(); var actor = branch == "opponent" ? "P2" : "P1";
        if (branch == "base") s = LocalDestructionRecallTests.At(s, "HOST", "P1", "BASE");
        if (branch == "other-location") s = LocalDestructionRecallTests.At(s, "ENEMY1", "P2", "BASE");
        if (branch == "friendly") s = LocalDestructionRecallTests.At(s, "ENEMY1", "P1", "BATTLEFIELD", "F");
        if (branch == "zero") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ENEMY1"] = s.CardObjects["ENEMY1"] with { Power = 1 } } };
        s = AttackDamageDistributionTests.Spell(s, "SPELL", "OGN·095/298", actor);
        var g = new Driver(s);
        if (branch != "base") await OpenBattle(g);
        await TakeFocus(g, actor);
        await g.Act(actor, new PlayCardCommand("SPELL", "OGN·095/298", ["ENEMY1"])); await g.Top();
        Assert.Equal(expected ? 1 : 0, g.State.StackItems.Count);
        if (expected) { await g.Top(); Assert.Equal(5, g.State.CardObjects["HOST"].Power); }
        else Assert.Equal(4, g.State.CardObjects["HOST"].Power);
        await g.Replay();
    }

    [Theory]
    [InlineData(1, true)] [InlineData(3, false)] [InlineData(12, false)]
    public async Task OfficialFaqObservesNewPassiveReductionEvenWhenFinalMightIsUnchanged(int original, bool expected)
    {
        var s = Position();
        var leona = OfficialGarenConquestBattlefieldCountTests.Printed("LEONA", "OGN·079/298", "P1");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["LEONA"] = leona, ["ENEMY1"] = s.CardObjects["ENEMY1"] with { Power = original, UntilEndOfTurnEffects = ["STUNNED"] } },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Battlefields = [..s.PlayerZones["P1"].Battlefields, "LEONA"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["LEONA"] = new("P1", "BATTLEFIELD", "F") } };
        s = AttackDamageDistributionTests.Spell(s, "SPELL", "OGN·058/298", "P2");
        var g = new Driver(s); await OpenBattle(g); await TakeFocus(g, "P2");
        await g.Act("P2", new PlayCardCommand("SPELL", "OGN·058/298", ["ENEMY1"])); await g.Top();
        Assert.Equal(expected ? 1 : 0, g.State.StackItems.Count);
        if (expected) {
            Assert.Equal(1, CurrentPowerTargetTests.DisplayPower(g.State, "ENEMY1"));
            Assert.NotNull(Assert.Single(g.State.StackItems).UnitEvent!.MightReduction!.AuraSource);
            await g.Top(); Assert.Equal(5, g.State.CardObjects["HOST"].Power);
        }
        await g.Replay();
    }

    [Fact]
    public async Task SourceReturningInResponseDoesNotGainMightAndRecoveryRejectsForgedFacts()
    {
        var s = AttackDamageDistributionTests.Spell(Position(), "SPELL", "OGN·095/298", "P1");
        s = AttackDamageDistributionTests.Spell(s, "RETURN", "OGN·104/298", "P1");
        var g = new Driver(s); await OpenBattle(g);
        await g.Act("P1", new PlayCardCommand("SPELL", "OGN·095/298", ["ENEMY1"])); await g.Top();
        var item = Assert.Single(g.State.StackItems); var context = item.UnitEvent!; var fact = context.MightReduction!;
        foreach (var forged in new[] { fact with { Actor = "P2" }, fact with { Target = new("ENEMY2", 999) },
            fact with { Target = new("HOST", 0) }, fact with { After = fact.Before }, fact with { AuraIndex = 0 } })
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(g.State with {
                StackItems = [item with { UnitEvent = context with { MightReduction = forged } }] }));
        await g.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await g.Top(); await g.Top();
        Assert.Contains("HOST", g.State.PlayerZones["P1"].Hand); Assert.Equal(4, g.State.CardObjects["HOST"].Power); await g.Replay();
    }
}
