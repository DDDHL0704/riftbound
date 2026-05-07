using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PostgresMatchRecoveryStoreSmokeTests
{
    [Fact]
    public async Task PostgresRecoveryStoreLoadsReplayInitialStateAndPassesRegistryReplayAudit()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await ApplySchemaAsync(dataSource);

        var roomId = $"recovery-smoke-{Guid.NewGuid():N}";
        try
        {
            var playerStore = new PostgresMatchPlayerStore(dataSource);
            await playerStore.SavePlayerSessionAsync(
                roomId,
                "alice",
                "P1",
                ReconnectTokenHasher.Hash("rt_alice"),
                CancellationToken.None);
            await playerStore.SavePlayerSessionAsync(
                roomId,
                "bob",
                "P2",
                ReconnectTokenHasher.Hash("rt_bob"),
                CancellationToken.None);

            var journal = new PostgresMatchJournal(dataSource);
            var ruleEngine = new PlaceholderRuleEngine();
            var initialState = MatchReplayInitialStateBuilder.FromSeats(
                roomId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["alice"] = "P1",
                    ["bob"] = "P2"
                });
            var liveSession = new MatchSession(initialState, ruleEngine, journal);

            await liveSession.ReadyAsync("alice", "intent-ready-a", RawCommand("READY"), CancellationToken.None);
            await liveSession.ReadyAsync("bob", "intent-ready-b", RawCommand("READY"), CancellationToken.None);
            await liveSession.SubmitAsync(
                "alice",
                "intent-pass-a",
                new PassCommand(),
                RawCommand("PASS"),
                CancellationToken.None);

            var recoveryStore = new PostgresMatchRecoveryStore(dataSource);
            var recovery = await recoveryStore.LoadAsync(roomId, CancellationToken.None);

            Assert.NotNull(recovery);
            Assert.True(recovery.IsConsistent, string.Join("; ", recovery.ValidationErrors));
            Assert.NotNull(recovery.AuthoritativeState);
            Assert.NotNull(recovery.ReplayInitialState);
            Assert.Equal(roomId, recovery.ReplayInitialState.RoomId);
            Assert.Equal("alice", recovery.ReplayInitialState.ActivePlayerId);
            Assert.Equal(3, recovery.Commands.Count);

            var replayErrors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(
                recovery,
                ruleEngine,
                CancellationToken.None);
            Assert.Empty(replayErrors);

            var registry = new InMemoryMatchSessionRegistry(
                ruleEngine,
                NoopMatchJournal.Instance,
                recoveryStore);
            var recoveredSession = await registry.GetOrCreateAsync(roomId, CancellationToken.None);
            var snapshot = recoveredSession.SnapshotFor("alice");

            Assert.Equal(recovery.AuthoritativeState.Tick, snapshot.Tick);
            Assert.Equal(recovery.AuthoritativeState.ActivePlayerId, snapshot.ActivePlayerId);
            Assert.Equal(MatchStatuses.InProgress, recovery.AuthoritativeState.Status);
        }
        finally
        {
            await DeleteRoomAsync(dataSource, roomId);
        }
    }

    private static async Task ApplySchemaAsync(NpgsqlDataSource dataSource)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        foreach (var schemaPath in SchemaPaths())
        {
            var sql = await File.ReadAllTextAsync(schemaPath);
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static IEnumerable<string> SchemaPaths()
    {
        var outputSql = Path.Combine(AppContext.BaseDirectory, "Sql");
        if (Directory.Exists(outputSql))
        {
            return Directory.GetFiles(outputSql, "*.sql").Order(StringComparer.Ordinal);
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var sourceSql = Path.Combine(current.FullName, "src", "Riftbound.Persistence", "Sql");
            if (Directory.Exists(sourceSql))
            {
                return Directory.GetFiles(sourceSql, "*.sql").Order(StringComparer.Ordinal);
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate Riftbound.Persistence SQL migrations.");
    }

    private static async Task DeleteRoomAsync(NpgsqlDataSource dataSource, string roomId)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "delete from matches where match_id = @match_id;",
            connection);
        command.Parameters.AddWithValue("match_id", roomId);
        await command.ExecuteNonQueryAsync();
    }

    private static JsonElement RawCommand(string cmdType)
    {
        return JsonDocument.Parse($$"""{"cmdType":"{{cmdType}}"}""").RootElement.Clone();
    }
}
