using Npgsql;
using Riftbound.Engine;

namespace Riftbound.Persistence;

public sealed class PostgresMatchResultStore(NpgsqlDataSource dataSource) : IMatchResultStore
{
    public async ValueTask RecordMatchResultAsync(MatchResultRecord result, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await UpsertMatchAsync(connection, transaction, result, cancellationToken).ConfigureAwait(false);
        await UpsertResultAsync(connection, transaction, result, cancellationToken).ConfigureAwait(false);
        await ReplaceResultPlayersAsync(connection, transaction, result, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<MatchResultRecord?> GetMatchResultAsync(
        string roomId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var result = await LoadResultAsync(connection, roomId, cancellationToken).ConfigureAwait(false);
        return result is null
            ? null
            : result with
            {
                Players = await LoadPlayersAsync(connection, result.RoomId, cancellationToken).ConfigureAwait(false)
            };
    }

    public async ValueTask<PlayerMatchStatsRecord> GetPlayerMatchStatsAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select count(*)::int,
                   count(*) filter (where won)::int
            from match_result_players
            where player_id = @player_id;
            """;
        var normalizedPlayerId = NormalizeRequired(playerId, nameof(playerId));
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("player_id", normalizedPlayerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new PlayerMatchStatsRecord(normalizedPlayerId, 0, 0, 0, 0);
        }

        var totalMatches = reader.GetInt32(0);
        var wins = reader.GetInt32(1);
        var losses = totalMatches - wins;
        return new PlayerMatchStatsRecord(
            normalizedPlayerId,
            totalMatches,
            wins,
            losses,
            totalMatches == 0 ? 0 : (double)wins / totalMatches);
    }

    public async ValueTask<IReadOnlyList<MatchResultRecord>> ListMatchResultsForPlayerAsync(
        string playerId,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select mr.match_id, mr.winner_player_id, mr.finished_at
            from match_results mr
            join match_result_players mrp on mrp.match_id = mr.match_id
            where mrp.player_id = @player_id
            order by mr.finished_at desc, mr.match_id
            limit @limit;
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("player_id", NormalizeRequired(playerId, nameof(playerId)));
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, 100));

        var results = new List<MatchResultRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadResult(reader));
        }

        await reader.CloseAsync().ConfigureAwait(false);
        for (var i = 0; i < results.Count; i++)
        {
            results[i] = results[i] with
            {
                Players = await LoadPlayersAsync(connection, results[i].RoomId, cancellationToken).ConfigureAwait(false)
            };
        }

        return results;
    }

    public async ValueTask<IReadOnlyList<PlayerLeaderboardEntryRecord>> ListLeaderboardAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select player_id,
                   count(*)::int as total_matches,
                   count(*) filter (where won)::int as wins
            from match_result_players
            group by player_id
            order by wins desc,
                     (case when count(*) = 0 then 0 else (count(*) filter (where won))::double precision / count(*) end) desc,
                     count(*) desc,
                     player_id
            limit @limit;
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, 100));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var entries = new List<PlayerLeaderboardEntryRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var playerId = reader.GetString(0);
            var totalMatches = reader.GetInt32(1);
            var wins = reader.GetInt32(2);
            var losses = totalMatches - wins;
            entries.Add(new PlayerLeaderboardEntryRecord(
                playerId,
                totalMatches,
                wins,
                losses,
                totalMatches == 0 ? 0 : (double)wins / totalMatches));
        }

        return entries;
    }

    private static async Task UpsertMatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MatchResultRecord result,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into matches (match_id, status, winner_player_id, updated_at)
            values (@match_id, 'FINISHED', @winner_player_id, now())
            on conflict (match_id) do update
            set status = 'FINISHED',
                winner_player_id = excluded.winner_player_id,
                updated_at = now();
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("match_id", NormalizeRequired(result.RoomId, nameof(result.RoomId)));
        command.Parameters.AddWithValue("winner_player_id", NormalizeRequired(result.WinnerPlayerId, nameof(result.WinnerPlayerId)));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertResultAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MatchResultRecord result,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into match_results (match_id, winner_player_id, finished_at, updated_at)
            values (@match_id, @winner_player_id, @finished_at, now())
            on conflict (match_id) do update
            set winner_player_id = excluded.winner_player_id,
                finished_at = excluded.finished_at,
                updated_at = now();
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("match_id", NormalizeRequired(result.RoomId, nameof(result.RoomId)));
        command.Parameters.AddWithValue("winner_player_id", NormalizeRequired(result.WinnerPlayerId, nameof(result.WinnerPlayerId)));
        command.Parameters.AddWithValue("finished_at", result.FinishedAtUtc.ToUniversalTime());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReplaceResultPlayersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MatchResultRecord result,
        CancellationToken cancellationToken)
    {
        await using (var delete = new NpgsqlCommand(
            "delete from match_result_players where match_id = @match_id;",
            connection,
            transaction))
        {
            delete.Parameters.AddWithValue("match_id", NormalizeRequired(result.RoomId, nameof(result.RoomId)));
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        const string insertSql = """
            insert into match_result_players (match_id, player_id, seat, score, won)
            values (@match_id, @player_id, @seat, @score, @won);
            """;
        foreach (var player in result.Players)
        {
            await using var insert = new NpgsqlCommand(insertSql, connection, transaction);
            insert.Parameters.AddWithValue("match_id", NormalizeRequired(result.RoomId, nameof(result.RoomId)));
            insert.Parameters.AddWithValue("player_id", NormalizeRequired(player.PlayerId, nameof(player.PlayerId)));
            insert.Parameters.AddWithValue("seat", NormalizeRequired(player.Seat, nameof(player.Seat)));
            insert.Parameters.AddWithValue("score", Math.Max(0, player.Score));
            insert.Parameters.AddWithValue("won", player.Won);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<MatchResultRecord?> LoadResultAsync(
        NpgsqlConnection connection,
        string roomId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select match_id, winner_player_id, finished_at
            from match_results
            where match_id = @match_id;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("match_id", NormalizeRequired(roomId, nameof(roomId)));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadResult(reader)
            : null;
    }

    private static async Task<IReadOnlyList<MatchResultPlayerRecord>> LoadPlayersAsync(
        NpgsqlConnection connection,
        string roomId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select player_id, seat, score, won
            from match_result_players
            where match_id = @match_id
            order by case seat when 'P1' then 0 when 'P2' then 1 else 2 end, player_id;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("match_id", NormalizeRequired(roomId, nameof(roomId)));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var players = new List<MatchResultPlayerRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            players.Add(new MatchResultPlayerRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetBoolean(3)));
        }

        return players;
    }

    private static MatchResultRecord ReadResult(NpgsqlDataReader reader)
    {
        return new MatchResultRecord(
            reader.GetString(0),
            [],
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2));
    }

    private static string NormalizeRequired(string? value, string name)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        return normalized;
    }
}
