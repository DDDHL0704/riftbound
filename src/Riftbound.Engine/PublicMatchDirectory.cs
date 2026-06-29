using Riftbound.Contracts;

namespace Riftbound.Engine;

public static class PublicMatchStatuses
{
    public const string Waiting = "WAITING";
}

public interface IPublicMatchDirectory
{
    ValueTask<PublicMatchDto> CreateAsync(
        string roomId,
        string hostPlayerId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<PublicMatchDto>> ListOpenAsync(CancellationToken cancellationToken);

    ValueTask NotifyPlayerJoinedAsync(
        string roomId,
        string playerId,
        CancellationToken cancellationToken);
}

public sealed class InMemoryPublicMatchDirectory : IPublicMatchDirectory
{
    private readonly Func<DateTimeOffset> clock;
    private readonly Dictionary<string, PublicMatchEntry> matches = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim gate = new(1, 1);

    public InMemoryPublicMatchDirectory()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    public InMemoryPublicMatchDirectory(Func<DateTimeOffset> clock)
    {
        this.clock = clock;
    }

    public async ValueTask<PublicMatchDto> CreateAsync(
        string roomId,
        string hostPlayerId,
        CancellationToken cancellationToken)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        var normalizedHostPlayerId = NormalizePlayerId(hostPlayerId);
        var createdAt = clock();
        var entry = new PublicMatchEntry(normalizedRoomId, normalizedHostPlayerId, createdAt);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            matches[normalizedRoomId] = entry;
            return entry.ToDto();
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<IReadOnlyList<PublicMatchDto>> ListOpenAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return matches.Values
                .OrderBy(match => match.CreatedAt)
                .ThenBy(match => match.RoomId, StringComparer.Ordinal)
                .Select(match => match.ToDto())
                .ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask NotifyPlayerJoinedAsync(
        string roomId,
        string playerId,
        CancellationToken cancellationToken)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        var normalizedPlayerId = NormalizePlayerId(playerId);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (matches.TryGetValue(normalizedRoomId, out var match)
                && !string.Equals(match.HostPlayerId, normalizedPlayerId, StringComparison.Ordinal))
            {
                matches.Remove(normalizedRoomId);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static string NormalizeRoomId(string roomId)
    {
        var normalized = (roomId ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Room id is required.", nameof(roomId));
        }

        return normalized;
    }

    private static string NormalizePlayerId(string playerId)
    {
        var normalized = PlayerIdentityService.NormalizeHandle(playerId);
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Player id is required.", nameof(playerId));
        }

        return normalized;
    }

    private sealed record PublicMatchEntry(
        string RoomId,
        string HostPlayerId,
        DateTimeOffset CreatedAt)
    {
        public PublicMatchDto ToDto()
        {
            return new PublicMatchDto(
                RoomId,
                HostPlayerId,
                SeatCount: 1,
                Capacity: 2,
                PublicMatchStatuses.Waiting,
                CreatedAt);
        }
    }
}
