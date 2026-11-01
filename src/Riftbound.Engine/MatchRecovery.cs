using System.Globalization;
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
    string? ErrorMessage)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TypedCommandInput? TypedCommand { get; init; }

    [System.Text.Json.Serialization.JsonRequired]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
    public string? ErrorCode { get; init; }

    [System.Text.Json.Serialization.JsonRequired]
    public RuleInputIdentity? RuleInputs { get; init; } = RuleInputIdentity.Current;
}

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

internal sealed record RuleQueueCoverageRecoveryRow(
    string Key,
    int LiveCount,
    IReadOnlyList<string> EvidenceKeys);

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
    [System.Text.Json.Serialization.JsonRequired]
    public long OpeningSeed { get; init; }

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
    private const string DevSeedScenarioCommandType = "DEV_SEED_SCENARIO";
    private static readonly JsonSerializerOptions JournalPayloadJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async ValueTask<IReadOnlyList<string>> ValidateRecoveryFrameAsync(
        MatchRecoveryFrame recovery,
        IRuleEngine ruleEngine,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        ArgumentNullException.ThrowIfNull(ruleEngine);

        var inputErrors = new List<string>();
        if (recovery.AuthoritativeState is { } final && !RuleInputIdentity.IsCurrentState(final))
            inputErrors.Add("action-log final rule input identity is missing or differs from running inputs");
        if (recovery.ReplayInitialState is { } initial && !RuleInputIdentity.IsCurrentState(initial))
            inputErrors.Add("action-log initial rule input identity is missing or differs from running inputs");
        if (recovery.Commands.Any(command => !RuleInputIdentity.IsCurrent(command.RuleInputs)))
            inputErrors.Add("action-log command rule input identity is missing or differs from running inputs");
        if (inputErrors.Count > 0) return inputErrors;

        var deathErrors = DravenBattleDeathRecoveryRules.ValidateOriginalTriggers(recovery);
        if (deathErrors.Count > 0) return deathErrors;
        var battleResultErrors = BattleResultRecoveryRules.ValidateOriginalTriggers(recovery);
        if (battleResultErrors.Count > 0) return battleResultErrors;
        var startErrors = BattlefieldStartRecoveryRules.ValidateOriginalTriggers(recovery);
        if (startErrors.Count > 0) return startErrors;

        if (recovery.Commands.Count == 0)
        {
            return recovery.AuthoritativeState is { } waiting && waiting.Seed != recovery.OpeningSeed
                ? ["waiting-room state seed differs from recorded opening seed"] : [];
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

            var recordedRoomSeats = recovery.Commands[0].Accepted && recovery.Commands[0].CommandType == MatchSession.RoomSeatCommandType;
            if (replayInitialState.Seats.Count == 0 && !recordedRoomSeats)
            {
                errors.Add("action-log replay initial state seats are required");
            }

            if (recordedRoomSeats && replayInitialState.Seats.Count != 0)
                errors.Add("recorded room seat journal requires empty replay initial seats");

            ValidateReplayInitialSeats(replayInitialState.Seats, errors);

            if (recovery.AuthoritativeState is { } authoritativeState
                && !recordedRoomSeats
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
            ValidateNullBaseline(
                "pending payment",
                replayInitialState.PendingPayment,
                errors);
            ValidateNullBaseline(
                "pending hand choice",
                replayInitialState.PendingHandChoice,
                errors);
            ValidateNullBaseline(
                "pending card choice",
                replayInitialState.PendingCardChoice,
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

            var expectedInitialStatus = recordedRoomSeats ? MatchStatuses.Empty : MatchStatuses.Seating;
            if (!string.Equals(replayInitialState.Status, expectedInitialStatus, StringComparison.Ordinal))
            {
                errors.Add(
                    $"action-log replay initial state status {replayInitialState.Status} must be {expectedInitialStatus}");
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

            if (replayInitialState.Seed != recovery.OpeningSeed)
            {
                errors.Add("action-log replay initial state seed differs from recorded opening seed");
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
        if (!RuleInputIdentity.IsCurrentState(initialState) || !RuleInputIdentity.IsCurrentState(expectedFinalState)
            || commands.Any(command => !RuleInputIdentity.IsCurrent(command.RuleInputs)))
            return new MatchActionLogReplayResult(false, MatchStateHasher.Hash(initialState),
                MatchStateHasher.Hash(expectedFinalState), ["action-log rule input identity is missing or differs from running inputs"]);
        var session = new MatchSession(initialState, ruleEngine, NoopMatchJournal.Instance);
        var replayedState = initialState;
        foreach (var command in commands
            .OrderBy(command => command.StartedEventSequence)
            .ThenBy(command => command.CompletedEventSequence)
            .ThenBy(command => command.ClientIntentId, StringComparer.Ordinal))
        {
            if (TryReadRawCommandType(command.RawCommand, out var rawCommandType)
                && !string.Equals(
                    rawCommandType,
                    ExpectedRawCommandType(command.CommandType.Trim()),
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} raw cmdType {rawCommandType} does not match recovered command type {command.CommandType}");
                continue;
            }

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

            if (!string.Equals(result.ErrorCode, command.ErrorCode, StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} error code {FormatReplayError(result.ErrorCode)} but recovered error code {FormatReplayError(command.ErrorCode)}");
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

            var replayedPayloadHash = ReplayPayloadHash(replayedEvents[index]);
            var recoveredPayloadHash = ReplayPayloadHash(recoveredEvents[index].Event);
            if (!string.Equals(replayedPayloadHash, recoveredPayloadHash, StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} replayed event {index + 1} payload hash {replayedPayloadHash} but recovered event sequence {recoveredEvents[index].Sequence} payload hash {recoveredPayloadHash}");
            }
        }
    }

    private static string ReplayPayloadHash(GameEvent gameEvent)
    {
        // Clearing all pools is simultaneous. Old journals inherited dictionary order,
        // which JSONB restoration may change; no other event array is order-insensitive.
        if (gameEvent.Kind == "RUNE_POOL_CLEARED" && gameEvent.Payload is { } payload
            && payload.TryGetValue("playerIds", out var value))
        {
            var ids = JsonSerializer.SerializeToElement(value);
            if (ids.ValueKind == JsonValueKind.Array && ids.EnumerateArray().All(id => id.ValueKind == JsonValueKind.String))
            {
                var normalized = payload.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                normalized["playerIds"] = ids.EnumerateArray().Select(id => id.GetString()!).Order(StringComparer.Ordinal).ToArray();
                return MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(normalized, JournalPayloadJson));
            }
        }
        // Compare the persisted journal representation of nested CLR records.
        // Recovered JSON keys and array order remain exact; state hashing is unchanged.
        return MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(gameEvent.Payload, JournalPayloadJson));
    }

    private static async ValueTask<ResolutionResult> ReplayCommandAsync(
        MatchSession session,
        RecoveredCommand recovered,
        CancellationToken cancellationToken)
    {
        if (recovered.TypedCommand is { } input
            && (recovered.RawCommand is not null || !input.MatchesRecordedType(recovered.CommandType)))
            throw new MatchSessionException(ErrorCodes.RecoveryInconsistent, "invalid typed command input");

        if (recovered.Accepted && recovered.CommandType == MatchSession.RoomSeatCommandType)
            return await session.ReplayRoomSeatAsync(recovered, cancellationToken).ConfigureAwait(false);

        if (recovered.CommandType.StartsWith(DevSeedScenarioPrefix, StringComparison.Ordinal))
        {
            return await session.SeedScenarioAsync(
                    recovered.PlayerId,
                    recovered.ClientIntentId,
                    recovered.TypedCommand is { } scenarioInput
                        ? scenarioInput.TryScenario(out var scenario) ? scenario
                            : throw new MatchSessionException(ErrorCodes.RecoveryInconsistent, "invalid typed scenario input")
                        : recovered.CommandType[DevSeedScenarioPrefix.Length..],
                    recovered.RawCommand,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var rawCommand = recovered.RawCommand;
        var command = rawCommand is { } raw ? GameCommandJsonMapper.Map(raw)
            : (recovered.TypedCommand ?? TypedCommandInput.Parameterless(recovered.CommandType))?.Restore()
                ?? throw new MatchSessionException(ErrorCodes.RecoveryInconsistent, "command execution input is missing");
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

public static partial class MatchRecoveryValidator
{
    private const string DevSeedScenarioPrefix = "DEV_SEED_SCENARIO:";
    private const string DevSeedScenarioCommandType = "DEV_SEED_SCENARIO";
    private const int BaseWinningScore = 8;
    private const int MaxRetainedResolutionHistoryItems = 12;
    private const string BlueSentinelDelayedTriggerIdPrefixForRecovery = "BLUE_SENTINEL_HELD_DELAYED_RESOURCE";
    private const string JhinMovementResourceTriggerIdPrefixForRecovery = "JHIN_MOVE_RESOURCE";
    private const string KogmawLastBreathAoeEffectKindForRecovery = "OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT";
    private const string KogmawLastBreathAoeSourceTriggerSpecLabelForRecovery =
        "BehaviorSpec unit last-breath source-battlefield AoE damage trigger";
    private const string KogmawTriggerBattlefieldMarkerForRecovery = "::BATTLEFIELD::";
    private const string OgsLuxHighCostSpellPowerEffectKindForRecovery = "OGS_LUX_HIGH_COST_SPELL_POWER_PLUS_3";
    private const string OgsLuxHighCostSpellSourceTriggerSpecLabelForRecovery =
        "BehaviorSpec unit high-cost spell power modifier trigger";
    private const string StandardTriggerIdPrefixForRecovery = "TRIGGER-";
    private const string TeemoOnPlaySelfPowerEffectKindForRecovery = "TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3";
    private const string TeemoAltAOnPlaySelfPowerEffectKindForRecovery = "TEEMO_ALT_A_PLAY_UNIT_SELF_POWER_PLUS_3";
    private const string TeemoAltBOnPlaySelfPowerEffectKindForRecovery = "TEEMO_ALT_B_PLAY_UNIT_SELF_POWER_PLUS_3";
    private const string FndTeemoOnPlaySelfPowerEffectKindForRecovery = "FND_TEEMO_PLAY_UNIT_SELF_POWER_PLUS_3";
    private const string OnPlaySourcePowerSourceTriggerSpecLabelForRecovery =
        "BehaviorSpec on-play source-unit power modifier trigger";
    private const int PowerfulUnitPowerThresholdForRecovery = 5;
    private const string WatchfulSentinelLastBreathDrawEffectKindForRecovery = "WATCHFUL_SENTINEL_LAST_BREATH_DRAW_1";
    private const string SadPoroLastBreathDrawEffectKindForRecovery = "SAD_PORO_LAST_BREATH_DRAW_1";
    private const string LoyalPoroLastBreathDrawEffectKindForRecovery = "LOYAL_PORO_LAST_BREATH_DRAW_1";
    private const string UnsungHeroLastBreathPowerfulDrawEffectKindForRecovery = "UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_2";
    private const string ScoutingWarhawkLastBreathCallRuneEffectKindForRecovery = "SCOUTING_WARHAWK_LAST_BREATH_CALL_RUNE_1";
    private const string HonestBrokerLastBreathCreateGoldEffectKindForRecovery = "HONEST_BROKER_LAST_BREATH_CREATE_GOLD";
    private const string MechanicalTricksterLastBreathCreateMinionsEffectKindForRecovery = "MECHANICAL_TRICKSTER_LAST_BREATH_CREATE_MINIONS";
    private const string UndercoverAgentLastBreathEffectKindForRecovery = "UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT";
    private const string IroncladVanguardLastBreathCreateRobotsEffectKindForRecovery = "IRONCLAD_VANGUARD_LAST_BREATH_CREATE_ROBOTS";
    private const string MuddyDredgerLastBreathCreateWarhawkEffectKindForRecovery = "MUDDY_DREDGER_LAST_BREATH_CREATE_WARHAWK";
    private const string WatchfulSentinelLastBreathDrawSourceTriggerSpecLabelForRecovery =
        "BehaviorSpec unit last-breath draw-one trigger";
    private const string SadPoroLastBreathDrawSourceTriggerSpecLabelForRecovery =
        "BehaviorSpec unit last-breath draw-if-alone trigger";
    private const string LoyalPoroLastBreathDrawSourceTriggerSpecLabelForRecovery =
        "BehaviorSpec unit last-breath draw-if-not-alone trigger";
    private const string UnsungHeroLastBreathPowerfulDrawSourceTriggerSpecLabelForRecovery =
        "BehaviorSpec unit last-breath powerful-draw trigger";
    private const string ScoutingWarhawkLastBreathCallRuneSourceTriggerSpecLabelForRecovery =
        "BehaviorSpec unit last-breath call-rune trigger";
    private const string HonestBrokerLastBreathCreateGoldSourceTriggerSpecLabelForRecovery =
        "BehaviorSpec unit last-breath create-dormant-gold trigger";
    private const string UndercoverAgentLastBreathDiscardDrawSourceTriggerSpecLabelForRecovery =
        "BehaviorSpec unit last-breath discard-draw trigger";
    private const string UnitLastBreathCreateBaseUnitSourceTriggerSpecLabelForRecovery =
        "BehaviorSpec unit last-breath create-base-unit trigger";

    private static readonly string[] KnownTriggerQueueTriggeredEventKinds =
    [
        "DAMAGE_APPLIED",
        "CARD_DISCARDED",
        "CARDS_RECYCLED",
        "RUNE_RECYCLED",
        "TURN_END_STEP_STARTED",
        "UNIT_BECAME_POWERFUL",
        "TURN_START_BEGAN",
        "MOVEMENT_INSTRUCTION_COMPLETED",
        "EQUIPMENT_ATTACHED",
        "UNIT_TOKEN_CREATED",
        "UNIT_PLAYED_TO_BATTLEFIELD",
        "UNIT_PLAYED_TO_BASE",
        "UNIT_DESTROYED",
        "BATTLEFIELD_HELD",
        "UNIT_MOVED_TO_BATTLEFIELD",
        "UNIT_MOVED_TO_BASE",
        "CARD_PLAYED",
        "SPELL_PLAY_COMPLETED",
        "SPELL_TARGET_CHOSEN",
        "TARGET_CONFIRMED",
        "SPELL_DUEL_STARTED",
        "BATTLEFIELD_CONQUERED",
        "BATTLE_DECLARED",
        "BATTLE_ROLES_ESTABLISHED",
        "BATTLE_ROLE_ACQUIRED",
        "OBJECT_DESTROYED",
        "UNIT_READY",
        "OBJECT_EMPOWERED"
    ];

    // Preserve the established enum diagnostic for existing recovery clients.
    // New event kinds are accepted and reported as an explicit extension.
    private static readonly string[] AdditionalTriggerQueueTriggeredEventKinds = ["UNIT_EXHAUSTED", "UNITS_EXHAUSTED", "EQUIPMENT_DESTROYED", "CARDS_DISCARDED", "EQUIPMENT_PLAYED_TO_BASE", "EQUIPMENT_PLAYED_TO_BATTLEFIELD", "EQUIPMENT_TOKEN_CREATED", "MAIN_PHASE_BEGAN", "REFLEXIVE_BURN_MIGHT_CREATED", "BATTLE_CLOSED", "STATUS_EFFECT_APPLIED", "CARD_BANISHED", "CARD_PLAY_COMPLETED"];

    private static readonly string[] KnownTriggerSourceVisibilities =
    [
        "VISIBLE",
        "HIDDEN"
    ];

    private static readonly string[] KnownRuleQueueCoverageKeys =
    [
        "stack",
        "trigger",
        "battle",
        "window",
        "payment"
    ];

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
        return ValidateFrame(roomId, lastEventSequence, commands, events, playerViews,
            authoritativeState, currentTick, spectatorReplayFrame, requireAcceptedBattleHistory: true);
    }

    // A component fixture has no journal. This checks its state and views, but
    // cannot certify accepted birth or restore a persisted match. Recovery uses
    // Validate above and retains the mandatory accepted-history check.
    public static IReadOnlyList<string> ValidateComponentStateAndViews(
        MatchState authoritativeState,
        IReadOnlyDictionary<string, RecoveredPlayerView>? playerViews = null,
        MatchReplayFrame? spectatorReplayFrame = null)
    {
        ArgumentNullException.ThrowIfNull(authoritativeState);
        return ValidateFrame(authoritativeState.RoomId, 0, [], [],
            playerViews ?? new Dictionary<string, RecoveredPlayerView>(), authoritativeState,
            authoritativeState.Tick, spectatorReplayFrame, requireAcceptedBattleHistory: false);
    }

    private static IReadOnlyList<string> ValidateFrame(
        string roomId,
        long lastEventSequence,
        IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events,
        IReadOnlyDictionary<string, RecoveredPlayerView> playerViews,
        MatchState? authoritativeState,
        long? currentTick,
        MatchReplayFrame? spectatorReplayFrame,
        bool requireAcceptedBattleHistory)
    {
        var errors = new List<string>();
        if (authoritativeState is { } inputState && !RuleInputIdentity.IsCurrentState(inputState))
            errors.Add("authoritative rule input identity is missing or differs from running inputs");
        if (commands.Any(command => !RuleInputIdentity.IsCurrent(command.RuleInputs)))
            errors.Add("command rule input identity is missing or differs from running inputs");

        RoomSeatRecoveryValidator.Validate(commands, events, errors);
        if (authoritativeState is not null)
        {
            ValidateTemporaryNumericUnitEffects(authoritativeState, events, requireAcceptedBattleHistory, errors);
            ValidateOriginalEffectPlayLocation(authoritativeState, events, requireAcceptedBattleHistory, errors);
        }
        if (authoritativeState is not null && requireAcceptedBattleHistory)
            ValidateBattlefieldConquestHistory(authoritativeState, events, errors);

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
        var visibleState = authoritativeState is null ? null : CoreRuleEngine.VisibleRuleChoiceState(authoritativeState);
        ValidatePlayerViews(currentTick, lastEventSequence, playerViews, errors, visibleState);
        ValidatePlayerViewAgreement(playerViews, errors);
        ValidateAuthoritativeState(roomId, currentTick, authoritativeState, playerViews, errors);
        if (requireAcceptedBattleHistory && authoritativeState is not null)
        {
            ValidateBattleLimitedHistory(authoritativeState, commands, events, errors);
            ValidateBattlefieldConquestMillHistory(authoritativeState, commands, events, errors);
            ValidateUnitConquestReturnHistory(authoritativeState, commands, events, errors);
            ValidateBattlefieldConquestChoiceHistory(authoritativeState, commands, events, errors);
            errors.AddRange(BattleResultRecoveryRules.ValidateClosedCleanupRecalls(authoritativeState, commands, events));
        }
        ValidateSpectatorReplayFrame(
            roomId,
            lastEventSequence,
            currentTick,
            authoritativeState,
            spectatorReplayFrame,
            errors);

        return errors;
    }

    private static void ValidateSnapshotTimingTriggerQueuePayloadValues(
        RecoveredPlayerView view,
        List<string> errors,
        MatchState? authoritativeState)
    {
        if (view.Snapshot.Timing is null
            || !TryReadObjectList(view.Snapshot.Timing, "triggerQueue", out var triggerPayloads))
        {
            return;
        }

        ValidateLastBreathUnitCreationViews(triggerPayloads, errors);
        ValidateLastBreathRuneCallViews(triggerPayloads, errors);
        ValidateLastBreathDrawViews(triggerPayloads, errors);
        ValidateLastBreathBattlefieldDamageViews(triggerPayloads, errors);

        var knownPlayerIds = view.Snapshot.Players is null
            ? null
            : BuildNormalizedPlayerIdSet(view.Snapshot.Players.Keys);
        var knownObjectIds = view.Snapshot.Players is null
            ? null
            : BuildSnapshotKnownObjectIds(view.Snapshot);
        var objectCardNos = view.Snapshot.Players is null
            ? null
            : BuildSnapshotObjectCardNoIndex(view.Snapshot);
        var objectPowers = view.Snapshot.Players is null
            ? null
            : BuildSnapshotObjectPowerIndex(view.Snapshot);
        var objectControllers = view.Snapshot.Players is null
            ? null
            : BuildSnapshotObjectControllerIndex(view.Snapshot);
        var objectFaceDowns = view.Snapshot.Players is null
            ? null
            : BuildSnapshotObjectFaceDownIndex(view.Snapshot);
        var objectTags = view.Snapshot.Players is null
            ? null
            : BuildSnapshotObjectTagIndex(view.Snapshot);
        var objectLocations = view.Snapshot.Players is null
            ? null
            : BuildSnapshotObjectLocationIndex(view.Snapshot);
        var playerBattlefieldObjectIdsByPlayer = view.Snapshot.Players is null
            ? null
            : BuildSnapshotPlayerBattlefieldObjectIdsByPlayer(view.Snapshot);
        var playerBaseObjectIdsByPlayer = view.Snapshot.Players is null
            ? null
            : BuildSnapshotPlayerBaseObjectIdsByPlayer(view.Snapshot);
        var playerGraveyardObjectIdsByPlayer = view.Snapshot.Players is null
            ? null
            : BuildSnapshotPlayerGraveyardObjectIdsByPlayer(view.Snapshot);
        var playerFieldObjectIdsByPlayer = view.Snapshot.Players is null
            ? null
            : BuildSnapshotPlayerFieldObjectIdsByPlayer(view.Snapshot);
        var knownBattlefieldStateObjectIds = BuildSnapshotBattlefieldStateObjectIds(view.Snapshot);
        var seenTriggerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var triggerPayload in triggerPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(triggerPayload))
            {
                continue;
            }

            const string payloadLabel = "trigger queue item";
            var triggerLabel = $"snapshot for {view.PlayerId} timing {payloadLabel}";
            ValidateUnitConquestReturnPrivateContextAbsent(triggerPayload, triggerLabel, errors);
            ValidateBattlefieldConquestChoiceView(triggerPayload, authoritativeState, false, triggerLabel, errors);
            var triggerId = ValidateSnapshotPayloadRequiredStringValue(
                triggerPayload,
                "triggerId",
                triggerLabel,
                "trigger id",
                errors);
            if (triggerId is not null && !seenTriggerIds.Add(triggerId))
            {
                errors.Add($"{triggerLabel} trigger id {triggerId} is duplicated");
            }

            var controllerId = ValidateSnapshotPayloadRequiredStringValue(
                triggerPayload,
                "controllerId",
                triggerLabel,
                "controller id",
                errors);
            var sourceObjectId = ValidateSnapshotPayloadRequiredStringValue(
                triggerPayload,
                "sourceObjectId",
                triggerLabel,
                "source object id",
                errors);
            var sourceVisibility = ValidateTriggerQueueSourceVisibilityValue(
                triggerPayload,
                triggerLabel,
                errors);
            var effectKind = ValidateSnapshotPayloadRequiredStringValue(
                triggerPayload,
                "effectKind",
                triggerLabel,
                "effect kind",
                errors);
            var triggeredEventKind = ValidateSnapshotPayloadRequiredStringValue(
                triggerPayload,
                "triggeredByEventKind",
                triggerLabel,
                "triggered event kind",
                errors);
            ValidateTriggerQueuePromptFieldAbsence(triggerPayload, triggerLabel, errors);
            ValidateTriggerQueueIdentityRedactionSentinelAbsence(
                triggerLabel,
                triggerId,
                controllerId,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueTriggeredEventKindKnownValue(triggerLabel, triggeredEventKind, errors);
            ValidateTriggerQueueControllerPlayerMembership(
                triggerLabel,
                controllerId,
                knownPlayerIds,
                "players",
                errors);

            ValidateTriggerQueueSourceRedactionConsistency(
                triggerLabel,
                sourceObjectId,
                sourceVisibility,
                effectKind,
                errors,
                HasValidatedPublicDelayedEmpowerEffect(authoritativeState, triggerId, effectKind)
                    || HasValidatedPublicBombRebuildEffect(authoritativeState, triggerId, effectKind)
                    || HasValidatedPublicLastBreathUnitCreationEffect(authoritativeState, triggerId, effectKind)
                    || (HasValidatedPublicLastBreathRuneCallEffect(authoritativeState, triggerId, effectKind) || (HasValidatedPublicLastBreathDrawEffect(authoritativeState, triggerId, effectKind) || HasValidatedPublicLastBreathBattlefieldDamageEffect(authoritativeState, triggerId, effectKind)))
                    || HasValidatedPublicMovementDiscardEffect(authoritativeState, triggerId, effectKind)
                    || HasValidatedPublicUnitConquestReturnEffect(authoritativeState, triggerId, effectKind));
            ValidateTriggerQueueBlueSentinelDelayedResourceContext(
                triggerLabel,
                triggerId,
                controllerId,
                sourceObjectId,
                sourceVisibility,
                objectControllers,
                "objects",
                objectCardNos,
                "objects",
                objectFaceDowns,
                "objects",
                objectTags,
                "objects",
                objectLocations,
                "object locations",
                playerBattlefieldObjectIdsByPlayer,
                "player zones",
                knownBattlefieldStateObjectIds,
                "battlefield states",
                view.Snapshot.TurnNumber,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueJhinMovementResourceContext(
                triggerLabel,
                triggerId,
                controllerId,
                sourceObjectId,
                objectControllers,
                "objects",
                objectCardNos,
                "objects",
                objectFaceDowns,
                "objects",
                objectTags,
                "objects",
                objectLocations,
                "object locations",
                knownBattlefieldStateObjectIds,
                "battlefield states",
                effectKind,
                triggeredEventKind,
                view.SnapshotTick,
                errors);
            ValidateTriggerQueueKogmawLastBreathContext(
                triggerLabel,
                triggerId,
                controllerId,
                sourceObjectId,
                sourceVisibility,
                objectControllers,
                "objects",
                knownObjectIds,
                "objects",
                objectCardNos,
                "objects",
                objectFaceDowns,
                "objects",
                objectTags,
                "objects",
                objectLocations,
                "object locations",
                knownBattlefieldStateObjectIds,
                "battlefield states",
                effectKind,
                triggeredEventKind,
                errors, authoritativeState, view.PlayerId);
            ValidateTriggerQueueOgsLuxHighCostSpellContext(
                triggerLabel,
                triggerId,
                controllerId,
                sourceObjectId,
                sourceVisibility,
                objectControllers,
                "objects",
                objectCardNos,
                "objects",
                objectFaceDowns,
                "objects",
                objectTags,
                "objects",
                playerFieldObjectIdsByPlayer,
                "player zones",
                objectLocations,
                "object locations",
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueTeemoOnPlaySelfPowerContext(
                triggerLabel,
                triggerId,
                controllerId,
                sourceObjectId,
                sourceVisibility,
                objectControllers,
                "objects",
                objectCardNos,
                "objects",
                objectFaceDowns,
                "objects",
                objectTags,
                "objects",
                playerBaseObjectIdsByPlayer,
                "player zones",
                objectLocations,
                "object locations",
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueWatchfulSentinelLastBreathDrawContext(
                triggerLabel,
                triggerId,
                sourceVisibility,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueUnsungHeroLastBreathPowerfulDrawContext(
                triggerLabel,
                triggerId,
                sourceVisibility,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueScoutingWarhawkLastBreathCallRuneContext(
                triggerLabel,
                triggerId,
                sourceVisibility,
                effectKind,
                triggeredEventKind,
                errors,
            (HasValidatedPublicLastBreathRuneCallEffect(authoritativeState, triggerId, effectKind) || (HasValidatedPublicLastBreathDrawEffect(authoritativeState, triggerId, effectKind) || HasValidatedPublicLastBreathBattlefieldDamageEffect(authoritativeState, triggerId, effectKind))));
            ValidateTriggerQueueSadPoroLastBreathDrawContext(
                triggerLabel,
                triggerId,
                sourceVisibility,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueLoyalPoroLastBreathDrawContext(
                triggerLabel,
                triggerId,
                sourceVisibility,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueHonestBrokerLastBreathCreateGoldContext(
                triggerLabel,
                triggerId,
                sourceVisibility,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueMechanicalTricksterLastBreathCreateMinionsContext(
                triggerLabel,
                triggerId,
                sourceVisibility,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueUndercoverAgentLastBreathContext(
                triggerLabel,
                triggerId,
                sourceVisibility,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueIroncladVanguardLastBreathCreateRobotsContext(
                triggerLabel,
                triggerId,
                sourceVisibility,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateTriggerQueueMuddyDredgerLastBreathCreateWarhawkContext(
                triggerLabel,
                triggerId,
                sourceVisibility,
                effectKind,
                triggeredEventKind,
                errors);
            ValidateLastBreathMightPayload(triggerPayload, effectKind, errors);
            ValidateBattlefieldConquestMightPayload(triggerPayload, effectKind, errors);
            ValidateBattlefieldConquestMillPayload(triggerPayload, effectKind, errors);
            ValidateMightySkillPayload(triggerPayload, effectKind, errors);
            ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
                triggerLabel,
                triggerId,
                controllerId,
                sourceObjectId,
                effectKind,
                objectControllers,
                "objects",
                objectCardNos,
                "objects",
                objectPowers,
                "objects",
                objectFaceDowns,
                "objects",
                objectTags,
                "objects",
                objectLocations,
                "object locations",
                playerBaseObjectIdsByPlayer,
                "player zones",
                playerGraveyardObjectIdsByPlayer,
                "player zones",
                errors,
                (HasValidatedPublicLastBreathUnitCreationEffect(authoritativeState, triggerId, effectKind) || (HasValidatedPublicLastBreathRuneCallEffect(authoritativeState, triggerId, effectKind) || (HasValidatedPublicLastBreathDrawEffect(authoritativeState, triggerId, effectKind) || HasValidatedPublicLastBreathBattlefieldDamageEffect(authoritativeState, triggerId, effectKind)))));
            var ceasedInsight = ValidateCapturedInsightView(triggerPayload,effectKind,controllerId,sourceObjectId,errors);
            if (!ValidateReflexiveDuelView(triggerPayload,effectKind,errors)
            && !ValidateHandDiscardObserverView(triggerPayload,effectKind,controllerId,sourceObjectId,errors)
            && !ValidateMovementObserverView(triggerPayload,effectKind,controllerId,sourceObjectId,errors)
            && !ValidateDeathObserverView(triggerPayload,effectKind,controllerId,sourceObjectId,errors)
            && !ValidateDestructionFamilyView(triggerPayload,effectKind,controllerId,sourceObjectId,errors)
            && !ceasedInsight
            && !HasValidatedCeasedLastBreathSource(authoritativeState, triggerId, controllerId, sourceObjectId, effectKind)
            && !HasValidatedCeasedBeginningRuneSource(authoritativeState, triggerId, controllerId, sourceObjectId, effectKind)
            && !HasValidatedUnitConquestReturnQueueSource(authoritativeState, triggerId, controllerId, sourceObjectId, effectKind))
            ValidateTriggerQueueVisibleSourceObjectMembership(
                triggerLabel,
                sourceObjectId,
                sourceVisibility,
                knownObjectIds,
                "objects",
                errors);
        }
    }

    private static void ValidateSpectatorSnapshotVisiblePlayerObjectScalars(
        string playerId,
        string objectId,
        object? objectPayload,
        CardObjectState cardObject,
        MatchState authoritativeState,
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
            errors,
            mustBeNonNegative: true);
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
            TemporaryUnitEffectRules.Number(cardObject, TemporaryUnitEffectRules.BaseMightPrefix)
                ?? cardObject.Power - cardObject.UntilEndOfTurnPowerModifier,
            "base power",
            errors);
        ValidateSpectatorSnapshotPlayerObjectIntScalar(
            playerId,
            objectId,
            objectPayload,
            "effectivePower",
            ResolutionResult.SnapshotEffectivePower(authoritativeState, cardObject,
                ExpectedSpectatorObjectLocation(authoritativeState, objectId)),
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
            errors,
            mustBeNonNegative: true);

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
            "tag",
            errors);
        ValidateSpectatorSnapshotPlayerObjectStringListScalar(
            playerId,
            objectId,
            objectPayload,
            "untilEndOfTurnEffects",
            cardObject.UntilEndOfTurnEffects.Where(text => !BattleLimitedMarkers.PrivateMarker(text))
                .Select(CoreRuleEngine.PublicTemporaryActivationEffect).ToArray(),
            "until-end-of-turn effects",
            "until-end-of-turn effect",
            errors);
    }

    private static void ValidateContinuousEffectSourceObjectConsistency(
        string effectLabel,
        string? scope,
        string? layer,
        string? sourceObjectId,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        if (string.Equals(layer, ContinuousEffectLayers.RuleText, StringComparison.Ordinal)
            && string.Equals(scope, "GLOBAL", StringComparison.Ordinal))
        {
            if (sourceObjectId is not null)
            {
                errors.Add($"{effectLabel} global rule text source object id must be absent");
            }

            return;
        }

        if (string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            && sourceObjectId is null)
        {
            errors.Add($"{effectLabel} static aura source object id is required");
        }
    }

    private static void ValidateContinuousEffectStaticAuraEffectIdConsistency(
        string effectLabel,
        string? effectId,
        string? scope,
        string? layer,
        string? duration,
        string? targetObjectId,
        string? sourceObjectId,
        string? effectKind,
        List<string> errors)
    {
        if (effectId is null
            || scope is null
            || layer is null
            || duration is null
            || targetObjectId is null
            || sourceObjectId is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !IsKnownContinuousEffectDuration(duration)
            || !string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        if (effectKind == CoreRuleEngine.BoonAmplifierEffect) {
            if (scope != "OBJECT" || duration != "UNTIL_END_OF_TURN" || !effectId.StartsWith("BOON_MIGHT:", StringComparison.Ordinal)
                || !effectId.EndsWith(":" + targetObjectId, StringComparison.Ordinal)
                || effectId.Length <= 12 + targetObjectId.Length
                || CoreRuleEngine.ReadBoonAmplifier(effectId[11..^(targetObjectId.Length + 1)]) is not { } grant
                || grant.Source.ObjectId != sourceObjectId)
                errors.Add($"{effectLabel} temporary Boon amplifier identity is invalid");
            return;
        }

        if (effectKind == CoreRuleEngine.EmpoweredUnitAuraEffect) {
            if (scope != "OBJECT" || duration != "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD"
                || !effectId.StartsWith($"EMPOWERED_UNIT_AURA:{sourceObjectId}:", StringComparison.Ordinal)
                || !effectId.EndsWith(":" + targetObjectId, StringComparison.Ordinal))
                errors.Add($"{effectLabel} empowered unit aura identity is invalid");
            return;
        }

        if (effectKind == StaticAuraKinds.SameLocationEnemyFilteredPower) {
            if (scope != "OBJECT" || duration != "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD"
                || !effectId.StartsWith($"CONDITIONAL_POWER_AURA:{sourceObjectId}:", StringComparison.Ordinal)
                || !effectId.EndsWith(":" + targetObjectId, StringComparison.Ordinal))
                errors.Add($"{effectLabel} conditional power aura identity is invalid");
            return;
        }

        if (string.Equals(scope, "OBJECT", StringComparison.Ordinal))
        {
            if (string.Equals(duration, "WHILE_SOURCE_ON_PUBLIC_FIELD", StringComparison.Ordinal)
                && string.Equals(targetObjectId, sourceObjectId, StringComparison.Ordinal))
            {
                if (effectKind == StaticAuraKinds.SourceOtherSameNameUnitCountPower)
                {
                    ValidateContinuousEffectStaticAuraExpectedEffectId(effectLabel, effectId,
                        $"STATIC_AURA:{effectKind}:{sourceObjectId}", "same name aura effect id", errors);
                    return;
                }
                if (effectKind == StaticAuraKinds.SourceControllerScorePower)
                {
                    ValidateContinuousEffectStaticAuraExpectedEffectId(effectLabel, effectId,
                        $"STATIC_AURA:SOURCE_SCORE_POWER:{sourceObjectId}", "score aura effect id", errors);
                    return;
                }
                if (effectKind == StaticAuraKinds.SourceGraveyardCountPower)
                {
                    ValidateContinuousEffectStaticAuraExpectedEffectId(effectLabel, effectId,
                        $"STATIC_AURA:SOURCE_GRAVEYARD_POWER:{sourceObjectId}", "graveyard aura effect id", errors);
                    return;
                }
                if (string.Equals(effectKind, StaticAuraKinds.SourceObjectPower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraExpectedEffectId(
                        effectLabel,
                        effectId,
                        $"STATIC_AURA:SOURCE_OBJECT_POWER:{sourceObjectId}",
                        "object static aura effect id",
                        errors);
                    return;
                }

                if (string.Equals(effectKind, StaticAuraKinds.SourceObjectFilteredPower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraExpectedEffectId(
                        effectLabel,
                        effectId,
                        $"STATIC_AURA:SOURCE_OBJECT_FILTERED_POWER:{sourceObjectId}",
                        "object static aura effect id",
                        errors);
                    return;
                }

                if (effectKind is StaticAuraKinds.FriendlyFilteredUnitCountToSourcePower
                    or StaticAuraKinds.SourceBattlefieldFriendlyObjectThresholdPower)
                {
                    ValidateContinuousEffectStaticAuraExpectedEffectId(effectLabel, effectId,
                        $"STATIC_AURA:{effectKind}:{sourceObjectId}", "object static aura effect id", errors);
                    return;
                }
                if (string.Equals(effectKind, StaticAuraKinds.SameBattlefieldFriendlyFilteredUnitCountToSourcePower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraExpectedEffectId(
                        effectLabel,
                        effectId,
                        $"STATIC_AURA:SAME_BATTLEFIELD_FRIENDLY_FILTERED_UNIT_COUNT_TO_SOURCE_POWER:{sourceObjectId}",
                        "object static aura effect id",
                        errors);
                    return;
                }

                if ((string.Equals(effectKind, StaticAuraKinds.SourceObjectPowerWithSameLocationEnemy, StringComparison.Ordinal)
                    || effectKind == StaticAuraKinds.SourceObjectPowerWithFriendlyEquipment))
                {
                    ValidateContinuousEffectStaticAuraExpectedEffectId(effectLabel, effectId,
                        $"STATIC_AURA:{effectKind}:{sourceObjectId}",
                        "object static aura effect id", errors);
                    return;
                }

                if (string.Equals(effectKind, StaticAuraKinds.SourceSameLocationOtherFriendlyUnitPower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraExpectedEffectId(
                        effectLabel,
                        effectId,
                        $"STATIC_AURA:SOURCE_SAME_LOCATION_OTHER_FRIENDLY_UNIT_POWER:{sourceObjectId}",
                        "object static aura effect id",
                        errors);
                    return;
                }

                if (!string.Equals(effectKind, StaticAuraKinds.FriendlyFieldEquipmentCountToSourceUnitPower, StringComparison.Ordinal))
                {
                    if (effectKind is not null)
                    {
                        errors.Add(
                            $"{effectLabel} object static aura effect kind {effectKind} is invalid for {duration}");
                    }

                    return;
                }

                ValidateContinuousEffectStaticAuraExpectedEffectId(
                    effectLabel,
                    effectId,
                    $"STATIC_AURA:FRIENDLY_EQUIPMENT_POWER:{sourceObjectId}",
                    "object static aura effect id",
                    errors);
                return;
            }

            if (string.Equals(duration, "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", StringComparison.Ordinal))
            {
                if (string.Equals(effectKind, StaticAuraKinds.FriendlyUnitsPower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraExpectedEffectId(
                        effectLabel,
                        effectId,
                        $"STATIC_AURA:FRIENDLY_UNITS_POWER:{sourceObjectId}:{targetObjectId}",
                        "object static aura effect id",
                        errors);
                    return;
                }

                if (string.Equals(effectKind, StaticAuraKinds.FriendlyFilteredUnitsPower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraExpectedEffectId(
                        effectLabel,
                        effectId,
                        $"STATIC_AURA:FRIENDLY_FILTERED_UNITS_POWER:{sourceObjectId}:{targetObjectId}",
                        "object static aura effect id",
                        errors);
                    return;
                }

                if (!string.Equals(effectKind, StaticAuraKinds.OtherFriendlyUnitsPower, StringComparison.Ordinal))
                {
                    if (effectKind is not null)
                    {
                        errors.Add(
                            $"{effectLabel} object static aura effect kind {effectKind} is invalid for {duration}");
                    }

                    return;
                }

                ValidateContinuousEffectStaticAuraExpectedEffectId(
                    effectLabel,
                    effectId,
                    $"STATIC_AURA:OTHER_FRIENDLY_UNITS_POWER:{sourceObjectId}:{targetObjectId}",
                    "object static aura effect id",
                    errors);
            }

            if (string.Equals(duration, "WHILE_SOURCE_ATTACKING_WITH_REQUIRED_ATTACKER_COUNT", StringComparison.Ordinal))
            {
                if (!string.Equals(effectKind, StaticAuraKinds.SourceAttackingWithAnotherUnitPower, StringComparison.Ordinal))
                {
                    if (effectKind is not null)
                    {
                        errors.Add(
                            $"{effectLabel} object static aura effect kind {effectKind} is invalid for {duration}");
                    }

                    return;
                }

                ValidateContinuousEffectStaticAuraExpectedEffectId(
                    effectLabel,
                    effectId,
                    $"STATIC_AURA:SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER:{sourceObjectId}",
                    "object static aura effect id",
                    errors);
                return;
            }

            if (string.Equals(duration, "WHILE_SOURCE_ATTACKING_OR_DEFENDING_ALONE", StringComparison.Ordinal))
            {
                if (!string.Equals(effectKind, StaticAuraKinds.SourceLoneBattlePower, StringComparison.Ordinal))
                {
                    if (effectKind is not null)
                    {
                        errors.Add(
                            $"{effectLabel} object static aura effect kind {effectKind} is invalid for {duration}");
                    }

                    return;
                }

                ValidateContinuousEffectStaticAuraExpectedEffectId(
                    effectLabel,
                    effectId,
                    $"STATIC_AURA:SOURCE_LONE_BATTLE_POWER:{sourceObjectId}",
                    "object static aura effect id",
                    errors);
                return;
            }

            if (string.Equals(duration, "WHILE_SOURCE_ATTACKING_READY_ENEMY_UNIT_BATTLEFIELD", StringComparison.Ordinal))
            {
                if (!string.Equals(effectKind, StaticAuraKinds.SourceAttackingReadyEnemyUnitPower, StringComparison.Ordinal))
                {
                    if (effectKind is not null)
                    {
                        errors.Add(
                            $"{effectLabel} object static aura effect kind {effectKind} is invalid for {duration}");
                    }

                    return;
                }

                ValidateContinuousEffectStaticAuraExpectedEffectId(
                    effectLabel,
                    effectId,
                    $"STATIC_AURA:SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER:{sourceObjectId}",
                    "object static aura effect id",
                    errors);
                return;
            }

            return;
        }

        if (!string.Equals(scope, "BATTLEFIELD", StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(duration, "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", StringComparison.Ordinal))
        {
            if (string.Equals(effectKind, StaticAuraKinds.BattlefieldFilteredUnitsPower, StringComparison.Ordinal))
            {
                ValidateContinuousEffectStaticAuraExpectedEffectId(
                    effectLabel,
                    effectId,
                    $"STATIC_AURA:BATTLEFIELD_FILTERED_UNITS_POWER:{sourceObjectId}:{targetObjectId}",
                    "battlefield static aura effect id",
                    errors);
                return;
            }

            if (!string.Equals(effectKind, StaticAuraKinds.BattlefieldAllUnitsPowerPlusOne, StringComparison.Ordinal))
            {
                if (effectKind is not null)
                {
                    errors.Add(
                        $"{effectLabel} battlefield static aura effect kind {effectKind} is invalid for {duration}");
                }

                return;
            }

            ValidateContinuousEffectStaticAuraExpectedEffectId(
                effectLabel,
                effectId,
                $"STATIC_AURA:BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE:{sourceObjectId}:{targetObjectId}",
                "battlefield static aura effect id",
                errors);
            return;
        }

        if (string.Equals(duration, "WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", StringComparison.Ordinal))
        {
            if (string.Equals(effectKind, StaticAuraKinds.SameBattlefieldOtherFriendlyFilteredUnitsPower, StringComparison.Ordinal))
            {
                ValidateContinuousEffectStaticAuraExpectedEffectId(
                    effectLabel,
                    effectId,
                    $"STATIC_AURA:SAME_BATTLEFIELD_OTHER_FRIENDLY_FILTERED_UNITS_POWER:{sourceObjectId}:{targetObjectId}",
                    "battlefield static aura effect id",
                    errors);
                return;
            }

            if (!string.Equals(effectKind, StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsPowerPlusOne, StringComparison.Ordinal))
            {
                if (effectKind is not null)
                {
                    errors.Add(
                        $"{effectLabel} battlefield static aura effect kind {effectKind} is invalid for {duration}");
                }

                return;
            }

            ValidateContinuousEffectStaticAuraExpectedEffectId(
                effectLabel,
                effectId,
                $"STATIC_AURA:SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE:{sourceObjectId}:{targetObjectId}",
                "battlefield static aura effect id",
                errors);
        }
    }

    private static void ValidateContinuousEffectStaticAuraExpectedEffectId(
        string effectLabel,
        string actual,
        string expected,
        string diagnosticLabel,
        List<string> errors)
    {
        if (string.Equals(actual, expected, StringComparison.Ordinal))
        {
            return;
        }

        errors.Add($"{effectLabel} {diagnosticLabel} must be {expected}");
    }

    private static void ValidateContinuousEffectStaticAuraMetadataConsistency(
        string effectLabel,
        string? scope,
        string? layer,
        string? duration,
        string? effectKind,
        string? sourceCardNo,
        string? sourcePath,
        string? condition,
        string? lifecycle,
        List<string> errors)
    {
        if (!string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal))
        {
            return;
        }

        if (effectKind is null)
        {
            errors.Add($"{effectLabel} static aura effect kind is required");
        }

        if (sourceCardNo is null)
        {
            errors.Add($"{effectLabel} static aura source card no is required");
        }

        if (sourcePath is null)
        {
            errors.Add($"{effectLabel} static aura source path is required");
        }

        if (condition is null)
        {
            errors.Add($"{effectLabel} static aura condition is required");
        }

        if (lifecycle is null)
        {
            errors.Add($"{effectLabel} static aura lifecycle is required");
        }

        ValidateContinuousEffectStaticAuraMetadataValueConsistency(
            effectLabel,
            scope,
            layer,
            duration,
            effectKind,
            sourceCardNo,
            sourcePath,
            condition,
            lifecycle,
            errors);
    }

    private static void ValidateContinuousEffectStaticAuraMetadataValueConsistency(
        string effectLabel,
        string? scope,
        string? layer,
        string? duration,
        string? effectKind,
        string? sourceCardNo,
        string? sourcePath,
        string? condition,
        string? lifecycle,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || duration is null
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !IsKnownContinuousEffectDuration(duration)
            || !string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        if (effectKind == CoreRuleEngine.BoonAmplifierEffect) {
            if (scope != "OBJECT" || duration != "UNTIL_END_OF_TURN"
                || sourceCardNo is null || !CardBehaviorRegistry.TryGetByCardNo(sourceCardNo, out var b) || b.FriendlyBoonBonusUntilEndOfTurn <= 0)
                errors.Add($"{effectLabel} temporary Boon amplifier requires matching source and scope");
            ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, sourcePath, "CoreRuleEngine.BoonAmplifiersFor", "Boon amplifier source path", errors);
            ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, condition, "CURRENT_FRIENDLY_UNIT_HAS_BOON", "Boon amplifier condition", errors);
            ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, lifecycle, "CAPTURED_SOURCE_CURRENT_CONTROLLER_AND_BOON", "Boon amplifier lifecycle", errors);
            return;
        }

        if (effectKind == CoreRuleEngine.EmpoweredUnitAuraEffect) {
            if (scope != "OBJECT" || duration != "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD" || CoreRuleEngine.EmpoweredUnitAuras(sourceCardNo).Count == 0)
                errors.Add($"{effectLabel} empowered unit aura requires a matching official source and scope");
            ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, sourcePath, "CoreRuleEngine.EmpoweredUnitAuraContributions", "empowered aura source path", errors);
            ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, condition, "SOURCE_AND_FRIENDLY_TARGET_EMPOWERED", "empowered aura condition", errors);
            ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, lifecycle, "RECOMPUTED_FROM_CURRENT_EMPOWERED_TEXT_AND_CONTROLLER", "empowered aura lifecycle", errors);
            return;
        }

        if (effectKind == StaticAuraKinds.SameLocationEnemyFilteredPower) {
            var spec = StaticAuraSpecRules.GetStaticAuras(sourceCardNo).FirstOrDefault(StaticAuraSpecRules.IsConditionalEnemyPowerAura);
            if (scope != "OBJECT" || duration != "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD" || spec is null)
                errors.Add($"{effectLabel} conditional power aura requires a matching official source and scope");
            ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, sourcePath,
                "CoreRuleEngine.ConditionalPowerAuraContributions", "conditional aura source path", errors);
            ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, condition,
                $"STUNNED_ENEMY_AT_SOURCE_LOCATION_MINIMUM:{spec?.MinimumPowerAfterModifier}", "conditional aura condition", errors);
            ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, lifecycle,
                "RECOMPUTED_FROM_CURRENT_CONDITION_LOCATION_AND_MIGHT", "conditional aura lifecycle", errors);
            return;
        }

        if (string.Equals(scope, "OBJECT", StringComparison.Ordinal))
        {
            if (string.Equals(duration, "WHILE_SOURCE_ON_PUBLIC_FIELD", StringComparison.Ordinal))
            {
                if (effectKind == StaticAuraKinds.SourceOtherSameNameUnitCountPower)
                {
                    if (!StaticAuraSpecRules.GetStaticAuras(sourceCardNo).Any(a => a.Kind == effectKind
                        && StaticAuraSpecRules.IsSourceParticipantCountPowerStaticAura(a)))
                        errors.Add($"{effectLabel} same name aura requires a matching official source");
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, sourcePath,
                        "CoreRuleEngine.ResolveSourceParticipantCountPowerStaticAuraBonus", "same name aura source path", errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, condition,
                        "SOURCE_PARTICIPANT_COUNT_POWER_SCOPE_MATCHES", "same name aura condition", errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, lifecycle,
                        "RECOMPUTED_FROM_CURRENT_SOURCE_PARTICIPANT_COUNT", "same name aura lifecycle", errors);
                    return;
                }
                if (effectKind == StaticAuraKinds.SourceControllerScorePower)
                {
                    if (sourceCardNo is null || !StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                        .Any(a => a.Kind == effectKind && StaticAuraSpecRules.IsSourceObjectPowerStaticAura(a)))
                        errors.Add($"{effectLabel} score aura requires a matching official source");
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, sourcePath,
                        "StaticAuraSpecRules.SourceObjectPowerDelta", "score aura source path", errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, condition,
                        "SOURCE_PUBLIC_FIELD_UNIT", "score aura condition", errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, lifecycle,
                        "RECOMPUTED_FROM_CURRENT_CONTROLLER_SCORE", "score aura lifecycle", errors);
                    return;
                }
                if (effectKind == StaticAuraKinds.SourceGraveyardCountPower)
                {
                    if (sourceCardNo is null || !StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                        .Any(a => a.Kind == effectKind && a.PowerMultiplierZone == "GRAVEYARD"))
                        errors.Add($"{effectLabel} graveyard aura requires a matching official source");
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, sourcePath,
                        "StaticAuraSpecRules.SourceObjectPowerDelta", "graveyard aura source path", errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, condition,
                        "SOURCE_PUBLIC_FIELD_UNIT", "graveyard aura condition", errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, lifecycle,
                        "RECOMPUTED_FROM_CURRENT_CONTROLLER_GRAVEYARD", "graveyard aura lifecycle", errors);
                    return;
                }
                if (string.Equals(effectKind, StaticAuraKinds.SourceObjectPower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraSourceObjectPowerSourceCardSpecConsistency(
                        effectLabel,
                        sourceCardNo,
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        sourcePath,
                        "CoreRuleEngine.ResolveSourceObjectPowerBonus",
                        "object static aura source path",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        condition,
                        "SOURCE_PUBLIC_FIELD_UNIT_AND_CONTROLLER_EXPERIENCE",
                        "object static aura condition",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        lifecycle,
                        "RECOMPUTED_FROM_CURRENT_CONTROLLER_EXPERIENCE",
                        "object static aura lifecycle",
                        errors);
                    return;
                }

                if (string.Equals(effectKind, StaticAuraKinds.SourceObjectFilteredPower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraSourceObjectFilteredSourceCardSpecConsistency(
                        effectLabel,
                        sourceCardNo,
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        sourcePath,
                        "CoreRuleEngine.ResolveSourceObjectFilteredPowerBonus",
                        "object static aura source path",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        condition,
                        "SOURCE_PUBLIC_FIELD_UNIT_MATCHES_FILTER",
                        "object static aura condition",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        lifecycle,
                        "RECOMPUTED_FROM_CURRENT_SOURCE_OBJECT_TAGS",
                        "object static aura lifecycle",
                        errors);
                    return;
                }

                if (effectKind is StaticAuraKinds.FriendlyFilteredUnitCountToSourcePower
                    or StaticAuraKinds.SourceBattlefieldFriendlyObjectThresholdPower)
                {
                    if (sourceCardNo is null || !StaticAuraSpecRules.GetStaticAuras(sourceCardNo).Any(a => a.Kind == effectKind))
                        errors.Add($"{effectLabel} source lacks the controlled unit count aura");
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, sourcePath,
                        "CoreRuleEngine.ResolveSourceParticipantCountPowerStaticAuraBonus", "object static aura source path", errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, condition,
                        "SOURCE_PARTICIPANT_COUNT_POWER_SCOPE_MATCHES", "object static aura condition", errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, lifecycle,
                        "RECOMPUTED_FROM_CURRENT_SOURCE_PARTICIPANT_COUNT", "object static aura lifecycle", errors);
                    return;
                }
                if (string.Equals(effectKind, StaticAuraKinds.SameBattlefieldFriendlyFilteredUnitCountToSourcePower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraSameBattlefieldFriendlyFilteredCountSourceCardSpecConsistency(
                        effectLabel,
                        sourceCardNo,
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        sourcePath,
                        "CoreRuleEngine.ResolveSameBattlefieldFriendlyFilteredUnitCountToSourcePowerBonus",
                        "object static aura source path",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        condition,
                        "SOURCE_AND_FRIENDLY_FILTERED_PUBLIC_UNITS_AT_SAME_BATTLEFIELD",
                        "object static aura condition",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        lifecycle,
                        "RECOMPUTED_FROM_CURRENT_SAME_BATTLEFIELD_FILTERED_FRIENDLY_UNIT_LOCATIONS",
                        "object static aura lifecycle",
                        errors);
                    return;
                }

                if ((string.Equals(effectKind, StaticAuraKinds.SourceObjectPowerWithSameLocationEnemy, StringComparison.Ordinal)
                    || effectKind == StaticAuraKinds.SourceObjectPowerWithFriendlyEquipment))
                {
                    if (sourceCardNo is null || !StaticAuraSpecRules.GetStaticAuras(sourceCardNo).Any(aura =>
                        aura.Kind == effectKind && StaticAuraSpecRules.IsSourceParticipantCountPowerStaticAura(aura)))
                        errors.Add($"{effectLabel} source card has no matching conditional participant aura");
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, sourcePath,
                        "CoreRuleEngine.ResolveSourceParticipantCountPowerStaticAuraBonus", "object static aura source path", errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, condition,
                        "SOURCE_PARTICIPANT_COUNT_POWER_SCOPE_MATCHES", "object static aura condition", errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(effectLabel, lifecycle,
                        "RECOMPUTED_FROM_CURRENT_SOURCE_PARTICIPANT_COUNT", "object static aura lifecycle", errors);
                    return;
                }

                if (string.Equals(effectKind, StaticAuraKinds.SourceSameLocationOtherFriendlyUnitPower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraSameLocationOtherFriendlySourceCardSpecConsistency(
                        effectLabel,
                        sourceCardNo,
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        sourcePath,
                        "CoreRuleEngine.ResolveSourceSameLocationOtherFriendlyUnitPowerBonus",
                        "object static aura source path",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        condition,
                        "SOURCE_AND_OTHER_FRIENDLY_PUBLIC_UNITS_AT_SAME_LOCATION",
                        "object static aura condition",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        lifecycle,
                        "RECOMPUTED_FROM_CURRENT_SAME_LOCATION_FRIENDLY_UNIT_LOCATIONS",
                        "object static aura lifecycle",
                        errors);
                    return;
                }

                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    effectKind,
                    "FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER",
                    "object static aura effect kind",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    sourcePath,
                    "CoreRuleEngine.ApplyFriendlyEquipmentStaticPowerRecompute",
                    "object static aura source path",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    condition,
                    "SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_PUBLIC_FIELD_EQUIPMENT_COUNT",
                    "object static aura condition",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    lifecycle,
                    "RECOMPUTED_FROM_CURRENT_AUTHORITATIVE_FIELD_STATE",
                    "object static aura lifecycle",
                    errors);
                return;
            }

            if (string.Equals(duration, "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD", StringComparison.Ordinal))
            {
                if (string.Equals(effectKind, StaticAuraKinds.FriendlyUnitsPower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraFriendlyUnitsSourceCardSpecConsistency(
                        effectLabel,
                        sourceCardNo,
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        sourcePath,
                        "CoreRuleEngine.ResolveFriendlyUnitsPowerBonus",
                        "object static aura source path",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        condition,
                        "SOURCE_PUBLIC_STATIC_AURA_AND_FRIENDLY_PUBLIC_UNITS",
                        "object static aura condition",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        lifecycle,
                        "DERIVED_FROM_CURRENT_PUBLIC_FIELD_FRIENDLY_UNIT_LOCATIONS",
                        "object static aura lifecycle",
                        errors);
                    return;
                }

                if (string.Equals(effectKind, StaticAuraKinds.FriendlyFilteredUnitsPower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraFriendlyFilteredSourceCardSpecConsistency(
                        effectLabel,
                        sourceCardNo,
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        sourcePath,
                        "CoreRuleEngine.ResolveFriendlyFilteredUnitsPowerBonus",
                        "object static aura source path",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        condition,
                        "SOURCE_PUBLIC_FIELD_UNIT_AND_FRIENDLY_FILTERED_PUBLIC_UNITS",
                        "object static aura condition",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        lifecycle,
                        "DERIVED_FROM_CURRENT_PUBLIC_FIELD_FILTERED_UNIT_LOCATIONS",
                        "object static aura lifecycle",
                        errors);
                    return;
                }

                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    effectKind,
                    "OTHER_FRIENDLY_UNITS_POWER",
                    "object static aura effect kind",
                    errors);
                ValidateContinuousEffectStaticAuraOtherFriendlySourceCardSpecConsistency(
                    effectLabel,
                    sourceCardNo,
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    sourcePath,
                    "CoreRuleEngine.ResolveOtherFriendlyUnitsPowerBonus",
                    "object static aura source path",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    condition,
                    "SOURCE_PUBLIC_FIELD_UNIT_AND_OTHER_FRIENDLY_PUBLIC_UNITS",
                    "object static aura condition",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    lifecycle,
                    "DERIVED_FROM_CURRENT_PUBLIC_FIELD_FRIENDLY_UNIT_LOCATIONS",
                    "object static aura lifecycle",
                    errors);
            }

            if (string.Equals(duration, "WHILE_SOURCE_ATTACKING_WITH_REQUIRED_ATTACKER_COUNT", StringComparison.Ordinal))
            {
                ValidateContinuousEffectStaticAuraSourceAttackingWithAnotherUnitSourceCardSpecConsistency(
                    effectLabel,
                    sourceCardNo,
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    effectKind,
                    "SOURCE_ATTACKING_WITH_ANOTHER_UNIT_POWER",
                    "object static aura effect kind",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    sourcePath,
                    "CoreRuleEngine.ResolveSourceAttackingWithAnotherUnitPowerBonus",
                    "object static aura source path",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    condition,
                    "SOURCE_ATTACKING_WITH_REQUIRED_ATTACKER_COUNT",
                    "object static aura condition",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    lifecycle,
                    "RECOMPUTED_FROM_CURRENT_BATTLE_ATTACKER_LOCATIONS",
                    "object static aura lifecycle",
                    errors);
                return;
            }

            if (string.Equals(duration, "WHILE_SOURCE_ATTACKING_OR_DEFENDING_ALONE", StringComparison.Ordinal))
            {
                ValidateContinuousEffectStaticAuraSourceLoneBattleSourceCardSpecConsistency(
                    effectLabel,
                    sourceCardNo,
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    effectKind,
                    "SOURCE_LONE_BATTLE_POWER",
                    "object static aura effect kind",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    sourcePath,
                    "CoreRuleEngine.ResolveSourceLoneBattlePowerBonus",
                    "object static aura source path",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    condition,
                    "SOURCE_ATTACKING_OR_DEFENDING_ALONE",
                    "object static aura condition",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    lifecycle,
                    "RECOMPUTED_FROM_CURRENT_BATTLE_PARTICIPANT_LOCATIONS",
                    "object static aura lifecycle",
                    errors);
                return;
            }

            if (string.Equals(duration, "WHILE_SOURCE_ATTACKING_READY_ENEMY_UNIT_BATTLEFIELD", StringComparison.Ordinal))
            {
                ValidateContinuousEffectStaticAuraSourceAttackingReadyEnemyUnitSourceCardSpecConsistency(
                    effectLabel,
                    sourceCardNo,
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    effectKind,
                    "SOURCE_ATTACKING_READY_ENEMY_UNIT_POWER",
                    "object static aura effect kind",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    sourcePath,
                    "CoreRuleEngine.ResolveSourceAttackingReadyEnemyUnitPowerBonus",
                    "object static aura source path",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    condition,
                    "SOURCE_ATTACKING_AND_READY_ENEMY_PUBLIC_UNITS_AT_SAME_BATTLEFIELD",
                    "object static aura condition",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    lifecycle,
                    "RECOMPUTED_FROM_CURRENT_SAME_BATTLEFIELD_READY_ENEMY_UNIT_LOCATIONS",
                    "object static aura lifecycle",
                    errors);
                return;
            }
        }

        if (string.Equals(scope, "BATTLEFIELD", StringComparison.Ordinal))
        {
            if (string.Equals(duration, "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", StringComparison.Ordinal))
            {
                if (string.Equals(effectKind, StaticAuraKinds.BattlefieldFilteredUnitsPower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraBattlefieldFilteredSourceCardSpecConsistency(
                        effectLabel,
                        sourceCardNo,
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        sourcePath,
                        "CoreRuleEngine.ResolveBattlefieldFilteredUnitsPowerBonus",
                        "battlefield static aura source path",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        condition,
                        "SOURCE_BATTLEFIELD_FILTERED_UNITS_POWER_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD",
                        "battlefield static aura condition",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        lifecycle,
                        "DERIVED_FROM_CURRENT_BATTLEFIELD_FILTERED_OBJECT_LOCATIONS",
                        "battlefield static aura lifecycle",
                        errors);
                    return;
                }

                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    effectKind,
                    "BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE",
                    "battlefield static aura effect kind",
                    errors);
                ValidateContinuousEffectStaticAuraSourceCardSpecConsistency(
                    effectLabel,
                    sourceCardNo,
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    sourcePath,
                    "CoreRuleEngine.ResolveBattlefieldAllUnitsPowerBonus",
                    "battlefield static aura source path",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    condition,
                    "SOURCE_BATTLEFIELD_ALL_UNITS_POWER_PLUS_ONE_AND_PARTICIPANT_UNIT_AT_BATTLEFIELD",
                    "battlefield static aura condition",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    lifecycle,
                    "DERIVED_FROM_CURRENT_BATTLEFIELD_OBJECT_LOCATIONS",
                    "battlefield static aura lifecycle",
                    errors);
                return;
            }

            if (string.Equals(duration, "WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", StringComparison.Ordinal))
            {
                if (string.Equals(effectKind, StaticAuraKinds.SameBattlefieldOtherFriendlyFilteredUnitsPower, StringComparison.Ordinal))
                {
                    ValidateContinuousEffectStaticAuraSameBattlefieldOtherFriendlyFilteredSourceCardSpecConsistency(
                        effectLabel,
                        sourceCardNo,
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        sourcePath,
                        "CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyFilteredUnitsPowerBonus",
                        "battlefield static aura source path",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        condition,
                        "SOURCE_AND_OTHER_FRIENDLY_FILTERED_PUBLIC_UNITS_AT_SAME_BATTLEFIELD",
                        "battlefield static aura condition",
                        errors);
                    ValidateContinuousEffectStaticAuraMetadataValue(
                        effectLabel,
                        lifecycle,
                        "DERIVED_FROM_CURRENT_SAME_BATTLEFIELD_FILTERED_FRIENDLY_UNIT_LOCATIONS",
                        "battlefield static aura lifecycle",
                        errors);
                    return;
                }

                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    effectKind,
                    "SAME_BATTLEFIELD_OTHER_FRIENDLY_UNITS_POWER_PLUS_ONE",
                    "battlefield static aura effect kind",
                    errors);
                ValidateContinuousEffectStaticAuraSameBattlefieldOtherFriendlySourceCardSpecConsistency(
                    effectLabel,
                    sourceCardNo,
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    sourcePath,
                    "CoreRuleEngine.ResolveSameBattlefieldOtherFriendlyUnitsPowerBonus",
                    "battlefield static aura source path",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    condition,
                    "SOURCE_AND_OTHER_FRIENDLY_PUBLIC_UNITS_AT_SAME_BATTLEFIELD",
                    "battlefield static aura condition",
                    errors);
                ValidateContinuousEffectStaticAuraMetadataValue(
                    effectLabel,
                    lifecycle,
                    "DERIVED_FROM_CURRENT_SAME_BATTLEFIELD_FRIENDLY_UNIT_LOCATIONS",
                    "battlefield static aura lifecycle",
                    errors);
            }
        }
    }

    private static void ValidateContinuousEffectStaticAuraSourceCardSpecConsistency(
        string effectLabel,
        string? sourceCardNo,
        List<string> errors)
    {
        if (sourceCardNo is null
            || StaticAuraSpecRules.GetStaticAuras(sourceCardNo)
                .Any(StaticAuraSpecRules.IsBattlefieldAllUnitsPowerStaticAura))
        {
            return;
        }

        errors.Add(
            $"{effectLabel} battlefield static aura source card no must reference a BehaviorSpec battlefield all-units power aura");
    }

    private static void ValidateContinuousEffectStaticAuraPowerDeltaConsistency(
        object? effectPayload,
        string effectLabel,
        string? scope,
        string? layer,
        string? duration,
        string? effectKind,
        string? sourceCardNo,
        int? powerDelta,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || duration is null
            || !powerDelta.HasValue
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !IsKnownContinuousEffectDuration(duration)
            || !string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        // A graveyard-count aura is not a fixed delta per field participant.
        // ValidateDynamicPowerAuraViews checks its exact amount
        // against the current authoritative effect, including zero.
        if (effectKind is CoreRuleEngine.EmpoweredUnitAuraEffect or StaticAuraKinds.SourceGraveyardCountPower or StaticAuraKinds.SourceControllerScorePower or StaticAuraKinds.SameLocationEnemyFilteredPower or CoreRuleEngine.BoonAmplifierEffect) return;

        if (string.Equals(scope, "OBJECT", StringComparison.Ordinal))
        {
            if (!IsObjectStaticAuraPowerDeltaValidatedDuration(duration))
            {
                return;
            }

            if (TryReadObjectValue(effectPayload, "participantObjectIds", out var participantObjectIdsPayload)
                && !IsNullSnapshotPayloadValue(participantObjectIdsPayload))
            {
                if (!TryReadObjectStringList(effectPayload, "participantObjectIds", out var participantObjectIds))
                {
                    return;
                }

                var expectedPowerDelta = ResolveObjectStaticAuraExpectedPowerDelta(
                    effectKind,
                    sourceCardNo,
                    participantObjectIds.Count);
                if (powerDelta.Value != expectedPowerDelta)
                {
                    errors.Add(
                        $"{effectLabel} object static aura power delta {powerDelta.Value} must equal expected power delta {expectedPowerDelta} for participant object count {participantObjectIds.Count}");
                }

                return;
            }

            if (powerDelta.Value != 0)
            {
                errors.Add($"{effectLabel} object static aura power delta {powerDelta.Value} must be 0 without participant object ids");
            }

            return;
        }

        if (string.Equals(scope, "BATTLEFIELD", StringComparison.Ordinal)
            && (string.Equals(duration, "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", StringComparison.Ordinal)
                || string.Equals(duration, "WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", StringComparison.Ordinal))
            && powerDelta.Value != 1)
        {
            errors.Add($"{effectLabel} battlefield static aura power delta {powerDelta.Value} must be 1");
        }
    }

    private static int ResolveObjectStaticAuraExpectedPowerDelta(
        string? effectKind,
        string? sourceCardNo,
        int participantCount)
    {
        var staticAuras = sourceCardNo is null
            ? Array.Empty<StaticAuraSpec>()
            : StaticAuraSpecRules.GetStaticAuras(sourceCardNo);

        if ((string.Equals(effectKind, StaticAuraKinds.SourceObjectPowerWithSameLocationEnemy, StringComparison.Ordinal)
                    || effectKind == StaticAuraKinds.SourceObjectPowerWithFriendlyEquipment
                    || effectKind == StaticAuraKinds.SourceBattlefieldFriendlyObjectThresholdPower)
            && staticAuras.FirstOrDefault(aura => aura.Kind == effectKind && StaticAuraSpecRules.IsSourceParticipantCountPowerStaticAura(aura)) is { } enemyAura)
            return participantCount >= enemyAura.RequiredParticipantCount.GetValueOrDefault(1) ? enemyAura.PowerDeltaPerParticipant : 0;

        if (string.Equals(effectKind, StaticAuraKinds.SourceSameLocationOtherFriendlyUnitPower, StringComparison.Ordinal)
            && staticAuras.FirstOrDefault(StaticAuraSpecRules.IsSameLocationOtherFriendlyUnitPowerStaticAura) is { } sameLocationAura)
        {
            return participantCount >= sameLocationAura.RequiredParticipantCount.GetValueOrDefault(1)
                ? sameLocationAura.PowerDeltaPerParticipant
                : 0;
        }

        if (string.Equals(effectKind, StaticAuraKinds.SourceAttackingWithAnotherUnitPower, StringComparison.Ordinal)
            && staticAuras.FirstOrDefault(StaticAuraSpecRules.IsSourceAttackingWithAnotherUnitPowerStaticAura) is { } attackingWithAnotherUnitAura)
        {
            return attackingWithAnotherUnitAura.PowerDeltaPerParticipant;
        }

        if (string.Equals(effectKind, StaticAuraKinds.SourceLoneBattlePower, StringComparison.Ordinal)
            && staticAuras.FirstOrDefault(StaticAuraSpecRules.IsSourceLoneBattlePowerStaticAura) is { } loneBattleAura)
        {
            return loneBattleAura.PowerDeltaPerParticipant;
        }

        if (string.Equals(effectKind, StaticAuraKinds.SourceAttackingReadyEnemyUnitPower, StringComparison.Ordinal)
            && staticAuras.FirstOrDefault(StaticAuraSpecRules.IsSourceAttackingReadyEnemyUnitPowerStaticAura) is { } readyEnemyUnitAura)
        {
            return readyEnemyUnitAura.PowerDeltaPerParticipant;
        }

        return participantCount * ResolveObjectStaticAuraPowerDeltaPerParticipant(effectKind, sourceCardNo);
    }

    private static bool IsObjectStaticAuraPowerDeltaValidatedDuration(string duration)
    {
        return string.Equals(duration, "WHILE_SOURCE_ON_PUBLIC_FIELD", StringComparison.Ordinal)
            || string.Equals(duration, "WHILE_SOURCE_ATTACKING_WITH_REQUIRED_ATTACKER_COUNT", StringComparison.Ordinal)
            || string.Equals(duration, "WHILE_SOURCE_ATTACKING_OR_DEFENDING_ALONE", StringComparison.Ordinal)
            || string.Equals(duration, "WHILE_SOURCE_ATTACKING_READY_ENEMY_UNIT_BATTLEFIELD", StringComparison.Ordinal);
    }

    private static int ResolveObjectStaticAuraPowerDeltaPerParticipant(string? effectKind, string? sourceCardNo)
    {
        if (sourceCardNo is null || effectKind is null)
        {
            return 1;
        }

        var staticAuras = StaticAuraSpecRules.GetStaticAuras(sourceCardNo);
        if (effectKind == StaticAuraKinds.FriendlyFilteredUnitCountToSourcePower
            && staticAuras.FirstOrDefault(a => a.Kind == effectKind) is { } controlledCount)
            return controlledCount.PowerDeltaPerParticipant;

        if (effectKind == StaticAuraKinds.SourceOtherSameNameUnitCountPower
            && staticAuras.FirstOrDefault(a => a.Kind == effectKind
                && StaticAuraSpecRules.IsSourceParticipantCountPowerStaticAura(a)) is { } sameNameAura)
        {
            return sameNameAura.PowerDeltaPerParticipant;
        }

        if (string.Equals(effectKind, StaticAuraKinds.SourceObjectFilteredPower, StringComparison.Ordinal)
            && staticAuras.FirstOrDefault(StaticAuraSpecRules.IsSourceObjectFilteredPowerStaticAura) is { } sourceObjectFilteredAura)
        {
            return sourceObjectFilteredAura.PowerDeltaPerParticipant;
        }

        if (string.Equals(effectKind, StaticAuraKinds.SourceObjectPower, StringComparison.Ordinal)
            && staticAuras.FirstOrDefault(StaticAuraSpecRules.IsSourceObjectUnfilteredPowerStaticAura) is { } sourceObjectAura)
        {
            return sourceObjectAura.PowerDeltaPerParticipant;
        }

        if (string.Equals(effectKind, StaticAuraKinds.SameBattlefieldFriendlyFilteredUnitCountToSourcePower, StringComparison.Ordinal)
            && staticAuras.FirstOrDefault(StaticAuraSpecRules.IsSameBattlefieldFriendlyFilteredUnitCountToSourcePowerStaticAura) is { } sameBattlefieldCountAura)
        {
            return sameBattlefieldCountAura.PowerDeltaPerParticipant;
        }

        if ((string.Equals(effectKind, StaticAuraKinds.SourceObjectPowerWithSameLocationEnemy, StringComparison.Ordinal)
                    || effectKind == StaticAuraKinds.SourceObjectPowerWithFriendlyEquipment)
            && staticAuras.FirstOrDefault(aura => aura.Kind == effectKind && StaticAuraSpecRules.IsSourceParticipantCountPowerStaticAura(aura)) is { } enemyAura)
            return enemyAura.PowerDeltaPerParticipant;

        if (string.Equals(effectKind, StaticAuraKinds.SourceSameLocationOtherFriendlyUnitPower, StringComparison.Ordinal)
            && staticAuras.FirstOrDefault(StaticAuraSpecRules.IsSameLocationOtherFriendlyUnitPowerStaticAura) is { } sameLocationAura)
        {
            return sameLocationAura.PowerDeltaPerParticipant;
        }

        if (string.Equals(effectKind, StaticAuraKinds.FriendlyFieldEquipmentCountToSourceUnitPower, StringComparison.Ordinal)
            && staticAuras.FirstOrDefault(StaticAuraSpecRules.IsFriendlyEquipmentCountToSourcePowerStaticAura) is { } friendlyEquipmentAura)
        {
            return friendlyEquipmentAura.PowerDeltaPerParticipant;
        }

        return 1;
    }

    private static void ValidateContinuousEffectStaticAuraBattlefieldPowerScalarConsistency(
        string effectLabel,
        string? scope,
        string? layer,
        string? duration,
        int? powerDelta,
        int? basePower,
        int? effectivePower,
        List<string> errors)
    {
        if (scope is null
            || layer is null
            || duration is null
            || !powerDelta.HasValue
            || !basePower.HasValue
            || !effectivePower.HasValue
            || !IsKnownContinuousEffectScope(scope)
            || !IsKnownContinuousEffectLayer(layer)
            || !IsKnownContinuousEffectDuration(duration)
            || !string.Equals(layer, ContinuousEffectLayers.StaticAura, StringComparison.Ordinal)
            || !string.Equals(scope, "BATTLEFIELD", StringComparison.Ordinal)
            || (!string.Equals(duration, "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", StringComparison.Ordinal)
                && !string.Equals(duration, "WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD", StringComparison.Ordinal))
            || !IsContinuousEffectLayerScopeValid(scope, layer))
        {
            return;
        }

        var expectedEffectivePower = basePower.Value + powerDelta.Value;
        if (effectivePower.Value == expectedEffectivePower)
        {
            return;
        }

        errors.Add(
            $"{effectLabel} battlefield static aura effective power {effectivePower.Value} must equal base power {basePower.Value} plus power delta {powerDelta.Value}");
    }

    private static void ValidateSpectatorTriggerQueuePayloads(
        IReadOnlyDictionary<string, object?> timing,
        MatchState authoritativeState,
        List<string> errors)
    {
        if (!timing.TryGetValue("triggerQueue", out var triggerQueuePayload)
            || IsNullSnapshotPayloadValue(triggerQueuePayload))
        {
            errors.Add("spectator replay frame timing trigger queue is required");
            if (authoritativeState.TriggerQueue.Count > 0)
            {
                AddSpectatorTriggerQueueCountMismatch(
                    spectatorCount: 0,
                    authoritativeCount: authoritativeState.TriggerQueue.Count,
                    errors);
            }

            ReportMissingSpectatorTriggerQueueAuthoritativeKeySet(authoritativeState.TriggerQueue, errors);
            return;
        }

        if (!TryReadObjectListValue(triggerQueuePayload, out var spectatorTriggers))
        {
            errors.Add("spectator replay frame timing trigger queue payload is required");
            if (authoritativeState.TriggerQueue.Count > 0)
            {
                AddSpectatorTriggerQueueCountMismatch(
                    spectatorCount: 0,
                    authoritativeCount: authoritativeState.TriggerQueue.Count,
                    errors);
            }

            ReportMissingSpectatorTriggerQueueAuthoritativeKeySet(authoritativeState.TriggerQueue, errors);
            return;
        }

        ValidateLastBreathUnitCreationViews(spectatorTriggers, errors);
        ValidateLastBreathRuneCallViews(spectatorTriggers, errors);
        ValidateLastBreathDrawViews(spectatorTriggers, errors);
        ValidateLastBreathBattlefieldDamageViews(spectatorTriggers, errors);

        var authoritativeTriggers = authoritativeState.TriggerQueue;
        var validateAuthoritativeParity = spectatorTriggers.Count == authoritativeTriggers.Count;
        if (!validateAuthoritativeParity)
        {
            AddSpectatorTriggerQueueCountMismatch(
                spectatorTriggers.Count,
                authoritativeTriggers.Count,
                errors);
        }

        var seatPlayerIds = BuildNormalizedPlayerIdSet(authoritativeState.Seats.Keys);
        var knownObjectIds = BuildAuthoritativeStateKnownObjectIds(authoritativeState);
        var objectCardNos = BuildAuthoritativeStateObjectCardNoIndex(authoritativeState);
        var objectPowers = BuildAuthoritativeStateObjectPowerIndex(authoritativeState);
        var objectControllers = BuildAuthoritativeStateObjectControllerIndex(authoritativeState);
        var objectFaceDowns = BuildAuthoritativeStateObjectFaceDownIndex(authoritativeState);
        var objectTags = BuildAuthoritativeStateObjectTagIndex(authoritativeState);
        var battlefieldStateObjectIds = BuildAuthoritativeStateBattlefieldStateObjectIds(authoritativeState);
        var playerBattlefieldObjectIdsByPlayer =
            BuildAuthoritativeStatePlayerBattlefieldObjectIdsByPlayer(authoritativeState.PlayerZones);
        var playerBaseObjectIdsByPlayer =
            BuildAuthoritativeStatePlayerBaseObjectIdsByPlayer(authoritativeState.PlayerZones);
        var playerGraveyardObjectIdsByPlayer =
            BuildAuthoritativeStatePlayerGraveyardObjectIdsByPlayer(authoritativeState.PlayerZones);
        var playerFieldObjectIdsByPlayer =
            BuildAuthoritativeStatePlayerFieldObjectIdsByPlayer(authoritativeState.PlayerZones);
        var seenTriggerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spectatorTrigger in spectatorTriggers)
        {
            if (!IsSnapshotPlayerPayloadObject(spectatorTrigger))
            {
                errors.Add("spectator replay frame timing trigger queue item payload is required");
                continue;
            }

            ValidateSnapshotPayloadObjectPropertyNames(
                spectatorTrigger,
                "spectator replay frame timing trigger queue item",
                errors);
            ValidateSpectatorTriggerQueuePayloadValues(
                spectatorTrigger,
                seenTriggerIds,
                seatPlayerIds,
                knownObjectIds,
                objectCardNos,
                objectPowers,
                objectControllers,
                objectFaceDowns,
                objectTags,
                authoritativeState.ObjectLocations,
                playerBattlefieldObjectIdsByPlayer,
                playerBaseObjectIdsByPlayer,
                playerGraveyardObjectIdsByPlayer,
                playerFieldObjectIdsByPlayer,
                battlefieldStateObjectIds,
                authoritativeState.Tick,
                authoritativeState.TurnNumber,
                authoritativeState,
                errors);
        }

        var authoritativeTriggersById = BuildAuthoritativeTriggerQueueById(authoritativeTriggers);

        ValidateSpectatorTriggerQueueAuthoritativeKeySet(
            spectatorTriggers,
            authoritativeTriggersById,
            errors);
        ValidateSpectatorTriggerQueueKeyedAuthoritativeValues(
            spectatorTriggers,
            authoritativeTriggersById,
            authoritativeState,
            errors);

        if (!validateAuthoritativeParity)
        {
            return;
        }

        var triggerIdsMatch = true;
        var controllerIdsMatch = true;
        var sourceObjectIdsMatch = true;
        var sourceVisibilitiesMatch = true;
        var effectKindsMatch = true;
        var triggeredEventKindsMatch = true;

        for (var index = 0; index < authoritativeTriggers.Count; index++)
        {
            var spectatorTrigger = spectatorTriggers[index];
            if (!IsSnapshotPlayerPayloadObject(spectatorTrigger))
            {
                continue;
            }

            var authoritativeTrigger = authoritativeTriggers[index];
            var hiddenSource = IsHiddenTriggerSourceForSpectator(authoritativeState, authoritativeTrigger);
            if (MatchStateHasher.HashValue(ReadMightySkillView(spectatorTrigger)) != MatchStateHasher.HashValue(CoreRuleEngine.PublicMightySkill(authoritativeTrigger.MightySkill))) errors.Add("spectator captured Mighty transition differs from authoritative state");
            if (MatchStateHasher.HashValue(ReadBattlefieldConquestMightView(spectatorTrigger)) != MatchStateHasher.HashValue(authoritativeTrigger.BattlefieldConquestMight)) errors.Add("spectator captured conquest Might differs from authoritative state");
            if (MatchStateHasher.HashValue(ReadBattlefieldConquestMillView(spectatorTrigger)) != MatchStateHasher.HashValue(CoreRuleEngine.PublicBattlefieldConquestMill(authoritativeTrigger.BattlefieldConquestMill))) errors.Add("conquest mill trigger view differs from authoritative state");
            if (MatchStateHasher.HashValue(ReadLastBreathMightView(spectatorTrigger)) != MatchStateHasher.HashValue(authoritativeTrigger.LastBreathMight)) errors.Add("spectator captured last-breath Might differs from authoritative state");
            if (MatchStateHasher.HashValue(ReadCapturedInsightView(spectatorTrigger)) != MatchStateHasher.HashValue(CapturedInsight(authoritativeTrigger.InsightContext))) errors.Add($"spectator {CapturedInsightLabel(authoritativeTrigger.InsightContext)} differs from authoritative state");
            CompareHandDiscardObserverView(spectatorTrigger, authoritativeTrigger.HandDiscardObserver, errors, "spectator");
            CompareMovementObserverView(spectatorTrigger, authoritativeTrigger.MovementObserver, errors, "spectator");
            if (MatchStateHasher.HashValue(ReadDeathObserverView(spectatorTrigger)) != MatchStateHasher.HashValue(CoreRuleEngine.PublicDeathObserver(authoritativeTrigger.DeathObserver))) errors.Add("spectator captured death observer differs from authoritative state");
            if (MatchStateHasher.HashValue(ReadDelayedDestructionView(spectatorTrigger)) != MatchStateHasher.HashValue(authoritativeTrigger.DelayedDestruction)) errors.Add("spectator captured delayed destruction differs from authoritative state");
            CompareDestructionFamilyView(spectatorTrigger, authoritativeTrigger.DamageDestruction, authoritativeTrigger.GraveyardPlay, errors);
            var privateBattleSource = CapturedBattleSourceIsPrivate(authoritativeState, authoritativeTrigger);
            var expectedSourceObjectId = hiddenSource || privateBattleSource ? "HIDDEN" : authoritativeTrigger.SourceObjectId;
            var expectedSourceVisibility = hiddenSource || privateBattleSource ? "HIDDEN" : "VISIBLE";
            var expectedEffectKind = hiddenSource ? "HIDDEN" : authoritativeTrigger.EffectKind;

            if (!TryReadObjectString(spectatorTrigger, "triggerId", out var triggerId)
                || !string.Equals(triggerId, authoritativeTrigger.TriggerId, StringComparison.Ordinal))
            {
                triggerIdsMatch = false;
            }

            if (!TryReadObjectString(spectatorTrigger, "controllerId", out var controllerId)
                || !string.Equals(controllerId, authoritativeTrigger.ControllerId, StringComparison.Ordinal))
            {
                controllerIdsMatch = false;
            }

            if (!TryReadObjectString(spectatorTrigger, "sourceObjectId", out var sourceObjectId)
                || !string.Equals(sourceObjectId, expectedSourceObjectId, StringComparison.Ordinal))
            {
                sourceObjectIdsMatch = false;
            }

            if (!TryReadObjectString(spectatorTrigger, "sourceVisibility", out var sourceVisibility)
                || !string.Equals(sourceVisibility, expectedSourceVisibility, StringComparison.Ordinal))
            {
                sourceVisibilitiesMatch = false;
            }

            if (!TryReadObjectString(spectatorTrigger, "effectKind", out var effectKind)
                || !string.Equals(effectKind, expectedEffectKind, StringComparison.Ordinal))
            {
                effectKindsMatch = false;
            }

            if (!TryReadObjectString(spectatorTrigger, "triggeredByEventKind", out var triggeredEventKind)
                || !string.Equals(triggeredEventKind, authoritativeTrigger.TriggeredByEventKind, StringComparison.Ordinal))
            {
                triggeredEventKindsMatch = false;
            }
        }

        if (!triggerIdsMatch)
        {
            AddSpectatorTriggerQueueStringAggregateMismatch(
                "ids",
                authoritativeTriggers.Select(trigger => trigger.TriggerId),
                spectatorTriggers,
                "triggerId",
                errors);
        }

        if (!controllerIdsMatch)
        {
            AddSpectatorTriggerQueueStringAggregateMismatch(
                "controller ids",
                authoritativeTriggers.Select(trigger => trigger.ControllerId),
                spectatorTriggers,
                "controllerId",
                errors);
        }

        if (!sourceObjectIdsMatch)
        {
            AddSpectatorTriggerQueueStringAggregateMismatch(
                "source object ids",
                authoritativeTriggers.Select(trigger =>
                    IsHiddenTriggerSourceForSpectator(authoritativeState, trigger)
                        ? "HIDDEN"
                        : trigger.SourceObjectId),
                spectatorTriggers,
                "sourceObjectId",
                errors);
        }

        if (!sourceVisibilitiesMatch)
        {
            AddSpectatorTriggerQueueStringAggregateMismatch(
                "source visibilities",
                authoritativeTriggers.Select(trigger =>
                    IsHiddenTriggerSourceForSpectator(authoritativeState, trigger)
                        ? "HIDDEN"
                        : "VISIBLE"),
                spectatorTriggers,
                "sourceVisibility",
                errors);
        }

        if (!effectKindsMatch)
        {
            AddSpectatorTriggerQueueStringAggregateMismatch(
                "effect kinds",
                authoritativeTriggers.Select(trigger =>
                    IsHiddenTriggerSourceForSpectator(authoritativeState, trigger)
                        ? "HIDDEN"
                        : trigger.EffectKind),
                spectatorTriggers,
                "effectKind",
                errors);
        }

        if (!triggeredEventKindsMatch)
        {
            AddSpectatorTriggerQueueStringAggregateMismatch(
                "triggered event kinds",
                authoritativeTriggers.Select(trigger => trigger.TriggeredByEventKind),
                spectatorTriggers,
                "triggeredByEventKind",
                errors);
        }
    }

    private static void ValidateSpectatorTriggerQueueKeyedAuthoritativeValues(
        IReadOnlyList<object?> triggerPayloads,
        IReadOnlyDictionary<string, TriggerQueueItemState> authoritativeTriggersById,
        MatchState authoritativeState,
        List<string> errors)
    {
        foreach (var triggerPayload in triggerPayloads)
        {
            if (!IsSnapshotPlayerPayloadObject(triggerPayload)
                || !TryReadObjectString(triggerPayload, "triggerId", out var triggerId)
                || string.IsNullOrWhiteSpace(triggerId))
            {
                continue;
            }

            var normalizedTriggerId = triggerId.Trim();
            if (!authoritativeTriggersById.TryGetValue(normalizedTriggerId, out var authoritativeTrigger))
            {
                continue;
            }

            var hiddenSource = IsHiddenTriggerSourceForSpectator(authoritativeState, authoritativeTrigger);
            if (MatchStateHasher.HashValue(ReadMightySkillView(triggerPayload)) != MatchStateHasher.HashValue(CoreRuleEngine.PublicMightySkill(authoritativeTrigger.MightySkill))) errors.Add("snapshot captured Mighty transition differs from authoritative state");
            if (MatchStateHasher.HashValue(ReadBattlefieldConquestMightView(triggerPayload)) != MatchStateHasher.HashValue(authoritativeTrigger.BattlefieldConquestMight)) errors.Add("snapshot captured conquest Might differs from authoritative state");
            if (MatchStateHasher.HashValue(ReadBattlefieldConquestMillView(triggerPayload)) != MatchStateHasher.HashValue(CoreRuleEngine.PublicBattlefieldConquestMill(authoritativeTrigger.BattlefieldConquestMill))) errors.Add("conquest mill trigger view differs from authoritative state");
            if (MatchStateHasher.HashValue(ReadLastBreathMightView(triggerPayload)) != MatchStateHasher.HashValue(authoritativeTrigger.LastBreathMight)) errors.Add("snapshot captured last-breath Might differs from authoritative state");
            if (MatchStateHasher.HashValue(ReadCapturedInsightView(triggerPayload)) != MatchStateHasher.HashValue(CapturedInsight(authoritativeTrigger.InsightContext))) errors.Add($"snapshot {CapturedInsightLabel(authoritativeTrigger.InsightContext)} differs from authoritative state");
            CompareHandDiscardObserverView(triggerPayload, authoritativeTrigger.HandDiscardObserver, errors, "spectator");
            CompareMovementObserverView(triggerPayload, authoritativeTrigger.MovementObserver, errors, "spectator");
            if (MatchStateHasher.HashValue(ReadDeathObserverView(triggerPayload)) != MatchStateHasher.HashValue(CoreRuleEngine.PublicDeathObserver(authoritativeTrigger.DeathObserver))) errors.Add("spectator captured death observer differs from authoritative state");
            if (MatchStateHasher.HashValue(ReadDelayedDestructionView(triggerPayload)) != MatchStateHasher.HashValue(authoritativeTrigger.DelayedDestruction)) errors.Add("spectator captured delayed destruction differs from authoritative state");
            CompareDestructionFamilyView(triggerPayload, authoritativeTrigger.DamageDestruction, authoritativeTrigger.GraveyardPlay, errors);
            var privateBattleSource = CapturedBattleSourceIsPrivate(authoritativeState, authoritativeTrigger);
            var expectedSourceObjectId = hiddenSource || privateBattleSource ? "HIDDEN" : authoritativeTrigger.SourceObjectId;
            var expectedSourceVisibility = hiddenSource || privateBattleSource ? "HIDDEN" : "VISIBLE";
            var expectedEffectKind = hiddenSource ? "HIDDEN" : authoritativeTrigger.EffectKind;

            ValidateSpectatorTriggerQueueKeyedRequiredStringValue(
                triggerPayload,
                "controllerId",
                "controller id",
                authoritativeTrigger.ControllerId,
                normalizedTriggerId,
                errors);
            ValidateSpectatorTriggerQueueKeyedRequiredStringValue(
                triggerPayload,
                "sourceObjectId",
                "source object id",
                expectedSourceObjectId,
                normalizedTriggerId,
                errors);
            ValidateSpectatorTriggerQueueKeyedRequiredStringValue(
                triggerPayload,
                "sourceVisibility",
                "source visibility",
                expectedSourceVisibility,
                normalizedTriggerId,
                errors);
            ValidateSpectatorTriggerQueueKeyedRequiredStringValue(
                triggerPayload,
                "effectKind",
                "effect kind",
                expectedEffectKind,
                normalizedTriggerId,
                errors);
            ValidateSpectatorTriggerQueueKeyedRequiredStringValue(
                triggerPayload,
                "triggeredByEventKind",
                "triggered event kind",
                authoritativeTrigger.TriggeredByEventKind,
                normalizedTriggerId,
                errors);
        }
    }

    private static void ValidateSpectatorTriggerQueuePayloadValues(
        object? triggerPayload,
        HashSet<string> seenTriggerIds,
        IReadOnlySet<string> seatPlayerIds,
        IReadOnlySet<string> knownObjectIds,
        IReadOnlyDictionary<string, string?> objectCardNos,
        IReadOnlyDictionary<string, int> objectPowers,
        IReadOnlyDictionary<string, string> objectControllers,
        IReadOnlyDictionary<string, bool> objectFaceDowns,
        IReadOnlyDictionary<string, IReadOnlySet<string>> objectTags,
        IReadOnlyDictionary<string, ObjectLocationState> objectLocations,
        IReadOnlyDictionary<string, IReadOnlySet<string>> playerBattlefieldObjectIdsByPlayer,
        IReadOnlyDictionary<string, IReadOnlySet<string>> playerBaseObjectIdsByPlayer,
        IReadOnlyDictionary<string, IReadOnlySet<string>> playerGraveyardObjectIdsByPlayer,
        IReadOnlyDictionary<string, IReadOnlySet<string>> playerFieldObjectIdsByPlayer,
        IReadOnlySet<string> battlefieldStateObjectIds,
        long currentTick,
        int currentTurnNumber,
        MatchState authoritativeState,
        List<string> errors)
    {
        const string payloadLabel = "spectator replay frame timing trigger queue item";
        ValidateUnitConquestReturnPrivateContextAbsent(triggerPayload, payloadLabel, errors);
        ValidateBattlefieldConquestChoiceView(triggerPayload, authoritativeState, false, payloadLabel, errors);
        var triggerId = ValidateSnapshotPayloadRequiredStringValue(
            triggerPayload,
            "triggerId",
            payloadLabel,
            "trigger id",
            errors);
        if (triggerId is not null && !seenTriggerIds.Add(triggerId))
        {
            errors.Add($"{payloadLabel} trigger id {triggerId} is duplicated");
        }

        var controllerId = ValidateSnapshotPayloadRequiredStringValue(
            triggerPayload,
            "controllerId",
            payloadLabel,
            "controller id",
            errors);
        var sourceObjectId = ValidateSnapshotPayloadRequiredStringValue(
            triggerPayload,
            "sourceObjectId",
            payloadLabel,
            "source object id",
            errors);
        var sourceVisibility = ValidateTriggerQueueSourceVisibilityValue(
            triggerPayload,
            payloadLabel,
            errors);
        var effectKind = ValidateSnapshotPayloadRequiredStringValue(
            triggerPayload,
            "effectKind",
            payloadLabel,
            "effect kind",
            errors);
        var triggeredEventKind = ValidateSnapshotPayloadRequiredStringValue(
            triggerPayload,
            "triggeredByEventKind",
            payloadLabel,
            "triggered event kind",
            errors);
        ValidateTriggerQueuePromptFieldAbsence(triggerPayload, payloadLabel, errors);
        ValidateTriggerQueueIdentityRedactionSentinelAbsence(
            payloadLabel,
            triggerId,
            controllerId,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueTriggeredEventKindKnownValue(payloadLabel, triggeredEventKind, errors);
        ValidateTriggerQueueControllerPlayerMembership(
            payloadLabel,
            controllerId,
            seatPlayerIds,
            "seats",
            errors);

        ValidateTriggerQueueSourceRedactionConsistency(
            payloadLabel,
            sourceObjectId,
            sourceVisibility,
            effectKind,
            errors,
            HasValidatedPublicDelayedEmpowerEffect(authoritativeState, triggerId, effectKind)
                    || HasValidatedPublicBombRebuildEffect(authoritativeState, triggerId, effectKind)
                    || HasValidatedPublicLastBreathUnitCreationEffect(authoritativeState, triggerId, effectKind)
                    || (HasValidatedPublicLastBreathRuneCallEffect(authoritativeState, triggerId, effectKind) || (HasValidatedPublicLastBreathDrawEffect(authoritativeState, triggerId, effectKind) || HasValidatedPublicLastBreathBattlefieldDamageEffect(authoritativeState, triggerId, effectKind)))
                    || HasValidatedPublicMovementDiscardEffect(authoritativeState, triggerId, effectKind)
                    || HasValidatedPublicUnitConquestReturnEffect(authoritativeState, triggerId, effectKind));
        ValidateTriggerQueueBlueSentinelDelayedResourceContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            sourceVisibility,
            objectControllers,
            "authoritative state object registry",
            objectCardNos,
            "authoritative state object registry",
            objectFaceDowns,
            "authoritative state object registry",
            objectTags,
            "authoritative state object registry",
            objectLocations,
            "authoritative state object locations",
            playerBattlefieldObjectIdsByPlayer,
            "authoritative state player zones",
            battlefieldStateObjectIds,
            "authoritative state battlefield states",
            currentTurnNumber,
            effectKind,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueJhinMovementResourceContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            objectControllers,
            "authoritative state object registry",
            objectCardNos,
            "authoritative state object registry",
            objectFaceDowns,
            "authoritative state object registry",
            objectTags,
            "authoritative state object registry",
            objectLocations,
            "authoritative state object locations",
            battlefieldStateObjectIds,
            "authoritative state battlefield states",
            effectKind,
            triggeredEventKind,
            currentTick,
            errors);
        ValidateTriggerQueueKogmawLastBreathContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            sourceVisibility,
            objectControllers,
            "authoritative state object registry",
            knownObjectIds,
            "object registry",
            objectCardNos,
            "authoritative state object registry",
            objectFaceDowns,
            "authoritative state object registry",
            objectTags,
            "authoritative state object registry",
            objectLocations,
            "authoritative state object locations",
            battlefieldStateObjectIds,
            "authoritative state battlefield states",
            effectKind,
            triggeredEventKind,
            errors, authoritativeState, string.Empty);
        ValidateTriggerQueueOgsLuxHighCostSpellContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            sourceVisibility,
            objectControllers,
            "authoritative state object registry",
            objectCardNos,
            "authoritative state object registry",
            objectFaceDowns,
            "authoritative state object registry",
            objectTags,
            "authoritative state object registry",
            playerFieldObjectIdsByPlayer,
            "authoritative state player zones",
            objectLocations,
            "authoritative state object locations",
            effectKind,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueTeemoOnPlaySelfPowerContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            sourceVisibility,
            objectControllers,
            "authoritative state object registry",
            objectCardNos,
            "authoritative state object registry",
            objectFaceDowns,
            "authoritative state object registry",
            objectTags,
            "authoritative state object registry",
            playerBaseObjectIdsByPlayer,
            "authoritative state player zones",
            objectLocations,
            "authoritative state object locations",
            effectKind,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueWatchfulSentinelLastBreathDrawContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueUnsungHeroLastBreathPowerfulDrawContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueScoutingWarhawkLastBreathCallRuneContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            errors,
            (HasValidatedPublicLastBreathRuneCallEffect(authoritativeState, triggerId, effectKind) || (HasValidatedPublicLastBreathDrawEffect(authoritativeState, triggerId, effectKind) || HasValidatedPublicLastBreathBattlefieldDamageEffect(authoritativeState, triggerId, effectKind))));
        ValidateTriggerQueueSadPoroLastBreathDrawContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueLoyalPoroLastBreathDrawContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueHonestBrokerLastBreathCreateGoldContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueMechanicalTricksterLastBreathCreateMinionsContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueUndercoverAgentLastBreathContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueIroncladVanguardLastBreathCreateRobotsContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueMuddyDredgerLastBreathCreateWarhawkContext(
            payloadLabel,
            triggerId,
            sourceVisibility,
            effectKind,
            triggeredEventKind,
            errors);
        ValidateTriggerQueueStandardLastBreathSourceObjectIdContext(
            payloadLabel,
            triggerId,
            controllerId,
            sourceObjectId,
            effectKind,
            objectControllers,
            "authoritative state object registry",
            objectCardNos,
            "authoritative state object registry",
            objectPowers,
            "authoritative state object registry",
            objectFaceDowns,
            "authoritative state object registry",
            objectTags,
            "authoritative state object registry",
            objectLocations,
            "authoritative state object locations",
            playerBaseObjectIdsByPlayer,
            "authoritative state player zones",
            playerGraveyardObjectIdsByPlayer,
            "authoritative state player zones",
            errors,
            (HasValidatedPublicLastBreathUnitCreationEffect(authoritativeState, triggerId, effectKind) || (HasValidatedPublicLastBreathRuneCallEffect(authoritativeState, triggerId, effectKind) || (HasValidatedPublicLastBreathDrawEffect(authoritativeState, triggerId, effectKind) || HasValidatedPublicLastBreathBattlefieldDamageEffect(authoritativeState, triggerId, effectKind)))));
        var ceasedInsight = ValidateCapturedInsightView(triggerPayload,effectKind,controllerId,sourceObjectId,errors);
        if (!ValidateReflexiveDuelView(triggerPayload,effectKind,errors)
            && !ValidateHandDiscardObserverView(triggerPayload,effectKind,controllerId,sourceObjectId,errors)
            && !ValidateMovementObserverView(triggerPayload,effectKind,controllerId,sourceObjectId,errors)
            && !ValidateDeathObserverView(triggerPayload,effectKind,controllerId,sourceObjectId,errors)
            && !ValidateDestructionFamilyView(triggerPayload,effectKind,controllerId,sourceObjectId,errors)
            && !ceasedInsight
            && !HasValidatedCeasedLastBreathSource(authoritativeState, triggerId, controllerId, sourceObjectId, effectKind)
            && !HasValidatedCeasedBeginningRuneSource(authoritativeState, triggerId, controllerId, sourceObjectId, effectKind)
            && !HasValidatedUnitConquestReturnQueueSource(authoritativeState, triggerId, controllerId, sourceObjectId, effectKind))
        ValidateTriggerQueueVisibleSourceObjectMembership(
            payloadLabel,
            sourceObjectId,
            sourceVisibility,
            knownObjectIds,
            "object registry",
            errors);
    }

    private static void ValidatePendingHandChoicePayloadValues(
        object? choicePayload,
        string payloadLabel,
        Func<string, bool> isKnownChoiceState,
        List<string> errors)
    {
        if (TryReadObjectValue(choicePayload, "lastBreathDraw", out _))
            errors.Add("public hand choice exposes a private last-breath draw birth receipt");
        if (EnumerateSnapshotPayloadObjectPropertyNames(choicePayload)
            .Any(name => string.Equals(name, "battlefieldConquestDiscardDraw", StringComparison.OrdinalIgnoreCase)))
            errors.Add("public hand choice exposes a private battlefield conquest binding");
        ValidateSnapshotPayloadRequiredStringValue(
            choicePayload,
            "choiceId",
            payloadLabel,
            "id",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            choicePayload,
            "choiceWindow",
            payloadLabel,
            "window",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            choicePayload,
            "playerId",
            payloadLabel,
            "player id",
            errors);
        var requiredCount = ValidateSnapshotPayloadRequiredPositiveIntValue(
            choicePayload,
            "requiredCount",
            payloadLabel,
            "required count",
            errors);
        var maxCount = ValidateSnapshotPayloadRequiredPositiveIntValue(
            choicePayload,
            "maxCount",
            payloadLabel,
            "max count",
            errors);
        if (requiredCount is not null
            && maxCount is not null
            && maxCount.Value < requiredCount.Value)
        {
            errors.Add($"{payloadLabel} max count {maxCount.Value} is less than required count {requiredCount.Value}");
        }

        ValidateSnapshotPayloadOptionalStringValue(
            choicePayload,
            "reason",
            payloadLabel,
            "reason",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            choicePayload,
            "sourceObjectId",
            payloadLabel,
            "source object id",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            choicePayload,
            "effectKind",
            payloadLabel,
            "effect kind",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            choicePayload,
            "choiceState",
            payloadLabel,
            "state",
            errors,
            isKnownChoiceState);
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

        ValidateSpellContinuations(authoritativeState, errors);
        ValidateTurnSequenceState(authoritativeState, errors);
        ValidateAuthoritativeStateScalars(authoritativeState, errors);
        ValidateAuthoritativeStateSeats(authoritativeState, errors);
        ValidateAuthoritativeStateResourceValues(authoritativeState, errors);
        ValidateAuthoritativeStatePlayerZoneValues(authoritativeState.PlayerZones, errors);
        ValidateAuthoritativeStateStackAndTriggerValues(authoritativeState, errors);
        ValidateAuthoritativeStatePendingPaymentValues(authoritativeState.PendingPayment, errors);
        var paymentActions = (authoritativeState.PendingPayment?.PaymentResourceActionIds ?? [])
            .Concat(authoritativeState.PendingPayment?.LegalPaymentChoiceIds ?? [])
            .Concat((authoritativeState.StackItems ?? []).Where(item => item is not null).SelectMany(item => item.OptionalCosts ?? []));
        if (paymentActions.Any(action => action?.StartsWith("TEMP_PAYMENT_RESOURCE:", StringComparison.Ordinal) == true))
            errors.Add("authoritative state contains an obsolete temporary payment resource action");
        ValidateAuthoritativeStatePendingHandChoiceValues(authoritativeState.PendingHandChoice, errors);
        ValidateAuthoritativeStatePendingCardChoiceValues(authoritativeState.PendingCardChoice, errors);
        ValidateAuthoritativeStateUntilEndOfTurnEffectValues(authoritativeState.UntilEndOfTurnEffects, errors);
        ValidateBattleLimitedMarkers(authoritativeState, errors);
        ValidateAuthoritativeStateResolutionHistory(authoritativeState, errors);
        ValidateAuthoritativeStatePlayerPointers(authoritativeState, errors);

        // Private checkpoint and decision transcript are validated above. Public
        // snapshots render the deterministic intermediate replacement preview.
        var paymentPromptState = authoritativeState;
        authoritativeState = CoreRuleEngine.VisibleRuleChoiceState(authoritativeState);

        foreach (var view in playerViews.Values)
        {
            if (view.Snapshot is null)
            {
                continue;
            }

            ValidateResourcePoolViews(view, authoritativeState, errors, paymentPromptState);
            if (view.Snapshot.Stack is null)
            {
                errors.Add($"snapshot for {view.PlayerId} requires a stack collection");
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

            ValidateHandDiscardObserverStackViews(view.Snapshot.Stack, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateLastBreathMightStackViews(view.Snapshot.Stack, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateCapturedInsightStackViews(view.Snapshot.Stack, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateDelayedDestructionStackViews(view.Snapshot.Stack, authoritativeState, errors);
            ValidateTemporaryActivationStackViews(view.Snapshot.Stack, authoritativeState, errors);
            ValidateDelayedEmpowerViews(view.Snapshot.Stack, errors);
            ValidateBombRebuildViews(view.Snapshot.Stack, errors);
            ValidateLastBreathUnitCreationViews(view.Snapshot.Stack, errors);
            ValidateLastBreathRuneCallViews(view.Snapshot.Stack, errors);
            ValidateLastBreathDrawViews(view.Snapshot.Stack, errors);
            ValidateLastBreathBattlefieldDamageViews(view.Snapshot.Stack, errors);
            ValidateDestructionFamilyStackViews(view.Snapshot.Stack, authoritativeState, errors);
            ValidateSourceReplayStackViews(view.Snapshot.Stack, authoritativeState, errors);
            ValidateResolutionDamageChoiceViews(view.Snapshot.Stack, authoritativeState, errors);
            ValidateDeckPlayPendingCardViews(view.Snapshot.Stack, authoritativeState, errors);
            ValidateFlowPlayViews(view.Snapshot.Stack, authoritativeState, errors);
            ValidateTurnUnitPlayBoonViews(view.Snapshot, authoritativeState, errors);
            ValidateBattlefieldConquestMightStackViews(view.Snapshot.Stack, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateBattlefieldConquestMillStackViews(view.Snapshot.Stack, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateBattlefieldConquestChoiceStackViews(view.Snapshot.Stack, authoritativeState, $"snapshot for {view.PlayerId}", errors);
            ValidateMightySkillStackViews(view.Snapshot.Stack, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateRepeatDestinationViews(view.Snapshot.Stack, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateLocationDamageViews(view.Snapshot.Stack, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateDamageMarkerViews(view.Snapshot, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateBattleLimitedView(view, authoritativeState, errors);
            ValidateDynamicPowerAuraViews(view.Snapshot, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateEmpoweredViews(view.Snapshot, authoritativeState, view.PlayerId, errors, $"snapshot for {view.PlayerId}");
            ValidateBeginningRecycleViews(view.Snapshot, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateBeginningRuneViews(view.Snapshot, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateBeginningDrawViews(view.Snapshot, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateEphemeralStartViews(view.Snapshot, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateGrantedTagViews(view.Snapshot, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            ValidateBattlefieldStartViews(view.Snapshot, authoritativeState, errors, $"snapshot for {view.PlayerId}");
            if (view.Snapshot.Timing is not null
                && TryReadObjectList(view.Snapshot.Timing, "triggerQueue", out var publicTriggers))
            {
                foreach (var trigger in authoritativeState.TriggerQueue.Where(t => t.ReflexiveDuel is not null))
                {
                    var projected=publicTriggers.FirstOrDefault(payload=>TryReadObjectString(payload,"triggerId",out var id) && id==trigger.TriggerId);
                    if(MatchStateHasher.HashValue(ReadReflexiveDuelView(projected))!=MatchStateHasher.HashValue(trigger.ReflexiveDuel))
                        errors.Add($"snapshot for {view.PlayerId} reflexive movement duel differs from authoritative state");
                }
                foreach (var trigger in authoritativeState.TriggerQueue)
                {
                    var projected = publicTriggers.FirstOrDefault(payload =>
                        TryReadObjectString(payload, "triggerId", out var id) && id == trigger.TriggerId);
                    CompareHandDiscardObserverView(projected, trigger.HandDiscardObserver, errors, $"snapshot for {view.PlayerId}");
                    CompareMovementObserverView(projected, trigger.MovementObserver, errors, $"snapshot for {view.PlayerId}");
                    CompareDestructionFamilyView(projected, trigger.DamageDestruction, trigger.GraveyardPlay, errors);
                    if (MatchStateHasher.HashValue(ReadDelayedDestructionView(projected)) != MatchStateHasher.HashValue(trigger.DelayedDestruction))
                        errors.Add($"snapshot for {view.PlayerId} captured delayed destruction differs from authoritative state");
                    if (MatchStateHasher.HashValue(ReadCapturedInsightView(projected)) != MatchStateHasher.HashValue(CapturedInsight(trigger.InsightContext)))
                        errors.Add($"snapshot for {view.PlayerId} {CapturedInsightLabel(trigger.InsightContext)} differs from authoritative state");
                }
                foreach (var trigger in authoritativeState.TriggerQueue.Where(t => t.DeathObserver is not null))
                {
                    var projected = publicTriggers.FirstOrDefault(payload =>
                        TryReadObjectString(payload, "triggerId", out var id) && id == trigger.TriggerId);
                    if (MatchStateHasher.HashValue(ReadDeathObserverView(projected)) != MatchStateHasher.HashValue(CoreRuleEngine.PublicDeathObserver(trigger.DeathObserver)))
                        errors.Add($"snapshot for {view.PlayerId} captured death observer differs from authoritative state");
                }
            }

            var viewSeats = ExtractSeats(view.Snapshot);
            if (!SeatsEqual(authoritativeState.Seats, viewSeats))
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees with authoritative state seats");
            }
        }
    }

    private static void ValidateEmpoweredViews(SnapshotDto snapshot, MatchState state, string? viewer, List<string> errors, string label)
    {
        foreach (var player in snapshot.Players?.Values ?? [])
        {
            if (!TryReadObjectValue(player, "objects", out var objects) || !TryReadObjectDictionaryValue(objects, out var payloads)) continue;
            foreach (var (id, payload) in payloads)
            {
                if (!state.CardObjects.TryGetValue(id, out var card)) continue;
                var present = TryReadObjectValue(payload, "isEmpowered", out _);
                var visible = !card.IsFaceDown || viewer is not null && CoreRuleEngine.CanInspectFaceDown(state, id, viewer);
                if (present && (!visible || !card.IsEmpowered || !TryReadObjectBool(payload, "isEmpowered", out var value) || !value)
                    || !present && visible && card.IsEmpowered)
                    errors.Add($"{label} empowered status differs from authoritative visible object");
                var countPresent = TryReadObjectValue(payload, "empowermentCount", out _);
                if (countPresent && (!visible || !card.IsEmpowered
                        || !TryReadObjectInt(payload, "empowermentCount", out var count) || count != card.EmpowermentCount)
                    || !countPresent && visible && card.IsEmpowered)
                    errors.Add($"{label} empowerment count differs from authoritative visible object");
            }
        }
    }

    private static bool IsValidatedDynamicPowerAura(string? kind, string? sourceCardNo)
        => kind is CoreRuleEngine.EmpoweredUnitAuraEffect or StaticAuraKinds.SourceGraveyardCountPower
            or StaticAuraKinds.SourceControllerScorePower
            or StaticAuraKinds.SameLocationEnemyFilteredPower or "ATTACHED_ARMAMENT_BASE_MIGHT"
            or "ATTACHED_RULE_TEXT_POWER" or "ATTACHED_RULE_TEXT_KEYWORD" or CoreRuleEngine.BoonAmplifierEffect
            or StaticAuraKinds.SourceOtherSameNameUnitCountPower
            || (kind == StaticAuraKinds.FriendlyUnitsPower
                && StaticAuraSpecRules.GetStaticAuras(sourceCardNo).Any(a => a.PowerDeltaIfSourceEmpowered.HasValue));

    private static void ValidateDynamicPowerAuraViews(SnapshotDto snapshot, MatchState state, List<string> errors, string label)
    {
        var expected = state.ContinuousEffects.Where(e => IsValidatedDynamicPowerAura(e.EffectKind, e.SourceCardNo))
            .ToDictionary(e => e.EffectId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (snapshot.Timing is not null && TryReadObjectList(snapshot.Timing, "continuousEffects", out var effects))
            foreach (var payload in effects)
            {
                if (!TryReadObjectString(payload, "effectId", out var id) || string.IsNullOrWhiteSpace(id)) continue;
                if (!expected.TryGetValue(id, out var effect))
                {
                    TryReadObjectString(payload, "sourceCardNo", out var sourceCardNo);
                    if (TryReadObjectString(payload, "effectKind", out var kind) && IsValidatedDynamicPowerAura(kind, sourceCardNo))
                        errors.Add($"{label} unexpected dynamic power aura");
                    continue;
                }
                seen.Add(id);
                var scalars = new Dictionary<string, int> { ["powerDelta"] = effect.PowerDelta,
                    ["basePower"] = effect.BasePower, ["effectivePower"] = effect.EffectivePower };
                foreach (var (key, value) in scalars)
                    if (!TryReadObjectInt(payload, key, out var actual) || actual != value)
                        errors.Add($"{label} dynamic power aura {key} differs from authoritative state");
                if (effect.EffectKind == CoreRuleEngine.BoonAmplifierEffect
                    && (!TryReadObjectInt(payload, "sourceOrder", out var sourceOrder) || sourceOrder != effect.SourceOrder))
                    errors.Add($"{label} temporary Boon amplifier source order differs from authoritative state");
                var strings = new Dictionary<string, string?> { ["sourceObjectId"] = effect.SourceObjectId,
                    ["targetObjectId"] = effect.TargetObjectId, ["sourceCardNo"] = effect.SourceCardNo,
                    ["effectKind"] = effect.EffectKind, ["sourcePath"] = effect.SourcePath,
                    ["scope"] = effect.Scope, ["layer"] = effect.Layer, ["duration"] = effect.Duration,
                    ["condition"] = effect.Condition, ["lifecycle"] = effect.Lifecycle };
                foreach (var (key, value) in strings)
                    if (!TryReadObjectString(payload, key, out var actual) || actual != value)
                        errors.Add($"{label} dynamic power aura {key} differs from authoritative state");
                if (effect.EffectKind is CoreRuleEngine.EmpoweredUnitAuraEffect or "ATTACHED_RULE_TEXT_POWER" or "ATTACHED_RULE_TEXT_KEYWORD" or StaticAuraKinds.SameLocationEnemyFilteredPower or CoreRuleEngine.BoonAmplifierEffect
                    or StaticAuraKinds.SourceOtherSameNameUnitCountPower or StaticAuraKinds.FriendlyUnitsPower)
                    foreach (var (key, value) in new Dictionary<string, IReadOnlyList<string>> {
                        ["sourceDependencyObjectIds"] = effect.SourceDependencyObjectIds ?? [],
                        ["targetDependencyObjectIds"] = effect.TargetDependencyObjectIds ?? [],
                        ["participantDependencyObjectIds"] = effect.ParticipantDependencyObjectIds ?? [],
                        ["participantObjectIds"] = effect.ParticipantObjectIds ?? [] })
                        if (!TryReadObjectStringList(payload, key, out var actual) || !actual.SequenceEqual(value))
                            errors.Add($"{label} copied rule text aura {key} differs from authoritative state");
            }
        if (!seen.SetEquals(expected.Keys)) errors.Add($"{label} dynamic power aura set differs from authoritative state");
    }

    private static bool ValidateMovementObserverView(object? payload, string? effect, string? controller, string? source, List<string> errors)
    {
        MovementObserverContext? context = null;
        try {
            var json = JsonSerializer.SerializeToElement(payload);
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("movementObserver", out var raw) && raw.ValueKind != JsonValueKind.Null)
                context = raw.Deserialize<MovementObserverContext>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        } catch (JsonException) { errors.Add("invalid public movement observer encoding"); }
        if (effect is not (CoreRuleEngine.MovementObserverEffect or CoreRuleEngine.MovementDiscardInlineEffect) && context is null) return false;
        // Public old-generation movement facts remain public, while the source
        // field can hide the source's new private incarnation. Authority parity
        // checks separately prove this exact captured fact and source visibility.
        var capturedSource = source == "HIDDEN" && context?.Kind != TriggerKinds.OpponentMovedToOtherBattlefieldDraw
            ? context?.Kind == TriggerKinds.BattlefieldUnitMovedAwayPowerModifier ? context.Unit?.Origin?.BattlefieldObjectId : context?.Unit?.ObjectId
            : source;
        if (context is null || !CoreRuleEngine.ValidMovementObserver(context, effect ?? "", controller ?? "", capturedSource ?? ""))
        { errors.Add("invalid public captured movement observer"); return false; }
        return true;
    }

    private static MightySkillView? ReadMightySkillView(object? payload)
    {
        try {
            var json = JsonSerializer.SerializeToElement(payload);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("mightySkill", out var raw)
                && raw.ValueKind != JsonValueKind.Null
                ? raw.Deserialize<MightySkillView>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
        } catch (JsonException) { return null; }
    }

    private static void ValidateMightySkillPayload(object? payload, string? effect, List<string> errors)
    {
        var context = ReadMightySkillView(payload);
        if (effect is CoreRuleEngine.MightyReadyEffect or CoreRuleEngine.MightyRuneEffect or CoreRuleEngine.MightyEmpowerEffect || context is not null)
            if (context is null || context.Kind != effect || context.BeforeMight >= context.AfterMight
                || effect is not (CoreRuleEngine.MightyReadyEffect or CoreRuleEngine.MightyRuneEffect or CoreRuleEngine.MightyEmpowerEffect))
                errors.Add("invalid public captured Mighty transition");
    }

    private static void ValidateMightySkillStackViews(IReadOnlyList<object?> payloads,
        MatchState authority, List<string> errors, string label)
    {
        foreach (var payload in payloads) {
            TryReadObjectString(payload, "stackItemId", out var id);
            TryReadObjectString(payload, "effectKind", out var effect);
            ValidateMightySkillPayload(payload, effect, errors);
            var expected = authority.StackItems.FirstOrDefault(i => i.StackItemId == id);
            if (MatchStateHasher.HashValue(ReadMightySkillView(payload)) != MatchStateHasher.HashValue(CoreRuleEngine.PublicMightySkill(expected?.MightySkill)))
                errors.Add($"{label} captured Mighty transition differs from authoritative state");
        }
    }

    private static BattlefieldConquestMillPublicContext? ReadBattlefieldConquestMillView(object? payload)
    {
        try {
            var json = JsonSerializer.SerializeToElement(payload);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("battlefieldConquestMill", out var raw)
                && raw.ValueKind != JsonValueKind.Null
                ? raw.Deserialize<BattlefieldConquestMillPublicContext>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
        } catch (JsonException) { return null; }
    }

    private static void ValidateBattlefieldConquestMillPayload(object? payload, string? effect, List<string> errors)
    {
        var context = ReadBattlefieldConquestMillView(payload);
        var present = TryReadObjectValue(payload, "battlefieldConquestMill", out _);
        if (effect == TriggerKinds.BattlefieldConquerMill || present)
            if (context is null || effect != TriggerKinds.BattlefieldConquerMill
                || !CoreRuleEngine.ValidBattlefieldConquestMillPublicFrame(context))
                errors.Add("invalid public captured battlefield conquest mill");
    }

    private static void ValidateBattlefieldConquestMillStackViews(IReadOnlyList<object?> payloads,
        MatchState authority, List<string> errors, string label)
    {
        foreach (var payload in payloads) {
            TryReadObjectString(payload, "stackItemId", out var id);
            TryReadObjectString(payload, "effectKind", out var effect);
            ValidateBattlefieldConquestMillPayload(payload, effect, errors);
            var expected = authority.StackItems.FirstOrDefault(i => i.StackItemId == id);
            if (MatchStateHasher.HashValue(ReadBattlefieldConquestMillView(payload))
                != MatchStateHasher.HashValue(CoreRuleEngine.PublicBattlefieldConquestMill(expected?.BattlefieldConquestMill)))
                errors.Add($"{label} captured battlefield conquest mill differs from authoritative state");
        }
    }

    private static void ValidateBattlefieldConquestMillHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var pending = state.TriggerQueue.Select(t => t.BattlefieldConquestMill)
            .Concat(state.StackItems.Select(i => i.BattlefieldConquestMill)).Where(c => c is not null);
        foreach (var context in pending) {
            var c = context!;
            var triggerId = $"CONQUEST-MILL:{c.ConquestTick}:{c.BattlefieldObjectId}";
            var conquests = events.Where(e => e.Tick == c.ConquestTick && e.Event.Kind == "BATTLEFIELD_CONQUERED"
                && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == c.ControllerId
                && TryReadObjectString(e.Event.Payload, "battlefieldId", out var battlefield) && battlefield == c.BattlefieldObjectId
                && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var source) && source == c.ConquestSourceObjectId).Take(2).ToArray();
            var queued = events.Where(e => e.Tick == c.ConquestTick && e.Event.Kind == "TRIGGER_QUEUED"
                && TryReadObjectString(e.Event.Payload, "triggerId", out var id) && id == triggerId
                && TryReadObjectString(e.Event.Payload, "controllerId", out var controller) && controller == c.ControllerId
                && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var source) && source == c.BattlefieldObjectId
                && TryReadObjectString(e.Event.Payload, "effectKind", out var effect) && effect == TriggerKinds.BattlefieldConquerMill
                && TryReadObjectString(e.Event.Payload, "triggeredByEventKind", out var origin) && origin == "BATTLEFIELD_CONQUERED").Take(2).ToArray();
            if (conquests.Length != 1 || queued.Length != 1 || queued[0].Sequence <= conquests[0].Sequence
                || !commands.Any(cmd => cmd.Accepted && cmd.CompletedTick == c.ConquestTick
                    && cmd.StartedEventSequence < conquests[0].Sequence && cmd.CompletedEventSequence >= queued[0].Sequence))
                errors.Add("captured battlefield conquest mill is not bound to accepted conquest history");
        }
    }

    private static BattlefieldConquestMightContext? ReadBattlefieldConquestMightView(object? payload)
    {
        try {
            var json = JsonSerializer.SerializeToElement(payload);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("battlefieldConquestMight", out var raw)
                && raw.ValueKind != JsonValueKind.Null
                ? raw.Deserialize<BattlefieldConquestMightContext>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
        } catch (JsonException) { return null; }
    }

    private static bool ValidMovementPlayers(MatchState state, MovementObserverContext context)
        => (context.MovementBatch ?? [context.Unit]).All(unit => state.Seats.ContainsKey(unit.ControllerId)
            && state.Seats.ContainsKey(unit.MovingPlayerId));

    private static void ValidateSpellContinuations(MatchState state, List<string> errors)
    {
        ValidateUnitConquestReturnContinuations(state, errors);
        ValidateBattlefieldConquestChoiceContinuations(state, errors);
        ValidateHandDiscardObservers(state, errors);
        foreach (var item in state.StackItems)
            if (item.MightySkill is { } mighty ? !CoreRuleEngine.ValidMightySkill(state, mighty,
                    item.EffectKind, item.SourceObjectId, item.ControllerId)
                : item.EffectKind is CoreRuleEngine.MightyReadyEffect or CoreRuleEngine.MightyRuneEffect)
                errors.Add("invalid captured Mighty transition stack");
        foreach (var item in state.TriggerQueue)
            if (item.MightySkill is { } mighty ? !CoreRuleEngine.ValidMightySkill(state, mighty,
                    item.EffectKind, item.SourceObjectId, item.ControllerId)
                : item.EffectKind is CoreRuleEngine.MightyReadyEffect or CoreRuleEngine.MightyRuneEffect)
                errors.Add("invalid captured Mighty transition queue");
        if (state.PendingPayment is { PaymentWindow: CoreRuleEngine.MightyCostWindow } mightyPayment
            && !CoreRuleEngine.ValidMightySkillPayment(state, mightyPayment)) errors.Add("invalid Mighty transition payment");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.MightyChoiceWindow } mightyChoice
            && !CoreRuleEngine.ValidMightySkillChoice(state, mightyChoice)) errors.Add("invalid Mighty transition choice");
        if (state.PendingPayment is { PaymentWindow: "TRIGGER_PAYMENT", Reason: { } oldReason }
            && (oldReason.StartsWith(TriggerKinds.BattlefieldConquerPowerfulPayDraw + "|", StringComparison.Ordinal)
                || oldReason.StartsWith(TriggerKinds.UnitControlledUnitPowerfulPayPowerReady + "|", StringComparison.Ordinal)))
            errors.Add("obsolete payment lacks captured conquest Might and independent ability");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.BattlefieldConquestMill is { } mill
                ? !CoreRuleEngine.ValidBattlefieldConquestMillContext(state, mill, trigger.EffectKind,
                    trigger.SourceObjectId, trigger.ControllerId, trigger.SourceCardNo)
                    || trigger.EnqueuedAtTick != mill.ConquestTick || trigger.TriggeredByEventKind != "BATTLEFIELD_CONQUERED"
                    || trigger.TriggerId != $"CONQUEST-MILL:{mill.ConquestTick}:{mill.BattlefieldObjectId}"
                    || MatchStateHasher.HashValue(trigger) != MatchStateHasher.HashValue(new TriggerQueueItemState(
                        trigger.TriggerId, trigger.ControllerId, trigger.SourceObjectId, trigger.EffectKind,
                        trigger.TriggeredByEventKind, trigger.TimingContext) { EnqueuedAtTick = trigger.EnqueuedAtTick,
                        ControllerOrderConfirmed = trigger.ControllerOrderConfirmed, SourceCardNo = trigger.SourceCardNo,
                        BattlefieldConquestMill = mill })
                : trigger.EffectKind == TriggerKinds.BattlefieldConquerMill)
                errors.Add("invalid captured battlefield conquest mill queue");
        foreach (var item in state.StackItems)
            if (item.BattlefieldConquestMill is { } mill
                ? !CoreRuleEngine.ValidBattlefieldConquestMillContext(state, mill, item.EffectKind,
                    item.SourceObjectId, item.ControllerId, item.CardNo)
                    || item.StackItemId != $"ordered-CONQUEST-MILL:{mill.ConquestTick}:{mill.BattlefieldObjectId}"
                    || MatchStateHasher.HashValue(item) != MatchStateHasher.HashValue(new StackItemState(
                        item.StackItemId, item.ControllerId, item.SourceObjectId, item.EffectKind, item.CardNo,
                        timingContext: item.TimingContext) { BattlefieldConquestMill = mill })
                : item.EffectKind == TriggerKinds.BattlefieldConquerMill)
                errors.Add("invalid captured battlefield conquest mill stack");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.BattlefieldConquestMight is { } captured ? !CoreRuleEngine.ValidBattlefieldConquestMightContext(state,
                captured, trigger.EffectKind, trigger.SourceObjectId, trigger.ControllerId)
                : CoreRuleEngine.IsCapturedConquestDrawKind(trigger.EffectKind)) errors.Add("invalid captured conquest Might");
        foreach (var item in state.StackItems)
            if (item.BattlefieldConquestMight is { } captured ? !CoreRuleEngine.ValidBattlefieldConquestMightContext(state,
                captured, item.EffectKind, item.SourceObjectId, item.ControllerId)
                : CoreRuleEngine.IsCapturedConquestDrawKind(item.EffectKind)) errors.Add("invalid captured conquest Might stack");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.LastBreathMight is { } captured ? !CoreRuleEngine.ValidLastBreathMightContext(state, captured,
                trigger.EffectKind, trigger.SourceObjectId, trigger.ControllerId)
                : trigger.EffectKind == TriggerKinds.UnitLastBreathPowerfulDraw)
                errors.Add("invalid captured last-breath Might");
        foreach (var item in state.StackItems)
            if (item.LastBreathMight is { } captured ? !CoreRuleEngine.ValidLastBreathMightContext(state, captured,
                item.EffectKind, item.SourceObjectId, item.ControllerId)
                : item.EffectKind == TriggerKinds.UnitLastBreathPowerfulDraw)
                errors.Add("invalid captured last-breath Might stack");
        var layeredKeywords = CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
        foreach (var (id, card) in state.CardObjects)
            if (!(card.RuleDerivedTags ?? []).SequenceEqual(layeredKeywords.CardObjects[id].RuleDerivedTags ?? [])
                || !card.Tags.SequenceEqual(layeredKeywords.CardObjects[id].Tags))
                errors.Add($"invalid current rule-derived keywords for {id}");
        var battleDeclarations = state.UntilEndOfTurnEffects?.Count(e => e?.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal) == true) ?? 0;
        if (battleDeclarations > 0 && (battleDeclarations != 1
            || !CoreRuleEngine.TryReadBattleResponseDeclarationContext(state, string.Empty, out _)))
            errors.Add("invalid battle response declaration");
        if (!CoreRuleEngine.ValidLegendDiscardDrawChoice(state)) errors.Add("invalid legend discard continuation");
        foreach (var card in state.CardObjects.Values)
            if (!CoreRuleEngine.ValidBanishReplacementOrigins(state, card)) errors.Add("invalid banish replacement origin");
        if (!CoreRuleEngine.ValidMovementDiscardChoice(state)) errors.Add("invalid movement discard continuation");
        if (!TimedBanishPlayRules.ValidAll(state)) errors.Add("invalid timed banish play permission");
        if (!CoreRuleEngine.ValidMovementDiscardState(state)) errors.Add("invalid movement discard inline bindings");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.MovementObserver is { } movement
                ? !CoreRuleEngine.ValidMovementObserver(state, movement, trigger.EffectKind, trigger.ControllerId, trigger.SourceObjectId)
                    || !CoreRuleEngine.ValidMovementInstanceId(movement, trigger.TriggerId)
                    || movement.TargetTokenFactoryCardNo is not null
                    || trigger.TriggeredByEventKind != (movement.DiscardInline is not null ? "CARD_DISCARDED"
                        : movement.Unit.Destination.Zone == "BASE" ? "UNIT_MOVED_TO_BASE" : "UNIT_MOVED_TO_BATTLEFIELD")
                    || !ValidMovementPlayers(state, movement)
                : trigger.EffectKind is CoreRuleEngine.MovementObserverEffect or CoreRuleEngine.MovementDiscardInlineEffect) errors.Add("invalid captured movement observer");
        foreach (var item in state.StackItems)
            if (item.MovementObserver is { } movement
                ? !CoreRuleEngine.ValidMovementObserver(state, movement, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
                    || CoreRuleEngine.NeedsMovementInstructionConfirmation(item)
                    || !CoreRuleEngine.ValidMovementInstanceId(movement, item.StackItemId, stack: true)
                    || !CoreRuleEngine.ValidMovementDamageStack(state, item)
                    || !ValidMovementPlayers(state, movement)
                : item.EffectKind is CoreRuleEngine.MovementObserverEffect or CoreRuleEngine.MovementDiscardInlineEffect) errors.Add("invalid captured movement observer stack");
        if (!CoreRuleEngine.ValidRuleChoice(state)) errors.Add("invalid replacement choice continuation");
        if (state.BattlefieldConquests is { } conquests && (conquests.TurnNumber != state.TurnNumber
            || conquests.Entries is null || conquests.Entries.Count == 0 || conquests.Entries.Distinct().Count() != conquests.Entries.Count
            || conquests.Entries.Any(entry => entry is null || string.IsNullOrWhiteSpace(entry.PlayerId)
                || !state.Seats.ContainsKey(entry.PlayerId) || entry.Battlefield is null
                || string.IsNullOrWhiteSpace(entry.Battlefield.ObjectId) || entry.Battlefield.Generation < 0
                || state.CardObjects.TryGetValue(entry.Battlefield.ObjectId, out var field)
                    && (!field.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag)
                        || field.ObjectGeneration < entry.Battlefield.Generation)))) errors.Add("invalid turn battlefield conquests");
        if (state.DeathLedger is null || state.DeathLedger.Counts is null || state.DeathLedger.TurnNumber < 0
            || state.DeathLedger.Counts.Count > 0 && state.DeathLedger.TurnNumber != state.TurnNumber
            || state.DeathLedger.Counts.Any(p=>!state.Seats.ContainsKey(p.Key) || p.Value<=0)
            || state.DeathLedger.PermanentControllers is null
            || state.DeathLedger.PermanentControllers.Any(player => !state.Seats.ContainsKey(player))
            || state.DeathLedger.PermanentControllers.Distinct(StringComparer.Ordinal).Count() != state.DeathLedger.PermanentControllers.Count
            || state.DeathLedger.PermanentControllers.Count > 0 && state.DeathLedger.TurnNumber != state.TurnNumber) errors.Add("invalid death ledger");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.DeathObserver is null && IsUncapturedDeathObserver(trigger.EffectKind, trigger.TriggerId))
                errors.Add("obsolete uncaptured death observer");
        foreach(var trigger in state.TriggerQueue)
            if (trigger.DeathObserver is { } death ? !CoreRuleEngine.ValidDeathObserver(state,death,trigger.EffectKind,trigger.ControllerId,trigger.SourceObjectId)
                || trigger.TriggerId.StartsWith("copied-death-", StringComparison.Ordinal) != (death.Origin is not null)
                || death.RuleTextInstanceId is { } triggerInstance && !trigger.TriggerId.EndsWith(":" + triggerInstance, StringComparison.Ordinal)
                || trigger.TriggeredByEventKind != TriggerTimings.UnitDestroyed || !state.Seats.ContainsKey(death.Destroyed.OwnerId)
                : trigger.EffectKind == CoreRuleEngine.DeathObserverEffect || CoreRuleEngine.IsDeathObserverKind(trigger.EffectKind)) errors.Add("invalid captured death observer");
        foreach (var item in state.StackItems)
            if (item.DeathObserver is null && IsUncapturedDeathObserver(item.EffectKind, item.StackItemId))
                errors.Add("obsolete uncaptured death observer stack");
        foreach(var item in state.StackItems)
            if (item.DeathObserver is { } death ? !CoreRuleEngine.ValidDeathObserver(state,death,item.EffectKind,item.ControllerId,item.SourceObjectId,item.CardNo)
                || !CoreRuleEngine.ValidDeathObserverTargets(item)
                || item.StackItemId.StartsWith("ordered-copied-death-", StringComparison.Ordinal) != (death.Origin is not null)
                || death.RuleTextInstanceId is { } stackInstance && !item.StackItemId.EndsWith(":" + stackInstance, StringComparison.Ordinal)
                || !state.Seats.ContainsKey(death.Destroyed.OwnerId)
                : item.EffectKind == CoreRuleEngine.DeathObserverEffect || CoreRuleEngine.IsDeathObserverKind(item.EffectKind)) errors.Add("invalid captured death observer stack");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.ReflexiveDuel is { } duel ? !CoreRuleEngine.ValidReflexiveDuel(duel,trigger.EffectKind)
                || duel.Destination is { } destination && !state.Seats.ContainsKey(destination.PlayerId)
                || trigger.TriggeredByEventKind != "MOVEMENT_INSTRUCTION_COMPLETED"
                : trigger.EffectKind == CoreRuleEngine.ReflexiveDuelEffect) errors.Add("invalid reflexive movement duel trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidReflexiveDuelStack(state,item)) errors.Add("invalid reflexive movement duel stack");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.UnitConquest is { } conquest ? !CoreRuleEngine.ValidUnitConquest(conquest,trigger.EffectKind)
                || !CoreRuleEngine.ValidCapturedConquestGrant(state, conquest, trigger.SourceObjectId)
                || trigger.TriggeredByEventKind != "BATTLEFIELD_CONQUERED"
                : trigger.EffectKind is CoreRuleEngine.ConquestDamageEffect or CoreRuleEngine.GrantedConquestReturnEffect) errors.Add("invalid captured unit conquest");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidConquestDamageStack(state,item)) errors.Add("invalid unit conquest stack");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.LegendConquest is { } context ? !CoreRuleEngine.ValidLegendConquest(context, trigger.EffectKind)
                || trigger.TriggeredByEventKind != "BATTLEFIELD_CONQUERED"
                : trigger.EffectKind == CoreRuleEngine.LegendConquestEffect) errors.Add("invalid captured legend conquest");
        foreach (var item in state.StackItems)
            if (item.LegendConquest is { } context ? !CoreRuleEngine.ValidLegendConquest(context, item.EffectKind, item.CardNo)
                || context.Kind == "EXHAUST_READY_UNIT" && item.TriggerCost is not null && (item.TargetObjectIds.Count != 1
                    || item.TargetGenerations is not { Count: 1 } generations || !generations.ContainsKey(item.TargetObjectIds[0]))
                : item.EffectKind == CoreRuleEngine.LegendConquestEffect) errors.Add("invalid legend conquest stack");
        if (state.PendingPayment is { PaymentWindow: CoreRuleEngine.TriggerTargetCostWindow } targetPayment
            && !CoreRuleEngine.ValidTriggerTargetPayment(state, targetPayment)) errors.Add("invalid trigger target payment");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidHeldTargetStack(state, item)) errors.Add("invalid held target confirmation");
        if (state.PendingCardChoice is { ChoiceWindow: "HOLD_EFFECT" })
            errors.Add("obsolete held resolution choice");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.TriggerCostWindow or CoreRuleEngine.OptionalTriggerWindow } creationCost
            && !CoreRuleEngine.ValidTriggerCostChoice(state, creationCost)) errors.Add("invalid trigger cost choice");
        foreach (var item in state.StackItems)
            if (CoreRuleEngine.NeedsTriggerCostConfirmation(item)
                && (state.PriorityPlayerId is not null || state.PendingCardChoice is null && state.PendingPayment is null && state.TriggerQueue.Count == 0))
                errors.Add("unconfirmed trigger cost");
        if (state.PendingPayment is { PaymentWindow: CoreRuleEngine.TriggerCostWindow } triggerCost
            && !CoreRuleEngine.ValidTriggerCostPayment(state, triggerCost)) errors.Add("invalid trigger cost payment");
        if (state.PendingPayment is { } conquestReturnPayment && IsUnitConquestReturnPayment(state, conquestReturnPayment)
            && !CoreRuleEngine.ValidTriggerCostPayment(state, conquestReturnPayment)) errors.Add("invalid trigger cost payment for unit conquest return");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidTriggerCostReceipt(state, item)) errors.Add("invalid trigger cost receipt");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidLegendUnitToken(item)) errors.Add("invalid legend unit token instruction");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidSelfCopyTokenPlay(state, item)) errors.Add("invalid self copy token instruction");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidTokenEntryPlan(state, item)
                || item.TokenEntryPlan is { } tokenPlan && tokenPlan.NextToken < tokenPlan.OriginalCount
                    && (state.PendingCardChoice?.ChoiceWindow != CoreRuleEngine.TokenReplacementWindow
                        || state.PendingCardChoice.ResolvingStackItemId != item.StackItemId)
                || item.CompletedRepeatExecutions < 0
                || item.CompletedRepeatExecutions > 0 && (item.RepeatExecutions is null || item.CompletedRepeatExecutions >= item.RepeatExecutions.Count))
                errors.Add("invalid token entry continuation");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.TokenReplacementWindow } tokenChoice
            && !CoreRuleEngine.ValidTokenReplacementChoice(state, tokenChoice)) errors.Add("invalid token replacement choice");

        if (!BattleResultRecoveryRules.ValidContinuation(state)) errors.Add("invalid captured battle result continuation");
        if (!TurnEquipmentPlayRules.ValidAll(state)) errors.Add("invalid turn equipment play permission");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidDepartedTokenTargets(state, item)) errors.Add("invalid departed token target");
        foreach (var item in state.StackItems)
            if (!DravenBattleDeathRecoveryRules.Valid(state, item)) errors.Add("invalid stacked Draven battle-death skill");
        foreach (var item in state.TriggerQueue)
            if (!DravenBattleDeathRecoveryRules.Valid(state, item)) errors.Add("invalid queued Draven battle-death skill");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.DravenDeathOpponentWindow } deathChoice
            && !DravenBattleDeathRecoveryRules.ValidChoice(state, deathChoice)) errors.Add("invalid Draven death opponent choice");
        foreach (var item in state.StackItems)
            if (!BattleResultRecoveryRules.Valid(state, item)) errors.Add("invalid stacked battle-result skill");
        foreach (var item in state.TriggerQueue)
            if (!BattleResultRecoveryRules.Valid(state, item)) errors.Add("invalid queued battle-result skill");
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidUnitEvent(state, trigger.UnitEvent, trigger.EffectKind, trigger.SourceObjectId))
                errors.Add("invalid captured unit event trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidUnitEvent(state, item.UnitEvent, item.EffectKind, item.SourceObjectId, item.CardNo)
                || item.UnitEvent is not null && (item.OptionalCosts.Count != 0 || item.EffectRepeatCount != 1
                    || !CoreRuleEngine.ValidUnitEventTargets(item)))
                errors.Add("invalid captured unit event stack");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.UnitEventModeWindow } unitEventMode
            && !CoreRuleEngine.ValidUnitEventModeChoice(state, unitEventMode))
            errors.Add("invalid unit event mode choice");
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidEmpowerInteraction(state, trigger.EmpowerInteraction, trigger.EffectKind, trigger.SourceObjectId, trigger.ControllerId))
                errors.Add("invalid empower interaction trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidEmpowerInteraction(state, item.EmpowerInteraction, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)
                || item.EmpowerInteraction is not null && (item.OptionalCosts.Count != 0 || item.EffectRepeatCount != 1))
                errors.Add("invalid empower interaction stack");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.EmpowerRemovalCostWindow or CoreRuleEngine.EmpowerOpponentWindow } empowerChoice
            && !CoreRuleEngine.ValidEmpowerInteractionChoice(state, empowerChoice)) errors.Add("invalid empower interaction choice");
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidSourceReadied(state, trigger.SourceReadied, trigger.EffectKind, trigger.SourceObjectId, trigger.ControllerId, trigger.SourceCardNo)
                || trigger.SourceReadied is { } readied && (readied.Mode is not null || trigger.TriggeredByEventKind != "UNIT_READY"))
                errors.Add("invalid source readied trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidSourceReadiedStack(state, item)) errors.Add("invalid source readied ability");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.ReadiedModeWindow } readiedChoice
            && !CoreRuleEngine.ValidReadiedModeChoice(state, readiedChoice)) errors.Add("invalid source readied mode choice");
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidReadyObjectTrigger(state, trigger.ReadyObjectTrigger, trigger.EffectKind, trigger.SourceObjectId, trigger.ControllerId, trigger.SourceCardNo))
                errors.Add("invalid ready other object trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidReadyOtherObjectStack(state, item)) errors.Add("invalid ready other object ability");
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidSourceUnitTokens(state, trigger.SourceUnitTokens, trigger.EffectKind, trigger.SourceObjectId, trigger.ControllerId))
                errors.Add("invalid source unit token trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidSourceUnitTokens(state, item.SourceUnitTokens, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo))
                errors.Add("invalid source unit token stack");
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidSourcePlayDraw(state, trigger.SourcePlayDraw, trigger.EffectKind, trigger.SourceObjectId))
                errors.Add("invalid source play draw trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidSourcePlayDraw(state, item.SourcePlayDraw, item.EffectKind, item.SourceObjectId, item.CardNo))
                errors.Add("invalid source play draw stack");
        foreach (var trigger in state.TriggerQueue)
            if (!PermanentPlayTriggerRecoveryRules.ValidQueued(state, trigger)) errors.Add("invalid deferred permanent play ordering origin");
        if (!TriggerQueueRecoveryRules.Valid(state)) errors.Add("invalid queued trigger chronology");
        if (!TriggerQueueRecoveryRules.ValidControllerOrderConfirmations(state)) errors.Add("invalid trigger controller order confirmations");
        foreach (var trigger in state.TriggerQueue)
            if (!RengarPlayRecoveryRules.ValidQueued(state, trigger))
                errors.Add("invalid unit play might trigger");
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidHighCostTrigger(state, trigger)) errors.Add("invalid high-cost play observer trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidHighCostStack(state, item)) errors.Add("invalid high-cost play observer stack");
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidOutsideHandPlayTrigger(state, trigger)) errors.Add("invalid card play observer trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidOutsideHandPlayStack(state, item)) errors.Add("invalid card play observer stack");
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidEmpowerOtherTrigger(state, trigger)) errors.Add("invalid empower observer trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidEmpowerOtherStack(state, item)) errors.Add("invalid empower observer stack");
        foreach (var item in state.StackItems)
            if (!RengarPlayRecoveryRules.ValidStack(state, item)) errors.Add("invalid unit play might stack");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.UnitEntryContext is { } entry ? !CoreRuleEngine.ValidUnitEntryTrigger(entry, trigger.EffectKind, trigger.ControllerId)
                || !state.Seats.ContainsKey(entry.EnteringPlayerId) : trigger.EffectKind == CoreRuleEngine.EnemyUnitEntryEffect) errors.Add("invalid unit entry trigger");
        foreach (var item in state.StackItems)
            if (item.UnitEntryContext is { } entry ? !CoreRuleEngine.ValidUnitEntryTrigger(entry, item.EffectKind, item.ControllerId, item.CardNo)
                || !state.Seats.ContainsKey(entry.EnteringPlayerId) : item.EffectKind == CoreRuleEngine.EnemyUnitEntryEffect) errors.Add("invalid unit entry stack");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.ReflexiveCopy is { } copy ? !CoreRuleEngine.ValidReflexiveCopy(copy, trigger.EffectKind, sourceObjectId: trigger.SourceObjectId) || !CoreRuleEngine.ValidCopyRecipients(state, copy)
                : trigger.EffectKind == CoreRuleEngine.ReflexiveCopyEffect) errors.Add("invalid reflexive copy trigger");
        foreach (var item in state.StackItems)
            if (item.ReflexiveCopy is { } copy ? !CoreRuleEngine.ValidReflexiveCopy(copy, item.EffectKind, item.CardNo) || !CoreRuleEngine.ValidCopyRecipients(state, copy)
                || !CoreRuleEngine.ValidReflexiveCopyStack(state, item)
                : item.EffectKind == CoreRuleEngine.ReflexiveCopyEffect) errors.Add("invalid reflexive copy stack");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.CreatedTokenReady is { } ready ? !CoreRuleEngine.ValidCreatedTokenReady(state, ready, trigger.EffectKind, trigger.SourceObjectId)
                : trigger.EffectKind == CoreRuleEngine.CreatedTokenReadyEffect) errors.Add("invalid created token ready trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidCreatedTokenReadyStack(state, item)) errors.Add("invalid created token ready stack");
        if (state.FaceDownLookPermissions.Any(p => p is null || !CoreRuleEngine.ValidFaceDownLookPermission(state, p))
            || state.FaceDownLookPermissions.Distinct().Count() != state.FaceDownLookPermissions.Count)
            errors.Add("invalid face-down look permission");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.DeathRevealContext is not null ? !CoreRuleEngine.ValidDeathReveal(trigger.DeathRevealContext, trigger.EffectKind, trigger.ControllerId)
                : trigger.EffectKind == CoreRuleEngine.DeathRevealEffect) errors.Add("invalid death reveal trigger context");
        foreach (var item in state.StackItems)
            if (item.DeathRevealContext is not null ? !CoreRuleEngine.ValidDeathReveal(item.DeathRevealContext, item.EffectKind, item.ControllerId, item.CardNo)
                : item.EffectKind == CoreRuleEngine.DeathRevealEffect) errors.Add("invalid death reveal stack context");
        if (state.PendingCardChoice is { } handChoice && (handChoice.HandContext is not null || handChoice.ChoiceWindow == "REVEALED_HAND_EFFECT")
            && !CoreRuleEngine.ValidRevealedHandChoice(state, handChoice))
            errors.Add("invalid revealed hand choice continuation");
        foreach (var item in state.StackItems)
            if (item.CompletedHandExecutions < 0 || item.CompletedHandExecutions > item.EffectRepeatCount
                || item.CompletedHandExecutions > 0 && !CoreRuleEngine.TryGetRevealedHandChoiceSpec(item, out _))
                errors.Add("invalid revealed hand execution count");
        if (state.PendingEffectPlay is { Parent.DeckPlaySequence: null, Parent.ObservedPlayPermission: null } handPlay && (handPlay.RevealedHand is not null || handPlay.IgnoreAllCosts
            || CardBehaviorRegistry.TryGetByEffectKind(handPlay.Parent.EffectKind, out var handDefinition)
                && handDefinition.EffectPlaySourceZone == "OPPONENT_HAND")
            && !CoreRuleEngine.ValidRevealedHandPlay(state, handPlay)
            && !CoreRuleEngine.ValidTranspositionMageEffectPlay(state, handPlay))
            errors.Add("invalid revealed hand effect-play continuation");
        if (state.PendingCardChoice is { ChoiceWindow: "RECYCLE_FOR_EFFECT_PLAY" } recycling
            && !CoreRuleEngine.ValidRecyclingChoice(state, recycling)) errors.Add("invalid recycling cost choice");
        foreach (var item in state.StackItems)
        {
            if (!CoreRuleEngine.ValidFlowPlay(state, item)) errors.Add("invalid flow play receipt");
            if (!CoreRuleEngine.ValidConquestFlowStack(state, item)) errors.Add("invalid conquest flow stack");
            if (item.RecycledUnit is not null && !CoreRuleEngine.ValidRecycledUnit(item)) errors.Add("invalid recycled unit receipt");
            if (item.RecastContext is { } context ? !CoreRuleEngine.ValidRecastContext(context, item.EffectKind, item.CardNo)
                : item.EffectKind == CoreRuleEngine.RecastTriggerEffect) errors.Add("invalid effect-play trigger context");
            if (item.AfterPlayRecycle is { } recycle && (item.PlayCost is null || !CoreRuleEngine.ValidRecycleInstruction(recycle)
                || !state.CardObjects.ContainsKey(recycle.SourceObjectId))) errors.Add("invalid after-play recycle instruction");
        }
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidConquestFlow(state, trigger.ConquestFlow, trigger.EffectKind, trigger.SourceObjectId)
                || trigger.ConquestFlow is not null && trigger.TriggeredByEventKind is not ("BATTLEFIELD_CONQUERED" or "BATTLEFIELD_HELD"))
                errors.Add("invalid queued conquest flow context");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.RecastContext is { } context ? !CoreRuleEngine.ValidRecastContext(context, trigger.EffectKind)
                || trigger.TriggeredByEventKind != "BATTLEFIELD_CONQUERED"
                : trigger.EffectKind == CoreRuleEngine.RecastTriggerEffect) errors.Add("invalid queued effect-play trigger context");
        if (state.PendingEffectPlay is { } recast && recast.SourceReplay is null && (recast.DestinationPolicy == "STACK" || recast.Parent.RecastContext is not null) && !CoreRuleEngine.ValidRecastPending(state, recast))
            errors.Add("invalid spell effect-play continuation");

        if (state.PendingEffectPlay is { SourcePlayerId: not null } foreignPlay && !CoreRuleEngine.ValidEmpowerInteractionPlay(state, foreignPlay))
            errors.Add("invalid foreign graveyard effect-play continuation");
        if (state.PendingEffectPlay is { } graveyard && graveyard.Parent.RecastContext is null
            && graveyard.DestinationPolicy != "STACK"
            && (graveyard.SourceZone == "GRAVEYARD"
                || CardBehaviorRegistry.TryGetByEffectKind(graveyard.Parent.EffectKind, out var graveyardBehavior)
                    && graveyardBehavior.EffectPlaySourceZone == "GRAVEYARD")
            && !CoreRuleEngine.ValidGraveyardUnitEffectPlay(state, graveyard))
            errors.Add("invalid graveyard unit effect-play continuation");

        if (state.PendingEffectPlay is { } banished && BanishPlayRecoveryRules.Applies(banished)
            && !BanishPlayRecoveryRules.Valid(state, banished))
            errors.Add("invalid banished unit effect-play continuation");

        foreach (var item in state.StackItems)
        {
            if (item.PlayCost is { } cost && (cost.CardMana < 0 || cost.PaidMana < 0
                || item.SpellContext is not null || !CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
                || behavior.PlaysSourceToBaseAsUnit || behavior.PlaysSourceToBaseAsEquipment)) errors.Add("invalid card play cost receipt");
            if (item.SpellContext is { } context ? !CoreRuleEngine.ValidSpellContext(context, item.EffectKind, item.CardNo)
                || !CoreRuleEngine.ValidSpellContextSource(state, context, item.SourceObjectId)
                || !state.Seats.ContainsKey(context.PlayedOwnerId)
                || item.TargetGenerations is not null && (item.TargetObjectIds.Count != 1 || item.TargetGenerations.Count != 1
                    || !item.TargetGenerations.ContainsKey(item.TargetObjectIds[0]))
                : item.EffectKind == CoreRuleEngine.SpellTriggerEffect) errors.Add("invalid spell completion trigger");
        }
        foreach (var trigger in state.TriggerQueue)
            if (trigger.SpellContext is { } context ? !CoreRuleEngine.ValidSpellContext(context, trigger.EffectKind)
                || !CoreRuleEngine.ValidSpellContextSource(state, context, trigger.SourceObjectId)
                || !state.Seats.ContainsKey(context.PlayedOwnerId)
                : trigger.EffectKind == CoreRuleEngine.SpellTriggerEffect) errors.Add("invalid queued spell completion trigger");
        var spellInstances = state.StackItems.Where(i => i.SpellContext is not null)
            .Select(i => (Source: i.SourceObjectId, Context: i.SpellContext!))
            .Concat(state.TriggerQueue.Where(i => i.SpellContext is not null)
                .Select(i => (Source: i.SourceObjectId, Context: i.SpellContext!)));
        if (spellInstances.GroupBy(i => (i.Source, i.Context.SourceGeneration, i.Context.InstanceId,
                i.Context.Kind, i.Context.PlayedObjectId, i.Context.PlayedGeneration)).Any(group => group.Count() > 1))
            errors.Add("duplicate spell completion trigger instance");
        if (state.PendingCardChoice is { ChoiceWindow: "SPELL_TRIGGER_CONFIRMATION" } spellChoice
            && !CoreRuleEngine.ValidSpellTriggerChoice(state, spellChoice)) errors.Add("invalid spell trigger confirmation");
        if (state.LinkedExiles.Any(g => string.IsNullOrWhiteSpace(g.SourceObjectId) || g.SourceGeneration < 0
            || !state.Seats.ContainsKey(g.ControllerId) || g.Objects.Any(e => string.IsNullOrWhiteSpace(e.Key) || e.Value < 0))
            || state.LinkedExiles.Select(g => (g.SourceObjectId, g.SourceGeneration, g.ControllerId)).Distinct().Count() != state.LinkedExiles.Count)
            errors.Add("invalid linked exile ledger");
        if (state.DrawLedger.TurnNumber < 0 || state.DrawLedger.TurnNumber > state.TurnNumber
            || state.DrawLedger.Counts.Any(e => !state.Seats.ContainsKey(e.Key) || e.Value < 0)) errors.Add("invalid turn draw ledger");
        if (!CoreRuleEngine.ValidCardPlayHistory(state)) errors.Add("invalid typed card play history");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.FieldContext is { } field ? (field.Kind == CoreRuleEngine.SelfActionPowerKind
                && (trigger.SourceCardNo != field.CardNo || trigger.TriggeredByEventKind != (field.SelfAction?.Action == "READIED" ? "UNIT_READY" : "TARGET_CONFIRMED")))
                || !CoreRuleEngine.ValidFieldContextState(state, field,
                trigger.SourceObjectId, trigger.ControllerId, trigger.EffectKind)
                    || !CoreRuleEngine.ValidAttackMightDamageTrigger(trigger)
                    || !CoreRuleEngine.ValidDisarmTrigger(trigger)
                    || field.Kind == CoreRuleEngine.BombDestructionKind && (trigger.TriggeredByEventKind is not ("EQUIPMENT_DESTROYED" or "OBJECT_DESTROYED" or "UNIT_DESTROYED") || trigger.SourceCardNo != field.CardNo)
                    || field.Kind == CoreRuleEngine.EquipmentDeathReturnKind && (trigger.TriggeredByEventKind is not ("EQUIPMENT_DESTROYED" or "OBJECT_DESTROYED" or "UNIT_DESTROYED") || trigger.SourceCardNo != field.CardNo)
                    || field.Kind == "RECYCLE_BOON" && trigger.TriggeredByEventKind != "CARDS_RECYCLED"
                    || field.Kind == CoreRuleEngine.AttackAreaDamageKind && trigger.TriggeredByEventKind is not ("BATTLE_DECLARED" or "BATTLE_ROLES_ESTABLISHED" or "BATTLE_ROLE_ACQUIRED")
                    || field.Kind == CoreRuleEngine.EmpoweredAttackDestroyKind && (trigger.SourceCardNo != field.CardNo
                        || trigger.TriggeredByEventKind is not ("BATTLE_DECLARED" or "BATTLE_ROLES_ESTABLISHED" or "BATTLE_ROLE_ACQUIRED"))
                    || field.Kind == CoreRuleEngine.AttackWeaponDamageKind && (trigger.SourceCardNo != field.CardNo
                        || trigger.TriggeredByEventKind is not ("BATTLE_DECLARED" or "BATTLE_ROLES_ESTABLISHED" or "BATTLE_ROLE_ACQUIRED"))
                    || CoreRuleEngine.IsPaidUnitTriggerKind(field.Kind) && (field.PaidUnitTrigger is not { CostPaid: false, DiscardCost: null } paid
                        || trigger.SourceCardNo != field.CardNo || (paid.Event == "PLAY"
                            ? trigger.TriggeredByEventKind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")
                            : trigger.TriggeredByEventKind is not ("BATTLE_DECLARED" or "BATTLE_ROLES_ESTABLISHED" or "BATTLE_ROLE_ACQUIRED")))
                    || field.Kind is CoreRuleEngine.StunReadyPowerKind or CoreRuleEngine.StunBoonKind
                        && (trigger.TriggeredByEventKind != "STATUS_EFFECT_APPLIED" || trigger.SourceCardNo != field.CardNo)
                    || field.Kind == CoreRuleEngine.AttackGraveyardBanishKind && trigger.TriggeredByEventKind is not ("BATTLE_DECLARED" or "BATTLE_ROLES_ESTABLISHED" or "BATTLE_ROLE_ACQUIRED")
                    || field.Kind == CoreRuleEngine.EmpoweredBanishKind && (trigger.TriggeredByEventKind != "OBJECT_EMPOWERED"
                        || trigger.SourceCardNo != field.CardNo || !trigger.TriggerId.EndsWith("-" + field.EmpoweredTransition!.RuleTextInstanceId, StringComparison.Ordinal))
                : trigger.EffectKind == CoreRuleEngine.FieldTriggerEffect) errors.Add("invalid field trigger context");
        foreach (var item in state.StackItems)
            if (item.FieldContext is { } field ? (field.Kind == CoreRuleEngine.SelfActionPowerKind
                && (item.TargetObjectIds.Count != 0 || item.TargetGenerations is not null || item.OptionalCosts.Count != 0
                    || item.DamageAmount != 0 || item.EffectRepeatCount != 1 || item.Destination.Length != 0))
                || !CoreRuleEngine.ValidFieldContextState(state, field,
                item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
                || !CoreRuleEngine.ValidAttackMightDamageStack(state, item)
                || !CoreRuleEngine.ValidDisarmStack(item)
                || !CoreRuleEngine.ValidAttackRallyStack(item)
                || !CoreRuleEngine.ValidBombDestructionStack(item)
                || !CoreRuleEngine.ValidEquipmentDeathStack(item)
                || !CoreRuleEngine.ValidDiscardDestroyStack(state, item)
                || item.TargetGenerations is not null && (item.TargetObjectIds.Count != 1 || item.TargetGenerations.Count != 1
                    || !item.TargetGenerations.ContainsKey(item.TargetObjectIds[0]) || item.TargetGenerations.Values.Any(g => g < 0))
                || field.Kind == CoreRuleEngine.PaidUnitStunKind && (item.OptionalCosts.Count != 0 || item.DamageAmount != 0
                    || item.EffectRepeatCount != 1 || item.Destination.Length != 0
                    || item.TargetGenerations is null && (item.TargetObjectIds.Count != 0 || field.PaidUnitTrigger!.CostPaid)
                    || item.TargetGenerations is not null && (field.PaidUnitTrigger!.CostPaid
                        ? state.PendingPayment?.ResolvingStackItemId == item.StackItemId
                        : state.PendingPayment is not { PaymentWindow: CoreRuleEngine.TriggerTargetCostWindow } pendingPaidUnit
                            || pendingPaidUnit.ResolvingStackItemId != item.StackItemId))
                || field.Kind == CoreRuleEngine.EmpoweredBanishKind && (!item.StackItemId.EndsWith("-" + field.EmpoweredTransition!.RuleTextInstanceId, StringComparison.Ordinal)
                    || item.OptionalCosts.Count != 0 || item.DamageAmount != 0 || item.EffectRepeatCount != 1 || item.Destination.Length != 0
                    || item.TargetGenerations is null && item.TargetObjectIds.Count != 0)
                || field.Kind == CoreRuleEngine.EmpoweredAttackDestroyKind && (item.OptionalCosts.Count != 0 || item.DamageAmount != 0
                    || item.EffectRepeatCount != 1 || item.Destination.Length != 0
                    || item.TargetGenerations is null && item.TargetObjectIds.Count != 0)
                || field.Kind == CoreRuleEngine.AttackWeaponDamageKind && (item.OptionalCosts.Count != 0 || item.DamageAmount != 0
                    || item.EffectRepeatCount != 1 || item.Destination.Length != 0
                    || item.TargetGenerations is null && item.TargetObjectIds.Count != 0)
                || field.ReturnFocusPlayerId is not null && !state.Seats.ContainsKey(field.ReturnFocusPlayerId)
                || field.Kind == CoreRuleEngine.AttackGraveyardBanishKind && (item.OptionalCosts.Count != 0
                    || item.DamageAmount != 0 || item.EffectRepeatCount != 1 || item.Destination.Length != 0
                    || item.TargetGenerations is null && item.TargetObjectIds.Count != 0)
                || field.Kind is "ENEMY_ATTACK_MIGHT" or "ATTACK_DECK_PLAY" or CoreRuleEngine.AttackAreaDamageKind or CoreRuleEngine.StunReadyPowerKind or "BATTLE_DOUBLE_SELF_ATTACK" or "BATTLE_DOUBLE_SELF_DEFEND" && (item.TargetObjectIds.Count != 0 || item.TargetGenerations is not null
                    || item.OptionalCosts.Count != 0 || item.DamageAmount != 0 || item.EffectRepeatCount != 1
                    || item.Destination.Length != 0 || item.TargetedPowerAbility is not null || item.DamageDistribution is not null)
                : item.EffectKind == CoreRuleEngine.FieldTriggerEffect) errors.Add("invalid field trigger stack context");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.TriggerHandDiscardWindow } discardCost
            && !CoreRuleEngine.ValidTriggerHandDiscardChoice(state, discardCost)) errors.Add("invalid trigger hand discard choice");
        if (state.PendingCardChoice is { ChoiceWindow: "TRIGGER_CONFIRMATION" } confirmation
            && !CoreRuleEngine.ValidTriggerChoice(state, confirmation)) errors.Add("invalid trigger confirmation");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.EffectDamageWindow } allocation
            && !CoreRuleEngine.ValidDistributionAllocation(state, allocation)) errors.Add("invalid effect damage distribution");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.DamageDistribution is { } distribution
                ? distribution.ResolutionDamage is not null || !CoreRuleEngine.ValidDistributionContext(state, distribution,
                    trigger.SourceObjectId, trigger.ControllerId, trigger.EffectKind)
                : trigger.EffectKind == CoreRuleEngine.DistributedDamageEffect) errors.Add("invalid distributed damage trigger");

        if (state.PendingPayment is { } payment
            && (payment.PaymentWindow is not (CoreRuleEngine.TriggerCostWindow or CoreRuleEngine.TriggerTargetCostWindow or CoreRuleEngine.TemperedPaymentWindow or CoreRuleEngine.MightyCostWindow) && payment.ResolvingStackItemId is not null || payment.PaymentWindow == "INSIGHT_EFFECT")
            && !IsUnitConquestReturnPayment(state, payment)
            && !CoreRuleEngine.ValidInsightPayment(state, payment)) errors.Add("invalid Insight payment continuation");
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidConquestSourceTrigger(state, trigger)) errors.Add("invalid conquest source trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidConquestSourceStack(state, item)) errors.Add("invalid conquest source stack");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.ConquestToken is { } token
                ? !CoreRuleEngine.ValidConquestToken(state, token, trigger.SourceObjectId, trigger.EffectKind)
                    || trigger.TriggeredByEventKind != "BATTLEFIELD_CONQUERED"
                : trigger.EffectKind is TriggerKinds.UnitConquestCreateDormantGold or TriggerKinds.UnitConquestCreateUnitToken)
                errors.Add("invalid captured conquest token trigger");
        foreach (var item in state.StackItems)
            if (item.ConquestToken is { } token
                ? !CoreRuleEngine.ValidConquestToken(state, token, item.SourceObjectId, item.EffectKind, item.CardNo)
                    || item.TargetObjectIds.Count != 0 || item.OptionalCosts.Count != 0 || item.EffectRepeatCount != 1
                : item.EffectKind is TriggerKinds.UnitConquestCreateDormantGold or TriggerKinds.UnitConquestCreateUnitToken)
                errors.Add("invalid captured conquest token stack item");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.SivirRuneRecycle is { } sivir
                ? !CoreRuleEngine.ValidSivirRuneContext(sivir, trigger.EffectKind)
                    || !CoreRuleEngine.ValidSivirRuneSource(state, sivir, trigger.SourceObjectId)
                : trigger.EffectKind == CoreRuleEngine.SivirRuneRecycleEffect)
                errors.Add("invalid captured Sivir rune recycle trigger");
        foreach (var item in state.StackItems)
            if (item.SivirRuneRecycle is { } sivir
                ? !CoreRuleEngine.ValidSivirRuneContext(sivir, item.EffectKind, item.CardNo)
                    || !CoreRuleEngine.ValidSivirRuneSource(state, sivir, item.SourceObjectId)
                    || item.TargetObjectIds.Count != 0 || item.OptionalCosts.Count != 0 || item.EffectRepeatCount != 1
                : item.EffectKind == CoreRuleEngine.SivirRuneRecycleEffect)
                errors.Add("invalid captured Sivir rune recycle stack item");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.InsightContext is { } captured
                ? captured.PaymentAccepted || !CoreRuleEngine.ValidInsightTrigger(captured, trigger.EffectKind, trigger.ControllerId)
                    || captured.Kind == "DUEL" && (trigger.TriggeredByEventKind != "SPELL_DUEL_STARTED" || trigger.SourceCardNo != captured.CardNo
                        || !state.Seats.ContainsKey(captured.ReturnFocusPlayerId!))
                    || captured.Kind == "EMPOWERED" && (trigger.TriggeredByEventKind != "OBJECT_EMPOWERED" || trigger.SourceCardNo != captured.CardNo)
                    || !CoreRuleEngine.ValidConquestLookSource(state,captured,trigger.SourceObjectId)
                    || !CoreRuleEngine.ValidInsightRuleTextSource(state,captured,trigger.SourceObjectId)
                    || !CoreRuleEngine.ValidPredictEntryBindings(state,captured,trigger.SourceObjectId)
                : trigger.EffectKind == CoreRuleEngine.InsightTriggerEffect)
                errors.Add("invalid captured Insight trigger");
        foreach (var item in state.StackItems)
        {
            if (!PermanentPlayTriggerRecoveryRules.Valid(state, item)) errors.Add("invalid battlefield-only permanent play trigger origin");
            if (!CoreRuleEngine.ValidPermanentPlayTargetStack(state, item)) errors.Add("invalid permanent play target confirmation");
            if (!CoreRuleEngine.ValidLocationDamageStack(state, item)) errors.Add("invalid per-location damage ability");
            if (!CoreRuleEngine.ValidDistributionStack(state, item)) errors.Add("invalid distributed damage ability");
            if (item.DamageDistribution?.ResolutionDamage is not null
                && (state.PendingCardChoice is not { ChoiceWindow: CoreRuleEngine.EffectDamageWindow } pendingDistribution
                    || pendingDistribution.ResolvingStackItemId != item.StackItemId)) errors.Add("orphan effect damage distribution");
            if (item.InsightContext is { } captured
                ? !CoreRuleEngine.ValidInsightTrigger(captured, item.EffectKind, item.ControllerId, item.CardNo)
                    || captured.Kind == "DUEL" && !state.Seats.ContainsKey(captured.ReturnFocusPlayerId!)
                    || !CoreRuleEngine.ValidConquestLookSource(state,captured,item.SourceObjectId)
                    || !CoreRuleEngine.ValidInsightRuleTextSource(state,captured,item.SourceObjectId)
                    || !CoreRuleEngine.ValidPredictEntryBindings(state,captured,item.SourceObjectId)
                : item.EffectKind == CoreRuleEngine.InsightTriggerEffect)
                errors.Add("invalid captured Insight stack item");
            if (item.InsightCompleted)
                errors.Add("completed Insight must resume atomically and cannot remain on stack");
            if (item.CompletedDeckExecutions < 0 || item.CompletedDeckExecutions > item.EffectRepeatCount
                || (item.DeckChoiceCompleted && item.CompletedDeckExecutions < item.EffectRepeatCount))
                errors.Add("invalid deck resolution continuation count");
            if (!CoreRuleEngine.ValidSequentialMovementPlan(state, item.EffectKind, item.TargetObjectIds.Count, item.MovementDestinations))
                errors.Add("invalid sequential movement plan");
            if (item.RepeatExecutions is { } executions && (executions.Count != item.EffectRepeatCount
                || executions.Count > 0 && (executions[0] is null || executions[0].Destination != item.Destination)
                || executions.Any(execution => execution is null || execution.TargetObjectIds is null || execution.TargetGenerations is null
                    || execution.TargetGenerations.Values.Any(generation => generation < 0)
                    || !CardBehaviorRegistry.TryGetByEffectKind(execution.EffectKind, out var definition)
                    || definition.CardNo != item.CardNo || !CoreRuleEngine.SupportsSeparateExecution(definition)
                    || !CoreRuleEngine.ValidRepeatDestination(state, item.ControllerId, execution)
                    || !CoreRuleEngine.ValidSequentialMovementPlan(state, execution.EffectKind, execution.TargetObjectIds.Count, execution.MovementDestinations))))
                errors.Add("invalid spell repeat execution plan");
        }
        if(state.DeckObservationView is not null && state.PendingRuleChoice is not {Request.Kind:"DECK_OBSERVATION_BANISH"})
            errors.Add("invalid observed deck visibility outside static choice");
        if(state.ObservedDeckPlays is { } observedHistory && (observedHistory.Any(c=>c is null || c.Card is null || c.Instruction is null)
            || observedHistory.Select(c=>c.Id).Distinct().Count()!=observedHistory.Count
            || observedHistory.Select(c=>c.Card.ObjectId).Distinct().Count()!=observedHistory.Count
            || observedHistory.Any(c=>state.StackItems.Count(s=>s.ObservedDeckPlay?.Id==c.Id)!=1)))
            errors.Add("invalid observed deck pending history membership");
        foreach(var observed in state.ObservedDeckPlays??[])
            if(observed is null || !CoreRuleEngine.ValidObservedDeckPlay(state,observed))errors.Add("invalid observed deck play history");
        if(!CoreRuleEngine.ValidObservedDeckOrder(state))errors.Add("invalid observed deck pending order");
        if(state.PendingEffectPlay is { } observedPending && !CoreRuleEngine.ValidObservedPlayPermission(state,observedPending))
            errors.Add("invalid observed deck play permission");
        foreach (var deckItem in state.StackItems)
        {
            if (!CoreRuleEngine.ValidObservedDeckSlot(state,deckItem))errors.Add("invalid observed deck pending slot");
            if (!CoreRuleEngine.ValidEndAbility(state, deckItem)) errors.Add("invalid end-step ability");
            if (!CoreRuleEngine.ValidDestroyDeckPlayContext(state, deckItem)) errors.Add("invalid destroy deck play context");
            if (!CoreRuleEngine.ValidDeckPlaySequence(state, deckItem)) errors.Add("invalid deck play sequence");
            if (!CoreRuleEngine.ValidDeckPlayPendingCard(state, deckItem)) errors.Add("invalid deck pending card");
            if (!CoreRuleEngine.ValidDeckPlayReflexive(state, deckItem)) errors.Add("invalid deck play reflexive ability");
        }
        foreach (var trigger in state.TriggerQueue)
            if (!CoreRuleEngine.ValidEndTrigger(state, trigger)) errors.Add("invalid end-step trigger");
        foreach (var marker in (state.UntilEndOfTurnEffects ?? []).Where(s => s.StartsWith(CoreRuleEngine.DelayedEndReadyPrefix, StringComparison.Ordinal)))
            if (!CoreRuleEngine.ValidDelayedEndReady(state, marker)) errors.Add("invalid delayed end-step ability");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.EndRuneChoiceWindow } endRuneChoice
            && !CoreRuleEngine.ValidEndRuneChoice(state, endRuneChoice)) errors.Add("invalid end-step rune choice");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.MovementDiscardRuneChoiceWindow } movementRuneChoice
            && !CoreRuleEngine.ValidMovementDiscardRuneChoice(state, movementRuneChoice)) errors.Add("invalid movement discard rune choice");
        if (state.PendingEffectPlay is { Parent.DeckPlaySequence: not null } deckPending
            && !CoreRuleEngine.ValidDeckPlayPending(state, deckPending)) errors.Add("invalid selected banished deck play continuation");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.DeckPlayChoiceWindow } deckChoice)
        {
            if (!CoreRuleEngine.ValidDeckPlayChoice(state, deckChoice)) errors.Add("invalid deck play selection");
        }
        else if (state.PendingCardChoice is { ChoiceWindow: "DECK_EFFECT" } choice)
        {
            var top = state.StackItems.LastOrDefault();
            if (choice.DeckContext is not { } context || top is null || context.StackItemId != top.StackItemId
                || context.ExecutionIndex != top.CompletedDeckExecutions || top.DeckChoiceCompleted
                || choice.PlayerId != top.ControllerId || choice.SourceObjectId != top.SourceObjectId
                || !CoreRuleEngine.TryGetDeckChoiceBehavior(state,top, out var behavior)
                || !(CoreRuleEngine.ObservedRemainingDeckCards(state,top,top.ControllerId,top.CompletedDeckExecutions)
                    ?? state.PlayerZones[top.ControllerId].MainDeck.Take(behavior.MainDeckLookCount).ToArray()).SequenceEqual(choice.ContextObjectIds))
                errors.Add("invalid private deck choice continuation");
            else
            {
                var legal = choice.ContextObjectIds.Where(id => CoreRuleEngine.DeckChoiceTargetAllowed(state, behavior, id)).ToArray();
                var required = behavior.MinTargetCount != 0 && legal.Length > 0 ? 1 : 0;
                if (!legal.SequenceEqual(choice.LegalObjectIds) || choice.RequiredCount != required
                    || choice.MaxCount != (legal.Length > 0 ? 1 : 0) || choice.EffectKind != top.EffectKind)
                    errors.Add("invalid private deck choice permissions");
            }
        }
        else if (state.PendingCardChoice is { ChoiceWindow: "INSIGHT" or "INSIGHT_ORDER" } insight)
        {
            if (!CoreRuleEngine.ValidInsightChoice(state, insight, state.StackItems.LastOrDefault()))
                errors.Add("invalid private Insight continuation");
        }
        else if (state.PendingCardChoice?.DeckContext is not null)
            errors.Add("deck choice context outside private deck window");
    }

    private static SourceReplayProgress? ReadSourceReplayView(object? payload)
    {
        try {
            var json = JsonSerializer.SerializeToElement(payload);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("sourceReplay", out var raw)
                && raw.ValueKind != JsonValueKind.Null
                ? raw.Deserialize<SourceReplayProgress>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
        } catch (JsonException) { return null; }
    }

    private static void ValidateDelayedDestructionStackViews(IReadOnlyList<object?> payloads, MatchState state, List<string> errors)
    {
        foreach (var payload in payloads) {
            TryReadObjectString(payload, "stackItemId", out var id);
            var expected = state.StackItems.FirstOrDefault(i => i.StackItemId == id)?.DelayedDestruction;
            if (MatchStateHasher.HashValue(ReadDelayedDestructionView(payload)) != MatchStateHasher.HashValue(expected))
                errors.Add("public captured delayed destruction differs from authoritative state");
        }
    }

    private static void ValidateTemporaryActivationStackViews(IReadOnlyList<object?> payloads, MatchState state, List<string> errors)
    {
        foreach (var payload in payloads) {
            TryReadObjectString(payload, "stackItemId", out var id);
            var expected = state.StackItems.FirstOrDefault(item => item.StackItemId == id)?.TemporaryActivation;
            TryReadObjectString(payload, "grantedAbilityId", out var ability);
            if (ability != (expected is null ? null : CoreRuleEngine.TemporaryActivationId(expected.Grant)))
                errors.Add("public granted activation differs from authoritative state");
            if (expected is not null) {
                TryReadObjectString(payload, "abilityLabel", out var label);
                if (label != "本回合获得的活跃技能") errors.Add("invalid public granted activation label");
            }
            if (TryReadObjectValue(payload, "selfMovement", out _))
                errors.Add("public self movement exposes private source binding");
            if (TryReadObjectValue(payload, "temporaryActivation", out _))
                errors.Add("public granted activation exposes private source binding");
        }
    }

    private static void ValidateTurnSequenceState(MatchState state, List<string> errors)
    {
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidSelfMovement(state, item)) errors.Add("invalid captured self movement");
        if (!CoreRuleEngine.ValidCurrentTemporaryActivations(state)) errors.Add("invalid temporary activated ability grant");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidTemporaryActivation(state, item)) errors.Add("invalid captured temporary activation");
        if (!CoreRuleEngine.ValidCurrentConquestGrants(state)) errors.Add("invalid temporary conquest grant");
        if (!CoreRuleEngine.ValidCurrentBoonAmplifiers(state)) errors.Add("invalid temporary friendly Boon amplifier");
        if (!CoreRuleEngine.ValidTurnFlowGrants(state)) errors.Add("invalid temporary flow grant");
        if (!CoreRuleEngine.ValidCurrentTurnUnitPlayGrants(state)) errors.Add("invalid temporary unit play grant");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidTurnUnitPlayBoon(state, item.TurnUnitPlayBoon, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo))
                errors.Add("invalid captured unit play boon stack");
        foreach (var item in state.TriggerQueue)
            if (!CoreRuleEngine.ValidTurnUnitPlayBoon(state, item.TurnUnitPlayBoon, item.EffectKind, item.SourceObjectId, item.ControllerId, item.SourceCardNo))
                errors.Add("invalid captured unit play boon trigger");
        if (state.PendingEffectPlay is { Parent.DeckPlaySequence: null } sourcePending && (sourcePending.SourceReplay is not null || sourcePending.SourceZone == "STACK" && sourcePending.Parent.ObservedPlayPermission is null)
            && !CoreRuleEngine.ValidSourceReplayPending(state, sourcePending)) errors.Add("invalid source replay continuation");
        foreach (var item in state.StackItems)
            if (item.SourceReplay is { } sourceProgress ? !CoreRuleEngine.ValidSourceReplayProgress(state, sourceProgress, item)
                : item.SourceReplayDecisionCompleted || CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var replayBehavior) && replayBehavior.SourceReplay is not null) errors.Add("invalid source replay progress");
        if (state.PendingRuleChoice is null && state.StackItems.Any(i => i.SourceReplayDecisionCompleted))
            errors.Add("invalid premature source replay completion");
        ValidateDelayedEmpowerState(state, errors);
        ValidateBombRebuildState(state, errors);
        ValidateLastBreathUnitCreationState(state, errors);
        ValidateLastBreathRuneCallState(state, errors);
        ValidateLastBreathDrawState(state, errors);
        ValidateLastBreathBattlefieldDamageState(state, errors);
        ValidateDelayedDestructionState(state, errors);
        ValidateDestructionFamilyState(state, errors);
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidMovementFollowUp(state, item)) errors.Add("invalid movement optional attachment continuation");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidResolutionDamageChoice(state, item)) errors.Add("invalid target controller damage alternative choice");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.ResolutionDamageChoiceWindow } damageChoice
            && !state.StackItems.Any(i => i.StackItemId == damageChoice.ResolvingStackItemId && i.ResolutionDamageChoice is not null))
            errors.Add("orphan target controller damage alternative choice");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.MovementAttachmentWindow } movementChoice
            && !state.StackItems.Any(i => i.StackItemId == movementChoice.ResolvingStackItemId && i.MovementFollowUp is not null))
            errors.Add("orphan movement optional attachment choice");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.AzirAttachmentWindow } azirChoice
            && !CoreRuleEngine.ValidAzirAttachmentChoice(state, azirChoice)) errors.Add("invalid Azir optional attachment choice");
        if (state.StackItems.Any(i => i.AzirMovement is { MovementsCompleted: true } ctx
            && (ctx.AttachmentChosen is not null || state.PendingCardChoice is not { } pending
                || pending.ResolvingStackItemId != i.StackItemId || pending.ChoiceWindow != CoreRuleEngine.AzirAttachmentWindow))) errors.Add("invalid Azir movement continuation");
        if (!CoreRuleEngine.ValidRuleTextModeLedger(state)) errors.Add("invalid printed text instance mode-use ledger");
        if (state.StackItems.Any(item => !CoreRuleEngine.ValidExhaustionModeStack(state, item))) errors.Add("invalid captured exhaustion mode ability");
        if (!CoreRuleEngine.ValidExhaustReadyUses(state)
            || state.StackItems.Any(item => !CoreRuleEngine.ValidExhaustReadyActivation(state, item)))
            errors.Add("invalid captured exhaust-ready activation");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.AttachmentModeWindow or CoreRuleEngine.AttachmentModeTargetWindow } modeChoice
            && !CoreRuleEngine.ValidAttachmentModeChoice(state, modeChoice)) errors.Add("invalid attachment mode confirmation");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.ExhaustionModeWindow or CoreRuleEngine.ExhaustionModeTargetWindow } exhaustionChoice
            && !CoreRuleEngine.ValidExhaustionModeChoice(state, exhaustionChoice)) errors.Add("invalid exhaustion mode confirmation");
        if (state.StackItems.Any(item => !CoreRuleEngine.ValidConfirmedAttachmentMode(state, item))) errors.Add("invalid confirmed attachment mode");
        foreach (var resolution in state.BattlefieldResolutions)
            if (resolution.PublicObjectBindings is { } publicRefs && (publicRefs.Any(b => b is null || b.Generation < 0
                || string.IsNullOrWhiteSpace(b.ObjectId) || !resolution.ParticipantObjectIds.Contains(b.ObjectId)
                || state.CardObjects.TryGetValue(b.ObjectId, out var current) && (b.Generation > current.ObjectGeneration
                    || b.Generation == current.ObjectGeneration && state.PlayerZones.Values.Any(z => z.Hand.Contains(b.ObjectId)
                        || z.MainDeck.Contains(b.ObjectId) || z.RuneDeck.Contains(b.ObjectId))))
                || publicRefs.Select(b => b.ObjectId).Distinct(StringComparer.Ordinal).Count() != publicRefs.Count))
                errors.Add("invalid captured public battlefield history reference");
        foreach (var resolution in state.BattleResolutions)
            if (resolution.PublicObjectBindings is { } publicRefs && (publicRefs.Any(b => b is null || b.Generation < 0
                || string.IsNullOrWhiteSpace(b.ObjectId)
                || !resolution.AttackerObjectIds.Concat(resolution.DefenderObjectIds).Contains(b.ObjectId)
                || state.CardObjects.TryGetValue(b.ObjectId, out var current) && (b.Generation > current.ObjectGeneration
                    || b.Generation == current.ObjectGeneration && state.PlayerZones.Values.Any(z => z.Hand.Contains(b.ObjectId)
                        || z.MainDeck.Contains(b.ObjectId) || z.RuneDeck.Contains(b.ObjectId))))
                || publicRefs.Select(b => b.ObjectId).Distinct(StringComparer.Ordinal).Count() != publicRefs.Count
                || resolution.ResolutionId != $"battle-result:{resolution.Tick}:{resolution.Kind}:{resolution.BattlefieldId}:{resolution.AttackerObjectIds.FirstOrDefault() ?? resolution.AttackingPlayerId}"))
                errors.Add("invalid captured public battle history reference");
        foreach (var card in state.CardObjects.Values)
        {
            if (!EmpowerRules.ValidCount(card)) errors.Add("invalid empowerment count");
            if (card.StandbyPlacement is not null && !StandbyRules.ValidPlacement(state, card)) errors.Add("invalid standby placement provenance");
            if (!StandbyRules.ValidLastPlay(state, card)) errors.Add("invalid last standby play provenance");
            if (!AttachedRuleTextRules.ValidState(state, card)) errors.Add("invalid attached printed rule text instance");
            if (!CopyFaceTimeline.Valid(state, card)) errors.Add("invalid copied face timeline");
            if (!AttachmentUnitCopyRules.Valid(state, card)) errors.Add("invalid attached unit copy ledger");
            foreach (var modifier in card.UntilEndOfTurnPowerModifiers ?? [])
                if (!EmpoweredSpellRules.Valid(state, card, modifier)) errors.Add("invalid captured chosen negative Might replacement");
            if (card.IsEmpowered && !state.PlayerZones.Values.Any(z => z.Base.Contains(card.ObjectId) || z.Battlefields.Contains(card.ObjectId)
                || z.LegendZone.Contains(card.ObjectId))) errors.Add("invalid empowered object outside play");
        }
        if (state.StackItems.Any(item => !StandbyRules.ValidPlayedOrigin(state, item))) errors.Add("invalid played standby origin");
        foreach (var item in state.StackItems)
            if (item.StandbyObserver is { } observer ? !CoreRuleEngine.ValidStandbyObserver(state, observer, item.EffectKind, item.SourceObjectId, item.CardNo)
                : item.EffectKind == CoreRuleEngine.StandbyPlayedEffect) errors.Add("invalid captured standby play observer");
        foreach (var trigger in state.TriggerQueue)
            if (trigger.StandbyObserver is { } observer ? !CoreRuleEngine.ValidStandbyObserver(state, observer, trigger.EffectKind, trigger.SourceObjectId, trigger.SourceCardNo)
                : trigger.EffectKind == CoreRuleEngine.StandbyPlayedEffect) errors.Add("invalid queued standby play observer");
        if (state.PendingPayment is { } referencedPayment && (referencedPayment.ExperienceCost < 0
            || referencedPayment.ExperienceCost != 0 && referencedPayment.PaymentWindow is not (CoreRuleEngine.TemperedPaymentWindow or CoreRuleEngine.TriggerTargetCostWindow)))
            errors.Add("invalid referenced equipment experience cost");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidAttachmentObserver(item.AttachmentObserver, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
                || !CoreRuleEngine.ValidAttachmentGenerations(state, item.AttachmentObserver, item.SourceObjectId)
                || item.AttachmentObserver is not null && (item.SourceConfirmed
                    || item.AttachmentObserver.References is null && item.TargetObjectIds.Count != 0))
                errors.Add("invalid captured attachment observer");
        foreach (var item in state.TriggerQueue)
            if (!CoreRuleEngine.ValidAttachmentObserver(item.AttachmentObserver, item.EffectKind, item.ControllerId, item.SourceObjectId)
                || !CoreRuleEngine.ValidAttachmentGenerations(state, item.AttachmentObserver, item.SourceObjectId)
                || item.AttachmentObserver is not null && item.TriggeredByEventKind != "EQUIPMENT_ATTACHED")
                errors.Add("invalid captured attachment trigger");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.TemperedConfirmationWindow } temperedChoice
            && !CoreRuleEngine.ValidTemperedChoice(state, temperedChoice)) errors.Add("invalid weaponmaster confirmation");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.TemperedCostWindow } temperedCost
            && !CoreRuleEngine.ValidTemperedCostChoice(state, temperedCost)) errors.Add("invalid weaponmaster cost choice");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.TemperedBaseCostWindow } temperedBaseCost
            && !CoreRuleEngine.ValidTemperedBaseCostChoice(state, temperedBaseCost)) errors.Add("invalid weaponmaster base cost choice");
        if (state.PendingPayment is { PaymentWindow: CoreRuleEngine.TemperedPaymentWindow } temperedPayment
            && !CoreRuleEngine.ValidTemperedPayment(state, temperedPayment)) errors.Add("invalid weaponmaster payment");
        foreach (var item in state.TriggerQueue)
            if (!CoreRuleEngine.ValidTempered(state, item.Tempered, item.EffectKind, item.ControllerId, item.SourceObjectId, item.SourceCardNo)
                || item.Tempered is { } capturedTempered && (capturedTempered.Equipment is not null || capturedTempered.Costs is not null
                    || capturedTempered.UnableToPay || item.TriggeredByEventKind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")))
                errors.Add("invalid captured weaponmaster trigger");
        foreach (var item in state.StackItems)
        {
            if (!CoreRuleEngine.ValidTemperedStack(state, item))
                errors.Add("invalid captured weaponmaster ability");
            if (!NamedUnitSwapRules.Valid(state, item)) errors.Add("invalid captured named unit swap");
            if (!CoreRuleEngine.ValidAzirMovement(state, item)) errors.Add("invalid captured Azir movement");
            if (!SourceBoonMightRules.Valid(state, item)) errors.Add("invalid source boon ability");
            if (!EmpowerRules.Valid(state, item)) errors.Add("invalid captured empower activation");
            if (!SelfMightRules.Valid(state, item)) errors.Add("invalid captured self Might activation");
            if (!RecycleMightRules.Valid(state, item)) errors.Add("invalid captured recycle-cost ability");
            if (!CoreRuleEngine.ValidLegendAttachment(state, item)) errors.Add("invalid captured legend attachment");
            if (!CoreRuleEngine.ValidLegendDuelMovement(state, item)) errors.Add("invalid captured legend duel movement");
            if (!CoreRuleEngine.ValidLegendUnitKeyword(state, item)) errors.Add("invalid captured legend unit keyword");
            if (!CoreRuleEngine.ValidLegendUnitEffect(state, item)) errors.Add("invalid captured legend unit effect");
            if (!CoreRuleEngine.ValidLegendEquipmentReady(state, item)) errors.Add("invalid captured legend equipment effect");
            if (!CoreRuleEngine.ValidLegendDiscardDraw(state, item)) errors.Add("invalid captured legend discard effect");
        }
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidEquipContext(item)) errors.Add("invalid captured equip ability");
        foreach (var item in state.StackItems)
            if (!BattlefieldStartRecoveryRules.Valid(state, item.BattlefieldStart, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
                || item.BattlefieldStart is not null && (item.TargetObjectIds.Count != 0 || item.SourceConfirmed))
                errors.Add("invalid captured battlefield start ability");
        foreach (var item in state.TriggerQueue)
            if (!BattlefieldStartRecoveryRules.Valid(state, item.BattlefieldStart, item.EffectKind, item.ControllerId, item.SourceObjectId)
                || item.BattlefieldStart is not null && item.TriggeredByEventKind != "TURN_START_BEGAN")
                errors.Add("invalid captured battlefield start trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidEphemeralStart(state, item.EphemeralStart, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
                || item.EphemeralStart is not null && (item.TargetObjectIds.Count != 0 || item.SourceConfirmed))
                errors.Add("invalid captured ephemeral start ability");
        foreach (var item in state.TriggerQueue)
            if (!CoreRuleEngine.ValidEphemeralStart(state, item.EphemeralStart, item.EffectKind, item.ControllerId, item.SourceObjectId)
                || item.EphemeralStart is not null && item.TriggeredByEventKind != "TURN_START_BEGAN")
                errors.Add("invalid captured ephemeral start trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidBeginningRecycle(state, item.BeginningRecycle, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
                || item.BeginningRecycle is not null && (item.TargetObjectIds.Count != 0 || item.SourceConfirmed))
                errors.Add("invalid captured beginning recycle ability");
        foreach (var item in state.TriggerQueue)
            if (!CoreRuleEngine.ValidBeginningRecycle(state, item.BeginningRecycle, item.EffectKind, item.ControllerId, item.SourceObjectId)
                || item.BeginningRecycle is not null && item.TriggeredByEventKind != "TURN_START_BEGAN")
                errors.Add("invalid captured beginning recycle trigger");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidBeginningRune(state, item.BeginningRune, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
                || item.BeginningRune is not null && (item.TargetObjectIds.Count != 0 || item.SourceConfirmed))
                errors.Add("invalid captured beginning rune ability");
        foreach (var item in state.TriggerQueue)
            if (!CoreRuleEngine.ValidBeginningRune(state, item.BeginningRune, item.EffectKind, item.ControllerId, item.SourceObjectId)
                || item.BeginningRune is not null && item.TriggeredByEventKind != "TURN_START_BEGAN")
                errors.Add("invalid captured beginning rune trigger");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.GraveyardRecycleWindow } recycle
            && !CoreRuleEngine.ValidGraveyardRecycleChoice(state, recycle)) errors.Add("invalid graveyard recycle choice");
        foreach (var item in state.StackItems)
            if (!CoreRuleEngine.ValidBeginningDraw(state, item.BeginningDraw, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
                || item.BeginningDraw is not null && (item.TargetObjectIds.Count != 0 || item.SourceConfirmed))
                errors.Add("invalid captured beginning draw ability");
        foreach (var item in state.TriggerQueue)
            if (!CoreRuleEngine.ValidBeginningDraw(state, item.BeginningDraw, item.EffectKind, item.ControllerId, item.SourceObjectId)
                || item.BeginningDraw is not null && item.TriggeredByEventKind != "TURN_START_BEGAN")
                errors.Add("invalid captured beginning draw trigger");
        if (state.TurnStartStep is { } step && (step is not ("READY" or "START" or "SCORE" or "CHANNEL" or "DRAW" or "MAIN")
            || state.Phase != MatchPhases.TurnStart)) errors.Add("invalid turn-start continuation");
        if (state.EndStep is { } ending && (state.Phase != MatchPhases.TurnEnd || ending.PlayerId != state.TurnPlayerId
            || ending.TurnNumber != state.TurnNumber || !state.Seats.ContainsKey(ending.PlayerId))) errors.Add("invalid end-step continuation");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gain in state.DelayedResourceGains)
        {
            if (string.IsNullOrWhiteSpace(gain.Id) || !ids.Add(gain.Id)
                || !state.Seats.ContainsKey(gain.ControllerId) || gain.Power != 1
                || gain.DueTurn != state.TurnNumber || state.TurnStartStep is null
                || string.IsNullOrWhiteSpace(gain.SourceObjectId) || string.IsNullOrWhiteSpace(gain.BattlefieldObjectId)
                || !P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId(P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityId, gain.SourceCardNo))
                errors.Add("invalid captured delayed resource gain");
            // A captured delayed ability is independent of the source's current zone/controller.
        }
        foreach (var context in state.StackItems.Select(x => x.HeldContext)
            .Concat(state.TriggerQueue.Select(x => x.HeldContext))
            .Where(x => x is not null))
        {
            if (string.IsNullOrWhiteSpace(context!.CardNo) || string.IsNullOrWhiteSpace(context.BattlefieldObjectId)
                || context.SourceGeneration < 0 || context.Amount < 0
                || context.Kind is not ("MINION" or "DRAW" or "RETURN_HERO" or "BOON" or "ACTIVATE_CONQUEST"
                    or "CHANNEL_OPTIONAL" or "SEVEN_WIN" or "SCORE" or "RETURN_SELF" or "RETURN_PERMANENT"
                    or "ROBOT" or "GOLD" or "RENATA" or "PAY_POWER_SCORE" or "CHANNEL_ALL" or "BOON_ALL"
                    or "VEX" or "IVERN" or "BRUSH_RETURN" or "LEBLANC_DISCARD" or "EXPERIENCE" or "MOVE_BASE"
                    or CoreRuleEngine.UnitHeldRuneKind or CoreRuleEngine.UnitHeldHiddenScoreKind
                    or "BURN" or "NEXT_ECHO" or "UNIT_TAX" or "CONQUEST_UNIT" or "LOOK_EQUIPMENT" or "EXACT_OTHER_DRAW" or "EXACT_OTHER_SCORE" or CoreRuleEngine.DeploymentHeldKind)) errors.Add("invalid captured Hold context");
        }
        foreach (var item in state.StackItems.Where(x => x.HeldContext is not null))
            if (!CoreRuleEngine.ValidExactOtherFriendlyHeldContext(state, item.HeldContext!, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo))
                errors.Add("invalid captured exact other friendly Hold context");
        foreach (var item in state.TriggerQueue.Where(x => x.HeldContext is not null))
            if (!CoreRuleEngine.ValidExactOtherFriendlyHeldContext(state, item.HeldContext!, item.EffectKind, item.ControllerId, item.SourceObjectId, item.SourceCardNo ?? item.HeldContext!.CardNo)
                || item.HeldContext!.Kind == CoreRuleEngine.DeploymentHeldKind && item.TriggeredByEventKind != "BATTLEFIELD_HELD")
                errors.Add("invalid captured exact other friendly Hold context");
        if (state.StackItems.Any(x => x.EffectKind.StartsWith("HOLD_", StringComparison.Ordinal) && x.HeldContext is null)
            || state.TriggerQueue.Any(x => x.EffectKind.StartsWith("HOLD_", StringComparison.Ordinal) && x.HeldContext is null)
            || state.PendingPayment is { PaymentWindow: "HOLD_EFFECT" })
            errors.Add("missing captured Hold context");
    }

    private static void ValidateAuthoritativeStateScalars(
        MatchState authoritativeState,
        List<string> errors)
    {
        var roomId = ValidateAuthoritativeStateRequiredText(
            "room id",
            authoritativeState.RoomId,
            errors);
        RejectAuthoritativeStateRedactionSentinel("room id", roomId, errors);

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
            errors,
            rejectRedactionSentinel: true);
        ValidateAuthoritativeStateKnownText(
            "phase",
            authoritativeState.Phase,
            IsKnownMatchPhase,
            errors,
            rejectRedactionSentinel: true);
        ValidateAuthoritativeStateKnownText(
            "timing state",
            authoritativeState.TimingState,
            IsKnownTimingState,
            errors,
            rejectRedactionSentinel: true);

        if (authoritativeState.RngCursor < 0)
        {
            errors.Add($"authoritative state rng cursor {authoritativeState.RngCursor} cannot be negative");
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
            rejectDuplicates: true,
            rejectRedactionSentinel: true);
    }

    private static void ValidateTargetedPowerAbilityStackValues(MatchState state, List<string> errors)
    {
        if (state.StackItems is null || state.CardObjects is null) return;
        foreach (var item in state.StackItems) {
            if (item is null) continue;
            var recognized = P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var effect)
                && P4ActivatedAbilityCatalog.IsOrdinaryTargetedExhaustAbility(effect);
            var context = item.TargetedPowerAbility;
            if (context is null) {
                if (recognized) errors.Add($"stack item {item.StackItemId} targeted power ability context is required");
                continue;
            }
            if (!recognized || !P4ActivatedAbilityCatalog.TryGetByAbilityId(context.AbilityId, out var ability)
                || !P4ActivatedAbilityCatalog.IsOrdinaryTargetedExhaustAbility(ability)
                || ability.EffectKind != item.EffectKind
                || !TargetedPowerAbilitySourceMatches(state, item.ControllerId, item.SourceObjectId, ability, item.CardNo)
                || string.IsNullOrWhiteSpace(context.SourceObjectId)
                || context.SourceObjectId != item.SourceObjectId || context.SourceGeneration < 0
                || !state.CardObjects.TryGetValue(context.SourceObjectId, out var source)
                || source.ObjectGeneration < context.SourceGeneration
                || source.ObjectGeneration == context.SourceGeneration
                    && !TargetedPowerAbilitySourceMatches(state, item.ControllerId, source.ObjectId, ability, source.CardNo)
                || (ability.PowerModifierAmountIfSourceEmpowered is null ? context.LastKnownSourceEmpowered is not null
                    : context.LastKnownSourceEmpowered is null || source.ObjectGeneration == context.SourceGeneration
                        && state.PlayerZones.Values.Any(z => z.Base.Contains(source.ObjectId) || z.Battlefields.Contains(source.ObjectId))
                        && context.LastKnownSourceEmpowered != source.IsEmpowered)
                || item.DamageAmount != 0 || item.EffectRepeatCount != 1
                || item.TargetObjectIds is not { Count: 1 }
                || item.TargetGenerations is not { Count: 1 }
                || !item.TargetGenerations.TryGetValue(item.TargetObjectIds[0], out var targetGeneration) || targetGeneration < 0
                || item.SpellContext is not null || item.AzirMovement is not null || item.DamageDistribution is not null
                || item.UnitEvent is not null || item.EmpowerInteraction is not null || item.ReflexiveCopy is not null || item.CreatedTokenReady is not null || item.UnitEntryContext is not null || item.UnitPlayMight is not null || item.SourcePlayDraw is not null || item.SourceUnitTokens is not null || item.ReadyObjectTrigger is not null || item.SourceReadied is not null || item.TriggerCost is not null
                || item.Equip is not null || item.AttachmentObserver is not null || item.Tempered is not null
                || item.LegendAttachment is not null || item.LegendDuelMovement is not null || item.LegendUnitKeyword is not null || item.LegendUnitEffect is not null || item.LegendEquipmentReady is not null || item.LegendDiscardDraw is not null || item.EmpowerOtherObserver is not null || item.OutsideHandPlayObserver is not null || item.HighCostPlayObserver is not null || item.ConquestSource is not null || item.TokenEntryPlan is not null || item.RepeatExecutions is not null
                || item.MovementDestinations is not null || item.TargetStackSources is not null
                || item.AfterPlayRecycle is not null || item.RecycledUnit is not null || item.PlayCost is not null
                || item.RecastContext is not null || item.HeldContext is not null || item.InsightContext is not null
                || item.DeathRevealContext is not null || item.DeathObserver is not null || item.HandDiscardObserver is not null || item.MovementObserver is not null
                || item.MightySkill is not null || item.BattlefieldConquestMight is not null || item.BattlefieldConquestMill is not null || item.UnitConquestReturn is not null || item.FieldContext is not null || item.LegendConquest is not null || item.ReflexiveDuel is not null
                || item.BattlefieldStart is not null || item.EphemeralStart is not null || item.BeginningRecycle is not null || item.BeginningRune is not null
                || item.BeginningDraw is not null || item.LocationDamage is not null || item.UnitConquest is not null
                || item.CompletedRepeatExecutions != 0 || item.CompletedHandExecutions != 0 || item.CompletedDeckExecutions != 0
                || item.DeckChoiceCompleted || item.InsightCompleted || item.PlayedAfterAnotherCardThisTurn
                || item.SourceConfirmed || item.EffectPlayCompleted || !string.IsNullOrEmpty(item.Destination)
                || !string.IsNullOrEmpty(item.TimingContext) || item.OptionalCosts.Count != 0)
                errors.Add($"stack item {item.StackItemId} targeted power ability identity, source or effect shape is invalid");
        }
    }

    private static bool TargetedPowerAbilitySourceMatches(
        MatchState state,
        string playerId,
        string sourceObjectId,
        P4ActivatedAbilityDefinition ability,
        string? cardNo)
    {
        if (string.IsNullOrWhiteSpace(sourceObjectId) || string.IsNullOrWhiteSpace(cardNo))
        {
            return false;
        }

        if (ability.SourceScope == ActivatedAbilitySourceScopes.FriendlyLegend)
        {
            return state.PlayerZones.TryGetValue(playerId, out var zones)
                && zones.LegendZone.Contains(sourceObjectId, StringComparer.Ordinal)
                && state.CardObjects.TryGetValue(sourceObjectId, out var source)
                && !source.IsFaceDown
                && CoreRuleEngine.HasControllerCardOnField(state, playerId, ability.RequiredControllerCardNoOnField);
        }

        return P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, cardNo)
            && state.CardObjects.TryGetValue(sourceObjectId, out var ordinarySource)
            && ordinarySource.Tags.Contains(ability.SourceRequiredTag, StringComparer.Ordinal);
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
            if (string.Equals(stackItemId, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add("authoritative state stack item id must not be redacted");
            }

            if (stackItemId is not null && !seenStackItemIds.Add(stackItemId))
            {
                errors.Add($"authoritative state stack item {stackItemId} is duplicated");
            }

            if (string.Equals(stackItem.ControllerId, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add($"authoritative state stack item {stackItemLabel} controller player must not be redacted");
            }

            var effectKind = ValidateAuthoritativeStateRequiredText(
                $"stack item {stackItemLabel} effect kind",
                stackItem.EffectKind,
                errors);
            if (string.Equals(effectKind, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add($"authoritative state stack item {stackItemLabel} effect kind must not be redacted");
            }

            var sourceObjectId = ValidateAuthoritativeStateOptionalTextValue(
                $"stack item {stackItemLabel} source object",
                stackItem.SourceObjectId,
                errors);
            if (string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add($"authoritative state stack item {stackItemLabel} source object must not be redacted");
            }

            var cardNo = ValidateAuthoritativeStateOptionalTextValue(
                $"stack item {stackItemLabel} card no",
                stackItem.CardNo,
                errors);
            if (string.Equals(cardNo, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add($"authoritative state stack item {stackItemLabel} card no must not be redacted");
            }

            var destination = ValidateAuthoritativeStateOptionalTextValue(
                $"stack item {stackItemLabel} destination",
                stackItem.Destination,
                errors);
            if (string.Equals(destination, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add($"authoritative state stack item {stackItemLabel} destination must not be redacted");
            }

            var timingContext = ValidateAuthoritativeStateOptionalTextValue(
                $"stack item {stackItemLabel} timing context",
                stackItem.TimingContext,
                errors);
            if (string.Equals(timingContext, "HIDDEN", StringComparison.Ordinal))
            {
                errors.Add($"authoritative state stack item {stackItemLabel} timing context must not be redacted");
            }

            ValidateAuthoritativeStateStringListValues(
                $"stack item {stackItemLabel} target object",
                stackItem.TargetObjectIds,
                errors,
                rejectRedactionSentinel: true);
            ValidateAuthoritativeStateStringListValues(
                $"stack item {stackItemLabel} optional cost",
                stackItem.OptionalCosts,
                errors,
                rejectRedactionSentinel: true);

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

    private static void ValidateAuthoritativeStatePendingCardChoicePlayer(
        PendingCardChoiceState? pendingCardChoice,
        IReadOnlySet<string> seatPlayerIds,
        List<string> errors)
    {
        if (pendingCardChoice is null)
        {
            return;
        }

        ValidateAuthoritativeStateRequiredObjectPlayer(
            "pending card choice player",
            pendingCardChoice.PlayerId,
            seatPlayerIds,
            errors);
        var choiceLabel = string.IsNullOrWhiteSpace(pendingCardChoice.ChoiceId)
            ? "<unknown>"
            : pendingCardChoice.ChoiceId.Trim();
        RejectAuthoritativeStateRedactionSentinel(
            $"pending card choice {choiceLabel} player",
            pendingCardChoice.PlayerId,
            errors);
    }

    private static void ValidateAuthoritativeStateObjectReferences(
        MatchState authoritativeState,
        List<string> errors)
    {
        CoreRuleEngine.ValidateBattlefieldReplacements(authoritativeState, errors);
        foreach (var card in authoritativeState.CardObjects.Values)
            if (!CoreRuleEngine.ValidSpellNameDeclaration(authoritativeState, card)) errors.Add($"card object {card.ObjectId} spell name declaration is invalid");
        var knownObjectIds = BuildAuthoritativeStateKnownObjectIds(authoritativeState);
        foreach (var (objectId, cardObject) in authoritativeState.CardObjects
            .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (cardObject is null)
            {
                continue;
            }

            if (cardObject.TokenFactoryCardNo is { } tokenFactory)
            {
                if (!P6TokenFactoryCatalog.IsTokenFactory(tokenFactory))
                    errors.Add($"card object {objectId} has invalid token factory identity");
                if (authoritativeState.PlayerZones.Values.Any(zones => zones.Hand.Contains(objectId)
                    || zones.Graveyard.Contains(objectId) || zones.Banished.Contains(objectId)
                    || zones.MainDeck.Contains(objectId) || zones.RuneDeck.Contains(objectId)
                    || zones.ChampionZone.Contains(objectId)))
                    errors.Add($"card object {objectId} token cannot persist outside field or stack");
            }

            ValidateAuthoritativeStateOptionalObjectReferenceWithExpectedDetails(
                $"card object {objectId} attached object",
                cardObject.AttachedToObjectId,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateCardObjectPowerModifierObjectReferences(
                objectId,
                cardObject.UntilEndOfTurnPowerModifiers,
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

            ValidateAuthoritativeStateOptionalObjectReferenceWithExpectedDetails(
                $"object location {objectId} battlefield object",
                location.BattlefieldObjectId,
                knownObjectIds,
                errors);
        }

        ValidateAuthoritativeStateStackItemObjectReferences(
            authoritativeState.StackItems,
            knownObjectIds,
            errors, authoritativeState);

        ValidateAuthoritativeStateTriggerQueueSourceObjectReferences(
            authoritativeState.TriggerQueue,
            knownObjectIds,
            errors, authoritativeState);

        if (authoritativeState.PendingHandChoice is not null)
        {
            if (!(authoritativeState.PendingHandChoice.MovementObserver is not null && CoreRuleEngine.ValidMovementDiscardChoice(authoritativeState))
                && !CoreRuleEngine.ValidLastBreathDiscardChoice(authoritativeState)
                && !(authoritativeState.PendingHandChoice.LegendDiscardDraw is not null && CoreRuleEngine.ValidLegendDiscardDrawChoice(authoritativeState)))
                ValidateAuthoritativeStateOptionalObjectReferenceWithExpectedDetails(
                    $"pending hand choice {authoritativeState.PendingHandChoice.ChoiceId} source object",
                    authoritativeState.PendingHandChoice.SourceObjectId,
                    knownObjectIds,
                    errors);
            ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
                $"pending hand choice {authoritativeState.PendingHandChoice.ChoiceId} legal object",
                authoritativeState.PendingHandChoice.LegalObjectIds,
                knownObjectIds,
                errors);
        }

        if (authoritativeState.PendingEffectPlay is { } effectPlay)
        {
            ValidateAuthoritativeStateStackItemObjectReferences([effectPlay.Parent], knownObjectIds, errors, authoritativeState);
            ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
                $"pending effect play {effectPlay.ChoiceId} source", effectPlay.Sources.Keys.ToArray(), knownObjectIds, errors);
            if (!authoritativeState.Seats.ContainsKey(effectPlay.PlayerId)
                || effectPlay.SourceZone is not ("HAND" or "GRAVEYARD" or "BANISHED" or "MAIN_DECK" or "STACK")
                || effectPlay.Sources.Values.Any(generation => generation < 0))
                errors.Add("pending effect play has an invalid actor, source zone or generation");
            if (effectPlay.SourceZone == "MAIN_DECK") errors.Add("obsolete direct deck effect-play continuation");
            if (effectPlay.ViewedCardIds is not null) errors.Add("viewed deck cards outside deck effect play");
        }

        if (authoritativeState.PendingCardChoice is not null)
        {
            var choice = authoritativeState.PendingCardChoice;
            var parent = choice.DeckContext is not null ? CoreRuleEngine.DeckPlayChoiceParent(authoritativeState, choice)
                : authoritativeState.StackItems.LastOrDefault();
            var capturedDeckSource = choice.DeckContext is not null
                && CoreRuleEngine.ValidDeckPlayChoice(authoritativeState, choice)
                && parent!.SourceObjectId == choice.SourceObjectId
                && parent.FieldContext is { SourceCeasedToExist: true } field
                && CoreRuleEngine.ValidFieldContextState(authoritativeState, field, parent.SourceObjectId, parent.ControllerId, parent.EffectKind, parent.CardNo);
            var capturedInsightSource = parent is not null && choice.SourceObjectId == parent.SourceObjectId
                && CoreRuleEngine.ValidInsightChoice(authoritativeState, choice, parent)
                && CoreRuleEngine.ValidCeasedInsightSource(authoritativeState, parent.InsightContext,
                    parent.SourceObjectId, parent.EffectKind, parent.ControllerId, parent.CardNo);
            var capturedEmpoweredSource = authoritativeState.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId) is { FieldContext: { Kind: CoreRuleEngine.EmpoweredBanishKind, SourceCeasedToExist: true } empowered } captured
                && captured.SourceObjectId == choice.SourceObjectId && CoreRuleEngine.ValidTriggerChoice(authoritativeState, choice)
                && CoreRuleEngine.ValidFieldContextState(authoritativeState, empowered, captured.SourceObjectId, captured.ControllerId, captured.EffectKind, captured.CardNo);
            var capturedBombSource = authoritativeState.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId) is { FieldContext: { Kind: CoreRuleEngine.BombDestructionKind, SourceCeasedToExist: true } bomb } capturedBomb
                && capturedBomb.SourceObjectId == choice.SourceObjectId && CoreRuleEngine.ValidTriggerChoice(authoritativeState, choice)
                && CoreRuleEngine.ValidFieldContextState(authoritativeState, bomb, capturedBomb.SourceObjectId, capturedBomb.ControllerId, capturedBomb.EffectKind, capturedBomb.CardNo);
            if (!capturedDeckSource && !capturedInsightSource && !capturedEmpoweredSource && !capturedBombSource) ValidateAuthoritativeStateOptionalObjectReferenceWithExpectedDetails(
                $"pending card choice {authoritativeState.PendingCardChoice.ChoiceId} source object",
                authoritativeState.PendingCardChoice.SourceObjectId,
                knownObjectIds,
                errors);
            if (authoritativeState.PendingCardChoice.ChoiceWindow is not (CoreRuleEngine.ReadiedModeWindow or CoreRuleEngine.UnitEventModeWindow or CoreRuleEngine.AttachmentModeWindow or CoreRuleEngine.ResolutionDamageChoiceWindow or CoreRuleEngine.DravenDeathOpponentWindow or CoreRuleEngine.EmpowerOpponentWindow or CoreRuleEngine.ExhaustionModeWindow)
                && !(choice.ChoiceWindow == CoreRuleEngine.TemperedBaseCostWindow && CoreRuleEngine.ValidTemperedBaseCostChoice(authoritativeState, choice)))
                ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
                $"pending card choice {authoritativeState.PendingCardChoice.ChoiceId} legal object",
                authoritativeState.PendingCardChoice.LegalObjectIds,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
                $"pending card choice {authoritativeState.PendingCardChoice.ChoiceId} context object",
                authoritativeState.PendingCardChoice.ContextObjectIds,
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

            var historicalKnown = knownObjectIds.Concat(resolution.PublicObjectBindings?.Select(b => b.ObjectId) ?? [])
                .ToHashSet(StringComparer.Ordinal);

            ValidateAuthoritativeStateRequiredObjectReferenceWithExpectedDetails(
                $"battlefield resolution {resolution.ResolutionId} battlefield object",
                resolution.BattlefieldObjectId,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateOptionalObjectReferenceWithExpectedDetails(
                $"battlefield resolution {resolution.ResolutionId} source object",
                resolution.SourceObjectId,
                historicalKnown,
                errors);
            ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
                $"battlefield resolution {resolution.ResolutionId} participant object",
                resolution.ParticipantObjectIds,
                historicalKnown,
                errors);
        }

        foreach (var resolution in authoritativeState.BattleResolutions
            .OrderBy(item => item?.ResolutionId ?? string.Empty, StringComparer.Ordinal))
        {
            if (resolution is null)
            {
                continue;
            }

            var historicalKnown = knownObjectIds.Concat(resolution.PublicObjectBindings?.Select(b => b.ObjectId) ?? []).ToHashSet(StringComparer.Ordinal);

            ValidateAuthoritativeStateRequiredObjectReferenceWithExpectedDetails(
                $"battle resolution {resolution.ResolutionId} battlefield object",
                resolution.BattlefieldId,
                knownObjectIds,
                errors);
            ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
                $"battle resolution {resolution.ResolutionId} attacker object",
                resolution.AttackerObjectIds,
                historicalKnown,
                errors);
            ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
                $"battle resolution {resolution.ResolutionId} defender object",
                resolution.DefenderObjectIds,
                historicalKnown,
                errors);
            ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
                $"battle resolution {resolution.ResolutionId} surviving attacker object",
                resolution.SurvivingAttackerObjectIds,
                historicalKnown,
                errors);
            ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
                $"battle resolution {resolution.ResolutionId} surviving defender object",
                resolution.SurvivingDefenderObjectIds,
                historicalKnown,
                errors);
            ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
                $"battle resolution {resolution.ResolutionId} destroyed object",
                resolution.DestroyedObjectIds,
                historicalKnown,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateStackItemObjectReferences(
        IReadOnlyList<StackItemState>? stackItems,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors, MatchState authoritativeState)
    {
        if (stackItems is null)
        {
            return;
        }

        foreach (var stackItem in stackItems.OrderBy(item => item?.StackItemId ?? string.Empty, StringComparer.Ordinal))
        {
            if (stackItem is null)
            {
                continue;
            }

            // Captured triggered abilities survive their source object leaving.
            // Only validated official contexts permit a missing source.
            if (!HasValidatedCeasedBeginningRuneSource(authoritativeState, stackItem.StackItemId, stackItem.ControllerId,
                    stackItem.SourceObjectId, stackItem.EffectKind)
                && !(stackItem.EndAbility is not null && CoreRuleEngine.ValidEndAbility(authoritativeState, stackItem))
                && !CoreRuleEngine.ValidCeasedInsightSource(authoritativeState, stackItem.InsightContext,
                    stackItem.SourceObjectId, stackItem.EffectKind, stackItem.ControllerId, stackItem.CardNo)
                && !(stackItem.FieldContext is { SourceCeasedToExist: true } field && CoreRuleEngine.ValidFieldContextState(authoritativeState,
                    field, stackItem.SourceObjectId, stackItem.ControllerId, stackItem.EffectKind, stackItem.CardNo))
                && !(stackItem.ConquestFlow is not null && CoreRuleEngine.ValidConquestFlowStack(authoritativeState, stackItem))
                && !(stackItem.ConquestToken is { } conquestToken && CoreRuleEngine.ValidConquestToken(authoritativeState, conquestToken, stackItem.SourceObjectId, stackItem.EffectKind, stackItem.CardNo))
                && !(stackItem.NamedUnitSwap is not null && NamedUnitSwapRules.Valid(authoritativeState, stackItem))
                && !(stackItem.UnitConquest is { } conquest && CoreRuleEngine.ValidUnitConquest(conquest,stackItem.EffectKind,stackItem.CardNo))
                && !(stackItem.ConquestSource is not null && CoreRuleEngine.ValidConquestSourceStack(authoritativeState, stackItem))
                && !(stackItem.UnitConquestReturn is not null && ValidUnitConquestReturnStack(authoritativeState, stackItem))
                && !(stackItem.HandDiscardObserver is { } discard && CoreRuleEngine.ValidHandDiscardObserver(authoritativeState, discard, stackItem.EffectKind, stackItem.SourceObjectId, stackItem.ControllerId, stackItem.CardNo))
                && !(stackItem.MovementObserver is { } movement && CoreRuleEngine.ValidMovementObserver(authoritativeState,movement,stackItem.EffectKind,stackItem.ControllerId,stackItem.SourceObjectId,stackItem.CardNo))
                && !(stackItem.DelayedDestruction is { } delayed && CoreRuleEngine.ValidDelayedDestruction(authoritativeState, delayed, stackItem.SourceObjectId, stackItem.ControllerId))
                && !(stackItem.DeathObserver is { } death && CoreRuleEngine.ValidDeathObserver(authoritativeState,death,stackItem.EffectKind,stackItem.ControllerId,stackItem.SourceObjectId,stackItem.CardNo))
                && !BattleResultRecoveryRules.IsCapturedSkill(authoritativeState, stackItem)
                && !DravenBattleDeathRecoveryRules.IsCapturedSkill(authoritativeState, stackItem)
                && !(stackItem.UnitEvent is not null && CoreRuleEngine.ValidUnitEvent(authoritativeState, stackItem.UnitEvent, stackItem.EffectKind, stackItem.SourceObjectId, stackItem.CardNo))
                && !(stackItem.EmpowerInteraction is not null && CoreRuleEngine.ValidEmpowerInteraction(authoritativeState, stackItem.EmpowerInteraction, stackItem.EffectKind, stackItem.SourceObjectId, stackItem.ControllerId, stackItem.CardNo))
                && !(stackItem.SourceReadied is not null && CoreRuleEngine.ValidSourceReadiedStack(authoritativeState, stackItem))
                && !(stackItem.ReadyObjectTrigger is not null && CoreRuleEngine.ValidReadyOtherObjectStack(authoritativeState, stackItem))
                && !(stackItem.SourceUnitTokens is not null && CoreRuleEngine.ValidSourceUnitTokens(authoritativeState, stackItem.SourceUnitTokens, stackItem.EffectKind, stackItem.SourceObjectId, stackItem.ControllerId, stackItem.CardNo))
                && !(stackItem.SourcePlayDraw is not null && CoreRuleEngine.ValidSourcePlayDraw(authoritativeState, stackItem.SourcePlayDraw, stackItem.EffectKind, stackItem.SourceObjectId, stackItem.CardNo))
                && !(stackItem.UnitEntryContext is { } entry && CoreRuleEngine.ValidUnitEntryTrigger(entry,
                    stackItem.EffectKind, stackItem.ControllerId, stackItem.CardNo))
                && !(stackItem.ExhaustReadyActivation is not null
                    && CoreRuleEngine.ValidExhaustReadyActivation(authoritativeState, stackItem))
                && !UnitDestroyedTriggerSpecRules.TryGetTrigger(stackItem.CardNo,
                    trigger => trigger.Timing == TriggerTimings.UnitDestroyed && trigger.Kind == stackItem.EffectKind, out _))
            {
                ValidateAuthoritativeStateOptionalObjectReferenceWithExpectedDetails(
                    $"stack item {stackItem.StackItemId} source object",
                    stackItem.SourceObjectId,
                    knownObjectIds,
                    errors);
            }
            var targetReferences = knownObjectIds.ToHashSet(StringComparer.Ordinal);
            if (CoreRuleEngine.SimpleActivationTargetsPlayer(authoritativeState, stackItem))
                foreach (var player in authoritativeState.Seats.Keys) targetReferences.Add(player);
            if (stackItem.SimpleActivation?.TargetTokenFactories is { } tokenTargets && CoreRuleEngine.ValidSimpleActivation(authoritativeState, stackItem))
                foreach (var id in tokenTargets.Keys) targetReferences.Add(id);
            if (CoreRuleEngine.ValidDepartedTokenTargets(authoritativeState, stackItem))
                foreach (var token in stackItem.DepartedTokenTargets ?? []) targetReferences.Add(token.Target.ObjectId);
            if (stackItem.UnitPlayMight is { TargetTokenFactoryCardNo: not null }
                && RengarPlayRecoveryRules.ValidStack(authoritativeState, stackItem))
                foreach (var id in stackItem.TargetObjectIds) targetReferences.Add(id);
            if (stackItem.MovementObserver is { Kind: TriggerKinds.UnitMovedDamageAtOriginOrDestination, TargetTokenFactoryCardNo: not null }
                && CoreRuleEngine.ValidMovementDamageStack(authoritativeState, stackItem))
                foreach (var id in stackItem.TargetObjectIds) targetReferences.Add(id);
            if (stackItem.DestroyDeckPlay is { } destroyDeck && CoreRuleEngine.ValidHistoricalDestroyDeckTarget(authoritativeState, stackItem, destroyDeck.Target.ObjectId))
                targetReferences.Add(destroyDeck.Target.ObjectId);
            if (stackItem.CreatedTokenReady is { } ready && CoreRuleEngine.ValidCreatedTokenReadyStack(authoritativeState, stackItem))
                foreach (var unit in ready.Units)
                    targetReferences.Add(unit.ObjectId);
            if (stackItem.LegendEquipmentReady is { TargetTokenFactories: { } equipmentTokens }
                && CoreRuleEngine.ValidLegendEquipmentReady(authoritativeState, stackItem))
                foreach (var id in equipmentTokens.Keys) targetReferences.Add(id);
            if (stackItem.BattlefieldConquestReadyEquipment is { TargetTokenFactoryCardNo: not null }
                && ValidBattlefieldConquestStack(authoritativeState, stackItem))
                foreach (var id in stackItem.TargetObjectIds) targetReferences.Add(id);
            if (CardBehaviorRegistry.TryGetByEffectKind(stackItem.EffectKind, out var behavior)
                && behavior.TargetScope is CardTargetScopes.StackSpell or CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell or CardTargetScopes.FriendlyUnitThenStackSpell)
            {
                foreach (var live in stackItems) targetReferences.Add(live.StackItemId);
                foreach (var binding in stackItem.TargetStackSources ?? new Dictionary<string,string>())
                {
                    var live = stackItems.FirstOrDefault(item => item.StackItemId == binding.Key);
                    if (!knownObjectIds.Contains(binding.Value) || live is not null && live.SourceObjectId != binding.Value
                        || !(stackItem.RepeatExecutions?.SelectMany(e => e.TargetObjectIds) ?? stackItem.TargetObjectIds).Contains(binding.Key))
                        errors.Add("invalid captured stack target identity");
                    else targetReferences.Add(binding.Key);
                }
            }
            else if (stackItem.TargetStackSources is { Count: > 0 })
                errors.Add("stack target identity captured for a non-stack target scope");
            ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
                $"stack item {stackItem.StackItemId} target object",
                stackItem.TargetObjectIds,
                targetReferences,
                errors);
            if (stackItem.RepeatExecutions is { } executions)
                foreach (var execution in executions)
                    ValidateAuthoritativeStateObjectReferenceListWithExpectedDetails(
                        $"stack item {stackItem.StackItemId} repeat target object", execution.TargetObjectIds, targetReferences, errors);
        }
    }

    private static void ValidateAuthoritativeStateCardObjectPowerModifierObjectReferences(
        string objectId,
        IReadOnlyList<PowerModifierLedgerEntry>? powerModifiers,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors)
    {
        if (powerModifiers is null)
        {
            return;
        }

        foreach (var modifier in powerModifiers.OrderBy(
            entry => entry?.EffectId ?? string.Empty,
            StringComparer.Ordinal))
        {
            if (modifier is null
                || string.IsNullOrWhiteSpace(modifier.SourceObjectId))
            {
                continue;
            }

            var sourceObjectId = modifier.SourceObjectId.Trim();
            if (string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal))
            {
                continue;
            }

            var modifierLabel = string.IsNullOrWhiteSpace(modifier.EffectId)
                ? "<unknown>"
                : modifier.EffectId.Trim();
            ValidateAuthoritativeStateOptionalObjectReferenceWithExpectedDetails(
                $"card object {objectId} power modifier {modifierLabel} source object",
                modifier.SourceObjectId,
                knownObjectIds,
                errors);
        }
    }

    private static void ValidateAuthoritativeStateTriggerQueueSourceObjectReferences(
        IReadOnlyList<TriggerQueueItemState>? triggerQueue,
        IReadOnlySet<string> knownObjectIds,
        List<string> errors, MatchState authoritativeState)
    {
        if (triggerQueue is null)
        {
            return;
        }

        foreach (var trigger in triggerQueue.OrderBy(item => item?.TriggerId ?? string.Empty, StringComparer.Ordinal))
        {
            if (trigger is null
                || string.IsNullOrWhiteSpace(trigger.SourceObjectId))
            {
                continue;
            }

            var sourceObjectId = trigger.SourceObjectId.Trim();
            if (string.Equals(sourceObjectId, "HIDDEN", StringComparison.Ordinal)
                || (knownObjectIds.Count == 0
                    && !string.Equals(sourceObjectId, trigger.SourceObjectId, StringComparison.Ordinal)))
            {
                continue;
            }

            if (!HasValidatedCeasedBeginningRuneSource(authoritativeState, trigger.TriggerId, trigger.ControllerId,
                    trigger.SourceObjectId, trigger.EffectKind)
                && !(trigger.EndAbility is not null && CoreRuleEngine.ValidEndTrigger(authoritativeState, trigger))
                && !CoreRuleEngine.ValidCeasedInsightSource(authoritativeState, trigger.InsightContext,
                    trigger.SourceObjectId, trigger.EffectKind, trigger.ControllerId)
                && !(trigger.FieldContext is { SourceCeasedToExist: true } field && CoreRuleEngine.ValidFieldContextState(authoritativeState,
                    field, trigger.SourceObjectId, trigger.ControllerId, trigger.EffectKind))
                && !(trigger.ReflexiveDuel is { } duel && CoreRuleEngine.ValidReflexiveDuel(duel,trigger.EffectKind))
                && !(trigger.ConquestFlow is not null && CoreRuleEngine.ValidConquestFlow(authoritativeState, trigger.ConquestFlow, trigger.EffectKind, trigger.SourceObjectId))
                && !(trigger.ConquestToken is { } conquestToken && CoreRuleEngine.ValidConquestToken(authoritativeState, conquestToken, trigger.SourceObjectId, trigger.EffectKind))
                && !(trigger.UnitConquest is { } conquest && CoreRuleEngine.ValidUnitConquest(conquest,trigger.EffectKind))
                && !(trigger.ConquestSource is not null && CoreRuleEngine.ValidConquestSourceTrigger(authoritativeState, trigger))
                && !(trigger.UnitConquestReturn is not null && ValidUnitConquestReturnQueue(authoritativeState, trigger))
                && !(trigger.HandDiscardObserver is { } discard && CoreRuleEngine.ValidHandDiscardObserver(authoritativeState, discard, trigger.EffectKind, trigger.SourceObjectId, trigger.ControllerId))
                && !(trigger.MovementObserver is { } movement && CoreRuleEngine.ValidMovementObserver(authoritativeState,movement,trigger.EffectKind,trigger.ControllerId,trigger.SourceObjectId))
                && !(trigger.DelayedDestruction is { } delayed && CoreRuleEngine.ValidDelayedDestruction(authoritativeState, delayed, trigger.SourceObjectId, trigger.ControllerId))
                && !(trigger.DeathObserver is { } death && CoreRuleEngine.ValidDeathObserver(authoritativeState,death,trigger.EffectKind,trigger.ControllerId,trigger.SourceObjectId))
                && !BattleResultRecoveryRules.IsCapturedSkill(authoritativeState, trigger)
                && !DravenBattleDeathRecoveryRules.IsCapturedSkill(authoritativeState, trigger)
                && !(trigger.UnitEvent is not null && CoreRuleEngine.ValidUnitEvent(authoritativeState, trigger.UnitEvent, trigger.EffectKind, trigger.SourceObjectId))
                && !(trigger.EmpowerInteraction is not null && CoreRuleEngine.ValidEmpowerInteraction(authoritativeState, trigger.EmpowerInteraction, trigger.EffectKind, trigger.SourceObjectId, trigger.ControllerId))
                && !(trigger.SourceReadied is not null && CoreRuleEngine.ValidSourceReadied(authoritativeState, trigger.SourceReadied, trigger.EffectKind, trigger.SourceObjectId, trigger.ControllerId, trigger.SourceCardNo))
                && !(trigger.ReadyObjectTrigger is not null && CoreRuleEngine.ValidReadyObjectTrigger(authoritativeState, trigger.ReadyObjectTrigger, trigger.EffectKind, trigger.SourceObjectId, trigger.ControllerId, trigger.SourceCardNo))
                && !(trigger.SourceUnitTokens is not null && CoreRuleEngine.ValidSourceUnitTokens(authoritativeState, trigger.SourceUnitTokens, trigger.EffectKind, trigger.SourceObjectId, trigger.ControllerId))
                && !(trigger.SourcePlayDraw is not null && CoreRuleEngine.ValidSourcePlayDraw(authoritativeState, trigger.SourcePlayDraw, trigger.EffectKind, trigger.SourceObjectId))
                && !(trigger.UnitEntryContext is { } entry && CoreRuleEngine.ValidUnitEntryTrigger(entry, trigger.EffectKind, trigger.ControllerId))
                && !(trigger.TriggeredByEventKind == "UNIT_DESTROYED"
                && UnitDestroyedTriggerSpecRules.TryGetTrigger(trigger.SourceCardNo,
                    spec => spec.Timing == TriggerTimings.UnitDestroyed && spec.Kind == trigger.EffectKind, out _)))
            {
                ValidateAuthoritativeStateOptionalObjectReferenceWithExpectedDetails(
                    $"trigger queue item {trigger.TriggerId} source object",
                    trigger.SourceObjectId,
                    knownObjectIds,
                    errors);
            }
        }
    }

    private static IReadOnlySet<string> BuildAuthoritativeStateKnownObjectIds(MatchState authoritativeState)
    {
        var knownObjectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var objectId in authoritativeState.CardObjects.Keys)
        {
            AddKnownObjectId(knownObjectIds, objectId);
        }

        foreach (var objectId in authoritativeState.ObjectLocations.Keys)
        {
            AddKnownObjectId(knownObjectIds, objectId);
        }

        return knownObjectIds;
    }

    private static void ValidateSpectatorSnapshotStackPlayerReferences(
        IReadOnlyList<object?> stackItems,
        IReadOnlySet<string> knownPlayerIds,
        List<string> errors)
    {
        for (var index = 0; index < stackItems.Count; index++)
        {
            var stackItem = stackItems[index];
            if (!IsSnapshotPlayerPayloadObject(stackItem))
            {
                continue;
            }

            var stackItemLabel = TryReadObjectString(stackItem, "stackItemId", out var stackItemId)
                && !string.IsNullOrWhiteSpace(stackItemId)
                ? stackItemId.Trim()
                : $"#{index + 1}";
            if (!TryReadObjectString(stackItem, "controllerId", out var controllerId)
                || string.IsNullOrWhiteSpace(controllerId))
            {
                continue;
            }

            ValidateTimingPlayerReference(
                $"spectator replay frame snapshot stack item {stackItemLabel} controller player",
                controllerId,
                knownPlayerIds,
                "seats",
                errors);
        }
    }

    private static void ValidateSpectatorSnapshotStackObjectReferences(
        IReadOnlyList<object?> stackItems,
        IReadOnlySet<string> knownObjectIds,
        MatchState authoritativeState,
        List<string> errors)
    {
        for (var index = 0; index < stackItems.Count; index++)
        {
            var stackItem = stackItems[index];
            if (!IsSnapshotPlayerPayloadObject(stackItem))
            {
                continue;
            }

            var stackItemLabel = TryReadObjectString(stackItem, "stackItemId", out var stackItemId)
                && !string.IsNullOrWhiteSpace(stackItemId)
                ? stackItemId.Trim()
                : $"#{index + 1}";
            var sourceReferences = knownObjectIds;
            if (HasValidatedCeasedLastBreathSource(authoritativeState, stackItemId,
                ReadNormalizedPayloadString(stackItem, "controllerId"), ReadNormalizedPayloadString(stackItem, "sourceObjectId"),
                ReadNormalizedPayloadString(stackItem, "effectKind")))
                sourceReferences = knownObjectIds.Append(ReadNormalizedPayloadString(stackItem, "sourceObjectId")!).ToHashSet(StringComparer.Ordinal);
            if (HasValidatedCeasedBeginningRuneSource(authoritativeState, stackItemId,
                ReadNormalizedPayloadString(stackItem, "controllerId"), ReadNormalizedPayloadString(stackItem, "sourceObjectId"),
                ReadNormalizedPayloadString(stackItem, "effectKind")))
                sourceReferences = knownObjectIds.Append(ReadNormalizedPayloadString(stackItem, "sourceObjectId")!).ToHashSet(StringComparer.Ordinal);
            if (authoritativeState.StackItems.FirstOrDefault(i => i.StackItemId == stackItemId) is { } battleSkill
                && CapturedBattleSourceIsPrivate(authoritativeState, battleSkill))
                sourceReferences = knownObjectIds.Append("HIDDEN").ToHashSet(StringComparer.Ordinal);
            if (authoritativeState.StackItems.FirstOrDefault(i => i.StackItemId == stackItemId) is { UnitConquestReturn: not null } conquestReturn
                && ValidUnitConquestReturnStack(authoritativeState, conquestReturn)
                && !CapturedBattleSourceIsPrivate(authoritativeState, conquestReturn))
                sourceReferences = knownObjectIds.Append(conquestReturn.SourceObjectId).ToHashSet(StringComparer.Ordinal);
            if (authoritativeState.StackItems.FirstOrDefault(i => i.StackItemId == stackItemId) is { FieldContext: { SourceCeasedToExist: true } field } ceased
                && CoreRuleEngine.ValidFieldContextState(authoritativeState, field, ceased.SourceObjectId, ceased.ControllerId, ceased.EffectKind, ceased.CardNo))
                sourceReferences = knownObjectIds.Append(ceased.SourceObjectId).ToHashSet(StringComparer.Ordinal);
            if (authoritativeState.StackItems.FirstOrDefault(i => i.StackItemId == stackItemId) is { LastBreathMight: { } captured } earned
                && CoreRuleEngine.ValidLastBreathMightContext(authoritativeState, captured, earned.EffectKind, earned.SourceObjectId, earned.ControllerId)
                && MatchStateHasher.HashValue(ReadLastBreathMightView(stackItem)) == MatchStateHasher.HashValue(captured))
                sourceReferences = knownObjectIds.Append(earned.SourceObjectId).ToHashSet(StringComparer.Ordinal);
            if (authoritativeState.StackItems.FirstOrDefault(i => i.StackItemId == stackItemId) is { } insight
                && CoreRuleEngine.ValidCeasedInsightSource(authoritativeState, insight.InsightContext,
                    insight.SourceObjectId, insight.EffectKind, insight.ControllerId, insight.CardNo)
                && MatchStateHasher.HashValue(ReadCapturedInsightView(stackItem)) == MatchStateHasher.HashValue(insight.InsightContext))
                sourceReferences = knownObjectIds.Append(insight.SourceObjectId).ToHashSet(StringComparer.Ordinal);
            if (authoritativeState.StackItems.FirstOrDefault(i => i.StackItemId == stackItemId) is { } exhaustReady
                && CoreRuleEngine.ValidExhaustReadyActivation(authoritativeState, exhaustReady))
            {
                var visibleSource = CapturedBattleSourceIsPrivate(authoritativeState, exhaustReady)
                    ? "HIDDEN"
                    : exhaustReady.SourceObjectId;
                sourceReferences = knownObjectIds.Append(visibleSource).ToHashSet(StringComparer.Ordinal);
            }
            ValidateTimingOptionalObjectReferenceWithExpectedDetails(
                stackItem,
                "sourceObjectId",
                $"spectator replay frame snapshot stack item {stackItemLabel} source object id",
                sourceReferences,
                "object registry",
                errors);
            var targetReferences = knownObjectIds.ToHashSet(StringComparer.Ordinal);
            if (authoritativeState.StackItems.FirstOrDefault(i => i.StackItemId == stackItemId) is { } original)
            {
                if (CoreRuleEngine.SimpleActivationTargetsPlayer(authoritativeState, original))
                    foreach (var player in authoritativeState.Seats.Keys) targetReferences.Add(player);
                if (original.SimpleActivation?.TargetTokenFactories is { } tokenTargets && CoreRuleEngine.ValidSimpleActivation(authoritativeState, original))
                    foreach (var id in tokenTargets.Keys) targetReferences.Add(id);
                if (CoreRuleEngine.ValidDepartedTokenTargets(authoritativeState, original))
                    foreach (var token in original.DepartedTokenTargets ?? []) targetReferences.Add(token.Target.ObjectId);
                if (original.UnitPlayMight is { TargetTokenFactoryCardNo: not null }
                    && RengarPlayRecoveryRules.ValidStack(authoritativeState, original))
                    foreach (var id in original.TargetObjectIds) targetReferences.Add(id);
                if (original.MovementObserver is { Kind: TriggerKinds.UnitMovedDamageAtOriginOrDestination, TargetTokenFactoryCardNo: not null }
                    && CoreRuleEngine.ValidMovementDamageStack(authoritativeState, original))
                    foreach (var id in original.TargetObjectIds) targetReferences.Add(id);
                if (original.DestroyDeckPlay is { } destroyDeck && CoreRuleEngine.ValidHistoricalDestroyDeckTarget(authoritativeState, original, destroyDeck.Target.ObjectId))
                    targetReferences.Add(destroyDeck.Target.ObjectId);
                if (original.CreatedTokenReady is { } ready && CoreRuleEngine.ValidCreatedTokenReadyStack(authoritativeState, original))
                    foreach (var unit in ready.Units)
                        targetReferences.Add(unit.ObjectId);
                if (original.LegendEquipmentReady is { TargetTokenFactories: { } equipmentTokens }
                    && CoreRuleEngine.ValidLegendEquipmentReady(authoritativeState, original))
                    foreach (var id in equipmentTokens.Keys) targetReferences.Add(id);
                if (original.BattlefieldConquestReadyEquipment is { TargetTokenFactoryCardNo: not null }
                    && ValidBattlefieldConquestStack(authoritativeState, original))
                    foreach (var id in original.TargetObjectIds) targetReferences.Add(id);
                if (VisibleObjectIdsForSpectator(authoritativeState, original.TargetObjectIds).Contains("HIDDEN"))
                    targetReferences.Add("HIDDEN");
                if (CardBehaviorRegistry.TryGetByEffectKind(original.EffectKind, out var behavior)
                    && behavior.TargetScope is CardTargetScopes.StackSpell or CardTargetScopes.FriendlyBattlefieldUnitThenStackSpell or CardTargetScopes.FriendlyUnitThenStackSpell)
                    foreach (var targetId in original.TargetObjectIds)
                        if (authoritativeState.StackItems.Any(item => item.StackItemId == targetId)
                            || original.TargetStackSources?.ContainsKey(targetId) == true)
                            targetReferences.Add(targetId);
            }
            ValidateTimingObjectReferenceListWithExpectedDetails(
                stackItem,
                "targetObjectIds",
                $"spectator replay frame snapshot stack item {stackItemLabel} target object id",
                // A chosen target can leave the public field before its skill resolves.
                // The keyed authoritative comparison below still requires exact redaction.
                targetReferences,
                "object registry",
                errors);
        }
    }

    private static void ValidateSpectatorSnapshotStackKeyedAuthoritativeValues(
        IReadOnlyList<object?> stackItems,
        MatchState authoritativeState,
        List<string> errors)
    {
        ValidateHandDiscardObserverStackViews(stackItems, authoritativeState, errors, "spectator replay frame");
        ValidateLastBreathMightStackViews(stackItems, authoritativeState, errors, "spectator replay frame");
        ValidateCapturedInsightStackViews(stackItems, authoritativeState, errors, "spectator replay frame");
        ValidateDelayedDestructionStackViews(stackItems, authoritativeState, errors);
        ValidateTemporaryActivationStackViews(stackItems, authoritativeState, errors);
        ValidateDelayedEmpowerViews(stackItems, errors);
        ValidateBombRebuildViews(stackItems, errors);
        ValidateLastBreathUnitCreationViews(stackItems, errors);
        ValidateLastBreathRuneCallViews(stackItems, errors);
        ValidateLastBreathDrawViews(stackItems, errors);
        ValidateLastBreathBattlefieldDamageViews(stackItems, errors);
        ValidateDestructionFamilyStackViews(stackItems, authoritativeState, errors);
        ValidateSourceReplayStackViews(stackItems, authoritativeState, errors);
        ValidateResolutionDamageChoiceViews(stackItems, authoritativeState, errors);
        ValidateDeckPlayPendingCardViews(stackItems, authoritativeState, errors);
        ValidateFlowPlayViews(stackItems, authoritativeState, errors);
        ValidateBattlefieldConquestMightStackViews(stackItems, authoritativeState, errors, "spectator replay frame");
        ValidateBattlefieldConquestMillStackViews(stackItems, authoritativeState, errors, "spectator replay frame");
        ValidateBattlefieldConquestChoiceStackViews(stackItems, authoritativeState, "spectator replay frame", errors);
        ValidateMightySkillStackViews(stackItems, authoritativeState, errors, "spectator replay frame");
        ValidateRepeatDestinationViews(stackItems, authoritativeState, errors, "spectator replay frame");
        ValidateLocationDamageViews(stackItems, authoritativeState, errors, "spectator replay frame");
        var authoritativeStackItemsById = new Dictionary<string, StackItemState>(StringComparer.Ordinal);
        foreach (var authoritativeStackItem in authoritativeState.StackItems)
        {
            if (string.IsNullOrWhiteSpace(authoritativeStackItem.StackItemId))
            {
                continue;
            }

            var normalizedStackItemId = authoritativeStackItem.StackItemId.Trim();
            if (!authoritativeStackItemsById.ContainsKey(normalizedStackItemId))
            {
                authoritativeStackItemsById.Add(normalizedStackItemId, authoritativeStackItem);
            }
        }

        foreach (var stackItem in stackItems)
        {
            if (!IsSnapshotPlayerPayloadObject(stackItem)
                || !TryReadObjectString(stackItem, "stackItemId", out var stackItemId)
                || string.IsNullOrWhiteSpace(stackItemId))
            {
                continue;
            }

            var normalizedStackItemId = stackItemId.Trim();
            if (!authoritativeStackItemsById.TryGetValue(normalizedStackItemId, out var authoritativeStackItem))
            {
                continue;
            }

            ValidateSpectatorSnapshotStackKeyedRequiredStringValue(
                stackItem,
                "controllerId",
                "controller id",
                authoritativeStackItem.ControllerId,
                normalizedStackItemId,
                errors);
            ValidateSpectatorSnapshotStackKeyedOptionalStringValue(
                stackItem,
                "sourceObjectId",
                "source object id",
                CapturedBattleSourceIsPrivate(authoritativeState, authoritativeStackItem) ? "HIDDEN" : authoritativeStackItem.SourceObjectId,
                normalizedStackItemId,
                errors);
            ValidateSpectatorSnapshotStackKeyedRequiredStringValue(
                stackItem,
                "effectKind",
                "effect kind",
                authoritativeStackItem.EffectKind,
                normalizedStackItemId,
                errors);
            ValidateSpectatorSnapshotStackKeyedOptionalStringValue(
                stackItem,
                "cardNo",
                "card no",
                authoritativeStackItem.CardNo,
                normalizedStackItemId,
                errors);
            ValidateSpectatorSnapshotStackKeyedRequiredStringListValue(
                stackItem,
                "targetObjectIds",
                "target object ids",
                VisibleObjectIdsForSpectator(authoritativeState, authoritativeStackItem.TargetObjectIds),
                normalizedStackItemId,
                errors);
            ValidateSpectatorSnapshotStackKeyedRequiredIntValue(
                stackItem,
                "damageAmount",
                "damage amount",
                authoritativeStackItem.DamageAmount,
                normalizedStackItemId,
                errors);
            ValidateSpectatorSnapshotStackKeyedOptionalStringValue(
                stackItem,
                "destination",
                "destination",
                authoritativeStackItem.Destination,
                normalizedStackItemId,
                errors);
        }
    }

    private static string? ValidateStackItemPayloadValues(
        object? stackItem,
        string payloadLabel,
        List<string> errors)
    {
        ValidateUnitConquestReturnPrivateContextAbsent(stackItem, payloadLabel, errors);
        var stackItemId = ValidateSnapshotPayloadRequiredStringValue(
            stackItem,
            "stackItemId",
            payloadLabel,
            "stack item id",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            stackItem,
            "controllerId",
            payloadLabel,
            "controller id",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            stackItem,
            "sourceObjectId",
            payloadLabel,
            "source object id",
            errors);
        ValidateSnapshotPayloadRequiredStringValue(
            stackItem,
            "effectKind",
            payloadLabel,
            "effect kind",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            stackItem,
            "cardNo",
            payloadLabel,
            "card no",
            errors);
        ValidateSnapshotPayloadRequiredStringListValues(
            stackItem,
            "targetObjectIds",
            payloadLabel,
            "target object id",
            errors,
            allowRepeatedHiddenTargets: true,
            allowRepeatedValues: TryReadObjectString(stackItem, "cardNo", out var targetCardNo)
                && targetCardNo is not null && CardBehaviorRegistry.TryGetByCardNo(targetCardNo, out var targetBehavior) && targetBehavior.AllowsRepeatedTargets);
        // Keyed state comparison below still requires exact target order and multiplicity.
        ValidateSnapshotPayloadRequiredNonNegativeIntValue(
            stackItem,
            "damageAmount",
            payloadLabel,
            "damage amount",
            errors);
        ValidateSnapshotPayloadOptionalStringValue(
            stackItem,
            "destination",
            payloadLabel,
            "destination",
            errors);
        return stackItemId;
    }

    private static IReadOnlyList<string> VisibleObjectIdsForSpectator(
        MatchState state,
        IEnumerable<string> objectIds)
    {
        return objectIds
            .Select(objectId => IsHiddenObjectIdForSpectator(state, objectId) ? "HIDDEN" : objectId)
            .ToArray();
    }

    private static bool CapturedBattleSourceIsPrivate(MatchState state, StackItemState item)
        => (item.HeldContext?.Kind == CoreRuleEngine.DeploymentHeldKind || item.EndAbility is not null || item.InsightContext is { Kind: "EXHAUSTED" } || item.UnitConquestReturn is not null || BattleResultRecoveryRules.IsCapturedSkill(state, item) || DravenBattleDeathRecoveryRules.IsCapturedSkill(state, item) || item.ConquestToken is not null || item.SourceUnitTokens is not null || item.NamedUnitSwap is not null && NamedUnitSwapRules.Valid(state, item) || item.ReadyObjectTrigger is not null || item.SourceReadied is not null || item.LastBreathUnitCreation is not null || item.LastBreathRuneCall is not null || item.LastBreathDraw is not null || item.LastBreathBattlefieldDamage is not null || item.DelayedEmpower is not null || item.EmpowerOtherObserver is not null || item.OutsideHandPlayObserver is not null || item.HighCostPlayObserver is not null || item.SourceBoonMight is not null || item.ConquestSource is not null || item.TemporaryActivation is not null || item.SelfMovement is not null || item.EmpowerActivation is not null || item.RecycleMightActivation is not null && RecycleMightRules.Valid(state, item) || item.SelfMightActivation is not null && SelfMightRules.Valid(state, item) || item.MightySkill is not null || item.HandDiscardObserver is not null || item.DeathObserver is not null || item.MovementObserver is not null || item.FieldContext?.Kind is CoreRuleEngine.BombDestructionKind or CoreRuleEngine.EmpoweredBanishKind or CoreRuleEngine.AttackAreaDamageKind or CoreRuleEngine.AttackMightDamageKind or CoreRuleEngine.CombatMightDamageKind or CoreRuleEngine.SelfActionPowerKind or CoreRuleEngine.AttackWeaponDamageKind or CoreRuleEngine.AttackRallyKind or CoreRuleEngine.DisarmKind or CoreRuleEngine.PaidUnitStunKind or CoreRuleEngine.PaidUnitDestroyKind or CoreRuleEngine.StunReadyPowerKind or CoreRuleEngine.StunBoonKind or CoreRuleEngine.EquipmentDeathReturnKind or CoreRuleEngine.ExhaustionDrawKind or CoreRuleEngine.OtherExhaustionPowerKind or CoreRuleEngine.SourceExhaustionModeKind
                || item.ExhaustReadyActivation is not null
                || item.SourceConfirmed && item.PermanentPlaySource is null && !CoreRuleEngine.HasHistoricalPublicDamageSource(state, item))
            && IsHiddenObjectIdForSpectator(state, item.SourceObjectId);

    private static bool CapturedBattleSourceIsPrivate(MatchState state, TriggerQueueItemState item)
        => (item.HeldContext?.Kind == CoreRuleEngine.DeploymentHeldKind || item.EndAbility is not null || item.UnitConquestReturn is not null || item.ConquestToken is not null || item.SourceUnitTokens is not null || item.ReadyObjectTrigger is not null || item.SourceReadied is not null || item.LastBreathUnitCreation is not null || item.LastBreathRuneCall is not null || item.LastBreathDraw is not null || item.LastBreathBattlefieldDamage is not null || item.DelayedEmpower is not null || item.EmpowerOtherObserver is not null || item.OutsideHandPlayObserver is not null || item.HighCostPlayObserver is not null || item.ConquestSource is not null || item.DeathObserver is not null || item.MovementObserver is not null || item.HandDiscardObserver is not null || item.FieldContext?.Kind is CoreRuleEngine.BombDestructionKind or CoreRuleEngine.EmpoweredBanishKind or CoreRuleEngine.AttackAreaDamageKind or CoreRuleEngine.AttackMightDamageKind or CoreRuleEngine.CombatMightDamageKind or CoreRuleEngine.SelfActionPowerKind or CoreRuleEngine.EmpoweredAttackDestroyKind or CoreRuleEngine.AttackWeaponDamageKind or CoreRuleEngine.AttackRallyKind or CoreRuleEngine.DisarmKind or CoreRuleEngine.PaidUnitStunKind or CoreRuleEngine.PaidUnitDestroyKind or CoreRuleEngine.StunReadyPowerKind or CoreRuleEngine.StunBoonKind or CoreRuleEngine.EquipmentDeathReturnKind or CoreRuleEngine.ExhaustionDrawKind or CoreRuleEngine.OtherExhaustionPowerKind or CoreRuleEngine.SourceExhaustionModeKind || BattleResultRecoveryRules.IsCapturedSkill(state, item) || DravenBattleDeathRecoveryRules.IsCapturedSkill(state, item))
            && IsHiddenObjectIdForSpectator(state, item.SourceObjectId);

    private static bool IsHiddenObjectIdForSpectator(MatchState state, string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return false;
        }

        if (CoreRuleEngine.IsPubliclyRevealedHandCard(state, objectId) || CoreRuleEngine.IsPubliclyRevealedDeckCard(state, objectId)) return false;

        foreach (var zones in state.PlayerZones.Values)
        {
            if (zones.Hand.Contains(objectId, StringComparer.Ordinal)
                || zones.MainDeck.Contains(objectId, StringComparer.Ordinal)
                || zones.RuneDeck.Contains(objectId, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return state.CardObjects.TryGetValue(objectId, out var cardObject)
            && cardObject.IsFaceDown
            && cardObject.Tags.Contains(CardObjectTags.Standby, StringComparer.Ordinal)
            && state.ObjectLocations.TryGetValue(objectId, out var location)
            && string.Equals(location.Zone, "BATTLEFIELD", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(location.BattlefieldObjectId);
    }

    private static bool IsHiddenTriggerSourceForSpectator(
        MatchState state,
        TriggerQueueItemState trigger)
    {
        if (trigger.FieldContext?.SourceCeasedToExist == true
            && !state.CardObjects.ContainsKey(trigger.SourceObjectId)) return true;

        return trigger.UnitConquestReturn is null
            && trigger.DamageDestruction is null
            && trigger.GraveyardPlay is null
            && trigger.DelayedDestruction is null
            && trigger.HandDiscardObserver is null
            && trigger.DeathObserver is null
            && IsHiddenBattlefieldStandbyForSpectator(state, trigger.SourceObjectId);
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
}
