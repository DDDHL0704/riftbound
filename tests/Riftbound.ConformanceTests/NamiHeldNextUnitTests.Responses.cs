using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class NamiHeldNextUnitTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task ARealReturnOfNamiBeforeOrAfterCreatingTheGrantKeepsTheEarnedDelayedSkill(bool beforeCreation)
    {
        var initial = RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await EarnHold(game);
        var capturedGeneration = game.State.CardObjects["HOST"].ObjectGeneration;
        if (!beforeCreation) await game.Top();
        await Fund(game, blue: true);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["HOST"].ObjectGeneration > capturedGeneration);
        if (beforeCreation)
        {
            Assert.Single(game.State.StackItems, item => item.UnitEvent?.CardNo == Nami);
            await game.Top();
        }
        await game.ColdCheckpoint();
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(capturedGeneration, skill.TurnUnitPlayBoon!.Grant.Source.Generation);
        var opponent = ResolutionResult.BuildSnapshots(game.State)["P2"];
        Assert.DoesNotContain(skill.TurnUnitPlayBoon.Grant.Marker,
            JsonSerializer.Serialize(opponent));
        await game.ColdCheckpoint();
        await game.Top();
        Assert.False(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["FIRST"]));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ReturningThePlayedUnitDoesNotRefundTheGrantOrAffectItsNextIncarnation()
    {
        var initial = RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await EarnHold(game); await game.Top(); await Fund(game, blue: true);
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        var generation = game.State.CardObjects["FIRST"].ObjectGeneration;
        Assert.NotNull(Assert.Single(game.State.StackItems).TurnUnitPlayBoon);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["FIRST"]));
        await game.Top();
        Assert.Contains("FIRST", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["FIRST"].ObjectGeneration > generation);
        await game.ColdCheckpoint();
        await game.Top();
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["FIRST"]));
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnUnusedGrantExpiresAndCannotAffectTheNextTurn()
    {
        var initial = RadGearTestPosition.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await EarnHold(game); await game.Top(); await Fund(game, blue: true);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        var created = game.Events().Count(e => e.Event.Kind == "UNIT_EVENT_TURN_PLAY_GRANT_CREATED");
        Assert.Equal(1, created);
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        Assert.Empty(game.State.StackItems);
        Assert.Equal(created, game.Events().Count(e => e.Event.Kind == "UNIT_EVENT_TURN_PLAY_GRANT_CREATED"));
        await Fund(game);
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["FIRST"]));
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }
}
