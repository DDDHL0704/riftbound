using Npgsql;
using NpgsqlTypes;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;

namespace Riftbound.Persistence;

public sealed class PostgresMatchPlayerStore(NpgsqlDataSource dataSource) : IMatchPlayerStore
{
    public async ValueTask SavePlayerSessionAsync(
        string roomId,
        string playerId,
        string seat,
        string reconnectTokenHash,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await AcquireMatchPlayerSeatLockAsync(connection, transaction, roomId, cancellationToken).ConfigureAwait(false);
        await EnsureMatchAsync(connection, transaction, roomId, cancellationToken).ConfigureAwait(false);
        try
        {
            await UpsertPlayerAsync(
                    connection,
                    transaction,
                    roomId,
                    playerId,
                    seat,
                    reconnectTokenHash,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw PlayerSeatConflict(roomId, playerId, seat, exception);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> HasReconnectTokenHashAsync(
        string roomId,
        string playerId,
        string reconnectTokenHash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select exists (
                select 1
                from match_players
                where match_id = @match_id
                  and player_id = @player_id
                  and reconnect_token_hash = @reconnect_token_hash
            );
            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("match_id", roomId);
        command.Parameters.AddWithValue("player_id", playerId);
        command.Parameters.AddWithValue("reconnect_token_hash", reconnectTokenHash);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
    }

    private static async Task EnsureMatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string roomId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into matches (match_id, status, updated_at, rule_inputs, ruleset_version, faq_version)
            values (@match_id, 'SEATING', now(), @rule_inputs, @ruleset_version, @faq_version)
            on conflict (match_id) do update
            set updated_at = now()
            where matches.rule_inputs = excluded.rule_inputs
              and matches.ruleset_version = excluded.ruleset_version
              and matches.faq_version = excluded.faq_version;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("match_id", roomId);
        command.Parameters.AddWithValue("rule_inputs", NpgsqlDbType.Jsonb,
            JsonSerializer.Serialize(RuleInputIdentity.Current, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        command.Parameters.AddWithValue("ruleset_version", RuleInputIdentity.Current.IdentitySha256);
        command.Parameters.AddWithValue("faq_version", RuleInputIdentity.Current.AuthoritySha256);
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            throw new MatchSessionException(ErrorCodes.RecoveryInconsistent,
                "stored room rule input identity differs from running inputs");
    }

    private static async Task AcquireMatchPlayerSeatLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string roomId,
        CancellationToken cancellationToken)
    {
        const string sql = "select pg_advisory_xact_lock(hashtextextended(@match_id, 0::bigint));";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("match_id", roomId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertPlayerAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string roomId,
        string playerId,
        string seat,
        string reconnectTokenHash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into match_players (
                match_id, player_id, seat, reconnect_token_hash, connection_state, updated_at
            )
            select
                @match_id, @player_id, @seat, @reconnect_token_hash, 'CONNECTED', now()
            where not exists (
                select 1
                from match_players
                where match_id = @match_id
                  and seat = @seat
                  and player_id <> @player_id
            )
            on conflict (match_id, player_id) do update
            set reconnect_token_hash = excluded.reconnect_token_hash,
                connection_state = 'CONNECTED',
                updated_at = now()
            where match_players.seat = excluded.seat;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("match_id", roomId);
        command.Parameters.AddWithValue("player_id", playerId);
        command.Parameters.AddWithValue("seat", seat);
        command.Parameters.AddWithValue("reconnect_token_hash", reconnectTokenHash);
        var changed = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (changed == 0)
        {
            throw PlayerSeatConflict(roomId, playerId, seat);
        }
    }

    private static InvalidOperationException PlayerSeatConflict(
        string roomId,
        string playerId,
        string seat,
        Exception? innerException = null)
    {
        return new InvalidOperationException(
            $"Postgres match player store rejected duplicate seat or player seat drift for room '{roomId}', player '{playerId}', seat '{seat}'.",
            innerException);
    }
}
