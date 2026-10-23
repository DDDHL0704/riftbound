using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static partial class RestrictedManaSqlAcceptance
{
    private sealed record StoredRow(string Table, long Id, string Payload);

    public static async Task TamperAsync(NpgsqlDataSource source, string output)
    {
        foreach (var kind in Cases)
        {
            var folder = Path.Combine(output, kind);
            using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(folder, "network-fixture.json")));
            var root = fixture.RootElement; var room = root.GetProperty("room").GetString()!;
            var actor = root.GetProperty("actor").GetString()!; var peer = root.GetProperty("peer").GetString()!;
            var original = await Load(source, room); var digest = await Digest(source, room);
            var rows = new List<StoredRow>(); await using var connection = await source.OpenConnectionAsync();
            foreach (var (table, tickColumn) in new[] { ("state_snapshots", "state_tick"), ("snapshots", "snapshot_tick"), ("action_prompts", "prompt_tick") })
            {
                await using var query = new NpgsqlCommand($"select id,payload::text from {table} where match_id=@room and {tickColumn}=(select max({tickColumn}) from {table} where match_id=@room) order by id", connection);
                query.Parameters.AddWithValue("room", room); await using var reader = await query.ExecuteReaderAsync();
                while (await reader.ReadAsync()) rows.Add(new(table, reader.GetInt64(0), reader.GetString(1)));
            }
            Check(rows.Count == 5, "Capture exactly one current authoritative row, two snapshots and two prompts.");
            var outcomes = new List<object>();
            foreach (var attack in new[] { "state-negative", "state-string", "state-overflow", "private-positive", "peer-positive", "coherent-forged-balance" })
            {
                string? refusal = null; bool? shapeConsistent = null;
                try
                {
                    if (attack == "coherent-forged-balance")
                    {
                        var state = original.AuthoritativeState!;
                        var forged = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                            [actor] = state.RunePools[actor] with { SpellOnlyMana = 3 } } };
                        var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
                        foreach (var row in rows)
                        {
                            if (row.Table == "state_snapshots") { await Write(row, JsonSerializer.Serialize(forged, Json)); continue; }
                            // Snapshot payloads do not carry a viewer id. Bind each exact row
                            // using its immutable database owner rather than guessing from hidden data.
                            var player = await Owner(row);
                            await Write(row, JsonSerializer.Serialize(row.Table == "snapshots" ? (object)snapshots[player] : prompts[player], Json));
                        }
                        var coherent = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default);
                        Check(coherent is not null && coherent.IsConsistent, "The positive forged balance and every regenerated view must first pass structural consistency.");
                        shapeConsistent = true;
                        Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(coherent!, new CoreRuleEngine(), default)).Count > 0,
                            "Only original birth/action replay may disprove this coherent forged resource result.");
                    }
                    else if (attack.StartsWith("state-", StringComparison.Ordinal))
                    {
                        var row = rows.Single(r => r.Table == "state_snapshots"); var node = JsonNode.Parse(row.Payload)!;
                        node["runePools"]![actor]!["spellOnlyMana"] = attack switch {
                            "state-negative" => JsonValue.Create(-1), "state-string" => JsonValue.Create("2"), _ => JsonValue.Create(2147483648L) };
                        await Write(row, node.ToJsonString());
                    }
                    else
                    {
                        var player = attack == "private-positive" ? actor : peer;
                        foreach (var row in rows.Where(r => r.Table == "snapshots"))
                            if (await Owner(row) == player)
                            { var node = JsonNode.Parse(row.Payload)!; node["players"]![actor]!["runePool"]!["spellOnlyMana"] = 99; await Write(row, node.ToJsonString()); }
                    }
                    var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new PostgresMatchJournal(source),
                        new PostgresMatchRecoveryStore(source), new PostgresMatchPlayerStore(source));
                    try { await registry.GetOrCreateAsync(room, default); }
                    catch (MatchSessionException error) when (error.Code == ErrorCodes.RecoveryInconsistent) { refusal = error.Message; }
                    Check(refusal is not null, "A fresh registry must refuse the deliberately forged restricted balance: " + attack);
                }
                finally { foreach (var row in rows) await Write(row, row.Payload); }
                Check(await Digest(source, room) == digest, "Restore every original payload and all seven table digests after " + attack);
                await Load(source, room);
                outcomes.Add(new { attack, shapeConsistent, refusal, originalSqlRestored = true, sevenTableDigestRestored = true });
            }
            await Save(folder, "registry-forgery-refusals.json", new { room, outcomes, originalBirthReplayRestored = true });

            async Task Write(StoredRow row, string value)
            {
                await using var update = new NpgsqlCommand($"update {row.Table} set payload=@value::jsonb where id=@id and match_id=@room", connection);
                update.Parameters.AddWithValue("value", value); update.Parameters.AddWithValue("id", row.Id); update.Parameters.AddWithValue("room", room);
                Check(await update.ExecuteNonQueryAsync() == 1, "Mutate only a captured current row of this new acceptance room.");
            }
            async Task<string> Owner(StoredRow row)
            {
                await using var query = new NpgsqlCommand($"select player_id from {row.Table} where id=@id and match_id=@room", connection);
                query.Parameters.AddWithValue("id", row.Id); query.Parameters.AddWithValue("room", room);
                return (string)(await query.ExecuteScalarAsync())!;
            }
        }
    }
}
