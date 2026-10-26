using System.Globalization;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
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
        var inputErrors = new List<string>();
        long? openingSeed = long.TryParse(metadata.Seed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSeed)
            && metadata.Seed == parsedSeed.ToString(CultureInfo.InvariantCulture)
            ? parsedSeed : null;
        if (openingSeed is null)
            inputErrors.Add("stored match opening seed is missing or invalid");
        if (!RuleInputIdentity.IsCurrent(metadata.RuleInputs)
            || metadata.RulesetVersion != metadata.RuleInputs?.IdentitySha256
            || metadata.FaqVersion != metadata.RuleInputs?.AuthoritySha256)
            inputErrors.Add("stored match rule input identity/version is missing or differs from running inputs");
        await ValidateStoredRuleInputsAsync(connection, roomId, inputErrors, cancellationToken).ConfigureAwait(false);
        MatchState? authoritativeState = null;
        try
        {
            authoritativeState = await LoadAuthoritativeStateAsync(
                connection, roomId, metadata.LastEventSequence, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            inputErrors.Add("authoritative state has an invalid or missing recorded rule input identity/payload");
        }
        // Every production seat is now an original atomic journal input.
        // Metadata alone cannot manufacture a seat or migrate a development room.
        if (authoritativeState is null)
            inputErrors.Add("stored match requires authoritative state with recorded rule input identity");
        if (authoritativeState is not null && openingSeed is not null && authoritativeState.Seed != openingSeed
            && !commands.Any(command => command.CommandType == "DEV_SEED_SCENARIO"
                || command.CommandType.StartsWith("DEV_SEED_SCENARIO:", StringComparison.Ordinal)))
            inputErrors.Add("authoritative state seed differs from recorded opening seed");
        var replayInitialState = authoritativeState is not null && commands.Count > 0 && openingSeed is not null
            ? (await LoadReplayInitialStateAsync(connection, roomId, openingSeed.Value, cancellationToken,
                commands[0].Accepted && commands[0].CommandType == MatchSession.RoomSeatCommandType).ConfigureAwait(false))
                with { RuleInputs = metadata.RuleInputs }
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
            metadata.CurrentTick,
            spectatorReplayFrame).Concat(inputErrors).ToArray();

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
            spectatorReplayFrame) { OpeningSeed = openingSeed ?? 0 };
    }

    private static async Task<MatchRecoveryMetadata?> LoadMetadataAsync(
        NpgsqlConnection connection,
        string roomId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select match_id, current_tick, last_event_sequence, rule_inputs::text, ruleset_version, faq_version, seed
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
            reader.GetInt64(2),
            reader.IsDBNull(3) ? null : ReadRuleInputs(reader.GetString(3)),
            reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6));
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
                   accepted, error_message, payload::text, rule_inputs::text
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
            var payload = reader.GetString(9);
            var errorCode = ReadCommandErrorCode(payload);
            commands.Add(new RecoveredCommand(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadRawCommand(payload),
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                reader.GetInt64(6),
                reader.GetBoolean(7),
                reader.IsDBNull(8) ? null : reader.GetString(8))
            {
                ErrorCode = errorCode,
                TypedCommand = ReadTypedCommand(reader.GetString(9)),
                RuleInputs = reader.IsDBNull(10) ? null : ReadRuleInputs(reader.GetString(10))
            });
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
        long openingSeed,
        CancellationToken cancellationToken,
        bool recordedRoomSeats = false)
    {
        if (recordedRoomSeats)
            return MatchState.Create(roomId) with { Seed = openingSeed };

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

        return MatchReplayInitialStateBuilder.FromSeats(roomId, seats) with { Seed = openingSeed };
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

    private static string? ReadCommandErrorCode(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("errorCode", out var code)
            || code.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
            throw new MatchSessionException(ErrorCodes.RecoveryInconsistent,
                "recorded command error code is missing or has an invalid JSON type");
        return code.ValueKind == JsonValueKind.Null ? null : code.GetString();
    }

    private static TypedCommandInput? ReadTypedCommand(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("typedCommand", out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        return value.Deserialize<TypedCommandInput>(JsonOptions)
            ?? throw new JsonException("Unable to deserialize typed command input.");
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
        long LastEventSequence,
        RuleInputIdentity? RuleInputs,
        string RulesetVersion,
        string FaqVersion,
        string? Seed);

    private static RuleInputIdentity? ReadRuleInputs(string json)
    {
        try { return Deserialize<RuleInputIdentity>(json); }
        catch (JsonException) { return null; }
    }

    private static async Task ValidateStoredRuleInputsAsync(NpgsqlConnection connection, string roomId,
        List<string> errors, CancellationToken cancellationToken)
    {
        // Fixed internal table names; every persisted row, including rejected
        // commands and older snapshots, must belong to the same actual inputs.
        foreach (var table in new[] { "matches", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
        {
            var payloadCheck = table switch
            {
                "command_log" => " or payload->'ruleInputs' is distinct from @rule_inputs or payload->>'rulesetVersion' is distinct from @ruleset_version or payload->>'faqVersion' is distinct from @faq_version",
                "state_snapshots" => " or payload->'ruleInputs' is distinct from @rule_inputs",
                _ => ""
            };
            var sql = $"select count(*) from {table} where match_id = @match_id and (rule_inputs is distinct from @rule_inputs or ruleset_version <> @ruleset_version or faq_version <> @faq_version{payloadCheck})";
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("match_id", roomId);
            command.Parameters.AddWithValue("rule_inputs", NpgsqlDbType.Jsonb,
                JsonSerializer.Serialize(RuleInputIdentity.Current, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            command.Parameters.AddWithValue("ruleset_version", RuleInputIdentity.Current.IdentitySha256);
            command.Parameters.AddWithValue("faq_version", RuleInputIdentity.Current.AuthoritySha256);
            if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is long count && count > 0)
                errors.Add($"{table} has {count} row(s) with missing or mismatched rule input identity/version");
        }
    }

    private sealed record RecoveredPrompt(
        long Tick,
        long LastEventSequence,
        ActionPromptDto Prompt);
}
