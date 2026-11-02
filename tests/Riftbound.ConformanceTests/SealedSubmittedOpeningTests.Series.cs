using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class SealedSubmittedOpeningTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(3, true)]
    [InlineData(3, false)]
    public async Task OnlyTheDesignatedPlayerCanChooseAndCoreOpeningUsesThatChoice(int bestOf, bool goFirst)
    {
        var run = new Run(bestOf);
        await run.Join("alice");
        Assert.False(run.Live.PromptFor("alice").Actionable);
        Assert.Null(run.Live.SnapshotFor("alice").Series);
        await run.Join("bob");
        var series = run.Live.SnapshotFor("alice").Series!;
        Assert.Equal(MatchSeriesPhases.ChoosingPlayOrder, series.Phase);
        Assert.Equal(1, series.GameNumber);
        Assert.All(series.Wins.Values, wins => Assert.Equal(0, wins));
        var chooser = series.PlayOrderChooserPlayerId!;
        var other = chooser == "alice" ? "bob" : "alice";
        var prompt = run.Live.PromptFor(chooser);
        Assert.True(prompt.Actionable);
        var candidate = Assert.Single(prompt.Candidates!);
        Assert.Equal(CommandTypes.ChoosePlayOrder, candidate.Action);
        Assert.True(candidate.Enabled);
        Assert.DoesNotContain(run.Live.PromptFor(other).Candidates!, item => item.Action == CommandTypes.ChoosePlayOrder);
        await run.Cold();
        var cursor = run.State.RngCursor;
        var raw = JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.ChoosePlayOrder, goFirst,
            promptId = prompt.PromptId, snapshotTick = prompt.SnapshotTick });
        var chosen = await run.Live.SubmitAsync(chooser, "choose-first-game", new ChoosePlayOrderCommand(goFirst), raw, default);
        Assert.True(chosen.Accepted, chosen.ErrorMessage);
        Assert.Equal(cursor, chosen.State.RngCursor);
        Assert.Equal(MatchSeriesPhases.SubmittingDecks, chosen.Snapshots[chooser].Series!.Phase);
        var repeated = await run.Live.SubmitAsync(chooser, "choose-first-game", new ChoosePlayOrderCommand(goFirst), raw, default);
        Assert.Same(chosen, repeated);
        await run.Cold();
        var restoredRepeat = await run.Live.SubmitAsync(chooser, "choose-first-game", new ChoosePlayOrderCommand(goFirst), raw, default);
        Assert.True(restoredRepeat.Accepted);
        var conflict = await run.Live.SubmitAsync(chooser, "choose-first-game", new ChoosePlayOrderCommand(!goFirst),
            Raw(new ChoosePlayOrderCommand(!goFirst)), default);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        var opened = await OpenCurrentGame(run, bestOf);
        Assert.Equal(goFirst ? chooser : other, opened.State.TurnPlayerId);
        Assert.Equal(goFirst ? other : chooser, opened.State.OpeningSecondActionPlayerId);
        Assert.Equal(MatchSeriesPhases.Playing, opened.Snapshots[chooser].Series!.Phase);
        Assert.Null(opened.Snapshots[chooser].Series!.PlayOrderChooserPlayerId);
        await run.Cold();
    }

    [Fact]
    public async Task WrongChooserAndStalePromptCannotMutateTheSeriesAndRejectedInputReplays()
    {
        var run = new Run();
        await run.Join("alice");
        await run.Join("bob");
        var chooser = run.Live.SnapshotFor("alice").Series!.PlayOrderChooserPlayerId!;
        var other = chooser == "alice" ? "bob" : "alice";
        var hash = MatchStateHasher.Hash(run.State);
        var command = new ChoosePlayOrderCommand(true);
        var rejected = await run.Live.SubmitAsync(other, "wrong-chooser", command, Raw(command), default);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        await run.Cold();
        var repeat = await run.Live.SubmitAsync(other, "wrong-chooser", command, Raw(command), default);
        Assert.Equal(rejected.ErrorMessage, repeat.ErrorMessage);
        var prompt = run.Live.PromptFor(chooser);
        await run.Choose();
        var stale = await run.Live.SubmitAsync(chooser, "stale-choice", command,
            JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.ChoosePlayOrder, goFirst = true,
                promptId = prompt.PromptId, snapshotTick = prompt.SnapshotTick }), default);
        Assert.Equal(ErrorCodes.PromptExpired, stale.ErrorCode);
        await run.Cold();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("\"true\"")]
    [InlineData("1")]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task PlayOrderRequiresAnExplicitBooleanAndNeverDefaultsMalformedInputToSecond(string? value)
    {
        var run = new Run();
        await run.Join("alice");
        await run.Join("bob");
        var chooser = run.Live.SnapshotFor("alice").Series!.PlayOrderChooserPlayerId!;
        var node = new JsonObject { ["cmdType"] = CommandTypes.ChoosePlayOrder };
        if (value is not null) node["goFirst"] = JsonNode.Parse(value);
        var raw = JsonSerializer.SerializeToElement(node, Json);
        var command = GameCommandJsonMapper.Map(raw);
        Assert.IsType<UnsupportedCommand>(command);
        var before = MatchStateHasher.Hash(run.State);
        var rejected = await run.Live.SubmitAsync(chooser, "malformed-order", command, raw, default);
        Assert.Equal(ErrorCodes.InvalidPayload, rejected.ErrorCode);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(2, run.Journal.Entries.Count);
        await run.Cold();
    }

    [Fact]
    public async Task FailedPlayOrderJournalCommitInstallsNeitherChoiceNorIntent()
    {
        var run = new Run();
        await run.Join("alice");
        await run.Join("bob");
        var chooser = run.Live.SnapshotFor("alice").Series!.PlayOrderChooserPlayerId!;
        var before = MatchStateHasher.HashValue(run.Live.SnapshotFor(chooser));
        var command = new ChoosePlayOrderCommand(false);
        run.Journal.FailNext = true;
        await Assert.ThrowsAsync<IOException>(() => run.Live.SubmitAsync(chooser, "failed-write", command, null, default).AsTask());
        Assert.Equal(before, MatchStateHasher.HashValue(run.Live.SnapshotFor(chooser)));
        Assert.Equal(2, run.Journal.Entries.Count);
        var retry = await run.Live.SubmitAsync(chooser, "failed-write", command, null, default);
        Assert.True(retry.Accepted, retry.ErrorMessage);
        Assert.Null(run.Journal.Entries[^1].RawCommand);
        Assert.NotNull(run.Journal.Entries[^1].TypedCommand);
        await run.Cold();
    }

    [Fact]
    public async Task ConstructedRoomRejectsTournamentOrderAndKeepsItsExistingOpeningFlow()
    {
        var live = new MatchSession("constructed-no-series", new CoreRuleEngine());
        await live.EnsurePlayerAsync("alice", default);
        await live.EnsurePlayerAsync("bob", default);
        var before = MatchStateHasher.HashValue(live.SnapshotFor("alice"));
        var command = new ChoosePlayOrderCommand(true);
        var rejected = await live.SubmitAsync("alice", "no-series", command, Raw(command), default);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal(before, MatchStateHasher.HashValue(live.SnapshotFor("alice")));
        Assert.Null(live.SnapshotFor("alice").Series);
    }

    [Fact]
    public async Task BestOfThreeWaitsForTwoWinsAndLoserChoosesEachLaterGameAcrossColdRecovery()
    {
        var run = new Run(3);
        await run.Join("alice");
        await run.Join("bob");
        await run.Choose(false);
        var originalPools = MatchStateHasher.HashValue(run.State.PlayerSealedPools);
        foreach (var loser in new[] { "bob", "alice", "bob" })
        {
            if (run.State.SeriesState!.Games.Count > 1)
            {
                var previousWinner = run.State.SeriesState.Games[^2].WinnerPlayerId!;
                var previousLoser = previousWinner == "alice" ? "bob" : "alice";
                Assert.Equal(previousLoser, run.Live.SnapshotFor("alice").Series!.PlayOrderChooserPlayerId);
                await run.Cold();
                await run.Choose(false);
            }
            await OpenCurrentGame(run, 3);
            var game = run.Live.SnapshotFor("alice").Series!.GameNumber;
            var ended = await run.Act(loser, new SurrenderCommand());
            var series = ended.Snapshots["alice"].Series!;
            Assert.Equal(originalPools, MatchStateHasher.HashValue(run.State.PlayerSealedPools));
            Assert.Single(ended.Events, item => item.Kind == "SERIES_GAME_COMPLETED");
            var terminal = Assert.IsType<CompletedSeriesGame>(ended.State.LastCompletedSeriesGame);
            Assert.Equal(game, terminal.GameNumber);
            Assert.Equal(MatchStatuses.Finished, terminal.State.Status);
            Assert.NotEmpty(terminal.State.CardObjects);
            Assert.Null(terminal.State.PlayerSealedPools);
            Assert.Null(terminal.State.SeriesState);
            Assert.Null(terminal.State.LastCompletedSeriesGame);
            Assert.DoesNotContain("lastCompletedSeriesGame", JsonSerializer.Serialize(ended.Snapshots, Json));
            if (game < 3)
            {
                Assert.Equal(MatchStatuses.Seating, ended.State.Status);
                Assert.Equal(MatchSeriesPhases.ChoosingPlayOrder, series.Phase);
                Assert.Empty(ended.State.CardObjects);
                Assert.Empty(ended.State.PlayerDecklists);
                Assert.Empty(ended.State.ReadyPlayerIds);
                Assert.DoesNotContain(ended.Events, item => item.Kind == "MATCH_WON");
            }
            else
            {
                Assert.Equal(MatchStatuses.Finished, ended.State.Status);
                Assert.Equal(MatchSeriesPhases.Finished, series.Phase);
                Assert.Equal("alice", ended.State.WinnerPlayerId);
                Assert.Equal(2, series.Wins["alice"]);
                Assert.Equal(1, series.Wins["bob"]);
                Assert.Single(ended.Events, item => item.Kind == "MATCH_WON");
            }
            await run.Cold();
        }
    }

    [Fact]
    public async Task TamperedTournamentLedgerCannotBeInstalledAsARecoveryState()
    {
        var run = new Run(3);
        await run.Join("alice");
        await run.Join("bob");
        var state = run.State;
        foreach (var altered in new[]
        {
            state with { SeriesState = null },
            state with { SeriesState = state.SeriesState! with { BestOf = 1 } },
            state with { SeriesState = state.SeriesState! with { InitialPlayOrderChooserPlayerId = "outsider" } },
            state with { SeriesState = state.SeriesState! with { Games = [new(1, "alice", true)] } }
        })
        {
            Assert.NotEmpty(RoomMatchSeries.Validate(altered));
            var exception = Assert.Throws<MatchSessionException>(() => new MatchSession(altered, new CoreRuleEngine(), NoopMatchJournal.Instance));
            Assert.Equal(ErrorCodes.RecoveryInconsistent, exception.Code);
        }
        await run.Cold();
    }

    internal static async Task<ResolutionResult> OpenCurrentGame(Run run, int bestOf)
    {
        foreach (var player in new[] { "alice", "bob" })
        {
            var deck = await Deck(run.State.PlayerSealedPools![player], false, false, bestOf == 3 ? 1 : 3);
            await run.Act(player, Command(deck));
            await run.Cold();
        }
        await run.Act("alice", new ReadyCommand());
        var opened = await run.Act("bob", new ReadyCommand());
        Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
        return opened;
    }
}
