using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class LevelAndGraveyardPlayCostTests
{
    private static MatchState Position(string no, int experience, int mana, string trait, int power)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = VenConditionalPlayCostTests.Place(state, "GRAVE", "OGN·219/298", "P1", "BANISHED");
        return state with {
            PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = experience, ["P2"] = 100 },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(mana, 0, new Dictionary<string, int> { [trait] = power }) } };
    }

    private static PlayCostQuoteDto Quote(MatchState state, string no)
    {
        var command = new PlayCardCommand("UNIT", no, []);
        var before = MatchStateHasher.Hash(state);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.Equal(before, MatchStateHasher.Hash(state));
        return quote;
    }

    [Theory]
    [InlineData(0, 5)] [InlineData(5, 5)] [InlineData(6, 3)]
    [InlineData(10, 3)] [InlineData(11, 1)] [InlineData(16, 1)] [InlineData(100, 1)]
    public async Task CenterYourMindUsesItsCurrentHighestLevelToPayThenDrawsTwo(int experience, int mana)
    {
        const string no = "UNL-091/219";
        var initial = Position(no, experience, mana, RuneTrait.Orange, 0);
        var quote = Quote(initial, no);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await RadGearTestPosition.Reject(initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(mana - 1, 0) } },
            "P1", new PlayCardCommand("UNIT", no, []));
        var game = new Driver(initial);
        Assert.Contains(game.PromptFor("P1").Candidates!, candidate => candidate.Action == CommandTypes.PlayCard && candidate.Enabled);
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Single(game.State.StackItems);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(experience, game.State.PlayerExperience["P1"]);
        Assert.Equal(5, game.State.CardObjects["UNIT"].ManaCost);
        await game.Top();
        Assert.Equal(new[] { "P1-DRAW-1", "P1-DRAW-2" }, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(new[] { "P1-DRAW-3" }, game.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        OfficialInsightAndSpellLockTests.Restore(game.State);
        await game.Replay();
    }

    [Fact]
    public void CenterYourMindRequotesTheActorsCurrentExperienceWithoutUsingTheOpponentsLevel()
    {
        const string no = "UNL-091/219";
        // Current-level quote boundary; experience is declared in these states,
        // rather than attributed to an unexecuted experience-granting effect.
        var initial = Position(no, 5, 20, RuneTrait.Orange, 0);
        Assert.Equal(100, initial.PlayerExperience["P2"]);
        Assert.Equal(5, Quote(initial, no).Cost!.Mana);
        var six = initial with { PlayerExperience = new Dictionary<string, int>(initial.PlayerExperience) { ["P1"] = 6 } };
        Assert.Equal(3, Quote(six, no).Cost!.Mana);
        var eleven = six with { PlayerExperience = new Dictionary<string, int>(six.PlayerExperience) { ["P1"] = 11 } };
        Assert.Equal(1, Quote(eleven, no).Cost!.Mana);
        Assert.Equal(5, Quote(initial, no).Cost!.Mana);
    }

    [Theory]
    [InlineData(0, 10)] [InlineData(1, 9)] [InlineData(3, 7)]
    [InlineData(9, 1)] [InlineData(10, 0)] [InlineData(14, 0)]
    public async Task RhasaCountsAllOwnGraveyardCardsAndOnlyReducesMana(int graves, int mana)
    {
        const string no = "OGN·195/298";
        var initial = Position(no, 0, mana, RuneTrait.Purple, 1);
        var types = new[] { "OGN·219/298", "OGN·048/298", "SFD·022/221" };
        for (var i = 0; i < graves; i++)
            initial = VenConditionalPlayCostTests.Place(initial, "OWN-" + i, types[i % types.Length], "P1", "GRAVEYARD");
        for (var i = 0; i < 12; i++)
            initial = VenConditionalPlayCostTests.Place(initial, "OTHER-" + i, types[i % types.Length], "P2", "GRAVEYARD");
        var quote = Quote(initial, no);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Purple));
        var noPurple = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(mana, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }) } };
        Assert.False(Quote(noPurple, no).CanPay);
        await RadGearTestPosition.Reject(noPurple, "P1", new PlayCardCommand("UNIT", no, []));
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(graves, game.State.PlayerZones["P1"].Graveyard.Count);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(6, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(10, game.State.CardObjects["UNIT"].ManaCost);
        OfficialInsightAndSpellLockTests.Restore(game.State);
        await game.Replay();
    }

    [Fact]
    public async Task RhasaRequotesAfterAnActualSpellEntersAndLeavesItsGraveyard()
    {
        const string no = "OGN·195/298";
        var initial = VenConditionalPlayCostTests.Place(Position(no, 0, 12, RuneTrait.Purple, 1),
            "SALVAGE", "VEN·049", "P1", "HAND");
        Assert.Equal(10, Quote(initial, no).Cost!.Mana);
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SALVAGE", "VEN·049", []));
        Assert.Empty(game.State.PlayerZones["P1"].Graveyard);
        await game.Top();
        Assert.Equal(new[] { "SALVAGE" }, game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(9, Quote(game.State, no).Cost!.Mana);
        Assert.True(Quote(game.State, no).CanPay);
        await game.Act("P1", new PlayCardCommand("SALVAGE", "VEN·049", []));
        Assert.Empty(game.State.PlayerZones["P1"].Graveyard);
        await game.Top();
        Assert.Contains("SALVAGE", game.State.PlayerZones["P1"].Banished);
        var quote = Quote(game.State, no);
        Assert.Equal(10, quote.Cost!.Mana);
        Assert.False(quote.CanPay);
        await RadGearTestPosition.Reject(game.State, "P1", new PlayCardCommand("UNIT", no, []));
        OfficialInsightAndSpellLockTests.Restore(game.State);
        await game.Replay();
    }

    [Fact]
    public async Task AGraveyardDiscountDoesNotGrantPermissionToPlayRhasaFromThatZone()
    {
        const string no = "OGN·195/298";
        var initial = VenConditionalPlayCostTests.Place(Position(no, 0, 20, RuneTrait.Purple, 1),
            "UNIT", no, "P1", "GRAVEYARD");
        for (var i = 0; i < 10; i++)
            initial = VenConditionalPlayCostTests.Place(initial, "OWN-" + i, "OGN·219/298", "P1", "GRAVEYARD");
        Assert.False(Quote(initial, no).CanPay);
        await RadGearTestPosition.Reject(initial, "P1", new PlayCardCommand("UNIT", no, []));
        OfficialInsightAndSpellLockTests.Restore(initial);
    }

    [Theory]
    [InlineData("OGN·195/298", 8)]
    [InlineData("VEN·096", 3)]
    public void PricingAPlayFromTheGraveyardExcludesItsOwnSourceBeforeCountingCards(string no, int mana)
    {
        // Cost-stage fixture: CN354 moves the source onto the chain before
        // CN356.4 counts the remaining graveyard. The calculator receives the
        // original command state and must apply that same pricing boundary.
        var state = VenConditionalPlayCostTests.Place(Position(no, 0, 20, RuneTrait.Purple, 1),
            "UNIT", no, "P1", "GRAVEYARD");
        state = VenConditionalPlayCostTests.Place(state, "SAME", no, "P1", "GRAVEYARD");
        state = VenConditionalPlayCostTests.Place(state, "OTHER", "OGN·219/298", "P1", "GRAVEYARD");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        var before = MatchStateHasher.Hash(state);
        Assert.Equal(mana, CoreRuleEngine.CalculatePlayManaCost(state, "P1", behavior, sourceObjectId: "UNIT").Total);
        Assert.Equal(mana, CoreRuleEngine.MinimumPlayManaCost(state, "P1", behavior, "UNIT").Total);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        OfficialInsightAndSpellLockTests.Restore(state);
    }
}
