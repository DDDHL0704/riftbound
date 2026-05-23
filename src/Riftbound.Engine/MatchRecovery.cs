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

        if (errors.Count > 0)
        {
            return errors;
        }

        var replay = await VerifyFinalStateAsync(
                recovery.ReplayInitialState!,
                recovery.Commands,
                recovery.AuthoritativeState!,
                ruleEngine,
                cancellationToken)
            .ConfigureAwait(false);
        return replay.IsMatch
            ? []
            : replay.Errors
                .Select(error => $"action-log replay audit failed: {error}")
                .ToArray();
    }

    public static async ValueTask<MatchActionLogReplayResult> VerifyFinalStateAsync(
        MatchState initialState,
        IReadOnlyList<RecoveredCommand> commands,
        MatchState expectedFinalState,
        IRuleEngine ruleEngine,
        CancellationToken cancellationToken)
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

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonicalValue(writer, JsonSerializer.SerializeToElement(state));
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

        ValidateEvents(lastEventSequence, events, errors);
        ValidateCommands(lastEventSequence, commands, events, errors);
        ValidatePlayerViews(lastEventSequence, playerViews, errors);
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
        foreach (var gameEvent in events)
        {
            if (gameEvent.Sequence <= previousFrameSequence)
            {
                errors.Add(
                    $"event stream is not ordered by sequence: {gameEvent.Sequence} after {previousFrameSequence}");
            }

            previousFrameSequence = gameEvent.Sequence;
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
        long lastEventSequence,
        IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events,
        List<string> errors)
    {
        var acceptedEventOwners = new Dictionary<long, string>();
        var seenCommandIntents = new HashSet<(string PlayerId, string ClientIntentId)>();
        foreach (var command in commands)
        {
            if (!seenCommandIntents.Add((command.PlayerId, command.ClientIntentId)))
            {
                errors.Add(
                    $"command {command.ClientIntentId} for player {command.PlayerId} appears more than once in recovery frame");
            }

            if (command.StartedEventSequence < 0)
            {
                errors.Add(
                    $"command {command.ClientIntentId} has negative started event sequence {command.StartedEventSequence}");
            }

            if (TryReadRawCommandType(command.RawCommand, out var rawCommandType)
                && !string.Equals(rawCommandType, ExpectedRawCommandType(command.CommandType), StringComparison.Ordinal))
            {
                errors.Add(
                    $"command {command.ClientIntentId} raw cmdType {rawCommandType} does not match recovered command type {command.CommandType}");
            }

            if (command.StartedTick < 0)
            {
                errors.Add(
                    $"command {command.ClientIntentId} has negative started tick {command.StartedTick}");
            }

            if (command.CompletedTick < command.StartedTick)
            {
                errors.Add(
                    $"command {command.ClientIntentId} completes before tick start: {command.StartedTick}->{command.CompletedTick}");
            }

            if (command.CompletedEventSequence < command.StartedEventSequence)
            {
                errors.Add(
                    $"command {command.ClientIntentId} completes before it starts: {command.StartedEventSequence}->{command.CompletedEventSequence}");
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
            if (!command.Accepted)
            {
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

            if (command.Accepted)
            {
                foreach (var gameEvent in events.Where(gameEvent =>
                    gameEvent.Sequence > command.StartedEventSequence
                    && gameEvent.Sequence <= command.CompletedEventSequence))
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

            if (view.Snapshot.Tick != view.SnapshotTick)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} has payload tick {view.Snapshot.Tick} but row tick {view.SnapshotTick}");
            }

            if (view.SnapshotEventSequence > lastEventSequence)
            {
                errors.Add(
                    $"snapshot for {view.PlayerId} points to future event sequence {view.SnapshotEventSequence}");
            }

            if (view.Prompt is not null && view.PromptEventSequence > lastEventSequence)
            {
                errors.Add(
                    $"prompt for {view.PlayerId} points to future event sequence {view.PromptEventSequence}");
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
                && !string.Equals(view.PlayerId, view.Prompt.PlayerId, StringComparison.Ordinal))
            {
                errors.Add($"prompt for {view.PlayerId} has payload player {view.Prompt.PlayerId}");
            }
        }
    }

    private static void ValidatePlayerViewAgreement(
        IReadOnlyDictionary<string, RecoveredPlayerView> playerViews,
        List<string> errors)
    {
        if (playerViews.Count <= 1)
        {
            return;
        }

        var baseline = playerViews.Values
            .OrderBy(view => view.PlayerId, StringComparer.Ordinal)
            .First()
            .Snapshot;
        var baselineSeats = ExtractSeats(baseline);
        foreach (var view in playerViews.Values)
        {
            if (view.Snapshot.TurnNumber != baseline.TurnNumber)
            {
                errors.Add($"snapshot for {view.PlayerId} disagrees on turn number");
            }

            if (!string.Equals(view.Snapshot.ActivePlayerId, baseline.ActivePlayerId, StringComparison.Ordinal))
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

        foreach (var view in playerViews.Values)
        {
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

        if (spectatorReplayFrame.SpectatorSnapshot.Timing.ContainsKey("seed")
            || spectatorReplayFrame.SpectatorSnapshot.Timing.ContainsKey("rngCursor"))
        {
            errors.Add("spectator replay frame timing leaks random state");
        }
    }

    public static IReadOnlyDictionary<string, string> ExtractSeats(SnapshotDto snapshot)
    {
        var seats = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (playerId, player) in snapshot.Players)
        {
            if (TryReadSeat(player, out var seat))
            {
                seats[playerId] = seat;
            }
        }

        return seats;
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
