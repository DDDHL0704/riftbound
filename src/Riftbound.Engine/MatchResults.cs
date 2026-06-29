using System.Collections.Concurrent;

namespace Riftbound.Engine;

public sealed record MatchResultPlayerRecord(
    string PlayerId,
    string Seat,
    int Score,
    bool Won);

public sealed record MatchResultRecord(
    string RoomId,
    IReadOnlyList<MatchResultPlayerRecord> Players,
    string WinnerPlayerId,
    DateTimeOffset FinishedAtUtc);

public interface IMatchResultStore
{
    ValueTask RecordMatchResultAsync(MatchResultRecord result, CancellationToken cancellationToken);

    ValueTask<MatchResultRecord?> GetMatchResultAsync(string roomId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<MatchResultRecord>> ListMatchResultsForPlayerAsync(
        string playerId,
        int limit,
        CancellationToken cancellationToken);
}

public sealed class InMemoryMatchResultStore : IMatchResultStore
{
    private readonly ConcurrentDictionary<string, MatchResultRecord> resultsByRoom = new(StringComparer.Ordinal);

    public ValueTask RecordMatchResultAsync(MatchResultRecord result, CancellationToken cancellationToken)
    {
        var normalized = NormalizeResult(result);
        resultsByRoom[normalized.RoomId] = normalized;
        return ValueTask.CompletedTask;
    }

    public ValueTask<MatchResultRecord?> GetMatchResultAsync(string roomId, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(resultsByRoom.TryGetValue(NormalizeRequired(roomId, nameof(roomId)), out var result)
            ? result
            : null);
    }

    public ValueTask<IReadOnlyList<MatchResultRecord>> ListMatchResultsForPlayerAsync(
        string playerId,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizeRequired(playerId, nameof(playerId));
        var safeLimit = Math.Clamp(limit, 1, 100);
        var results = resultsByRoom.Values
            .Where(result => result.Players.Any(player =>
                string.Equals(player.PlayerId, normalizedPlayerId, StringComparison.Ordinal)))
            .OrderByDescending(result => result.FinishedAtUtc)
            .ThenBy(result => result.RoomId, StringComparer.Ordinal)
            .Take(safeLimit)
            .ToArray();
        return ValueTask.FromResult<IReadOnlyList<MatchResultRecord>>(results);
    }

    private static MatchResultRecord NormalizeResult(MatchResultRecord result)
    {
        var roomId = NormalizeRequired(result.RoomId, nameof(result.RoomId));
        var winnerPlayerId = NormalizeRequired(result.WinnerPlayerId, nameof(result.WinnerPlayerId));
        var players = result.Players
            .Select(player => new MatchResultPlayerRecord(
                NormalizeRequired(player.PlayerId, nameof(player.PlayerId)),
                NormalizeRequired(player.Seat, nameof(player.Seat)),
                Math.Max(0, player.Score),
                player.Won))
            .GroupBy(player => player.PlayerId, StringComparer.Ordinal)
            .Select(group => group.Last())
            .OrderBy(player => SeatSortKey(player.Seat), StringComparer.Ordinal)
            .ThenBy(player => player.PlayerId, StringComparer.Ordinal)
            .ToArray();
        if (!players.Any(player => string.Equals(player.PlayerId, winnerPlayerId, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Match result winner must be one of the recorded players.", nameof(result));
        }

        return new MatchResultRecord(
            roomId,
            players,
            winnerPlayerId,
            result.FinishedAtUtc == default ? DateTimeOffset.UtcNow : result.FinishedAtUtc.ToUniversalTime());
    }

    private static string NormalizeRequired(string? value, string name)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        return normalized;
    }

    private static string SeatSortKey(string seat)
    {
        return string.Equals(seat, "P1", StringComparison.Ordinal)
            ? "0"
            : string.Equals(seat, "P2", StringComparison.Ordinal)
                ? "1"
                : $"2:{seat}";
    }
}
