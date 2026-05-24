using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record RecoveredCommand(
    string PlayerId,
    string ClientIntentId,
    string CommandType,
    JsonElement? RawCommand,
    long StartedTick,
    long CompletedTick,
    long StartedEventSequence,
    long CompletedEventSequence,
    bool Accepted,
    string? ErrorMessage);

public sealed record RecoveredEvent(
    long Sequence,
    long Tick,
    int Order,
    GameEvent Event);

public sealed record RecoveredPlayerView(
    string PlayerId,
    long SnapshotTick,
    long SnapshotEventSequence,
    SnapshotDto Snapshot,
    long? PromptTick,
    long? PromptEventSequence,
    ActionPromptDto? Prompt);

public sealed record MatchRecoveryFrame(
    string RoomId,
    long CurrentTick,
    long LastEventSequence,
    IReadOnlyList<RecoveredCommand> Commands,
    IReadOnlyList<RecoveredEvent> Events,
    IReadOnlyDictionary<string, RecoveredPlayerView> PlayerViews,
    IReadOnlyList<string> ValidationErrors,
    MatchState? AuthoritativeState = null,
    MatchState? ReplayInitialState = null,
    MatchReplayFrame? SpectatorReplayFrame = null)
{
    public bool IsConsistent => ValidationErrors.Count == 0;

    public long ReplayFromEventSequence =>
        PlayerViews.Count == 0 ? 0 : PlayerViews.Values.Min(view => view.SnapshotEventSequence);

    public IReadOnlyList<RecoveredEvent> EventsAfterReplayPoint =>
        Events.Where(gameEvent => gameEvent.Sequence > ReplayFromEventSequence).ToArray();
}

public sealed record MatchReplayFrame(
    string RoomId,
    long Tick,
    long EventSequence,
    IReadOnlyList<GameEvent> Events,
    string AuthoritativeStateHash,
    SnapshotDto SpectatorSnapshot);

public static class MatchReplayRedactor
{
    public static MatchReplayFrame BuildSpectatorFrame(MatchJournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return BuildSpectatorFrame(
            entry.RoomId,
            entry.CompletedTick,
            entry.CompletedEventSequence,
            entry.Events,
            entry.AuthoritativeState);
    }

    public static MatchReplayFrame BuildSpectatorFrame(
        string roomId,
        long tick,
        long eventSequence,
        IReadOnlyList<GameEvent> events,
        MatchState authoritativeState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(authoritativeState);

        return new MatchReplayFrame(
            roomId.Trim(),
            tick,
            eventSequence,
            events,
            MatchStateHasher.Hash(authoritativeState),
            ResolutionResult.BuildSpectatorSnapshot(authoritativeState));
    }
}

public sealed record MatchActionLogReplayResult(
    bool IsMatch,
    string ReplayedStateHash,
    string ExpectedStateHash,
    IReadOnlyList<string> Errors);

public static class MatchActionLogReplayer
{
    private const string DevSeedScenarioPrefix = "DEV_SEED_SCENARIO:";

    public static async ValueTask<IReadOnlyList<string>> ValidateRecoveryFrameAsync(
        MatchRecoveryFrame recovery,
        IRuleEngine ruleEngine,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(ruleEngine);

        if (recovery.Commands.Count == 0)
        {
            return [];
        }

        if (recovery.AuthoritativeState is null
            && recovery.ReplayInitialState is null)
        {
            return [];
        }

        var errors = new List<string>();
        if (recovery.AuthoritativeState is null)
        {
            errors.Add("action-log replay audit requires authoritative final state");
        }

        if (recovery.ReplayInitialState is null)
        {
            errors.Add("action-log replay audit requires replay initial state");
        }

        if (recovery.ReplayInitialState is { } replayInitialState)
        {
            if (!string.Equals(replayInitialState.RoomId, recovery.RoomId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"action-log replay initial state room {replayInitialState.RoomId} does not match recovery room {recovery.RoomId}");
            }

            if (replayInitialState.Tick != 0)
            {
                errors.Add($"action-log replay initial state tick {replayInitialState.Tick} must be 0");
            }

            if (replayInitialState.TurnNumber != 1)
            {
                errors.Add($"action-log replay initial state turn number {replayInitialState.TurnNumber} must be 1");
            }

            if (replayInitialState.Seats.Count == 0)
            {
                errors.Add("action-log replay initial state seats are required");
            }

            ValidateReplayInitialSeats(replayInitialState.Seats, errors);

            if (recovery.AuthoritativeState is { } authoritativeState
                && !StringMapEquals(replayInitialState.Seats, authoritativeState.Seats))
            {
                errors.Add("action-log replay initial state seats do not match authoritative final state seats");
            }

            if (!DictionaryKeysEqual(replayInitialState.RunePools, replayInitialState.Seats))
            {
                errors.Add("action-log replay initial state rune pool players must match seats");
            }

            foreach (var runePool in replayInitialState.RunePools.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if (!RunePool.Empty.Equals(runePool.Value))
                {
                    errors.Add($"action-log replay initial state rune pool for {runePool.Key} must be empty");
                }
            }

            if (!DictionaryKeysEqual(replayInitialState.PlayerZones, replayInitialState.Seats))
            {
                errors.Add("action-log replay initial state zone players must match seats");
            }

            foreach (var playerZones in replayInitialState.PlayerZones.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if (!IsEmptyPlayerZones(playerZones.Value))
                {
                    errors.Add($"action-log replay initial state zones for {playerZones.Key} must be empty");
                }
            }

            ValidateZeroCounterBaseline(
                "score",
                replayInitialState.PlayerScores,
                replayInitialState.Seats,
                errors);
            ValidateZeroCounterBaseline(
                "experience",
                replayInitialState.PlayerExperience,
                replayInitialState.Seats,
                errors);
            ValidateZeroCounterBaseline(
                "cards played this turn",
                replayInitialState.PlayerCardsPlayedThisTurn,
                replayInitialState.Seats,
                errors,
                allowMissingZeroEntries: true);

            ValidateEmptyListBaseline(
                "ready players",
                replayInitialState.ReadyPlayerIds,
                errors);
            ValidateEmptyListBaseline(
                "passed priority players",
                replayInitialState.PassedPriorityPlayerIds,
                errors);
            ValidateEmptyListBaseline(
                "passed focus players",
                replayInitialState.PassedFocusPlayerIds,
                errors);
            ValidateEmptyListBaseline(
                "mulligan completed players",
                replayInitialState.MulliganCompletedPlayerIds,
                errors);
            ValidateEmptyListBaseline(
                "destroyed unit owners this turn",
                replayInitialState.DestroyedUnitOwnerIdsThisTurn,
                errors);

            ValidateEmptyOptionalBaseline(
                "priority player",
                replayInitialState.PriorityPlayerId,
                errors);
            ValidateEmptyOptionalBaseline(
                "focus player",
                replayInitialState.FocusPlayerId,
                errors);
            ValidateEmptyOptionalBaseline(
                "winner player",
                replayInitialState.WinnerPlayerId,
                errors);
            ValidateEmptyOptionalBaseline(
                "opening second action player",
                replayInitialState.OpeningSecondActionPlayerId,
                errors);
            ValidateEmptyOptionalBaseline(
                "extra turn player",
                replayInitialState.ExtraTurnPlayerId,
                errors);

            ValidateEmptyDictionaryBaseline(
                "card objects",
                replayInitialState.CardObjects,
                errors);
            ValidateEmptyDictionaryBaseline(
                "object locations",
                replayInitialState.ObjectLocations,
                errors);
            ValidateEmptyDictionaryBaseline(
                "player decklists",
                replayInitialState.PlayerDecklists,
                errors);
            ValidateEmptyListBaseline(
                "stack items",
                replayInitialState.StackItems,
                errors);
            ValidateEmptyListBaseline(
                "trigger queue",
                replayInitialState.TriggerQueue,
                errors);
            ValidateEmptyListBaseline(
                "battlefield resolutions",
                replayInitialState.BattlefieldResolutions,
                errors);
            ValidateEmptyListBaseline(
                "battle resolutions",
                replayInitialState.BattleResolutions,
                errors);
            ValidateEmptyListBaseline(
                "until end of turn effects",
                replayInitialState.UntilEndOfTurnEffects,
                errors);
            ValidateEmptyListBaseline(
                "temporary payment resources",
                replayInitialState.TemporaryPaymentResources,
                errors);
            ValidateNullBaseline(
                "pending payment",
                replayInitialState.PendingPayment,
                errors);
            ValidateNullBaseline(
                "pending hand choice",
                replayInitialState.PendingHandChoice,
                errors);

            var expectedInitialPlayerId = ReplayInitialPlayerIdFor(replayInitialState);
            if (!string.Equals(replayInitialState.ActivePlayerId, expectedInitialPlayerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"action-log replay initial state active player {replayInitialState.ActivePlayerId} must be {expectedInitialPlayerId}");
            }

            if (!string.Equals(replayInitialState.TurnPlayerId, expectedInitialPlayerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"action-log replay initial state turn player {replayInitialState.TurnPlayerId} must be {expectedInitialPlayerId}");
            }

            if (!string.Equals(replayInitialState.Status, MatchStatuses.Seating, StringComparison.Ordinal))
            {
                errors.Add(
                    $"action-log replay initial state status {replayInitialState.Status} must be {MatchStatuses.Seating}");
            }

            if (!string.Equals(replayInitialState.Phase, MatchPhases.Room, StringComparison.Ordinal))
            {
                errors.Add(
                    $"action-log replay initial state phase {replayInitialState.Phase} must be {MatchPhases.Room}");
            }

            if (!string.Equals(replayInitialState.TimingState, TimingStates.Room, StringComparison.Ordinal))
            {
                errors.Add(
                    $"action-log replay initial state timing state {replayInitialState.TimingState} must be {TimingStates.Room}");
            }

            if (replayInitialState.Seed != 0)
            {
                errors.Add($"action-log replay initial state seed {replayInitialState.Seed} must be 0");
            }

            if (replayInitialState.RngCursor != 0)
            {
                errors.Add($"action-log replay initial state rng cursor {replayInitialState.RngCursor} must be 0");
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var replay = await VerifyFinalStateAsync(
                recovery.ReplayInitialState!,
                recovery.Commands,
                recovery.AuthoritativeState!,
                ruleEngine,
                cancellationToken,
                recovery.Events)
            .ConfigureAwait(false);
        return replay.IsMatch
            ? []
            : replay.Errors
                .Select(error => $"action-log replay audit failed: {error}")
                .ToArray();
    }

    private static string ReplayInitialPlayerIdFor(MatchState replayInitialState)
    {
        return replayInitialState.Seats
            .OrderBy(entry => ReplayInitialSeatSort(entry.Value))
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .FirstOrDefault()
            ?? "P1";
    }

    private static int ReplayInitialSeatSort(string seat)
    {
        return seat switch
        {
            "P1" => 0,
            "P2" => 1,
            _ => 10
        };
    }

    private static void ValidateReplayInitialSeats(
        IReadOnlyDictionary<string, string> seats,
        List<string> errors)
    {
        var seenSeats = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (playerId, seat) in seats.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var normalizedPlayerId = playerId.Trim();
            if (string.IsNullOrWhiteSpace(playerId))
            {
                errors.Add("action-log replay initial state seat player id is required");
            }
            else if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"action-log replay initial state seat player {normalizedPlayerId} has surrounding whitespace");
            }

            var playerLabel = string.IsNullOrWhiteSpace(normalizedPlayerId)
                ? "<blank>"
                : normalizedPlayerId;
            if (string.IsNullOrWhiteSpace(seat))
            {
                errors.Add($"action-log replay initial state seat for {playerLabel} is required");
                continue;
            }

            var normalizedSeat = seat.Trim();
            if (!string.Equals(seat, normalizedSeat, StringComparison.Ordinal))
            {
                errors.Add(
                    $"action-log replay initial state seat {normalizedSeat} for {playerLabel} has surrounding whitespace");
            }

            if (!IsKnownSeat(normalizedSeat))
            {
                errors.Add($"action-log replay initial state seat {normalizedSeat} for {playerLabel} is invalid");
            }

            if (!seenSeats.Add(normalizedSeat))
            {
                errors.Add($"action-log replay initial state seat {normalizedSeat} is duplicated");
            }
        }
    }

    private static bool IsKnownSeat(string seat)
    {
        return string.Equals(seat, "P1", StringComparison.Ordinal)
            || string.Equals(seat, "P2", StringComparison.Ordinal);
    }

    private static bool DictionaryKeysEqual<TValue>(
        IReadOnlyDictionary<string, TValue> values,
        IReadOnlyDictionary<string, string> seats)
    {
        return values.Count == seats.Count
            && values.Keys.All(seats.ContainsKey);
    }

    private static bool IsEmptyPlayerZones(PlayerZones zones)
    {
        return zones.MainDeck.Count == 0
            && zones.RuneDeck.Count == 0
            && zones.Hand.Count == 0
            && zones.Base.Count == 0
            && zones.Battlefields.Count == 0
            && zones.Graveyard.Count == 0
            && zones.Banished.Count == 0
            && zones.LegendZone.Count == 0
            && zones.ChampionZone.Count == 0;
    }

    private static void ValidateZeroCounterBaseline(
        string counterName,
        IReadOnlyDictionary<string, int> counters,
        IReadOnlyDictionary<string, string> seats,
        List<string> errors,
        bool allowMissingZeroEntries = false)
    {
        var playersMatchSeats = allowMissingZeroEntries
            ? counters.Keys.All(seats.ContainsKey)
            : DictionaryKeysEqual(counters, seats);
        if (!playersMatchSeats)
        {
            errors.Add($"action-log replay initial state {counterName} players must match seats");
        }

        foreach (var counter in counters.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (counter.Value != 0)
            {
                errors.Add(
                    $"action-log replay initial state {counterName} for {counter.Key} must be 0");
            }
        }
    }

    private static void ValidateEmptyDictionaryBaseline<TValue>(
        string mapName,
        IReadOnlyDictionary<string, TValue> values,
        List<string> errors)
    {
        if (values.Count > 0)
        {
            errors.Add($"action-log replay initial state {mapName} must be empty");
        }
    }

    private static void ValidateEmptyListBaseline<TValue>(
        string listName,
        IReadOnlyList<TValue> values,
        List<string> errors)
    {
        if (values.Count > 0)
        {
            errors.Add($"action-log replay initial state {listName} must be empty");
        }
    }

    private static void ValidateNullBaseline(
        string valueName,
        object? value,
        List<string> errors)
    {
        if (value is not null)
        {
            errors.Add($"action-log replay initial state {valueName} must be empty");
        }
    }

    private static void ValidateEmptyOptionalBaseline(
        string valueName,
        string? value,
        List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"action-log replay initial state {valueName} must be empty");
        }
    }

    private static bool StringMapEquals(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        return left.Count == right.Count
            && left.All(entry =>
                right.TryGetValue(entry.Key, out var rightValue)
                && string.Equals(entry.Value, rightValue, StringComparison.Ordinal));
    }

    public static async ValueTask<MatchActionLogReplayResult> VerifyFinalStateAsync(
        MatchState initialState,
        IReadOnlyList<RecoveredCommand> commands,
        MatchState expectedFinalState,
        IRuleEngine ruleEngine,
        CancellationToken cancellationToken,
        IReadOnlyList<RecoveredEvent>? events = null)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(expectedFinalState);
        ArgumentNullException.ThrowIfNull(ruleEngine);

        var errors = new List<string>();
        var session = new MatchSession(initialState, ruleEngine, NoopMatchJournal.Instance);
        var replayedState = initialState;
        foreach (var command in commands
            .OrderBy(command => command.StartedEventSequence)
            .ThenBy(command => command.CompletedEventSequence)
            .ThenBy(command => command.ClientIntentId, StringComparer.Ordinal))
        {
            ResolutionResult result;
            try
            {
                result = await ReplayCommandAsync(session, command, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is MatchSessionException or ArgumentException or InvalidOperationException or JsonException)
            {
                errors.Add($"command {command.ClientIntentId} failed to replay: {ex.Message}");
                continue;
            }

            if (result.Accepted != command.Accepted)
            {
                errors.Add(
                    $"command {command.ClientIntentId} accepted={result.Accepted} but recovered accepted={command.Accepted}");
            }

            if (!string.Equals(result.ErrorMessage, command.ErrorMessage, StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} error message {FormatReplayError(result.ErrorMessage)} but recovered error message {FormatReplayError(command.ErrorMessage)}");
            }

            if (result.State.Tick != command.CompletedTick)
            {
                errors.Add(
                    $"command {command.ClientIntentId} completed tick {result.State.Tick} but recovered tick {command.CompletedTick}");
            }

            var expectedEventCount = command.CompletedEventSequence - command.StartedEventSequence;
            if (result.Events.Count != expectedEventCount)
            {
                errors.Add(
                    $"command {command.ClientIntentId} replayed {result.Events.Count} event(s) but recovered span expects {expectedEventCount}");
            }

            if (events is not null)
            {
                var recoveredEvents = events
                    .Where(gameEvent =>
                        gameEvent.Sequence > command.StartedEventSequence
                        && gameEvent.Sequence <= command.CompletedEventSequence)
                    .OrderBy(gameEvent => gameEvent.Sequence)
                    .ThenBy(gameEvent => gameEvent.Order)
                    .ToArray();
                ValidateReplayedEvents(command, result.Events, recoveredEvents, errors);
            }

            replayedState = result.State;
        }

        var replayedHash = MatchStateHasher.Hash(replayedState);
        var expectedHash = MatchStateHasher.Hash(expectedFinalState);
        if (!string.Equals(replayedHash, expectedHash, StringComparison.Ordinal))
        {
            errors.Add($"replayed final state hash {replayedHash} does not match expected {expectedHash}");
        }

        return new MatchActionLogReplayResult(
            errors.Count == 0,
            replayedHash,
            expectedHash,
            errors);
    }

    private static string FormatReplayError(string? errorMessage)
    {
        return errorMessage is null ? "<null>" : $"\"{errorMessage}\"";
    }

    private static void ValidateReplayedEvents(
        RecoveredCommand command,
        IReadOnlyList<GameEvent> replayedEvents,
        IReadOnlyList<RecoveredEvent> recoveredEvents,
        List<string> errors)
    {
        if (replayedEvents.Count != recoveredEvents.Count)
        {
            errors.Add(
                $"command {command.ClientIntentId} replayed {replayedEvents.Count} event(s) but recovered event stream has {recoveredEvents.Count}");
        }

        var comparableCount = Math.Min(replayedEvents.Count, recoveredEvents.Count);
        for (var index = 0; index < comparableCount; index++)
        {
            if (!string.Equals(replayedEvents[index].Kind, recoveredEvents[index].Event.Kind, StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} replayed event {index + 1} kind {replayedEvents[index].Kind} but recovered event sequence {recoveredEvents[index].Sequence} kind {recoveredEvents[index].Event.Kind}");
            }

            if (!string.Equals(
                    replayedEvents[index].Description,
                    recoveredEvents[index].Event.Description,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} replayed event {index + 1} description {FormatReplayError(replayedEvents[index].Description)} but recovered event sequence {recoveredEvents[index].Sequence} description {FormatReplayError(recoveredEvents[index].Event.Description)}");
            }

            var replayedPayloadHash = MatchStateHasher.HashValue(replayedEvents[index].Payload);
            var recoveredPayloadHash = MatchStateHasher.HashValue(recoveredEvents[index].Event.Payload);
            if (!string.Equals(replayedPayloadHash, recoveredPayloadHash, StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} replayed event {index + 1} payload hash {replayedPayloadHash} but recovered event sequence {recoveredEvents[index].Sequence} payload hash {recoveredPayloadHash}");
            }
        }
    }

    private static async ValueTask<ResolutionResult> ReplayCommandAsync(
        MatchSession session,
        RecoveredCommand recovered,
        CancellationToken cancellationToken)
    {
        if (recovered.CommandType.StartsWith(DevSeedScenarioPrefix, StringComparison.Ordinal))
        {
            return await session.SeedScenarioAsync(
                    recovered.PlayerId,
                    recovered.ClientIntentId,
                    recovered.CommandType[DevSeedScenarioPrefix.Length..],
                    RawCommandFor(recovered),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var rawCommand = RawCommandFor(recovered);
        var command = GameCommandJsonMapper.Map(rawCommand);
        return command switch
        {
            ReadyCommand => await session.ReadyAsync(
                    recovered.PlayerId,
                    recovered.ClientIntentId,
                    rawCommand,
                    cancellationToken)
                .ConfigureAwait(false),
            SubmitDeckCommand submitDeckCommand => await session.SubmitDeckAsync(
                    recovered.PlayerId,
                    recovered.ClientIntentId,
                    submitDeckCommand,
                    rawCommand,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => await session.SubmitAsync(
                    recovered.PlayerId,
                    recovered.ClientIntentId,
                    command,
                    rawCommand,
                    cancellationToken)
                .ConfigureAwait(false)
        };
    }

    private static JsonElement RawCommandFor(RecoveredCommand recovered)
    {
        if (recovered.RawCommand is { } rawCommand)
        {
            return rawCommand.Clone();
        }

        using var document = JsonDocument.Parse($$"""{"cmdType":"{{recovered.CommandType}}"}""");
        return document.RootElement.Clone();
    }
}

public static class MatchReplayInitialStateBuilder
{
    public static MatchState FromSeats(
        string roomId,
        IReadOnlyDictionary<string, string> seats)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(seats);

        var normalizedSeats = seats
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && !string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => new KeyValuePair<string, string>(entry.Key.Trim(), entry.Value.Trim()))
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Value,
                StringComparer.Ordinal);
        var playerIds = normalizedSeats
            .OrderBy(entry => SeatSort(entry.Value))
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .ToArray();
        var activePlayerId = playerIds.FirstOrDefault()
            ?? "P1";

        return new MatchState(
            roomId.Trim(),
            0,
            1,
            activePlayerId,
            normalizedSeats,
            MatchStatuses.Seating,
            [],
            activePlayerId,
            MatchPhases.Room,
            TimingStates.Room,
            playerIds.ToDictionary(playerId => playerId, _ => RunePool.Empty, StringComparer.Ordinal),
            playerIds.ToDictionary(playerId => playerId, _ => PlayerZones.Empty, StringComparer.Ordinal),
            playerIds.ToDictionary(playerId => playerId, _ => 0, StringComparer.Ordinal),
            new Dictionary<string, CardObjectState>(StringComparer.Ordinal),
            null,
            [],
            [],
            null,
            [],
            null,
            [],
            0,
            0,
            [],
            null,
            playerIds.ToDictionary(playerId => playerId, _ => 0, StringComparer.Ordinal),
            playerIds.ToDictionary(playerId => playerId, _ => 0, StringComparer.Ordinal),
            [],
            new Dictionary<string, OfficialDecklist>(StringComparer.Ordinal),
            [],
            null,
            new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal),
            []);
    }

    private static int SeatSort(string seat)
    {
        return seat switch
        {
            "P1" => 0,
            "P2" => 1,
            _ => 10
        };
    }
}

public static class MatchStateHasher
{
    public static string Hash(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return HashValue(state);
    }

    public static string HashValue(object? value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonicalValue(writer, JsonSerializer.SerializeToElement(value));
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonicalValue(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalValue(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON value kind {element.ValueKind}.");
        }
    }
}

public interface IMatchRecoveryStore
{
    ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken);
}

public sealed class NoopMatchRecoveryStore : IMatchRecoveryStore
{
    public static NoopMatchRecoveryStore Instance { get; } = new();

    private NoopMatchRecoveryStore()
    {
    }

    public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<MatchRecoveryFrame?>(null);
    }
}

public static class MatchRecoveryValidator
{
    private const string DevSeedScenarioPrefix = "DEV_SEED_SCENARIO:";
    private const string DevSeedScenarioCommandType = "DEV_SEED_SCENARIO";
    private const int BaseWinningScore = 8;
    private const string BattlefieldIncreaseWinningScoreCardNo = "OGN·276/298";
    private const string BattlefieldIncreaseWinningScoreAltCardNo = "OGN·276a/298";

    private sealed record BattleRequiredAssignmentView(
        string SourceObjectId,
        int Damage,
        IReadOnlyList<string> LegalTargetObjectIds);

