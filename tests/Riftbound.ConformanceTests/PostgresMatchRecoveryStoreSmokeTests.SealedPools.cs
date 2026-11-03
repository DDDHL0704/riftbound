using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class PostgresMatchRecoveryStoreSmokeTests
{
    [Theory]
    [InlineData("OGN", 1)]
    [InlineData("SFD", 3)]
    public async Task PostgresSealedPoolsSurviveColdReconnectBetweenOriginalSeatCommits(string setCode, int bestOf)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await ApplySchemaAsync(dataSource);
        var roomId = $"sealed-pool-smoke-{Guid.NewGuid():N}";
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        try
        {
            var players = new PostgresMatchPlayerStore(dataSource);
            var journal = new PostgresMatchJournal(dataSource);
            var engine = new CoreRuleEngine();
            var store = new PostgresMatchRecoveryStore(dataSource);
            IMatchSession live = new MatchSession(roomId, engine, journal, players, MatchSessionOptions.ForSealed(setCode, bestOf));
            var alice = await live.EnsurePlayerAsync("alice", default);
            var originalPool = JsonSerializer.Serialize(live.SnapshotFor("alice").SealedPool, json);
            var first = Assert.IsType<MatchRecoveryFrame>(await store.LoadAsync(roomId, default));
            Assert.True(first.IsConsistent, string.Join("; ", first.ValidationErrors));
            Assert.Single(first.AuthoritativeState!.PlayerSealedPools!);
            Assert.Empty(first.ReplayInitialState!.Seats);
            Assert.Null(first.ReplayInitialState.PlayerSealedPools);
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(first, engine, default));

            live = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(roomId, default);
            await live.ReconnectPlayerAsync("alice", alice.ReconnectToken, default);
            var bob = await live.EnsurePlayerAsync("bob", default);
            var final = Assert.IsType<MatchRecoveryFrame>(await store.LoadAsync(roomId, default));
            Assert.True(final.IsConsistent, string.Join("; ", final.ValidationErrors));
            Assert.Equal(2, final.Commands.Count);
            Assert.Equal(2, final.AuthoritativeState!.PlayerSealedPools!.Count);
            Assert.Equal(originalPool, JsonSerializer.Serialize(live.SnapshotFor("alice").SealedPool, json));
            Assert.Equal(MatchSessionOptions.ForSealed(setCode, bestOf), final.ReplayInitialState!.SessionOptions);
            Assert.Null(final.SpectatorReplayFrame!.SpectatorSnapshot.SealedPool);
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(final, engine, default));

            var cold = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(roomId, default);
            await cold.ReconnectPlayerAsync("bob", bob.ReconnectToken, default);
            // PostgreSQL jsonb may reorder object properties, including player keys.
            // Compare every snapshot value while preserving array order.
            Assert.True(JsonNode.DeepEquals(
                JsonSerializer.SerializeToNode(live.SnapshotFor("bob"), json),
                JsonSerializer.SerializeToNode(cold.SnapshotFor("bob"), json)),
                "The cold reconnect snapshot differs from the live snapshot.");
        }
        finally { await DeleteRoomAsync(dataSource, roomId); }
    }
}
