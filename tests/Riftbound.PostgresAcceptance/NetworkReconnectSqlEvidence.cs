using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;

// Reconnect may rotate credentials; every other boundary is measured separately.
internal static class NetworkReconnectSqlEvidence
{
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];

    internal static async Task<string> VerifyAsync(NpgsqlDataSource source, string output, string key, string room,
        string stage, string p1, string p2)
    {
        using var network = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, key + "-network-" + stage + ".json")));
        var observed = network.RootElement;
        Check(observed.GetProperty("room").GetString() == room && observed.GetProperty("failure").ValueKind == JsonValueKind.Null
            && observed.GetProperty("recover").GetBoolean() == (stage == "after-restart"), "Use the actual successful room/stage network evidence.");
        using var credentials = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output,
            stage == "after-restart" ? "network-resume.json" : "network-fixture.json")));
        var priorCredentials = credentials.RootElement.GetProperty("cases").EnumerateArray()
            .Single(row => row.GetProperty("room").GetString() == room).GetProperty("tokens");
        Check(JsonNode.DeepEquals(JsonNode.Parse(priorCredentials.GetRawText()), JsonNode.Parse(observed.GetProperty("consumedTokens").GetRawText())),
            "Reconnect must consume the previous actual Joined tokens.");
        CheckReconnectRows(observed, room, p1, p2);
        CheckTrace(observed, room, p1, p2);
        using var resume = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output,
            stage == "after-restart" ? "network-resume-after-restart.json" : "network-resume.json")));
        var saved = resume.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("room").GetString() == room);
        Check(saved.GetProperty("tokens").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(new[] { p1, p2 }.Order(StringComparer.Ordinal)), "Save exactly both successfully joined identities.");
        foreach (var player in new[] { p1, p2 })
            Check(saved.GetProperty("tokens").GetProperty(player).GetString()
                == observed.GetProperty("joined").GetProperty(player).GetProperty("reconnectToken").GetString(),
                "The next resume must retain each own actual Joined token.");
        var digest = await Digest(source, room);
        Check(CapturedDigest(observed.GetProperty("finalSql")) == digest,
            "The real network final SQL sample must match every current row in all seven tables.");
        if (stage == "after-restart") {
            using var before = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "sql-before-restart.json")));
            var prior = before.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("room").GetString() == room);
            Check(prior.GetProperty("sevenTableDigest").GetString() == CapturedDigest(observed.GetProperty("beforeReconnect")),
                "Actual process restart alone must preserve all seven tables before either reconnect.");
            var afterReconnect = CapturedDigest(observed.GetProperty("afterReconnect"));
            Check(afterReconnect == CapturedDigest(observed.GetProperty("duplicateBefore"))
                && afterReconnect == CapturedDigest(observed.GetProperty("duplicateAfter")) && afterReconnect == digest,
                "The actual duplicate and read-only view requests must each preserve their own post-reconnect seven-table baseline.");
        }
        return digest;
    }

    internal static void CheckTrace(JsonElement observed, string room, string p1, string p2)
    {
        var recover = observed.GetProperty("recover").GetBoolean();
        var expected = observed.GetProperty("expectedErrors").EnumerateArray().ToArray();
        Check(expected.Length == (recover ? 0 : 2), "Only the two deliberate first-phase identity/stale rejections may emit Error.");
        Check(expected.All(error => new[] { p1, p2 }.Contains(error.GetProperty("recipient").GetString(), StringComparer.Ordinal)
            && new[] { p1, p2 }.Contains(error.GetProperty("playerId").GetString(), StringComparer.Ordinal)),
            "Expected Error identities must remain the room's two actual players.");
        if (!recover) Check(expected[0].GetProperty("code").GetString() == ErrorCodes.IdentityMismatch
            && expected[0].GetProperty("recipient").GetString() != expected[0].GetProperty("playerId").GetString()
            && expected[1].GetProperty("code").GetString() == ErrorCodes.PromptExpired
            && expected[1].GetProperty("recipient").GetString() == expected[1].GetProperty("playerId").GetString(),
            "Expected Error identities/codes must match the deliberate peer submission and stale prompt.");
        var traces = observed.GetProperty("traces").EnumerateArray().ToArray();
        var joins = traces.Where(row => row.GetProperty("method").GetString() == "Joined").ToArray();
        Check(joins.Length == 2, "Two actual successful Joined messages are required; authenticated views alone cannot prove reconnect.");
        foreach (var player in new[] { p1, p2 }) {
            var joined = joins.Single(row => row.GetProperty("recipient").GetString() == player).GetProperty("message");
            Check(joined.GetProperty("roomId").GetString() == room && joined.GetProperty("playerId").GetString() == player
                && joined.GetProperty("type").GetString() == "RECONNECT"
                && JsonNode.DeepEquals(JsonNode.Parse(joined.GetProperty("payload").GetRawText()),
                    JsonNode.Parse(observed.GetProperty("joined").GetProperty(player).GetRawText())),
                "Each saved session must come from that recipient's real successful Reconnect Joined.");
        }
        var errors = traces.Where(row => row.GetProperty("method").GetString() == "Error").ToArray();
        Check(errors.Length == expected.Length, "No missing, extra, invalid-token or otherwise unexpected network Error may be swallowed.");
        for (var i = 0; i < errors.Length; i++) {
            var actual = errors[i]; var message = actual.GetProperty("message");
            Check(actual.GetProperty("recipient").GetString() == expected[i].GetProperty("recipient").GetString()
                && message.GetProperty("roomId").GetString() == room && message.GetProperty("type").GetString() == "ERROR"
                && message.GetProperty("playerId").GetString() == expected[i].GetProperty("playerId").GetString()
                && message.GetProperty("payload").GetProperty("code").GetString() == expected[i].GetProperty("code").GetString(),
                "Each actual Error must match its specifically armed rejection recipient, requested player and code.");
        }
    }

    internal static void CheckReconnectRows(JsonElement observed, string room, string p1, string p2)
    {
        var before = observed.GetProperty("beforeReconnect"); var after = observed.GetProperty("afterReconnect");
        _ = CapturedDigest(before); _ = CapturedDigest(after);
        var start = before.GetProperty("clock").GetDateTimeOffset(); var end = after.GetProperty("clock").GetDateTimeOffset();
        Check(end >= start, "Use ordered database-clock samples around the actual reconnects.");
        foreach (var table in Tables.Skip(2))
            Check(CapturedRows(before, table).SequenceEqual(CapturedRows(after, table), StringComparer.Ordinal),
                "Reconnect changed an exact command/event/snapshot/prompt row in " + table + ".");
        var oldMatch = OneRow(before, "matches"); var newMatch = OneRow(after, "matches");
        Check(oldMatch["match_id"]!.GetValue<string>() == room && newMatch["match_id"]!.GetValue<string>() == room,
            "Reconnect samples must retain the original room row.");
        var matchAt = ChangedAt(oldMatch, newMatch);
        oldMatch.Remove("updated_at"); newMatch.Remove("updated_at");
        Check(JsonNode.DeepEquals(oldMatch, newMatch), "Only this room's EnsureMatchAsync updated_at may change.");
        var beforePlayers = PlayerRows(before); var afterPlayers = PlayerRows(after);
        var expected = new[] { p1, p2 }.Order(StringComparer.Ordinal).ToArray();
        Check(beforePlayers.Keys.Order(StringComparer.Ordinal).SequenceEqual(expected)
            && afterPlayers.Keys.Order(StringComparer.Ordinal).SequenceEqual(expected)
            && observed.GetProperty("joined").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(expected)
            && observed.GetProperty("consumedTokens").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(expected),
            "Keep exactly the two real seated identities; no missing, extra or replaced rows/Joined sessions.");
        var rotatedAt = new List<DateTimeOffset>();
        foreach (var player in expected) {
            var original = beforePlayers[player]; var current = afterPlayers[player]; var joined = observed.GetProperty("joined").GetProperty(player);
            var consumed = observed.GetProperty("consumedTokens").GetProperty(player).GetString()!;
            var issued = joined.GetProperty("reconnectToken").GetString()!;
            Check(!string.IsNullOrWhiteSpace(issued) && issued != consumed && joined.GetProperty("playerId").GetString() == player
                && joined.GetProperty("seat").GetString() == original["seat"]!.GetValue<string>()
                && original["match_id"]!.GetValue<string>() == room && current["match_id"]!.GetValue<string>() == room
                && original["connection_state"]!.GetValue<string>() == "CONNECTED"
                && original["reconnect_token_hash"]!.GetValue<string>() == ReconnectTokenHasher.Hash(consumed)
                && current["reconnect_token_hash"]!.GetValue<string>() == ReconnectTokenHasher.Hash(issued),
                "Each own actual Joined token must replace exactly its previous persisted token hash, seat and identity intact.");
            rotatedAt.Add(ChangedAt(original, current));
            foreach (var field in new[] { "reconnect_token_hash", "updated_at" }) { original.Remove(field); current.Remove(field); }
            Check(JsonNode.DeepEquals(original, current), "Reconnect changed a player column outside its token hash and updated_at.");
        }
        Check(rotatedAt.All(at => at >= start && at <= end) && matchAt == rotatedAt.Max(),
            "Player timestamps must lie inside the database reconnect window; the match timestamp must equal the last same-transaction player update.");

        DateTimeOffset ChangedAt(JsonObject original, JsonObject current)
        {
            var oldAt = DateTimeOffset.Parse(original["updated_at"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);
            var newAt = DateTimeOffset.Parse(current["updated_at"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);
            Check(newAt > oldAt && newAt >= start && newAt <= end, "Only a real fresh timestamp inside the database reconnect window is permitted.");
            return newAt;
        }
    }

    private static string[] CapturedRows(JsonElement sample, string table)
        => sample.GetProperty("tables").GetProperty(table).EnumerateArray().Select(row => row.GetString()!).ToArray();
    private static JsonObject OneRow(JsonElement sample, string table)
        => JsonNode.Parse(CapturedRows(sample, table).Single())!.AsObject();
    private static Dictionary<string, JsonObject> PlayerRows(JsonElement sample)
        => CapturedRows(sample, "match_players").Select(row => JsonNode.Parse(row)!.AsObject())
            .ToDictionary(row => row["player_id"]!.GetValue<string>(), StringComparer.Ordinal);
    private static string CapturedDigest(JsonElement sample)
    {
        Check(sample.GetProperty("tables").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(Tables.Order(StringComparer.Ordinal)), "Capture every exact SQL table, with no omissions or replacements.");
        var rows = new StringBuilder();
        foreach (var table in Tables) { rows.AppendLine(table); foreach (var row in CapturedRows(sample, table)) rows.AppendLine(row); }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString()))).ToLowerInvariant();
    }

    private static async Task<string> Digest(NpgsqlDataSource source, string room)
    {
        var rows = new StringBuilder();
        await using var connection = await source.OpenConnectionAsync();
        foreach (var table in Tables)
        {
            rows.AppendLine(table);
            await using var query = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection);
            query.Parameters.AddWithValue("room", room);
            await using var reader = await query.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.AppendLine(reader.GetString(0));
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString()))).ToLowerInvariant();
    }

    private static void Check(bool value, string? message)
    { if (!value) throw new InvalidOperationException(message); }
}
