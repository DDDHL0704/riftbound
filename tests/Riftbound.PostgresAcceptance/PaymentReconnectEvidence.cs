using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;

// Test infrastructure only. Real Joined tokens and exact SQL deltas are separate
// from gameplay proofs; an authenticated Snapshot is never a successful Reconnect.
internal sealed class PaymentReconnectEvidence : IAsyncDisposable
{
    internal sealed record Rows(DateTimeOffset Started, DateTimeOffset Finished,
        IReadOnlyDictionary<string, JsonArray> Tables, JsonArray OtherMatches, JsonArray OtherPlayers);
    internal sealed record Rotation(Rows Before, Rows After, IReadOnlyDictionary<string, string> SuppliedHashes,
        IReadOnlyDictionary<string, string> JoinedHashes);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];
    private readonly NpgsqlDataSource source;
    private readonly string room, folder, mode;
    private readonly IReadOnlyDictionary<string, string> supplied;
    private readonly ConcurrentDictionary<string, string> joined = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> expectedErrors = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> failures = new();
    private Rows? before;
    internal PaymentReconnectEvidence(NpgsqlDataSource source, string room, string folder, string mode, IReadOnlyDictionary<string, string> tokens)
    { this.source = source; this.room = room; this.folder = folder; this.mode = mode; supplied = tokens; }
    internal IReadOnlyDictionary<string, string> Tokens => supplied;

    internal static async Task<PaymentReconnectEvidence> Begin(string room, string folder, string mode,
        IReadOnlyDictionary<string, string> initial)
    {
        var path = Path.Combine(folder, "current-session-tokens.json");
        var tokens = File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(path), Json)!
            : mode == "play" ? initial : throw new InvalidOperationException("Missing actual Joined tokens from the preceding phase.");
        Check(tokens.Keys.Order().SequenceEqual(initial.Keys.Order()), "Keep exactly the fixture's two identities.");
        var source = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound")
            ?? throw new ArgumentException("Network audit requires the same dedicated ConnectionStrings__Riftbound for exact read-only SQL evidence."));
        var audit = new PaymentReconnectEvidence(source, room, folder, mode, tokens);
        audit.before = await Capture(source, room); return audit;
    }

    internal bool Observe(string recipient, string method, WsServerMessage message)
    {
        if (method is not ("Joined" or "Error")) return true;
        if (message.RoomId != room || message.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
        { failures.Enqueue("Invalid Joined/Error envelope."); return false; }
        if (method == "Joined")
        {
            if (!payload.TryGetProperty("playerId", out var player) || player.ValueKind != JsonValueKind.String || player.GetString() != recipient
                || message.PlayerId != recipient || !supplied.ContainsKey(recipient)
                || !payload.TryGetProperty("reconnectToken", out var value) || value.ValueKind != JsonValueKind.String || value.GetString() is not { Length: > 0 } token
                || token == supplied[recipient] || !joined.TryAdd(recipient, token))
            { failures.Enqueue("Missing, duplicated, foreign or unrotated Joined session."); return false; }
        }
        else
        {
            var code = payload.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            var key = recipient + "|" + message.PlayerId + "|" + code;
            if (!expectedErrors.TryUpdate(key, 0, 1)) { failures.Enqueue("Unexpected Error: " + key); return false; }
        }
        return true;
    }
    internal void Healthy() => Check(failures.IsEmpty, string.Join("; ", failures));
    internal async Task RequireJoined(string player, CancellationToken token)
    { while (!joined.ContainsKey(player)) { Healthy(); await Task.Delay(10, token); } Healthy(); }
    internal async Task Connected()
    {
        Healthy(); Check(joined.Count == 2 && joined.Keys.Order().SequenceEqual(supplied.Keys.Order()), "Require actual Joined for both identities.");
        await File.WriteAllTextAsync(Path.Combine(folder, "current-session-tokens.json"), JsonSerializer.Serialize(joined, Json));
        var after = await Capture(source, room);
        var rotation = new Rotation(before!, after, supplied.ToDictionary(p => p.Key, p => Hash(p.Value)), joined.ToDictionary(p => p.Key, p => Hash(p.Value)));
        ValidateRotation(rotation);
        await Write("reconnect-" + mode + ".json", rotation);
    }
    internal async Task<CommandReceiptDto> Reject(string recipient, string claimedPlayer, string code, Func<Task<CommandReceiptDto>> action, CancellationToken token)
    {
        var key = recipient + "|" + claimedPlayer + "|" + code;
        Check(expectedErrors.TryAdd(key, 1), "Do not overlap expected rejections.");
        try
        {
            var result = await action(); Healthy();
            Check(!result.Accepted && result.ErrorCode == code, "Expected a specific rejected receipt: " + code);
            while (expectedErrors[key] != 0) { Healthy(); await Task.Delay(10, token); }
            return result;
        }
        finally { expectedErrors.TryRemove(key, out _); }
    }
    internal async Task<T> WithoutWrites<T>(string label, Func<Task<T>> action)
    {
        var previous = await Capture(source, room); var result = await action(); var current = await Capture(source, room);
        ValidateExact(previous, current); await Write("no-writes-" + mode + "-" + label + ".json", new { previous, current }); return result;
    }
    internal async Task Complete()
    {
        Healthy(); Check(expectedErrors.IsEmpty && joined.Count == 2, "Every anticipated Error and both Joined must be accounted for.");
        await Write("network-end-" + mode + "-rows.json", await Capture(source, room));
    }
    public async ValueTask DisposeAsync()
    {
        if (joined.Count > 0)
        {
            var latest = supplied.ToDictionary(p => p.Key, p => p.Value);
            foreach (var pair in joined) latest[pair.Key] = pair.Value;
            await File.WriteAllTextAsync(Path.Combine(folder, "current-session-tokens.json"), JsonSerializer.Serialize(latest, Json));
        }
        await source.DisposeAsync();
    }
    private Task Write(string name, object value) => File.WriteAllTextAsync(Path.Combine(folder, name), JsonSerializer.Serialize(value, value.GetType(), Json));

    internal static async Task<Rows> Capture(NpgsqlDataSource source, string room)
    {
        await using var connection = await source.OpenConnectionAsync();
        var start = await Now(); var rows = new Dictionary<string, JsonArray>();
        foreach (var table in Tables) rows[table] = await Read(table, "=");
        var otherMatches = await Read("matches", "<>"); var otherPlayers = await Read("match_players", "<>");
        return new(start, await Now(), rows, otherMatches, otherPlayers);
        async Task<JsonArray> Read(string table, string relation)
        {
            await using var command = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id {relation} @room order by row_to_json(t)::text", connection);
            command.Parameters.AddWithValue("room", room); await using var reader = await command.ExecuteReaderAsync(); var result = new JsonArray();
            while (await reader.ReadAsync()) result.Add(JsonNode.Parse(reader.GetString(0))); return result;
        }
        async Task<DateTimeOffset> Now()
        { await using var query = new NpgsqlCommand("select clock_timestamp()", connection); return new DateTimeOffset((DateTime)(await query.ExecuteScalarAsync())!); }
    }
    internal static void ValidateExact(Rows before, Rows after, bool includeOtherRooms = true)
    {
        foreach (var table in Tables) Check(EqualJson(before.Tables[table], after.Tables[table]), "Read/duplicate changed exact table " + table);
        if (includeOtherRooms) Check(EqualJson(before.OtherMatches, after.OtherMatches) && EqualJson(before.OtherPlayers, after.OtherPlayers), "Unrelated room/player authentication rows changed.");
    }
    internal static void ValidateRotation(Rotation proof)
    {
        var before = proof.Before; var after = proof.After;
        foreach (var table in Tables.Except(["matches", "match_players"]))
            Check(EqualJson(before.Tables[table], after.Tables[table]), "Reconnect changed gameplay table " + table);
        Check(EqualJson(before.OtherMatches, after.OtherMatches) && EqualJson(before.OtherPlayers, after.OtherPlayers), "Reconnect changed another room/player.");
        Check(before.Tables["matches"].Count == 1 && after.Tables["matches"].Count == 1, "Keep exactly this room row.");
        Compare(before.Tables["matches"][0]!, after.Tables["matches"][0]!, ["updated_at"]);
        var oldPlayers = Players(before); var newPlayers = Players(after);
        Check(oldPlayers.Count == 2 && newPlayers.Keys.Order().SequenceEqual(oldPlayers.Keys.Order())
            && proof.JoinedHashes.Keys.Order().SequenceEqual(oldPlayers.Keys.Order()) && proof.SuppliedHashes.Keys.Order().SequenceEqual(oldPlayers.Keys.Order()), "Keep both exact player identities.");
        foreach (var (player, row) in newPlayers)
        {
            var old = oldPlayers[player]; Compare(old, row, ["reconnect_token_hash", "updated_at"]);
            Check(old["reconnect_token_hash"]!.GetValue<string>() == proof.SuppliedHashes[player]
                && row["reconnect_token_hash"]!.GetValue<string>() == proof.JoinedHashes[player]
                && proof.SuppliedHashes[player] != proof.JoinedHashes[player], "SQL must bind each actual returned new token and retire its old hash.");
        }
        void Compare(JsonNode old, JsonNode current, string[] allowed)
        {
            var first = old.DeepClone().AsObject(); var second = current.DeepClone().AsObject();
            foreach (var field in allowed) Check(first.Remove(field) && second.Remove(field), "Missing required authentication column " + field);
            Check(EqualJson(first, second), "Reconnect changed a protected column.");
            var updated = DateTimeOffset.Parse(current["updated_at"]!.GetValue<string>(), CultureInfo.InvariantCulture);
            Check(updated >= before.Started && updated <= after.Finished, "Authentication update must fall inside the measured SQL reconnect interval.");
        }
        static Dictionary<string, JsonNode> Players(Rows rows) => rows.Tables["match_players"].Select(r => r!)
            .ToDictionary(r => r["player_id"]!.GetValue<string>(), r => r, StringComparer.Ordinal);
    }
    internal static async Task VerifyCheckpoint(NpgsqlDataSource source, string room, string folder, string stage)
    {
        var current = await Capture(source, room);
        if (stage == "after-restart")
        {
            var previous = await Read<Rows>("sql-before-restart-rows.json");
            var rotation = await Read<Rotation>("reconnect-recover.json");
            ValidateExact(previous, rotation.Before, includeOtherRooms: false); ValidateRotation(rotation);
            ValidateExact(rotation.After, current, includeOtherRooms: false);
        }
        else if (stage == "final") ValidateExact(await Read<Rows>("network-end-finish-rows.json"), current, includeOtherRooms: false);
        await File.WriteAllTextAsync(Path.Combine(folder, "sql-" + stage + "-rows.json"), JsonSerializer.Serialize(current, Json));
        async Task<T> Read<T>(string name) => JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(Path.Combine(folder, name)), Json)!;
    }
    private static bool EqualJson(JsonNode? first, JsonNode? second)
        => string.Equals(first?.ToJsonString(), second?.ToJsonString(), StringComparison.Ordinal);
    private static string Hash(string token) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()))).ToLowerInvariant();
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
