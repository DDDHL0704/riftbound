using Riftbound.Contracts;
using Riftbound.Engine;

namespace Riftbound.Api;

public static class PlayerProfileEndpoints
{
    private const int DefaultMatchLimit = 20;
    private const int MaximumMatchLimit = 100;

    public static async Task<PlayerProfileDto> GetPlayerProfileAsync(
        string handle,
        IMatchResultStore matchResults,
        CancellationToken cancellationToken)
    {
        var normalizedHandle = NormalizeHandle(handle);
        var stats = await matchResults.GetPlayerMatchStatsAsync(normalizedHandle, cancellationToken)
            .ConfigureAwait(false);
        return new PlayerProfileDto(
            normalizedHandle,
            stats.TotalMatches,
            stats.Wins,
            stats.Losses,
            stats.WinRate);
    }

    public static async Task<IReadOnlyList<PlayerMatchDto>> GetPlayerMatchesAsync(
        string handle,
        int? limit,
        IMatchResultStore matchResults,
        CancellationToken cancellationToken)
    {
        var normalizedHandle = NormalizeHandle(handle);
        var results = await matchResults.ListMatchResultsForPlayerAsync(
                normalizedHandle,
                NormalizeLimit(limit),
                cancellationToken)
            .ConfigureAwait(false);
        return results.Select(ToDto).ToArray();
    }

    private static PlayerMatchDto ToDto(MatchResultRecord result)
    {
        return new PlayerMatchDto(
            result.RoomId,
            result.WinnerPlayerId,
            result.FinishedAtUtc,
            result.Players.Select(player => new PlayerMatchParticipantDto(
                player.PlayerId,
                player.Seat,
                player.Score,
                player.Won)).ToArray());
    }

    private static string NormalizeHandle(string handle)
    {
        return PlayerIdentityService.NormalizeHandle(handle);
    }

    private static int NormalizeLimit(int? limit)
    {
        return Math.Clamp(limit.GetValueOrDefault(DefaultMatchLimit), 1, MaximumMatchLimit);
    }
}
