using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AzirTokenAttackMovementTests
{
    private const string Sand = "SFD·031/221";
    private static MatchState Position(string no)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "HOST", no, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ENEMY", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        for (var i = 0; i < 2; i++)
            state = RadGearTestPosition.Put(state, "MAKE-" + i, Sand, "P1", "HAND");
        return state;
    }
    private static async Task EnterAndAttack(Driver game, string no)
    {
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("HOST", no, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        Assert.Equal(mana - 5, game.State.RunePools["P1"].Mana);
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
    }
    [Theory]
    [InlineData("SFD·177/221", 0)] [InlineData("SFD·177/221", 1)] [InlineData("SFD·177/221", 2)]
    [InlineData("SFD·177a/221", 0)] [InlineData("SFD·177a/221", 1)] [InlineData("SFD·177a/221", 2)]
    public async Task ActualHastePlayAndAttackChooseAnySubsetOfActuallyCreatedFriendlyUnitTokens(string no, int count)
    {
        var game = new Driver(Position(no));
        for (var i = 0; i < 2; i++)
        {
            await game.Act("P1", new PlayCardCommand("MAKE-" + i, Sand, []));
            await game.Top();
        }
        var tokens = game.State.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.SandSoldier))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(2, tokens.Length);
        var exhausted = tokens.ToDictionary(id => id, id => game.State.CardObjects[id].IsExhausted);
        await EnterAndAttack(game, no);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(0, choice.RequiredCount); Assert.Equal(2, choice.MaxCount);
        Assert.Equal(tokens, choice.LegalObjectIds);
        var selected = tokens.Take(count).ToArray();
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
        Assert.All(tokens, id => Assert.Equal("BASE", game.State.ObjectLocations[id].Zone));
        if (count > 0) await game.Top();
        foreach (var id in tokens)
        {
            Assert.Equal(selected.Contains(id) ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations[id].Zone);
            if (selected.Contains(id)) Assert.Equal("OTHER", game.State.ObjectLocations[id].BattlefieldObjectId);
            Assert.Equal(exhausted[id], game.State.CardObjects[id].IsExhausted);
        }
        await game.ColdCheckpoint();
    }
    [Theory, InlineData("SFD·177/221"), InlineData("SFD·177a/221")]
    public async Task AnAttackWithNoLegalFriendlyTokenDoesNotCreateAnImpossibleChoice(string no)
    {
        var game = new Driver(Position(no)); await EnterAndAttack(game, no);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }
}
