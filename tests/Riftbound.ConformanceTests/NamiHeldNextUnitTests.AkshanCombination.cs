using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class NamiHeldNextUnitTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnEarnedHoldAndAnActualAkshanTheftKeepBothOriginalHistoriesThroughHisDeparture(bool reverseOrder)
    {
        var initial = RadGearTestPosition.Put(Position(), "FIRST", "SFD·109/221", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "GEAR", "SFD·022/221", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        for (var i = 0; i < 2; i++)
            initial = RadGearTestPosition.Put(initial, "ORANGE-" + i, "UNL-R04", "P1", "BASE");
        var game = new Driver(initial);
        await EarnHold(game); await game.Top(); await Fund(game);
        await game.Act("P1", new RecycleRuneCommand("ORANGE-0"));
        await game.Act("P1", new RecycleRuneCommand("ORANGE-1"));
        await game.Act("P1", new PlayCardCommand("FIRST", "SFD·109/221", [], OptionalCosts: ["SPEND_POWER:2:ORANGE"]));
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        Assert.Single(game.State.TriggerQueue, trigger => trigger.TurnUnitPlayBoon?.Unit.ObjectId == "FIRST");
        Assert.Single(game.State.TriggerQueue, trigger => trigger.Tempered?.Unit.ObjectId == "FIRST");
        Assert.Contains(CommandTypes.OrderTriggers, game.PromptFor("P1").EnabledActions());
        var order = game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray();
        if (reverseOrder) Array.Reverse(order);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: order));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        Assert.Single(game.State.StackItems, item => item.TurnUnitPlayBoon?.Unit.ObjectId == "FIRST");
        Assert.Single(game.State.StackItems, item => item.SourceObjectId == "FIRST" && item.SourceConfirmed);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.False(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["FIRST"]));
        Assert.Equal("P1", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal("FIRST", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.NotNull(game.State.CardObjects["GEAR"].EquipmentControl);
        Assert.Single(CoreRuleEngine.UnitEventTurnPlayGrantUses(game.State));
        await game.ColdCheckpoint();

        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["FIRST"])); await game.Top();
        Assert.Contains("FIRST", game.State.PlayerZones["P1"].Hand);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Contains("GEAR", game.State.PlayerZones["P2"].Base);
        Assert.Null(game.State.CardObjects["GEAR"].EquipmentControl);
        await game.Act("P1", new PlayCardCommand("SECOND", Unit, []));
        Assert.True(game.State.CardObjects["SECOND"].IsExhausted);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["SECOND"]));
        Assert.Single(CoreRuleEngine.UnitEventTurnPlayGrantUses(game.State));
        await game.ColdCheckpoint();
    }
}
