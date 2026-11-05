using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class CithriaUnitPlayBoonTests
{
    private const string Cithria = "OGN·139/298", Follower = "OGN·219/298";

    [Theory]
    [InlineData("BASE")]
    [InlineData("BATTLEFIELD:BF")]
    public async Task AnotherFriendlyUnitCreatesARespondableBoonAndLaterBoonsDoNotStack(string destination)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Cithria, [], Destination: "BASE"));
        Assert.Empty(game.State.StackItems);
        Assert.Equal(1, game.State.CardObjects["UNIT"].Power);
        await game.Act("P1", new PlayCardCommand("FOLLOW", Follower, [], Destination: destination));
        Assert.Single(game.State.StackItems, item => item.SourceObjectId == "UNIT");
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["UNIT"].Power);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        await game.Act("P1", new PlayCardCommand("FOLLOW2", Follower, [], Destination: "BASE"));
        await game.Top();
        Assert.Equal(2, game.State.CardObjects["UNIT"].Power);
        Assert.Single(game.State.CardObjects["UNIT"].Tags, tag => tag == CardObjectTags.Boon);
        Assert.Single(game.Events(), e => e.Event.Kind == "BOON_GRANTED");
        await game.Replay();
    }

    [Fact]
    public async Task SpellsAndEnemyUnitsDoNotTriggerItAndTheBoonSurvivesTurnEnd()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "SPELL", "OGN·048/298", "P1");
        initial = HandDiscardObserverTests.Put(initial, "ENEMY-FOLLOW", Follower, "P2", "HAND");
        for (var i = 1; i <= 4; i++) initial = HandDiscardObserverTests.Put(initial, "RUNE-" + i, "UNL-R01", "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Cithria, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        await game.Act("P1", new PlayCardCommand("FOLLOW", Follower, [], Destination: "BASE"));
        await game.Top();
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(2, game.State.CardObjects["UNIT"].Power);
        await game.Act("P2", new TapRuneCommand("RUNE-1", ["RUNE-1", "RUNE-2", "RUNE-3", "RUNE-4"]));
        await game.Act("P2", new PlayCardCommand("ENEMY-FOLLOW", Follower, [], Destination: "BASE"));
        Assert.Empty(game.State.StackItems);
        Assert.Equal(2, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningCithriaInResponseDoesNotGiveItsNewHandGenerationABoon()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Cithria, [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("FOLLOW", Follower, [], Destination: "BASE"));
        if (game.State.PriorityPlayerId != "P1") await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top(); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        Assert.Equal(1, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    private static MatchState Position()
    {
        var state = RadUnitEffectTestFixture.Place(VenUnitEntryBasicTests.Position(Cithria),
            "ALLY", "P1", "BATTLEFIELD", "BF");
        state = HandDiscardObserverTests.Put(state, "FOLLOW", Follower, "P1", "HAND");
        return HandDiscardObserverTests.Put(state, "FOLLOW2", Follower, "P1", "HAND");
    }
}
