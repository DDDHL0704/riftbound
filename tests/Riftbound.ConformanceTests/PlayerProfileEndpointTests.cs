using Riftbound.Api;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PlayerProfileEndpointTests
{
    [Fact]
    public async Task PlayerProfileEndpointsReturnStatsAndRecentPublicMatches()
    {
        var store = new InMemoryMatchResultStore();
        await store.RecordMatchResultAsync(
            new MatchResultRecord(
                "profile-room-older",
                [
                    new MatchResultPlayerRecord("alice", "P1", 8, true),
                    new MatchResultPlayerRecord("bob", "P2", 4, false)
                ],
                "alice",
                DateTimeOffset.Parse("2026-06-29T01:00:00Z")),
            CancellationToken.None);
        await store.RecordMatchResultAsync(
            new MatchResultRecord(
                "profile-room-newer",
                [
                    new MatchResultPlayerRecord("charlie", "P1", 8, true),
                    new MatchResultPlayerRecord("alice", "P2", 7, false)
                ],
                "charlie",
                DateTimeOffset.Parse("2026-06-29T02:00:00Z")),
            CancellationToken.None);

        var profile = await PlayerProfileEndpoints.GetPlayerProfileAsync("Alice", store, CancellationToken.None);
        Assert.Equal("alice", profile.Handle);
        Assert.Equal(2, profile.TotalMatches);
        Assert.Equal(1, profile.Wins);
        Assert.Equal(1, profile.Losses);
        Assert.Equal(0.5, profile.WinRate);

        var matches = await PlayerProfileEndpoints.GetPlayerMatchesAsync("Alice", 10, store, CancellationToken.None);
        Assert.Equal(["profile-room-newer", "profile-room-older"], matches.Select(match => match.RoomId).ToArray());
        Assert.All(matches, match =>
        {
            Assert.DoesNotContain("hand", match.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("deck", match.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, match.Players.Count);
        });
        Assert.Equal("charlie", matches[0].WinnerPlayerId);
        Assert.Equal(
            ["charlie:P1:8:True", "alice:P2:7:False"],
            matches[0].Players.Select(player => $"{player.PlayerId}:{player.Seat}:{player.Score}:{player.Won}").ToArray());
    }

    [Fact]
    public async Task LeaderboardEndpointOrdersByWinsWinRateAndHandle()
    {
        var store = new InMemoryMatchResultStore();
        await store.RecordMatchResultAsync(
            Result("leader-room-1", "alice", "bob", winnerPlayerId: "alice", finishedAt: "2026-06-29T01:00:00Z"),
            CancellationToken.None);
        await store.RecordMatchResultAsync(
            Result("leader-room-2", "alice", "charlie", winnerPlayerId: "alice", finishedAt: "2026-06-29T02:00:00Z"),
            CancellationToken.None);
        await store.RecordMatchResultAsync(
            Result("leader-room-3", "charlie", "bob", winnerPlayerId: "charlie", finishedAt: "2026-06-29T03:00:00Z"),
            CancellationToken.None);

        var leaderboard = await PlayerProfileEndpoints.GetLeaderboardAsync(limit: 10, store, CancellationToken.None);

        Assert.Equal(
            ["1:alice:2:2:0:1", "2:charlie:2:1:1:0.5", "3:bob:2:0:2:0"],
            leaderboard.Select(entry => $"{entry.Rank}:{entry.Handle}:{entry.TotalMatches}:{entry.Wins}:{entry.Losses}:{entry.WinRate}").ToArray());
    }

    private static MatchResultRecord Result(
        string roomId,
        string p1,
        string p2,
        string winnerPlayerId,
        string finishedAt)
    {
        return new MatchResultRecord(
            roomId,
            [
                new MatchResultPlayerRecord(p1, "P1", string.Equals(p1, winnerPlayerId, StringComparison.Ordinal) ? 8 : 4, string.Equals(p1, winnerPlayerId, StringComparison.Ordinal)),
                new MatchResultPlayerRecord(p2, "P2", string.Equals(p2, winnerPlayerId, StringComparison.Ordinal) ? 8 : 4, string.Equals(p2, winnerPlayerId, StringComparison.Ordinal))
            ],
            winnerPlayerId,
            DateTimeOffset.Parse(finishedAt));
    }
}
