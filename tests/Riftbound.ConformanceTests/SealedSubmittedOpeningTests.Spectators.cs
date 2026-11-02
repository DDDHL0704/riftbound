using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class SealedSubmittedOpeningTests
{
    [Theory]
    [InlineData("OGN", 1)]
    [InlineData("SFD", 3)]
    public async Task SpectatorNamedPlayerKeepsTheirHandPrivateAcrossOpeningRecoveryAndReplay(string setCode, int bestOf)
    {
        var run = new Run(bestOf, setCode: setCode);
        await run.Join("__spectator__");
        await run.Join("bob");
        // Also runs on the private-pool stage, before series orchestration is connected.
        if (run.Live.SnapshotFor("bob").Series is { PlayOrderChooserPlayerId: { } chooser })
        {
            var raw = JsonSerializer.SerializeToElement(new { cmdType = "CHOOSE_PLAY_ORDER", goFirst = true }, Json);
            await run.Act(chooser, GameCommandJsonMapper.Map(raw), raw);
        }
        foreach (var player in run.State.Seats.Keys)
            await run.Act(player, Command(await Deck(run.State.PlayerSealedPools![player], false, false, bestOf == 3 ? 1 : 3)));
        await run.Act("__spectator__", new ReadyCommand());
        await run.Act("bob", new ReadyCommand());
        AssertSpectatorCannotBecomeASeatedPlayer(run.State);
        var replay = MatchReplayRedactor.BuildSpectatorFrame(run.Journal.Entries[^1]);
        AssertSpectatorCannotBecomeASeatedPlayer(run.State, replay.SpectatorSnapshot);
        await run.Cold();
        AssertSpectatorCannotBecomeASeatedPlayer(run.State);
        var frame = run.Frame();
        Assert.Empty(MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            frame.PlayerViews, frame.AuthoritativeState, frame.CurrentTick, replay));
    }

    [Fact]
    public async Task ConstructedSpectatorNamedPlayerKeepsTheirOwnViewWhileSpectatorsSeeNoHand()
    {
        var journal = new Journal();
        var initial = MatchState.Create("constructed-spectator-identity") with { Seed = 424242,
            SessionOptions = new(AllowLegacyReadyWithoutDeck: false, DeckFormat: OfficialDeckFormat.ChinaStandard20260724) };
        var live = new MatchSession(initial, new CoreRuleEngine(), journal, NoopMatchPlayerStore.Instance);
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var original = PreconstructedDeckCatalog.Build(catalog)[0].Decklist;
        var battlefields = catalog.Cards.Where(card => card.CardCategoryName == "战场"
                && ChinaStandardBanList.IsAllowed(card, OfficialDeckFormat.ChinaStandard20260724))
            .DistinctBy(card => (card.CardName, card.SubTitle)).Take(3).Select(card => card.CardNo).ToArray();
        var deck = Command(original with { Battlefields = battlefields });
        foreach (var player in new[] { "__spectator__", "bob" })
        {
            await live.EnsurePlayerAsync(player, default);
            var result = await live.SubmitDeckAsync(player, "deck", deck, Raw(deck), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        Assert.True((await live.ReadyAsync("__spectator__", "ready", null, default)).Accepted);
        Assert.True((await live.ReadyAsync("bob", "ready", null, default)).Accepted);
        AssertSpectatorCannotBecomeASeatedPlayer(journal.Entries[^1].AuthoritativeState);
    }

    private static void AssertSpectatorCannotBecomeASeatedPlayer(MatchState state, SnapshotDto? spectator = null)
    {
        spectator ??= ResolutionResult.BuildSpectatorSnapshot(state);
        Assert.Null(spectator.SealedPool);
        Assert.Equal("__spectator__", spectator.Table!.ViewerPlayerId);
        foreach (var player in state.Seats.Keys)
        {
            var hand = state.PlayerZones[player].Hand;
            Assert.NotEmpty(hand);
            var own = ResolutionResult.BuildSnapshots(state)[player];
            Assert.Equal(hand, own.Table!.Players.Single(item => item.PlayerId == player).Zones.Hand);
            var publicPlayer = spectator.Table.Players.Single(item => item.PlayerId == player);
            Assert.False(publicPlayer.IsViewer);
            Assert.Equal("spectator", publicPlayer.Perspective);
            Assert.Empty(publicPlayer.Zones.Hand);
            Assert.Equal(hand.Count, publicPlayer.Zones.HandHidden);
            var json = JsonSerializer.Serialize(spectator, Json);
            foreach (var card in hand)
                Assert.DoesNotContain(card, json);
        }
    }
}
