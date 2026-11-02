using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class SealedSubmittedOpeningTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SkippedFirstGameConsumesNoBattlefieldAndLocksInitialConfigurationThroughRecovery(bool submitted)
    {
        var run = new Run(3);
        await run.Join("alice");
        await run.Join("bob");
        var decks = new Dictionary<string, OfficialDecklist>();
        if (submitted)
        {
            await run.Choose();
            foreach (var player in new[] { "alice", "bob" })
            {
                decks[player] = await DeckWithRealBattlefields(run, player, 1);
                await run.Act(player, Command(decks[player]));
            }
        }
        var decision = new TournamentGameLossDecision(1, "bob");
        var result = await Award(run, "skip-first", decision);
        Assert.True(run.State.SeriesState!.Games[0].Skipped);
        Assert.False(run.State.SeriesState.Games[0].Started);
        Assert.Null(run.State.SealedBattlefieldHistory);
        Assert.All(result.Snapshots["alice"].Series!.UsedBattlefields.Values, Assert.Empty);
        Assert.Equal(1, result.Snapshots["alice"].Series!.Wins["alice"]);
        Assert.Empty(run.State.LastCompletedSeriesGame!.State.CardObjects);
        Assert.Equal("bob", result.Snapshots["alice"].Series!.PlayOrderChooserPlayerId);
        var adjudicator = Assert.IsAssignableFrom<IMatchTournamentAdjudicator>(run.Live);
        var count = run.Journal.Entries.Count;
        Assert.Same(result, await adjudicator.ApplyGameLossAsync("skip-first", decision, default));
        Assert.Equal(count, run.Journal.Entries.Count);
        await run.Cold();
        await run.Choose();
        foreach (var player in new[] { "alice", "bob" })
        {
            // An unrevealed battlefield remains available; battlefield choice
            // is distinct from swapping the main configuration (403.9/406).
            var original = submitted ? decks[player] : await DeckWithRealBattlefields(run, player, 1);
            await run.Act(player, Command(original));
            await run.Cold();
            var changed = Command(await Deck(run.State.PlayerSealedPools![player], true, false, 1));
            var before = MatchStateHasher.Hash(run.State);
            var rejected = await run.Live.SubmitDeckAsync(player, "skip-no-sideboard-" + player, changed, Raw(changed), default);
            Assert.False(rejected.Accepted);
            Assert.Contains("完全跳过", rejected.ErrorMessage);
            Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
            await run.Cold();
        }
        await run.Act("alice", new ReadyCommand());
        await run.Act("bob", new ReadyCommand());
        Assert.Equal(2, Assert.Single(run.State.SealedBattlefieldHistory!).GameNumber);
        Assert.False(run.State.SealedBattlefieldHistory![0].ReusesPrevious);
        await run.Cold();
        await run.Act("bob", new SurrenderCommand());
        Assert.Equal(MatchSeriesPhases.Finished, run.Live.SnapshotFor("alice").Series!.Phase);
        Assert.Equal(2, run.Live.SnapshotFor("alice").Series!.Wins["alice"]);
        await run.Cold();
    }

    [Fact]
    public async Task GameLossAfterBattlefieldRevealConsumesTheCopyAndDecisionIdCannotTargetAnotherGameOrPlayer()
    {
        var run = new Run(3);
        await run.Join("alice");
        await run.Join("bob");
        await run.Choose();
        var deck = await DeckWithRealBattlefields(run, "alice", 1);
        await run.Act("alice", Command(deck));
        await run.Act("bob", Command(await Deck(run.State.PlayerSealedPools!["bob"], false, false, 1)));
        await run.Act("alice", new ReadyCommand());
        await run.Act("bob", new ReadyCommand());
        Assert.Equal(1, run.State.PlayerSealedPools!["alice"].Pool[deck.Battlefields[0]]);
        await Award(run, "shown-loss", new(1, "bob"));
        Assert.False(run.State.SeriesState!.Games[0].Skipped);
        Assert.True(run.State.SeriesState.Games[0].Started);
        Assert.Equal(deck.Battlefields[0], Assert.Single(run.Live.SnapshotFor("bob").Series!.UsedBattlefields["alice"]));
        await run.Cold();
        var before = MatchStateHasher.Hash(run.State);
        var adjudicator = Assert.IsAssignableFrom<IMatchTournamentAdjudicator>(run.Live);
        var conflict = await adjudicator.ApplyGameLossAsync("shown-loss", new(2, "alice"), default);
        Assert.False(conflict.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Equal(before, MatchStateHasher.Hash(conflict.State));
        var stale = await adjudicator.ApplyGameLossAsync("stale-loss", new(1, "bob"), default);
        Assert.False(stale.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(stale.State));
        await run.Cold();
        await run.Choose();
        var command = Command(deck);
        var rejected = await run.Live.SubmitDeckAsync("alice", "shown-copy-exhausted", command, Raw(command), default);
        Assert.False(rejected.Accepted);
        Assert.Contains("每张实体只能使用一次", rejected.ErrorMessage);
        await run.Cold();
        // Regular changes remain legal after an actual revealed game loss.
        await run.Act("alice", Command(await Deck(run.State.PlayerSealedPools!["alice"], true, true, 1)));
        await run.Cold();
    }

    [Fact]
    public async Task FailedPenaltyCommitDoesNotInstallWinSkippedMarkerOrDecisionCache()
    {
        var run = new Run(3);
        await run.Join("alice");
        await run.Join("bob");
        var adjudicator = Assert.IsAssignableFrom<IMatchTournamentAdjudicator>(run.Live);
        var before = MatchStateHasher.HashValue(run.Live.SnapshotFor("alice"));
        run.Journal.FailNext = true;
        await Assert.ThrowsAsync<IOException>(() => adjudicator.ApplyGameLossAsync("retry-penalty", new(1, "bob"), default).AsTask());
        Assert.Equal(before, MatchStateHasher.HashValue(run.Live.SnapshotFor("alice")));
        Assert.Single(run.State.SeriesState!.Games);
        await Award(run, "retry-penalty", new(1, "bob"));
        await run.Cold();
    }

    [Fact]
    public async Task PlayerWireCannotDeclareAnOpponentGameLossOrReplayATrustedDecision()
    {
        var run = new Run(3);
        await run.Join("alice");
        await run.Join("bob");
        var wire = JsonSerializer.SerializeToElement(new { cmdType = TournamentGameLossDecision.CommandType, gameNumber = 1, loserPlayerId = "bob" });
        var mapped = Assert.IsType<UnsupportedCommand>(GameCommandJsonMapper.Map(wire));
        var before = MatchStateHasher.Hash(run.State);
        var count = run.Journal.Entries.Count;
        var rejected = await run.Live.SubmitAsync("alice", "forged-penalty", mapped, wire, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, rejected.ErrorCode);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(count, run.Journal.Entries.Count);
        await Award(run, "trusted-id", new(1, "bob"));
        await run.Cold();
        before = MatchStateHasher.Hash(run.State);
        rejected = await run.Live.SubmitAsync("bob", "trusted-id", mapped, null, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCommand, rejected.ErrorCode);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task ConstructedRoomRejectsServerGameLossAndKeepsItsOriginalState()
    {
        var session = new MatchSession("constructed-no-penalty-entry", new CoreRuleEngine());
        await session.EnsurePlayerAsync("alice", default);
        await session.EnsurePlayerAsync("bob", default);
        var before = MatchStateHasher.HashValue(session.SnapshotFor("alice"));
        var rejected = await session.ApplyGameLossAsync("constructed-penalty", new(1, "bob"), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.PhaseNotAllowed, rejected.ErrorCode);
        Assert.Equal(before, MatchStateHasher.HashValue(session.SnapshotFor("alice")));
    }

    [Fact]
    public async Task RecoveryRejectsAWireReplacementMissingDecisionAndChangedDecisionPlayer()
    {
        var run = new Run(3);
        await run.Join("alice");
        await run.Join("bob");
        await Award(run, "protected-decision", new(1, "bob"));
        var frame = run.Frame();
        var command = frame.Commands[^1];
        var wire = JsonSerializer.SerializeToElement(new { cmdType = TournamentGameLossDecision.CommandType, gameNumber = 1, loserPlayerId = "bob" });
        foreach (var altered in new[]
        {
            command with { RawCommand = wire, TypedCommand = null },
            command with { TypedCommand = null },
            command with { PlayerId = "alice" },
            command with { TypedCommand = new(TournamentGameLossDecision.CommandType,
                JsonSerializer.SerializeToElement(new { gameNumber = 1, loserPlayerId = "bob", injected = true })) }
        })
        {
            var forged = frame with { Commands = frame.Commands.SkipLast(1).Append(altered).ToArray() };
            Assert.NotEmpty(MatchRecoveryValidator.Validate(forged.RoomId, forged.LastEventSequence, forged.Commands, forged.Events,
                forged.PlayerViews, forged.AuthoritativeState, forged.CurrentTick));
            Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forged, new CoreRuleEngine(), default));
        }
        await run.Cold();
    }

    private static async Task<ResolutionResult> Award(Run run, string id, TournamentGameLossDecision decision)
    {
        var result = await Assert.IsAssignableFrom<IMatchTournamentAdjudicator>(run.Live).ApplyGameLossAsync(id, decision, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        return result;
    }
}
