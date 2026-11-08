using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AzirTokenAttackMovementTests
{
    [Fact]
    public async Task ASeparateDefendSkillKeepsItsConfirmedAllyAfterTheActualImageSourceCeases()
    {
        var initial = RadGearTestPosition.Put(Position("SFD·177/221"), "TEMPLATE", "VEN·020", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "COPY", "UNL-200/219", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "ALLY", "OGN·219/298", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "ATTACKER", "OGN·219/298", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "RUNE", "UNL-R06", "P1", "BASE");
        // Keep BF under friendly control so the setup move does not start a duel.
        initial = RadGearTestPosition.Put(initial, "HOLDER", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["TEMPLATE"]));
        await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        await game.Act("P1", new MoveUnitCommand(image.ObjectId, "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PlayCardCommand("ALLY", "OGN·219/298", []));
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new MoveUnitCommand("ATTACKER", "BASE", "BATTLEFIELD:BF", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("P1", choice.PlayerId);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("DEFEND", skill.UnitEvent!.Timing);
        if (game.State.PriorityPlayerId != "P1")
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P1", new TapRuneCommand("RUNE"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [image.ObjectId]));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(image.ObjectId));
        Assert.Equal(skill.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Top();
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        await game.ColdCheckpoint();
    }
}
