using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

// Independent strict expectations for the test gate. These run without opening
// PostgreSQL or a hub, and cannot be mistaken for an actual network acceptance.
public sealed class PaymentReconnectEvidenceTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")]
    public async Task DedicatedLoopbackTargetsCanBeConstructedWithoutOpeningADatabase(string host)
    {
        await using var source = PaymentReconnectEvidence.AcceptanceSource($"Host={host};Port=1;Database=riftbound_acceptance;Username=test");
        Assert.NotNull(source);
    }

    [Theory]
    [InlineData("database.example", "riftbound_acceptance")]
    [InlineData("127.0.0.1", "riftbound")]
    [InlineData("localhost", "postgres")]
    [InlineData("127.0.0.1,database.example", "riftbound_acceptance")]
    public void NativeCaptureEntryRejectsSharedOrRemoteTargetsBeforeOpeningAnything(string host, string database)
        => Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.AcceptanceSource($"Host={host};Port=1;Database={database};Username=test"));

    [Fact]
    public async Task AllSevenTablesOtherRoomsAndBothDatabaseClocksShareOneReadOnlyRepeatableReadTransaction()
    {
        await using var connection = new RecordingConnection();
        var rows = await PaymentReconnectEvidence.Capture(connection, "R");
        var transaction = Assert.IsType<RecordingTransaction>(connection.Transaction);
        Assert.Equal(IsolationLevel.RepeatableRead, transaction.IsolationLevel);
        Assert.True(transaction.Committed); Assert.True(transaction.Disposed);
        Assert.Equal(12, connection.Commands.Count);
        Assert.Equal("SET TRANSACTION READ ONLY", connection.Commands[0].Text);
        Assert.Equal("select clock_timestamp()", connection.Commands[1].Text);
        Assert.Equal("select clock_timestamp()", connection.Commands[^1].Text);
        Assert.All(connection.Commands, command => Assert.Same(transaction, command.Transaction));
        Assert.Equal(9, connection.Commands.Count(command => command.Text.StartsWith("select row_to_json", StringComparison.Ordinal)));
        Assert.All(connection.Commands.Where(command => command.Text.StartsWith("select row_to_json", StringComparison.Ordinal)), command => Assert.Equal("R", command.Room));
        Assert.Equal(RecordingConnection.Start, rows.Started); Assert.Equal(RecordingConnection.Finish, rows.Finished);
        Assert.Equal(7, rows.Tables.Count); Assert.Single(rows.OtherMatches); Assert.Single(rows.OtherPlayers);
    }

    [Fact]
    public void OnlyTheMeasuredTwoTokenRotationsAreAllowedAcrossReconnect()
        => PaymentReconnectEvidence.ValidateRotation(Proof());

    [Theory]
    [InlineData("unchanged-room")]
    [InlineData("unchanged-player")]
    [InlineData("backwards-room")]
    [InlineData("backwards-player")]
    [InlineData("during-before-capture")]
    [InlineData("during-after-capture")]
    [InlineData("wrong-last-transaction")]
    public void AuthenticationTimesMustAdvanceOutsideBothCapturesAndIdentifyTheLastSave(string attack)
    {
        var proof = Proof(); var room = proof.After.Tables["matches"][0]!; var player = proof.After.Tables["match_players"][0]!;
        switch (attack)
        {
            case "unchanged-room": proof.Before.Tables["matches"][0]!["updated_at"] = room["updated_at"]!.DeepClone(); break;
            case "unchanged-player": proof.Before.Tables["match_players"][0]!["updated_at"] = player["updated_at"]!.DeepClone(); break;
            case "backwards-room": proof.Before.Tables["matches"][0]!["updated_at"] = proof.After.Started.ToString("O"); break;
            case "backwards-player": proof.Before.Tables["match_players"][0]!["updated_at"] = proof.After.Started.ToString("O"); break;
            case "during-before-capture": player["updated_at"] = proof.Before.Finished.AddTicks(-1).ToString("O"); break;
            case "during-after-capture": player["updated_at"] = proof.After.Started.AddTicks(1).ToString("O"); break;
            case "wrong-last-transaction": room["updated_at"] = proof.After.Started.ToString("O"); break;
        }
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateRotation(proof));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactOuterWindowBoundariesAndDistinctPlayerTransactionsRemainValid(bool upperBoundary)
    {
        var proof = Proof();
        proof.After.Tables["match_players"][0]!["updated_at"] = proof.Before.Finished.ToString("O");
        var last = upperBoundary ? proof.After.Started : proof.Before.Finished.AddTicks(1);
        proof.After.Tables["match_players"][1]!["updated_at"] = last.ToString("O");
        proof.After.Tables["matches"][0]!["updated_at"] = last.ToString("O");
        PaymentReconnectEvidence.ValidateRotation(proof);
    }

    [Theory]
    [InlineData("command_log")]
    [InlineData("game_events")]
    [InlineData("state_snapshots")]
    [InlineData("snapshots")]
    [InlineData("action_prompts")]
    public void EveryGameplayTableRemainsExact(string table)
    {
        var proof = Proof(); proof.After.Tables[table][0]!["payload"]!["immutable"] = 2;
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateRotation(proof));
    }

    [Theory]
    [InlineData("status")]
    [InlineData("ruleset_version")]
    [InlineData("seed")]
    public void MatchesCannotUseItsTimestampExceptionToChangeOtherColumns(string column)
    {
        var proof = Proof(); proof.After.Tables["matches"][0]![column] = "FORGED";
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateRotation(proof));
    }

    [Theory]
    [InlineData("seat")]
    [InlineData("connection_state")]
    [InlineData("player_id")]
    public void PlayerRowsCannotUseTheirTokenExceptionToChangeIdentityOrState(string column)
    {
        var proof = Proof(); proof.After.Tables["match_players"][0]![column] = "FORGED";
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateRotation(proof));
    }

    [Theory]
    [InlineData("old-hash")]
    [InlineData("returned-hash")]
    [InlineData("unchanged-hash")]
    [InlineData("outside-time")]
    [InlineData("missing-player")]
    [InlineData("extra-player")]
    [InlineData("other-room")]
    [InlineData("other-player")]
    public void BothReturnedSessionsAndTheExactAllowedSqlFootprintMustBeProven(string attack)
    {
        var proof = Proof(); var row = proof.After.Tables["match_players"][0]!;
        switch (attack)
        {
            case "old-hash": proof.Before.Tables["match_players"][0]!["reconnect_token_hash"] = "FORGED"; break;
            case "returned-hash": row["reconnect_token_hash"] = "FORGED"; break;
            case "unchanged-hash": row["reconnect_token_hash"] = "old-P1"; break;
            case "outside-time": row["updated_at"] = "2000-01-01T00:00:00Z"; break;
            case "missing-player": proof.After.Tables["match_players"].RemoveAt(1); break;
            case "extra-player": proof.After.Tables["match_players"].Add(JsonNode.Parse("{\"player_id\":\"P3\"}")); break;
            case "other-room": proof.After.OtherMatches[0]!["status"] = "FORGED"; break;
            case "other-player": proof.After.OtherPlayers[0]!["seat"] = "FORGED"; break;
        }
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateRotation(proof));
    }

    [Theory]
    [InlineData("matches")]
    [InlineData("match_players")]
    [InlineData("command_log")]
    [InlineData("game_events")]
    [InlineData("state_snapshots")]
    [InlineData("snapshots")]
    [InlineData("action_prompts")]
    public void DuplicateAndPureReadHaveNoAuthenticationException(string table)
    {
        var first = Rows(false); var second = Rows(false);
        PaymentReconnectEvidence.ValidateExact(first, second);
        second.Tables[table][0]!["extra"] = 1;
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateExact(first, second));
    }

    [Fact]
    public void PersistedEvidenceRoundTripRetainsExactUnicodeAndQuotedSqlValues()
    {
        var original = Rows(false);
        original.Tables["snapshots"][0]!["payload"]!["text"] = "符能 <private> \"quoted\"";
        var restored = JsonSerializer.Deserialize<PaymentReconnectEvidence.Rows>(JsonSerializer.Serialize(original, Web), Web)!;
        PaymentReconnectEvidence.ValidateExact(original, restored);
    }

    [Fact]
    public void NumericReformattingDoesNotBecomeAnExactRowMatch()
    {
        var first = Rows(false); var second = Rows(false);
        second.Tables["game_events"][0]!["payload"]!["immutable"] = JsonNode.Parse("1.0");
        Assert.Throws<InvalidOperationException>(() => PaymentReconnectEvidence.ValidateExact(first, second));
    }

    [Fact]
    public async Task AFailedReconnectFollowedByValidAuthenticatedViewsStillFails()
    {
        var folder = Path.Combine(Path.GetTempPath(), "payment-reconnect-guard-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            await using var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=never-opened;Username=test");
            await using var audit = new PaymentReconnectEvidence(source, "R", folder, "recover", new Dictionary<string, string> { ["P1"] = "old", ["P2"] = "old2" });
            audit.Observe("P1", "Error", new(MessageType.ERROR, "R", "P1", 0,
                JsonSerializer.SerializeToElement(new ErrorDto(ErrorCodes.InvalidReconnectToken, "actual rejected reconnect"), Web)));
            audit.Observe("P1", "Snapshot", new(MessageType.SNAPSHOT, "R", "P1", 7, JsonSerializer.SerializeToElement(new { tick = 7 })));
            await Assert.ThrowsAsync<InvalidOperationException>(() => audit.RequireJoined("P1", default));
            Assert.Throws<InvalidOperationException>(audit.Healthy);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Theory]
    [InlineData("same-token")]
    [InlineData("foreign-player")]
    [InlineData("wrong-room")]
    [InlineData("duplicate")]
    [InlineData("number-token")]
    [InlineData("wrong-type-join")]
    [InlineData("wrong-type-snapshot")]
    public async Task JoinedMustBeUniqueAndBindTheRecipientAndANewSession(string attack)
    {
        var folder = Path.Combine(Path.GetTempPath(), "payment-joined-guard-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            await using var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=never-opened;Username=test");
            await using var audit = new PaymentReconnectEvidence(source, "R", folder, "recover", new Dictionary<string, string> { ["P1"] = "old", ["P2"] = "old2" });
            var message = new WsServerMessage(MessageType.RECONNECT, attack == "wrong-room" ? "OTHER" : "R", "P1", 0,
                JsonSerializer.SerializeToElement(new PlayerSessionDto(attack == "foreign-player" ? "P2" : "P1", "P1", attack == "same-token" ? "old" : "new"), Web));
            if (attack == "number-token") message = message with { Payload = JsonSerializer.SerializeToElement(new { playerId = "P1", seat = "P1", reconnectToken = 7 }, Web) };
            if (attack == "wrong-type-join") message = message with { Type = MessageType.JOIN };
            if (attack == "wrong-type-snapshot") message = message with { Type = MessageType.SNAPSHOT };
            audit.Observe("P1", "Joined", message); if (attack == "duplicate") audit.Observe("P1", "Joined", message);
            Assert.Throws<InvalidOperationException>(audit.Healthy);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task AnExpectedErrorCodeCannotHideTheWrongMessageType()
    {
        var folder = Path.Combine(Path.GetTempPath(), "payment-error-type-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            await using var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=never-opened;Username=test");
            await using var audit = new PaymentReconnectEvidence(source, "R", folder, "play", new Dictionary<string, string> { ["P1"] = "old", ["P2"] = "old2" });
            await Assert.ThrowsAsync<InvalidOperationException>(() => audit.Reject("P1", "P1", ErrorCodes.InvalidTarget, () =>
            {
                audit.Observe("P1", "Error", new(MessageType.SNAPSHOT, "R", "P1", 0,
                    JsonSerializer.SerializeToElement(new ErrorDto(ErrorCodes.InvalidTarget, "expected code, wrong type"), Web)));
                return Task.FromResult(new CommandReceiptDto("R", "P1", "I", "DECIDE", false, 0, "unchanged", "rejected", ErrorCodes.InvalidTarget));
            }, default));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task BothFreshJoinedSessionsCanAdvanceAndPersistTheirActualTokens()
    {
        var folder = Path.Combine(Path.GetTempPath(), "payment-joined-positive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=never-opened;Username=test");
            await using (var audit = new PaymentReconnectEvidence(source, "R", folder, "recover",
                new Dictionary<string, string> { ["P1"] = "old-one", ["P2"] = "old-two" }))
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                foreach (var player in new[] { "P2", "P1" })
                {
                    Assert.True(audit.Observe(player, "Joined", new(MessageType.RECONNECT, "R", player, 0,
                        JsonSerializer.SerializeToElement(new PlayerSessionDto(player, player, "actual-new-" + player), Web))));
                    await audit.RequireJoined(player, deadline.Token);
                    audit.Healthy();
                }
            }
            var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(
                await File.ReadAllTextAsync(Path.Combine(folder, "current-session-tokens.json")), Web)!;
            Assert.Equal(2, saved.Count);
            Assert.Equal("actual-new-P1", saved["P1"]);
            Assert.Equal("actual-new-P2", saved["P2"]);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task MatchingExpectedErrorsCanBeConsumedInConsecutiveRejectedIntents()
    {
        var folder = Path.Combine(Path.GetTempPath(), "payment-error-positive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var source = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=never-opened;Username=test");
            await using var audit = new PaymentReconnectEvidence(source, "R", folder, "play",
                new Dictionary<string, string> { ["P1"] = "old-one", ["P2"] = "old-two" });
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            foreach (var intent in new[] { "first", "second" })
            {
                var expected = new CommandReceiptDto("R", "P1", intent, "DECIDE", false, 7, "unchanged", "rejected", ErrorCodes.InvalidTarget);
                var receipt = await audit.Reject("P1", "P1", ErrorCodes.InvalidTarget, () =>
                {
                    Assert.True(audit.Observe("P1", "Error", new(MessageType.ERROR, "R", "P1", 7,
                        JsonSerializer.SerializeToElement(new ErrorDto(ErrorCodes.InvalidTarget, "expected rejection"), Web))));
                    return Task.FromResult(expected);
                }, deadline.Token);
                Assert.Same(expected, receipt);
                audit.Healthy();
            }
        }
        finally { Directory.Delete(folder, true); }
    }

    private static PaymentReconnectEvidence.Rotation Proof() => new(Rows(false), Rows(true),
        new Dictionary<string, string> { ["P1"] = "old-P1", ["P2"] = "old-P2" },
        new Dictionary<string, string> { ["P1"] = "new-P1", ["P2"] = "new-P2" });
    private static PaymentReconnectEvidence.Rows Rows(bool rotated)
    {
        var now = DateTimeOffset.Parse("2026-10-24T01:00:00Z"); var stamp = (rotated ? now.AddSeconds(1) : now.AddSeconds(-2)).ToString("O");
        var tables = new Dictionary<string, JsonArray> {
            ["matches"] = new(JsonSerializer.SerializeToNode(new { match_id = "R", status = "PLAYING", ruleset_version = "fixed", seed = "7", updated_at = stamp })),
            ["match_players"] = new(new[] { "P1", "P2" }.Select(player => JsonSerializer.SerializeToNode(new { match_id = "R", player_id = player,
                seat = player, connection_state = "CONNECTED", reconnect_token_hash = (rotated ? "new-" : "old-") + player, updated_at = stamp })).ToArray()) };
        foreach (var table in new[] { "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
            tables[table] = new(JsonNode.Parse("{\"id\":1,\"match_id\":\"R\",\"payload\":{\"immutable\":1}}"));
        return new(rotated ? now.AddSeconds(2) : now.AddSeconds(-1), rotated ? now.AddSeconds(3) : now, tables, new(JsonNode.Parse("{\"match_id\":\"OTHER\",\"status\":\"PLAYING\"}")),
            new(JsonNode.Parse("{\"match_id\":\"OTHER\",\"player_id\":\"P3\",\"seat\":\"P1\"}")));
    }
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // Executes Capture's actual command/transaction path without PostgreSQL. The
    // separate clock values are database results, never the process wall clock.
    private sealed class RecordingConnection : DbConnection
    {
        internal static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-24T01:00:00Z");
        internal static readonly DateTimeOffset Finish = Start.AddSeconds(1);
        internal readonly List<(string Text, DbTransaction? Transaction, object? Room)> Commands = [];
        internal RecordingTransaction? Transaction;
        private int clocks;
        [AllowNull] public override string ConnectionString { get; set; } = "";
        public override string Database => "not-opened";
        public override string DataSource => "in-memory-command-recorder";
        public override string ServerVersion => "0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        public override void Close() { }
        public override void Open() => throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        { Assert.Null(Transaction); return Transaction = new(this, isolationLevel); }
        protected override DbCommand CreateDbCommand() => new RecordingCommand(this);
        private sealed class RecordingCommand(RecordingConnection owner) : DbCommand
        {
            private readonly NpgsqlCommand parameters = new();
            [AllowNull] public override string CommandText { get; set; } = "";
            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; }
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }
            protected override DbConnection? DbConnection { get; set; } = owner;
            protected override DbTransaction? DbTransaction { get; set; }
            protected override DbParameterCollection DbParameterCollection => parameters.Parameters;
            protected override DbParameter CreateDbParameter() => new NpgsqlParameter();
            private void Record() => owner.Commands.Add((CommandText, DbTransaction, Parameters.Contains("room") ? Parameters["room"].Value : null));
            public override void Cancel() => throw new NotSupportedException();
            public override void Prepare() => throw new NotSupportedException();
            public override int ExecuteNonQuery() { Record(); return 0; }
            public override object ExecuteScalar() { Record(); return (owner.clocks++ == 0 ? Start : Finish).UtcDateTime; }
            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            {
                Record(); var table = new DataTable(); table.Columns.Add("json", typeof(string)); table.Rows.Add("{}"); return table.CreateDataReader();
            }
            protected override void Dispose(bool disposing) { if (disposing) parameters.Dispose(); base.Dispose(disposing); }
        }
    }
    private sealed class RecordingTransaction(DbConnection connection, IsolationLevel isolation) : DbTransaction
    {
        internal bool Committed, Disposed;
        protected override DbConnection DbConnection => connection;
        public override IsolationLevel IsolationLevel => isolation;
        public override void Commit() => Committed = true;
        public override void Rollback() => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
