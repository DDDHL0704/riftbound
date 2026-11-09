using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

internal static class UnitPlayObserverTestPosition
{
    internal static MatchState Create(string cardNo, string controller = "P1")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "OBSERVER", cardNo, "P2", "BASE");
        // Explicit initial controller/owner boundary, without fabricated history.
        state = RadUnitEffectTestFixture.Place(state, "OBSERVER", controller, "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OBSERVER"] = state.CardObjects["OBSERVER"] with { ControllerId = controller } } };
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "UNIT", "VEN·123", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "BASIC", "OGN·219/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "BURST", "UNL-069/219", "P1", "HAND");
        return RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
    }

    internal static async Task Duel(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.True(game.State.SpellDuelState.IsActive);
    }

    internal static Task Ambush(Driver game)
        => game.Act("P1", new PlayCardCommand("UNIT", "VEN·123", [], Destination: "BATTLEFIELD:OTHER"));

    internal static Task Order(Driver game)
        => game.State.TriggerQueue.Count == 0 ? Task.CompletedTask : game.Act("P1",
            new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));

    internal static Task PayRose(Driver game, bool accept = true)
    {
        var cost = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", cost.PaymentWindow);
        Assert.Equal(1, cost.ManaCost); Assert.Equal(0, cost.PowerCost);
        return game.Act("P1", new PayCostCommand(cost.PaymentId, cost.PaymentWindow, [accept ? "PAY" : "DECLINE"]));
    }

    internal static Task ExhaustBeans(Driver game, bool accept = true)
    {
        var cost = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", cost.ChoiceWindow);
        return game.Act("P1", new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow,
            accept ? [cost.SourceObjectId!] : []));
    }
}

public sealed class MagicBeansPlayObserverTests
{
    private const string Beans = "UNL-011/219";

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealDuelUnitPlayConfirmsTheOptionalExhaustBeforeARespondableDraw(bool accept)
    {
        var game = new Driver(UnitPlayObserverTestPosition.Create(Beans));
        await UnitPlayObserverTestPosition.Duel(game); await UnitPlayObserverTestPosition.Ambush(game);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        Assert.False(game.State.CardObjects["OBSERVER"].IsExhausted);
        Assert.True(Assert.Single(game.State.StackItems).UnitEvent!.PlayedUnitDuringSpellDuel);
        await UnitPlayObserverTestPosition.ExhaustBeans(game, accept);
        Assert.Equal(accept, game.State.CardObjects["OBSERVER"].IsExhausted);
        Assert.Equal(hand, game.State.PlayerZones["P1"].Hand.Count);
        if (accept) await game.Top();
        Assert.Equal(hand + (accept ? 1 : 0), game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(deck - (accept ? 1 : 0), game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MainPhasePlaysAndADuelStartedByThatPlayDoNotRetroactivelyTriggerTheBeans(bool startsDuel)
    {
        var initial = UnitPlayObserverTestPosition.Create(Beans);
        if (startsDuel) initial = RadGearTestPosition.Put(initial, "RENGAR", "UNL-120/219", "P1", "HAND");
        var game = new Driver(initial); Assert.False(game.State.SpellDuelState.IsActive);
        await game.Act("P1", new PlayCardCommand(startsDuel ? "RENGAR" : "BASIC",
            startsDuel ? "UNL-120/219" : "OGN·219/298", [], Destination: startsDuel ? "BATTLEFIELD:OTHER" : "BASE"));
        Assert.Equal(startsDuel, game.State.SpellDuelState.IsActive);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.False(game.State.CardObjects["OBSERVER"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task AnAlreadyExhaustedEquipmentCannotPayForTheDraw()
    {
        var initial = UnitPlayObserverTestPosition.Create(Beans);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OBSERVER"] = initial.CardObjects["OBSERVER"] with { IsExhausted = true } } };
        var game = new Driver(initial); await UnitPlayObserverTestPosition.Duel(game);
        var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        await UnitPlayObserverTestPosition.Ambush(game);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal(deck, game.State.PlayerZones["P1"].MainDeck.Count); await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task TheCurrentControllerObservesItsOwnUnitRatherThanItsPhysicalOwnersUnit(string controller)
    {
        var game = new Driver(UnitPlayObserverTestPosition.Create(Beans, controller));
        await UnitPlayObserverTestPosition.Duel(game); await UnitPlayObserverTestPosition.Ambush(game);
        if (controller == "P1")
        {
            Assert.Equal("P1", game.State.PendingCardChoice!.PlayerId);
            await UnitPlayObserverTestPosition.ExhaustBeans(game); await game.Top();
            Assert.True(game.State.CardObjects["OBSERVER"].IsExhausted);
        }
        else { Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); }
        await game.Replay();
    }

    [Fact]
    public async Task ConfirmationRejectsTheOtherPlayerAndRecoveryRejectsAChangedDuelFact()
    {
        var game = new Driver(UnitPlayObserverTestPosition.Create(Beans));
        await UnitPlayObserverTestPosition.Duel(game); await UnitPlayObserverTestPosition.Ambush(game);
        var choice = game.State.PendingCardChoice!;
        await RadGearTestPosition.Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OBSERVER"]));
        var skill = Assert.Single(game.State.StackItems);
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with {
            StackItems = [skill with { UnitEvent = skill.UnitEvent! with { PlayedUnitDuringSpellDuel = false } }] }));
        await UnitPlayObserverTestPosition.ExhaustBeans(game); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task TwoEquipmentInstancesPaySeparatelyForTwoIndependentDraws()
    {
        var initial = RadGearTestPosition.Put(UnitPlayObserverTestPosition.Create(Beans), "SECOND-BEANS", Beans, "P1", "BASE");
        var game = new Driver(initial); await UnitPlayObserverTestPosition.Duel(game);
        await UnitPlayObserverTestPosition.Ambush(game); await UnitPlayObserverTestPosition.Order(game);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        for (var i = 0; i < 2; i++) await UnitPlayObserverTestPosition.ExhaustBeans(game);
        Assert.Equal(hand, game.State.PlayerZones["P1"].Hand.Count);
        for (var i = 0; i < 2; i++) await game.Top();
        Assert.Equal(hand + 2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.True(game.State.CardObjects["OBSERVER"].IsExhausted);
        Assert.True(game.State.CardObjects["SECOND-BEANS"].IsExhausted); await game.Replay();
    }
    [Theory]
    [InlineData("erase")] [InlineData("tick")]
    public async Task AcceptedPlayCaptureCannotBeErasedOrRetimedAfterPayment(string mutation)
    {
        var game = new Driver(UnitPlayObserverTestPosition.Create(Beans));
        await UnitPlayObserverTestPosition.Duel(game); await UnitPlayObserverTestPosition.Ambush(game);
        await UnitPlayObserverTestPosition.ExhaustBeans(game);
        var skill = Assert.Single(game.State.StackItems);
        var changed = mutation == "erase" ? game.State with { StackItems = [] }
            : game.State with { StackItems = [skill with { UnitEvent = skill.UnitEvent! with {
                CapturedTick = skill.UnitEvent.CapturedTick + 1 } }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, changed));
        await game.Top(); await game.Replay();
    }

}
