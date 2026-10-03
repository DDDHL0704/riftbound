using Riftbound.Contracts;

namespace Riftbound.Engine;

public static class MatchmakingStates
{
    public const string Queued = "QUEUED";
    public const string Matched = "MATCHED";
    public const string Cancelled = "CANCELLED";
    public const string Idle = "IDLE";
    public const string Rejected = "REJECTED";
}

public sealed record MatchmakingQueueResult(
    string State,
    string PlayerId,
    string? RoomId = null,
    string? OpponentPlayerId = null,
    PlayerSessionDto? PlayerSession = null,
    PlayerSessionDto? OpponentSession = null);

public interface IMatchmakingQueue
{
    ValueTask<MatchmakingQueueResult> EnqueueAsync(string playerId, CancellationToken cancellationToken);

    ValueTask<MatchmakingQueueResult> CancelAsync(string playerId, CancellationToken cancellationToken);

    ValueTask RemoveDisconnectedAsync(string playerId, CancellationToken cancellationToken);
}

public sealed class InMemoryMatchmakingQueue : IMatchmakingQueue
{
    private readonly IMatchSessionRegistry sessions;
    private readonly Func<string> roomIdFactory;
    private readonly PlayerConnections? connections;
    private readonly Queue<string> waitingOrder = new();
    private readonly HashSet<string> waitingPlayers = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim gate = new(1, 1);

    public InMemoryMatchmakingQueue(IMatchSessionRegistry sessions)
        : this(sessions, RoomCodeGenerator.NewRoomId)
    {
    }

    public InMemoryMatchmakingQueue(IMatchSessionRegistry sessions, Func<string> roomIdFactory, PlayerConnections? connections = null)
    {
        this.sessions = sessions;
        this.roomIdFactory = roomIdFactory;
        this.connections = connections;
    }

    public async ValueTask<MatchmakingQueueResult> EnqueueAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizeQueuedPlayer(playerId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (connections is not null && !connections.IsConnected(normalizedPlayerId))
            {
                return new MatchmakingQueueResult(MatchmakingStates.Idle, normalizedPlayerId);
            }

            if (waitingPlayers.Contains(normalizedPlayerId))
            {
                return new MatchmakingQueueResult(MatchmakingStates.Queued, normalizedPlayerId);
            }

            while (waitingOrder.Count > 0)
            {
                var opponentPlayerId = waitingOrder.Dequeue();
                if (!waitingPlayers.Remove(opponentPlayerId)
                    || (connections is not null && !connections.IsConnected(opponentPlayerId))
                    || string.Equals(opponentPlayerId, normalizedPlayerId, StringComparison.Ordinal))
                {
                    continue;
                }

                var roomId = NormalizeRoomId(roomIdFactory());
                var session = await sessions.GetOrCreateAsync(roomId, cancellationToken).ConfigureAwait(false);
                var opponentSession = await session.EnsurePlayerAsync(opponentPlayerId, cancellationToken)
                    .ConfigureAwait(false);
                var playerSession = await session.EnsurePlayerAsync(normalizedPlayerId, cancellationToken)
                    .ConfigureAwait(false);

                return new MatchmakingQueueResult(
                    MatchmakingStates.Matched,
                    normalizedPlayerId,
                    roomId,
                    opponentPlayerId,
                    playerSession,
                    opponentSession);
            }

            waitingPlayers.Add(normalizedPlayerId);
            waitingOrder.Enqueue(normalizedPlayerId);
            return new MatchmakingQueueResult(MatchmakingStates.Queued, normalizedPlayerId);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<MatchmakingQueueResult> CancelAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizeQueuedPlayer(playerId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return waitingPlayers.Remove(normalizedPlayerId)
                ? new MatchmakingQueueResult(MatchmakingStates.Cancelled, normalizedPlayerId)
                : new MatchmakingQueueResult(MatchmakingStates.Idle, normalizedPlayerId);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask RemoveDisconnectedAsync(string playerId, CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizeQueuedPlayer(playerId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Recheck inside the queue lock: an older connection closing must not
            // cancel a newer connection's queue entry.
            if (connections is not null && !connections.IsConnected(normalizedPlayerId))
            {
                waitingPlayers.Remove(normalizedPlayerId);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static string NormalizeQueuedPlayer(string playerId)
    {
        var normalizedPlayerId = PlayerIdentityService.NormalizeHandle(playerId);
        if (normalizedPlayerId.Length == 0)
        {
            throw new ArgumentException("Player id is required.", nameof(playerId));
        }

        return normalizedPlayerId;
    }

    private static string NormalizeRoomId(string roomId)
    {
        var normalized = (roomId ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length == 0)
        {
            throw new InvalidOperationException("Matchmaking room id factory returned an empty room id.");
        }

        return normalized;
    }

}
