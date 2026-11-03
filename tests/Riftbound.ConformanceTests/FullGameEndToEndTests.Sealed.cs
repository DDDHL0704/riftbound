using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class FullGameEndToEndTests
{
    [Fact]
    public Task SealedBestOfOneWithoutRolesScoresToVictoryAndReplaysEveryGame() =>
        DriveSealedMatchAsync(1, "OGN", false, ["alice"]);

    [Fact]
    public Task SealedBestOfOneWithOptionalRolesScoresToVictoryAndReplaysEveryGame() =>
        DriveSealedMatchAsync(1, "SFD", true, ["bob"]);

    [Fact]
    public Task SealedBestOfThreeSideboardsAcrossReconnectAndReplaysEveryGame() =>
        DriveSealedMatchAsync(3, "OGN", false, ["alice", "bob", "alice"]);

    [Fact]
    public Task SealedBestOfThreeWithRolesScoresTwoWinsAndReplaysEveryGame() =>
        DriveSealedMatchAsync(3, "SFD", true, ["bob", "bob"]);

    private async Task DriveSealedMatchAsync(int bestOf, string setCode, bool withRoles, string[] winners)
    {
        // Every card comes from the real six packs. Opponents cooperate by
        // passing; all units, claims, holds and score victories use core rules.
        // Persisted JSONB object keys can return in a different order. Exercise
        // the original score games through that cold path, including live play.
        var run = new SealedSubmittedOpeningTests.Run(bestOf, setCode: setCode, reorderRecoveryObjects: true);
        await run.Join("alice");
        await run.Join("bob");
        var poolHash = MatchStateHasher.HashValue(run.State.PlayerSealedPools);
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var cards = catalog.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        OfficialDecklist? previousAliceDeck = null;
        for (var index = 0; index < winners.Length; index++)
        {
            var gameNumber = index + 1;
            var beforeGame = run.State;
            var journalStart = run.Journal.Entries.Count;
            if (index > 0)
            {
                var loser = winners[index - 1] == "alice" ? "bob" : "alice";
                Assert.Equal(loser, run.Live.SnapshotFor("alice").Series!.PlayOrderChooserPlayerId);
            }
            if (index == 0) await run.Cold();
            await run.Choose(index % 2 == 0);
            foreach (var player in new[] { "alice", "bob" })
            {
                // Change roles and the rune configuration legally between
                // games. This also exercises returning to an absent role.
                var legend = index % 2 == 0 ? withRoles && player == "alice" : !withRoles;
                var champion = index % 2 == 0 ? withRoles && player == "bob" : !withRoles;
                var deck = await SealedSubmittedOpeningTests.Deck(run.State.PlayerSealedPools![player],
                    legend, champion, bestOf == 3 ? 1 : 3);
                var traits = deck.MainDeck.Append(deck.LegendCardNo).Where(cards.ContainsKey)
                    .SelectMany(no => cards[no].CardColorList).Select(RuneTrait.Normalize)
                    .Where(trait => trait != "colorless").Distinct().Order(StringComparer.Ordinal).ToArray();
                var runes = traits.Select(trait => catalog.Cards.First(card => SealedDeckRules.IsBasicRune(card)
                    && card.CardColorList.Select(RuneTrait.Normalize).Contains(trait)).CardNo).ToArray();
                deck = deck with { RuneDeck = Enumerable.Range(0, 12).Select(n => runes[n % runes.Length]).ToArray() };
                Assert.True(OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.Sealed,
                    new(run.State.PlayerSealedPools![player].Pool, bestOf)).IsValid);
                if (player == "alice")
                {
                    if (previousAliceDeck is not null)
                        Assert.NotEqual(previousAliceDeck.LegendCardNo, deck.LegendCardNo);
                    previousAliceDeck = deck;
                }
                var submitted = await run.Act(player, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo,
                    deck.MainDeck, deck.RuneDeck, deck.Battlefields));
                AssertNoHiddenZoneLeak(submitted);
                // Freeze after only one hidden submission, then recover and
                // reconnect both players before the other can submit.
                // This test verifies each complete game's original action log
                // below; do not repeat every previous game at each checkpoint.
                if (player == "alice") await run.Cold(verifyReplay: false);
                Assert.Equal(MatchStateHasher.HashValue(deck), MatchStateHasher.HashValue(run.State.PlayerDecklists[player]));
                Assert.Equal(poolHash, MatchStateHasher.HashValue(run.State.PlayerSealedPools));
                Assert.Null(ResolutionResult.BuildSpectatorSnapshot(run.State).SealedPool);
            }
            await run.Act("alice", new ReadyCommand());
            var result = await run.Act("bob", new ReadyCommand());
            var first = result.State.TurnPlayerId;
            var second = result.State.Seats.Keys.Single(player => player != first);
            await run.Act(first, new MulliganCommand([]));
            result = await run.Act(second, new MulliganCommand([]));
            result = await DriveSealedGameToScoreAsync(run, result, winners[index], gameNumber);

            var terminal = Assert.IsType<CompletedSeriesGame>(result.State.LastCompletedSeriesGame);
            Assert.Equal(gameNumber, terminal.GameNumber);
            Assert.Equal(winners[index], terminal.State.WinnerPlayerId);
            Assert.True(terminal.State.PlayerScores[winners[index]] >= 8);
            Assert.Equal(MatchStatuses.Finished, terminal.State.Status);
            var entries = run.Journal.Entries.Skip(journalStart).ToArray();
            Assert.Contains(entries, entry => entry.CommandType == CommandTypes.PlayCard && entry.Accepted);
            Assert.Contains(entries, entry => entry.CommandType == CommandTypes.MoveUnit && entry.Accepted);
            Assert.Contains(entries.SelectMany(entry => entry.Events), item => item.Kind == "BATTLEFIELD_CONQUERED");
            Assert.Contains(entries.SelectMany(entry => entry.Events), item => item.Kind == "SCORE_GAINED");
            Assert.DoesNotContain(entries, entry => entry.CommandType is CommandTypes.Surrender or "TOURNAMENT_GAME_LOSS");
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(beforeGame,
                entries.Select(ToRecoveredCommand).ToArray(), result.State, new CoreRuleEngine(), default,
                ToRecoveredEvents(entries));
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            Assert.Equal(MatchStateHasher.Hash(result.State), replay.ReplayedStateHash);
            await run.Cold(verifyReplay: false);
            _output.WriteLine($"{setCode} bestOf={bestOf} game={gameNumber}: {entries.Length} journal entries, winner={winners[index]}, hash={replay.ReplayedStateHash}");
            if (index + 1 < winners.Length)
            {
                Assert.Equal(MatchStatuses.Seating, result.State.Status);
                Assert.DoesNotContain(result.Events, item => item.Kind == "MATCH_WON");
            }
        }
        Assert.Equal(MatchStatuses.Finished, run.State.Status);
        var series = run.Live.SnapshotFor("alice").Series!;
        Assert.Equal(MatchSeriesPhases.Finished, series.Phase);
        Assert.Equal(bestOf == 3 ? 2 : 1, series.Wins[run.State.WinnerPlayerId!]);
        Assert.Single(run.Journal.Entries.SelectMany(entry => entry.Events), item => item.Kind == "MATCH_WON");
        Assert.Equal(poolHash, MatchStateHasher.HashValue(run.State.PlayerSealedPools));
        Assert.All(run.Journal.Entries, entry => Assert.Null(ResolutionResult.BuildSpectatorSnapshot(entry.AuthoritativeState).SealedPool));
    }

    private static async Task<ResolutionResult> DriveSealedGameToScoreAsync(
        SealedSubmittedOpeningTests.Run run, ResolutionResult current, string winner, int gameNumber)
    {
        var result = current;
        var recoveredInGame = false;
        for (var step = 0; step < 64 && !run.State.SeriesState!.Games[gameNumber - 1].Completed; step++)
        {
            var session = Assert.IsType<MatchSession>(run.Live);
            var prefix = $"sealed-{gameNumber}-{step}";
            result = await PassOpenSpellDuelAsync(session, result, prefix + "-resolve");
            if (run.State.SeriesState!.Games[gameNumber - 1].Completed) break;
            var actor = result.State.ActivePlayerId;
            if (actor == winner && !result.State.BattlefieldStates.Values.Any(field => field.ControllerId == winner))
            {
                var field = result.State.BattlefieldStates.Values.First(field => field.OccupantObjectIds.Count == 0);
                if (result.State.PlayerZones[actor].Base.Any(id => IsReadyUnit(result.State, id)))
                {
                    result = await MoveBaseUnitToBattlefieldAsync(session, actor, result,
                        "BATTLEFIELD:" + field.BattlefieldObjectId, prefix + "-claim");
                    continue;
                }
                if (!result.State.PlayerZones[actor].Base.Any(id => result.State.CardObjects[id].Tags.Contains(CardObjectTags.UnitCard)))
                {
                    result = await TapAllAvailableRunesAsync(session, actor, result, prefix + "-tap");
                    if (EnabledCandidate(result.Prompts[actor], CommandTypes.PlayCard)?.Sources?.Any(source =>
                        result.State.CardObjects.TryGetValue(source.Id, out var card)
                        && !IsDriverStandbyUnit(card) && card.ManaCost <= result.State.RunePools[actor].Mana
                        && CardBehaviorRegistry.TryGetByCardNo(card.CardNo!, out var behavior)
                        && behavior.PlaysSourceToBaseAsUnit && behavior.RequiredTargetCount == 0
                        && CanFundPrintedPowerWithExposedRunes(result, actor, card.CardNo!)) == true)
                        result = await TryPlayFirstUnitAsync(session, actor, result, prefix + "-play", false);
                }
            }
            result = await EndTurnAsync(session, result.State.ActivePlayerId, prefix + "-end");
            if (!recoveredInGame && !run.State.SeriesState!.Games[gameNumber - 1].Completed && result.State.TurnNumber >= 6)
            {
                await run.Cold(verifyReplay: false);
                recoveredInGame = true;
            }
        }
        Assert.True(recoveredInGame);
        Assert.True(run.State.SeriesState!.Games[gameNumber - 1].Completed,
            $"Sealed score driver did not finish: {JsonSerializer.Serialize(result.State.PlayerScores)}, turn={result.State.TurnNumber}");
        return result;
    }
}
