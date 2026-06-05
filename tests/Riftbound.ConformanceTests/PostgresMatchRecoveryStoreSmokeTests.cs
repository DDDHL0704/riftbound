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
            Assert.NotNull(recovery.SpectatorReplayFrame);
            Assert.Equal(roomId, recovery.ReplayInitialState.RoomId);
            Assert.Equal("alice", recovery.ReplayInitialState.ActivePlayerId);
            Assert.Equal(3, recovery.Commands.Count);
            Assert.Equal(recovery.LastEventSequence, recovery.SpectatorReplayFrame.EventSequence);
            Assert.Equal(
                MatchStateHasher.Hash(recovery.AuthoritativeState),
                recovery.SpectatorReplayFrame.AuthoritativeStateHash);
            Assert.DoesNotContain("seed", recovery.SpectatorReplayFrame.SpectatorSnapshot.Timing.Keys);
            Assert.DoesNotContain("rngCursor", recovery.SpectatorReplayFrame.SpectatorSnapshot.Timing.Keys);

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

    [Fact]
    public async Task PostgresRecoveryStoreLoadsRawCommandPayloadsForAcceptedAndRejectedCommands()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await ApplySchemaAsync(dataSource);

        var roomId = $"recovery-raw-smoke-{Guid.NewGuid():N}";
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
            var initialState = MatchReplayInitialStateBuilder.FromSeats(
                roomId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["alice"] = "P1",
                    ["bob"] = "P2"
                });
            var liveSession = new MatchSession(initialState, new PlaceholderRuleEngine(), journal);
            var acceptedRawCommand = RawCommand("PASS", "clientNote", "accepted raw smoke payload");
            var rejectedRawCommand = RawCommand(
                "UNKNOWN_RECOVERY_TEST",
                "clientNote",
                "rejected raw smoke payload");

            await liveSession.ReadyAsync("alice", "intent-ready-a", RawCommand("READY"), CancellationToken.None);
            await liveSession.ReadyAsync("bob", "intent-ready-b", RawCommand("READY"), CancellationToken.None);
            await liveSession.SubmitAsync(
                "alice",
                "intent-pass-with-client-note",
                new PassCommand(),
                acceptedRawCommand,
                CancellationToken.None);
            await liveSession.SubmitAsync(
                "alice",
                "intent-unsupported-with-client-note",
                new UnsupportedCommand("UNKNOWN_RECOVERY_TEST", rejectedRawCommand),
                rejectedRawCommand,
                CancellationToken.None);

            var recoveryStore = new PostgresMatchRecoveryStore(dataSource);
            var recovery = await recoveryStore.LoadAsync(roomId, CancellationToken.None);

            Assert.NotNull(recovery);
            Assert.True(recovery.IsConsistent, string.Join("; ", recovery.ValidationErrors));
            var accepted = Assert.Single(
                recovery.Commands,
                command => command.ClientIntentId == "intent-pass-with-client-note");
            var rejected = Assert.Single(
                recovery.Commands,
                command => command.ClientIntentId == "intent-unsupported-with-client-note");

            Assert.True(accepted.Accepted);
            Assert.False(rejected.Accepted);
            Assert.Equal("PASS", accepted.CommandType);
            Assert.Equal("UNKNOWN_RECOVERY_TEST", rejected.CommandType);
            AssertRawCommand(accepted.RawCommand, "PASS", "clientNote", "accepted raw smoke payload");
            AssertRawCommand(
                rejected.RawCommand,
                "UNKNOWN_RECOVERY_TEST",
                "clientNote",
                "rejected raw smoke payload");
        }
        finally
        {
            await DeleteRoomAsync(dataSource, roomId);
        }
    }

    [Fact]
    public async Task PostgresMatchPlayerStoreRejectsSeatConflictsAndPlayerSeatDrift()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await ApplySchemaAsync(dataSource);

        var roomId = $"player-seat-uniqueness-smoke-{Guid.NewGuid():N}";
        try
        {
            var playerStore = new PostgresMatchPlayerStore(dataSource);
            var aliceOriginalHash = ReconnectTokenHasher.Hash("rt_alice_original");
            var aliceUpdatedHash = ReconnectTokenHasher.Hash("rt_alice_updated");

            await playerStore.SavePlayerSessionAsync(
                roomId,
                "alice",
                "P1",
                aliceOriginalHash,
                CancellationToken.None);
            await playerStore.SavePlayerSessionAsync(
                roomId,
                "alice",
                "P1",
                aliceUpdatedHash,
                CancellationToken.None);

            Assert.True(await playerStore.HasReconnectTokenHashAsync(
                roomId,
                "alice",
                aliceUpdatedHash,
                CancellationToken.None));
            Assert.False(await playerStore.HasReconnectTokenHashAsync(
                roomId,
                "alice",
                aliceOriginalHash,
                CancellationToken.None));

            var duplicateSeatException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await playerStore.SavePlayerSessionAsync(
                    roomId,
                    "bob",
                    "P1",
                    ReconnectTokenHasher.Hash("rt_bob_duplicate_seat"),
                    CancellationToken.None));
            Assert.Contains("duplicate seat", duplicateSeatException.Message, StringComparison.OrdinalIgnoreCase);

            var bobOriginalHash = ReconnectTokenHasher.Hash("rt_bob_original");
            await playerStore.SavePlayerSessionAsync(
                roomId,
                "bob",
                "P2",
                bobOriginalHash,
                CancellationToken.None);

            var driftHash = ReconnectTokenHasher.Hash("rt_bob_drift");
            var seatDriftException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await playerStore.SavePlayerSessionAsync(
                    roomId,
                    "bob",
                    "P3",
                    driftHash,
                    CancellationToken.None));
            Assert.Contains("seat drift", seatDriftException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(await playerStore.HasReconnectTokenHashAsync(
                roomId,
                "bob",
                bobOriginalHash,
                CancellationToken.None));
            Assert.False(await playerStore.HasReconnectTokenHashAsync(
                roomId,
                "bob",
                driftHash,
                CancellationToken.None));

            await AssertPlayerSeatAsync(dataSource, roomId, "alice", "P1");
            await AssertPlayerSeatAsync(dataSource, roomId, "bob", "P2");
        }
        finally
        {
            await DeleteRoomAsync(dataSource, roomId);
        }
    }

    [Fact]
    public async Task PostgresMatchJournalRejectsDuplicateClientIntentRawPayloadOrCommandDrift()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await ApplySchemaAsync(dataSource);

        var roomId = $"journal-dup-intent-smoke-{Guid.NewGuid():N}";
        try
        {
            var journal = new PostgresMatchJournal(dataSource);
            var original = CommandEntry(
                roomId,
                "intent-duplicate-payload",
                CommandTypes.Pass,
                RawCommand(CommandTypes.Pass, "clientNote", "original raw smoke payload"));

            await journal.RecordAsync(original, CancellationToken.None);
            await journal.RecordAsync(original, CancellationToken.None);

            var rawConflict = original with
            {
                RawCommand = RawCommand(CommandTypes.Pass, "clientNote", "changed raw smoke payload")
            };
            var rawException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await journal.RecordAsync(rawConflict, CancellationToken.None));
            Assert.Contains("duplicate client intent", rawException.Message, StringComparison.OrdinalIgnoreCase);
            await AssertCommandLogRawCommandAsync(
                dataSource,
                roomId,
                "intent-duplicate-payload",
                "original raw smoke payload");

            var commandOriginal = CommandEntry(
                roomId,
                "intent-duplicate-command",
                CommandTypes.Pass,
                RawCommand(CommandTypes.Pass, "clientNote", "command original payload"));
            await journal.RecordAsync(commandOriginal, CancellationToken.None);

            var commandConflict = commandOriginal with
            {
                CommandType = CommandTypes.EndTurn
            };
            var commandException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await journal.RecordAsync(commandConflict, CancellationToken.None));
            Assert.Contains("duplicate client intent", commandException.Message, StringComparison.OrdinalIgnoreCase);
            await AssertCommandLogRawCommandAsync(
                dataSource,
                roomId,
                "intent-duplicate-command",
                "command original payload");
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

    private static async Task AssertPlayerSeatAsync(
        NpgsqlDataSource dataSource,
        string roomId,
        string playerId,
        string expectedSeat)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            select seat
            from match_players
            where match_id = @match_id
              and player_id = @player_id;
            """,
            connection);
        command.Parameters.AddWithValue("match_id", roomId);
        command.Parameters.AddWithValue("player_id", playerId);

        var seat = await command.ExecuteScalarAsync();
        Assert.Equal(expectedSeat, Assert.IsType<string>(seat));
    }

    private static MatchJournalEntry CommandEntry(
        string roomId,
        string clientIntentId,
        string commandType,
        JsonElement rawCommand)
    {
        var state = MatchReplayInitialStateBuilder.FromSeats(
            roomId,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["alice"] = "P1",
                ["bob"] = "P2"
            }) with
            {
                Tick = 1,
                Status = MatchStatuses.InProgress,
                ReadyPlayerIds = ["alice", "bob"],
                Phase = MatchPhases.Main,
                TimingState = TimingStates.NeutralOpen
            };

        return new MatchJournalEntry(
            roomId,
            "alice",
            clientIntentId,
            commandType,
            rawCommand,
            0,
            state.Tick,
            0,
            0,
            true,
            null,
            state,
            [],
            ResolutionResult.BuildSnapshots(state),
            ResolutionResult.BuildPrompts(state),
            DateTimeOffset.UtcNow);
    }

    private static async Task AssertCommandLogRawCommandAsync(
        NpgsqlDataSource dataSource,
        string roomId,
        string clientIntentId,
        string expectedClientNote)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            select count(*)::bigint, max(payload #>> '{rawCommand,clientNote}')
            from command_log
            where match_id = @match_id
              and player_id = @player_id
              and client_intent_id = @client_intent_id;
            """,
            connection);
        command.Parameters.AddWithValue("match_id", roomId);
        command.Parameters.AddWithValue("player_id", "alice");
        command.Parameters.AddWithValue("client_intent_id", clientIntentId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt64(0));
        Assert.Equal(expectedClientNote, reader.GetString(1));
    }

    private static JsonElement RawCommand(string cmdType)
    {
        return JsonDocument.Parse($$"""{"cmdType":"{{cmdType}}"}""").RootElement.Clone();
    }

    private static JsonElement RawCommand(string cmdType, string propertyName, string propertyValue)
    {
        return JsonSerializer.SerializeToElement(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cmdType"] = cmdType,
            [propertyName] = propertyValue
        });
    }

    private static void AssertRawCommand(
        JsonElement? rawCommand,
        string cmdType,
        string propertyName,
        string propertyValue)
    {
        Assert.True(rawCommand.HasValue);
        var raw = rawCommand.Value;
        Assert.Equal(JsonValueKind.Object, raw.ValueKind);
        Assert.Equal(cmdType, raw.GetProperty("cmdType").GetString());
        Assert.Equal(propertyValue, raw.GetProperty(propertyName).GetString());
    }
}
