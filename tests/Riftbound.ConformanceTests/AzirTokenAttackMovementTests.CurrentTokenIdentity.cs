using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AzirTokenAttackMovementTests
{
    // The CN July 17 rules update supersedes the May judge FAQ: a copied
    // Image retains its intrinsic token identity (current CN185.1.a).
    [Theory, InlineData("SFD·177/221"), InlineData("SFD·177a/221")]
    public async Task AnActualImageCopiedToMightyPoroRemainsATokenAndCanBeMovedByAzir(string no)
    {
        const string poro = "OGN·210/298";
        var initial = RadGearTestPosition.Put(Position(no), "TEMPLATE", poro, "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "COPY", "UNL-200/219", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["TEMPLATE"]));
        await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values,
            card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal(poro, image.CardNo);
        Assert.True(TokenObjectRules.IsToken(image));
        Assert.Equal("BASE", game.State.ObjectLocations[image.ObjectId].Zone);
        await EnterAndAttack(game, no);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(new[] { image.ObjectId }, choice.LegalObjectIds);
        await Select(game, image.ObjectId);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations[image.ObjectId].BattlefieldObjectId);
        Assert.Equal(poro, game.State.CardObjects[image.ObjectId].CardNo);
        Assert.True(TokenObjectRules.IsToken(game.State.CardObjects[image.ObjectId]));
        await game.ColdCheckpoint();
    }
}
