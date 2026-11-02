using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class SealedSubmittedOpeningTests
{
    [Fact]
    public async Task RealBattlefieldsStaySecretUntilBothPlayersAreReadyAndTheirPrintedCopiesAreConsumed()
    {
        var run = new Run(3);
        await run.Join("alice");
        await run.Join("bob");
        await run.Choose();
        var decks = new Dictionary<string, OfficialDecklist>();
        foreach (var player in new[] { "alice", "bob" })
        {
            decks[player] = await DeckWithRealBattlefields(run, player, 1);
            var submitted = await run.Act(player, Command(decks[player]));
            Assert.Null(submitted.State.SealedBattlefieldHistory);
            foreach (var snapshot in submitted.Snapshots.Values.Append(ResolutionResult.BuildSpectatorSnapshot(submitted.State)))
                Assert.All(snapshot.Series!.UsedBattlefields.Values, Assert.Empty);
            var other = player == "alice" ? "bob" : "alice";
            var publicData = JsonSerializer.Serialize(new { snapshot = submitted.Snapshots[other] with { SealedPool = null },
                prompt = submitted.Prompts[other], submitted.Events }, Json);
            Assert.DoesNotContain(JsonSerializer.Serialize(decks[player].Battlefields[0], Json), publicData);
            await run.Cold();
        }
        await run.Act("alice", new ReadyCommand());
        Assert.Null(run.State.SealedBattlefieldHistory);
        await run.Cold();
        var opened = await run.Act("bob", new ReadyCommand());
        var record = Assert.Single(opened.State.SealedBattlefieldHistory!);
        Assert.False(record.ReusesPrevious);
        foreach (var player in decks.Keys)
        {
            Assert.Equal(decks[player].Battlefields[0], record.Cards[player]);
            Assert.Equal(record.Cards[player], Assert.Single(opened.Snapshots["alice"].Series!.UsedBattlefields[player]));
        }
        await run.Cold();
        await run.Act("bob", new SurrenderCommand());
        await run.Choose();
        // The controlled six-pack pool has a single copy of this battlefield.
        var oneCopy = decks.Keys.First(player => run.State.PlayerSealedPools![player].Pool[decks[player].Battlefields[0]] == 1);
        var before = MatchStateHasher.Hash(run.State);
        var command = Command(decks[oneCopy]);
        var rejected = await run.Live.SubmitDeckAsync(oneCopy, "exhausted-battlefield", command, Raw(command), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, rejected.ErrorCode);
        Assert.Contains("每张实体只能使用一次", rejected.ErrorMessage);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await run.Cold();
        // An unused blank remains legal after the physical copy is exhausted.
        await run.Act(oneCopy, Command(decks[oneCopy] with { Battlefields = [SealedDeckRules.BlankBattlefieldCardNo] }));
        await run.Cold();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task DrawLocksDeckAndActualSelectedBattlefieldAcrossColdRecoveryAndReplay(int bestOf)
    {
        // A deterministic terminal adapter supplies the draw; ordinary opening,
        // deck admission, series orchestration, persistence and replay are real.
        var run = new Run(bestOf, new DrawFirstGameEngine());
        await run.Join("alice");
        await run.Join("bob");
        await run.Choose();
        var decks = new Dictionary<string, OfficialDecklist>();
        foreach (var player in new[] { "alice", "bob" })
        {
            decks[player] = await DeckWithRealBattlefields(run, player, bestOf == 3 ? 1 : 3);
            await run.Act(player, Command(decks[player]));
        }
        await run.Act("alice", new ReadyCommand());
        await run.Act("bob", new ReadyCommand());
        var first = run.State.SealedBattlefieldHistory![0];
        await run.Act("bob", new SurrenderCommand());
        Assert.Equal(MatchSeriesPhases.SubmittingDecks, run.Live.SnapshotFor("alice").Series!.Phase);
        await run.Cold();
        foreach (var player in decks.Keys)
        {
            var altered = await Deck(run.State.PlayerSealedPools![player], true, false, bestOf == 3 ? 1 : 3);
            altered = altered with { Battlefields = decks[player].Battlefields };
            var command = Command(altered);
            var before = MatchStateHasher.Hash(run.State);
            var rejected = await run.Live.SubmitDeckAsync(player, "draw-deck-change-" + player, command, Raw(command), default);
            Assert.False(rejected.Accepted);
            Assert.Contains("平局后不能更换备牌", rejected.ErrorMessage);
            Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
            var changedBattlefield = Command(decks[player] with
            { Battlefields = Enumerable.Repeat(SealedDeckRules.BlankBattlefieldCardNo, bestOf == 3 ? 1 : 3).ToArray() });
            rejected = await run.Live.SubmitDeckAsync(player, "draw-field-change-" + player, changedBattlefield, Raw(changedBattlefield), default);
            Assert.False(rejected.Accepted);
            Assert.Contains("平局后不能更换备牌", rejected.ErrorMessage);
            // Configuration compares quantities, so input array order is irrelevant.
            await run.Act(player, Command(decks[player] with { MainDeck = decks[player].MainDeck.Reverse().ToArray() }));
            await run.Cold();
        }
        await run.Act("alice", new ReadyCommand());
        await run.Cold();
        var cursor = run.State.RngCursor;
        await run.Act("bob", new ReadyCommand());
        // Only main and rune shuffles consume randomness: neither player
        // randomly selects another battlefield after a draw, even in bestOf=1.
        Assert.Equal(cursor + 4, run.State.RngCursor);
        var second = run.State.SealedBattlefieldHistory![1];
        Assert.True(second.ReusesPrevious);
        Assert.Equal(first.Cards, second.Cards);
        foreach (var player in decks.Keys)
        {
            var objectId = Assert.Single(run.State.PlayerZones[player].Battlefields);
            Assert.Equal(first.Cards[player], run.State.CardObjects[objectId].CardNo);
            Assert.Single(run.Live.SnapshotFor(player).Series!.UsedBattlefields[player]);
        }
        await run.Cold();
    }

    [Fact]
    public async Task TwoRealPrintedCopiesPermitTwoGamesAndFailedOpeningCommitCannotConsumeTheSecond()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var cards = catalog.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        Run? run = null;
        string? battlefield = null;
        // Find an ordinary private server seed yielding two physical copies;
        // no test installs a client-supplied or replacement authoritative pool.
        for (var seed = 0; seed < 64 && battlefield is null; seed++)
        {
            var candidate = new Run(3, seed: seed);
            await candidate.Join("alice");
            await candidate.Join("bob");
            battlefield = candidate.State.PlayerSealedPools!["alice"].Pool
                .Where(pair => pair.Value == 2 && cards[pair.Key].CardCategoryName == "战场")
                .Select(pair => pair.Key).Order(StringComparer.Ordinal).FirstOrDefault();
            if (battlefield is not null) run = candidate;
        }
        Assert.NotNull(run);
        Assert.NotNull(battlefield);
        await run.Choose();
        for (var game = 1; game <= 2; game++)
        {
            foreach (var player in new[] { "alice", "bob" })
            {
                var deck = await Deck(run.State.PlayerSealedPools![player], false, false, 1);
                if (player == "alice") deck = deck with { Battlefields = [battlefield] };
                await run.Act(player, Command(deck));
                await run.Cold();
            }
            await run.Act("alice", new ReadyCommand());
            if (game == 2)
            {
                var before = MatchStateHasher.HashValue(run.Live.SnapshotFor("alice"));
                run.Journal.FailNext = true;
                await Assert.ThrowsAsync<IOException>(() => run.Act("bob", new ReadyCommand()));
                Assert.Equal(before, MatchStateHasher.HashValue(run.Live.SnapshotFor("alice")));
                Assert.Single(run.State.SealedBattlefieldHistory!);
                await run.Cold();
            }
            await run.Act("bob", new ReadyCommand());
            Assert.Equal(game, run.Live.SnapshotFor("bob").Series!.UsedBattlefields["alice"].Count);
            await run.Act(game == 1 ? "bob" : "alice", new SurrenderCommand());
            await run.Cold();
            await run.Choose();
        }
        var lastDeck = await Deck(run.State.PlayerSealedPools!["alice"], false, false, 1);
        var exhausted = Command(lastDeck with { Battlefields = [battlefield] });
        var rejected = await run.Live.SubmitDeckAsync("alice", "third-use", exhausted, Raw(exhausted), default);
        Assert.False(rejected.Accepted);
        Assert.Contains("已使用 2 张，卡池共 2 张", rejected.ErrorMessage);
        await run.Cold();
    }

    [Fact]
    public async Task RecoveryRejectsMissingChangedAndDuplicateConsumptionRecords()
    {
        var run = new Run(3);
        await run.Join("alice");
        await run.Join("bob");
        await run.Choose();
        await OpenCurrentGame(run, 3);
        var state = run.State;
        var selection = state.SealedBattlefieldHistory![0];
        foreach (var altered in new[]
        {
            state with { SealedBattlefieldHistory = null },
            state with { SealedBattlefieldHistory = [selection with { ReusesPrevious = true }] },
            state with { SealedBattlefieldHistory = [selection, selection] },
            state with { SealedBattlefieldHistory = [selection with { Cards = new Dictionary<string, string> { ["alice"] = "OGN·999" } }] }
        })
        {
            Assert.NotEmpty(RoomMatchSeries.Validate(altered));
            var error = Assert.Throws<MatchSessionException>(() => new MatchSession(altered, new CoreRuleEngine(), NoopMatchJournal.Instance));
            Assert.Equal(ErrorCodes.RecoveryInconsistent, error.Code);
        }
        await run.Cold();
    }

    private static async Task<OfficialDecklist> DeckWithRealBattlefields(Run run, string player, int count)
    {
        var pool = run.State.PlayerSealedPools![player];
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var cards = catalog.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        var real = pool.Pool.Keys.Where(no => cards[no].CardCategoryName == "战场")
            .OrderBy(no => pool.Pool[no]).ThenBy(no => no, StringComparer.Ordinal).First();
        var deck = await Deck(pool, false, false, count);
        return deck with { Battlefields = new[] { real }.Concat(Enumerable.Repeat(SealedDeckRules.BlankBattlefieldCardNo, count - 1)).ToArray() };
    }

    private sealed class DrawFirstGameEngine : IRuleEngine
    {
        private readonly IRuleEngine core = new CoreRuleEngine();
        public async ValueTask<ResolutionResult> ResolveAsync(MatchState state, PlayerIntent intent, GameCommand command, CancellationToken cancellationToken)
        {
            var result = await core.ResolveAsync(state, intent, command, cancellationToken);
            return command is SurrenderCommand && state.SeriesState!.Games.Count == 1 && result.Accepted
                ? result with { State = result.State with { WinnerPlayerId = null }, Events = [new GameEvent("GAME_DRAWN", "本局平局", new Dictionary<string, object?>())] }
                : result;
        }
    }
}
