using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class NamiHeldNextUnitTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ARealFaceDownPlayIndependentlyConsumesNamisHoldAndEarnsEvelynnsOwnTurnMovement(bool reverseOrder)
    {
        const string evelynn = "UNL-141/219";
        var initial = RadGearTestPosition.Put(Position(), "FIRST", evelynn, "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "ENEMY", "OGN·096/298", "P2", "BASE");
        var game = new Driver(initial);
        await EarnHold(game); await game.Top(); await Fund(game, blue: true);
        await game.Act("P1", new HideCardCommand("FIRST", evelynn, "BATTLEFIELD:F", ["STANDBY_A"]));
        Assert.True(game.State.CardObjects["FIRST"].IsFaceDown);
        Assert.Empty(CoreRuleEngine.UnitEventTurnPlayGrantUses(game.State));
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", game.State.TurnPlayerId);
        Assert.Equal(2, game.State.PlayerScores["P1"]);
        Assert.Single(game.State.StackItems, item => item.UnitEvent?.CardNo == Nami);
        await game.Top();
        await game.Act("P1", new PlayCardCommand("FIRST", evelynn, []));
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["FIRST"]));
        Assert.Equal(2, game.State.TriggerQueue.Count);
        Assert.Single(game.State.TriggerQueue, item => item.TurnUnitPlayBoon?.Unit.ObjectId == "FIRST");
        Assert.Single(game.State.TriggerQueue, item => item.UnitEvent?.CardNo == evelynn);
        Assert.Contains(CommandTypes.OrderTriggers, game.PromptFor("P1").EnabledActions());
        var order = game.State.TriggerQueue.Select(item => item.TriggerId).ToArray();
        if (reverseOrder) Array.Reverse(order);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: order));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        Assert.Single(game.State.StackItems, item => item.TurnUnitPlayBoon?.Unit.ObjectId == "FIRST");
        var movement = Assert.Single(game.State.StackItems, item => item.UnitEvent?.CardNo == evelynn);
        var origin = movement.UnitEvent!.SourcePlayCondition!;
        Assert.Equal("P1", origin.TurnPlayerId); Assert.Equal("P1", origin.PlayingPlayerId);
        Assert.Equal("F", origin.FromStandby!.Battlefield.ObjectId);
        Assert.Equal("BASE", game.State.ObjectLocations["ENEMY"].Zone);
        await game.ColdCheckpoint();
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.False(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["FIRST"]));
        Assert.Equal("F", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Equal("P2", game.State.CardObjects["ENEMY"].ControllerId);
        var use = Assert.Single(CoreRuleEngine.UnitEventTurnPlayGrantUses(game.State));
        Assert.Equal("HOST", use.Grant.Source.ObjectId); Assert.Equal("FIRST", use.Unit.ObjectId);
        await game.ColdCheckpoint();
    }
}
