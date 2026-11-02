using System.Collections;
using System.Globalization;
using System.Text.Json;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MatchSeriesTests
{
    private const long Seed = 424242;
    private static readonly string[] Players = ["alice", "bob"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SplitWinsAndDrawReachTwoWinsOnlyAfterTheFourthActualGame()
    {
        var series = MatchSeries.Create(3, Players, Seed);
        var history = new List<Operation>();
        void Step(Operation operation)
        {
            history.Add(operation);
            series = Apply(series, operation);
            AssertRecoveredAndReplayed(series, history);
        }

        AssertRecoveredAndReplayed(series, history);
        Step(ChooseFirst(series, "alice"));
        Assert.Equal(MatchSeriesPhase.SubmittingDecks, series.Phase);
        Step(new("start"));
        Step(new("complete", GameNumber: 1, Winner: "alice"));
        Assert.Equal("bob", series.PlayOrderChooserPlayerId);
        Assert.Null(series.WinnerPlayerId);
        Assert.Equal(1, series.Wins["alice"]);

        Step(new("choose", "bob", PlayFirst: false));
        Assert.Equal("alice", series.FirstPlayerId);
        Step(new("start"));
        Step(new("complete", GameNumber: 2));
        Assert.Equal(3, series.GameNumber);
        Assert.Equal("alice", series.FirstPlayerId);
        Assert.Null(series.PlayOrderChooserPlayerId);
        Assert.Equal(MatchSeriesPhase.SubmittingDecks, series.Phase);
        Assert.Equal(1, series.Wins.Values.Sum());

        Step(new("start"));
        Step(new("complete", GameNumber: 3, Winner: "bob"));
        Assert.Equal("alice", series.PlayOrderChooserPlayerId);
        Assert.Null(series.WinnerPlayerId);
        Step(new("choose", "alice", PlayFirst: true));
        Step(new("start"));
        Step(new("complete", GameNumber: 4, Winner: "alice"));
        Assert.Equal(4, series.GameNumber);
        Assert.Equal(MatchSeriesPhase.Finished, series.Phase);
        Assert.Equal("alice", series.WinnerPlayerId);
        Assert.Equal(2, series.Wins["alice"]);
        Assert.Equal(1, series.Wins["bob"]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ConsecutiveDrawsNeverEndTheSeriesOrPermitAReplacementOrderChoice(int bestOf)
    {
        var series = ChooseAndStart(MatchSeries.Create(bestOf, Players, Seed), "bob");
        for (var game = 1; game <= 6; game++)
        {
            series = series.CompleteGame(game, null);
            Assert.Equal(game + 1, series.GameNumber);
            Assert.Equal(MatchSeriesPhase.SubmittingDecks, series.Phase);
            Assert.Equal("bob", series.FirstPlayerId);
            Assert.Null(series.PlayOrderChooserPlayerId);
            Assert.Null(series.WinnerPlayerId);
            Assert.All(series.Wins.Values, wins => Assert.Equal(0, wins));
            Assert.Throws<InvalidOperationException>(() => series.ChoosePlayOrder("alice", true));
            series = Cold(series).StartGame();
        }

        series = series.CompleteGame(7, "alice");
        Assert.Equal(bestOf == 1 ? MatchSeriesPhase.Finished : MatchSeriesPhase.ChoosingPlayOrder, series.Phase);
        Assert.Equal(bestOf == 1 ? "alice" : null, series.WinnerPlayerId);
        Assert.Equal(bestOf == 1 ? null : "bob", series.PlayOrderChooserPlayerId);
    }

    [Fact]
    public void TwoStraightWinsEndTheMatchAndBlockFurtherPlay()
    {
        var series = ChooseAndStart(MatchSeries.Create(3, Players, Seed), "alice").CompleteGame(1, "bob");
        Assert.Equal("alice", series.PlayOrderChooserPlayerId);
        series = ChooseAndStart(series, "bob").CompleteGame(2, "bob");
        Assert.Equal("bob", series.WinnerPlayerId);
        Assert.Equal(2, series.Wins["bob"]);
        Assert.Equal(2, series.State.Games.Count);
        Assert.Null(series.PlayOrderChooserPlayerId);
        Assert.Throws<InvalidOperationException>(() => series.StartGame());
        Assert.Throws<InvalidOperationException>(() => series.ChoosePlayOrder("alice", true));
        Assert.Throws<InvalidOperationException>(() => series.CompleteGame(3, "alice"));
        Assert.Equal(Persist(series), Persist(Cold(series)));
    }

    [Fact]
    public void OnlyTheDesignatedPlayerCanChooseAndRejectedOperationsDoNotChangeState()
    {
        var series = MatchSeries.Create(3, Players, Seed);
        var before = Persist(series);
        var other = Players.Single(player => player != series.PlayOrderChooserPlayerId);
        Assert.Throws<InvalidOperationException>(() => series.ChoosePlayOrder(other, true));
        Assert.Throws<InvalidOperationException>(() => series.ChoosePlayOrder("spectator", true));
        Assert.Throws<InvalidOperationException>(() => series.StartGame());
        Assert.Throws<InvalidOperationException>(() => series.CompleteGame(1, "alice"));
        Assert.Equal(before, Persist(series));

        var selected = series.ChoosePlayOrder(series.PlayOrderChooserPlayerId!, false);
        Assert.Equal(other, selected.FirstPlayerId);
        Assert.Equal(before, Persist(series));
        before = Persist(selected);
        Assert.Throws<InvalidOperationException>(() => selected.ChoosePlayOrder(other, true));
        Assert.Throws<InvalidOperationException>(() => selected.CompleteGame(1, "alice"));
        Assert.Equal(before, Persist(selected));
    }

    [Fact]
    public void DuplicateOutcomesAreIdempotentAcrossLaterGamesAndConflictsCannotRewriteHistory()
    {
        var series = ChooseAndStart(MatchSeries.Create(3, Players, Seed), "alice");
        var before = Persist(series);
        Assert.Throws<InvalidOperationException>(() => series.CompleteGame(0, "alice"));
        Assert.Throws<InvalidOperationException>(() => series.CompleteGame(2, "alice"));
        Assert.Throws<InvalidOperationException>(() => series.CompleteGame(1, "spectator"));
        Assert.Throws<InvalidOperationException>(() => series.CompleteGame(1, ""));
        Assert.Equal(before, Persist(series));

        series = ChooseAndStart(series.CompleteGame(1, "alice"), "bob");
        before = Persist(series);
        Assert.Same(series, series.CompleteGame(1, "alice"));
        Assert.Throws<InvalidOperationException>(() => series.CompleteGame(1, "bob"));
        Assert.Throws<InvalidOperationException>(() => series.CompleteGame(1, null));
        Assert.Equal(before, Persist(series));
        series = series.CompleteGame(2, null);
        Assert.Same(series, series.CompleteGame(2, null));
    }

    // Independently calculated with Python hashlib, not the implementation's helper.
    [Theory]
    [InlineData(0L, "bob")]
    [InlineData(1L, "bob")]
    [InlineData(2L, "bob")]
    [InlineData(424242L, "bob")]
    [InlineData(long.MinValue, "alice")]
    [InlineData(long.MaxValue, "alice")]
    public void PrivateSeedChoosesTheSamePlayerAcrossCulturesAndRestarts(long seed, string expected)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var series = MatchSeries.Create(3, Players, seed);
            Assert.Equal(expected, series.PlayOrderChooserPlayerId);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(expected, Cold(series, seed).PlayOrderChooserPlayerId);
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    [Fact]
    public void RestoreRejectsAChangedInitialChooserAndAnUnrelatedRecordedSeed()
    {
        var series = MatchSeries.Create(3, Players, 0);
        Assert.Throws<ArgumentException>(() => MatchSeries.Restore(series.State with
        {
            InitialPlayOrderChooserPlayerId = Players.Single(player => player != series.PlayOrderChooserPlayerId)
        }, 0));
        Assert.Throws<ArgumentException>(() => MatchSeries.Restore(series.State, long.MinValue));
    }

    public static IEnumerable<object[]> InvalidFrames()
    {
        var initial = MatchSeries.Create(3, Players, Seed).State;
        yield return [initial with { BestOf = 2 }];
        yield return [initial with { PlayerIds = ["alice", "alice"] }];
        yield return [initial with { PlayerIds = [" alice", "bob"] }];
        yield return [initial with { PlayerIds = null! }];
        yield return [initial with { Games = null! }];
        yield return [initial with { Games = [] }];
        yield return [initial with { Games = [null!] }];
        yield return [initial with { Games = [new(2)] }];
        yield return [initial with { Games = [new(1, "spectator")] }];
        yield return [initial with { Games = [new(1, Started: true)] }];
        yield return [initial with { Games = [new(1, "alice", Completed: true)] }];
        yield return [initial with { Games = [new(1, "alice", true, false, "alice")] }];
        yield return [initial with { Games = [new(1, "alice", true, true, "spectator")] }];
        yield return [initial with { Games = [new(1), new(2)] }];
        yield return [initial with { Games = [new(1, "alice", true, true, "alice")] }];
        yield return [initial with { Games = [new(1, "alice", true, true), new(2)] }];
        yield return [initial with { Games = [new(1, "alice", true, true), new(2, "bob")] }];
        yield return [initial with { Games = [new(1, "alice", true, true, "alice"),
            new(2, "bob", true, true, "alice"), new(3)] }];
    }

    [Theory]
    [MemberData(nameof(InvalidFrames))]
    public void RestoreRejectsMalformedOrInconsistentGameHistory(MatchSeriesState state)
    {
        Assert.Throws<ArgumentException>(() => MatchSeries.Restore(state, Seed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    public void UnsupportedMatchLengthsAreRejected(int bestOf)
    {
        Assert.Throws<ArgumentException>(() => MatchSeries.Create(bestOf, Players, Seed));
    }

    [Fact]
    public void CallerCollectionsCannotMutateTheLiveModelOrItsPriorStates()
    {
        var players = new[] { "alice", "bob" };
        var series = MatchSeries.Create(3, players, Seed);
        players[0] = "spectator";
        Assert.Equal(Players, series.State.PlayerIds);
        Assert.Throws<NotSupportedException>(() => ((IList)series.State.PlayerIds)[0] = "spectator");
        Assert.Throws<NotSupportedException>(() => ((IList)series.State.Games)[0] = new MatchSeriesGameState(9));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)series.Wins)["alice"] = 2);

        var games = series.State.Games.ToArray();
        var restored = MatchSeries.Restore(series.State with { Games = games }, Seed);
        var original = Persist(restored);
        games[0] = new(1, "bob", true, true, "bob");
        Assert.Equal(original, Persist(restored));
        _ = ChooseAndStart(restored, "alice").CompleteGame(1, "bob");
        Assert.Equal(original, Persist(restored));
    }

    private sealed record Operation(string Kind, string? Player = null, bool PlayFirst = false,
        int GameNumber = 0, string? Winner = null);

    private static Operation ChooseFirst(MatchSeries series, string firstPlayer) =>
        new("choose", series.PlayOrderChooserPlayerId, series.PlayOrderChooserPlayerId == firstPlayer);

    private static MatchSeries ChooseAndStart(MatchSeries series, string firstPlayer) =>
        Apply(series, ChooseFirst(series, firstPlayer)).StartGame();

    private static MatchSeries Apply(MatchSeries series, Operation operation) => operation.Kind switch
    {
        "choose" => series.ChoosePlayOrder(operation.Player!, operation.PlayFirst),
        "start" => series.StartGame(),
        "complete" => series.CompleteGame(operation.GameNumber, operation.Winner),
        _ => throw new InvalidOperationException(operation.Kind)
    };

    private static string Persist(MatchSeries series) => JsonSerializer.Serialize(series.State, Json);

    private static MatchSeries Cold(MatchSeries series, long seed = Seed) =>
        MatchSeries.Restore(JsonSerializer.Deserialize<MatchSeriesState>(Persist(series), Json)!, seed);

    private static void AssertRecoveredAndReplayed(MatchSeries expected, List<Operation> history)
    {
        var recovered = Cold(expected);
        var transcript = JsonSerializer.Deserialize<Operation[]>(JsonSerializer.Serialize(history, Json), Json)!;
        var replayed = transcript.Aggregate(MatchSeries.Create(3, Players, Seed), Apply);
        foreach (var actual in new[] { recovered, replayed })
        {
            Assert.Equal(Persist(expected), Persist(actual));
            Assert.Equal(MatchStateHasher.HashValue(expected.State), MatchStateHasher.HashValue(actual.State));
            Assert.Equal(expected.Phase, actual.Phase);
            Assert.Equal(expected.WinnerPlayerId, actual.WinnerPlayerId);
            Assert.Equal(expected.PlayOrderChooserPlayerId, actual.PlayOrderChooserPlayerId);
            Assert.Equal(expected.FirstPlayerId, actual.FirstPlayerId);
            Assert.Equal(expected.Wins.OrderBy(entry => entry.Key), actual.Wins.OrderBy(entry => entry.Key));
        }
    }
}
