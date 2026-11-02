using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class SealedSubmittedOpeningTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PublicSideboardCountUsesTheRealPoolAndCountsTheChosenChampionOnlyOnce(bool legend, bool champion)
    {
        var run = new Run();
        await run.Join("alice");
        await run.Join("bob");
        Assert.Null(run.Live.SnapshotFor("alice").SideboardCounts);
        await run.Choose();
        var pool = run.State.PlayerSealedPools!["alice"];
        var deck = await Deck(pool, legend, champion);
        var submitted = await run.Act("alice", Command(deck));
        var expected = pool.Pool.Values.Sum() - 25 - (legend ? 1 : 0);
        foreach (var snapshot in submitted.Snapshots.Values.Append(ResolutionResult.BuildSpectatorSnapshot(submitted.State)))
        {
            var count = Assert.Single(snapshot.SideboardCounts!);
            Assert.Equal("alice", count.Key);
            Assert.Equal(expected, count.Value);
        }
        var publicView = JsonSerializer.Serialize(submitted.Snapshots["bob"] with { SealedPool = null }, Json);
        foreach (var cardNo in deck.MainDeck.Distinct(StringComparer.Ordinal))
            Assert.DoesNotContain(JsonSerializer.Serialize(cardNo, Json), publicView);
        Assert.Null(ResolutionResult.BuildSpectatorSnapshot(submitted.State).SealedPool);
        await run.Cold();
        Assert.Equal(expected, run.Live.SnapshotFor("bob").SideboardCounts!["alice"]);

        var forged = submitted.Snapshots["bob"] with { SideboardCounts = new Dictionary<string, int> { ["alice"] = expected + 1 } };
        Assert.NotNull(SealedRoomPools.ViewError(submitted.State, forged, "bob"));
    }

    [Fact]
    public async Task BetweenGamesPublicCountsRetainThePreviousConfigurationUntilEachPlayerSubmitsAgain()
    {
        var run = new Run(3);
        await run.Join("alice");
        await run.Join("bob");
        await run.Choose();
        await OpenCurrentGame(run, 3);
        var before = run.Live.SnapshotFor("alice").SideboardCounts!;
        await run.Act("bob", new SurrenderCommand());
        Assert.Empty(run.State.PlayerDecklists);
        Assert.Equal(before, run.Live.SnapshotFor("alice").SideboardCounts!);
        await run.Cold();
        await run.Choose();
        var replacement = await Deck(run.State.PlayerSealedPools!["alice"], true, false, 1);
        await run.Act("alice", Command(replacement));
        var after = run.Live.SnapshotFor("bob").SideboardCounts!;
        Assert.Equal(before["alice"] - 1, after["alice"]);
        Assert.Equal(before["bob"], after["bob"]);
        await run.Cold();
        Assert.Equal(after, run.Live.SnapshotFor("alice").SideboardCounts!);
    }

    [Fact]
    public async Task ConstructedSnapshotsKeepTheirExistingAbsentSideboardCount()
    {
        var live = new MatchSession("constructed-without-sealed-sideboard-count", new CoreRuleEngine());
        await live.EnsurePlayerAsync("alice", default);
        Assert.Null(live.SnapshotFor("alice").SideboardCounts);
    }
}
