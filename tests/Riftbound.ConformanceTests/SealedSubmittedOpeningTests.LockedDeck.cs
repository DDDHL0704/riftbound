using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class SealedSubmittedOpeningTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task SkippedOpeningProjectsOnlyTheViewersLockedDeckAndReconnectNeedsNoLocalConfiguration(int bestOf)
    {
        const string owner = "__spectator__";
        var run = new Run(bestOf);
        await run.Join(owner);
        await run.Join("bob");
        var chooser = run.Live.SnapshotFor(owner).Series!.PlayOrderChooserPlayerId!;
        await run.Act(chooser, new ChoosePlayOrderCommand(true));
        var count = bestOf == 3 ? 1 : 3;
        var initial = await Deck(run.State.PlayerSealedPools![owner], true, true, count);
        var physical = await DeckWithRealBattlefields(run, owner, count);
        initial = initial with { Battlefields = physical.Battlefields };
        await run.Act(owner, Command(initial));
        AssertLockedDeckViews(run, new Dictionary<string, OfficialDecklist>());
        await Award(run, "skip-with-one-private-deck", new(1, "bob"));
        var expected = new Dictionary<string, OfficialDecklist>();
        if (bestOf == 3) expected[owner] = initial;
        AssertLockedDeckViews(run, expected);
        await run.Cold();
        AssertLockedDeckViews(run, expected);
        if (bestOf == 1)
        {
            Assert.Equal(MatchSeriesPhases.Finished, run.Live.SnapshotFor(owner).Series!.Phase);
            return;
        }

        await run.Act("bob", new ChoosePlayOrderCommand(false));
        AssertLockedDeckViews(run, expected);
        // Nothing was registered for bob before the skipped opening. Their
        // first legal submission becomes locked without exposing the other deck.
        var bob = await Deck(run.State.PlayerSealedPools!["bob"], false, false, count);
        await run.Act("bob", Command(bob));
        expected["bob"] = bob;
        await run.Cold();
        AssertLockedDeckViews(run, expected);

        // A skipped game never consumed or revealed the original battlefield;
        // its replacement remains legal and is included on the next device.
        var recovered = run.Live.SnapshotFor(owner).Series!.LockedDeck!;
        await run.Act(owner, new SubmitDeckCommand(recovered.LegendCardNo, recovered.ChampionCardNo,
            recovered.MainDeck, recovered.RuneDeck, [SealedDeckRules.BlankBattlefieldCardNo]));
        expected[owner] = initial with { Battlefields = [SealedDeckRules.BlankBattlefieldCardNo] };
        await run.Cold();
        AssertLockedDeckViews(run, expected);
        await run.Act(owner, new ReadyCommand());
        await run.Cold();
        AssertLockedDeckViews(run, expected);
        await run.Act("bob", new ReadyCommand());
        AssertLockedDeckViews(run, new Dictionary<string, OfficialDecklist>());
        await run.Act("bob", new SurrenderCommand());
        Assert.Equal(MatchSeriesPhases.Finished, run.Live.SnapshotFor(owner).Series!.Phase);
        await run.Cold();
        AssertLockedDeckViews(run, new Dictionary<string, OfficialDecklist>());
    }

    private static void AssertLockedDeckViews(Run run, IReadOnlyDictionary<string, OfficialDecklist> expected)
    {
        foreach (var player in run.State.Seats.Keys)
        {
            var snapshot = run.Live.SnapshotFor(player);
            var locked = snapshot.Series!.LockedDeck;
            if (expected.TryGetValue(player, out var deck))
            {
                Assert.NotNull(locked);
                Assert.Equal(JsonSerializer.Serialize(deck, Json), JsonSerializer.Serialize(locked, Json));
                var wire = JsonSerializer.SerializeToElement(snapshot, Json).GetProperty("series").GetProperty("lockedDeck");
                Assert.Equal(new[] { "battlefields", "championCardNo", "legendCardNo", "mainDeck", "runeDeck" },
                    wire.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
            }
            else
            {
                Assert.Null(locked);
                Assert.False(JsonSerializer.SerializeToElement(snapshot, Json).GetProperty("series").TryGetProperty("lockedDeck", out _));
            }
        }
        foreach (var snapshot in new[] { ResolutionResult.BuildSpectatorSnapshot(run.State),
            MatchReplayRedactor.BuildSpectatorFrame(run.Journal.Entries[^1]).SpectatorSnapshot })
        {
            Assert.Null(snapshot.Series!.LockedDeck);
            Assert.False(JsonSerializer.SerializeToElement(snapshot, Json).GetProperty("series").TryGetProperty("lockedDeck", out _));
        }
        Assert.Null(RoomMatchSeries.ViewFor(run.State)?.LockedDeck);
        Assert.Null(RoomMatchSeries.ViewFor(run.State, "unseated-viewer")?.LockedDeck);
    }

    private static void AssertLockedDeckRecoveryRejectsLeaks(Run run)
    {
        var frame = run.Frame();
        var alice = frame.PlayerViews["alice"].Snapshot.Series!.LockedDeck!;
        var bob = frame.PlayerViews["bob"].Snapshot.Series!.LockedDeck!;
        Assert.NotEqual(JsonSerializer.Serialize(alice, Json), JsonSerializer.Serialize(bob, Json));
        foreach (var altered in new SealedLockedDeckDto?[] { null, bob,
            alice with { MainDeck = alice.MainDeck.Skip(1).ToArray() },
            alice with { Battlefields = ["OGN·999"] } })
        {
            var views = frame.PlayerViews.ToDictionary(pair => pair.Key, pair => pair.Value);
            var view = views["alice"];
            views["alice"] = view with { Snapshot = view.Snapshot with
                { Series = view.Snapshot.Series! with { LockedDeck = altered } } };
            Assert.NotEmpty(Errors(frame with { PlayerViews = views }));
        }
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(run.Journal.Entries[^1]);
        spectator = spectator with { SpectatorSnapshot = spectator.SpectatorSnapshot with
            { Series = spectator.SpectatorSnapshot.Series! with { LockedDeck = alice } } };
        Assert.NotEmpty(Errors(frame with { SpectatorReplayFrame = spectator }));

        static IReadOnlyList<string> Errors(MatchRecoveryFrame candidate) => MatchRecoveryValidator.Validate(
            candidate.RoomId, candidate.LastEventSequence, candidate.Commands, candidate.Events,
            candidate.PlayerViews, candidate.AuthoritativeState, candidate.CurrentTick, candidate.SpectatorReplayFrame);
    }
}
