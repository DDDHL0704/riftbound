using System.Text.Encodings.Web;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Engine;

namespace Riftbound.Persistence;

public sealed class PostgresMatchJournal(NpgsqlDataSource dataSource) : IMatchJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await UpsertMatchAsync(connection, transaction, entry, cancellationToken).ConfigureAwait(false);
        await InsertCommandAsync(connection, transaction, entry, cancellationToken).ConfigureAwait(false);
        await InsertEventsAsync(connection, transaction, entry, cancellationToken).ConfigureAwait(false);
        await UpsertSnapshotsAsync(connection, transaction, entry, cancellationToken).ConfigureAwait(false);
        await UpsertPromptsAsync(connection, transaction, entry, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertMatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MatchJournalEntry entry,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into matches (match_id, status, current_tick, updated_at)
            values (@match_id, 'IN_PROGRESS', @current_tick, now())
            on conflict (match_id) do update
            set current_tick = greatest(matches.current_tick, excluded.current_tick),
                updated_at = now();
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("match_id", entry.RoomId);
        command.Parameters.AddWithValue("current_tick", entry.CompletedTick);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertCommandAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MatchJournalEntry entry,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into command_log (
                match_id, player_id, client_intent_id, command_type,
                started_tick, completed_tick, accepted, error_message, payload
            )
            values (
                @match_id, @player_id, @client_intent_id, @command_type,
                @started_tick, @completed_tick, @accepted, @error_message, @payload
            )
            on conflict (match_id, player_id, client_intent_id) do nothing;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("match_id", entry.RoomId);
        command.Parameters.AddWithValue("player_id", entry.PlayerId);
        command.Parameters.AddWithValue("client_intent_id", entry.ClientIntentId);
        command.Parameters.AddWithValue("command_type", entry.CommandType);
        command.Parameters.AddWithValue("started_tick", entry.StartedTick);
        command.Parameters.AddWithValue("completed_tick", entry.CompletedTick);
        command.Parameters.AddWithValue("accepted", entry.Accepted);
        command.Parameters.AddWithValue("error_message", (object?)entry.ErrorMessage ?? DBNull.Value);
        AddJson(command, "payload", new
        {
            entry.RoomId,
            entry.PlayerId,
            entry.ClientIntentId,
            entry.CommandType,
            entry.Accepted,
            entry.ErrorMessage
        });
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertEventsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MatchJournalEntry entry,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into game_events (match_id, event_tick, event_order, event_type, payload)
            values (@match_id, @event_tick, @event_order, @event_type, @payload)
            on conflict (match_id, event_tick, event_order) do nothing;
            """;

        for (var i = 0; i < entry.Events.Count; i++)
        {
            var gameEvent = entry.Events[i];
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("match_id", entry.RoomId);
            command.Parameters.AddWithValue("event_tick", entry.CompletedTick);
            command.Parameters.AddWithValue("event_order", i);
            command.Parameters.AddWithValue("event_type", gameEvent.Kind);
            AddJson(command, "payload", gameEvent);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task UpsertSnapshotsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MatchJournalEntry entry,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into snapshots (match_id, player_id, snapshot_tick, payload)
            values (@match_id, @player_id, @snapshot_tick, @payload)
            on conflict (match_id, player_id, snapshot_tick) do update
            set payload = excluded.payload,
                created_at = now();
            """;

        foreach (var (playerId, snapshot) in entry.Snapshots)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("match_id", entry.RoomId);
            command.Parameters.AddWithValue("player_id", playerId);
            command.Parameters.AddWithValue("snapshot_tick", snapshot.Tick);
            AddJson(command, "payload", snapshot);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task UpsertPromptsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MatchJournalEntry entry,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into action_prompts (match_id, player_id, prompt_tick, payload)
            values (@match_id, @player_id, @prompt_tick, @payload)
            on conflict (match_id, player_id, prompt_tick) do update
            set payload = excluded.payload,
                created_at = now();
            """;

        foreach (var (playerId, prompt) in entry.Prompts)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("match_id", entry.RoomId);
            command.Parameters.AddWithValue("player_id", playerId);
            command.Parameters.AddWithValue("prompt_tick", entry.CompletedTick);
            AddJson(command, "payload", prompt);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void AddJson<T>(NpgsqlCommand command, string name, T value)
    {
        command.Parameters.AddWithValue(name, NpgsqlDbType.Jsonb, JsonSerializer.Serialize(value, JsonOptions));
    }
}
