using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class TurnHistoryCostReductionTests
{
    private const string Spoils = "OGN·144/298", Ram = "SFD·012/221";
    private static MatchState Position(string no)
        => VenUnitEntryBasicTests.Position(no) with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(40, 40), ["P2"] = new(40, 40) } };

    [Theory]
    [InlineData(true, "P1")] [InlineData(true, "P2")]
    [InlineData(false, "P1")] [InlineData(false, "P2")]
    public async Task SpoilsUsesControlAtActualDestructionInsteadOfOwnership(bool enemy, string owner)
    {
        var victim = enemy ? "ENEMY" : "ALLY";
        var initial = RadGearTestPosition.Put(Position(Spoils), "KILL", "OGN·229/298", "P1", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [victim] = initial.CardObjects[victim] with { OwnerId = owner } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", [victim]));
        await game.Top();
        Assert.Equal(1, game.State.DeathLedger.Counts[enemy ? "P2" : "P1"]);
        Assert.Contains(victim, game.State.PlayerZones[owner].Graveyard);
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("UNIT", Spoils, []));
        Assert.Equal(mana - (enemy ? 2 : 4), game.State.RunePools["P1"].Mana);
        Assert.Equal(37, game.State.RunePools["P1"].TotalPower);
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await game.Top();
        Assert.Equal(hand + 2, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Fact]
    public async Task ReturningAnEnemyUnitDoesNotEarnTheDestructionDiscount()
    {
        var initial = RadGearTestPosition.Put(Position(Spoils), "RETURN", "OGN·172/298", "P1", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·172/298", ["ENEMY"]));
        await game.Top();
        Assert.Empty(game.State.DeathLedger.Counts);
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("UNIT", Spoils, []));
        Assert.Equal(mana - 4, game.State.RunePools["P1"].Mana);
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task SpoilsNoLongerCountsDeathsFromThePreviousTurn()
    {
        var initial = RadGearTestPosition.Put(Position(Spoils), "KILL", "OGN·229/298", "P1", "HAND");
        initial = WithRunes(initial, 6);
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("KILL", "OGN·229/298", ["ENEMY"]));
        await game.Top();
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        Assert.Empty(game.State.DeathLedger.Counts);
        await game.Act("P1", new RecycleRuneCommand("MANA-1"));
        await game.TapRunes("P1", Enumerable.Range(2, 5).Select(i => "MANA-" + i));
        await game.Act("P1", new PlayCardCommand("UNIT", Spoils, []));
        Assert.Equal(1, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(0, 5)] [InlineData(1, 4)] [InlineData(4, 1)] [InlineData(5, 1)]
    public async Task RamCountsPreviouslyConfirmedCardsAndKeepsItsOwnOneManaFloor(int previous, int expected)
    {
        var initial = Position(Ram);
        for (var i = 0; i < previous; i++)
            initial = RadGearTestPosition.Put(initial, "EARLIER-" + i, "OGN·219/298", "P1", "HAND");
        var game = new Driver(initial);
        for (var i = 0; i < previous; i++)
            await game.Act("P1", new PlayCardCommand("EARLIER-" + i, "OGN·219/298", []));
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("UNIT", Ram, []));
        Assert.Equal(mana - expected, game.State.RunePools["P1"].Mana);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(previous + 1, game.State.CardPlays.Finalized.Count(play => play.PlayerId == "P1"));
        await game.Replay();
    }

    [Fact]
    public async Task RamCountsItsControllersCounteredSpellAndDoesNotCountTheOpponentsCounterspell()
    {
        var initial = RadGearTestPosition.Put(Position(Ram), "EARLIER", "OGN·104/298", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "COUNTER", "OGN·064/298", "P2", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("EARLIER", "OGN·104/298", ["ALLY"]));
        if (game.State.PriorityPlayerId != "P2")
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [Assert.Single(game.State.StackItems).StackItemId]));
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        Assert.Single(game.State.CardPlays.Finalized, play => play.PlayerId == "P1");
        Assert.Single(game.State.CardPlays.Finalized, play => play.PlayerId == "P2");
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("UNIT", Ram, []));
        Assert.Equal(mana - 4, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Fact]
    public async Task RamCountsTheRobotMakerCardButNotThePlayedToken()
    {
        var initial = RadGearTestPosition.Put(Position(Ram), "MAKER", "RAD·049/167", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("MAKER", "RAD·049/167", []));
        await game.Top();
        Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == "SFD·T01");
        Assert.Single(game.State.CardPlays.Finalized, play => play.PlayerId == "P1");
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("UNIT", Ram, []));
        Assert.Equal(mana - 4, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Fact]
    public async Task RamDoesNotCountItsControllersCardsFromAnEarlierTurn()
    {
        var initial = RadGearTestPosition.Put(Position(Ram), "EARLIER", "OGN·219/298", "P1", "HAND");
        initial = WithRunes(initial, 5);
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("EARLIER", "OGN·219/298", []));
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P2", new EndTurnCommand());
        Assert.Empty(game.State.CardPlays.Finalized);
        await game.TapRunes("P1", Enumerable.Range(1, 5).Select(i => "MANA-" + i));
        await game.Act("P1", new PlayCardCommand("UNIT", Ram, []));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    private static MatchState WithRunes(MatchState state, int count)
    {
        for (var i = 1; i <= count; i++)
            state = RadGearTestPosition.Put(state, "MANA-" + i, "UNL-R04", "P1", "BASE");
        return state;
    }
}
