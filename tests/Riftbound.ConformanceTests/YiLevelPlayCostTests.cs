using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class YiLevelPlayCostTests
{
    public static IEnumerable<object[]> YiCosts()
    {
        foreach (var no in new[] { "UNL-059/219", "UNL-059a/219" })
            foreach (var row in new (int Experience, int Mana, int Green)[] {
                (Experience: 0, Mana: 12, Green: 3), (2, 12, 3),
                (3, 10, 2), (5, 10, 2), (6, 8, 1), (10, 8, 1),
                (11, 6, 0), (16, 6, 0), (100, 6, 0) })
                yield return new object[] { no, row.Experience, row.Mana, row.Green };
    }

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

    [Theory, MemberData(nameof(YiCosts))]
    public async Task EachYiPrintingUsesTheHighestCurrentLevelRatherThanAddingTheThreeDiscounts(
        string no, int experience, int mana, int green)
    {
        var state = Position(no, experience, mana, RuneTrait.Green, green);
        var quote = Quote(state, no);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(green, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Green));
        Assert.Equal(0, quote.Cost.GenericPower);
        if (mana > 0)
            await RadGearTestPosition.Reject(state with {
                RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                    ["P1"] = new(mana - 1, 0, new Dictionary<string, int> { [RuneTrait.Green] = green }) } },
                "P1", new PlayCardCommand("UNIT", no, []));
        var game = new Driver(state);
        Assert.Contains(game.PromptFor("P1").Candidates!, candidate => candidate.Action == CommandTypes.PlayCard && candidate.Enabled);
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(12, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(12, game.State.CardObjects["UNIT"].ManaCost);
        Assert.Equal(experience, game.State.PlayerExperience["P1"]);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        OfficialInsightAndSpellLockTests.Restore(game.State);
        await game.Replay();
    }

    [Theory, InlineData("UNL-059/219"), InlineData("UNL-059a/219")]
    public async Task YiDoesNotUseTheOpponentsExperienceOrAcceptTheWrongRemainingColor(string no)
    {
        var underLevel = Position(no, 2, 10, RuneTrait.Green, 2);
        Assert.False(Quote(underLevel, no).CanPay);
        await RadGearTestPosition.Reject(underLevel, "P1", new PlayCardCommand("UNIT", no, []));
        var wrongColor = Position(no, 6, 8, RuneTrait.Blue, 1);
        Assert.False(Quote(wrongColor, no).CanPay);
        await RadGearTestPosition.Reject(wrongColor, "P1", new PlayCardCommand("UNIT", no, []));
    }

    [Theory, InlineData("UNL-059/219"), InlineData("UNL-059a/219")]
    public async Task YiPaysItsRemainingGreenWithARealInlineRecycledRune(string no)
    {
        var initial = VenConditionalPlayCostTests.Place(Position(no, 6, 8, RuneTrait.Green, 0),
            "GREEN", "OGN·042/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: ["RECYCLE_RUNE:GREEN"]));
        Assert.Contains("GREEN", game.State.PlayerZones["P1"].RuneDeck);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(6, game.State.PlayerExperience["P1"]);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Contains(game.Events(), entry => entry.Event.Kind == "RUNE_RECYCLED");
        OfficialInsightAndSpellLockTests.Restore(game.State);
        await game.Replay();
    }

    [Theory, InlineData("UNL-059/219"), InlineData("UNL-059a/219")]
    public async Task YiStillAllowsWildcardPowerToPayTheOneRemainingGreen(string no)
    {
        var initial = Position(no, 6, 8, RuneTrait.Green, 0);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(8, 1) } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(6, game.State.PlayerExperience["P1"]);
        await game.Replay();
    }

    [Fact]
    public void TheCurrentExperienceIsRequotedWithoutChangingThePrintedCosts()
    {
        const string no = "UNL-059/219";
        // Current-level boundary: these declared states do not claim that an
        // unexecuted effect granted experience.
        var initial = Position(no, 5, 20, RuneTrait.Green, 3);
        Assert.Equal(10, Quote(initial, no).Cost!.Mana);
        var six = initial with { PlayerExperience = new Dictionary<string, int>(initial.PlayerExperience) { ["P1"] = 6 } };
        Assert.Equal(8, Quote(six, no).Cost!.Mana);
        Assert.Equal(1, Quote(six, no).Cost!.PowerByTrait.GetValueOrDefault(RuneTrait.Green));
        Assert.Equal(10, Quote(initial, no).Cost!.Mana);
        Assert.Equal(12, six.CardObjects["UNIT"].ManaCost);
        Assert.Equal(12, six.CardObjects["UNIT"].Power);
    }
}
