using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class GameHubJoinTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SealedHubRepairsFinalResultWriteOnRestoredIntentRetry(bool failAfterWrite)
    {
        var run = new SealedSubmittedOpeningTests.Run(1);
        await run.Join("alice");
        await run.Join("bob");
        await run.Choose();
        await SealedSubmittedOpeningTests.OpenCurrentGame(run, 1);
        var store = new SealedTournamentResultTests.Results { FailNext = true, FailAfterWrite = failAfterWrite };
        var hub = CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "before-result-restart",
            new FixedMatchSessionRegistry(run.Live), matchResultStore: store, authenticatedHandle: "bob");
        var command = JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.Surrender });
        await Assert.ThrowsAsync<IOException>(() => hub.SubmitIntent(run.State.RoomId, "bob", "final-result-retry", command));
        Assert.Equal(MatchSeriesPhases.Finished, run.Live.SnapshotFor("alice").Series!.Phase);
        var count = run.Journal.Entries.Count;
        var prior = await store.GetMatchResultAsync(run.State.RoomId, default);
        Assert.Equal(failAfterWrite, prior is not null);
        await run.Cold();
        var clients = new RecordingHubClients();
        hub = CreateHub(clients, new RecordingGroupManager(), "after-result-restart",
            new FixedMatchSessionRegistry(run.Live), matchResultStore: store, authenticatedHandle: "bob");
        var retry = await hub.SubmitIntent(run.State.RoomId, "bob", "final-result-retry", command);
        Assert.True(retry.Accepted, retry.Message);
        Assert.Equal(count, run.Journal.Entries.Count);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Equal(1, store.Writes);
        var recorded = await store.GetMatchResultAsync(run.State.RoomId, default);
        Assert.NotNull(recorded);
        Assert.Equal("alice", recorded.WinnerPlayerId);
        Assert.Equal(new[] { 1, 0 }, recorded.Players.Select(player => player.Score));
        if (prior is not null) Assert.Equal(prior, recorded);
        Assert.Single(await store.ListMatchResultsForPlayerAsync("alice", 10, default));
        Assert.Single(await store.ListMatchResultsForPlayerAsync("bob", 10, default));
    }

    [Fact]
    public async Task SealedHubRecordsOnlyTheCompletedSeriesWithGameWinsAsThePublicScore()
    {
        var run = new SealedSubmittedOpeningTests.Run(3);
        await run.Join("alice");
        await run.Join("bob");
        var store = new InMemoryMatchResultStore();
        var chooser = run.Live.SnapshotFor("alice").Series!.PlayOrderChooserPlayerId!;
        var chooseHub = CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "choose-order",
            new FixedMatchSessionRegistry(run.Live), matchResultStore: store, authenticatedHandle: chooser);
        var choice = await chooseHub.SubmitIntent(run.State.RoomId, chooser, "hub-choose-order",
            JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.ChoosePlayOrder, goFirst = false }));
        Assert.True(choice.Accepted, choice.Message);
        Assert.Equal(MatchSeriesPhases.SubmittingDecks, run.Live.SnapshotFor(chooser).Series!.Phase);
        var game = 0;
        foreach (var loser in new[] { "bob", "alice", "bob" })
        {
            game++;
            if (game > 1) await run.Choose();
            await SealedSubmittedOpeningTests.OpenCurrentGame(run, 3);
            var hub = CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "series-game-" + game,
                new FixedMatchSessionRegistry(run.Live), matchResultStore: store, authenticatedHandle: loser);
            var receipt = await hub.SubmitIntent(run.State.RoomId, loser, "hub-game-result-" + game,
                JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.Surrender }));
            Assert.True(receipt.Accepted, receipt.Message);
            var recorded = await store.GetMatchResultAsync(run.State.RoomId, default);
            if (game < 3) Assert.Null(recorded);
            else
            {
                Assert.NotNull(recorded);
                Assert.Equal("alice", recorded.WinnerPlayerId);
                Assert.Equal(new[] { "alice:2:True", "bob:1:False" }, recorded.Players
                    .Select(player => $"{player.PlayerId}:{player.Score}:{player.Won}"));
                Assert.Single(await store.ListMatchResultsForPlayerAsync("alice", 10, default));
                Assert.Single(await store.ListMatchResultsForPlayerAsync("bob", 10, default));
            }
            await run.Cold();
        }
    }
}