    public static IReadOnlyList<string> Validate(
        string roomId,
        long lastEventSequence,
        IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events,
        IReadOnlyDictionary<string, RecoveredPlayerView> playerViews,
        MatchState? authoritativeState = null,
        long? currentTick = null,
        MatchReplayFrame? spectatorReplayFrame = null)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(roomId))
        {
            errors.Add("room id is required");
        }

        if (lastEventSequence < 0)
        {
            errors.Add("last event sequence cannot be negative");
        }

        if (currentTick is < 0)
        {
            errors.Add("current tick cannot be negative");
        }

        IEnumerable<string> commandPlayerIds = playerViews.Keys;
        var commandPlayerScopeLabel = "recovered player views";
        if (playerViews.Count == 0 && authoritativeState is { } state)
        {
            commandPlayerIds = state.Seats.Keys;
            commandPlayerScopeLabel = "authoritative state seats";
        }

        ValidateEvents(currentTick, lastEventSequence, events, errors);
        ValidateCommands(
            currentTick,
            lastEventSequence,
            commands,
            events,
            commandPlayerIds,
            commandPlayerScopeLabel,
            errors);
        ValidatePlayerViews(currentTick, lastEventSequence, playerViews, errors);
        ValidatePlayerViewAgreement(playerViews, errors);
        ValidateAuthoritativeState(roomId, currentTick, authoritativeState, playerViews, errors);
        ValidateSpectatorReplayFrame(
            roomId,
            lastEventSequence,
            currentTick,
            authoritativeState,
            spectatorReplayFrame,
            errors);

        return errors;
    }

    private static void ValidateEvents(
        long? currentTick,
        long lastEventSequence,
        IReadOnlyList<RecoveredEvent> events,
        List<string> errors)
    {
        var orderedEvents = events.OrderBy(gameEvent => gameEvent.Sequence).ToArray();
        if (orderedEvents.Length == 0)
        {
            if (lastEventSequence != 0)
            {
                errors.Add($"event stream is empty but match last event sequence is {lastEventSequence}");
            }

            return;
        }

        if (orderedEvents[^1].Sequence != lastEventSequence)
        {
            errors.Add(
                $"event stream ends at {orderedEvents[^1].Sequence} but match last event sequence is {lastEventSequence}");
        }

        var previousFrameSequence = 0L;
        var previousFrameTick = 0L;
        foreach (var gameEvent in events)
        {
            if (gameEvent.Sequence <= 0)
            {
                errors.Add($"event sequence value {gameEvent.Sequence} must be positive");
            }

            if (string.IsNullOrWhiteSpace(gameEvent.Event.Kind))
            {
                errors.Add($"event sequence {gameEvent.Sequence} kind is required");
            }

            if (gameEvent.Event.Payload is null)
            {
                errors.Add($"event sequence {gameEvent.Sequence} payload is required");
            }

            if (gameEvent.Sequence <= previousFrameSequence)
            {
                errors.Add(
                    $"event stream is not ordered by sequence: {gameEvent.Sequence} after {previousFrameSequence}");
            }

            if (gameEvent.Tick < 0)
            {
                errors.Add($"event sequence {gameEvent.Sequence} has negative tick {gameEvent.Tick}");
            }

            if (gameEvent.Order < 0)
            {
                errors.Add($"event sequence {gameEvent.Sequence} has negative order {gameEvent.Order}");
            }

            if (currentTick is { } recoveryTick && gameEvent.Tick > recoveryTick)
            {
                errors.Add(
                    $"event sequence {gameEvent.Sequence} has tick {gameEvent.Tick} after recovery tick {recoveryTick}");
            }

            if (gameEvent.Tick < previousFrameTick)
            {
                errors.Add(
                    $"event sequence {gameEvent.Sequence} tick {gameEvent.Tick} is before previous event tick {previousFrameTick}");
            }

            previousFrameSequence = gameEvent.Sequence;
            previousFrameTick = gameEvent.Tick;
        }

        var previous = 0L;
        var seen = new HashSet<long>();
        foreach (var gameEvent in orderedEvents)
        {
            if (!seen.Add(gameEvent.Sequence))
            {
                errors.Add($"duplicate event sequence {gameEvent.Sequence}");
            }

            if (gameEvent.Sequence != previous + 1)
            {
                errors.Add($"event sequence gap before {gameEvent.Sequence}; expected {previous + 1}");
            }

            previous = gameEvent.Sequence;
        }
    }

    private static void ValidateCommands(
        long? currentTick,
        long lastEventSequence,
        IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events,
        IEnumerable<string> recoveredPlayerIds,
        string recoveredPlayerScopeLabel,
        List<string> errors)
    {
        var acceptedEventOwners = new Dictionary<long, string>();
        var seenCommandIntents = new HashSet<(string PlayerId, string ClientIntentId)>();
        var knownRecoveredPlayerIds = recoveredPlayerIds.ToHashSet(StringComparer.Ordinal);
        var previousFrameStartedEventSequence = 0L;
        var previousFrameCompletedEventSequence = 0L;
        var previousFrameCompletedTick = 0L;
        foreach (var command in commands)
        {
            if (command.StartedEventSequence < previousFrameStartedEventSequence
                || (command.StartedEventSequence == previousFrameStartedEventSequence
                    && command.CompletedEventSequence < previousFrameCompletedEventSequence))
            {
                errors.Add(
                    $"command stream is not ordered by event span: {command.ClientIntentId} {command.StartedEventSequence}->{command.CompletedEventSequence} after {previousFrameStartedEventSequence}->{previousFrameCompletedEventSequence}");
            }

            if (command.StartedEventSequence != previousFrameCompletedEventSequence)
            {
                errors.Add(
                    $"command {command.ClientIntentId} starts at event sequence {command.StartedEventSequence} but previous command completed at {previousFrameCompletedEventSequence}; command event spans must be contiguous");
            }

            previousFrameStartedEventSequence = command.StartedEventSequence;
            previousFrameCompletedEventSequence = command.CompletedEventSequence;

            var normalizedPlayerId = ValidateCommandRequiredNormalizedString(
                command.PlayerId,
                "player id",
                errors);
            var normalizedClientIntentId = ValidateCommandRequiredNormalizedString(
                command.ClientIntentId,
                "client intent id",
                errors);
            var normalizedCommandType = ValidateCommandRequiredNormalizedString(
                command.CommandType,
                "type",
                errors);

            if (normalizedPlayerId.Length > 0
                && knownRecoveredPlayerIds.Count > 0
                && !knownRecoveredPlayerIds.Contains(normalizedPlayerId))
            {
                errors.Add(
                    $"command {normalizedClientIntentId} player {normalizedPlayerId} is missing from {recoveredPlayerScopeLabel}");
            }

            if (!seenCommandIntents.Add((normalizedPlayerId, normalizedClientIntentId)))
            {
                errors.Add(
                    $"command {normalizedClientIntentId} for player {normalizedPlayerId} appears more than once in recovery frame");
            }

            if (command.StartedEventSequence < 0)
            {
                errors.Add(
                    $"command {command.ClientIntentId} has negative started event sequence {command.StartedEventSequence}");
            }

            if (command.CompletedEventSequence < 0)
            {
                errors.Add(
                    $"command {command.ClientIntentId} has negative completed event sequence {command.CompletedEventSequence}");
            }

            ValidateRawCommandShape(command, errors);
            if (!string.IsNullOrWhiteSpace(normalizedCommandType)
                && TryReadRawCommandType(command.RawCommand, out var rawCommandType)
                && !string.Equals(rawCommandType, ExpectedRawCommandType(normalizedCommandType), StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} raw cmdType {rawCommandType} does not match recovered command type {command.CommandType}");
            }

            if (command.StartedTick < 0)
            {
                errors.Add(
                    $"command {command.ClientIntentId} has negative started tick {command.StartedTick}");
            }

            if (command.CompletedTick < 0)
            {
                errors.Add(
                    $"command {command.ClientIntentId} has negative completed tick {command.CompletedTick}");
            }

            if (command.CompletedTick < command.StartedTick)
            {
                errors.Add(
                    $"command {command.ClientIntentId} completes before tick start: {command.StartedTick}->{command.CompletedTick}");
            }

            if (command.StartedTick < previousFrameCompletedTick)
            {
                errors.Add(
                    $"command {command.ClientIntentId} starts at tick {command.StartedTick} before previous command completed tick {previousFrameCompletedTick}");
            }

            if (command.StartedTick != previousFrameCompletedTick)
            {
                errors.Add(
                    $"command {command.ClientIntentId} starts at tick {command.StartedTick} but previous command completed at tick {previousFrameCompletedTick}; command ticks must be contiguous");
            }

            previousFrameCompletedTick = Math.Max(previousFrameCompletedTick, command.CompletedTick);

            if (currentTick is { } recoveryTick)
            {
                if (command.StartedTick > recoveryTick)
                {
                    errors.Add(
                        $"command {command.ClientIntentId} starts at tick {command.StartedTick} after recovery tick {recoveryTick}");
                }

                if (command.CompletedTick > recoveryTick)
                {
                    errors.Add(
                        $"command {command.ClientIntentId} completes at tick {command.CompletedTick} after recovery tick {recoveryTick}");
                }
            }

            if (command.CompletedEventSequence < command.StartedEventSequence)
            {
                errors.Add(
                    $"command {command.ClientIntentId} completes before it starts: {command.StartedEventSequence}->{command.CompletedEventSequence}");
            }

            if (command.StartedEventSequence > lastEventSequence)
            {
                errors.Add(
                    $"command {command.ClientIntentId} starts at {command.StartedEventSequence} after match sequence {lastEventSequence}");
            }

            if (command.CompletedEventSequence > lastEventSequence)
            {
                errors.Add(
                    $"command {command.ClientIntentId} completes at {command.CompletedEventSequence} after match sequence {lastEventSequence}");
            }

            var expectedEventCount = command.CompletedEventSequence - command.StartedEventSequence;
            var actualEventCount = events.Count(gameEvent =>
                gameEvent.Sequence > command.StartedEventSequence
                && gameEvent.Sequence <= command.CompletedEventSequence);
            if (command.Accepted && command.ErrorMessage is not null)
            {
                errors.Add($"accepted command {command.ClientIntentId} has error message");
            }

            if (!command.Accepted)
            {
                if (string.IsNullOrWhiteSpace(command.ErrorMessage))
                {
                    errors.Add($"rejected command {command.ClientIntentId} is missing error message");
                }
                else if (!string.Equals(command.ErrorMessage, command.ErrorMessage.Trim(), StringComparison.Ordinal))
                {
                    errors.Add($"rejected command {command.ClientIntentId} error message has surrounding whitespace");
                }

                if (expectedEventCount > 0)
                {
                    errors.Add(
                        $"rejected command {command.ClientIntentId} covers {expectedEventCount} event(s); rejected commands must not record events");
                }

                if (command.CompletedTick != command.StartedTick)
                {
                    errors.Add(
                        $"rejected command {command.ClientIntentId} advances tick {command.StartedTick}->{command.CompletedTick}");
                }
            }
            else if (actualEventCount != expectedEventCount)
            {
                errors.Add(
                    $"command {command.ClientIntentId} covers {expectedEventCount} event(s) but {actualEventCount} were loaded");
            }
            else if (expectedEventCount == 0 && command.CompletedTick != command.StartedTick)
            {
                errors.Add(
                    $"accepted command {command.ClientIntentId} advances tick {command.StartedTick}->{command.CompletedTick} without events");
            }

            if (command.Accepted)
            {
                var commandEvents = events
                    .Where(gameEvent =>
                        gameEvent.Sequence > command.StartedEventSequence
                        && gameEvent.Sequence <= command.CompletedEventSequence)
                    .OrderBy(gameEvent => gameEvent.Sequence)
                    .ToArray();
                for (var eventIndex = 0; eventIndex < commandEvents.Length; eventIndex++)
                {
                    var gameEvent = commandEvents[eventIndex];
                    if (gameEvent.Order != eventIndex)
                    {
                        errors.Add(
                            $"event sequence {gameEvent.Sequence} has order {gameEvent.Order} but command {command.ClientIntentId} expects order {eventIndex}");
                    }

                    if (gameEvent.Tick < command.StartedTick || gameEvent.Tick > command.CompletedTick)
                    {
                        errors.Add(
                            $"event sequence {gameEvent.Sequence} has tick {gameEvent.Tick} outside command {command.ClientIntentId} tick span {command.StartedTick}->{command.CompletedTick}");
                    }

                    if (gameEvent.Tick != command.CompletedTick)
                    {
                        errors.Add(
                            $"event sequence {gameEvent.Sequence} has tick {gameEvent.Tick} but command {command.ClientIntentId} completed tick is {command.CompletedTick}");
                    }
                }

                if (commandEvents.Length > 0)
                {
                    var lastCoveredEventTick = commandEvents.Max(gameEvent => gameEvent.Tick);
                    if (lastCoveredEventTick != command.CompletedTick)
                    {
                        errors.Add(
                            $"command {command.ClientIntentId} completes at tick {command.CompletedTick} but covered event tick tail is {lastCoveredEventTick}");
                    }
                }

                foreach (var gameEvent in commandEvents)
                {
                    if (acceptedEventOwners.TryGetValue(gameEvent.Sequence, out var owner))
                    {
                        if (!string.Equals(owner, command.ClientIntentId, StringComparison.Ordinal))
                        {
                            errors.Add(
                                $"event sequence {gameEvent.Sequence} is covered by multiple accepted commands: {owner} and {command.ClientIntentId}");
                        }
                    }
                    else
                    {
                        acceptedEventOwners.Add(gameEvent.Sequence, command.ClientIntentId);
                    }
                }
            }
        }

        if (commands.Count == 0)
        {
            return;
        }

        if (currentTick is { } frameCurrentTick && previousFrameCompletedTick != frameCurrentTick)
        {
            errors.Add(
                $"command tick tail {previousFrameCompletedTick} does not match recovery tick {frameCurrentTick}");
        }

        foreach (var gameEvent in events.OrderBy(gameEvent => gameEvent.Sequence))
        {
            if (!acceptedEventOwners.ContainsKey(gameEvent.Sequence))
            {
                errors.Add(
                    $"event sequence {gameEvent.Sequence} is not covered by an accepted command");
            }
        }
    }

    private static string ValidateCommandRequiredNormalizedString(
        string value,
        string label,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"command {label} is required");
            return string.Empty;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add($"command {label} {normalizedValue} has surrounding whitespace");
        }

        return normalizedValue;
    }

    private static void ValidateRawCommandShape(RecoveredCommand command, List<string> errors)
    {
        if (command.RawCommand is not { } rawCommand)
        {
            return;
        }

        if (rawCommand.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"command {command.ClientIntentId} raw command must be a JSON object");
            return;
        }

        if (!rawCommand.TryGetProperty("cmdType", out var rawCommandType))
        {
            errors.Add($"command {command.ClientIntentId} raw command is missing cmdType");
            return;
        }

        if (rawCommandType.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(rawCommandType.GetString()))
        {
            errors.Add($"command {command.ClientIntentId} raw cmdType is required");
            return;
        }

        var rawCommandTypeValue = rawCommandType.GetString()!;
        var normalizedRawCommandType = rawCommandTypeValue.Trim();
        if (!string.Equals(rawCommandTypeValue, normalizedRawCommandType, StringComparison.Ordinal))
        {
            errors.Add(
                $"command {command.ClientIntentId} raw cmdType {normalizedRawCommandType} has surrounding whitespace");
        }
    }

    private static bool TryReadRawCommandType(JsonElement? rawCommand, out string commandType)
    {
        commandType = string.Empty;
        if (rawCommand is not { ValueKind: JsonValueKind.Object } raw
            || !raw.TryGetProperty("cmdType", out var rawCommandType)
            || rawCommandType.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var value = rawCommandType.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        commandType = value.Trim();
        return true;
    }

    private static string ExpectedRawCommandType(string recoveredCommandType)
    {
        return recoveredCommandType.StartsWith(DevSeedScenarioPrefix, StringComparison.Ordinal)
            ? DevSeedScenarioCommandType
            : recoveredCommandType;
    }

    private static void ValidatePlayerViews(
        long? currentTick,
        long lastEventSequence,
        IReadOnlyDictionary<string, RecoveredPlayerView> playerViews,
        List<string> errors)
    {
        foreach (var (playerId, view) in playerViews)
        {
            if (!string.Equals(playerId, view.PlayerId, StringComparison.Ordinal))
            {
                errors.Add($"player view key {playerId} does not match payload player {view.PlayerId}");
            }

            if (view.Snapshot is null)
            {
                errors.Add($"snapshot for {view.PlayerId} is required");
                continue;
            }

            ValidateSnapshotShape(view, errors);

            if (view.Snapshot.Players is null)
            {
                errors.Add($"snapshot for {view.PlayerId} players are required");
            }
            else
            {
                ValidateSnapshotPlayerPayloads(view, errors);
                ValidateSnapshotPlayerCoverage(view, playerViews.Keys, errors);
                ValidateSnapshotActivePlayer(view, errors);
                ValidateSnapshotTimingPlayerMembership(view, "turnPlayerId", "turn player", errors);
                ValidateSnapshotTimingPlayerMembership(
                    view,
                    "priorityPlayerId",
                    "priority player",
                    errors,
                    optional: true);
                ValidateSnapshotTimingPlayerMembership(
                    view,
                    "focusPlayerId",
                    "focus player",
                    errors,
                    optional: true);
                ValidateSnapshotTimingPlayerMembership(
                    view,
                    "winnerPlayerId",
                    "winner player",
                    errors,
                    optional: true);
                ValidateSnapshotTimingPlayerListMembership(view, "readyPlayerIds", "ready player", errors);
                ValidateSnapshotTimingPlayerListMembership(
                    view,
                    "passedPriorityPlayerIds",
                    "passed priority player",
                    errors);
                ValidateSnapshotTimingPlayerListMembership(
                    view,
                    "passedFocusPlayerIds",
                    "passed focus player",
                    errors);
                ValidateSnapshotTimingPlayerListMembership(
                    view,
                    "destroyedUnitOwnerIdsThisTurn",
                    "destroyed unit owner",
                    errors);
            }

            if (view.Snapshot.Tick != view.SnapshotTick)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} has payload tick {view.Snapshot.Tick} but row tick {view.SnapshotTick}");
            }

            if (view.Snapshot.TurnNumber < 1)
            {
                errors.Add($"snapshot for {view.PlayerId} has invalid turn number {view.Snapshot.TurnNumber}");
            }

            if (view.SnapshotTick < 0)
            {
                errors.Add($"snapshot for {view.PlayerId} has negative row tick {view.SnapshotTick}");
            }

            if (currentTick is { } recoveryTick && view.SnapshotTick > recoveryTick)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} has row tick {view.SnapshotTick} after recovery tick {recoveryTick}");
            }

            if (currentTick is { } snapshotRecoveryTick && view.SnapshotTick != snapshotRecoveryTick)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} has row tick {view.SnapshotTick} but recovery tick {snapshotRecoveryTick}");
            }

            if (view.SnapshotEventSequence < 0)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} has negative event sequence {view.SnapshotEventSequence}");
            }

            if (view.SnapshotEventSequence > lastEventSequence)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} points to future event sequence {view.SnapshotEventSequence}");
            }

            if (view.SnapshotEventSequence != lastEventSequence)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} has event sequence {view.SnapshotEventSequence} but recovery event sequence {lastEventSequence}");
            }

            if (view.Prompt is null
                && (view.PromptTick is not null || view.PromptEventSequence is not null))
            {
                errors.Add(
                    $"prompt metadata for {view.PlayerId} has tick/event sequence without prompt payload");
            }

            if (view.Prompt is not null
                && (view.PromptTick is null || view.PromptEventSequence is null))
            {
                errors.Add(
                    $"prompt for {view.PlayerId} is missing row tick/event sequence metadata");
            }

            if (view.Prompt is not null && view.PromptEventSequence > lastEventSequence)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} points to future event sequence {view.PromptEventSequence}");
            }

            if (view.PromptTick is { } negativePromptRowTick && negativePromptRowTick < 0)
            {
                errors.Add($"prompt for {view.PlayerId} has negative row tick {negativePromptRowTick}");
            }

            if (view.PromptEventSequence is { } negativePromptEventSequence && negativePromptEventSequence < 0)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} has negative event sequence {negativePromptEventSequence}");
            }

            if (currentTick is { } currentRecoveryTick
                && view.PromptTick is { } futurePromptRowTick
                && futurePromptRowTick > currentRecoveryTick)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} has row tick {futurePromptRowTick} after recovery tick {currentRecoveryTick}");
            }

            if (view.Prompt is not null
                && view.Prompt.SnapshotTick is { } promptSnapshotTick
                && view.PromptTick is { } promptRowTick
                && promptSnapshotTick != promptRowTick)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} has payload snapshot tick {promptSnapshotTick} but row tick {promptRowTick}");
            }

            if (view.Prompt is not null
                && view.PromptTick is { } promptTick
                && promptTick != view.SnapshotTick)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} has row tick {promptTick} but snapshot row tick {view.SnapshotTick}");
            }

            if (view.Prompt is not null
                && view.PromptEventSequence is { } promptEventSequence
                && promptEventSequence != view.SnapshotEventSequence)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} has event sequence {promptEventSequence} but snapshot event sequence {view.SnapshotEventSequence}");
            }

            if (view.Prompt is not null
                && !string.Equals(view.PlayerId, view.Prompt.PlayerId, StringComparison.Ordinal))
            {
                errors.Add($"prompt for {view.PlayerId} has payload player {view.Prompt.PlayerId}");
            }
        }
    }

    private static void ValidateSnapshotShape(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (view.Snapshot.Lanes is null)
        {
            errors.Add($"snapshot for {view.PlayerId} lanes are required");
        }

        if (view.Snapshot.Stack is null)
        {
            errors.Add($"snapshot for {view.PlayerId} stack is required");
        }

        if (view.Snapshot.Timing is null)
        {
            errors.Add($"snapshot for {view.PlayerId} timing is required");
        }

        if (string.IsNullOrWhiteSpace(view.Snapshot.TurnState))
        {
            errors.Add($"snapshot for {view.PlayerId} turn state is required");
        }
        else if (!IsKnownTimingState(view.Snapshot.TurnState))
        {
            errors.Add($"snapshot for {view.PlayerId} turn state {view.Snapshot.TurnState} is invalid");
        }

        if (view.Snapshot.Timing is not null)
        {
            if (!TryReadString(view.Snapshot.Timing, "timingState", out var snapshotTimingState)
                || string.IsNullOrWhiteSpace(snapshotTimingState))
            {
                errors.Add($"snapshot for {view.PlayerId} timing state is required");
            }
            else
            {
                if (!IsKnownTimingState(snapshotTimingState))
                {
                    errors.Add($"snapshot for {view.PlayerId} timing state {snapshotTimingState} is invalid");
                }

                if (!string.IsNullOrWhiteSpace(view.Snapshot.TurnState)
                    && !string.Equals(view.Snapshot.TurnState, snapshotTimingState, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"snapshot for {view.PlayerId} turn state {view.Snapshot.TurnState} does not match timing state {snapshotTimingState}");
                }
            }

            ValidateSnapshotTimingAllowedString(view, "phase", "phase", IsKnownMatchPhase, errors);
            ValidateSnapshotTimingRequiredString(view, "turnPlayerId", "turn player", errors);
            ValidateSnapshotTimingAllowedString(view, "roomStatus", "room status", IsKnownMatchStatus, errors);
        }
    }

    private static void ValidateSnapshotTimingRequiredString(
        RecoveredPlayerView view,
        string key,
        string label,
        List<string> errors)
    {
        if (!TryReadString(view.Snapshot.Timing, key, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"snapshot for {view.PlayerId} timing {label} is required");
        }
    }

    private static void ValidateSnapshotTimingAllowedString(
        RecoveredPlayerView view,
        string key,
        string label,
        Func<string, bool> isKnownValue,
        List<string> errors)
    {
        if (!TryReadString(view.Snapshot.Timing, key, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"snapshot for {view.PlayerId} timing {label} is required");
            return;
        }

        if (!isKnownValue(value))
        {
            errors.Add($"snapshot for {view.PlayerId} timing {label} {value} is invalid");
        }
    }

    private static bool IsKnownMatchPhase(string value)
    {
        return string.Equals(value, MatchPhases.Room, StringComparison.Ordinal)
            || string.Equals(value, MatchPhases.Mulligan, StringComparison.Ordinal)
            || string.Equals(value, MatchPhases.TurnStart, StringComparison.Ordinal)
            || string.Equals(value, MatchPhases.Main, StringComparison.Ordinal)
            || string.Equals(value, MatchPhases.TurnEnd, StringComparison.Ordinal);
    }

    private static bool IsKnownTimingState(string value)
    {
        return string.Equals(value, TimingStates.Room, StringComparison.Ordinal)
            || string.Equals(value, TimingStates.Mulligan, StringComparison.Ordinal)
            || string.Equals(value, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || string.Equals(value, TimingStates.NeutralClosed, StringComparison.Ordinal)
            || string.Equals(value, TimingStates.SpellDuelOpen, StringComparison.Ordinal)
            || string.Equals(value, TimingStates.SpellDuelClosed, StringComparison.Ordinal);
    }

    private static bool IsKnownMatchStatus(string value)
    {
        return string.Equals(value, MatchStatuses.Empty, StringComparison.Ordinal)
            || string.Equals(value, MatchStatuses.Seating, StringComparison.Ordinal)
            || string.Equals(value, MatchStatuses.InProgress, StringComparison.Ordinal)
            || string.Equals(value, MatchStatuses.Finished, StringComparison.Ordinal);
    }

    private static bool IsKnownSeat(string value)
    {
        return string.Equals(value, "P1", StringComparison.Ordinal)
            || string.Equals(value, "P2", StringComparison.Ordinal);
    }

    private static void ValidateSnapshotTimingPlayerMembership(
        RecoveredPlayerView view,
        string key,
        string label,
        List<string> errors,
        bool optional = false)
    {
        if (view.Snapshot.Timing is null
            || !view.Snapshot.Timing.TryGetValue(key, out var rawPlayerId)
            || rawPlayerId is null
            || rawPlayerId is JsonElement { ValueKind: JsonValueKind.Null })
        {
            return;
        }

        if (!TryReadString(view.Snapshot.Timing, key, out var playerId))
        {
            if (optional)
            {
                errors.Add($"snapshot for {view.PlayerId} timing {label} id is invalid");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(playerId))
        {
            if (optional)
            {
                errors.Add($"snapshot for {view.PlayerId} timing {label} id is required");
            }

            return;
        }

        var normalizedPlayerId = playerId.Trim();
        if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
        {
            errors.Add(
                $"snapshot for {view.PlayerId} timing {label} {normalizedPlayerId} has surrounding whitespace");
        }

        if (!view.Snapshot.Players.ContainsKey(normalizedPlayerId))
        {
            errors.Add(
                $"snapshot for {view.PlayerId} timing {label} {normalizedPlayerId} is missing from players");
        }
    }

    private static void ValidateSnapshotTimingPlayerListMembership(
        RecoveredPlayerView view,
        string key,
        string label,
        List<string> errors)
    {
        if (view.Snapshot.Timing is null
            || !view.Snapshot.Timing.TryGetValue(key, out var rawPlayerIds)
            || rawPlayerIds is null
            || rawPlayerIds is JsonElement { ValueKind: JsonValueKind.Null })
        {
            return;
        }

        if (!TryReadStringList(view.Snapshot.Timing, key, out var playerIds))
        {
            errors.Add($"snapshot for {view.PlayerId} timing {label} list is invalid");
            return;
        }

        var seenPlayerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var playerId in playerIds)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                errors.Add($"snapshot for {view.PlayerId} timing {label} id is required");
                continue;
            }

            var normalizedPlayerId = playerId.Trim();
            if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} timing {label} {normalizedPlayerId} has surrounding whitespace");
            }

            if (!seenPlayerIds.Add(normalizedPlayerId))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} timing {label} {normalizedPlayerId} is duplicated");
                continue;
            }

            if (!view.Snapshot.Players.ContainsKey(normalizedPlayerId))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} timing {label} {normalizedPlayerId} is missing from players");
            }
        }
    }

    private static void ValidateSnapshotPlayerPayloads(
        RecoveredPlayerView view,
        List<string> errors)
    {
        var seenSeats = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (snapshotPlayerId, playerPayload) in view.Snapshot.Players)
        {
            if (!IsSnapshotPlayerPayloadObject(playerPayload))
            {
                errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} payload is required");
                continue;
            }

            if (!TryReadObjectString(playerPayload, "id", out var payloadId)
                || string.IsNullOrWhiteSpace(payloadId))
            {
                errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} id is required");
            }
            else if (!string.Equals(payloadId, snapshotPlayerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} player {snapshotPlayerId} payload id {payloadId} does not match player key");
            }

            if (!TryReadObjectString(playerPayload, "seat", out var seat)
                || string.IsNullOrWhiteSpace(seat))
            {
                errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} seat is required");
                continue;
            }

            var normalizedSeat = seat.Trim();
            if (!string.Equals(seat, normalizedSeat, StringComparison.Ordinal))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} player {snapshotPlayerId} seat {normalizedSeat} has surrounding whitespace");
            }

            if (!IsKnownSeat(normalizedSeat))
            {
                errors.Add($"snapshot for {view.PlayerId} player {snapshotPlayerId} seat {normalizedSeat} is invalid");
                continue;
            }

            if (!seenSeats.Add(normalizedSeat))
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} player {snapshotPlayerId} seat {normalizedSeat} is duplicated");
            }
        }
    }

    private static bool IsSnapshotPlayerPayloadObject(object? playerPayload)
    {
        return playerPayload switch
        {
            IReadOnlyDictionary<string, object?> => true,
            IDictionary<string, object?> => true,
            JsonElement { ValueKind: JsonValueKind.Object } => true,
            _ => false
        };
    }

    private static bool IsNullSnapshotPayloadValue(object? payload)
    {
        return payload is null
            || payload is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };
    }

    private static void ValidateSpectatorSnapshotPlayerPayloads(
        SnapshotDto snapshot,
        MatchState authoritativeState,
        List<string> errors)
    {
        foreach (var expectedPlayerId in authoritativeState.Seats.Keys)
        {
            if (!snapshot.Players.ContainsKey(expectedPlayerId))
            {
                errors.Add($"spectator replay frame snapshot is missing player {expectedPlayerId}");
            }
        }

        foreach (var (playerId, playerPayload) in snapshot.Players)
        {
            if (!authoritativeState.Seats.ContainsKey(playerId))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} is not in authoritative seats");
            }

            if (!IsSnapshotPlayerPayloadObject(playerPayload))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} payload is required");
                continue;
            }

            if (!TryReadObjectString(playerPayload, "id", out var payloadId)
                || !string.Equals(payloadId, playerId, StringComparison.Ordinal))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} payload id does not match player key");
            }

            if (!TryReadObjectString(playerPayload, "name", out var name)
                || !string.Equals(name, playerId, StringComparison.Ordinal))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} name does not match player id");
            }

            var expectedReady = authoritativeState.ReadyPlayerIds.Contains(playerId, StringComparer.Ordinal);
            if (!TryReadObjectBool(playerPayload, "ready", out var ready)
                || ready != expectedReady)
            {
                errors.Add($"spectator replay frame snapshot player {playerId} ready does not match authoritative state ready");
            }

            var zones = authoritativeState.PlayerZones.TryGetValue(playerId, out var playerZones)
                ? playerZones
                : PlayerZones.Empty;
            if (!TryReadObjectInt(playerPayload, "handSize", out var handSize)
                || handSize != zones.Hand.Count)
            {
                errors.Add($"spectator replay frame snapshot player {playerId} hand size does not match authoritative state hand size");
            }

            var expectedScore = authoritativeState.PlayerScores.TryGetValue(playerId, out var score)
                ? score
                : 0;
            if (!TryReadObjectInt(playerPayload, "score", out var payloadScore)
                || payloadScore != expectedScore)
            {
                errors.Add($"spectator replay frame snapshot player {playerId} score does not match authoritative state score");
            }

            var expectedExperience = authoritativeState.PlayerExperience.TryGetValue(playerId, out var experience)
                ? experience
                : 0;
            if (!TryReadObjectInt(playerPayload, "experience", out var payloadExperience)
                || payloadExperience != expectedExperience)
            {
                errors.Add($"spectator replay frame snapshot player {playerId} experience does not match authoritative state experience");
            }

            var expectedCardsPlayedThisTurn = authoritativeState.PlayerCardsPlayedThisTurn.TryGetValue(
                playerId,
                out var cardsPlayedThisTurn)
                ? cardsPlayedThisTurn
                : 0;
            if (!TryReadObjectInt(playerPayload, "cardsPlayedThisTurn", out var payloadCardsPlayedThisTurn)
                || payloadCardsPlayedThisTurn != expectedCardsPlayedThisTurn)
            {
                errors.Add($"spectator replay frame snapshot player {playerId} cards played this turn does not match authoritative state cards played this turn");
            }

            var expectedDeckSubmitted = authoritativeState.PlayerDecklists.ContainsKey(playerId);
            if (!TryReadObjectBool(playerPayload, "deckSubmitted", out var deckSubmitted)
                || deckSubmitted != expectedDeckSubmitted)
            {
                errors.Add($"spectator replay frame snapshot player {playerId} deck submitted does not match authoritative state deck submitted");
            }

            var expectedMulliganCompleted = authoritativeState.MulliganCompletedPlayerIds.Contains(
                playerId,
                StringComparer.Ordinal);
            if (!TryReadObjectBool(playerPayload, "mulliganCompleted", out var mulliganCompleted)
                || mulliganCompleted != expectedMulliganCompleted)
            {
                errors.Add($"spectator replay frame snapshot player {playerId} mulligan completed does not match authoritative state mulligan completed");
            }

            ValidateSpectatorSnapshotPlayerRunePoolPayload(playerId, playerPayload, authoritativeState, errors);
            ValidateSpectatorSnapshotPlayerZonePayloads(playerId, playerPayload, zones, authoritativeState, errors);
            ValidateSpectatorSnapshotPlayerObjectPayloads(playerId, playerPayload, authoritativeState, errors);
        }
    }

    private static void ValidateSpectatorSnapshotPlayerRunePoolPayload(
        string playerId,
        object? playerPayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "runePool", out var runePoolPayload)
            || !IsSnapshotPlayerPayloadObject(runePoolPayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} rune pool is required");
            return;
        }

        var expectedRunePool = authoritativeState.RunePools.TryGetValue(playerId, out var runePool)
            ? runePool
            : RunePool.Empty;
        if (!TryReadObjectInt(runePoolPayload, "mana", out var mana)
            || mana != expectedRunePool.Mana)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} rune pool mana does not match authoritative rune pool mana");
        }

        if (!TryReadObjectInt(runePoolPayload, "power", out var power)
            || power != expectedRunePool.TotalPower)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} rune pool total power does not match authoritative rune pool total power");
        }

        if (!TryReadObjectInt(runePoolPayload, "untypedPower", out var untypedPower)
            || untypedPower != expectedRunePool.Power)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} rune pool untyped power does not match authoritative rune pool untyped power");
        }

        if (!TryReadObjectIntDictionary(runePoolPayload, "powerByTrait", out var powerByTrait)
            || !IntDictionariesEqual(powerByTrait, expectedRunePool.PowerByTrait))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} rune pool power by trait does not match authoritative rune pool power by trait");
        }
    }

    private static void ValidateSpectatorSnapshotPlayerZonePayloads(
        string playerId,
        object? playerPayload,
        PlayerZones zones,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!TryReadObjectValue(playerPayload, "zones", out var zonePayload)
            || !IsSnapshotPlayerPayloadObject(zonePayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} zones are required");
            return;
        }

        if (!TryReadObjectInt(zonePayload, "mainDeckCount", out var mainDeckCount)
            || mainDeckCount != zones.MainDeck.Count)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} main deck count does not match authoritative state main deck count");
        }

        if (!TryReadObjectInt(zonePayload, "runeDeckCount", out var runeDeckCount)
            || runeDeckCount != zones.RuneDeck.Count)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} rune deck count does not match authoritative state rune deck count");
        }

        if (!TryReadObjectStringList(zonePayload, "hand", out var handObjects)
            || handObjects.Count != 0)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} hand objects must be redacted for spectator view");
        }

        if (!TryReadObjectInt(zonePayload, "handHidden", out var handHidden)
            || handHidden != zones.Hand.Count)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} hidden hand count does not match authoritative state hand count");
        }

        ValidateSpectatorSnapshotPlayerZoneStringList(
            playerId,
            zonePayload,
            "base",
            zones.Base,
            "base objects",
            errors);
        ValidateSpectatorSnapshotPlayerZoneStringList(
            playerId,
            zonePayload,
            "battlefields",
            zones.Battlefields
                .Where(objectId => !IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId))
                .ToArray(),
            "battlefield objects",
            errors);

        var expectedHiddenStandbyCount = zones.Battlefields.Count(objectId =>
            IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId));
        if (!TryReadObjectInt(zonePayload, "battlefieldHiddenStandbyCount", out var hiddenStandbyCount)
            || hiddenStandbyCount != expectedHiddenStandbyCount)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} hidden battlefield standby count does not match authoritative state hidden battlefield standby count");
        }

        ValidateSpectatorSnapshotPlayerZoneStringList(
            playerId,
            zonePayload,
            "graveyard",
            zones.Graveyard,
            "graveyard objects",
            errors);
        ValidateSpectatorSnapshotPlayerZoneStringList(
            playerId,
            zonePayload,
            "banished",
            zones.Banished,
            "banished objects",
            errors);
        ValidateSpectatorSnapshotPlayerZoneStringList(
            playerId,
            zonePayload,
            "legendZone",
            zones.LegendZone,
            "legend zone objects",
            errors);
        ValidateSpectatorSnapshotPlayerZoneStringList(
            playerId,
            zonePayload,
            "championZone",
            zones.ChampionZone,
            "champion zone objects",
            errors);
    }

    private static void ValidateSpectatorSnapshotPlayerZoneStringList(
        string playerId,
        object? zonePayload,
        string key,
        IReadOnlyList<string> expected,
        string description,
        List<string> errors)
    {
        if (!TryReadObjectStringList(zonePayload, key, out var value)
            || !StringListsEqual(value, expected))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} {description} do not match authoritative state {description}");
        }
    }

    private static void ValidateSpectatorSnapshotPlayerObjectPayloads(
        string playerId,
        object? playerPayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!TryReadObjectDictionary(playerPayload, "objects", out var objectPayloads))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} objects are required");
            return;
        }

        var expectedObjectIds = ExpectedSpectatorPlayerObjectIds(authoritativeState, playerId);
        var expectedObjectIdSet = expectedObjectIds.ToHashSet(StringComparer.Ordinal);
        foreach (var expectedObjectId in expectedObjectIds)
        {
            if (!objectPayloads.ContainsKey(expectedObjectId))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} is missing visible object {expectedObjectId}");
            }
        }

        foreach (var objectId in objectPayloads.Keys)
        {
            if (!expectedObjectIdSet.Contains(objectId))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} is not visible in authoritative spectator view");
            }
        }

        foreach (var expectedObjectId in expectedObjectIds)
        {
            if (!objectPayloads.TryGetValue(expectedObjectId, out var objectPayload))
            {
                continue;
            }

            ValidateSpectatorSnapshotPlayerObjectPayload(
                playerId,
                expectedObjectId,
                objectPayload,
                authoritativeState,
                errors);
        }
    }

    private static IReadOnlyList<string> ExpectedSpectatorPlayerObjectIds(
        MatchState authoritativeState,
        string playerId)
    {
        var zones = authoritativeState.PlayerZones.TryGetValue(playerId, out var playerZones)
            ? playerZones
            : PlayerZones.Empty;
        return zones.Base
            .Concat(zones.Battlefields.Where(objectId => !IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId)))
            .Concat(zones.Graveyard)
            .Concat(zones.Banished)
            .Concat(zones.LegendZone)
            .Concat(zones.ChampionZone)
            .Where(objectId => !string.IsNullOrWhiteSpace(objectId))
            .Distinct(StringComparer.Ordinal)
            .Where(objectId => authoritativeState.CardObjects.ContainsKey(objectId))
            .ToArray();
    }

    private static void ValidateSpectatorSnapshotPlayerObjectPayload(
        string playerId,
        string objectId,
        object? objectPayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!IsSnapshotPlayerPayloadObject(objectPayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} payload is required");
            return;
        }

        if (!TryReadObjectString(objectPayload, "objectId", out var payloadObjectId)
            || !string.Equals(payloadObjectId, objectId, StringComparison.Ordinal))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} object id does not match authoritative object id");
        }

        var hasAuthoritativeObject = authoritativeState.CardObjects.TryGetValue(objectId, out var cardObject);
        var expectedFaceDown = hasAuthoritativeObject && cardObject is not null && cardObject.IsFaceDown;
        if (!TryReadObjectBool(objectPayload, "isFaceDown", out var isFaceDown)
            || isFaceDown != expectedFaceDown)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} face-down flag does not match authoritative spectator redaction");
        }

        if (!hasAuthoritativeObject || cardObject is null)
        {
            return;
        }

        ValidateSpectatorSnapshotPlayerObjectLocation(playerId, objectId, objectPayload, authoritativeState, errors);

        if (!expectedFaceDown)
        {
            ValidateSpectatorSnapshotVisiblePlayerObjectScalars(playerId, objectId, objectPayload, cardObject, errors);
            return;
        }

        if (TryReadObjectValue(objectPayload, "cardNo", out _)
            || TryReadObjectValue(objectPayload, "tags", out _)
            || TryReadObjectValue(objectPayload, "power", out _))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} hidden face-down object {objectId} exposes private metadata");
        }
    }

    private static void ValidateSpectatorSnapshotPlayerObjectLocation(
        string playerId,
        string objectId,
        object? objectPayload,
        MatchState authoritativeState,
        List<string> errors)
    {
        var expectedLocation = ExpectedSpectatorObjectLocation(authoritativeState, objectId);
        if (expectedLocation is null)
        {
            if (TryReadObjectValue(objectPayload, "location", out _))
            {
                errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} location must be absent without authoritative object location");
            }

            return;
        }

        if (!TryReadObjectValue(objectPayload, "location", out var locationPayload)
            || !IsSnapshotPlayerPayloadObject(locationPayload))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} location is required");
            return;
        }

        if (!TryReadObjectString(locationPayload, "playerId", out var locationPlayerId)
            || !string.Equals(locationPlayerId, expectedLocation.PlayerId, StringComparison.Ordinal))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} location player id does not match authoritative object location player id");
        }

        if (!TryReadObjectString(locationPayload, "zone", out var zone)
            || !string.Equals(zone, expectedLocation.Zone, StringComparison.Ordinal))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} location zone does not match authoritative object location zone");
        }

        if (!TryReadObjectOptionalString(locationPayload, "battlefieldObjectId", out var battlefieldObjectId)
            || !string.Equals(
                battlefieldObjectId,
                expectedLocation.BattlefieldObjectId ?? string.Empty,
                StringComparison.Ordinal))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} location battlefield object id does not match authoritative object location battlefield object id");
        }
    }

    private static ObjectLocationState? ExpectedSpectatorObjectLocation(
        MatchState authoritativeState,
        string objectId)
    {
        if (authoritativeState.ObjectLocations.TryGetValue(objectId, out var location))
        {
            return location;
        }

        foreach (var (playerId, zones) in authoritativeState.PlayerZones)
        {
            if (zones.MainDeck.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "MAIN_DECK");
            }

            if (zones.RuneDeck.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "RUNE_DECK");
            }

            if (zones.Hand.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "HAND");
            }

            if (zones.Base.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "BASE");
            }

            if (zones.Battlefields.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "BATTLEFIELD");
            }

            if (zones.Graveyard.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "GRAVEYARD");
            }

            if (zones.Banished.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "BANISHED");
            }

            if (zones.LegendZone.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "LEGEND");
            }

            if (zones.ChampionZone.Contains(objectId, StringComparer.Ordinal))
            {
                return new ObjectLocationState(playerId, "CHAMPION");
            }
        }

        return null;
    }

    private static void ValidateSpectatorSnapshotVisiblePlayerObjectScalars(
        string playerId,
        string objectId,
        object? objectPayload,
        CardObjectState cardObject,
        List<string> errors)
    {
        ValidateSpectatorSnapshotPlayerObjectOptionalStringScalar(
            playerId,
            objectId,
            objectPayload,
            "cardNo",
            cardObject.CardNo,
            "card number",
            errors);
        ValidateSpectatorSnapshotPlayerObjectOptionalStringScalar(
            playerId,
            objectId,
            objectPayload,
            "ownerId",
            cardObject.OwnerId,
            "owner id",
            errors);
        ValidateSpectatorSnapshotPlayerObjectOptionalStringScalar(
            playerId,
            objectId,
            objectPayload,
            "controllerId",
            cardObject.ControllerId,
            "controller id",
            errors);
        ValidateSpectatorSnapshotPlayerObjectOptionalStringScalar(
            playerId,
            objectId,
            objectPayload,
            "attachedToObjectId",
            cardObject.AttachedToObjectId,
            "attached object id",
            errors);

        ValidateSpectatorSnapshotPlayerObjectIntScalar(
            playerId,
            objectId,
            objectPayload,
            "damage",
            cardObject.Damage,
            "damage",
            errors);
        ValidateSpectatorSnapshotPlayerObjectIntScalar(
            playerId,
            objectId,
            objectPayload,
            "power",
            cardObject.Power,
            "power",
            errors);
        ValidateSpectatorSnapshotPlayerObjectIntScalar(
            playerId,
            objectId,
            objectPayload,
            "basePower",
            cardObject.Power - cardObject.UntilEndOfTurnPowerModifier,
            "base power",
            errors);
        ValidateSpectatorSnapshotPlayerObjectIntScalar(
            playerId,
            objectId,
            objectPayload,
            "effectivePower",
            cardObject.Power,
            "effective power",
            errors);
        ValidateSpectatorSnapshotPlayerObjectIntScalar(
            playerId,
            objectId,
            objectPayload,
            "untilEndOfTurnPowerModifier",
            cardObject.UntilEndOfTurnPowerModifier,
            "until-end-of-turn power modifier",
            errors);
        ValidateSpectatorSnapshotPlayerObjectIntScalar(
            playerId,
            objectId,
            objectPayload,
            "manaCost",
            cardObject.ManaCost,
            "mana cost",
            errors);

        ValidateSpectatorSnapshotPlayerObjectBoolScalar(
            playerId,
            objectId,
            objectPayload,
            "isExhausted",
            cardObject.IsExhausted,
            "exhausted state",
            errors);
        ValidateSpectatorSnapshotPlayerObjectBoolScalar(
            playerId,
            objectId,
            objectPayload,
            "isAttacking",
            cardObject.IsAttacking,
            "attacking state",
            errors);
        ValidateSpectatorSnapshotPlayerObjectBoolScalar(
            playerId,
            objectId,
            objectPayload,
            "isDefending",
            cardObject.IsDefending,
            "defending state",
            errors);

        ValidateSpectatorSnapshotPlayerObjectStringListScalar(
            playerId,
            objectId,
            objectPayload,
            "tags",
            cardObject.Tags,
            "tags",
            errors);
        ValidateSpectatorSnapshotPlayerObjectStringListScalar(
            playerId,
            objectId,
            objectPayload,
            "untilEndOfTurnEffects",
            cardObject.UntilEndOfTurnEffects,
            "until-end-of-turn effects",
            errors);
    }

    private static void ValidateSpectatorSnapshotPlayerObjectOptionalStringScalar(
        string playerId,
        string objectId,
        object? objectPayload,
        string key,
        string? expected,
        string description,
        List<string> errors)
    {
        if (!TryReadObjectOptionalString(objectPayload, key, out var value)
            || !string.Equals(value, expected ?? string.Empty, StringComparison.Ordinal))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} {description} does not match authoritative object {description}");
        }
    }

    private static void ValidateSpectatorSnapshotPlayerObjectIntScalar(
        string playerId,
        string objectId,
        object? objectPayload,
        string key,
        int expected,
        string description,
        List<string> errors)
    {
        if (!TryReadObjectInt(objectPayload, key, out var value)
            || value != expected)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} {description} does not match authoritative object {description}");
        }
    }

    private static void ValidateSpectatorSnapshotPlayerObjectBoolScalar(
        string playerId,
        string objectId,
        object? objectPayload,
        string key,
        bool expected,
        string description,
        List<string> errors)
    {
        if (!TryReadObjectBool(objectPayload, key, out var value)
            || value != expected)
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} {description} does not match authoritative object {description}");
        }
    }

    private static void ValidateSpectatorSnapshotPlayerObjectStringListScalar(
        string playerId,
        string objectId,
        object? objectPayload,
        string key,
        IReadOnlyList<string> expected,
        string description,
        List<string> errors)
    {
        if (!TryReadObjectStringList(objectPayload, key, out var value)
            || !StringListsEqual(value, expected))
        {
            errors.Add($"spectator replay frame snapshot player {playerId} object {objectId} {description} do not match authoritative object {description}");
        }
    }

    private static void ValidateSpectatorSnapshotLanePayloads(
        SnapshotDto snapshot,
        MatchState authoritativeState,
        List<string> errors)
    {
        var expectedBattlefieldObjectPairs = ExpectedSpectatorLaneBattlefieldObjectPairs(authoritativeState);
        if (!TryReadObjectInt(snapshot.Lanes, "battlefieldCount", out var battlefieldCount)
            || battlefieldCount != expectedBattlefieldObjectPairs.Count)
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield count {battlefieldCount} does not match authoritative state battlefield object count {expectedBattlefieldObjectPairs.Count}");
        }

        if (!TryReadObjectList(snapshot.Lanes, "battlefieldObjectIds", out var battlefieldObjectItems))
        {
            errors.Add("spectator replay frame snapshot lane battlefield object ids are required");
        }
        else
        {
            var spectatorBattlefieldObjectPairs = new List<(string PlayerId, string ObjectId)>();
            var malformedPair = false;
            foreach (var item in battlefieldObjectItems)
            {
                if (!TryReadObjectString(item, "playerId", out var playerId)
                    || string.IsNullOrWhiteSpace(playerId)
                    || !TryReadObjectString(item, "objectId", out var objectId)
                    || string.IsNullOrWhiteSpace(objectId))
                {
                    malformedPair = true;
                    continue;
                }

                spectatorBattlefieldObjectPairs.Add((playerId, objectId));
            }

            if (malformedPair)
            {
                errors.Add("spectator replay frame snapshot lane battlefield object ids require player id and object id");
            }

            if (!BattlefieldObjectPairsEqual(spectatorBattlefieldObjectPairs, expectedBattlefieldObjectPairs))
            {
                errors.Add("spectator replay frame snapshot lane battlefield object ids disagree with authoritative state battlefield object ids");
            }
        }

        if (!TryReadObjectList(snapshot.Lanes, "battlefields", out var battlefieldItems))
        {
            errors.Add("spectator replay frame snapshot lane battlefields are required");
            return;
        }

        var spectatorBattlefieldObjectIds = ExtractObjectStringValues(battlefieldItems, "battlefieldObjectId");
        var authoritativeBattlefieldObjectIds = authoritativeState.BattlefieldStates.Values
            .Select(battlefield => battlefield.BattlefieldObjectId)
            .ToArray();
        if (!StringListsEqual(spectatorBattlefieldObjectIds, authoritativeBattlefieldObjectIds))
        {
            errors.Add("spectator replay frame snapshot lane battlefields disagree with authoritative state battlefields");
        }

        ValidateSpectatorSnapshotBattlefieldScalarPayloads(battlefieldItems, authoritativeState, errors);
        ValidateSpectatorSnapshotBattlefieldListPayloads(battlefieldItems, authoritativeState, errors);
        ValidateSpectatorSnapshotStandbySlotPayloads(battlefieldItems, authoritativeState, errors);
    }

    private static void ValidateSpectatorSnapshotBattlefieldScalarPayloads(
        IReadOnlyList<object?> spectatorBattlefields,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativeBattlefields = authoritativeState.BattlefieldStates.Values.ToArray();
        var count = Math.Min(spectatorBattlefields.Count, authoritativeBattlefields.Length);
        for (var index = 0; index < count; index++)
        {
            var spectatorBattlefield = spectatorBattlefields[index];
            var authoritativeBattlefield = authoritativeBattlefields[index];
            var battlefieldObjectId = authoritativeBattlefield.BattlefieldObjectId;

            if (!TryReadObjectString(spectatorBattlefield, "zonePlayerId", out var zonePlayerId)
                || !string.Equals(zonePlayerId, authoritativeBattlefield.ZonePlayerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} zone player does not match authoritative state zone player");
            }

            if (!TryReadObjectOptionalString(spectatorBattlefield, "cardNo", out var cardNo)
                || !string.Equals(cardNo, authoritativeBattlefield.CardNo ?? string.Empty, StringComparison.Ordinal))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} card number does not match authoritative state card number");
            }

            if (!TryReadObjectOptionalString(spectatorBattlefield, "controllerId", out var controllerId)
                || !string.Equals(controllerId, authoritativeBattlefield.ControllerId ?? string.Empty, StringComparison.Ordinal))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} controller does not match authoritative state controller");
            }

            if (!TryReadObjectString(spectatorBattlefield, "status", out var status)
                || !string.Equals(status, authoritativeBattlefield.Status, StringComparison.Ordinal))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} status does not match authoritative state status");
            }

            if (!TryReadObjectBool(spectatorBattlefield, "contested", out var contested)
                || contested != authoritativeBattlefield.Contested)
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} contested does not match authoritative state contested");
            }

            if (!TryReadObjectInt(spectatorBattlefield, "standbySlotCount", out var standbySlotCount)
                || standbySlotCount != authoritativeBattlefield.StandbyObjectIds.Count)
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot count does not match authoritative state standby slot count");
            }

            if (!TryReadObjectInt(spectatorBattlefield, "faceDownStandbyCount", out var faceDownStandbyCount)
                || faceDownStandbyCount != authoritativeBattlefield.FaceDownStandbyCount)
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} face-down standby count does not match authoritative state face-down standby count");
            }

            var hiddenStandbyCount = authoritativeBattlefield.StandbyObjectIds.Count(objectId =>
                IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId));
            if (!TryReadObjectInt(spectatorBattlefield, "hiddenStandbyCount", out var spectatorHiddenStandbyCount)
                || spectatorHiddenStandbyCount != hiddenStandbyCount)
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} hidden standby count does not match authoritative state hidden standby count");
            }
        }
    }

    private static void ValidateSpectatorSnapshotBattlefieldListPayloads(
        IReadOnlyList<object?> spectatorBattlefields,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativeBattlefields = authoritativeState.BattlefieldStates.Values.ToArray();
        var count = Math.Min(spectatorBattlefields.Count, authoritativeBattlefields.Length);
        for (var index = 0; index < count; index++)
        {
            var spectatorBattlefield = spectatorBattlefields[index];
            var authoritativeBattlefield = authoritativeBattlefields[index];
            var battlefieldObjectId = authoritativeBattlefield.BattlefieldObjectId;

            if (!TryReadObjectStringList(spectatorBattlefield, "occupantObjectIds", out var occupantObjectIds)
                || !StringListsEqual(occupantObjectIds, authoritativeBattlefield.OccupantObjectIds))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} occupant object ids do not match authoritative state occupant object ids");
            }

            if (!TryReadObjectStringList(spectatorBattlefield, "occupantControllerIds", out var occupantControllerIds)
                || !StringListsEqual(occupantControllerIds, authoritativeBattlefield.OccupantControllerIds))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} occupant controller ids do not match authoritative state occupant controller ids");
            }

            var expectedUnitsBySide = ExpectedSpectatorUnitsBySide(authoritativeState, authoritativeBattlefield);
            if (!TryReadObjectStringListDictionary(spectatorBattlefield, "unitsBySide", out var unitsBySide)
                || !StringListDictionariesEqual(unitsBySide, expectedUnitsBySide))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} units by side do not match authoritative state units by side");
            }

            var visibleStandbyObjectIds = authoritativeBattlefield.StandbyObjectIds
                .Where(objectId => !IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId))
                .ToArray();
            if (!TryReadObjectStringList(spectatorBattlefield, "standbyObjectIds", out var standbyObjectIds)
                || !StringListsEqual(standbyObjectIds, visibleStandbyObjectIds))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby object ids do not match authoritative state visible standby object ids");
            }

            var expectedScoredPlayerIds = SpectatorBattlefieldScoredThisTurnByPlayers(
                authoritativeState,
                battlefieldObjectId);
            if (!TryReadObjectStringList(spectatorBattlefield, "scoredThisTurnPlayerIds", out var scoredPlayerIds)
                || !StringListsEqual(scoredPlayerIds, expectedScoredPlayerIds))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} scored player ids do not match authoritative state scored player ids");
            }

            var expectedPendingTaskKinds = authoritativeState.PendingCleanupTasks
                .Where(task => string.Equals(task.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal))
                .Select(task => task.Kind)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(kind => kind, StringComparer.Ordinal)
                .ToArray();
            if (!TryReadObjectStringList(spectatorBattlefield, "pendingTaskKinds", out var pendingTaskKinds)
                || !StringListsEqual(pendingTaskKinds, expectedPendingTaskKinds))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} pending task kinds do not match authoritative state pending task kinds");
            }
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ExpectedSpectatorUnitsBySide(
        MatchState authoritativeState,
        BattlefieldState battlefield)
    {
        return battlefield.OccupantObjectIds
            .Where(objectId => authoritativeState.CardObjects.ContainsKey(objectId))
            .GroupBy(
                objectId => EffectiveFieldControllerIdForRecovery(
                    authoritativeState,
                    objectId,
                    authoritativeState.CardObjects[objectId]),
                StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.OrderBy(objectId => objectId, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> SpectatorBattlefieldScoredThisTurnByPlayers(
        MatchState authoritativeState,
        string battlefieldObjectId)
    {
        return authoritativeState.UntilEndOfTurnEffects
            .Select(effectId => TryParseBattlefieldScoreGainedMarker(effectId, out var markerBattlefieldObjectId, out var playerId)
                && string.Equals(markerBattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
                    ? playerId
                    : null)
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(playerId => playerId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryParseBattlefieldScoreGainedMarker(
        string effectId,
        out string battlefieldObjectId,
        out string playerId)
    {
        battlefieldObjectId = string.Empty;
        playerId = string.Empty;
        if (!effectId.StartsWith(BattlefieldTaskMarkers.ScoreGainedThisTurnPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var marker = effectId[BattlefieldTaskMarkers.ScoreGainedThisTurnPrefix.Length..];
        var separator = marker.LastIndexOf(':');
        if (separator <= 0 || separator == marker.Length - 1)
        {
            return false;
        }

        battlefieldObjectId = marker[..separator];
        playerId = marker[(separator + 1)..];
        return !string.IsNullOrWhiteSpace(battlefieldObjectId)
            && !string.IsNullOrWhiteSpace(playerId);
    }

    private static void ValidateSpectatorSnapshotStandbySlotPayloads(
        IReadOnlyList<object?> spectatorBattlefields,
        MatchState authoritativeState,
        List<string> errors)
    {
        var authoritativeBattlefields = authoritativeState.BattlefieldStates.Values.ToArray();
        var count = Math.Min(spectatorBattlefields.Count, authoritativeBattlefields.Length);
        for (var index = 0; index < count; index++)
        {
            var spectatorBattlefield = spectatorBattlefields[index];
            var authoritativeBattlefield = authoritativeBattlefields[index];
            var battlefieldObjectId = authoritativeBattlefield.BattlefieldObjectId;
            if (!TryReadObjectList(spectatorBattlefield, "standbySlots", out var standbySlots))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slots are required");
                continue;
            }

            if (standbySlots.Count != authoritativeBattlefield.StandbyObjectIds.Count)
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slots count does not match authoritative state standby slots count");
            }

            var slotCount = Math.Min(standbySlots.Count, authoritativeBattlefield.StandbyObjectIds.Count);
            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                ValidateSpectatorSnapshotStandbySlotPayload(
                    standbySlots[slotIndex],
                    authoritativeState,
                    authoritativeBattlefield,
                    authoritativeBattlefield.StandbyObjectIds[slotIndex],
                    slotIndex,
                    errors);
            }
        }
    }

    private static void ValidateSpectatorSnapshotStandbySlotPayload(
        object? spectatorStandbySlot,
        MatchState authoritativeState,
        BattlefieldState authoritativeBattlefield,
        string standbyObjectId,
        int slotIndex,
        List<string> errors)
    {
        var battlefieldObjectId = authoritativeBattlefield.BattlefieldObjectId;
        var expectedSlotId = $"{battlefieldObjectId}:standby:{slotIndex + 1}";
        if (!TryReadObjectString(spectatorStandbySlot, "slotId", out var slotId)
            || !string.Equals(slotId, expectedSlotId, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} slot id does not match authoritative state slot id");
        }

        if (!TryReadObjectString(spectatorStandbySlot, "battlefieldObjectId", out var payloadBattlefieldObjectId)
            || !string.Equals(payloadBattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} battlefield id does not match authoritative state battlefield id");
        }

        var expectedControllerId = ExpectedStandbySlotControllerId(authoritativeState, standbyObjectId);
        var expectedSidePlayerId = ExpectedStandbySlotSidePlayerId(
            authoritativeState,
            standbyObjectId,
            expectedControllerId);
        if (!TryReadObjectOptionalString(spectatorStandbySlot, "sidePlayerId", out var sidePlayerId)
            || !string.Equals(sidePlayerId, expectedSidePlayerId, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} side player does not match authoritative state side player");
        }

        if (!TryReadObjectOptionalString(spectatorStandbySlot, "controllerId", out var controllerId)
            || !string.Equals(controllerId, expectedControllerId, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} controller does not match authoritative state controller");
        }

        var expectedVisible = !IsHiddenBattlefieldStandbyForSpectator(authoritativeState, standbyObjectId);
        if (!TryReadObjectBool(spectatorStandbySlot, "visible", out var visible)
            || visible != expectedVisible)
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} visibility does not match authoritative spectator visibility");
        }

        var expectedState = expectedVisible ? "VISIBLE" : "HIDDEN";
        if (!TryReadObjectString(spectatorStandbySlot, "state", out var state)
            || !string.Equals(state, expectedState, StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} state does not match authoritative spectator state");
        }

        var expectedIsFaceDown = authoritativeState.CardObjects.TryGetValue(standbyObjectId, out var cardObject)
            && cardObject.IsFaceDown;
        if (!TryReadObjectBool(spectatorStandbySlot, "isFaceDown", out var isFaceDown)
            || isFaceDown != expectedIsFaceDown)
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} face-down flag does not match authoritative state face-down flag");
        }

        if (expectedVisible)
        {
            if (!TryReadObjectString(spectatorStandbySlot, "objectId", out var objectId)
                || !string.Equals(objectId, standbyObjectId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} object id does not match authoritative visible standby object id");
            }
        }
        else if (TryReadObjectValue(spectatorStandbySlot, "objectId", out _))
        {
            errors.Add(
                $"spectator replay frame snapshot lane battlefield {battlefieldObjectId} standby slot {expectedSlotId} hidden object id must be redacted");
        }
    }

    private static string ExpectedStandbySlotControllerId(MatchState authoritativeState, string standbyObjectId)
    {
        authoritativeState.CardObjects.TryGetValue(standbyObjectId, out var cardObject);
        authoritativeState.ObjectLocations.TryGetValue(standbyObjectId, out var location);
        if (cardObject is null)
        {
            return location?.PlayerId ?? string.Empty;
        }

        return EffectiveFieldControllerIdForRecovery(authoritativeState, standbyObjectId, cardObject);
    }

    private static string ExpectedStandbySlotSidePlayerId(
        MatchState authoritativeState,
        string standbyObjectId,
        string controllerId)
    {
        return authoritativeState.ObjectLocations.TryGetValue(standbyObjectId, out var location)
            ? location.PlayerId
            : controllerId;
    }

    private static IReadOnlyList<(string PlayerId, string ObjectId)> ExpectedSpectatorLaneBattlefieldObjectPairs(
        MatchState authoritativeState)
    {
        return authoritativeState.PlayerZones
            .OrderBy(entry => authoritativeState.Seats.TryGetValue(entry.Key, out var seat) ? seat : entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields
                .Where(objectId => !IsHiddenBattlefieldStandbyForSpectator(authoritativeState, objectId))
                .Select(objectId => (entry.Key, objectId)))
            .ToArray();
    }

    private static bool IsHiddenBattlefieldStandbyForSpectator(MatchState authoritativeState, string objectId)
    {
        if (!authoritativeState.CardObjects.TryGetValue(objectId, out var cardObject)
            || !cardObject.IsFaceDown
            || !cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)
            || !authoritativeState.ObjectLocations.TryGetValue(objectId, out var location)
            || !string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(location.BattlefieldObjectId))
        {
            return false;
        }

        return !string.Equals(cardObject.OwnerId, "__spectator__", StringComparison.Ordinal)
            && !string.Equals(
                EffectiveFieldControllerIdForRecovery(authoritativeState, objectId, cardObject),
                "__spectator__",
                StringComparison.Ordinal);
    }

    private static string EffectiveFieldControllerIdForRecovery(
        MatchState authoritativeState,
        string objectId,
        CardObjectState cardObject)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return cardObject.ControllerId;
        }

        if (!string.IsNullOrWhiteSpace(cardObject.OwnerId))
        {
            return cardObject.OwnerId;
        }

        return authoritativeState.ObjectLocations.TryGetValue(objectId, out var location)
            ? location.PlayerId
            : string.Empty;
    }

    private static int EffectiveWinningScoreForRecovery(MatchState authoritativeState)
    {
        var modifier = authoritativeState.PlayerZones
            .Sum(entry => entry.Value.Battlefields.Count(objectId =>
                authoritativeState.CardObjects.TryGetValue(objectId, out var cardObject)
                && (string.Equals(cardObject.CardNo, BattlefieldIncreaseWinningScoreCardNo, StringComparison.Ordinal)
                    || string.Equals(cardObject.CardNo, BattlefieldIncreaseWinningScoreAltCardNo, StringComparison.Ordinal))
                && SourceObjectControlledByPlayerOrLegacyOwnedForRecovery(cardObject, entry.Key)));
        return BaseWinningScore + modifier;
    }

    private static bool SourceObjectControlledByPlayerOrLegacyOwnedForRecovery(
        CardObjectState cardObject,
        string playerId)
    {
        if (!string.IsNullOrWhiteSpace(cardObject.ControllerId))
        {
            return string.Equals(cardObject.ControllerId, playerId, StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(cardObject.OwnerId)
            || string.Equals(cardObject.OwnerId, playerId, StringComparison.Ordinal);
    }

    private static void ValidateSpectatorPendingTaskQueuePayload(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!timing.TryGetValue("pendingTaskQueue", out var queuePayload)
            || !IsSnapshotPlayerPayloadObject(queuePayload))
        {
            errors.Add("spectator replay frame timing pending task queue is required");
            return;
        }

        var authoritativeQueue = authoritativeState.PendingTaskQueue;
        if (!TryReadObjectBool(queuePayload, "hasTasks", out var hasTasks)
            || hasTasks != authoritativeQueue.HasTasks)
        {
            errors.Add("spectator replay frame timing pending task queue has tasks does not match authoritative state pending task queue has tasks");
        }

        if (!TryReadObjectBool(queuePayload, "isBlocking", out var isBlocking)
            || isBlocking != authoritativeQueue.IsBlocking)
        {
            errors.Add("spectator replay frame timing pending task queue blocking state does not match authoritative state pending task queue blocking state");
        }

        if (!TryReadObjectString(queuePayload, "phase", out var phase)
            || !string.Equals(phase, authoritativeQueue.Phase, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending task queue phase does not match authoritative state pending task queue phase");
        }

        var activeTaskId = ExpectedSpectatorPendingTaskQueueActiveTaskId(authoritativeState);
        if (!TryReadObjectOptionalString(queuePayload, "activeTaskId", out var spectatorActiveTaskId)
            || !string.Equals(spectatorActiveTaskId, activeTaskId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending task queue active task id does not match authoritative state pending task queue active task id");
        }

        if (!TryReadObjectList(queuePayload, "tasks", out var taskPayloads)
            || taskPayloads.Count != authoritativeQueue.Tasks.Count)
        {
            errors.Add("spectator replay frame timing pending task queue task count does not match authoritative state pending task queue task count");
        }
        else
        {
            ValidateSpectatorPendingTaskQueueTaskPayloads(
                taskPayloads,
                authoritativeState,
                authoritativeQueue.Tasks,
                errors);
        }

        if (!TryReadObjectValue(queuePayload, "metadata", out var metadataPayload)
            || !IsSnapshotPlayerPayloadObject(metadataPayload))
        {
            errors.Add("spectator replay frame timing pending task queue metadata is required");
            return;
        }

        if (!TryReadObjectInt(metadataPayload, "taskCount", out var taskCount)
            || taskCount != authoritativeQueue.Tasks.Count)
        {
            errors.Add("spectator replay frame timing pending task queue metadata task count does not match authoritative state pending task queue task count");
        }

        if (!TryReadObjectStringList(metadataPayload, "stateBasedTaskKinds", out var stateBasedTaskKinds)
            || !StringListsEqual(stateBasedTaskKinds, ExpectedStateBasedTaskKinds(authoritativeQueue.Tasks)))
        {
            errors.Add("spectator replay frame timing pending task queue metadata state-based task kinds do not match authoritative state pending task queue state-based task kinds");
        }
    }

    private static void ValidateSpectatorPendingTaskQueueTaskPayloads(
        IReadOnlyList<object?> taskPayloads,
        MatchState authoritativeState,
        IReadOnlyList<CleanupTaskState> authoritativeTasks,
        List<string> errors)
    {
        if (!StringListsEqual(
                ExtractObjectStringValues(taskPayloads, "taskId"),
                authoritativeTasks.Select(task => VisibleCleanupTaskIdForRecovery(authoritativeState, task)).ToArray()))
        {
            errors.Add("spectator replay frame timing pending task queue task ids do not match authoritative state pending task queue task ids");
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(taskPayloads, "kind"),
                authoritativeTasks.Select(task => task.Kind).ToArray()))
        {
            errors.Add("spectator replay frame timing pending task queue task kinds do not match authoritative state pending task queue task kinds");
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(taskPayloads, "reason"),
                authoritativeTasks.Select(task => task.Reason).ToArray()))
        {
            errors.Add("spectator replay frame timing pending task queue task reasons do not match authoritative state pending task queue task reasons");
        }

        if (!StringListsEqual(
                ExtractObjectOptionalStringValues(taskPayloads, "playerId"),
                authoritativeTasks.Select(task => task.PlayerId ?? string.Empty).ToArray()))
        {
            errors.Add("spectator replay frame timing pending task queue task players do not match authoritative state pending task queue task players");
        }

        if (!StringListsEqual(
                ExtractObjectOptionalStringValues(taskPayloads, "battlefieldObjectId"),
                authoritativeTasks.Select(task => task.BattlefieldObjectId ?? string.Empty).ToArray()))
        {
            errors.Add("spectator replay frame timing pending task queue task battlefield object ids do not match authoritative state pending task queue task battlefield object ids");
        }

        var objectIdMismatch = false;
        var hiddenObjectMismatch = false;
        var hiddenObjectKindMismatch = false;
        for (var index = 0; index < authoritativeTasks.Count; index++)
        {
            var task = authoritativeTasks[index];
            var taskPayload = taskPayloads[index];
            if (ShouldHideCleanupTaskObjectIdForRecovery(authoritativeState, task))
            {
                if (TryReadObjectValue(taskPayload, "objectId", out _))
                {
                    objectIdMismatch = true;
                }

                if (!TryReadObjectBool(taskPayload, "hiddenObject", out var hiddenObject)
                    || !hiddenObject)
                {
                    hiddenObjectMismatch = true;
                }

                if (!TryReadObjectString(taskPayload, "hiddenObjectKind", out var hiddenObjectKind)
                    || !string.Equals(hiddenObjectKind, "BATTLEFIELD_STANDBY", StringComparison.Ordinal))
                {
                    hiddenObjectKindMismatch = true;
                }

                continue;
            }

            if (!TryReadObjectOptionalString(taskPayload, "objectId", out var objectId)
                || !string.Equals(objectId, task.ObjectId ?? string.Empty, StringComparison.Ordinal))
            {
                objectIdMismatch = true;
            }

            if (TryReadObjectValue(taskPayload, "hiddenObject", out _))
            {
                hiddenObjectMismatch = true;
            }

            if (TryReadObjectValue(taskPayload, "hiddenObjectKind", out _))
            {
                hiddenObjectKindMismatch = true;
            }
        }

        if (objectIdMismatch)
        {
            errors.Add("spectator replay frame timing pending task queue task object ids do not match authoritative state pending task queue task object ids");
        }

        if (hiddenObjectMismatch)
        {
            errors.Add("spectator replay frame timing pending task queue task hidden object flags do not match authoritative state pending task queue task hidden object flags");
        }

        if (hiddenObjectKindMismatch)
        {
            errors.Add("spectator replay frame timing pending task queue task hidden object kinds do not match authoritative state pending task queue task hidden object kinds");
        }
    }

    private static string ExpectedSpectatorPendingTaskQueueActiveTaskId(MatchState authoritativeState)
    {
        var queue = authoritativeState.PendingTaskQueue;
        var activeTask = queue.Tasks.FirstOrDefault(task =>
            string.Equals(task.TaskId, queue.ActiveTaskId, StringComparison.Ordinal));
        return activeTask is null
            ? queue.ActiveTaskId ?? string.Empty
            : VisibleCleanupTaskIdForRecovery(authoritativeState, activeTask);
    }

    private static string VisibleCleanupTaskIdForRecovery(MatchState authoritativeState, CleanupTaskState task)
    {
        if (!ShouldHideCleanupTaskObjectIdForRecovery(authoritativeState, task))
        {
            return task.TaskId;
        }

        var ordinal = HiddenStandbyOrdinalForRecovery(
            authoritativeState,
            task.ObjectId!,
            task.BattlefieldObjectId);
        return $"cleanup:illegal-standby:{task.BattlefieldObjectId}:hidden-standby-{ordinal}";
    }

    private static bool ShouldHideCleanupTaskObjectIdForRecovery(MatchState authoritativeState, CleanupTaskState task)
    {
        return string.Equals(task.Kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(task.ObjectId)
            && authoritativeState.CardObjects.TryGetValue(task.ObjectId, out var cardObject)
            && cardObject.IsFaceDown
            && cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal);
    }

    private static int HiddenStandbyOrdinalForRecovery(
        MatchState authoritativeState,
        string objectId,
        string? battlefieldObjectId)
    {
        if (string.IsNullOrWhiteSpace(battlefieldObjectId)
            || !authoritativeState.BattlefieldStates.TryGetValue(battlefieldObjectId, out var battlefield))
        {
            return 1;
        }

        var hiddenIds = battlefield.StandbyObjectIds
            .Where(candidate => IsHiddenBattlefieldStandbyForSpectator(authoritativeState, candidate))
            .OrderBy(candidate => candidate, StringComparer.Ordinal)
            .ToArray();
        var index = Array.IndexOf(hiddenIds, objectId);
        return index < 0 ? 1 : index + 1;
    }

    private static IReadOnlyList<string> ExpectedStateBasedTaskKinds(IReadOnlyList<CleanupTaskState> tasks)
    {
        return tasks
            .Where(task => IsSnapshotStateBasedCleanupTaskForRecovery(task.Kind))
            .Select(task => task.Kind)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(kind => kind, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsSnapshotStateBasedCleanupTaskForRecovery(string kind)
    {
        return string.Equals(kind, "DESTROY_LETHAL_UNIT", StringComparison.Ordinal)
            || string.Equals(kind, "DESTROY_ZERO_POWER_UNIT", StringComparison.Ordinal)
            || string.Equals(kind, "REMOVE_ILLEGAL_STANDBY", StringComparison.Ordinal)
            || string.Equals(kind, "RECALL_UNATTACHED_EQUIPMENT", StringComparison.Ordinal);
    }

    private static void ValidateSpectatorPendingPaymentPayload(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!timing.TryGetValue("pendingPayment", out var paymentPayload))
        {
            errors.Add("spectator replay frame timing pending payment is required");
            return;
        }

        var authoritativePayment = authoritativeState.PendingPayment;
        if (authoritativePayment is null)
        {
            if (!IsNullSnapshotPayloadValue(paymentPayload))
            {
                errors.Add("spectator replay frame timing pending payment must be empty when authoritative state has no pending payment");
            }

            return;
        }

        if (!IsSnapshotPlayerPayloadObject(paymentPayload))
        {
            errors.Add("spectator replay frame timing pending payment is required");
            return;
        }

        if (!TryReadObjectString(paymentPayload, "paymentId", out var paymentId)
            || !string.Equals(paymentId, authoritativePayment.PaymentId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending payment id does not match authoritative state pending payment id");
        }

        if (!TryReadObjectString(paymentPayload, "paymentWindow", out var paymentWindow)
            || !string.Equals(paymentWindow, authoritativePayment.PaymentWindow, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending payment window does not match authoritative state pending payment window");
        }

        if (!TryReadObjectString(paymentPayload, "playerId", out var playerId)
            || !string.Equals(playerId, authoritativePayment.PlayerId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending payment player does not match authoritative state pending payment player");
        }

        if (!TryReadObjectValue(paymentPayload, "cost", out var costPayload)
            || !IsSnapshotPlayerPayloadObject(costPayload))
        {
            errors.Add("spectator replay frame timing pending payment cost is required");
        }
        else
        {
            if (!TryReadObjectInt(costPayload, "mana", out var manaCost)
                || manaCost != authoritativePayment.ManaCost)
            {
                errors.Add("spectator replay frame timing pending payment mana cost does not match authoritative state pending payment mana cost");
            }

            if (!TryReadObjectInt(costPayload, "power", out var powerCost)
                || powerCost != authoritativePayment.PowerCost)
            {
                errors.Add("spectator replay frame timing pending payment power cost does not match authoritative state pending payment power cost");
            }

            if (!TryReadObjectIntDictionary(costPayload, "powerByTrait", out var powerCostByTrait)
                || !IntDictionariesEqual(powerCostByTrait, authoritativePayment.PowerCostByTrait))
            {
                errors.Add("spectator replay frame timing pending payment power cost traits do not match authoritative state pending payment power cost traits");
            }
        }

        if (!TryReadObjectStringList(paymentPayload, "paymentChoices", out var paymentChoices)
            || !StringListsEqual(paymentChoices, authoritativePayment.LegalPaymentChoiceIds))
        {
            errors.Add("spectator replay frame timing pending payment choices do not match authoritative state pending payment choices");
        }

        if (!TryReadObjectStringList(paymentPayload, "paymentResourceActions", out var paymentResourceActions)
            || !StringListsEqual(paymentResourceActions, ExpectedSpectatorPendingPaymentResourceActionIds(authoritativeState, authoritativePayment)))
        {
            errors.Add("spectator replay frame timing pending payment resource actions do not match authoritative state pending payment resource actions");
        }
    }

    private static IReadOnlyList<string> ExpectedSpectatorPendingPaymentResourceActionIds(
        MatchState authoritativeState,
        PendingPaymentState payment)
    {
        return payment.PaymentResourceActionIds
            .Concat(payment.LegalPaymentChoiceIds.Where(choiceId =>
                choiceId.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal)))
            .Concat(ExpectedTemporaryPaymentResourceActionIdsForRecovery(authoritativeState, payment))
            .Concat(ExpectedBlueSentinelDelayedResourceActionIdsForRecovery(authoritativeState, payment))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ExpectedTemporaryPaymentResourceActionIdsForRecovery(
        MatchState authoritativeState,
        PendingPaymentState payment)
    {
        if (payment.PowerCost <= 0 && payment.PowerCostByTrait.Count == 0)
        {
            return [];
        }

        var runePool = authoritativeState.RunePools.TryGetValue(payment.PlayerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        if (PaymentCostRules.CanPayPowerCost(runePool, payment.PowerCost, payment.PowerCostByTrait))
        {
            return [];
        }

        return authoritativeState.TemporaryPaymentResources
            .Where(resource => string.Equals(resource.OwnerPlayerId, payment.PlayerId, StringComparison.Ordinal)
                && TemporaryPaymentResourceTotalRemainingPowerForRecovery(resource) > 0
                && resource.AllowedPaymentKinds.Contains(PaymentCostRules.RuneCostPaymentKind, StringComparer.Ordinal)
                && TemporaryPaymentResourceCanHelpPowerCostForRecovery(
                    runePool,
                    resource,
                    payment.PowerCost,
                    payment.PowerCostByTrait))
            .Select(resource => PaymentCostRules.TemporaryPaymentResourceActionId(resource.ResourceId))
            .ToArray();
    }

    private static IReadOnlyList<string> ExpectedBlueSentinelDelayedResourceActionIdsForRecovery(
        MatchState authoritativeState,
        PendingPaymentState payment)
    {
        if (payment.PowerCost <= 0 && payment.PowerCostByTrait.Count == 0)
        {
            return [];
        }

        var runePool = authoritativeState.RunePools.TryGetValue(payment.PlayerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        if (PaymentCostRules.CanPayPowerCost(runePool, payment.PowerCost, payment.PowerCostByTrait))
        {
            return [];
        }

        return authoritativeState.TriggerQueue
            .Where(trigger => BlueSentinelDelayedTriggerCanPayForRecovery(authoritativeState, payment, trigger))
            .Select(trigger => $"{P4ActivatedAbilityCatalog.BlueSentinelDelayedResourceActionPrefix}{trigger.TriggerId}")
            .ToArray();
    }

    private static bool BlueSentinelDelayedTriggerCanPayForRecovery(
        MatchState authoritativeState,
        PendingPaymentState payment,
        TriggerQueueItemState trigger)
    {
        if (!TryReadBlueSentinelDelayedTriggerContextForRecovery(
                trigger.TriggerId,
                out var capturedTurnNumber,
                out var sourceObjectId,
                out var battlefieldObjectId)
            || !string.Equals(trigger.ControllerId, payment.PlayerId, StringComparison.Ordinal)
            || !string.Equals(trigger.SourceObjectId, sourceObjectId, StringComparison.Ordinal)
            || !string.Equals(trigger.EffectKind, P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityEffectKind, StringComparison.Ordinal)
            || !string.Equals(trigger.TriggeredByEventKind, "BATTLEFIELD_HELD", StringComparison.Ordinal)
            || authoritativeState.TurnNumber != capturedTurnNumber + 1
            || !string.Equals(authoritativeState.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(authoritativeState.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || !string.Equals(authoritativeState.ActivePlayerId, payment.PlayerId, StringComparison.Ordinal)
            || !BlueSentinelDelayedSourceStillHoldsBattlefieldForRecovery(
                authoritativeState,
                payment.PlayerId,
                sourceObjectId,
                battlefieldObjectId))
        {
            return false;
        }

        var runePool = authoritativeState.RunePools.TryGetValue(payment.PlayerId, out var currentPool)
            ? currentPool
            : RunePool.Empty;
        return TemporaryPaymentResourceCanHelpPowerCostForRecovery(
            runePool,
            new TemporaryPaymentResourceState(
                $"BLUE_SENTINEL:QUOTE:{trigger.TriggerId}",
                payment.PlayerId,
                sourceObjectId,
                P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityId,
                payment.PaymentWindow,
                generatedPower: P4ActivatedAbilityCatalog.BlueSentinelGeneratedPower,
                remainingPower: P4ActivatedAbilityCatalog.BlueSentinelGeneratedPower,
                allowedPaymentKinds: [PaymentCostRules.RuneCostPaymentKind],
                createdTick: authoritativeState.Tick),
            payment.PowerCost,
            payment.PowerCostByTrait);
    }

    private static bool TryReadBlueSentinelDelayedTriggerContextForRecovery(
        string triggerId,
        out int capturedTurnNumber,
        out string sourceObjectId,
        out string battlefieldObjectId)
    {
        capturedTurnNumber = 0;
        sourceObjectId = string.Empty;
        battlefieldObjectId = string.Empty;
        var parts = triggerId.Split("::", StringSplitOptions.None);
        if (parts.Length != 4
            || !string.Equals(parts[0], "BLUE_SENTINEL_HELD_DELAYED_RESOURCE", StringComparison.Ordinal)
            || !int.TryParse(
                parts[1],
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out capturedTurnNumber)
            || string.IsNullOrWhiteSpace(parts[2])
            || string.IsNullOrWhiteSpace(parts[3]))
        {
            return false;
        }

        sourceObjectId = parts[2];
        battlefieldObjectId = parts[3];
        return capturedTurnNumber > 0;
    }

    private static bool BlueSentinelDelayedSourceStillHoldsBattlefieldForRecovery(
        MatchState authoritativeState,
        string playerId,
        string sourceObjectId,
        string battlefieldObjectId)
    {
        return authoritativeState.CardObjects.TryGetValue(sourceObjectId, out var sourceState)
            && string.Equals(sourceState.CardNo, P4ActivatedAbilityCatalog.BlueSentinelCardNo, StringComparison.Ordinal)
            && sourceState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && !sourceState.IsFaceDown
            && !sourceState.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)
            && SourceObjectControlledByPlayerOrLegacyOwnedForRecovery(sourceState, playerId)
            && authoritativeState.ObjectLocations.TryGetValue(sourceObjectId, out var sourceLocation)
            && string.Equals(sourceLocation.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && string.Equals(sourceLocation.BattlefieldObjectId, battlefieldObjectId, StringComparison.Ordinal)
            && authoritativeState.PlayerZones.TryGetValue(playerId, out var zones)
            && zones.Battlefields.Contains(sourceObjectId, StringComparer.Ordinal)
            && (!authoritativeState.CardObjects.TryGetValue(battlefieldObjectId, out var battlefieldState)
                || SourceObjectControlledByPlayerOrLegacyOwnedForRecovery(battlefieldState, playerId));
    }

    private static int TemporaryPaymentResourceTotalRemainingPowerForRecovery(TemporaryPaymentResourceState resource)
    {
        return resource.RemainingPower + resource.RemainingPowerByTrait.Values.Sum();
    }

    private static bool TemporaryPaymentResourceCanHelpPowerCostForRecovery(
        RunePool runePool,
        TemporaryPaymentResourceState resource,
        int genericPowerCost,
        IReadOnlyDictionary<string, int> powerCostByTrait)
    {
        if (PaymentCostRules.CanPayPowerCost(runePool, genericPowerCost, powerCostByTrait)
            || TemporaryPaymentResourceTotalRemainingPowerForRecovery(resource) <= 0)
        {
            return false;
        }

        var powerByTrait = runePool.PowerByTrait.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var entry in resource.RemainingPowerByTrait)
        {
            powerByTrait[entry.Key] = powerByTrait.TryGetValue(entry.Key, out var existing)
                ? existing + entry.Value
                : entry.Value;
        }

        return PaymentCostRules.CanPayPowerCost(
            new RunePool(runePool.Mana, runePool.Power + resource.RemainingPower, powerByTrait),
            genericPowerCost,
            powerCostByTrait);
    }

    private static void ValidateSpectatorTemporaryPaymentResourcePayloads(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!TryReadObjectList(timing, "temporaryPaymentResources", out var spectatorResources))
        {
            errors.Add("spectator replay frame timing temporary payment resources are required");
            return;
        }

        var authoritativeResources = authoritativeState.TemporaryPaymentResources;
        if (spectatorResources.Count != authoritativeResources.Count)
        {
            errors.Add(
                $"spectator replay frame timing temporary payment resource count {spectatorResources.Count} does not match authoritative state temporary payment resource count {authoritativeResources.Count}");
            return;
        }

        var resourceIdsMatch = true;
        var ownerPlayerIdsMatch = true;
        var sourceObjectIdsMatch = true;
        var abilityIdsMatch = true;
        var paymentWindowsMatch = true;
        var generatedPowersMatch = true;
        var remainingPowersMatch = true;
        var generatedPowerTraitsMatch = true;
        var remainingPowerTraitsMatch = true;
        var allowedPaymentKindsMatch = true;
        var paymentOnlyFlagsMatch = true;
        var resourceRestrictionsMatch = true;
        var createdTicksMatch = true;

        for (var index = 0; index < authoritativeResources.Count; index++)
        {
            var spectatorResource = spectatorResources[index];
            if (!IsSnapshotPlayerPayloadObject(spectatorResource))
            {
                errors.Add("spectator replay frame timing temporary payment resource payload is required");
                return;
            }

            var authoritativeResource = authoritativeResources[index];
            if (!TryReadObjectString(spectatorResource, "resourceId", out var resourceId)
                || !string.Equals(resourceId, authoritativeResource.ResourceId, StringComparison.Ordinal))
            {
                resourceIdsMatch = false;
            }

            if (!TryReadObjectString(spectatorResource, "ownerPlayerId", out var ownerPlayerId)
                || !string.Equals(ownerPlayerId, authoritativeResource.OwnerPlayerId, StringComparison.Ordinal))
            {
                ownerPlayerIdsMatch = false;
            }

            if (!TryReadObjectString(spectatorResource, "sourceObjectId", out var sourceObjectId)
                || !string.Equals(sourceObjectId, authoritativeResource.SourceObjectId, StringComparison.Ordinal))
            {
                sourceObjectIdsMatch = false;
            }

            if (!TryReadObjectString(spectatorResource, "abilityId", out var abilityId)
                || !string.Equals(abilityId, authoritativeResource.AbilityId, StringComparison.Ordinal))
            {
                abilityIdsMatch = false;
            }

            if (!TryReadObjectString(spectatorResource, "paymentWindow", out var paymentWindow)
                || !string.Equals(paymentWindow, authoritativeResource.PaymentWindow, StringComparison.Ordinal))
            {
                paymentWindowsMatch = false;
            }

            if (!TryReadObjectInt(spectatorResource, "generatedPower", out var generatedPower)
                || generatedPower != authoritativeResource.GeneratedPower)
            {
                generatedPowersMatch = false;
            }

            if (!TryReadObjectInt(spectatorResource, "remainingPower", out var remainingPower)
                || remainingPower != authoritativeResource.RemainingPower)
            {
                remainingPowersMatch = false;
            }

            if (!TryReadObjectIntDictionary(spectatorResource, "generatedPowerByTrait", out var generatedPowerByTrait)
                || !IntDictionariesEqual(generatedPowerByTrait, authoritativeResource.GeneratedPowerByTrait))
            {
                generatedPowerTraitsMatch = false;
            }

            if (!TryReadObjectIntDictionary(spectatorResource, "remainingPowerByTrait", out var remainingPowerByTrait)
                || !IntDictionariesEqual(remainingPowerByTrait, authoritativeResource.RemainingPowerByTrait))
            {
                remainingPowerTraitsMatch = false;
            }

            if (!TryReadObjectStringList(spectatorResource, "allowedPaymentKinds", out var allowedPaymentKinds)
                || !StringListsEqual(allowedPaymentKinds, authoritativeResource.AllowedPaymentKinds))
            {
                allowedPaymentKindsMatch = false;
            }

            if (!TryReadObjectBool(spectatorResource, "paymentOnly", out var paymentOnly)
                || !paymentOnly)
            {
                paymentOnlyFlagsMatch = false;
            }

            if (!TryReadObjectString(spectatorResource, "resourceRestriction", out var resourceRestriction)
                || !string.Equals(
                    resourceRestriction,
                    TemporaryPaymentResourceRestrictionForRecovery(authoritativeResource),
                    StringComparison.Ordinal))
            {
                resourceRestrictionsMatch = false;
            }

            if (!TryReadObjectLong(spectatorResource, "createdTick", out var createdTick)
                || createdTick != authoritativeResource.CreatedTick)
            {
                createdTicksMatch = false;
            }
        }

        if (!resourceIdsMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource ids disagree with authoritative state temporary payment resource ids");
        }

        if (!ownerPlayerIdsMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource owners disagree with authoritative state temporary payment resource owners");
        }

        if (!sourceObjectIdsMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource source objects disagree with authoritative state temporary payment resource source objects");
        }

        if (!abilityIdsMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource ability ids disagree with authoritative state temporary payment resource ability ids");
        }

        if (!paymentWindowsMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource payment windows disagree with authoritative state temporary payment resource payment windows");
        }

        if (!generatedPowersMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource generated powers disagree with authoritative state temporary payment resource generated powers");
        }

        if (!remainingPowersMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource remaining powers disagree with authoritative state temporary payment resource remaining powers");
        }

        if (!generatedPowerTraitsMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource generated power traits disagree with authoritative state temporary payment resource generated power traits");
        }

        if (!remainingPowerTraitsMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource remaining power traits disagree with authoritative state temporary payment resource remaining power traits");
        }

        if (!allowedPaymentKindsMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource allowed payment kinds disagree with authoritative state temporary payment resource allowed payment kinds");
        }

        if (!paymentOnlyFlagsMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource payment-only flags disagree with authoritative state temporary payment resource payment-only flags");
        }

        if (!resourceRestrictionsMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource restrictions disagree with authoritative state temporary payment resource restrictions");
        }

        if (!createdTicksMatch)
        {
            errors.Add("spectator replay frame timing temporary payment resource created ticks disagree with authoritative state temporary payment resource created ticks");
        }
    }

    private static string TemporaryPaymentResourceRestrictionForRecovery(TemporaryPaymentResourceState resource)
    {
        if (P4ActivatedAbilityCatalog.TryGetSigilTypedResourceProfile(resource.AbilityId, out var profile))
        {
            return profile.ResourceRestriction;
        }

        return string.Equals(resource.AbilityId, P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId, StringComparison.Ordinal)
            ? P4ActivatedAbilityCatalog.AncientStelePaymentOnlyResourceRestriction
            : string.Equals(resource.AbilityId, P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, StringComparison.Ordinal)
                ? P4ActivatedAbilityCatalog.JhinMoveResourceRestriction
            : P4ActivatedAbilityCatalog.IsBlueSentinelResourceAbility(resource.AbilityId)
                ? P4ActivatedAbilityCatalog.BlueSentinelPaymentOnlyResourceRestriction
            : P4ActivatedAbilityCatalog.IsHoneyfruitResourceAbility(resource.AbilityId)
                ? P4ActivatedAbilityCatalog.HoneyfruitPaymentOnlyResourceRestriction
            : P4ActivatedAbilityCatalog.IsGoldTokenResourceAbility(resource.AbilityId)
                ? P4ActivatedAbilityCatalog.GoldTokenPaymentOnlyResourceRestriction
            : P4ActivatedAbilityCatalog.MalzaharPaymentOnlyResourceRestriction;
    }

    private static void ValidateSpectatorPendingHandChoicePayload(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!timing.TryGetValue("pendingHandChoice", out var choicePayload))
        {
            errors.Add("spectator replay frame timing pending hand choice is required");
            return;
        }

        var authoritativeChoice = authoritativeState.PendingHandChoice;
        if (authoritativeChoice is null)
        {
            if (!IsNullSnapshotPayloadValue(choicePayload))
            {
                errors.Add("spectator replay frame timing pending hand choice must be empty when authoritative state has no pending hand choice");
            }

            return;
        }

        if (!IsSnapshotPlayerPayloadObject(choicePayload))
        {
            errors.Add("spectator replay frame timing pending hand choice is required");
            return;
        }

        if (!TryReadObjectString(choicePayload, "choiceId", out var choiceId)
            || !string.Equals(choiceId, authoritativeChoice.ChoiceId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending hand choice id does not match authoritative state pending hand choice id");
        }

        if (!TryReadObjectString(choicePayload, "choiceWindow", out var choiceWindow)
            || !string.Equals(choiceWindow, authoritativeChoice.ChoiceWindow, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending hand choice window does not match authoritative state pending hand choice window");
        }

        if (!TryReadObjectString(choicePayload, "playerId", out var playerId)
            || !string.Equals(playerId, authoritativeChoice.PlayerId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending hand choice player does not match authoritative state pending hand choice player");
        }

        if (!TryReadObjectInt(choicePayload, "requiredCount", out var requiredCount)
            || requiredCount != authoritativeChoice.RequiredCount)
        {
            errors.Add("spectator replay frame timing pending hand choice required count does not match authoritative state pending hand choice required count");
        }

        if (!TryReadObjectInt(choicePayload, "maxCount", out var maxCount)
            || maxCount != authoritativeChoice.MaxCount)
        {
            errors.Add("spectator replay frame timing pending hand choice max count does not match authoritative state pending hand choice max count");
        }

        if (!TryReadObjectString(choicePayload, "reason", out var reason)
            || !string.Equals(reason, authoritativeChoice.Reason, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending hand choice reason does not match authoritative state pending hand choice reason");
        }

        if (!TryReadObjectString(choicePayload, "sourceObjectId", out var sourceObjectId)
            || !string.Equals(sourceObjectId, authoritativeChoice.SourceObjectId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending hand choice source object does not match authoritative state pending hand choice source object");
        }

        if (!TryReadObjectString(choicePayload, "effectKind", out var effectKind)
            || !string.Equals(effectKind, authoritativeChoice.EffectKind, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending hand choice effect kind does not match authoritative state pending hand choice effect kind");
        }

        if (!TryReadObjectString(choicePayload, "choiceState", out var choiceState)
            || !string.Equals(choiceState, "WAITING_FOR_CHOICE", StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing pending hand choice state does not match authoritative spectator pending hand choice state");
        }

        if (TryReadObjectValue(choicePayload, "legalObjectIds", out _))
        {
            errors.Add("spectator replay frame timing pending hand choice legal object ids must be redacted");
        }
    }

    private static void ValidateSpectatorBattlefieldTaskPayloads(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!TryReadObjectList(timing, "battlefieldTasks", out var spectatorBattlefieldTasks))
        {
            errors.Add("spectator replay frame timing battlefield tasks are required");
            return;
        }

        var authoritativeBattlefieldTasks = authoritativeState.BattlefieldTasks;
        if (spectatorBattlefieldTasks.Count != authoritativeBattlefieldTasks.Count)
        {
            errors.Add(
                $"spectator replay frame timing battlefield task count {spectatorBattlefieldTasks.Count} does not match authoritative state battlefield task count {authoritativeBattlefieldTasks.Count}");
            return;
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(spectatorBattlefieldTasks, "taskId"),
                authoritativeBattlefieldTasks.Select(task => task.TaskId).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task ids disagree with authoritative state battlefield task ids");
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(spectatorBattlefieldTasks, "kind"),
                authoritativeBattlefieldTasks.Select(task => task.Kind).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task kinds disagree with authoritative state battlefield task kinds");
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(spectatorBattlefieldTasks, "status"),
                authoritativeBattlefieldTasks.Select(task => task.Status).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task statuses disagree with authoritative state battlefield task statuses");
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(spectatorBattlefieldTasks, "reason"),
                authoritativeBattlefieldTasks.Select(task => task.Reason).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task reasons disagree with authoritative state battlefield task reasons");
        }

        if (!StringListsEqual(
                ExtractObjectStringValues(spectatorBattlefieldTasks, "battlefieldObjectId"),
                authoritativeBattlefieldTasks.Select(task => task.BattlefieldObjectId).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task battlefield object ids disagree with authoritative state battlefield task battlefield object ids");
        }

        if (!StringListCollectionsEqual(
                ExtractObjectStringListValues(spectatorBattlefieldTasks, "participantControllerIds"),
                authoritativeBattlefieldTasks.Select(task => task.ParticipantControllerIds).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task participant controller ids disagree with authoritative state battlefield task participant controller ids");
        }

        if (!StringListCollectionsEqual(
                ExtractObjectStringListValues(spectatorBattlefieldTasks, "participantObjectIds"),
                authoritativeBattlefieldTasks.Select(task => task.ParticipantObjectIds).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task participant object ids disagree with authoritative state battlefield task participant object ids");
        }

        if (!StringListsEqual(
                ExtractObjectOptionalStringValues(spectatorBattlefieldTasks, "actingPlayerId"),
                authoritativeBattlefieldTasks.Select(task => task.ActingPlayerId ?? string.Empty).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task acting players disagree with authoritative state battlefield task acting players");
        }

        if (!StringListCollectionsEqual(
                ExtractObjectStringListValues(spectatorBattlefieldTasks, "stackItemIds"),
                authoritativeBattlefieldTasks.Select(task => task.StackItemIds).ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task stack item ids disagree with authoritative state battlefield task stack item ids");
        }

        if (!StringListsEqual(
                ExtractObjectOptionalStringValues(spectatorBattlefieldTasks, "spellDuelId"),
                authoritativeBattlefieldTasks
                    .Select(task => string.Equals(task.Kind, "START_SPELL_DUEL", StringComparison.Ordinal)
                        ? BattleLifecycleIds.SpellDuelIdForBattlefield(task.BattlefieldObjectId) ?? string.Empty
                        : string.Empty)
                    .ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task spell duel ids disagree with authoritative state battlefield task spell duel ids");
        }

        if (!StringListsEqual(
                ExtractObjectOptionalStringValues(spectatorBattlefieldTasks, "battleId"),
                authoritativeBattlefieldTasks
                    .Select(task => string.Equals(task.Kind, "START_BATTLE", StringComparison.Ordinal)
                        ? BattleLifecycleIds.BattleIdForBattlefield(task.BattlefieldObjectId) ?? string.Empty
                        : string.Empty)
                    .ToArray()))
        {
            errors.Add("spectator replay frame timing battlefield task battle ids disagree with authoritative state battlefield task battle ids");
        }
    }

    private static bool BattlefieldObjectPairsEqual(
        IReadOnlyList<(string PlayerId, string ObjectId)> left,
        IReadOnlyList<(string PlayerId, string ObjectId)> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].PlayerId, right[index].PlayerId, StringComparison.Ordinal)
                || !string.Equals(left[index].ObjectId, right[index].ObjectId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateSnapshotPlayerCoverage(
        RecoveredPlayerView view,
        IEnumerable<string> expectedPlayerIds,
        List<string> errors)
    {
        foreach (var expectedPlayerId in expectedPlayerIds)
        {
            if (!view.Snapshot.Players.ContainsKey(expectedPlayerId))
            {
                errors.Add($"snapshot for {view.PlayerId} is missing player {expectedPlayerId}");
            }
        }
    }

    private static void ValidateSnapshotActivePlayer(
        RecoveredPlayerView view,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(view.Snapshot.ActivePlayerId))
        {
            errors.Add($"snapshot for {view.PlayerId} active player is required");
            return;
        }

        if (!view.Snapshot.Players.ContainsKey(view.Snapshot.ActivePlayerId))
        {
            errors.Add(
                $"snapshot for {view.PlayerId} active player {view.Snapshot.ActivePlayerId} is missing from players");
        }
    }

    private static void ValidatePlayerViewAgreement(
        IReadOnlyDictionary<string, RecoveredPlayerView> playerViews,
        List<string> errors)
    {
        var viewsWithSnapshots = playerViews.Values
            .Where(view => view.Snapshot is not null)
            .ToArray();
        if (viewsWithSnapshots.Length <= 1)
        {
            return;
        }

        var baseline = viewsWithSnapshots
            .OrderBy(view => view.PlayerId, StringComparer.Ordinal)
            .First();
        var baselineSnapshot = baseline.Snapshot;
        var baselineSnapshotTick = baseline.SnapshotTick;
        var baselineSnapshotEventSequence = baseline.SnapshotEventSequence;
        var baselineSeats = ExtractSeats(baselineSnapshot);
        foreach (var view in viewsWithSnapshots)
        {
            if (view.SnapshotTick != baselineSnapshotTick)
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees on row tick");
            }

            if (view.SnapshotEventSequence != baselineSnapshotEventSequence)
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees on event sequence");
            }

            if (view.Snapshot.TurnNumber != baselineSnapshot.TurnNumber)
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees on turn number");
            }

            if (!string.Equals(view.Snapshot.ActivePlayerId, baselineSnapshot.ActivePlayerId, StringComparison.Ordinal))
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees on active player");
            }

            var seats = ExtractSeats(view.Snapshot);
            if (!SeatsEqual(baselineSeats, seats))
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees on player seats");
            }
        }
    }

    private static void ValidateAuthoritativeState(
        string roomId,
        long? currentTick,
        MatchState? authoritativeState,
        IReadOnlyDictionary<string, RecoveredPlayerView> playerViews,
        List<string> errors)
    {
        if (authoritativeState is null)
        {
            return;
        }

        if (!string.Equals(authoritativeState.RoomId, roomId, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state room {authoritativeState.RoomId} does not match recovery room {roomId}");
        }

        if (currentTick is { } expectedTick && authoritativeState.Tick != expectedTick)
        {
            errors.Add($"authoritative state tick {authoritativeState.Tick} does not match recovery tick {expectedTick}");
        }

        ValidateAuthoritativeStateScalars(authoritativeState, errors);
        ValidateAuthoritativeStateSeats(authoritativeState, errors);
        ValidateAuthoritativeStateResourceValues(authoritativeState, errors);
        ValidateAuthoritativeStatePlayerZoneValues(authoritativeState.PlayerZones, errors);
        ValidateAuthoritativeStateStackAndTriggerValues(authoritativeState, errors);
        ValidateAuthoritativeStatePendingPaymentValues(authoritativeState.PendingPayment, errors);
        ValidateAuthoritativeStatePendingHandChoiceValues(authoritativeState.PendingHandChoice, errors);
        ValidateAuthoritativeStateUntilEndOfTurnEffectValues(authoritativeState.UntilEndOfTurnEffects, errors);
        ValidateAuthoritativeStateResolutionHistory(authoritativeState, errors);
        ValidateAuthoritativeStatePlayerPointers(authoritativeState, errors);

        foreach (var view in playerViews.Values)
        {
            if (view.Snapshot is null)
            {
                continue;
            }

            if (view.Snapshot.TurnNumber != authoritativeState.TurnNumber)
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees with authoritative state turn number");
            }

            if (!string.Equals(view.Snapshot.ActivePlayerId, authoritativeState.ActivePlayerId, StringComparison.Ordinal))
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees with authoritative state active player");
            }

            var viewSeats = ExtractSeats(view.Snapshot);
            if (!SeatsEqual(authoritativeState.Seats, viewSeats))
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees with authoritative state seats");
            }
        }
    }

    private static void ValidateAuthoritativeStateScalars(
        MatchState authoritativeState,
        List<string> errors)
    {
        ValidateAuthoritativeStateRequiredText(
            "room id",
            authoritativeState.RoomId,
            errors);

        if (authoritativeState.Tick < 0)
        {
            errors.Add($"authoritative state tick {authoritativeState.Tick} cannot be negative");
        }

        if (authoritativeState.TurnNumber < 1)
        {
            errors.Add($"authoritative state turn number {authoritativeState.TurnNumber} is invalid");
        }

        ValidateAuthoritativeStateKnownText(
            "status",
            authoritativeState.Status,
            IsKnownMatchStatus,
            errors);
        ValidateAuthoritativeStateKnownText(
            "phase",
            authoritativeState.Phase,
            IsKnownMatchPhase,
            errors);
        ValidateAuthoritativeStateKnownText(
            "timing state",
            authoritativeState.TimingState,
            IsKnownTimingState,
            errors);

        if (authoritativeState.RngCursor < 0)
        {
            errors.Add($"authoritative state rng cursor {authoritativeState.RngCursor} cannot be negative");
        }
    }

    private static string? ValidateAuthoritativeStateRequiredText(
        string valueLabel,
        string? value,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"authoritative state {valueLabel} is required");
            return null;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {valueLabel} {normalizedValue} has surrounding whitespace");
        }

        return normalizedValue;
    }

    private static void ValidateAuthoritativeStateKnownText(
        string valueLabel,
        string? value,
        Func<string, bool> isKnownValue,
        List<string> errors)
    {
        var normalizedValue = ValidateAuthoritativeStateRequiredText(valueLabel, value, errors);
        if (normalizedValue is null)
        {
            return;
        }

        if (!isKnownValue(normalizedValue))
        {
            errors.Add($"authoritative state {valueLabel} {normalizedValue} is invalid");
        }
    }

    private static string? ValidateAuthoritativeStateOptionalTextValue(
        string valueLabel,
        string? value,
        List<string> errors)
    {
        if (value is null)
        {
            errors.Add($"authoritative state {valueLabel} value is required");
            return null;
        }

        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"authoritative state {valueLabel} is blank");
            return null;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {valueLabel} {normalizedValue} has surrounding whitespace");
        }

        return normalizedValue;
    }

    private static string? ValidateAuthoritativeStateNullableTextValue(
        string valueLabel,
        string? value,
        List<string> errors)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"authoritative state {valueLabel} is blank");
            return null;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {valueLabel} {normalizedValue} has surrounding whitespace");
        }

        return normalizedValue;
    }

    private static void ValidateAuthoritativeStateResourceValues(
        MatchState authoritativeState,
        List<string> errors)
    {
        ValidateAuthoritativeStateRunePoolValues(authoritativeState.RunePools, errors);
        ValidateAuthoritativeStateIntegerMapValues(
            "score",
            authoritativeState.PlayerScores,
            requirePositive: false,
            errors);
        ValidateAuthoritativeStateIntegerMapValues(
            "experience",
            authoritativeState.PlayerExperience,
            requirePositive: false,
            errors);
        ValidateAuthoritativeStateIntegerMapValues(
            "cards played this turn",
            authoritativeState.PlayerCardsPlayedThisTurn,
            requirePositive: true,
            errors);
        ValidateAuthoritativeStateTemporaryPaymentResourceValues(
            authoritativeState.TemporaryPaymentResources,
            authoritativeState.Tick,
            errors);
    }

    private static void ValidateAuthoritativeStateRunePoolValues(
        IReadOnlyDictionary<string, RunePool>? runePools,
        List<string> errors)
    {
        if (runePools is null)
        {
            return;
        }

        foreach (var (playerId, runePool) in runePools.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                continue;
            }

            var playerLabel = playerId.Trim();
            if (runePool is null)
            {
                errors.Add($"authoritative state rune pool for {playerLabel} is required");
                continue;
            }

            if (runePool.Mana < 0)
            {
                errors.Add(
                    $"authoritative state rune pool for {playerLabel} mana {runePool.Mana} cannot be negative");
            }

            if (runePool.Power < 0)
            {
                errors.Add(
                    $"authoritative state rune pool for {playerLabel} power {runePool.Power} cannot be negative");
            }

            ValidateAuthoritativeStateTraitPowerValues(
                $"rune pool for {playerLabel}",
                "power trait",
                runePool.PowerByTrait,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateIntegerMapValues(
        string valueLabel,
        IReadOnlyDictionary<string, int>? values,
        bool requirePositive,
        List<string> errors)
    {
        if (values is null)
        {
            return;
        }

        foreach (var (playerId, value) in values.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                continue;
            }

            var playerLabel = playerId.Trim();
            if (requirePositive)
            {
                if (value <= 0)
                {
                    errors.Add(
                        $"authoritative state {valueLabel} for {playerLabel} {value} must be positive");
                }

                continue;
            }

            if (value < 0)
            {
                errors.Add(
                    $"authoritative state {valueLabel} for {playerLabel} {value} cannot be negative");
            }
        }
    }

    private static void ValidateAuthoritativeStateTemporaryPaymentResourceValues(
        IReadOnlyList<TemporaryPaymentResourceState>? temporaryPaymentResources,
        long authoritativeTick,
        List<string> errors)
    {
        if (temporaryPaymentResources is null)
        {
            return;
        }

        var seenResourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in temporaryPaymentResources.OrderBy(
            item => item?.ResourceId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (resource is null)
            {
                continue;
            }

            var resourceId = ValidateAuthoritativeStateRequiredText(
                "temporary payment resource id",
                resource.ResourceId,
                errors);
            var resourceLabel = resourceId ?? "<unknown>";
            if (resourceId is not null && !seenResourceIds.Add(resourceId))
            {
                errors.Add($"authoritative state temporary payment resource {resourceId} is duplicated");
            }

            ValidateAuthoritativeStateOptionalTextValue(
                $"temporary payment resource {resourceLabel} source object",
                resource.SourceObjectId,
                errors);
            ValidateAuthoritativeStateOptionalTextValue(
                $"temporary payment resource {resourceLabel} ability id",
                resource.AbilityId,
                errors);
            ValidateAuthoritativeStateOptionalTextValue(
                $"temporary payment resource {resourceLabel} payment window",
                resource.PaymentWindow,
                errors);

            if (resource.GeneratedPower < 0)
            {
                errors.Add(
                    $"authoritative state temporary payment resource {resourceLabel} generated power {resource.GeneratedPower} cannot be negative");
            }

            if (resource.RemainingPower < 0)
            {
                errors.Add(
                    $"authoritative state temporary payment resource {resourceLabel} remaining power {resource.RemainingPower} cannot be negative");
            }

            if (resource.CreatedTick < 0)
            {
                errors.Add(
                    $"authoritative state temporary payment resource {resourceLabel} created tick {resource.CreatedTick} cannot be negative");
            }
            else if (resource.CreatedTick > authoritativeTick)
            {
                errors.Add(
                    $"authoritative state temporary payment resource {resourceLabel} created tick {resource.CreatedTick} is after authoritative state tick {authoritativeTick}");
            }

            ValidateAuthoritativeStateTraitPowerValues(
                $"temporary payment resource {resourceLabel}",
                "generated power trait",
                resource.GeneratedPowerByTrait,
                errors);
            ValidateAuthoritativeStateTraitPowerValues(
                $"temporary payment resource {resourceLabel}",
                "remaining power trait",
                resource.RemainingPowerByTrait,
                errors);
            ValidateAuthoritativeStatePaymentKindValues(
                resourceLabel,
                resource.AllowedPaymentKinds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateTraitPowerValues(
        string ownerLabel,
        string traitLabel,
        IReadOnlyDictionary<string, int>? values,
        List<string> errors)
    {
        if (values is null)
        {
            errors.Add($"authoritative state {ownerLabel} {traitLabel} map is required");
            return;
        }

        var seenTraits = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (trait, value) in values.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(trait))
            {
                errors.Add($"authoritative state {ownerLabel} {traitLabel} is required");
                continue;
            }

            var normalizedTrait = RuneTrait.Normalize(trait);
            var trimmedTrait = trait.Trim();
            if (!string.Equals(trait, trimmedTrait, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state {ownerLabel} {traitLabel} {normalizedTrait} has surrounding whitespace");
            }

            if (value <= 0)
            {
                errors.Add(
                    $"authoritative state {ownerLabel} {traitLabel} {normalizedTrait} value {value} must be positive");
            }

            if (!seenTraits.Add(normalizedTrait))
            {
                errors.Add($"authoritative state {ownerLabel} {traitLabel} {normalizedTrait} is duplicated");
            }
        }
    }

    private static void ValidateAuthoritativeStatePaymentKindValues(
        string resourceLabel,
        IReadOnlyList<string>? allowedPaymentKinds,
        List<string> errors)
    {
        if (allowedPaymentKinds is null)
        {
            errors.Add(
                $"authoritative state temporary payment resource {resourceLabel} allowed payment kind list is required");
            return;
        }

        var seenPaymentKinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var paymentKind in allowedPaymentKinds)
        {
            if (string.IsNullOrWhiteSpace(paymentKind))
            {
                errors.Add(
                    $"authoritative state temporary payment resource {resourceLabel} allowed payment kind is required");
                continue;
            }

            var normalizedPaymentKind = paymentKind.Trim();
            if (!string.Equals(paymentKind, normalizedPaymentKind, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state temporary payment resource {resourceLabel} allowed payment kind {normalizedPaymentKind} has surrounding whitespace");
            }

            if (!seenPaymentKinds.Add(normalizedPaymentKind))
            {
                errors.Add(
                    $"authoritative state temporary payment resource {resourceLabel} allowed payment kind {normalizedPaymentKind} is duplicated");
            }
        }
    }

    private static void ValidateAuthoritativeStateStackAndTriggerValues(
        MatchState authoritativeState,
        List<string> errors)
    {
        ValidateAuthoritativeStateStackItemValues(authoritativeState.StackItems, errors);
        ValidateAuthoritativeStateTriggerQueueValues(authoritativeState.TriggerQueue, errors);
    }

    private static void ValidateAuthoritativeStatePendingPaymentValues(
        PendingPaymentState? pendingPayment,
        List<string> errors)
    {
        if (pendingPayment is null)
        {
            return;
        }

        var paymentId = ValidateAuthoritativeStateRequiredText(
            "pending payment id",
            pendingPayment.PaymentId,
            errors);
        var paymentLabel = paymentId ?? "<unknown>";
        ValidateAuthoritativeStateRequiredText(
            $"pending payment {paymentLabel} window",
            pendingPayment.PaymentWindow,
            errors);
        ValidateAuthoritativeStateOptionalTextValue(
            $"pending payment {paymentLabel} reason",
            pendingPayment.Reason,
            errors);

        if (pendingPayment.ManaCost < 0)
        {
            errors.Add(
                $"authoritative state pending payment {paymentLabel} mana cost {pendingPayment.ManaCost} cannot be negative");
        }

        if (pendingPayment.PowerCost < 0)
        {
            errors.Add(
                $"authoritative state pending payment {paymentLabel} power cost {pendingPayment.PowerCost} cannot be negative");
        }

        ValidateAuthoritativeStateTraitPowerValues(
            $"pending payment {paymentLabel}",
            "power cost trait",
            pendingPayment.PowerCostByTrait,
            errors);
        ValidateAuthoritativeStateStringListValues(
            $"pending payment {paymentLabel} legal payment choice",
            pendingPayment.LegalPaymentChoiceIds,
            errors,
            rejectDuplicates: true);
        ValidateAuthoritativeStateStringListValues(
            $"pending payment {paymentLabel} payment resource action",
            pendingPayment.PaymentResourceActionIds,
            errors,
            rejectDuplicates: true);
    }

    private static void ValidateAuthoritativeStatePendingHandChoiceValues(
        PendingHandChoiceState? pendingHandChoice,
        List<string> errors)
    {
        if (pendingHandChoice is null)
        {
            return;
        }

        var choiceId = ValidateAuthoritativeStateRequiredText(
            "pending hand choice id",
            pendingHandChoice.ChoiceId,
            errors);
        var choiceLabel = choiceId ?? "<unknown>";
        ValidateAuthoritativeStateRequiredText(
            $"pending hand choice {choiceLabel} window",
            pendingHandChoice.ChoiceWindow,
            errors);
        ValidateAuthoritativeStateOptionalTextValue(
            $"pending hand choice {choiceLabel} reason",
            pendingHandChoice.Reason,
            errors);
        ValidateAuthoritativeStateOptionalTextValue(
            $"pending hand choice {choiceLabel} source object",
            pendingHandChoice.SourceObjectId,
            errors);
        ValidateAuthoritativeStateOptionalTextValue(
            $"pending hand choice {choiceLabel} effect kind",
            pendingHandChoice.EffectKind,
            errors);

        if (pendingHandChoice.RequiredCount < 1)
        {
            errors.Add(
                $"authoritative state pending hand choice {choiceLabel} required count {pendingHandChoice.RequiredCount} is invalid");
        }

        if (pendingHandChoice.MaxCount < pendingHandChoice.RequiredCount)
        {
            errors.Add(
                $"authoritative state pending hand choice {choiceLabel} max count {pendingHandChoice.MaxCount} is less than required count {pendingHandChoice.RequiredCount}");
        }

        ValidateAuthoritativeStateStringListValues(
            $"pending hand choice {choiceLabel} legal object",
            pendingHandChoice.LegalObjectIds,
            errors,
            rejectDuplicates: true);

        if (pendingHandChoice.LegalObjectIds is not null && pendingHandChoice.RequiredCount > 0)
        {
            var distinctLegalObjectCount = pendingHandChoice.LegalObjectIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (distinctLegalObjectCount < pendingHandChoice.RequiredCount)
            {
                errors.Add(
                    $"authoritative state pending hand choice {choiceLabel} legal object count {distinctLegalObjectCount} is less than required count {pendingHandChoice.RequiredCount}");
            }
        }
    }

    private static void ValidateAuthoritativeStateUntilEndOfTurnEffectValues(
        IReadOnlyList<string>? untilEndOfTurnEffects,
        List<string> errors)
    {
        ValidateAuthoritativeStateStringListValues(
            "until end of turn effect",
            untilEndOfTurnEffects,
            errors,
            rejectDuplicates: true);
    }

    private static void ValidateAuthoritativeStateStackItemValues(
        IReadOnlyList<StackItemState>? stackItems,
        List<string> errors)
    {
        if (stackItems is null)
        {
            return;
        }

        var seenStackItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stackItem in stackItems.OrderBy(item => item?.StackItemId ?? string.Empty, StringComparer.Ordinal))
        {
            if (stackItem is null)
            {
                continue;
            }

            var stackItemId = ValidateAuthoritativeStateRequiredText(
                "stack item id",
                stackItem.StackItemId,
                errors);
            var stackItemLabel = stackItemId ?? "<unknown>";
            if (stackItemId is not null && !seenStackItemIds.Add(stackItemId))
            {
                errors.Add($"authoritative state stack item {stackItemId} is duplicated");
            }

            ValidateAuthoritativeStateRequiredText(
                $"stack item {stackItemLabel} effect kind",
                stackItem.EffectKind,
                errors);
            ValidateAuthoritativeStateOptionalTextValue(
                $"stack item {stackItemLabel} source object",
                stackItem.SourceObjectId,
                errors);
            ValidateAuthoritativeStateOptionalTextValue(
                $"stack item {stackItemLabel} card no",
                stackItem.CardNo,
                errors);
            ValidateAuthoritativeStateOptionalTextValue(
                $"stack item {stackItemLabel} destination",
                stackItem.Destination,
                errors);
            ValidateAuthoritativeStateOptionalTextValue(
                $"stack item {stackItemLabel} timing context",
                stackItem.TimingContext,
                errors);
            ValidateAuthoritativeStateStringListValues(
                $"stack item {stackItemLabel} target object",
                stackItem.TargetObjectIds,
                errors);
            ValidateAuthoritativeStateStringListValues(
                $"stack item {stackItemLabel} optional cost",
                stackItem.OptionalCosts,
                errors);

            if (stackItem.DamageAmount < 0)
            {
                errors.Add(
                    $"authoritative state stack item {stackItemLabel} damage amount {stackItem.DamageAmount} cannot be negative");
            }

            if (stackItem.EffectRepeatCount < 1)
            {
                errors.Add(
                    $"authoritative state stack item {stackItemLabel} effect repeat count {stackItem.EffectRepeatCount} is invalid");
            }
        }
    }

    private static void ValidateAuthoritativeStateTriggerQueueValues(
        IReadOnlyList<TriggerQueueItemState>? triggerQueue,
        List<string> errors)
    {
        if (triggerQueue is null)
        {
            return;
        }

        var seenTriggerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trigger in triggerQueue.OrderBy(item => item?.TriggerId ?? string.Empty, StringComparer.Ordinal))
        {
            if (trigger is null)
            {
                continue;
            }

            var triggerId = ValidateAuthoritativeStateRequiredText(
                "trigger queue item id",
                trigger.TriggerId,
                errors);
            var triggerLabel = triggerId ?? "<unknown>";
            if (triggerId is not null && !seenTriggerIds.Add(triggerId))
            {
                errors.Add($"authoritative state trigger queue item {triggerId} is duplicated");
            }

            ValidateAuthoritativeStateRequiredText(
                $"trigger queue item {triggerLabel} effect kind",
                trigger.EffectKind,
                errors);
            ValidateAuthoritativeStateRequiredText(
                $"trigger queue item {triggerLabel} triggered event kind",
                trigger.TriggeredByEventKind,
                errors);
            ValidateAuthoritativeStateOptionalTextValue(
                $"trigger queue item {triggerLabel} source object",
                trigger.SourceObjectId,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateStringListValues(
        string itemLabel,
        IReadOnlyList<string>? values,
        List<string> errors,
        bool rejectDuplicates = false)
    {
        if (values is null)
        {
            errors.Add($"authoritative state {itemLabel} list is required");
            return;
        }

        var seenValues = rejectDuplicates
            ? new HashSet<string>(StringComparer.Ordinal)
            : null;
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"authoritative state {itemLabel} is required");
                continue;
            }

            var normalizedValue = value.Trim();
            if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
            {
                errors.Add($"authoritative state {itemLabel} {normalizedValue} has surrounding whitespace");
            }

            if (seenValues is not null && !seenValues.Add(normalizedValue))
            {
                errors.Add($"authoritative state {itemLabel} {normalizedValue} is duplicated");
            }
        }
    }

    private static void ValidateAuthoritativeStateResolutionHistory(
        MatchState authoritativeState,
        List<string> errors)
    {
        ValidateAuthoritativeStateBattlefieldResolutionMetadata(
            authoritativeState.BattlefieldResolutions,
            authoritativeState.Tick,
            errors);
        ValidateAuthoritativeStateBattleResolutionMetadata(
            authoritativeState.BattleResolutions,
            authoritativeState.Tick,
            errors);
    }

    private static void ValidateAuthoritativeStateBattlefieldResolutionMetadata(
        IReadOnlyList<BattlefieldResolutionState>? battlefieldResolutions,
        long authoritativeTick,
        List<string> errors)
    {
        if (battlefieldResolutions is null)
        {
            errors.Add("authoritative state battlefield resolutions list is required");
            return;
        }

        var seenResolutionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resolution in battlefieldResolutions.OrderBy(
            item => item?.ResolutionId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (resolution is null)
            {
                errors.Add("authoritative state battlefield resolution is required");
                continue;
            }

            var resolutionId = ValidateAuthoritativeStateResolutionId(
                "battlefield resolution",
                resolution.ResolutionId,
                seenResolutionIds,
                errors);
            var diagnosticResolutionId = string.IsNullOrEmpty(resolutionId) ? "<missing>" : resolutionId;
            ValidateAuthoritativeStateResolutionTick(
                "battlefield resolution",
                resolutionId,
                resolution.Tick,
                authoritativeTick,
                errors);
            ValidateAuthoritativeStateResolutionText(
                "battlefield resolution",
                resolutionId,
                "kind",
                resolution.Kind,
                errors);
            ValidateAuthoritativeStateResolutionText(
                "battlefield resolution",
                resolutionId,
                "reason",
                resolution.Reason,
                errors);
            ValidateAuthoritativeStateNullableTextValue(
                $"battlefield resolution {diagnosticResolutionId} source object",
                resolution.SourceObjectId,
                errors);
            ValidateAuthoritativeStateResolutionTextList(
                "battlefield resolution",
                resolutionId,
                "participant object",
                resolution.ParticipantObjectIds,
                errors,
                requireList: false);
            ValidateAuthoritativeStateResolutionTextList(
                "battlefield resolution",
                resolutionId,
                "related event kind",
                resolution.RelatedEventKinds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateBattleResolutionMetadata(
        IReadOnlyList<BattleResolutionState>? battleResolutions,
        long authoritativeTick,
        List<string> errors)
    {
        if (battleResolutions is null)
        {
            errors.Add("authoritative state battle resolutions list is required");
            return;
        }

        var seenResolutionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resolution in battleResolutions.OrderBy(
            item => item?.ResolutionId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (resolution is null)
            {
                errors.Add("authoritative state battle resolution is required");
                continue;
            }

            var resolutionId = ValidateAuthoritativeStateResolutionId(
                "battle resolution",
                resolution.ResolutionId,
                seenResolutionIds,
                errors);
            ValidateAuthoritativeStateResolutionTick(
                "battle resolution",
                resolutionId,
                resolution.Tick,
                authoritativeTick,
                errors);
            ValidateAuthoritativeStateResolutionText(
                "battle resolution",
                resolutionId,
                "kind",
                resolution.Kind,
                errors);
            ValidateAuthoritativeStateResolutionText(
                "battle resolution",
                resolutionId,
                "reason",
                resolution.Reason,
                errors);
            ValidateAuthoritativeStateResolutionTextList(
                "battle resolution",
                resolutionId,
                "attacker object",
                resolution.AttackerObjectIds,
                errors,
                requireList: false);
            ValidateAuthoritativeStateResolutionTextList(
                "battle resolution",
                resolutionId,
                "defender object",
                resolution.DefenderObjectIds,
                errors,
                requireList: false);
            ValidateAuthoritativeStateResolutionTextList(
                "battle resolution",
                resolutionId,
                "surviving attacker object",
                resolution.SurvivingAttackerObjectIds,
                errors,
                requireList: false);
            ValidateAuthoritativeStateResolutionTextList(
                "battle resolution",
                resolutionId,
                "surviving defender object",
                resolution.SurvivingDefenderObjectIds,
                errors,
                requireList: false);
            ValidateAuthoritativeStateResolutionTextList(
                "battle resolution",
                resolutionId,
                "destroyed object",
                resolution.DestroyedObjectIds,
                errors,
                requireList: false);
            ValidateAuthoritativeStateResolutionTextList(
                "battle resolution",
                resolutionId,
                "related event kind",
                resolution.RelatedEventKinds,
                errors);
        }
    }

    private static string ValidateAuthoritativeStateResolutionId(
        string resolutionLabel,
        string? resolutionId,
        ISet<string> seenResolutionIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(resolutionId))
        {
            errors.Add($"authoritative state {resolutionLabel} id is required");
            return string.Empty;
        }

        var normalizedResolutionId = resolutionId.Trim();
        if (!string.Equals(resolutionId, normalizedResolutionId, StringComparison.Ordinal))
        {
            errors.Add(
                $"authoritative state {resolutionLabel} {normalizedResolutionId} id has surrounding whitespace");
        }

        if (!seenResolutionIds.Add(normalizedResolutionId))
        {
            errors.Add($"authoritative state {resolutionLabel} {normalizedResolutionId} id is duplicated");
        }

        return normalizedResolutionId;
    }

    private static void ValidateAuthoritativeStateResolutionTick(
        string resolutionLabel,
        string resolutionId,
        long tick,
        long authoritativeTick,
        List<string> errors)
    {
        var diagnosticResolutionId = string.IsNullOrEmpty(resolutionId) ? "<missing>" : resolutionId;
        if (tick < 0)
        {
            errors.Add(
                $"authoritative state {resolutionLabel} {diagnosticResolutionId} tick {tick} cannot be negative");
        }

        if (tick > authoritativeTick)
        {
            errors.Add(
                $"authoritative state {resolutionLabel} {diagnosticResolutionId} tick {tick} is after authoritative state tick {authoritativeTick}");
        }
    }

    private static void ValidateAuthoritativeStateResolutionText(
        string resolutionLabel,
        string resolutionId,
        string valueLabel,
        string? value,
        List<string> errors)
    {
        var diagnosticResolutionId = string.IsNullOrEmpty(resolutionId) ? "<missing>" : resolutionId;
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(
                $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} is required");
            return;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add(
                $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} {normalizedValue} has surrounding whitespace");
        }
    }

    private static void ValidateAuthoritativeStateResolutionTextList(
        string resolutionLabel,
        string resolutionId,
        string valueLabel,
        IReadOnlyList<string>? values,
        List<string> errors,
        bool requireList = true)
    {
        var diagnosticResolutionId = string.IsNullOrEmpty(resolutionId) ? "<missing>" : resolutionId;
        if (values is null)
        {
            if (requireList)
            {
                errors.Add(
                    $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} list is required");
            }

            return;
        }

        var seenValues = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add(
                    $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} is required");
                continue;
            }

            var normalizedValue = value.Trim();
            if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} {normalizedValue} has surrounding whitespace");
            }

            if (!seenValues.Add(normalizedValue))
            {
                errors.Add(
                    $"authoritative state {resolutionLabel} {diagnosticResolutionId} {valueLabel} {normalizedValue} is duplicated");
            }
        }
    }

    private static void ValidateAuthoritativeStatePlayerPointers(
        MatchState authoritativeState,
        List<string> errors)
    {
        var seatPlayerIds = authoritativeState.Seats.Keys
            .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
            .Select(playerId => playerId.Trim())
            .ToHashSet(StringComparer.Ordinal);

        ValidateAuthoritativeStateRequiredPlayerPointer(
            "active player",
            authoritativeState.ActivePlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateRequiredPlayerPointer(
            "turn player",
            authoritativeState.TurnPlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateOptionalPlayerPointer(
            "priority player",
            authoritativeState.PriorityPlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateOptionalPlayerPointer(
            "focus player",
            authoritativeState.FocusPlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateOptionalPlayerPointer(
            "winner player",
            authoritativeState.WinnerPlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateOptionalPlayerPointer(
            "opening second action player",
            authoritativeState.OpeningSecondActionPlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateOptionalPlayerPointer(
            "extra turn player",
            authoritativeState.ExtraTurnPlayerId,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerList(
            "ready player",
            authoritativeState.ReadyPlayerIds,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerList(
            "passed priority player",
            authoritativeState.PassedPriorityPlayerIds,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerList(
            "passed focus player",
            authoritativeState.PassedFocusPlayerIds,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerList(
            "mulligan completed player",
            authoritativeState.MulliganCompletedPlayerIds,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerList(
            "destroyed unit owner",
            authoritativeState.DestroyedUnitOwnerIdsThisTurn,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerMap(
            "rune pool player",
            authoritativeState.RunePools,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerMap(
            "zone player",
            authoritativeState.PlayerZones,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerMap(
            "score player",
            authoritativeState.PlayerScores,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerMap(
            "experience player",
            authoritativeState.PlayerExperience,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerMap(
            "cards played player",
            authoritativeState.PlayerCardsPlayedThisTurn,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStatePlayerMap(
            "decklist player",
            authoritativeState.PlayerDecklists,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateMapKeys("card object map key", authoritativeState.CardObjects, errors);
        ValidateAuthoritativeStateMapKeys("object location map key", authoritativeState.ObjectLocations, errors);
        ValidateAuthoritativeStateCardObjectIdentities(authoritativeState.CardObjects, errors);
        ValidateAuthoritativeStateCardObjectValues(authoritativeState.CardObjects, errors);
        ValidateAuthoritativeStateCardObjectPlayers(authoritativeState.CardObjects, seatPlayerIds, errors);
        ValidateAuthoritativeStateObjectLocationPlayers(authoritativeState.ObjectLocations, seatPlayerIds, errors);
        ValidateAuthoritativeStateObjectLocationZones(authoritativeState.ObjectLocations, errors);
        ValidateAuthoritativeStateObjectLocationPlayerZones(
            authoritativeState.PlayerZones,
            authoritativeState.ObjectLocations,
            errors);
        ValidateAuthoritativeStateStackPlayers(authoritativeState.StackItems, seatPlayerIds, errors);
        ValidateAuthoritativeStateTriggerQueuePlayers(authoritativeState.TriggerQueue, seatPlayerIds, errors);
        ValidateAuthoritativeStatePendingPaymentPlayer(authoritativeState.PendingPayment, seatPlayerIds, errors);
        ValidateAuthoritativeStatePendingHandChoicePlayer(authoritativeState.PendingHandChoice, seatPlayerIds, errors);
        ValidateAuthoritativeStateTemporaryPaymentResourcePlayers(
            authoritativeState.TemporaryPaymentResources,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateBattlefieldResolutionPlayers(
            authoritativeState.BattlefieldResolutions,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateBattleResolutionPlayers(
            authoritativeState.BattleResolutions,
            seatPlayerIds,
            errors);
        ValidateAuthoritativeStateObjectReferences(authoritativeState, errors);
    }

    private static void ValidateAuthoritativeStateRequiredPlayerPointer(
        string pointerName,
        string? playerId,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            errors.Add($"authoritative state {pointerName} is required");
            return;
        }

        var normalizedPlayerId = playerId.Trim();
        if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {pointerName} {normalizedPlayerId} has surrounding whitespace");
        }

        if (!seatPlayerIds.Contains(normalizedPlayerId))
        {
            errors.Add($"authoritative state {pointerName} {normalizedPlayerId} is missing from seats");
        }
    }

    private static void ValidateAuthoritativeStatePlayerList(
        string listMemberName,
        IReadOnlyList<string>? playerIds,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (playerIds is null)
        {
            errors.Add($"authoritative state {listMemberName} list is required");
            return;
        }

        var seenPlayerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var playerId in playerIds)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                errors.Add($"authoritative state {listMemberName} id is required");
                continue;
            }

            var normalizedPlayerId = playerId.Trim();
            if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state {listMemberName} {normalizedPlayerId} has surrounding whitespace");
            }

            if (!seatPlayerIds.Contains(normalizedPlayerId))
            {
                errors.Add(
                    $"authoritative state {listMemberName} {normalizedPlayerId} is missing from seats");
            }

            if (!seenPlayerIds.Add(normalizedPlayerId))
            {
                errors.Add($"authoritative state {listMemberName} {normalizedPlayerId} is duplicated");
            }
        }
    }

    private static void ValidateAuthoritativeStatePlayerMap<TValue>(
        string mapKeyName,
        IReadOnlyDictionary<string, TValue>? values,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (values is null)
        {
            errors.Add($"authoritative state {mapKeyName} map is required");
            return;
        }

        var seenPlayerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var playerId in values.Keys.OrderBy(playerId => playerId, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                errors.Add($"authoritative state {mapKeyName} id is required");
                continue;
            }

            var normalizedPlayerId = playerId.Trim();
            if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
            {
                errors.Add($"authoritative state {mapKeyName} {normalizedPlayerId} has surrounding whitespace");
            }

            if (!seatPlayerIds.Contains(normalizedPlayerId))
            {
                errors.Add($"authoritative state {mapKeyName} {normalizedPlayerId} is missing from seats");
            }

            if (!seenPlayerIds.Add(normalizedPlayerId))
            {
                errors.Add($"authoritative state {mapKeyName} {normalizedPlayerId} is duplicated");
            }
        }
    }

    private static void ValidateAuthoritativeStateCardObjectPlayers(
        IReadOnlyDictionary<string, CardObjectState>? cardObjects,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (cardObjects is null)
        {
            errors.Add("authoritative state card objects map is required");
            return;
        }

        foreach (var (objectId, cardObject) in cardObjects.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (cardObject is null)
            {
                errors.Add($"authoritative state card object {objectId} is required");
                continue;
            }

            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"card object {objectId} owner player",
                cardObject.OwnerId,
                seatPlayerIds,
                errors);
            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"card object {objectId} controller player",
                cardObject.ControllerId,
                seatPlayerIds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateMapKeys<TValue>(
        string mapKeyLabel,
        IReadOnlyDictionary<string, TValue>? values,
        List<string> errors)
    {
        if (values is null)
        {
            return;
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in values.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"authoritative state {mapKeyLabel} is required");
                continue;
            }

            var normalizedKey = key.Trim();
            if (!string.Equals(key, normalizedKey, StringComparison.Ordinal))
            {
                errors.Add($"authoritative state {mapKeyLabel} {normalizedKey} has surrounding whitespace");
            }

            if (!seenKeys.Add(normalizedKey))
            {
                errors.Add($"authoritative state {mapKeyLabel} {normalizedKey} is duplicated");
            }
        }
    }

    private static void ValidateAuthoritativeStateCardObjectIdentities(
        IReadOnlyDictionary<string, CardObjectState>? cardObjects,
        List<string> errors)
    {
        if (cardObjects is null)
        {
            return;
        }

        foreach (var (objectId, cardObject) in cardObjects.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (cardObject is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(cardObject.ObjectId))
            {
                errors.Add($"authoritative state card object {objectId} object id is required");
                continue;
            }

            var normalizedMapObjectId = objectId.Trim();
            var normalizedCardObjectId = cardObject.ObjectId.Trim();
            if (!string.Equals(cardObject.ObjectId, normalizedCardObjectId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state card object {objectId} object id {normalizedCardObjectId} has surrounding whitespace");
            }

            if (!string.Equals(normalizedMapObjectId, normalizedCardObjectId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state card object {objectId} object id {normalizedCardObjectId} does not match map key");
            }
        }
    }

    private static void ValidateAuthoritativeStateCardObjectValues(
        IReadOnlyDictionary<string, CardObjectState>? cardObjects,
        List<string> errors)
    {
        if (cardObjects is null)
        {
            return;
        }

        foreach (var (objectId, cardObject) in cardObjects.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (cardObject is null)
            {
                continue;
            }

            ValidateAuthoritativeStateNullableTextValue(
                $"card object {objectId} card no",
                cardObject.CardNo,
                errors);
            ValidateAuthoritativeStateNullableTextValue(
                $"card object {objectId} attached object",
                cardObject.AttachedToObjectId,
                errors);

            if (cardObject.Damage < 0)
            {
                errors.Add($"authoritative state card object {objectId} damage {cardObject.Damage} cannot be negative");
            }

            if (cardObject.ManaCost < 0)
            {
                errors.Add($"authoritative state card object {objectId} mana cost {cardObject.ManaCost} cannot be negative");
            }

            ValidateAuthoritativeStateStringListValues(
                $"card object {objectId} until end of turn effect",
                cardObject.UntilEndOfTurnEffects,
                errors,
                rejectDuplicates: true);
            ValidateAuthoritativeStateStringListValues(
                $"card object {objectId} tag",
                cardObject.Tags,
                errors,
                rejectDuplicates: true);
            ValidateAuthoritativeStateCardObjectPowerModifierValues(
                objectId,
                cardObject.UntilEndOfTurnPowerModifiers,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateCardObjectPowerModifierValues(
        string objectId,
        IReadOnlyList<PowerModifierLedgerEntry>? powerModifiers,
        List<string> errors)
    {
        if (powerModifiers is null)
        {
            errors.Add($"authoritative state card object {objectId} power modifier list is required");
            return;
        }

        var seenEffectIds = new HashSet<string>(StringComparer.Ordinal);
        var seenAppliedOrders = new HashSet<int>();
        foreach (var modifier in powerModifiers.OrderBy(
            entry => entry?.EffectId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (modifier is null)
            {
                errors.Add($"authoritative state card object {objectId} power modifier is required");
                continue;
            }

            var modifierId = ValidateAuthoritativeStateRequiredText(
                $"card object {objectId} power modifier id",
                modifier.EffectId,
                errors);
            var modifierLabel = modifierId ?? "<unknown>";
            if (modifierId is not null && !seenEffectIds.Add(modifierId))
            {
                errors.Add($"authoritative state card object {objectId} power modifier {modifierId} is duplicated");
            }

            ValidateAuthoritativeStateRequiredText(
                $"card object {objectId} power modifier {modifierLabel} effect kind",
                modifier.EffectKind,
                errors);
            ValidateAuthoritativeStateRequiredText(
                $"card object {objectId} power modifier {modifierLabel} duration",
                modifier.Duration,
                errors);
            var targetObjectId = ValidateAuthoritativeStateRequiredText(
                $"card object {objectId} power modifier {modifierLabel} target object",
                modifier.TargetObjectId,
                errors);
            ValidateAuthoritativeStateRequiredText(
                $"card object {objectId} power modifier {modifierLabel} source path",
                modifier.SourcePath,
                errors);
            ValidateAuthoritativeStateNullableTextValue(
                $"card object {objectId} power modifier {modifierLabel} source object",
                modifier.SourceObjectId,
                errors);
            ValidateAuthoritativeStateNullableTextValue(
                $"card object {objectId} power modifier {modifierLabel} source card no",
                modifier.SourceCardNo,
                errors);

            if (targetObjectId is not null && !string.Equals(targetObjectId, objectId.Trim(), StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state card object {objectId} power modifier {modifierLabel} target object {targetObjectId} does not match card object");
            }

            if (modifier.PowerDelta == 0)
            {
                errors.Add(
                    $"authoritative state card object {objectId} power modifier {modifierLabel} power delta 0 is invalid");
            }

            if (modifier.MinimumPower < 0)
            {
                errors.Add(
                    $"authoritative state card object {objectId} power modifier {modifierLabel} minimum power {modifier.MinimumPower} cannot be negative");
            }

            if (modifier.MinimumPower > 0 && modifier.ResultingPower < modifier.MinimumPower)
            {
                errors.Add(
                    $"authoritative state card object {objectId} power modifier {modifierLabel} resulting power {modifier.ResultingPower} is less than minimum power {modifier.MinimumPower}");
            }

            if (modifier.AppliedOrder is { } appliedOrder)
            {
                if (appliedOrder < 1)
                {
                    errors.Add(
                        $"authoritative state card object {objectId} power modifier {modifierLabel} applied order {appliedOrder} is invalid");
                }
                else if (!seenAppliedOrders.Add(appliedOrder))
                {
                    errors.Add(
                        $"authoritative state card object {objectId} power modifier applied order {appliedOrder} is duplicated");
                }
            }
        }
    }

    private static void ValidateAuthoritativeStateObjectLocationPlayers(
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (objectLocations is null)
        {
            errors.Add("authoritative state object locations map is required");
            return;
        }

        foreach (var (objectId, location) in objectLocations.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (location is null)
            {
                errors.Add($"authoritative state object location {objectId} is required");
                continue;
            }

            ValidateAuthoritativeStateRequiredObjectPlayer(
                $"object location {objectId} player",
                location.PlayerId,
                seatPlayerIds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateObjectLocationZones(
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        List<string> errors)
    {
        if (objectLocations is null)
        {
            return;
        }

        foreach (var (objectId, location) in objectLocations.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (location is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(location.Zone))
            {
                errors.Add($"authoritative state object location {objectId} zone is required");
                continue;
            }

            var normalizedZone = location.Zone.Trim();
            if (!string.Equals(location.Zone, normalizedZone, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state object location {objectId} zone {normalizedZone} has surrounding whitespace");
            }

            if (!IsKnownAuthoritativeObjectLocationZone(normalizedZone))
            {
                errors.Add($"authoritative state object location {objectId} zone {normalizedZone} is not supported");
            }

            ValidateAuthoritativeStateNullableTextValue(
                $"object location {objectId} battlefield object",
                location.BattlefieldObjectId,
                errors);
        }
    }

    private static bool IsKnownAuthoritativeObjectLocationZone(string zone)
    {
        return zone switch
        {
            "MAIN_DECK"
                or "RUNE_DECK"
                or "HAND"
                or "BASE"
                or "BATTLEFIELD"
                or "GRAVEYARD"
                or "BANISHED"
                or "LEGEND"
                or "CHAMPION"
                or "STACK" => true,
            _ => false
        };
    }

    private static void ValidateAuthoritativeStateObjectLocationPlayerZones(
        IReadOnlyDictionary<string, PlayerZones>? playerZones,
        IReadOnlyDictionary<string, ObjectLocationState>? objectLocations,
        List<string> errors)
    {
        if (playerZones is null || objectLocations is null)
        {
            return;
        }

        var playerZoneLocations = BuildAuthoritativeStatePlayerZoneObjectIndex(
            playerZones,
            errors,
            reportZoneValueErrors: false);
        if (playerZoneLocations.Count == 0)
        {
            return;
        }

        foreach (var (objectId, location) in objectLocations.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (location is null
                || string.IsNullOrWhiteSpace(objectId)
                || string.IsNullOrWhiteSpace(location.PlayerId)
                || string.IsNullOrWhiteSpace(location.Zone))
            {
                continue;
            }

            var normalizedZone = location.Zone.Trim();
            if (!IsKnownAuthoritativeObjectLocationZone(normalizedZone)
                || string.Equals(normalizedZone, "STACK", StringComparison.Ordinal))
            {
                continue;
            }

            var normalizedObjectId = objectId.Trim();
            var normalizedPlayerId = location.PlayerId.Trim();
            if (!playerZoneLocations.TryGetValue(normalizedObjectId, out var playerZoneLocation))
            {
                errors.Add($"authoritative state object location {normalizedObjectId} is missing from player zones");
                continue;
            }

            if (!string.Equals(playerZoneLocation.PlayerId, normalizedPlayerId, StringComparison.Ordinal)
                || !string.Equals(playerZoneLocation.Zone, normalizedZone, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state object location {normalizedObjectId} {normalizedPlayerId}/{normalizedZone} disagrees with player zones {playerZoneLocation.PlayerId}/{playerZoneLocation.Zone}");
            }
        }
    }

    private static void ValidateAuthoritativeStatePlayerZoneValues(
        IReadOnlyDictionary<string, PlayerZones>? playerZones,
        List<string> errors)
    {
        if (playerZones is null)
        {
            return;
        }

        _ = BuildAuthoritativeStatePlayerZoneObjectIndex(
            playerZones,
            errors,
            reportZoneValueErrors: true);
    }

    private static Dictionary<string, AuthoritativeStateZoneLocation> BuildAuthoritativeStatePlayerZoneObjectIndex(
        IReadOnlyDictionary<string, PlayerZones> playerZones,
        List<string> errors,
        bool reportZoneValueErrors)
    {
        var playerZoneLocations = new Dictionary<string, AuthoritativeStateZoneLocation>(StringComparer.Ordinal);
        foreach (var (playerId, zones) in playerZones.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                continue;
            }

            var normalizedPlayerId = playerId.Trim();
            if (zones is null)
            {
                if (reportZoneValueErrors)
                {
                    errors.Add($"authoritative state player zones for {normalizedPlayerId} are required");
                }

                continue;
            }

            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "MAIN_DECK",
                zones.MainDeck,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "RUNE_DECK",
                zones.RuneDeck,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "HAND",
                zones.Hand,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "BASE",
                zones.Base,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "BATTLEFIELD",
                zones.Battlefields,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "GRAVEYARD",
                zones.Graveyard,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "BANISHED",
                zones.Banished,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "LEGEND",
                zones.LegendZone,
                errors,
                reportZoneValueErrors);
            AddAuthoritativeStatePlayerZoneObjects(
                playerZoneLocations,
                normalizedPlayerId,
                "CHAMPION",
                zones.ChampionZone,
                errors,
                reportZoneValueErrors);
        }

        return playerZoneLocations;
    }

    private static void AddAuthoritativeStatePlayerZoneObjects(
        Dictionary<string, AuthoritativeStateZoneLocation> playerZoneLocations,
        string playerId,
        string zone,
        IReadOnlyList<string>? objectIds,
        List<string> errors,
        bool reportZoneValueErrors)
    {
        if (objectIds is null)
        {
            if (reportZoneValueErrors)
            {
                errors.Add($"authoritative state player zones {playerId}/{zone} list is required");
            }

            return;
        }

        foreach (var objectId in objectIds)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                if (reportZoneValueErrors)
                {
                    errors.Add($"authoritative state player zones {playerId}/{zone} object id is required");
                }

                continue;
            }

            var normalizedObjectId = objectId.Trim();
            if (reportZoneValueErrors
                && !string.Equals(objectId, normalizedObjectId, StringComparison.Ordinal))
            {
                errors.Add(
                    $"authoritative state player zones {playerId}/{zone} object {normalizedObjectId} has surrounding whitespace");
            }

            var location = new AuthoritativeStateZoneLocation(playerId, zone);
            if (playerZoneLocations.TryGetValue(normalizedObjectId, out var existingLocation))
            {
                if (reportZoneValueErrors)
                {
                    errors.Add(
                        $"authoritative state player zones object {normalizedObjectId} is duplicated between {existingLocation.PlayerId}/{existingLocation.Zone} and {playerId}/{zone}");
                }

                continue;
            }

            playerZoneLocations[normalizedObjectId] = location;
        }
    }

    private readonly record struct AuthoritativeStateZoneLocation(string PlayerId, string Zone);

    private static void ValidateAuthoritativeStateStackPlayers(
        IReadOnlyList<StackItemState>? stackItems,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (stackItems is null)
        {
            errors.Add("authoritative state stack items list is required");
            return;
        }

        foreach (var stackItem in stackItems.OrderBy(item => item?.StackItemId ?? string.Empty, StringComparer.Ordinal))
        {
            if (stackItem is null)
            {
                errors.Add("authoritative state stack item is required");
                continue;
            }

            ValidateAuthoritativeStateRequiredObjectPlayer(
                $"stack item {stackItem.StackItemId} controller player",
                stackItem.ControllerId,
                seatPlayerIds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateTriggerQueuePlayers(
        IReadOnlyList<TriggerQueueItemState>? triggerQueue,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (triggerQueue is null)
        {
            errors.Add("authoritative state trigger queue list is required");
            return;
        }

        foreach (var trigger in triggerQueue.OrderBy(item => item?.TriggerId ?? string.Empty, StringComparer.Ordinal))
        {
            if (trigger is null)
            {
                errors.Add("authoritative state trigger queue item is required");
                continue;
            }

            ValidateAuthoritativeStateRequiredObjectPlayer(
                $"trigger queue item {trigger.TriggerId} controller player",
                trigger.ControllerId,
                seatPlayerIds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStatePendingPaymentPlayer(
        PendingPaymentState? pendingPayment,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (pendingPayment is null)
        {
            return;
        }

        ValidateAuthoritativeStateRequiredObjectPlayer(
            "pending payment player",
            pendingPayment.PlayerId,
            seatPlayerIds,
            errors);
    }

    private static void ValidateAuthoritativeStatePendingHandChoicePlayer(
        PendingHandChoiceState? pendingHandChoice,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (pendingHandChoice is null)
        {
            return;
        }

        ValidateAuthoritativeStateRequiredObjectPlayer(
            "pending hand choice player",
            pendingHandChoice.PlayerId,
            seatPlayerIds,
            errors);
    }

    private static void ValidateAuthoritativeStateTemporaryPaymentResourcePlayers(
        IReadOnlyList<TemporaryPaymentResourceState>? temporaryPaymentResources,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (temporaryPaymentResources is null)
        {
            errors.Add("authoritative state temporary payment resources list is required");
            return;
        }

        foreach (var resource in temporaryPaymentResources.OrderBy(
            item => item?.ResourceId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (resource is null)
            {
                errors.Add("authoritative state temporary payment resource is required");
                continue;
            }

            ValidateAuthoritativeStateRequiredObjectPlayer(
                $"temporary payment resource {resource.ResourceId} owner player",
                resource.OwnerPlayerId,
                seatPlayerIds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateObjectReferences(
        MatchState authoritativeState,
        List<string> errors)
    {
        var knownObjectIds = BuildAuthoritativeStateKnownObjectIds(authoritativeState);
        if (knownObjectIds.Count == 0)
        {
            return;
        }

        foreach (var (objectId, cardObject) in authoritativeState.CardObjects
            .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (cardObject is null)
            {
                continue;
            }

            ValidateAuthoritativeStateOptionalObjectReference(
                $"card object {objectId} attached object",
                cardObject.AttachedToObjectId,
                knownObjectIds,
                errors);
        }

        foreach (var (objectId, location) in authoritativeState.ObjectLocations
            .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (location is null)
            {
                continue;
            }

            ValidateAuthoritativeStateOptionalObjectReference(
                $"object location {objectId} battlefield object",
                location.BattlefieldObjectId,
                knownObjectIds,
                errors);
        }

        foreach (var stackItem in authoritativeState.StackItems
            .OrderBy(item => item?.StackItemId ?? string.Empty, StringComparer.Ordinal))
        {
            if (stackItem is null)
            {
                continue;
            }

            ValidateAuthoritativeStateOptionalObjectReference(
                $"stack item {stackItem.StackItemId} source object",
                stackItem.SourceObjectId,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateObjectReferenceList(
                $"stack item {stackItem.StackItemId} target object",
                stackItem.TargetObjectIds,
                knownObjectIds,
                errors);
        }

        foreach (var trigger in authoritativeState.TriggerQueue
            .OrderBy(item => item?.TriggerId ?? string.Empty, StringComparer.Ordinal))
        {
            if (trigger is null)
            {
                continue;
            }

            ValidateAuthoritativeStateOptionalObjectReference(
                $"trigger queue item {trigger.TriggerId} source object",
                trigger.SourceObjectId,
                knownObjectIds,
                errors);
        }

        if (authoritativeState.PendingHandChoice is not null)
        {
            ValidateAuthoritativeStateOptionalObjectReference(
                $"pending hand choice {authoritativeState.PendingHandChoice.ChoiceId} source object",
                authoritativeState.PendingHandChoice.SourceObjectId,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateObjectReferenceList(
                $"pending hand choice {authoritativeState.PendingHandChoice.ChoiceId} legal object",
                authoritativeState.PendingHandChoice.LegalObjectIds,
                knownObjectIds,
                errors);
        }

        foreach (var resource in authoritativeState.TemporaryPaymentResources
            .OrderBy(item => item?.ResourceId ?? string.Empty, StringComparer.Ordinal))
        {
            if (resource is null)
            {
                continue;
            }

            ValidateAuthoritativeStateOptionalObjectReference(
                $"temporary payment resource {resource.ResourceId} source object",
                resource.SourceObjectId,
                knownObjectIds,
                errors);
        }

        foreach (var resolution in authoritativeState.BattlefieldResolutions
            .OrderBy(item => item?.ResolutionId ?? string.Empty, StringComparer.Ordinal))
        {
            if (resolution is null)
            {
                continue;
            }

            ValidateAuthoritativeStateRequiredObjectReference(
                $"battlefield resolution {resolution.ResolutionId} battlefield object",
                resolution.BattlefieldObjectId,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateOptionalObjectReference(
                $"battlefield resolution {resolution.ResolutionId} source object",
                resolution.SourceObjectId,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateObjectReferenceList(
                $"battlefield resolution {resolution.ResolutionId} participant object",
                resolution.ParticipantObjectIds,
                knownObjectIds,
                errors);
        }

        foreach (var resolution in authoritativeState.BattleResolutions
            .OrderBy(item => item?.ResolutionId ?? string.Empty, StringComparer.Ordinal))
        {
            if (resolution is null)
            {
                continue;
            }

            ValidateAuthoritativeStateRequiredObjectReference(
                $"battle resolution {resolution.ResolutionId} battlefield object",
                resolution.BattlefieldId,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateObjectReferenceList(
                $"battle resolution {resolution.ResolutionId} attacker object",
                resolution.AttackerObjectIds,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateObjectReferenceList(
                $"battle resolution {resolution.ResolutionId} defender object",
                resolution.DefenderObjectIds,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateObjectReferenceList(
                $"battle resolution {resolution.ResolutionId} surviving attacker object",
                resolution.SurvivingAttackerObjectIds,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateObjectReferenceList(
                $"battle resolution {resolution.ResolutionId} surviving defender object",
                resolution.SurvivingDefenderObjectIds,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateObjectReferenceList(
                $"battle resolution {resolution.ResolutionId} destroyed object",
                resolution.DestroyedObjectIds,
                knownObjectIds,
                errors);
        }
    }

    private static IReadOnlySet<string> BuildAuthoritativeStateKnownObjectIds(MatchState authoritativeState)
    {
        var knownObjectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var objectId in authoritativeState.CardObjects.Keys)
        {
            AddKnownObjectId(knownObjectIds, objectId);
        }

        foreach (var cardObject in authoritativeState.CardObjects.Values)
        {
            AddKnownObjectId(knownObjectIds, cardObject.ObjectId);
        }

        foreach (var objectId in authoritativeState.ObjectLocations.Keys)
        {
            AddKnownObjectId(knownObjectIds, objectId);
        }

        return knownObjectIds;
    }

    private static void AddKnownObjectId(HashSet<string> knownObjectIds, string? objectId)
    {
        if (!string.IsNullOrWhiteSpace(objectId))
        {
            knownObjectIds.Add(objectId.Trim());
        }
    }

    private static void ValidateAuthoritativeStateObjectReferenceList(
        string objectLabel,
        IReadOnlyList<string>? objectIds,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors)
    {
        if (objectIds is null)
        {
            errors.Add($"authoritative state {objectLabel} list is required");
            return;
        }

        foreach (var objectId in objectIds)
        {
            ValidateAuthoritativeStateOptionalObjectReference(objectLabel, objectId, knownObjectIds, errors);
        }
    }

    private static void ValidateAuthoritativeStateRequiredObjectReference(
        string objectLabel,
        string? objectId,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            errors.Add($"authoritative state {objectLabel} is required");
            return;
        }

        ValidateAuthoritativeStateOptionalObjectReference(objectLabel, objectId, knownObjectIds, errors);
    }

    private static void ValidateAuthoritativeStateOptionalObjectReference(
        string objectLabel,
        string? objectId,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        var normalizedObjectId = objectId.Trim();
        if (!string.Equals(objectId, normalizedObjectId, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {objectLabel} {normalizedObjectId} has surrounding whitespace");
        }

        if (!knownObjectIds.Contains(normalizedObjectId))
        {
            errors.Add($"authoritative state {objectLabel} {normalizedObjectId} is missing from object registry");
        }
    }

    private static void ValidateAuthoritativeStateRequiredObjectPlayer(
        string playerLabel,
        string? playerId,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            errors.Add($"authoritative state {playerLabel} is required");
            return;
        }

        ValidateAuthoritativeStateObjectPlayer(playerLabel, playerId, seatPlayerIds, errors);
    }

    private static void ValidateAuthoritativeStateOptionalObjectPlayer(
        string playerLabel,
        string? playerId,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (playerId is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(playerId))
        {
            errors.Add($"authoritative state {playerLabel} is blank");
            return;
        }

        ValidateAuthoritativeStateObjectPlayer(playerLabel, playerId, seatPlayerIds, errors);
    }

    private static void ValidateAuthoritativeStateObjectPlayer(
        string playerLabel,
        string playerId,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        var normalizedPlayerId = playerId.Trim();
        if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {playerLabel} {normalizedPlayerId} has surrounding whitespace");
        }

        if (!seatPlayerIds.Contains(normalizedPlayerId))
        {
            errors.Add($"authoritative state {playerLabel} {normalizedPlayerId} is missing from seats");
        }
    }

    private static void ValidateAuthoritativeStateBattlefieldResolutionPlayers(
        IReadOnlyList<BattlefieldResolutionState>? battlefieldResolutions,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (battlefieldResolutions is null)
        {
            errors.Add("authoritative state battlefield resolutions list is required");
            return;
        }

        foreach (var resolution in battlefieldResolutions.OrderBy(
            item => item?.ResolutionId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (resolution is null)
            {
                errors.Add("authoritative state battlefield resolution is required");
                continue;
            }

            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"battlefield resolution {resolution.ResolutionId} player",
                resolution.PlayerId,
                seatPlayerIds,
                errors);
            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"battlefield resolution {resolution.ResolutionId} previous controller player",
                resolution.PreviousControllerId,
                seatPlayerIds,
                errors);
            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"battlefield resolution {resolution.ResolutionId} controller player",
                resolution.ControllerId,
                seatPlayerIds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateBattleResolutionPlayers(
        IReadOnlyList<BattleResolutionState>? battleResolutions,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (battleResolutions is null)
        {
            errors.Add("authoritative state battle resolutions list is required");
            return;
        }

        foreach (var resolution in battleResolutions.OrderBy(
            item => item?.ResolutionId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (resolution is null)
            {
                errors.Add("authoritative state battle resolution is required");
                continue;
            }

            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"battle resolution {resolution.ResolutionId} attacking player",
                resolution.AttackingPlayerId,
                seatPlayerIds,
                errors);
            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"battle resolution {resolution.ResolutionId} defending player",
                resolution.DefendingPlayerId,
                seatPlayerIds,
                errors);
            ValidateAuthoritativeStateOptionalObjectPlayer(
                $"battle resolution {resolution.ResolutionId} winner player",
                resolution.WinnerPlayerId,
                seatPlayerIds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateOptionalPlayerPointer(
        string pointerName,
        string? playerId,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (playerId is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(playerId))
        {
            errors.Add($"authoritative state {pointerName} is blank");
            return;
        }

        var normalizedPlayerId = playerId.Trim();
        if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {pointerName} {normalizedPlayerId} has surrounding whitespace");
        }

        if (!seatPlayerIds.Contains(normalizedPlayerId))
        {
            errors.Add($"authoritative state {pointerName} {normalizedPlayerId} is missing from seats");
        }
    }

    private static void ValidateAuthoritativeStateSeats(
        MatchState authoritativeState,
        List<string> errors)
    {
        var seenSeats = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (playerId, seat) in authoritativeState.Seats.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var normalizedPlayerId = playerId.Trim();
            if (string.IsNullOrWhiteSpace(playerId))
            {
                errors.Add("authoritative state seat player id is required");
            }
            else if (!string.Equals(playerId, normalizedPlayerId, StringComparison.Ordinal))
            {
                errors.Add($"authoritative state seat player {normalizedPlayerId} has surrounding whitespace");
            }

            var playerLabel = string.IsNullOrWhiteSpace(normalizedPlayerId)
                ? "<blank>"
                : normalizedPlayerId;
            if (string.IsNullOrWhiteSpace(seat))
            {
                errors.Add($"authoritative state seat for {playerLabel} is required");
                continue;
            }

            var normalizedSeat = seat.Trim();
            if (!string.Equals(seat, normalizedSeat, StringComparison.Ordinal))
            {
                errors.Add($"authoritative state seat {normalizedSeat} for {playerLabel} has surrounding whitespace");
            }

            if (!IsKnownSeat(normalizedSeat))
            {
                errors.Add($"authoritative state seat {normalizedSeat} for {playerLabel} is invalid");
            }

            if (!seenSeats.Add(normalizedSeat))
            {
                errors.Add($"authoritative state seat {normalizedSeat} is duplicated");
            }
        }
    }

    private static void ValidateSpectatorReplayFrame(
        string roomId,
        long lastEventSequence,
        long? currentTick,
        MatchState? authoritativeState,
        MatchReplayFrame? spectatorReplayFrame,
        List<string> errors)
    {
        if (spectatorReplayFrame is null)
        {
            return;
        }

        if (authoritativeState is null)
        {
            errors.Add("spectator replay frame requires authoritative state");
            return;
        }

        if (!string.Equals(spectatorReplayFrame.RoomId, roomId, StringComparison.Ordinal))
        {
            errors.Add($"spectator replay frame room {spectatorReplayFrame.RoomId} does not match recovery room {roomId}");
        }

        if (spectatorReplayFrame.EventSequence != lastEventSequence)
        {
            errors.Add(
                $"spectator replay frame event sequence {spectatorReplayFrame.EventSequence} does not match recovery sequence {lastEventSequence}");
        }

        if (spectatorReplayFrame.Tick != authoritativeState.Tick)
        {
            errors.Add(
                $"spectator replay frame tick {spectatorReplayFrame.Tick} does not match authoritative state tick {authoritativeState.Tick}");
        }

        if (currentTick is { } expectedTick && spectatorReplayFrame.Tick != expectedTick)
        {
            errors.Add($"spectator replay frame tick {spectatorReplayFrame.Tick} does not match recovery tick {expectedTick}");
        }

        var expectedHash = MatchStateHasher.Hash(authoritativeState);
        if (!string.Equals(spectatorReplayFrame.AuthoritativeStateHash, expectedHash, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame hash does not match authoritative state hash");
        }

        if (spectatorReplayFrame.SpectatorSnapshot is null)
        {
            errors.Add("spectator replay frame snapshot is required");
            return;
        }

        if (spectatorReplayFrame.SpectatorSnapshot.Timing is null)
        {
            errors.Add("spectator replay frame snapshot timing is required");
            return;
        }

        if (spectatorReplayFrame.SpectatorSnapshot.Players is null)
        {
            errors.Add("spectator replay frame snapshot players are required");
        }
        else
        {
            ValidateSpectatorSnapshotPlayerPayloads(
                spectatorReplayFrame.SpectatorSnapshot,
                authoritativeState,
                errors);
        }

        if (spectatorReplayFrame.SpectatorSnapshot.Lanes is null)
        {
            errors.Add("spectator replay frame snapshot lanes are required");
        }
        else
        {
            ValidateSpectatorSnapshotLanePayloads(
                spectatorReplayFrame.SpectatorSnapshot,
                authoritativeState,
                errors);
        }

        if (spectatorReplayFrame.SpectatorSnapshot.Stack is null)
        {
            errors.Add("spectator replay frame snapshot stack is required");
        }
        else
        {
            if (spectatorReplayFrame.SpectatorSnapshot.Stack.Count != authoritativeState.StackItems.Count)
            {
                errors.Add(
                    $"spectator replay frame snapshot stack count {spectatorReplayFrame.SpectatorSnapshot.Stack.Count} does not match authoritative state stack count {authoritativeState.StackItems.Count}");
            }

            var spectatorStackItemIds = ExtractStackItemIds(spectatorReplayFrame.SpectatorSnapshot);
            var authoritativeStackItemIds = authoritativeState.StackItems
                .Select(item => item.StackItemId)
                .ToArray();
            if (!StringListsEqual(spectatorStackItemIds, authoritativeStackItemIds))
            {
                errors.Add("spectator replay frame snapshot stack item ids disagree with authoritative state stack item ids");
            }

            var spectatorStackControllerIds = ExtractStackItemStringValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "controllerId");
            var authoritativeStackControllerIds = authoritativeState.StackItems
                .Select(item => item.ControllerId)
                .ToArray();
            if (!StringListsEqual(spectatorStackControllerIds, authoritativeStackControllerIds))
            {
                errors.Add("spectator replay frame snapshot stack controller ids disagree with authoritative state stack controller ids");
            }

            var spectatorStackSourceObjectIds = ExtractStackItemStringValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "sourceObjectId");
            var authoritativeStackSourceObjectIds = authoritativeState.StackItems
                .Select(item => item.SourceObjectId)
                .ToArray();
            if (!StringListsEqual(spectatorStackSourceObjectIds, authoritativeStackSourceObjectIds))
            {
                errors.Add("spectator replay frame snapshot stack source object ids disagree with authoritative state stack source object ids");
            }

            var spectatorStackEffectKinds = ExtractStackItemStringValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "effectKind");
            var authoritativeStackEffectKinds = authoritativeState.StackItems
                .Select(item => item.EffectKind)
                .ToArray();
            if (!StringListsEqual(spectatorStackEffectKinds, authoritativeStackEffectKinds))
            {
                errors.Add("spectator replay frame snapshot stack effect kinds disagree with authoritative state stack effect kinds");
            }

            var spectatorStackCardNumbers = ExtractStackItemStringValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "cardNo");
            var authoritativeStackCardNumbers = authoritativeState.StackItems
                .Select(item => item.CardNo)
                .ToArray();
            if (!StringListsEqual(spectatorStackCardNumbers, authoritativeStackCardNumbers))
            {
                errors.Add("spectator replay frame snapshot stack card numbers disagree with authoritative state stack card numbers");
            }

            var spectatorStackTargetObjectIds = ExtractStackItemStringListValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "targetObjectIds");
            var authoritativeStackTargetObjectIds = authoritativeState.StackItems
                .Select(item => item.TargetObjectIds)
                .ToArray();
            if (!StringListCollectionsEqual(spectatorStackTargetObjectIds, authoritativeStackTargetObjectIds))
            {
                errors.Add("spectator replay frame snapshot stack target object ids disagree with authoritative state stack target object ids");
            }

            var spectatorStackDamageAmounts = ExtractStackItemIntValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "damageAmount");
            var authoritativeStackDamageAmounts = authoritativeState.StackItems
                .Select(item => item.DamageAmount)
                .ToArray();
            if (!IntListsEqual(spectatorStackDamageAmounts, authoritativeStackDamageAmounts))
            {
                errors.Add("spectator replay frame snapshot stack damage amounts disagree with authoritative state stack damage amounts");
            }

            var spectatorStackDestinations = ExtractStackItemOptionalStringValues(
                spectatorReplayFrame.SpectatorSnapshot,
                "destination");
            var authoritativeStackDestinations = authoritativeState.StackItems
                .Select(item => item.Destination)
                .ToArray();
            if (!StringListsEqual(spectatorStackDestinations, authoritativeStackDestinations))
            {
                errors.Add("spectator replay frame snapshot stack destinations disagree with authoritative state stack destinations");
            }
        }

        if (string.IsNullOrWhiteSpace(spectatorReplayFrame.SpectatorSnapshot.TurnState))
        {
            errors.Add("spectator replay frame snapshot turn state is required");
        }

        if (!string.Equals(
                spectatorReplayFrame.SpectatorSnapshot.TurnState,
                authoritativeState.TimingState,
                StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame snapshot turn state does not match authoritative state timing state");
        }

        if (spectatorReplayFrame.SpectatorSnapshot.Tick != spectatorReplayFrame.Tick)
        {
            errors.Add(
                $"spectator replay frame snapshot tick {spectatorReplayFrame.SpectatorSnapshot.Tick} does not match frame tick {spectatorReplayFrame.Tick}");
        }

        if (spectatorReplayFrame.SpectatorSnapshot.TurnNumber != authoritativeState.TurnNumber)
        {
            errors.Add(
                $"spectator replay frame snapshot turn number {spectatorReplayFrame.SpectatorSnapshot.TurnNumber} does not match authoritative state turn number {authoritativeState.TurnNumber}");
        }

        if (!string.Equals(
                spectatorReplayFrame.SpectatorSnapshot.ActivePlayerId,
                authoritativeState.ActivePlayerId,
                StringComparison.Ordinal))
        {
            errors.Add(
                $"spectator replay frame snapshot active player {spectatorReplayFrame.SpectatorSnapshot.ActivePlayerId} does not match authoritative state active player {authoritativeState.ActivePlayerId}");
        }

        var spectatorSeats = ExtractSeats(spectatorReplayFrame.SpectatorSnapshot);
        if (!SeatsEqual(authoritativeState.Seats, spectatorSeats))
        {
            errors.Add("spectator replay frame snapshot seats disagree with authoritative state seats");
        }

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "phase",
                out var spectatorPhase)
            || !string.Equals(spectatorPhase, authoritativeState.Phase, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing phase does not match authoritative state phase");
        }

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "timingState",
                out var spectatorTimingState)
            || !string.Equals(spectatorTimingState, authoritativeState.TimingState, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing state does not match authoritative state timing state");
        }

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "turnPlayerId",
                out var spectatorTurnPlayerId)
            || !string.Equals(spectatorTurnPlayerId, authoritativeState.TurnPlayerId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing turn player does not match authoritative state turn player");
        }

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "priorityPlayerId",
                out var spectatorPriorityPlayerId)
            || !string.Equals(spectatorPriorityPlayerId, authoritativeState.PriorityPlayerId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing priority player does not match authoritative state priority player");
        }

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "focusPlayerId",
                out var spectatorFocusPlayerId)
            || !string.Equals(spectatorFocusPlayerId, authoritativeState.FocusPlayerId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing focus player does not match authoritative state focus player");
        }

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "winnerPlayerId",
                out var spectatorWinnerPlayerId)
            || !string.Equals(spectatorWinnerPlayerId, authoritativeState.WinnerPlayerId, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing winner player does not match authoritative state winner player");
        }

        if (!TryReadStringList(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "passedPriorityPlayerIds",
                out var spectatorPassedPriorityPlayerIds)
            || !StringListsEqual(spectatorPassedPriorityPlayerIds, authoritativeState.PassedPriorityPlayerIds))
        {
            errors.Add("spectator replay frame timing passed priority players do not match authoritative state passed priority players");
        }

        if (!TryReadStringList(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "passedFocusPlayerIds",
                out var spectatorPassedFocusPlayerIds)
            || !StringListsEqual(spectatorPassedFocusPlayerIds, authoritativeState.PassedFocusPlayerIds))
        {
            errors.Add("spectator replay frame timing passed focus players do not match authoritative state passed focus players");
        }

        if (!TryReadStringList(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "destroyedUnitOwnerIdsThisTurn",
                out var spectatorDestroyedUnitOwnerIdsThisTurn)
            || !StringListsEqual(spectatorDestroyedUnitOwnerIdsThisTurn, authoritativeState.DestroyedUnitOwnerIdsThisTurn))
        {
            errors.Add("spectator replay frame timing destroyed unit owners do not match authoritative state destroyed unit owners");
        }

        if (!TryReadString(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "roomStatus",
                out var spectatorRoomStatus)
            || !string.Equals(spectatorRoomStatus, authoritativeState.Status, StringComparison.Ordinal))
        {
            errors.Add("spectator replay frame timing room status does not match authoritative state room status");
        }

        if (!TryReadStringList(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "readyPlayerIds",
                out var spectatorReadyPlayerIds)
            || !StringListsEqual(spectatorReadyPlayerIds, authoritativeState.ReadyPlayerIds))
        {
            errors.Add("spectator replay frame timing ready players do not match authoritative state ready players");
        }

        if (!TryReadObjectInt(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "winningScore",
                out var spectatorWinningScore)
            || spectatorWinningScore != EffectiveWinningScoreForRecovery(authoritativeState))
        {
            errors.Add("spectator replay frame timing winning score does not match authoritative state winning score");
        }

        ValidateSpectatorPendingTaskQueuePayload(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, errors);
        ValidateSpectatorPendingPaymentPayload(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, errors);
        ValidateSpectatorPendingHandChoicePayload(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, errors);
        ValidateSpectatorTemporaryPaymentResourcePayloads(
            spectatorReplayFrame.SpectatorSnapshot.Timing,
            authoritativeState,
            errors);
        ValidateSpectatorBattlefieldTaskPayloads(spectatorReplayFrame.SpectatorSnapshot.Timing, authoritativeState, errors);

        if (!spectatorReplayFrame.SpectatorSnapshot.Timing.TryGetValue("turnWindow", out var spectatorTurnWindow)
            || !TurnWindowMatches(spectatorTurnWindow, authoritativeState.TurnWindow))
        {
            errors.Add("spectator replay frame timing turn window does not match authoritative state turn window");
        }

        if (!spectatorReplayFrame.SpectatorSnapshot.Timing.TryGetValue("spellDuel", out var spectatorSpellDuel)
            || !SpellDuelMatches(spectatorSpellDuel, authoritativeState.SpellDuelState))
        {
            errors.Add("spectator replay frame timing spell duel does not match authoritative state spell duel");
        }

        if (!spectatorReplayFrame.SpectatorSnapshot.Timing.TryGetValue("battle", out var spectatorBattle)
            || !BattleMatches(spectatorBattle, authoritativeState, authoritativeState.BattleState))
        {
            errors.Add("spectator replay frame timing battle does not match authoritative state battle");
        }

        if (!TryReadObjectList(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "battlefieldResolutions",
                out var spectatorBattlefieldResolutions))
        {
            errors.Add("spectator replay frame timing battlefield resolutions are required");
        }
        else if (spectatorBattlefieldResolutions.Count != authoritativeState.BattlefieldResolutions.Count)
        {
            errors.Add(
                $"spectator replay frame timing battlefield resolution count {spectatorBattlefieldResolutions.Count} does not match authoritative state battlefield resolution count {authoritativeState.BattlefieldResolutions.Count}");
        }
        else
        {
            var spectatorBattlefieldResolutionIds = ExtractObjectStringValues(
                spectatorBattlefieldResolutions,
                "resolutionId");
            var authoritativeBattlefieldResolutionIds = authoritativeState.BattlefieldResolutions
                .Select(resolution => resolution.ResolutionId)
                .ToArray();
            if (!StringListsEqual(spectatorBattlefieldResolutionIds, authoritativeBattlefieldResolutionIds))
            {
                errors.Add("spectator replay frame timing battlefield resolution ids disagree with authoritative state battlefield resolution ids");
            }

            var spectatorBattlefieldResolutionTicks = ExtractObjectLongValues(
                spectatorBattlefieldResolutions,
                "tick");
            var authoritativeBattlefieldResolutionTicks = authoritativeState.BattlefieldResolutions
                .Select(resolution => resolution.Tick)
                .ToArray();
            if (!LongListsEqual(spectatorBattlefieldResolutionTicks, authoritativeBattlefieldResolutionTicks))
            {
                errors.Add("spectator replay frame timing battlefield resolution ticks disagree with authoritative state battlefield resolution ticks");
            }

            var spectatorBattlefieldResolutionKinds = ExtractObjectStringValues(
                spectatorBattlefieldResolutions,
                "kind");
            var authoritativeBattlefieldResolutionKinds = authoritativeState.BattlefieldResolutions
                .Select(resolution => resolution.Kind)
                .ToArray();
            if (!StringListsEqual(spectatorBattlefieldResolutionKinds, authoritativeBattlefieldResolutionKinds))
            {
                errors.Add("spectator replay frame timing battlefield resolution kinds disagree with authoritative state battlefield resolution kinds");
            }

            var spectatorBattlefieldResolutionReasons = ExtractObjectStringValues(
                spectatorBattlefieldResolutions,
                "reason");
            var authoritativeBattlefieldResolutionReasons = authoritativeState.BattlefieldResolutions
                .Select(resolution => resolution.Reason)
                .ToArray();
            if (!StringListsEqual(spectatorBattlefieldResolutionReasons, authoritativeBattlefieldResolutionReasons))
            {
                errors.Add("spectator replay frame timing battlefield resolution reasons disagree with authoritative state battlefield resolution reasons");
            }

            var spectatorBattlefieldResolutionBattlefieldObjectIds = ExtractObjectStringValues(
                spectatorBattlefieldResolutions,
                "battlefieldObjectId");
            var authoritativeBattlefieldResolutionBattlefieldObjectIds = authoritativeState.BattlefieldResolutions
                .Select(resolution => resolution.BattlefieldObjectId)
                .ToArray();
            if (!StringListsEqual(spectatorBattlefieldResolutionBattlefieldObjectIds, authoritativeBattlefieldResolutionBattlefieldObjectIds))
            {
                errors.Add("spectator replay frame timing battlefield resolution battlefield object ids disagree with authoritative state battlefield resolution battlefield object ids");
            }

            var spectatorBattlefieldResolutionPlayerIds = ExtractObjectStringValues(
                spectatorBattlefieldResolutions,
                "playerId");
            var authoritativeBattlefieldResolutionPlayerIds = authoritativeState.BattlefieldResolutions
                .Select(resolution => resolution.PlayerId ?? string.Empty)
                .ToArray();
            if (!StringListsEqual(spectatorBattlefieldResolutionPlayerIds, authoritativeBattlefieldResolutionPlayerIds))
            {
                errors.Add("spectator replay frame timing battlefield resolution player ids disagree with authoritative state battlefield resolution player ids");
            }

            var spectatorBattlefieldResolutionPreviousControllerIds = ExtractObjectStringValues(
                spectatorBattlefieldResolutions,
                "previousControllerId");
            var authoritativeBattlefieldResolutionPreviousControllerIds = authoritativeState.BattlefieldResolutions
                .Select(resolution => resolution.PreviousControllerId ?? string.Empty)
                .ToArray();
            if (!StringListsEqual(spectatorBattlefieldResolutionPreviousControllerIds, authoritativeBattlefieldResolutionPreviousControllerIds))
            {
                errors.Add("spectator replay frame timing battlefield resolution previous controller ids disagree with authoritative state battlefield resolution previous controller ids");
            }

            var spectatorBattlefieldResolutionControllerIds = ExtractObjectStringValues(
                spectatorBattlefieldResolutions,
                "controllerId");
            var authoritativeBattlefieldResolutionControllerIds = authoritativeState.BattlefieldResolutions
                .Select(resolution => resolution.ControllerId ?? string.Empty)
                .ToArray();
            if (!StringListsEqual(spectatorBattlefieldResolutionControllerIds, authoritativeBattlefieldResolutionControllerIds))
            {
                errors.Add("spectator replay frame timing battlefield resolution controller ids disagree with authoritative state battlefield resolution controller ids");
            }

            var spectatorBattlefieldResolutionSourceObjectIds = ExtractObjectStringValues(
                spectatorBattlefieldResolutions,
                "sourceObjectId");
            var authoritativeBattlefieldResolutionSourceObjectIds = authoritativeState.BattlefieldResolutions
                .Select(resolution => resolution.SourceObjectId ?? string.Empty)
                .ToArray();
            if (!StringListsEqual(spectatorBattlefieldResolutionSourceObjectIds, authoritativeBattlefieldResolutionSourceObjectIds))
            {
                errors.Add("spectator replay frame timing battlefield resolution source object ids disagree with authoritative state battlefield resolution source object ids");
            }

            var spectatorBattlefieldResolutionParticipantObjectIds = ExtractObjectStringListValues(
                spectatorBattlefieldResolutions,
                "participantObjectIds");
            var authoritativeBattlefieldResolutionParticipantObjectIds = authoritativeState.BattlefieldResolutions
                .Select(resolution => resolution.ParticipantObjectIds)
                .ToArray();
            if (!StringListCollectionsEqual(spectatorBattlefieldResolutionParticipantObjectIds, authoritativeBattlefieldResolutionParticipantObjectIds))
            {
                errors.Add("spectator replay frame timing battlefield resolution participant object ids disagree with authoritative state battlefield resolution participant object ids");
            }

            var spectatorBattlefieldResolutionRelatedEventKinds = ExtractObjectStringListValues(
                spectatorBattlefieldResolutions,
                "relatedEventKinds");
            var authoritativeBattlefieldResolutionRelatedEventKinds = authoritativeState.BattlefieldResolutions
                .Select(resolution => resolution.RelatedEventKinds)
                .ToArray();
            if (!StringListCollectionsEqual(spectatorBattlefieldResolutionRelatedEventKinds, authoritativeBattlefieldResolutionRelatedEventKinds))
            {
                errors.Add("spectator replay frame timing battlefield resolution related event kinds disagree with authoritative state battlefield resolution related event kinds");
            }
        }

        if (!TryReadObjectList(
                spectatorReplayFrame.SpectatorSnapshot.Timing,
                "battleResolutions",
                out var spectatorBattleResolutions))
        {
            errors.Add("spectator replay frame timing battle resolutions are required");
        }
        else if (spectatorBattleResolutions.Count != authoritativeState.BattleResolutions.Count)
        {
            errors.Add(
                $"spectator replay frame timing battle resolution count {spectatorBattleResolutions.Count} does not match authoritative state battle resolution count {authoritativeState.BattleResolutions.Count}");
        }
        else
        {
            var spectatorBattleResolutionIds = ExtractObjectStringValues(
                spectatorBattleResolutions,
                "resolutionId");
            var authoritativeBattleResolutionIds = authoritativeState.BattleResolutions
                .Select(resolution => resolution.ResolutionId)
                .ToArray();
            if (!StringListsEqual(spectatorBattleResolutionIds, authoritativeBattleResolutionIds))
            {
                errors.Add("spectator replay frame timing battle resolution ids disagree with authoritative state battle resolution ids");
            }

            var spectatorBattleResolutionTicks = ExtractObjectLongValues(
                spectatorBattleResolutions,
                "tick");
            var authoritativeBattleResolutionTicks = authoritativeState.BattleResolutions
                .Select(resolution => resolution.Tick)
                .ToArray();
            if (!LongListsEqual(spectatorBattleResolutionTicks, authoritativeBattleResolutionTicks))
            {
                errors.Add("spectator replay frame timing battle resolution ticks disagree with authoritative state battle resolution ticks");
            }

            var spectatorBattleResolutionKinds = ExtractObjectStringValues(
                spectatorBattleResolutions,
                "kind");
            var authoritativeBattleResolutionKinds = authoritativeState.BattleResolutions
                .Select(resolution => resolution.Kind)
                .ToArray();
            if (!StringListsEqual(spectatorBattleResolutionKinds, authoritativeBattleResolutionKinds))
            {
                errors.Add("spectator replay frame timing battle resolution kinds disagree with authoritative state battle resolution kinds");
            }

            var spectatorBattleResolutionReasons = ExtractObjectStringValues(
                spectatorBattleResolutions,
                "reason");
            var authoritativeBattleResolutionReasons = authoritativeState.BattleResolutions
                .Select(resolution => resolution.Reason)
                .ToArray();
            if (!StringListsEqual(spectatorBattleResolutionReasons, authoritativeBattleResolutionReasons))
            {
                errors.Add("spectator replay frame timing battle resolution reasons disagree with authoritative state battle resolution reasons");
            }

            var spectatorBattleResolutionBattlefieldIds = ExtractObjectStringValues(
                spectatorBattleResolutions,
                "battlefieldId");
            var authoritativeBattleResolutionBattlefieldIds = authoritativeState.BattleResolutions
                .Select(resolution => resolution.BattlefieldId)
                .ToArray();
            if (!StringListsEqual(spectatorBattleResolutionBattlefieldIds, authoritativeBattleResolutionBattlefieldIds))
            {
                errors.Add("spectator replay frame timing battle resolution battlefield ids disagree with authoritative state battle resolution battlefield ids");
            }

            var spectatorBattleResolutionAttackingPlayerIds = ExtractObjectStringValues(
                spectatorBattleResolutions,
                "attackingPlayerId");
            var authoritativeBattleResolutionAttackingPlayerIds = authoritativeState.BattleResolutions
                .Select(resolution => resolution.AttackingPlayerId ?? string.Empty)
                .ToArray();
            if (!StringListsEqual(spectatorBattleResolutionAttackingPlayerIds, authoritativeBattleResolutionAttackingPlayerIds))
            {
                errors.Add("spectator replay frame timing battle resolution attacking player ids disagree with authoritative state battle resolution attacking player ids");
            }

            var spectatorBattleResolutionDefendingPlayerIds = ExtractObjectStringValues(
                spectatorBattleResolutions,
                "defendingPlayerId");
            var authoritativeBattleResolutionDefendingPlayerIds = authoritativeState.BattleResolutions
                .Select(resolution => resolution.DefendingPlayerId ?? string.Empty)
                .ToArray();
            if (!StringListsEqual(spectatorBattleResolutionDefendingPlayerIds, authoritativeBattleResolutionDefendingPlayerIds))
            {
                errors.Add("spectator replay frame timing battle resolution defending player ids disagree with authoritative state battle resolution defending player ids");
            }

            var spectatorBattleResolutionWinnerPlayerIds = ExtractObjectStringValues(
                spectatorBattleResolutions,
                "winnerPlayerId");
            var authoritativeBattleResolutionWinnerPlayerIds = authoritativeState.BattleResolutions
                .Select(resolution => resolution.WinnerPlayerId ?? string.Empty)
                .ToArray();
            if (!StringListsEqual(spectatorBattleResolutionWinnerPlayerIds, authoritativeBattleResolutionWinnerPlayerIds))
            {
                errors.Add("spectator replay frame timing battle resolution winner player ids disagree with authoritative state battle resolution winner player ids");
            }

            var spectatorBattleResolutionAttackerObjectIds = ExtractObjectStringListValues(
                spectatorBattleResolutions,
                "attackerObjectIds");
            var authoritativeBattleResolutionAttackerObjectIds = authoritativeState.BattleResolutions
                .Select(resolution => resolution.AttackerObjectIds)
                .ToArray();
            if (!StringListCollectionsEqual(spectatorBattleResolutionAttackerObjectIds, authoritativeBattleResolutionAttackerObjectIds))
            {
                errors.Add("spectator replay frame timing battle resolution attacker object ids disagree with authoritative state battle resolution attacker object ids");
            }

            var spectatorBattleResolutionDefenderObjectIds = ExtractObjectStringListValues(
                spectatorBattleResolutions,
                "defenderObjectIds");
            var authoritativeBattleResolutionDefenderObjectIds = authoritativeState.BattleResolutions
                .Select(resolution => resolution.DefenderObjectIds)
                .ToArray();
            if (!StringListCollectionsEqual(spectatorBattleResolutionDefenderObjectIds, authoritativeBattleResolutionDefenderObjectIds))
            {
                errors.Add("spectator replay frame timing battle resolution defender object ids disagree with authoritative state battle resolution defender object ids");
            }

            var spectatorBattleResolutionSurvivingAttackerObjectIds = ExtractObjectStringListValues(
                spectatorBattleResolutions,
                "survivingAttackerObjectIds");
            var authoritativeBattleResolutionSurvivingAttackerObjectIds = authoritativeState.BattleResolutions
                .Select(resolution => resolution.SurvivingAttackerObjectIds)
                .ToArray();
            if (!StringListCollectionsEqual(spectatorBattleResolutionSurvivingAttackerObjectIds, authoritativeBattleResolutionSurvivingAttackerObjectIds))
            {
                errors.Add("spectator replay frame timing battle resolution surviving attacker object ids disagree with authoritative state battle resolution surviving attacker object ids");
            }

            var spectatorBattleResolutionSurvivingDefenderObjectIds = ExtractObjectStringListValues(
                spectatorBattleResolutions,
                "survivingDefenderObjectIds");
            var authoritativeBattleResolutionSurvivingDefenderObjectIds = authoritativeState.BattleResolutions
                .Select(resolution => resolution.SurvivingDefenderObjectIds)
                .ToArray();
            if (!StringListCollectionsEqual(spectatorBattleResolutionSurvivingDefenderObjectIds, authoritativeBattleResolutionSurvivingDefenderObjectIds))
            {
                errors.Add("spectator replay frame timing battle resolution surviving defender object ids disagree with authoritative state battle resolution surviving defender object ids");
            }

            var spectatorBattleResolutionDestroyedObjectIds = ExtractObjectStringListValues(
                spectatorBattleResolutions,
                "destroyedObjectIds");
            var authoritativeBattleResolutionDestroyedObjectIds = authoritativeState.BattleResolutions
                .Select(resolution => resolution.DestroyedObjectIds)
                .ToArray();
            if (!StringListCollectionsEqual(spectatorBattleResolutionDestroyedObjectIds, authoritativeBattleResolutionDestroyedObjectIds))
            {
                errors.Add("spectator replay frame timing battle resolution destroyed object ids disagree with authoritative state battle resolution destroyed object ids");
            }

            var spectatorBattleResolutionRelatedEventKinds = ExtractObjectStringListValues(
                spectatorBattleResolutions,
                "relatedEventKinds");
            var authoritativeBattleResolutionRelatedEventKinds = authoritativeState.BattleResolutions
                .Select(resolution => resolution.RelatedEventKinds)
                .ToArray();
            if (!StringListCollectionsEqual(spectatorBattleResolutionRelatedEventKinds, authoritativeBattleResolutionRelatedEventKinds))
            {
                errors.Add("spectator replay frame timing battle resolution related event kinds disagree with authoritative state battle resolution related event kinds");
            }
        }

        if (spectatorReplayFrame.SpectatorSnapshot.Timing.ContainsKey("seed")
            || spectatorReplayFrame.SpectatorSnapshot.Timing.ContainsKey("rngCursor"))
        {
            errors.Add("spectator replay frame timing leaks random state");
        }
    }

    public static IReadOnlyDictionary<string, string> ExtractSeats(SnapshotDto snapshot)
    {
        var seats = new Dictionary<string, string>(StringComparer.Ordinal);
        if (snapshot.Players is null)
        {
            return seats;
        }

        foreach (var (playerId, player) in snapshot.Players)
        {
            if (TryReadSeat(player, out var seat))
            {
                seats[playerId] = seat;
            }
        }

        return seats;
    }

    private static IReadOnlyList<string> ExtractStackItemIds(SnapshotDto snapshot)
    {
        if (snapshot.Stack is null)
        {
            return [];
        }

        var stackItemIds = new List<string>();
        foreach (var item in snapshot.Stack)
        {
            if (TryReadObjectString(item, "stackItemId", out var stackItemId)
                && !string.IsNullOrWhiteSpace(stackItemId))
            {
                stackItemIds.Add(stackItemId);
            }
        }

        return stackItemIds;
    }

    private static IReadOnlyList<string> ExtractStackItemStringValues(SnapshotDto snapshot, string key)
    {
        if (snapshot.Stack is null)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var item in snapshot.Stack)
        {
            if (TryReadObjectString(item, key, out var value))
            {
                values.Add(value ?? string.Empty);
            }
        }

        return values;
    }

    private static IReadOnlyList<string> ExtractStackItemOptionalStringValues(SnapshotDto snapshot, string key)
    {
        if (snapshot.Stack is null)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var item in snapshot.Stack)
        {
            if (TryReadObjectOptionalString(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ExtractStackItemStringListValues(
        SnapshotDto snapshot,
        string key)
    {
        if (snapshot.Stack is null)
        {
            return [];
        }

        var values = new List<IReadOnlyList<string>>();
        foreach (var item in snapshot.Stack)
        {
            if (TryReadObjectStringList(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<int> ExtractStackItemIntValues(SnapshotDto snapshot, string key)
    {
        if (snapshot.Stack is null)
        {
            return [];
        }

        var values = new List<int>();
        foreach (var item in snapshot.Stack)
        {
            if (TryReadObjectInt(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static bool TryReadSeat(object? player, out string seat)
    {
        if (player is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue("seat", out var readOnlySeat)
            && readOnlySeat is string readOnlySeatString)
        {
            seat = readOnlySeatString;
            return true;
        }

        if (player is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue("seat", out var dictionarySeat)
            && dictionarySeat is string dictionarySeatString)
        {
            seat = dictionarySeatString;
            return true;
        }

        if (player is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty("seat", out var jsonSeat)
            && jsonSeat.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(jsonSeat.GetString()))
        {
            seat = jsonSeat.GetString()!;
            return true;
        }

        seat = string.Empty;
        return false;
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out string? text)
    {
        if (!values.TryGetValue(key, out var value))
        {
            text = null;
            return false;
        }

        return TryReadStringValue(value, out text);
    }

    private static bool TryReadObjectList(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out IReadOnlyList<object?> items)
    {
        items = [];
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        return TryReadObjectListValue(value, out items);
    }

    private static bool TryReadObjectList(
        object? value,
        string key,
        out IReadOnlyList<object?> items)
    {
        items = [];
        return TryReadObjectValue(value, key, out var nested)
            && nested is not null
            && TryReadObjectListValue(nested, out items);
    }

    private static bool TryReadObjectListValue(object? value, out IReadOnlyList<object?> items)
    {
        items = [];
        if (value is IReadOnlyList<object?> readOnlyList)
        {
            items = readOnlyList;
            return true;
        }

        if (value is IEnumerable<object?> enumerable)
        {
            items = enumerable.ToArray();
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            items = jsonArray.EnumerateArray().Select(item => (object?)item).ToArray();
            return true;
        }

        return false;
    }

    private static bool TryReadObjectDictionary(
        object? value,
        string key,
        out IReadOnlyDictionary<string, object?> objects)
    {
        objects = new Dictionary<string, object?>(StringComparer.Ordinal);
        return TryReadObjectValue(value, key, out var nested)
            && TryReadObjectDictionaryValue(nested, out objects);
    }

    private static bool TryReadObjectDictionaryValue(
        object? value,
        out IReadOnlyDictionary<string, object?> objects)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            objects = readOnlyDictionary;
            return true;
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            objects = new Dictionary<string, object?>(dictionary, StringComparer.Ordinal);
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json)
        {
            objects = json.EnumerateObject()
                .ToDictionary(property => property.Name, property => (object?)property.Value, StringComparer.Ordinal);
            return true;
        }

        objects = new Dictionary<string, object?>(StringComparer.Ordinal);
        return false;
    }

    private static IReadOnlyList<string> ExtractObjectStringValues(
        IReadOnlyList<object?> items,
        string key)
    {
        var values = new List<string>();
        foreach (var item in items)
        {
            if (TryReadObjectString(item, key, out var value))
            {
                values.Add(value ?? string.Empty);
            }
        }

        return values;
    }

    private static IReadOnlyList<string> ExtractObjectOptionalStringValues(
        IReadOnlyList<object?> items,
        string key)
    {
        var values = new List<string>();
        foreach (var item in items)
        {
            if (TryReadObjectOptionalString(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<long> ExtractObjectLongValues(
        IReadOnlyList<object?> items,
        string key)
    {
        var values = new List<long>();
        foreach (var item in items)
        {
            if (TryReadObjectLong(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ExtractObjectStringListValues(
        IReadOnlyList<object?> items,
        string key)
    {
        var values = new List<IReadOnlyList<string>>();
        foreach (var item in items)
        {
            if (TryReadObjectStringList(item, key, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static bool TryReadObjectString(object? value, string key, out string? text)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadStringValue(readOnlyValue, out text);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadStringValue(dictionaryValue, out text);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadStringValue(jsonValue, out text);
        }

        text = null;
        return false;
    }

    private static bool TryReadObjectOptionalString(object? value, string key, out string text)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            if (!readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
            {
                text = string.Empty;
                return true;
            }

            return TryReadOptionalStringValue(readOnlyValue, out text);
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            if (!dictionary.TryGetValue(key, out var dictionaryValue))
            {
                text = string.Empty;
                return true;
            }

            return TryReadOptionalStringValue(dictionaryValue, out text);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json)
        {
            if (!json.TryGetProperty(key, out var jsonValue))
            {
                text = string.Empty;
                return true;
            }

            return TryReadOptionalStringValue(jsonValue, out text);
        }

        text = string.Empty;
        return false;
    }

    private static bool TryReadObjectStringList(object? value, string key, out IReadOnlyList<string> texts)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadStringListValue(readOnlyValue, out texts);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadStringListValue(dictionaryValue, out texts);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadStringListValue(jsonValue, out texts);
        }

        texts = [];
        return false;
    }

    private static bool TryReadObjectInt(object? value, string key, out int number)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadIntValue(readOnlyValue, out number);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadIntValue(dictionaryValue, out number);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadIntValue(jsonValue, out number);
        }

        number = 0;
        return false;
    }

    private static bool TryReadObjectLong(object? value, string key, out long number)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadLongValue(readOnlyValue, out number);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadLongValue(dictionaryValue, out number);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadLongValue(jsonValue, out number);
        }

        number = 0;
        return false;
    }

    private static bool TurnWindowMatches(object? value, TurnWindowState expected)
    {
        return TryReadObjectString(value, "state", out var state)
            && string.Equals(state, expected.State, StringComparison.Ordinal)
            && TryReadObjectBool(value, "isSpellDuel", out var isSpellDuel)
            && isSpellDuel == expected.IsSpellDuel
            && TryReadObjectBool(value, "isClosed", out var isClosed)
            && isClosed == expected.IsClosed
            && TryReadObjectBool(value, "hasStack", out var hasStack)
            && hasStack == expected.HasStack
            && TryReadObjectString(value, "actingPlayerId", out var actingPlayerId)
            && string.Equals(actingPlayerId, expected.ActingPlayerId, StringComparison.Ordinal);
    }

    private static bool SpellDuelMatches(object? value, SpellDuelState expected)
    {
        return TryReadObjectBool(value, "isActive", out var isActive)
            && isActive == expected.IsActive
            && TryReadObjectBool(value, "isClosed", out var isClosed)
            && isClosed == expected.IsClosed
            && TryReadObjectString(value, "spellDuelId", out var spellDuelId)
            && string.Equals(spellDuelId, expected.SpellDuelId, StringComparison.Ordinal)
            && TryReadObjectString(value, "battlefieldObjectId", out var battlefieldObjectId)
            && string.Equals(battlefieldObjectId, expected.BattlefieldObjectId, StringComparison.Ordinal)
            && TryReadObjectString(value, "focusPlayerId", out var focusPlayerId)
            && string.Equals(focusPlayerId, expected.FocusPlayerId, StringComparison.Ordinal)
            && TryReadObjectStringList(value, "passedFocusPlayerIds", out var passedFocusPlayerIds)
            && StringListsEqual(passedFocusPlayerIds, expected.PassedFocusPlayerIds)
            && TryReadObjectStringList(value, "stackItemIds", out var stackItemIds)
            && StringListsEqual(stackItemIds, expected.StackItemIds)
            && TryReadObjectStringList(value, "stackControllerIds", out var stackControllerIds)
            && StringListsEqual(stackControllerIds, expected.StackControllerIds);
    }

    private static bool BattleMatches(object? value, MatchState state, BattleState expected)
    {
        return TryReadObjectBool(value, "isActive", out var isActive)
            && isActive == expected.IsActive
            && TryReadObjectString(value, "battleId", out var battleId)
            && string.Equals(battleId, expected.BattleId, StringComparison.Ordinal)
            && TryReadObjectString(value, "battlefieldObjectId", out var battlefieldObjectId)
            && string.Equals(battlefieldObjectId, expected.BattlefieldObjectId, StringComparison.Ordinal)
            && TryReadObjectStringList(value, "attackerObjectIds", out var attackerObjectIds)
            && StringListsEqual(attackerObjectIds, expected.AttackerObjectIds)
            && TryReadObjectStringList(value, "defenderObjectIds", out var defenderObjectIds)
            && StringListsEqual(defenderObjectIds, expected.DefenderObjectIds)
            && TryReadObjectStringDictionary(value, "participantControllerIds", out var participantControllerIds)
            && StringDictionariesEqual(participantControllerIds, expected.ParticipantControllerIds)
            && TryReadObjectValue(value, "damageAssignment", out var damageAssignment)
            && BattleDamageAssignmentMatches(damageAssignment, state);
    }

    private static bool BattleDamageAssignmentMatches(object? value, MatchState state)
    {
        var expectedPending = ResolutionResult.HasOpenBattleDamageAssignmentWindow(state);
        if (!TryReadObjectBool(value, "isPending", out var isPending)
            || isPending != expectedPending)
        {
            return false;
        }

        if (!expectedPending)
        {
            return true;
        }

        return TryReadObjectString(value, "phase", out var phase)
            && string.Equals(phase, "DAMAGE_ASSIGNMENT", StringComparison.Ordinal)
            && TryReadObjectString(value, "battleId", out var battleId)
            && string.Equals(battleId, state.BattleState.BattleId, StringComparison.Ordinal)
            && TryReadObjectString(value, "battlefieldId", out var battlefieldId)
            && string.Equals(battlefieldId, state.BattleState.BattlefieldObjectId, StringComparison.Ordinal)
            && TryReadObjectString(value, "assigningPlayerId", out var assigningPlayerId)
            && string.Equals(assigningPlayerId, ResolutionResult.BattleDamageAssigningPlayerId(state), StringComparison.Ordinal)
            && TryReadObjectIntDictionary(value, "damagePool", out var damagePool)
            && IntDictionariesEqual(
                damagePool,
                ResolutionResult.BattleDamagePoolFor(state, state.BattleState))
            && TryReadObjectStringListDictionary(value, "legalTargets", out var legalTargets)
            && StringListDictionariesEqual(
                legalTargets,
                ResolutionResult.BattleDamageLegalTargetsFor(state.BattleState))
            && TryReadObjectIntDictionary(value, "existingDamage", out var existingDamage)
            && IntDictionariesEqual(
                existingDamage,
                ResolutionResult.BattleExistingDamageFor(state, state.BattleState))
            && TryReadObjectIntDictionary(value, "lethalDamageThreshold", out var lethalDamageThreshold)
            && IntDictionariesEqual(
                lethalDamageThreshold,
                ResolutionResult.BattleLethalDamageThresholdFor(state, state.BattleState))
            && TryReadObjectRequiredAssignments(value, "requiredAssignments", out var requiredAssignments)
            && RequiredAssignmentsEqual(
                requiredAssignments,
                ResolutionResult.BattleRequiredAssignmentsFor(state, state.BattleState));
    }

    private static bool TryReadObjectBool(object? value, string key, out bool flag)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadBoolValue(readOnlyValue, out flag);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadBoolValue(dictionaryValue, out flag);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadBoolValue(jsonValue, out flag);
        }

        flag = false;
        return false;
    }

    private static bool TryReadObjectValue(object? value, string key, out object? nested)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            nested = readOnlyValue;
            return true;
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            nested = dictionaryValue;
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            nested = jsonValue;
            return true;
        }

        nested = null;
        return false;
    }

    private static bool TryReadObjectStringDictionary(
        object? value,
        string key,
        out IReadOnlyDictionary<string, string> texts)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadStringDictionaryValue(readOnlyValue, out texts);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadStringDictionaryValue(dictionaryValue, out texts);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadStringDictionaryValue(jsonValue, out texts);
        }

        texts = new Dictionary<string, string>(StringComparer.Ordinal);
        return false;
    }

    private static bool TryReadObjectIntDictionary(
        object? value,
        string key,
        out IReadOnlyDictionary<string, int> numbers)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadIntDictionaryValue(readOnlyValue, out numbers);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadIntDictionaryValue(dictionaryValue, out numbers);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadIntDictionaryValue(jsonValue, out numbers);
        }

        numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        return false;
    }

    private static bool TryReadObjectStringListDictionary(
        object? value,
        string key,
        out IReadOnlyDictionary<string, IReadOnlyList<string>> texts)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadStringListDictionaryValue(readOnlyValue, out texts);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadStringListDictionaryValue(dictionaryValue, out texts);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadStringListDictionaryValue(jsonValue, out texts);
        }

        texts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        return false;
    }

    private static bool TryReadObjectRequiredAssignments(
        object? value,
        string key,
        out IReadOnlyList<BattleRequiredAssignmentView> assignments)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary
            && readOnlyDictionary.TryGetValue(key, out var readOnlyValue))
        {
            return TryReadRequiredAssignmentsValue(readOnlyValue, out assignments);
        }

        if (value is IDictionary<string, object?> dictionary
            && dictionary.TryGetValue(key, out var dictionaryValue))
        {
            return TryReadRequiredAssignmentsValue(dictionaryValue, out assignments);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } json
            && json.TryGetProperty(key, out var jsonValue))
        {
            return TryReadRequiredAssignmentsValue(jsonValue, out assignments);
        }

        assignments = [];
        return false;
    }

    private static bool TryReadStringValue(object? value, out string? text)
    {
        text = null;
        if (value is null)
        {
            return true;
        }

        if (value is string stringValue)
        {
            text = stringValue;
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.String } jsonString)
        {
            text = jsonString.GetString();
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Null })
        {
            return true;
        }

        return false;
    }

    private static bool TryReadBoolValue(object? value, out bool flag)
    {
        switch (value)
        {
            case bool boolValue:
                flag = boolValue;
                return true;
            case JsonElement { ValueKind: JsonValueKind.True }:
                flag = true;
                return true;
            case JsonElement { ValueKind: JsonValueKind.False }:
                flag = false;
                return true;
            default:
                flag = false;
                return false;
        }
    }

    private static bool TryReadStringDictionaryValue(
        object? value,
        out IReadOnlyDictionary<string, string> texts)
    {
        texts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (value is null)
        {
            return false;
        }

        if (value is IReadOnlyDictionary<string, string> stringDictionary)
        {
            texts = stringDictionary;
            return true;
        }

        if (value is IEnumerable<KeyValuePair<string, string>> stringPairs)
        {
            texts = stringPairs.ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.Ordinal);
            return true;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return TryConvertObjectDictionary(readOnlyDictionary, out texts);
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            return TryConvertObjectDictionary(dictionary, out texts);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } jsonObject)
        {
            var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in jsonObject.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                parsed[property.Name] = property.Value.GetString()!;
            }

            texts = parsed;
            return true;
        }

        return false;
    }

    private static bool TryConvertObjectDictionary(
        IEnumerable<KeyValuePair<string, object?>> values,
        out IReadOnlyDictionary<string, string> texts)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (value is not string text)
            {
                texts = new Dictionary<string, string>(StringComparer.Ordinal);
                return false;
            }

            parsed[key] = text;
        }

        texts = parsed;
        return true;
    }

    private static bool TryReadIntDictionaryValue(
        object? value,
        out IReadOnlyDictionary<string, int> numbers)
    {
        numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        if (value is null)
        {
            return false;
        }

        if (value is IReadOnlyDictionary<string, int> intDictionary)
        {
            numbers = intDictionary;
            return true;
        }

        if (value is IEnumerable<KeyValuePair<string, int>> intPairs)
        {
            numbers = intPairs.ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.Ordinal);
            return true;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return TryConvertObjectIntDictionary(readOnlyDictionary, out numbers);
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            return TryConvertObjectIntDictionary(dictionary, out numbers);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } jsonObject)
        {
            var parsed = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var property in jsonObject.EnumerateObject())
            {
                if (!TryReadIntValue(property.Value, out var number))
                {
                    return false;
                }

                parsed[property.Name] = number;
            }

            numbers = parsed;
            return true;
        }

        return false;
    }

    private static bool TryConvertObjectIntDictionary(
        IEnumerable<KeyValuePair<string, object?>> values,
        out IReadOnlyDictionary<string, int> numbers)
    {
        var parsed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!TryReadIntValue(value, out var number))
            {
                numbers = new Dictionary<string, int>(StringComparer.Ordinal);
                return false;
            }

            parsed[key] = number;
        }

        numbers = parsed;
        return true;
    }

    private static bool TryReadStringListDictionaryValue(
        object? value,
        out IReadOnlyDictionary<string, IReadOnlyList<string>> texts)
    {
        texts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (value is null)
        {
            return false;
        }

        if (value is IReadOnlyDictionary<string, IReadOnlyList<string>> stringListDictionary)
        {
            texts = stringListDictionary;
            return true;
        }

        if (value is IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> stringListPairs)
        {
            texts = stringListPairs.ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.Ordinal);
            return true;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return TryConvertObjectStringListDictionary(readOnlyDictionary, out texts);
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            return TryConvertObjectStringListDictionary(dictionary, out texts);
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Object } jsonObject)
        {
            var parsed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var property in jsonObject.EnumerateObject())
            {
                if (!TryReadStringListValue(property.Value, out var values))
                {
                    return false;
                }

                parsed[property.Name] = values;
            }

            texts = parsed;
            return true;
        }

        return false;
    }

    private static bool TryConvertObjectStringListDictionary(
        IEnumerable<KeyValuePair<string, object?>> values,
        out IReadOnlyDictionary<string, IReadOnlyList<string>> texts)
    {
        var parsed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!TryReadStringListValue(value, out var strings))
            {
                texts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
                return false;
            }

            parsed[key] = strings;
        }

        texts = parsed;
        return true;
    }

    private static bool TryReadRequiredAssignmentsValue(
        object? value,
        out IReadOnlyList<BattleRequiredAssignmentView> assignments)
    {
        assignments = [];
        if (value is null)
        {
            return false;
        }

        if (value is IReadOnlyList<BattleRequiredAssignmentView> assignmentList)
        {
            assignments = assignmentList;
            return true;
        }

        if (value is IEnumerable<BattleRequiredAssignmentView> assignmentEnumerable)
        {
            assignments = assignmentEnumerable.ToArray();
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            var parsed = new List<BattleRequiredAssignmentView>();
            foreach (var item in jsonArray.EnumerateArray())
            {
                if (!TryReadRequiredAssignmentValue(item, out var assignment))
                {
                    return false;
                }

                parsed.Add(assignment);
            }

            assignments = parsed;
            return true;
        }

        if (value is IEnumerable<object?> objectValues)
        {
            return TryConvertRequiredAssignmentObjects(objectValues, out assignments);
        }

        return false;
    }

    private static bool TryConvertRequiredAssignmentObjects(
        IEnumerable<object?> values,
        out IReadOnlyList<BattleRequiredAssignmentView> assignments)
    {
        var parsed = new List<BattleRequiredAssignmentView>();
        foreach (var value in values)
        {
            if (!TryReadRequiredAssignmentValue(value, out var assignment))
            {
                assignments = [];
                return false;
            }

            parsed.Add(assignment);
        }

        assignments = parsed;
        return true;
    }

    private static bool TryReadRequiredAssignmentValue(
        object? value,
        out BattleRequiredAssignmentView assignment)
    {
        assignment = new BattleRequiredAssignmentView(string.Empty, 0, []);
        if (!TryReadObjectString(value, "sourceObjectId", out var sourceObjectId)
            || sourceObjectId is null
            || !TryReadObjectInt(value, "damage", out var damage)
            || !TryReadObjectStringList(value, "legalTargetObjectIds", out var legalTargetObjectIds))
        {
            return false;
        }

        assignment = new BattleRequiredAssignmentView(sourceObjectId, damage, legalTargetObjectIds);
        return true;
    }

    private static bool TryReadOptionalStringValue(object? value, out string text)
    {
        if (TryReadStringValue(value, out var maybeText))
        {
            text = maybeText ?? string.Empty;
            return true;
        }

        text = string.Empty;
        return false;
    }

    private static bool TryReadIntValue(object? value, out int number)
    {
        switch (value)
        {
            case int intValue:
                number = intValue;
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                number = (int)longValue;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } jsonNumber
                when jsonNumber.TryGetInt32(out var jsonInt):
                number = jsonInt;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static bool TryReadLongValue(object? value, out long number)
    {
        switch (value)
        {
            case int intValue:
                number = intValue;
                return true;
            case long longValue:
                number = longValue;
                return true;
            case JsonElement { ValueKind: JsonValueKind.Number } jsonNumber
                when jsonNumber.TryGetInt64(out var jsonLong):
                number = jsonLong;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static bool TryReadStringListValue(object? value, out IReadOnlyList<string> texts)
    {
        texts = [];
        if (value is null)
        {
            return false;
        }

        if (value is IReadOnlyList<string> stringList)
        {
            texts = stringList;
            return true;
        }

        if (value is IEnumerable<string> stringEnumerable)
        {
            texts = stringEnumerable.ToArray();
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            var parsed = new List<string>();
            foreach (var item in jsonArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                parsed.Add(item.GetString()!);
            }

            texts = parsed;
            return true;
        }

        return false;
    }

    private static bool TryReadStringList(
        IReadOnlyDictionary<string, object?> values,
        string key,
        out IReadOnlyList<string> texts)
    {
        texts = [];
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        if (value is IReadOnlyList<string> stringList)
        {
            texts = stringList;
            return true;
        }

        if (value is IEnumerable<string> stringEnumerable)
        {
            texts = stringEnumerable.ToArray();
            return true;
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            var parsed = new List<string>();
            foreach (var item in jsonArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                parsed.Add(item.GetString()!);
            }

            texts = parsed;
            return true;
        }

        return false;
    }

    private static bool StringListsEqual(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        return left.Count == right.Count
            && left.SequenceEqual(right, StringComparer.Ordinal);
    }

    private static bool IntListsEqual(
        IReadOnlyList<int> left,
        IReadOnlyList<int> right)
    {
        return left.Count == right.Count
            && left.SequenceEqual(right);
    }

    private static bool LongListsEqual(
        IReadOnlyList<long> left,
        IReadOnlyList<long> right)
    {
        return left.Count == right.Count
            && left.SequenceEqual(right);
    }

    private static bool StringListCollectionsEqual(
        IReadOnlyList<IReadOnlyList<string>> left,
        IReadOnlyList<IReadOnlyList<string>> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!StringListsEqual(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool StringDictionariesEqual(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        return left.Count == right.Count
            && left.All(entry =>
                right.TryGetValue(entry.Key, out var value)
                && string.Equals(value, entry.Value, StringComparison.Ordinal));
    }

    private static bool IntDictionariesEqual(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right)
    {
        return left.Count == right.Count
            && left.All(entry =>
                right.TryGetValue(entry.Key, out var value)
                && value == entry.Value);
    }

    private static bool StringListDictionariesEqual(
        IReadOnlyDictionary<string, IReadOnlyList<string>> left,
        IReadOnlyDictionary<string, IReadOnlyList<string>> right)
    {
        return left.Count == right.Count
            && left.All(entry =>
                right.TryGetValue(entry.Key, out var values)
                && StringListsEqual(entry.Value, values));
    }

    private static bool RequiredAssignmentsEqual(
        IReadOnlyList<BattleRequiredAssignmentView> left,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> right)
    {
        return TryConvertRequiredAssignmentObjects(right, out var parsedRight)
            && RequiredAssignmentsEqual(left, parsedRight);
    }

    private static bool RequiredAssignmentsEqual(
        IReadOnlyList<BattleRequiredAssignmentView> left,
        IReadOnlyList<BattleRequiredAssignmentView> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!RequiredAssignmentEqual(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool RequiredAssignmentEqual(
        BattleRequiredAssignmentView left,
        BattleRequiredAssignmentView right)
    {
        return string.Equals(left.SourceObjectId, right.SourceObjectId, StringComparison.Ordinal)
            && left.Damage == right.Damage
            && StringListsEqual(left.LegalTargetObjectIds, right.LegalTargetObjectIds);
    }

    private static bool SeatsEqual(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        return left.Count == right.Count
            && left.All(entry =>
                right.TryGetValue(entry.Key, out var seat)
                && string.Equals(seat, entry.Value, StringComparison.Ordinal));
    }
}
