using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadRadiantAltarSourceContinuityTests
{
    [Fact]
    public async Task ActualDestructionPreservesTheConfirmedBuffDrawAndDormantRuneCall()
    {
        // The printed equipment is fixture setup; conversion, payment,
        // confirmation, destruction and both earned skills use real commands.
        var state = Put(Put(Create(), "ALTAR", "RAD·046/167", "P1", "BASE"),
            "DESTROY", "SFD·163/221", "P1", "HAND");
        var game = new Driver(WithOrianna(state));
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "ALTAR");
        var mana = game.State.RunePools["P1"].Mana;
        var power = game.State.RunePools["P1"].TotalPower;
        var deck = game.State.PlayerZones["P1"].MainDeck.Count;
        var runes = game.State.PlayerZones["P1"].RuneDeck.ToArray();
        Assert.NotEmpty(runes);
        var sourcePower = game.State.CardObjects["ALTAR"].Power;
        var allyPower = game.State.CardObjects["ALLY"].Power;
        await game.Act("P1", new ActivateAbilityCommand("ALTAR", "RAD_046_EXHAUST_DRAW_CALL_RUNE", []));
        Assert.True(game.State.CardObjects["ALTAR"].IsExhausted);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var buff = Assert.Single(game.State.StackItems, item => item.UnitEvent is not null);
        var activation = Assert.Single(game.State.StackItems, item => item.SimpleActivation is not null);
        Assert.Equal("BECAME_EXHAUSTED", buff.UnitEvent!.Timing);
        if (game.State.PriorityPlayerId != "P1")
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("DESTROY", "SFD·163/221", ["ALTAR", "ALLY"]));
        await game.Top();

        Assert.Contains("ALTAR", game.State.PlayerZones["P1"].Graveyard);
        Assert.True(game.State.CardObjects["ALTAR"].ObjectGeneration > buff.UnitEvent.SourceGeneration);
        Assert.Equal(allyPower + sourcePower, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(deck - 1, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(runes, game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(buff, Assert.Single(game.State.StackItems, item => item.StackItemId == buff.StackItemId));
        Assert.Equal(activation, Assert.Single(game.State.StackItems, item => item.StackItemId == activation.StackItemId));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = game.State.StackItems.Select(item => item.StackItemId == activation.StackItemId
                ? item with { SimpleActivation = item.SimpleActivation! with { AcquiredTick = game.State.Tick + 1 } }
                : item).ToArray() }));
        await game.Top();
        Assert.Equal(allyPower + sourcePower + 3, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(deck - 1, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(runes, game.State.PlayerZones["P1"].RuneDeck);
        await game.Top();
        Assert.Equal(deck - 2, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(runes.Skip(1), game.State.PlayerZones["P1"].RuneDeck);
        Assert.Contains(runes[0], game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects[runes[0]].IsExhausted);
        Assert.Equal(mana - 2, game.State.RunePools["P1"].Mana);
        Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.PendingPayment);
        await game.Replay();
    }

    private static MatchState WithOrianna(MatchState state)
        => OriannaEquipmentConsumerFixture.WithResources(state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } });
}
