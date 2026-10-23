using System.Data;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;

internal sealed record VenEmpoweredSelfSqlAuditFrame(DateTimeOffset DbStartedAt, DateTimeOffset DbCompletedAt,
    string SevenTableDigest, IReadOnlyDictionary<string, string> TableDigests,
    IReadOnlyList<JsonElement> Matches, IReadOnlyList<JsonElement> Players);

internal static class QaReconnectAssertions
{
    internal static PlayerSessionDto ReadJoined(JsonElement payload, string player, string priorToken, JsonSerializerOptions json)
    {
        var joined = payload.Deserialize<PlayerSessionDto>(json);
        if (joined is null || joined.PlayerId != player || string.IsNullOrWhiteSpace(joined.ReconnectToken) || joined.ReconnectToken == priorToken)
            throw new InvalidOperationException("A real reconnect must return this identity's newly rotated token.");
        return joined;
    }

    internal static void CheckJoined(IEnumerable<string> expectedPlayers, IReadOnlyDictionary<string, PlayerSessionDto> joined)
    {
        if (joined.Count != 2 || !joined.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedPlayers)
            || joined.Any(p => p.Value.PlayerId != p.Key || string.IsNullOrWhiteSpace(p.Value.ReconnectToken)))
            throw new InvalidOperationException("Both exact identities must receive actual Joined acknowledgements and reconnect tokens.");
    }

    internal static void CheckErrors(IEnumerable<(string Recipient, WsServerMessage Message)> actual, string room,
        IReadOnlyDictionary<(string Recipient, string Player, string Code), int> expected)
    {
        var groups = actual.GroupBy(e => (e.Recipient, e.Message.PlayerId,
            e.Message.Payload is JsonElement p && p.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString()! : "INVALID_ERROR_PAYLOAD")).ToArray();
        foreach (var group in groups)
            if (group.Any(e => e.Message.RoomId != room || e.Message.Type != MessageType.ERROR)
                || !expected.TryGetValue(group.Key, out var count) || count != 1 || group.Count() != 1)
                throw new InvalidOperationException("Unexpected, duplicate or wrong-recipient API Error escaped the network gate: " + group.Key);
        if (expected.Any(p => p.Value != 1 || !groups.Any(g => g.Key == p.Key)))
            throw new InvalidOperationException("An expected deliberate API Error was not received exactly once.");
    }
}

internal sealed class QaNetworkReplyAudit
{
    private readonly string room;
    private readonly Dictionary<string, string> priorTokens;
    private readonly JsonSerializerOptions json;
    private readonly ConcurrentDictionary<string, PlayerSessionDto> joined = new();
    private readonly Dictionary<string, TaskCompletionSource<PlayerSessionDto>> joinedWaiters;
    private readonly ConcurrentDictionary<(string Recipient, string Player, string Code), ExpectedError> errors = new();
    private readonly TaskCompletionSource<bool> failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Exception? failure;

