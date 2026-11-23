using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.StandbyDefendObserverTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class MainDeckRecycleShuffleTests
{
    [Theory]
    [InlineData("062", "062", "062")]
    [InlineData("071", "073", "075")]
    public void ThreeCardBatchesReachAllSixPermutationsWithoutTheMeasuredHashSortBias(string first, string second, string third)
    {
        string[] cards = [$"P1-MAIN-UNL-{first}-219-15", $"P1-MAIN-UNL-{second}-219-16", $"P1-MAIN-UNL-{third}-219-17"];
        const string source = "P1-MAIN-VEN-056-14";
        var initial = MatchState.Create("recycle-distribution");
        Assert.Equal(MainDeckRecycleShuffleVersion.FisherYates, initial.RecycleShuffleVersion);
        var random = new Random(4175);
        var counts = new Dictionary<string, int>();
        for (var i = 0; i < 6000; i++)
        {
            var state = initial with { Seed = random.NextInt64(long.MinValue, long.MaxValue) };
            var shuffled = MainDeckRecycleShuffle.Shuffle(state, cards, 7, source);
            var order = string.Concat(shuffled.Select(id => Array.IndexOf(cards, id) + 1));
            counts[order] = counts.GetValueOrDefault(order) + 1;
        }

        Assert.Equal(new[] { "123", "132", "213", "231", "312", "321" }, counts.Keys.Order(StringComparer.Ordinal));
        // Uniform reference is 1,000 per permutation. Wide bounds catch the original measured bias.
        Assert.All(counts.Values, count => Assert.InRange(count, 850, 1150));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task InsightKeepsOnlyTopOrderAndReplaysTheSamePrivateBottomAfterRecovery(int count)
    {
        var initial = ClairvoyanceTests.Position(8) with { Seed = 4175, RngCursor = 7,
            RecycleShuffleVersion = MainDeckRecycleShuffleVersion.FisherYates };
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SPELL", "VEN·056", []));
        await driver.Top();
        var window = driver.State.PendingCardChoice!;
        string[] selected = new[] { "D1", "D3", "D5" }.Take(count).ToArray();
        var reverse = new ChooseCardsCommand(window.ChoiceId, window.ChoiceWindow, selected.Reverse().ToArray());
        var reversed = await OfficialInsightAndSpellLockTests.Act(driver.State, "P1", reverse);
        var actual = await driver.Act("P1", new ChooseCardsCommand(window.ChoiceId, window.ChoiceWindow, selected));
        Assert.Equal(MatchStateHasher.Hash(reversed.State), MatchStateHasher.Hash(actual.State));
        Assert.Equal(initial.RngCursor + (count > 1 ? 1 : 0), driver.State.RngCursor);
        var retained = window.ContextObjectIds.Except(selected).Reverse().ToArray();
        var order = driver.State.PendingCardChoice!;
        Assert.Equal("INSIGHT_ORDER", order.ChoiceWindow);
        await driver.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, retained));
        Assert.Equal(retained.Take(2), driver.State.PlayerZones["P1"].Hand);
        string[] bottom = count switch { 3 => ["D3", "D5", "D1"], 1 => ["D1"], _ => [] };
        Assert.Equal(retained.Skip(2).Concat(new[] { "D6", "D7", "D8" }).Concat(bottom), driver.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain("recycleShuffleVersion", JsonSerializer.Serialize(new { actual.Snapshots, actual.Prompts, actual.Events }), StringComparison.OrdinalIgnoreCase);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State, new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Fact]
    public async Task UnversionedInsightCheckpointKeepsTheOriginalAcceptedBottomAndReplay()
    {
        var initial = ClairvoyanceTests.Position(8) with { Seed = 4175, RngCursor = 7 };
        var oldJson = JsonSerializer.Serialize(initial);
        Assert.DoesNotContain(nameof(MatchState.RecycleShuffleVersion), oldJson);
        var oldState = JsonSerializer.Deserialize<MatchState>(oldJson)!;
        Assert.Equal(MainDeckRecycleShuffleVersion.LegacyHashSort, oldState.RecycleShuffleVersion);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(oldState));
        var driver = new Driver(oldState);
        await driver.Act("P1", new PlayCardCommand("SPELL", "VEN·056", []));
        await driver.Top();
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["D1", "D3", "D5"]));
        Assert.Equal(new[] { "D1", "D5", "D3" }, driver.State.PlayerZones["P1"].MainDeck.TakeLast(3));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(oldState, driver.Commands(), driver.State, new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        var changed = oldState with { RecycleShuffleVersion = MainDeckRecycleShuffleVersion.FisherYates };
        var rejected = await MatchActionLogReplayer.VerifyFinalStateAsync(changed, driver.Commands(), driver.State, new CoreRuleEngine(), default, driver.Events());
        Assert.False(rejected.IsMatch);
        Assert.Contains(rejected.Errors, error => error.Contains("recycle shuffle version", StringComparison.Ordinal));
    }
}
