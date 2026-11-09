using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

// CN 191.5.b / 204.1.c / 383.3.b / 415.4: paying the leading cost confirms a skill
// controlled independently of the source's later location or controller.
public sealed class RadTitanShrineSourceContinuityTests
{
    private const string Shrine = "RAD·089/167";
    private const string Ambush = "UNL-120/219";

    [Theory]
    [InlineData("P1", "OGN·104/298", "HAND")]
    [InlineData("P2", "OGN·104/298", "HAND")]
    [InlineData("P1", "SFD·163/221", "GRAVEYARD")]
    [InlineData("P2", "SFD·163/221", "GRAVEYARD")]
    public async Task ActualAnimatedSourceRecallOrDestructionPreservesThePaidDraw(
        string owner, string response, string destination)
    {
        // The source begins as an explicitly controlled printed equipment.
        // Actual Orianna conversion permits Reaction spells to return or destroy
        // the source while its paid skill is on the closed stack.
        var state = Put(Create(), "SHRINE", Shrine, owner, "BASE");
        state = RadUnitEffectTestFixture.Place(state, "SHRINE", "P1", "BASE");
        state = Put(state, "NEW", Ambush, "P1", "HAND");
        state = Put(state, "RESPONSE", response, "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
        var game = new Driver(WithOrianna(state));
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "SHRINE");
        await game.TapRunes("P1", ExtraMana);
        var orange = game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Orange);
        await game.Act("P1", new RecycleRuneCommand(ExtraOrange));
        Assert.Contains(ExtraOrange, game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(orange + 1, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Orange));
        var paid = await EnterDuelAndPay(game, "P1", "ALLY");
        Assert.Equal(orange, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Orange));

        await GivePriority(game, "P1");
        await game.Act("P1", new PlayCardCommand("RESPONSE", response,
            response == "OGN·104/298" ? ["SHRINE"] : ["SHRINE", "ALLY"]));
        await game.Top();

        var zones = game.State.PlayerZones[owner];
        Assert.Contains("SHRINE", destination == "HAND" ? zones.Hand : zones.Graveyard);
        Assert.True(game.State.CardObjects["SHRINE"].ObjectGeneration > paid.UnitEvent!.SourceGeneration);
        AssertCapturedSkill(game, paid);
        var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        var drawn = DrawnCards(game, "P1");
        var opponentDeck = game.State.PlayerZones["P2"].MainDeck.Count;
        await game.Top();
        AssertDraw(game, "P1", deck, drawn);
        Assert.Equal(opponentDeck, game.State.PlayerZones["P2"].MainDeck.Count);
        await game.Replay();
    }

    [Fact]
    public async Task ActualAkshanDepartureReturnsControlWithoutRedirectingThePaidDraw()
    {
        var state = Put(Create(), "SHRINE", Shrine, "P1", "BASE");
        state = Put(state, "AKSHAN", "SFD·109/221", "P2", "HAND");
        state = Put(state, "NEW", Ambush, "P2", "HAND");
        state = Put(state, "RECALL", "OGN·104/298", "P2", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("AKSHAN", "SFD·109/221", [],
            OptionalCosts: ["SPEND_POWER:2:ORANGE"]));
        var theft = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P2", new ChooseCardsCommand(theft.ChoiceId, theft.ChoiceWindow, ["SHRINE"]));
        await game.Top();
        Assert.Equal("P1", game.State.CardObjects["SHRINE"].OwnerId);
        Assert.Equal("P2", game.State.CardObjects["SHRINE"].ControllerId);
        Assert.Contains("SHRINE", game.State.PlayerZones["P2"].Base);

        var paid = await EnterDuelAndPay(game, "P2", "ENEMY");
        var deck = game.State.PlayerZones["P2"].MainDeck.Count;
        await GivePriority(game, "P2");
        await game.Act("P2", new PlayCardCommand("RECALL", "OGN·104/298", ["AKSHAN"]));
        await game.Top();
        Assert.Contains("AKSHAN", game.State.PlayerZones["P2"].Hand);
        Assert.Equal("P1", game.State.CardObjects["SHRINE"].ControllerId);
        AssertCapturedSkill(game, paid);
        await game.Top();
        AssertDraw(game, "P2", deck);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    private static async Task<StackItemState> EnterDuelAndPay(Driver game, string controller, string attacker)
    {
        await game.Act(controller, new MoveUnitCommand(attacker, "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.SpellDuelState.IsActive);
        await game.Act(controller, new PlayCardCommand("NEW", Ambush, [], Destination: "BATTLEFIELD:BF"));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(controller, choice.PlayerId);
        await game.Act(controller, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SHRINE"]));
        Assert.True(game.State.CardObjects["SHRINE"].IsExhausted);
        var paid = Assert.Single(game.State.StackItems);
        Assert.Equal(controller, paid.ControllerId);
        Assert.Equal("SHRINE", paid.SourceObjectId);
        Assert.NotNull(paid.TriggerCost);
        return paid;
    }

    private static async Task GivePriority(Driver game, string player)
    {
        if (game.State.PriorityPlayerId != player)
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(player, game.State.PriorityPlayerId);
    }

    private static void AssertCapturedSkill(Driver game, StackItemState paid)
    {
        var surviving = Assert.Single(game.State.StackItems);
        Assert.Equal(paid.StackItemId, surviving.StackItemId);
        Assert.Equal(paid.ControllerId, surviving.ControllerId);
        Assert.Equal(paid.UnitEvent, surviving.UnitEvent);
        Assert.Equal(paid.TriggerCost, surviving.TriggerCost);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [surviving with { UnitEvent = surviving.UnitEvent! with { CapturedTick = game.State.Tick + 1 } }] }));
    }

    private const string ExtraOrange = "SHRINE-ORANGE";
    private static readonly string[] ExtraMana = Enumerable.Range(0, 8).Select(i => "SHRINE-MANA-" + i).ToArray();

    private static MatchState WithOrianna(MatchState state)
    {
        state = Put(state, ExtraOrange, "UNL-R04", "P1", "BASE");
        foreach (var id in ExtraMana) state = Put(state, id, "UNL-R03", "P1", "BASE");
        return OriannaEquipmentConsumerFixture.WithResources(state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } });
    }

    private static int DrawnCards(Driver game, string controller)
        => game.State.PlayerZones[controller].Hand.Count(id => id.StartsWith(controller + "-D", StringComparison.Ordinal)
            || id.StartsWith("CONVERT-DECK-" + controller, StringComparison.Ordinal));

    private static void AssertDraw(Driver game, string controller, int previousDeckSize, int previousDrawnCards = 0)
    {
        Assert.Equal(previousDeckSize - 1, game.State.PlayerZones[controller].MainDeck.Count);
        Assert.Equal(previousDrawnCards + 1, DrawnCards(game, controller));
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Null(game.State.PendingPayment);
    }
}
