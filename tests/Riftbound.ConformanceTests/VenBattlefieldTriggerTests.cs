using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenBattlefieldTriggerTests
{
    [Theory]
    [InlineData(0, true)] [InlineData(4, true)] [InlineData(4, false)] [InlineData(5, true)]
    public async Task BuriedSandsCapturesTheRuneCeilingAndDrawsOnlyAfterPaidResponse(int runes, bool pay)
    {
        var initial = Field("VEN·162");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = initial.CardObjects["BF"] with { ControllerId = null } } };
        for (var i = 0; i < runes; i++) initial = HandDiscardObserverTests.Put(initial, "R" + i, "UNL-R01", "P1", "BASE");
        initial = HandDiscardObserverTests.Put(initial, "OPPONENT-RUNE", "UNL-R01", "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        if (runes > 4) {
            Assert.Null(game.State.PendingPayment); Assert.Empty(game.State.StackItems);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "BATTLEFIELD_TRIGGER_RESOLVED");
        } else {
            var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
            var item = Assert.Single(game.State.StackItems); Assert.Equal(TriggerKinds.BattlefieldConquerLowRunesPayDraw, item.EffectKind);
            Assert.Empty(item.BattlefieldConquestMight!.PowerfulUnits);
            await Reject(game.State, "P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
            Assert.Equal(pay ? 19 : 20, game.State.RunePools["P1"].Mana); Assert.Empty(game.State.PlayerZones["P1"].Hand);
            if (pay) { Assert.Single(game.State.StackItems); await game.Top(); Assert.Single(game.State.PlayerZones["P1"].Hand); }
            else Assert.Empty(game.State.StackItems);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ShadowTempleBurnsBeforeNormalDrawAndUsesBurnoutWhenNecessary(bool burnout)
    {
        var initial = Hold();
        if (burnout) {
            var removed = initial.PlayerZones["P1"].MainDeck.Skip(2).ToHashSet(StringComparer.Ordinal);
            initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { MainDeck = initial.PlayerZones["P1"].MainDeck.Take(2).ToArray() } },
                CardObjects = initial.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary(),
                ObjectLocations = initial.ObjectLocations.Where(p => !removed.Contains(p.Key)).ToDictionary() };
        }
        var game = new Driver(initial); await game.Act("P1", new PassPriorityCommand());
        Assert.Equal("BURN", Assert.Single(game.State.StackItems).HeldContext!.Kind);
        Assert.Empty(game.State.PlayerZones["P1"].Graveyard); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.Single(game.State.PlayerZones["P1"].Hand);
        Assert.Equal(3, game.Events().Where(e => e.Event.Kind == "CARDS_BURNED").Sum(e => Convert.ToInt32(e.Event.Payload["count"])));
        Assert.Single(game.Events(), e => e.Event.Kind == "CARD_DRAWN");
        Assert.Equal(burnout ? 1 : 0, game.State.PlayerScores["P2"]);
        if (!burnout) {
            Assert.Equal(new[] { "D0", "D1", "D2" }, game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(new[] { "D3" }, game.State.PlayerZones["P1"].Hand);
        }
        await game.Replay();
    }

    [Fact]
    public async Task ShadowTempleDoesNotBurnForTheOtherPlayersHold()
    {
        var initial = Hold(); initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = initial.CardObjects["F"] with { ControllerId = "P2" } } };
        var game = new Driver(initial); await game.Act("P1", new PassPriorityCommand());
        Assert.Equal(MatchPhases.Main, game.State.Phase); Assert.Single(game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARDS_BURNED"); await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task DeathsDoorGainsManaForBothSidesImmediatelyOnlyAtItsBattleStart(bool here)
    {
        var initial = Field(here ? "VEN·166" : "OGN·278/298");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BF"] = initial.CardObjects["BF"] with { ControllerId = "P2" } } };
        if (!here) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER"] = OfficialGarenConquestBattlefieldCountTests.Printed("OTHER", "VEN·166", "P2") with { ControllerId = "P2" } } };
        var game = new Driver(initial); await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        Assert.Equal("P1", game.State.FocusPlayerId); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Equal(here ? 21 : 20, game.State.RunePools["P1"].Mana); Assert.Equal(here ? 21 : 20, game.State.RunePools["P2"].Mana);
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        await game.Act("P1", new DeclareBattleCommand("BF", ["ALLY"], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
        Assert.Equal(here ? 2 : 0, game.Events().Count(e => e.Event.Kind == "MANA_GAINED"));
        await game.Replay();
    }

    private static MatchState Field(string no)
    {
        var initial = VenUnitEntryBasicTests.Position("OGN·004/298");
        var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        cards.Remove("UNIT"); locations.Remove("UNIT");
        cards["BF"] = OfficialGarenConquestBattlefieldCountTests.Printed("BF", no, "P1") with { ControllerId = "P1" };
        return initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Hand = [] } } };
    }
    private static MatchState Hold()
    {
        var initial = OfficialHoldSequenceTests.State("VEN·165");
        return initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = OfficialGarenConquestBattlefieldCountTests.Printed("F", "VEN·165", "P1") with { ControllerId = "P1" } } };
    }
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-battlefield-trigger", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
