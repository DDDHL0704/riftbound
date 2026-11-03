using System.Text;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class SealedSubmittedOpeningTests
{
    [Theory]
    [InlineData("OGN", 1, false)]
    [InlineData("OGN", 1, true)]
    [InlineData("SFD", 3, false)]
    [InlineData("SFD", 3, true)]
    public async Task JsonbObjectKeyReorderingPreservesOpeningSideboardingAndCompletedSeriesViews(
        string setCode, int bestOf, bool bobSeatsFirst)
    {
        var run = new Run(bestOf, setCode: setCode, reorderRecoveryObjects: true);
        var seatOrder = bobSeatsFirst ? new[] { "bob", "alice" } : new[] { "alice", "bob" };
        foreach (var player in seatOrder) await run.Join(player);
        await run.Cold();
        var pools = MatchStateHasher.HashValue(run.State.PlayerSealedPools);
        var losers = bestOf == 1 ? new[] { "bob" } : new[] { "bob", "alice", "bob" };

        for (var index = 0; index < losers.Length; index++)
        {
            var game = index + 1;
            await run.Choose(goFirst: game % 2 == 1);
            await run.Cold();
            foreach (var player in seatOrder.Reverse())
            {
                var deck = await Deck(run.State.PlayerSealedPools![player],
                    withLegend: game > 1, withChampion: game == 3, battlefieldCount: bestOf == 1 ? 3 : 1);
                await run.Act(player, Command(deck));
                // Includes a private single submission and changing sideboard counts.
                await run.Cold();
            }
            Assert.Equal(seatOrder, run.Live.SnapshotFor("alice").SideboardCounts!.Keys);
            await run.Act(seatOrder[0], new ReadyCommand());
            await run.Cold();
            var opened = await run.Act(seatOrder[1], new ReadyCommand());
            Assert.Equal(MatchPhases.Mulligan, opened.State.Phase);
            await run.Cold();

            var first = run.State.TurnPlayerId;
            var second = run.State.OpeningSecondActionPlayerId!;
            await run.Act(first, new MulliganCommand([]));
            await run.Cold();
            await run.Act(second, new MulliganCommand([]));
            await run.Cold();
            await run.Act(losers[index], new SurrenderCommand());
            // Includes the archived terminal core frame and the next game's wait.
            await run.Cold();
            var snapshot = run.Live.SnapshotFor("alice");
            Assert.Equal(seatOrder, snapshot.Players.Keys);
            Assert.Equal(seatOrder, snapshot.Series!.Wins.Keys);
            Assert.Equal(seatOrder, snapshot.Series.UsedBattlefields.Keys);
            Assert.Equal(seatOrder, snapshot.SideboardCounts!.Keys);
            Assert.All(snapshot.Series.UsedBattlefields.Values, used => Assert.Equal(game, used.Count));
            Assert.Equal(pools, MatchStateHasher.HashValue(run.State.PlayerSealedPools));
            Assert.Null(ResolutionResult.BuildSpectatorSnapshot(run.State).SealedPool);
        }
        Assert.Equal(MatchSeriesPhases.Finished, run.Live.SnapshotFor("alice").Series!.Phase);
        Assert.Equal(bestOf == 1 ? 1 : 2, run.Live.SnapshotFor("alice").Series!.Wins["alice"]);
    }

    private static MatchRecoveryFrame ReorderRecoveryFrameObjects(MatchRecoveryFrame frame)
    {
        var payload = Reorder(JsonSerializer.SerializeToElement(frame, Json));
        return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(payload, Json), Json)!;

        // JSONB reorders object properties; values and array order remain authoritative.
        static object Reorder(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Object => value.EnumerateObject()
                .OrderBy(property => Encoding.UTF8.GetByteCount(property.Name))
                .ThenBy(property => property.Name, StringComparer.Ordinal)
                .ToDictionary(property => property.Name, property => Reorder(property.Value), StringComparer.Ordinal),
            JsonValueKind.Array => value.EnumerateArray().Select(Reorder).ToArray(),
            _ => value.Clone()
        };
    }
}
