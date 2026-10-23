using System.Data;
using Npgsql;

// Read-only evidence shared by the C5/C6 and C7 acceptance leaves.
internal static class NetworkReconnectSqlCapture
{
    private static readonly string[] SqlTables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];
    internal sealed record SqlSample(DateTimeOffset Clock, Dictionary<string, string[]> Tables);

    internal static async Task<SqlSample> CaptureSql(NpgsqlDataSource source, string room, CancellationToken token)
    {
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        await using (var readOnly = new NpgsqlCommand("set transaction read only", connection, transaction))
            await readOnly.ExecuteNonQueryAsync(token);
        var tables = new Dictionary<string, string[]>();
        foreach (var table in SqlTables) {
            var rows = new List<string>();
            await using var query = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection, transaction);
            query.Parameters.AddWithValue("room", room);
            await using var reader = await query.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) rows.Add(reader.GetString(0));
            tables[table] = rows.ToArray();
        }
        await using var clock = new NpgsqlCommand("select clock_timestamp()", connection, transaction);
        var at = new DateTimeOffset((DateTime)(await clock.ExecuteScalarAsync(token))!);
        await transaction.CommitAsync(token);
        return new(at, tables);
    }

    internal static bool SameSql(SqlSample first, SqlSample second)
        => SqlTables.All(table => first.Tables[table].SequenceEqual(second.Tables[table], StringComparer.Ordinal));

}
