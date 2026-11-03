using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Riftbound.Contracts;

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

public static class MatchResultRecords
{
    private static readonly ConditionalWeakTable<IMatchResultStore, SemaphoreSlim> FinalWriteGates = new();

    public static async ValueTask RecordFinishedSeriesAsync(
        IMatchResultStore results, ResolutionResult result, CancellationToken cancellationToken)
    {
        // Restored duplicate receipts contain the current final state but no
        // events. The ledger remains authoritative when repairing a result
        // write interrupted by a store failure or a process restart.
        if (!result.Accepted || result.State.Status != MatchStatuses.Finished
            || RoomMatchSeries.Current(result.State) is not { Phase: MatchSeriesPhase.Finished })
            return;
        await results.RecordFinalMatchResultAsync(FromFinishedState(result.State, DateTimeOffset.UtcNow), cancellationToken)
            .ConfigureAwait(false);
    }

    // Compatibility for injected stores. Production stores implement atomic
    // admission in their own storage; adapters sharing one store serialize here.
    internal static async ValueTask RecordFinalWithGateAsync(
        IMatchResultStore results, MatchResultRecord record, CancellationToken cancellationToken)
    {
        var gate = FinalWriteGates.GetValue(results, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await results.GetMatchResultAsync(record.RoomId, cancellationToken).ConfigureAwait(false);
            if (existing is null)
                await results.RecordMatchResultAsync(record, cancellationToken).ConfigureAwait(false);
            else RequireSameFinalResult(existing, record);
        }
        finally { gate.Release(); }
    }

    public static void RequireSameFinalResult(MatchResultRecord existing, MatchResultRecord record)
    {
        // The original finish time belongs to the first committed result.
        if (existing.RoomId != record.RoomId || existing.WinnerPlayerId != record.WinnerPlayerId
            || !existing.Players.OrderBy(player => player.PlayerId, StringComparer.Ordinal)
                .SequenceEqual(record.Players.OrderBy(player => player.PlayerId, StringComparer.Ordinal)))
            throw new MatchSessionException(ErrorCodes.RecoveryInconsistent, "已保存的比赛战绩与最终比赛小分不一致。");
    }

    public static MatchResultRecord FromFinishedState(MatchState state, DateTimeOffset finishedAtUtc)
    {
        if (state.Status != MatchStatuses.Finished || string.IsNullOrWhiteSpace(state.WinnerPlayerId))
            throw new InvalidOperationException("Cannot record a match result without a finished match and a winner.");
        var winner = state.WinnerPlayerId;
        var series = RoomMatchSeries.ViewFor(state);
        var players = state.Seats.Select(entry => new MatchResultPlayerRecord(entry.Key, entry.Value,
                series is not null ? series.Wins.GetValueOrDefault(entry.Key) : state.PlayerScores.GetValueOrDefault(entry.Key),
                string.Equals(entry.Key, winner, StringComparison.Ordinal)))
            .OrderBy(player => player.Seat == "P1" ? "0" : player.Seat == "P2" ? "1" : $"2:{player.Seat}", StringComparer.Ordinal)
            .ThenBy(player => player.PlayerId, StringComparer.Ordinal).ToArray();
        return new(state.RoomId, players, winner, finishedAtUtc);
    }
}

public sealed record PlayerMatchStatsRecord(
    string PlayerId,
    int TotalMatches,
    int Wins,
    int Losses,
    double WinRate);

public sealed record PlayerLeaderboardEntryRecord(
    string PlayerId,
    int TotalMatches,
    int Wins,
    int Losses,
    double WinRate);

public interface IMatchResultStore
{
    ValueTask RecordMatchResultAsync(MatchResultRecord result, CancellationToken cancellationToken);

    // First final result wins. Equal retries preserve it; conflicting retries fail.
    ValueTask RecordFinalMatchResultAsync(MatchResultRecord result, CancellationToken cancellationToken) =>
        MatchResultRecords.RecordFinalWithGateAsync(this, result, cancellationToken);

    ValueTask<MatchResultRecord?> GetMatchResultAsync(string roomId, CancellationToken cancellationToken);

    ValueTask<PlayerMatchStatsRecord> GetPlayerMatchStatsAsync(string playerId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<PlayerLeaderboardEntryRecord>> ListLeaderboardAsync(
        int limit,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<MatchResultRecord>> ListMatchResultsForPlayerAsync(
        string playerId,
        int limit,
        CancellationToken cancellationToken);
}

public sealed class InMemoryMatchResultStore : IMatchResultStore
{
    private readonly ConcurrentDictionary<string, MatchResultRecord> resultsByRoom = new(StringComparer.Ordinal);

    public ValueTask RecordFinalMatchResultAsync(MatchResultRecord result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizeResult(result);
        var stored = resultsByRoom.GetOrAdd(normalized.RoomId, normalized);
        MatchResultRecords.RequireSameFinalResult(stored, normalized);
        return ValueTask.CompletedTask;
    }

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

    public ValueTask<PlayerMatchStatsRecord> GetPlayerMatchStatsAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        var normalizedPlayerId = NormalizeRequired(playerId, nameof(playerId));
        var playerResults = resultsByRoom.Values
            .SelectMany(result => result.Players)
            .Where(player => string.Equals(player.PlayerId, normalizedPlayerId, StringComparison.Ordinal))
            .ToArray();
        var totalMatches = playerResults.Length;
        var wins = playerResults.Count(player => player.Won);
        var losses = totalMatches - wins;
        return ValueTask.FromResult(new PlayerMatchStatsRecord(
            normalizedPlayerId,
            totalMatches,
            wins,
            losses,
            totalMatches == 0 ? 0 : (double)wins / totalMatches));
    }

    public ValueTask<IReadOnlyList<PlayerLeaderboardEntryRecord>> ListLeaderboardAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        var entries = resultsByRoom.Values
            .SelectMany(result => result.Players)
            .GroupBy(player => player.PlayerId, StringComparer.Ordinal)
            .Select(group =>
            {
                var totalMatches = group.Count();
                var wins = group.Count(player => player.Won);
                var losses = totalMatches - wins;
                return new PlayerLeaderboardEntryRecord(
                    group.Key,
                    totalMatches,
                    wins,
                    losses,
                    totalMatches == 0 ? 0 : (double)wins / totalMatches);
            })
            .OrderByDescending(entry => entry.Wins)
            .ThenByDescending(entry => entry.WinRate)
            .ThenByDescending(entry => entry.TotalMatches)
            .ThenBy(entry => entry.PlayerId, StringComparer.Ordinal)
            .Take(safeLimit)
            .ToArray();
        return ValueTask.FromResult<IReadOnlyList<PlayerLeaderboardEntryRecord>>(entries);
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
