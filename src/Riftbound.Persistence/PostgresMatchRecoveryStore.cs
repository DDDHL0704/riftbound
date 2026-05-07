using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;

namespace Riftbound.Persistence;

public sealed class PostgresMatchRecoveryStore(NpgsqlDataSource dataSource) : IMatchRecoveryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var metadata = await LoadMetadataAsync(connection, roomId, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return null;
        }

        var commands = await LoadCommandsAsync(connection, roomId, cancellationToken).ConfigureAwait(false);
        var events = await LoadEventsAsync(connection, roomId, cancellationToken).ConfigureAwait(false);
        var playerViews = await LoadPlayerViewsAsync(connection, roomId, cancellationToken).ConfigureAwait(false);
        var authoritativeState = await LoadAuthoritativeStateAsync(
            connection,
            roomId,
            metadata.LastEventSequence,
            cancellationToken).ConfigureAwait(false);
        var replayInitialState = authoritativeState is not null && commands.Count > 0
            ? await LoadReplayInitialStateAsync(connection, roomId, cancellationToken).ConfigureAwait(false)
            : null;
        var spectatorReplayFrame = authoritativeState is null
            ? null
            : MatchReplayRedactor.BuildSpectatorFrame(
                metadata.RoomId,
                authoritativeState.Tick,
                metadata.LastEventSequence,
                events.Select(recoveredEvent => recoveredEvent.Event).ToArray(),
                authoritativeState);
        var validationErrors = MatchRecoveryValidator.Validate(
            metadata.RoomId,
            metadata.LastEventSequence,
            commands,
            events,
            playerViews,
            authoritativeState,
            metadata.CurrentTick);

        return new MatchRecoveryFrame(
            metadata.RoomId,
            metadata.CurrentTick,
            metadata.LastEventSequence,
            commands,
            events,
            playerViews,
            validationErrors,
            authoritativeState,
            replayInitialState,
            spectatorReplayFrame);
    }

    private static async Task<MatchRecoveryMetadata?> LoadMetadataAsync(
        NpgsqlConnection connection,
        string roomId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select match_id, current_tick, last_event_sequence
            from matches
            where match_id = @match_id;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("match_id", roomId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new MatchRecoveryMetadata(
            reader.GetString(0),
            reader.GetInt64(1),
            reader.GetInt64(2));
    }

    private static async Task<IReadOnlyList<RecoveredCommand>> LoadCommandsAsync(
        NpgsqlConnection connection,
        string roomId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select player_id, client_intent_id, command_type,
                   started_tick, completed_tick,
                   started_event_sequence, completed_event_sequence,
                   accepted, error_message, payload::text
            from command_log
            where match_id = @match_id
            order by started_event_sequence, completed_event_sequence, id;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("match_id", roomId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var commands = new List<RecoveredCommand>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            commands.Add(new RecoveredCommand(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadRawCommand(reader.GetString(9)),
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                reader.GetInt64(6),
                reader.GetBoolean(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return commands;
    }

    private static async Task<IReadOnlyList<RecoveredEvent>> LoadEventsAsync(
        NpgsqlConnection connection,
        string roomId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select event_sequence, event_tick, event_order, payload::text
            from game_events
            where match_id = @match_id
            order by event_sequence;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("match_id", roomId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var events = new List<RecoveredEvent>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            events.Add(new RecoveredEvent(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetInt32(2),
                Deserialize<GameEvent>(reader.GetString(3))));
        }

        return events;
    }

    private static async Task<IReadOnlyDictionary<string, RecoveredPlayerView>> LoadPlayerViewsAsync(
        NpgsqlConnection connection,
        string roomId,
        CancellationToken cancellationToken)
    {
        var prompts = await LoadLatestPromptsAsync(connection, roomId, cancellationToken).ConfigureAwait(false);
        const string sql = """
            select distinct on (player_id)
                   player_id, snapshot_tick, last_event_sequence, payload::text
            from snapshots
            where match_id = @match_id
            order by player_id, last_event_sequence desc, snapshot_tick desc, id desc;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("match_id", roomId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var views = new Dictionary<string, RecoveredPlayerView>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var playerId = reader.GetString(0);
            prompts.TryGetValue(playerId, out var prompt);
            views[playerId] = new RecoveredPlayerView(
                playerId,
                reader.GetInt64(1),
                reader.GetInt64(2),
                Deserialize<SnapshotDto>(reader.GetString(3)),
                prompt?.Tick,
                prompt?.LastEventSequence,
                prompt?.Prompt);
        }

        return views;
    }

    private static async Task<MatchState?> LoadAuthoritativeStateAsync(
        NpgsqlConnection connection,
        string roomId,
        long lastEventSequence,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select payload::text
            from state_snapshots
            where match_id = @match_id
              and last_event_sequence = @last_event_sequence
            order by last_event_sequence desc, state_tick desc, id desc
            limit 1;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("match_id", roomId);
        command.Parameters.AddWithValue("last_event_sequence", lastEventSequence);
        var payload = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return payload is string json ? Deserialize<MatchState>(json) : null;
    }

    private static async Task<MatchState> LoadReplayInitialStateAsync(
        NpgsqlConnection connection,
        string roomId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select player_id, seat
            from match_players
            where match_id = @match_id
            order by
                case seat
                    when 'P1' then 0
                    when 'P2' then 1
                    else 10
                end,
                joined_at,
                player_id;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("match_id", roomId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var seats = new Dictionary<string, string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            seats[reader.GetString(0)] = reader.GetString(1);
        }

        return MatchReplayInitialStateBuilder.FromSeats(roomId, seats);
    }

    private static async Task<IReadOnlyDictionary<string, RecoveredPrompt>> LoadLatestPromptsAsync(
        NpgsqlConnection connection,
        string roomId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select distinct on (player_id)
                   player_id, prompt_tick, last_event_sequence, payload::text
            from action_prompts
            where match_id = @match_id
            order by player_id, last_event_sequence desc, prompt_tick desc, id desc;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("match_id", roomId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var prompts = new Dictionary<string, RecoveredPrompt>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            prompts[reader.GetString(0)] = new RecoveredPrompt(
                reader.GetInt64(1),
                reader.GetInt64(2),
                Deserialize<ActionPromptDto>(reader.GetString(3)));
        }

        return prompts;
    }

    private static JsonElement? ReadRawCommand(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("rawCommand", out var rawCommand)
            || rawCommand.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return rawCommand.Clone();
    }

    private static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new JsonException($"Unable to deserialize {typeof(T).Name} recovery payload.");
    }

    private sealed record MatchRecoveryMetadata(
        string RoomId,
        long CurrentTick,
        long LastEventSequence);

    private sealed record RecoveredPrompt(
        long Tick,
        long LastEventSequence,
        ActionPromptDto Prompt);
}
