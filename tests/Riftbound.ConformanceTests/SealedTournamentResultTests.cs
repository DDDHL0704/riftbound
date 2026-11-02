using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SealedTournamentResultTests
{
    [Fact]
    public async Task AtomicMemoryAdmissionRetainsFirstTimeAndRejectsConflictingFinalScores()
    {
        var run = await Create(1);
        var terminal = await Adjudicator(run).ApplyGameLossAsync("memory-final", new(1, "bob"), default);
        var original = MatchResultRecords.FromFinishedState(terminal.State, DateTimeOffset.Parse("2026-11-02T01:00:00Z"));
        var store = new InMemoryMatchResultStore();
        await store.RecordFinalMatchResultAsync(original, default);
        await Task.WhenAll(Enumerable.Range(1, 8).Select(index => Task.Run(async () =>
            await store.RecordFinalMatchResultAsync(original with { FinishedAtUtc = original.FinishedAtUtc.AddSeconds(index) }, default))));
        Assert.Equal(original.FinishedAtUtc, (await store.GetMatchResultAsync(original.RoomId, default))!.FinishedAtUtc);
        var error = await Assert.ThrowsAsync<MatchSessionException>(() => store.RecordFinalMatchResultAsync(original with
        {
            Players = [new("alice", "P1", 2, true), new("bob", "P2", 0, false)]
        }, default).AsTask());
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Equal(original.Players, (await store.GetMatchResultAsync(original.RoomId, default))!.Players);
        Assert.Single(await store.ListMatchResultsForPlayerAsync("alice", 10, default));
    }

    [Fact]
    public async Task OrdinaryResultWritesKeepTheirExistingReplacementBehavior()
    {
        var store = new InMemoryMatchResultStore();
        var initial = new MatchResultRecord("constructed-result-replacement",
            [new("alice", "P1", 8, true), new("bob", "P2", 4, false)], "alice", DateTimeOffset.Parse("2026-11-02T01:00:00Z"));
        await store.RecordMatchResultAsync(initial, default);
        var updated = initial with { FinishedAtUtc = initial.FinishedAtUtc.AddSeconds(1) };
        await store.RecordMatchResultAsync(updated, default);
        Assert.Equal(updated.FinishedAtUtc, (await store.GetMatchResultAsync(initial.RoomId, default))!.FinishedAtUtc);
    }

    [Fact]
    public async Task ConcurrentFinalRetriesWriteOnlyOneResultAndRetainItsOriginalTime()
    {
        var run = await Create(1);
        var terminal = await Adjudicator(run).ApplyGameLossAsync("concurrent-final", new(1, "bob"), default);
        Assert.True(terminal.Accepted, terminal.ErrorMessage);
        var store = new Results { PauseNextWrite = true };
        var first = MatchResultRecords.RecordFinishedSeriesAsync(store, terminal, default).AsTask();
        await store.WritePaused.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = MatchResultRecords.RecordFinishedSeriesAsync(store, terminal, default).AsTask();
        store.ResumeWrite.TrySetResult();
        await Task.WhenAll(first, second);
        var original = await store.GetMatchResultAsync(run.State.RoomId, default);
        Assert.NotNull(original);
        Assert.Equal(1, store.Writes);
        await MatchResultRecords.RecordFinishedSeriesAsync(store, terminal, default);
        Assert.Equal(1, store.Writes);
        Assert.Equal(original, await store.GetMatchResultAsync(run.State.RoomId, default));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task OnlyTheFinalPenaltyRecordsTheSeriesAndRestoredRetriesKeepOneResult(int bestOf)
    {
        var run = await Create(bestOf);
        var store = new Results();
        var service = new MatchTournamentAdjudicationService(store);
        var losers = bestOf == 1 ? new[] { "bob" } : new[] { "bob", "alice", "bob" };
        for (var index = 0; index < losers.Length; index++)
        {
            var decision = new TournamentGameLossDecision(index + 1, losers[index]);
            var id = "result-loss-" + index;
            var result = await service.ApplyGameLossAsync(Adjudicator(run), id, decision, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            if (index < losers.Length - 1)
            {
                Assert.Null(await store.GetMatchResultAsync(run.State.RoomId, default));
                Assert.Equal(0, store.Writes);
                await run.Cold();
                continue;
            }
            var recorded = await store.GetMatchResultAsync(run.State.RoomId, default);
            Assert.NotNull(recorded);
            Assert.Equal("alice", recorded.WinnerPlayerId);
            Assert.Equal(bestOf == 1 ? new[] { "alice:1:True", "bob:0:False" } : new[] { "alice:2:True", "bob:1:False" },
                recorded.Players.Select(player => $"{player.PlayerId}:{player.Score}:{player.Won}"));
            var journalCount = run.Journal.Entries.Count;
            await run.Cold();
            var retry = await new MatchTournamentAdjudicationService(store)
                .ApplyGameLossAsync(Adjudicator(run), id, decision, default);
            Assert.True(retry.Accepted, retry.ErrorMessage);
            Assert.Empty(retry.Events);
            Assert.Equal(journalCount, run.Journal.Entries.Count);
            Assert.Equal(1, store.Writes);
            Assert.Equal(recorded, await store.GetMatchResultAsync(run.State.RoomId, default));
            Assert.Single(await store.ListMatchResultsForPlayerAsync("alice", 10, default));
            Assert.Single(await store.ListMatchResultsForPlayerAsync("bob", 10, default));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResultWriteFailureCanBeRetriedAfterColdRecoveryWithoutAnotherPenalty(bool failAfterWrite)
    {
        var run = await Create(1);
        var store = new Results { FailNext = true, FailAfterWrite = failAfterWrite };
        var decision = new TournamentGameLossDecision(1, "bob");
        await Assert.ThrowsAsync<IOException>(() => new MatchTournamentAdjudicationService(store)
            .ApplyGameLossAsync(Adjudicator(run), "retry-result", decision, default).AsTask());
        Assert.Equal(MatchSeriesPhases.Finished, run.Live.SnapshotFor("alice").Series!.Phase);
        Assert.Equal(1, run.State.SeriesState!.Games.Count(game => game.Completed));
        var journalCount = run.Journal.Entries.Count;
        var storedBeforeRecovery = await store.GetMatchResultAsync(run.State.RoomId, default);
        Assert.Equal(failAfterWrite, storedBeforeRecovery is not null);
        await run.Cold();
        var result = await new MatchTournamentAdjudicationService(store)
            .ApplyGameLossAsync(Adjudicator(run), "retry-result", decision, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(journalCount, run.Journal.Entries.Count);
        Assert.Equal(1, store.Writes);
        var recorded = await store.GetMatchResultAsync(run.State.RoomId, default);
        Assert.NotNull(recorded);
        Assert.Equal(new[] { 1, 0 }, recorded.Players.Select(player => player.Score));
        if (storedBeforeRecovery is not null) Assert.Equal(storedBeforeRecovery, recorded);
        Assert.Single(await store.ListMatchResultsForPlayerAsync("alice", 10, default));
    }

    [Fact]
    public async Task RejectedOrUncommittedPenaltyNeverWritesAPublicResult()
    {
        var run = await Create(1);
        var store = new Results();
        var service = new MatchTournamentAdjudicationService(store);
        var rejected = await service.ApplyGameLossAsync(Adjudicator(run), "wrong-game", new(2, "bob"), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(0, store.Writes);
        run.Journal.FailNext = true;
        await Assert.ThrowsAsync<IOException>(() => service.ApplyGameLossAsync(Adjudicator(run), "journal-failure", new(1, "bob"), default).AsTask());
        Assert.Equal(0, store.Writes);
        Assert.Null(await store.GetMatchResultAsync(run.State.RoomId, default));
        Assert.Equal(MatchSeriesPhases.ChoosingPlayOrder, run.Live.SnapshotFor("alice").Series!.Phase);
        var accepted = await service.ApplyGameLossAsync(Adjudicator(run), "journal-failure", new(1, "bob"), default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public async Task ExistingConflictingPublicResultIsRejectedWithoutOverwritingIt()
    {
        var run = await Create(1);
        var store = new Results();
        await store.RecordMatchResultAsync(new(run.State.RoomId,
            [new("alice", "P1", 0, false), new("bob", "P2", 1, true)], "bob", DateTimeOffset.UtcNow), default);
        var original = await store.GetMatchResultAsync(run.State.RoomId, default);
        var error = await Assert.ThrowsAsync<MatchSessionException>(() => new MatchTournamentAdjudicationService(store)
            .ApplyGameLossAsync(Adjudicator(run), "conflicting-store", new(1, "bob"), default).AsTask());
        Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        Assert.Equal(original, await store.GetMatchResultAsync(run.State.RoomId, default));
        Assert.Equal(1, store.Writes);
    }

    private static async Task<SealedSubmittedOpeningTests.Run> Create(int bestOf)
    {
        var run = new SealedSubmittedOpeningTests.Run(bestOf);
        await run.Join("alice");
        await run.Join("bob");
        return run;
    }

    private static IMatchTournamentAdjudicator Adjudicator(SealedSubmittedOpeningTests.Run run) =>
        Assert.IsAssignableFrom<IMatchTournamentAdjudicator>(run.Live);

    internal sealed class Results : IMatchResultStore
    {
        private readonly InMemoryMatchResultStore inner = new();
        public int Writes { get; private set; }
        public bool FailNext { get; set; }
        public bool FailAfterWrite { get; init; }
        public bool PauseNextWrite { get; set; }
        public TaskCompletionSource WritePaused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ResumeWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask RecordMatchResultAsync(MatchResultRecord result, CancellationToken cancellationToken)
        {
            var fail = FailNext;
            FailNext = false;
            if (PauseNextWrite)
            {
                PauseNextWrite = false;
                WritePaused.TrySetResult();
                await ResumeWrite.Task.WaitAsync(cancellationToken);
            }
            if (fail && !FailAfterWrite) throw new IOException("result store unavailable before commit");
            await inner.RecordMatchResultAsync(result, cancellationToken);
            Writes++;
            if (fail) throw new IOException("result store response lost after commit");
        }
        public ValueTask<MatchResultRecord?> GetMatchResultAsync(string roomId, CancellationToken cancellationToken) =>
            inner.GetMatchResultAsync(roomId, cancellationToken);
        public ValueTask<PlayerMatchStatsRecord> GetPlayerMatchStatsAsync(string playerId, CancellationToken cancellationToken) =>
            inner.GetPlayerMatchStatsAsync(playerId, cancellationToken);
        public ValueTask<IReadOnlyList<PlayerLeaderboardEntryRecord>> ListLeaderboardAsync(int limit, CancellationToken cancellationToken) =>
            inner.ListLeaderboardAsync(limit, cancellationToken);
        public ValueTask<IReadOnlyList<MatchResultRecord>> ListMatchResultsForPlayerAsync(string playerId, int limit, CancellationToken cancellationToken) =>
            inner.ListMatchResultsForPlayerAsync(playerId, limit, cancellationToken);
    }
}
