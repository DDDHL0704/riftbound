using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AzirTokenAttackMovementTests
{
    // CN355.9 / 359.3.e.6: a movement prohibition negates the instruction;
    // it does not add a targeting restriction to "your token units".
    [Theory, InlineData("SFD·177/221"), InlineData("SFD·177a/221")]
    public async Task AnActualVexMovementRestrictionDoesNotRemoveFriendlyTokensFromTheTargetChoices(string no)
    {
        var initial = RadGearTestPosition.Put(Position(no), "HOST", no, "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "ENEMY", "UNL-150/219", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        for (var i = 0; i < 2; i++)
        {
            await game.Act("P1", new PlayCardCommand("MAKE-" + i, Sand, []));
            await game.Top();
            while (game.State.StackItems.Count > 0) await game.Top();
        }
        var tokens = game.State.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.SandSoldier))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(2, tokens.Length);
        Assert.All(tokens, id => Assert.False(MovementRestrictionRules.CanMove(game.State.CardObjects[id], "P1")));
        Assert.True(MovementRestrictionRules.CanMove(game.State.CardObjects["HOST"], "P1"));
        Assert.False(game.State.CardObjects["HOST"].IsExhausted);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.True(game.State.CardObjects["HOST"].IsAttacking);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(tokens, choice.LegalObjectIds);
        await Select(game, tokens);
        var wallet = MatchStateHasher.HashValue(game.State.RunePools);
        var prefix = game.Events().Length;
        await game.Top();
        Assert.All(tokens, id => Assert.Equal("BASE", game.State.ObjectLocations[id].Zone));
        Assert.Equal(wallet, MatchStateHasher.HashValue(game.State.RunePools));
        Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnActualAzirImageMayTargetItselfEvenThoughItIsAlreadyAtTheDestination()
    {
        var initial = RadGearTestPosition.Put(Position("SFD·177/221"), "TEMPLATE", "SFD·177/221", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "COPY", "UNL-200/219", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["TEMPLATE"]));
        await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.True(TokenObjectRules.IsToken(image));
        await game.Act("P1", new MoveUnitCommand(image.ObjectId, "BASE", "BATTLEFIELD:OTHER", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(new[] { image.ObjectId }, choice.LegalObjectIds);
        await Select(game, image.ObjectId);
        Assert.Equal(new[] { image.ObjectId }, Assert.Single(game.State.StackItems).TargetObjectIds);
        var at = game.State.ObjectLocations[image.ObjectId];
        var exhausted = game.State.CardObjects[image.ObjectId].IsExhausted;
        var prefix = game.Events().Length;
        await game.Top();
        Assert.Equal(at, game.State.ObjectLocations[image.ObjectId]);
        Assert.Equal(exhausted, game.State.CardObjects[image.ObjectId].IsExhausted);
        Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
        await game.ColdCheckpoint();
    }
}
