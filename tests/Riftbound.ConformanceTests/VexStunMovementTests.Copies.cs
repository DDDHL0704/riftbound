using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VexStunMovementTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task AnActuallyPlayedImageKeepsTheCapturedSkillWhenItsSourceTokenCeases(bool returnCopy)
    {
        const string copy = "COPY-TOKEN-001";
        var state = RadGearTestPosition.Put(Position(), "COPY", "UNL-200/219", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "COPY-RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["HOST"]));
        await game.Top(); await game.Top();
        Assert.Equal(Vex, game.State.CardObjects[copy].CardNo);
        Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, game.State.CardObjects[copy].TokenFactoryCardNo);
        for (var passes = 0; game.State.FocusPlayerId is { } player; passes++)
        {
            Assert.True(passes < 4); await game.Act(player, new PassFocusCommand());
        }
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        for (var passes = 0; game.State.FocusPlayerId is { } player; passes++)
        {
            Assert.True(passes < 4); await game.Act(player, new PassFocusCommand());
        }
        await StunEnemy(game);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(new[] { copy }, choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [copy]));
        if (returnCopy)
        {
            await game.Act("P1", new PlayCardCommand("COPY-RETURN", "OGN·104/298", [copy])); await game.Top();
            Assert.False(game.State.CardObjects.ContainsKey(copy));
            var captured = Assert.Single(game.State.StackItems).FieldContext!;
            Assert.True(captured.SourceCeasedToExist);
            Assert.NotNull(captured.StunObserver!.CeasedSource);
            await game.ColdCheckpoint();
        }
        await game.Top();
        Assert.Empty(game.State.StackItems);
        if (returnCopy) Assert.False(game.State.CardObjects.ContainsKey(copy));
        else Assert.Equal("OTHER", game.State.ObjectLocations[copy].BattlefieldObjectId);
        Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await game.ColdCheckpoint();
    }
}