    internal QaNetworkReplyAudit(string room, IReadOnlyDictionary<string, string> priorTokens, JsonSerializerOptions json)
    {
        this.room = room; this.priorTokens = priorTokens.ToDictionary(); this.json = json;
        if (this.priorTokens.Count != 2) throw new InvalidOperationException("Two exact reconnect identities are required.");
        joinedWaiters = this.priorTokens.Keys.ToDictionary(p => p,
            _ => new TaskCompletionSource<PlayerSessionDto>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    internal void Observe(string method, string recipient, WsServerMessage message)
    {
        try {
            CheckHealthy();
            if (method is not ("Joined" or "Error")) return;
            if (!priorTokens.ContainsKey(recipient) || message.RoomId != room || message.Payload is not JsonElement payload)
                throw new InvalidOperationException("Network reply has a wrong room, recipient or payload envelope.");
            if (method == "Joined") {
                if (message.Type != MessageType.RECONNECT || message.PlayerId != recipient)
                    throw new InvalidOperationException("Joined must be RECONNECT for the exact expected envelope player.");
                var player = QaReconnectAssertions.ReadJoined(payload, recipient, priorTokens[recipient], json);
                if (!joined.TryAdd(recipient, player)) throw new InvalidOperationException("Duplicate Joined acknowledgement.");
                joinedWaiters[recipient].TrySetResult(player);
            } else {
                if (message.Type != MessageType.ERROR) throw new InvalidOperationException("Error callback has a non-ERROR envelope.");
                var error = payload.Deserialize<ErrorDto>(json) ?? throw new InvalidOperationException("Missing Error payload.");
                if (!errors.TryGetValue((recipient, message.PlayerId, error.Code), out var expected)
                    || Interlocked.Increment(ref expected.Seen) != 1)
                    throw new InvalidOperationException("Unexpected, late-registered or duplicate Error reply.");
                expected.Received.TrySetResult(message);
            }
        } catch (Exception error) {
            Interlocked.CompareExchange(ref failure, error, null); failed.TrySetResult(true);
        }
    }

    internal void ExpectError(string recipient, string player, string code)
    {
        CheckHealthy();
        if (!priorTokens.ContainsKey(recipient) || !priorTokens.ContainsKey(player) || string.IsNullOrWhiteSpace(code)
            || !errors.TryAdd((recipient, player, code), new ExpectedError()))
            throw new InvalidOperationException("Register each exact deliberate Error once, before its request.");
    }

    internal Task<PlayerSessionDto> WaitForJoinedAsync(string player, CancellationToken token)
        => UntilFaultAsync(joinedWaiters[player].Task, token);

    internal Task<WsServerMessage> WaitForErrorAsync(string recipient, string player, string code, CancellationToken token)
        => UntilFaultAsync(errors[(recipient, player, code)].Received.Task, token);

    internal async Task<T> UntilFaultAsync<T>(Task<T> pending, CancellationToken token)
    {
        CheckHealthy(); await Task.WhenAny(pending, failed.Task).WaitAsync(token); CheckHealthy();
        var result = await pending.WaitAsync(token); CheckHealthy(); return result;
    }

    internal async Task UntilFaultAsync(Task pending, CancellationToken token)
    {
        CheckHealthy(); await Task.WhenAny(pending, failed.Task).WaitAsync(token); CheckHealthy();
        await pending.WaitAsync(token); CheckHealthy();
    }

    internal void CheckHealthy()
    {
        if (Volatile.Read(ref failure) is { } error) throw new InvalidOperationException("Invalid or unexpected network reply.", error);
    }

    internal void AssertComplete()
    {
        CheckHealthy(); QaReconnectAssertions.CheckJoined(priorTokens.Keys, joined);
        if (errors.Values.Any(e => Volatile.Read(ref e.Seen) != 1))
            throw new InvalidOperationException("A deliberate Error acknowledgement is missing.");
    }

    private sealed class ExpectedError
    {
        internal int Seen;
        internal readonly TaskCompletionSource<WsServerMessage> Received = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

// Test-only read access. Native samples bracket the actual API reconnect and
// duplicate separately; they never modify a durable row or call an API reset.
internal static class VenEmpoweredSelfSqlAudit
{
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];

    internal static NpgsqlDataSource DedicatedSource()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound")
            ?? throw new InvalidOperationException("N9 network audit requires the existing dedicated PostgreSQL connection.");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        Check(settings.Database == "riftbound_acceptance" && settings.Host is "127.0.0.1" or "localhost",
            "N9 network SQL reads require the dedicated loopback riftbound_acceptance database.");
        return NpgsqlDataSource.Create(connection);
    }

    internal static async Task<VenEmpoweredSelfSqlAuditFrame> CaptureAsync(NpgsqlDataSource source, string room, CancellationToken token = default)
    {
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        await using (var readOnly = new NpgsqlCommand("set transaction read only", connection, transaction)) await readOnly.ExecuteNonQueryAsync(token);
        var start = await Clock(); var rows = new StringBuilder(); var hashes = new Dictionary<string, string>();
        IReadOnlyList<JsonElement> matches = [], players = [];
        foreach (var table in Tables) {
            await using var query = new NpgsqlCommand($"select coalesce(string_agg(row_to_json(t)::text, E'\\n' order by row_to_json(t)::text), '') from {table} t where match_id=@room", connection, transaction);
            query.Parameters.AddWithValue("room", room); var value = (string)(await query.ExecuteScalarAsync(token))!;
            rows.Append(table).Append('\n').Append(value).Append('\n'); hashes[table] = Hash(value);
            if (table is "matches" or "match_players") {
                using var parsed = JsonDocument.Parse("[" + string.Join(',', value.Split('\n', StringSplitOptions.RemoveEmptyEntries)) + "]");
                var records = parsed.RootElement.EnumerateArray().Select(row => row.Clone()).ToArray();
                if (table == "matches") matches = records; else players = records;
            }
        }
        var end = await Clock(); await transaction.CommitAsync(token);
        return new(start, end, Hash(rows.ToString()), hashes, matches, players);
        async Task<DateTimeOffset> Clock() {
            await using var clock = new NpgsqlCommand("select clock_timestamp()::text", connection, transaction);
            return DateTimeOffset.Parse((string)(await clock.ExecuteScalarAsync(token))!, CultureInfo.InvariantCulture);
        }
    }

    internal static void CheckExact(VenEmpoweredSelfSqlAuditFrame before, VenEmpoweredSelfSqlAuditFrame after)
    {
        Check(before.SevenTableDigest == after.SevenTableDigest && Tables.All(t => before.TableDigests[t] == after.TableDigests[t]),
            "Recovery or original API duplicate changed at least one of the exact seven SQL tables.");
    }

    internal static void CheckReconnect(string room, VenEmpoweredSelfSqlAuditFrame before, VenEmpoweredSelfSqlAuditFrame after,
        IReadOnlyDictionary<string, string> priorTokens, IReadOnlyDictionary<string, string> joinedTokens)
    {
        Check(before.TableDigests.Count == 7 && after.TableDigests.Count == 7
            && Tables.All(t => before.TableDigests.ContainsKey(t) && after.TableDigests.ContainsKey(t)), "Missing exact seven-table sample.");
        foreach (var table in Tables.Skip(2))
            Check(before.TableDigests[table] == after.TableDigests[table], "Reconnect changed a game log/snapshot/prompt table: " + table);
        Check(before.Matches.Count == 1 && after.Matches.Count == 1, "Reconnect must retain exactly this room's match row.");
        Compare(before.Matches[0], after.Matches[0], ["updated_at"]);
        Check(priorTokens.Count == 2 && joinedTokens.Count == 2 && before.Players.Count == 2 && after.Players.Count == 2
            && priorTokens.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(joinedTokens.Keys), "Reconnect audit requires exactly the same two identities.");
        var oldRows = before.Players.ToDictionary(p => p.GetProperty("player_id").GetString()!, StringComparer.Ordinal);
        var newRows = after.Players.ToDictionary(p => p.GetProperty("player_id").GetString()!, StringComparer.Ordinal);
        Check(oldRows.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(priorTokens.Keys)
            && newRows.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(joinedTokens.Keys), "Reconnect added, removed or substituted a player row.");
        foreach (var player in priorTokens.Keys) {
            var oldRow = oldRows[player]; var newRow = newRows[player];
            Check(oldRow.GetProperty("reconnect_token_hash").GetString() == TokenHash(priorTokens[player])
                && newRow.GetProperty("reconnect_token_hash").GetString() == TokenHash(joinedTokens[player])
                && priorTokens[player] != joinedTokens[player], "Stored reconnect hashes must match each real old and Joined token and rotate once.");
            Compare(oldRow, newRow, ["reconnect_token_hash", "updated_at"]);
        }
        Check(after.Matches[0].GetProperty("updated_at").GetDateTimeOffset()
            == after.Players.Max(row => row.GetProperty("updated_at").GetDateTimeOffset()),
            "The match timestamp must equal the last of the two actual player-session transaction timestamps.");
        void Compare(JsonElement oldRow, JsonElement newRow, string[] permitted) {
            Check(oldRow.GetProperty("match_id").GetString() == room && newRow.GetProperty("match_id").GetString() == room, "Reconnect sample contains another room.");
            var oldTime = oldRow.GetProperty("updated_at").GetDateTimeOffset(); var newTime = newRow.GetProperty("updated_at").GetDateTimeOffset();
            Check(newTime > oldTime && newTime >= before.DbCompletedAt && newTime <= after.DbStartedAt,
                "Only this actual reconnect's database time window may advance updated_at.");
            var original = JsonNode.Parse(oldRow.GetRawText())!.AsObject(); var changed = JsonNode.Parse(newRow.GetRawText())!.AsObject();
            foreach (var key in permitted) Check(original.Remove(key) && changed.Remove(key), "Required permitted field is missing.");
            Check(string.Equals(original.ToJsonString(), changed.ToJsonString(), StringComparison.Ordinal),
                "Reconnect changed a match/player column or raw numeric representation outside the exact token/updated_at boundary.");
        }
    }

    internal static void CheckCurrentPlayers(string room, VenEmpoweredSelfSqlAuditFrame sample, IReadOnlyDictionary<string, string> tokens)
    {
        Check(sample.Matches.Count == 1 && sample.Matches[0].GetProperty("match_id").GetString() == room
            && sample.Players.Count == 2 && tokens.Count == 2, "Dedicated room or identity rows are missing.");
        var rows = sample.Players.ToDictionary(p => p.GetProperty("player_id").GetString()!, StringComparer.Ordinal);
        Check(rows.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(tokens.Keys), "Stored identities differ from the actual Joined identities.");
        foreach (var (player, token) in tokens)
            Check(rows[player].GetProperty("match_id").GetString() == room && rows[player].GetProperty("reconnect_token_hash").GetString() == TokenHash(token),
                "Durable player hash differs from the actual returned reconnect token.");
    }

    internal static async Task<VenEmpoweredSelfSqlAuditFrame> VerifyNativeCheckpointAsync(NpgsqlDataSource source, string room,
        string directory, string stage, JsonSerializerOptions json)
    {
        Check(stage is "before-restart" or "after-restart", "Unknown actual network SQL checkpoint.");
        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory, "network-" + stage + "-sql-audit.json")));
        var root = document.RootElement; Check(root.GetProperty("room").GetString() == room, "Network SQL sample belongs to another room.");
        var beforeReconnect = Read("beforeReconnect"); var afterReconnect = Read("afterReconnect");
        var duplicateBefore = Read("duplicateBefore"); var duplicateAfter = Read("duplicateAfter"); var final = Read("finalSql");
        var prior = Tokens(root.GetProperty("priorTokens")); var joined = Tokens(root.GetProperty("reconnectTokens"));
        CheckReconnect(room, beforeReconnect, afterReconnect, prior, joined);
        if (stage == "after-restart") {
            using var previous = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory, "sql-before-restart.json")));
            CheckExact(previous.RootElement.GetProperty("sqlSample").Deserialize<VenEmpoweredSelfSqlAuditFrame>(json)!, beforeReconnect);
            CheckExact(afterReconnect, duplicateBefore);
        } else CheckExact(Read("forbiddenBefore"), Read("forbiddenAfter"));
        CheckExact(duplicateBefore, duplicateAfter); CheckExact(duplicateAfter, final);
        var current = await CaptureAsync(source, room); CheckExact(final, current); CheckCurrentPlayers(room, current, joined); return current;
        VenEmpoweredSelfSqlAuditFrame Read(string name) => root.GetProperty(name).Deserialize<VenEmpoweredSelfSqlAuditFrame>(json)
            ?? throw new InvalidOperationException("Missing actual network SQL sample: " + name);
        static Dictionary<string, string> Tokens(JsonElement value) => value.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
    }

    internal static string TokenHash(string token) => "sha256:" + Hash(token.Trim());
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
