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
    MatchState? AuthoritativeState = null)
{
    public bool IsConsistent => ValidationErrors.Count == 0;

    public long ReplayFromEventSequence =>
        PlayerViews.Count == 0 ? 0 : PlayerViews.Values.Min(view => view.SnapshotEventSequence);

    public IReadOnlyList<RecoveredEvent> EventsAfterReplayPoint =>
        Events.Where(gameEvent => gameEvent.Sequence > ReplayFromEventSequence).ToArray();
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
    public static IReadOnlyList<string> Validate(
        string roomId,
        long lastEventSequence,
        IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events,
        IReadOnlyDictionary<string, RecoveredPlayerView> playerViews,
        MatchState? authoritativeState = null)
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
        ValidateAuthoritativeState(roomId, authoritativeState, playerViews, errors);

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
        foreach (var command in commands)
        {
            if (command.StartedEventSequence < 0)
            {
                errors.Add(
                    $"command {command.ClientIntentId} has negative started event sequence {command.StartedEventSequence}");
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
            if (command.Accepted && actualEventCount != expectedEventCount)
            {
                errors.Add(
                    $"command {command.ClientIntentId} covers {expectedEventCount} event(s) but {actualEventCount} were loaded");
            }
        }
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
