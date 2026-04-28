using System.Security.Cryptography;
using System.Text;

namespace Riftbound.Engine;

public interface IMatchPlayerStore
{
    ValueTask SavePlayerSessionAsync(
        string roomId,
        string playerId,
        string seat,
        string reconnectTokenHash,
        CancellationToken cancellationToken);

    ValueTask<bool> HasReconnectTokenHashAsync(
        string roomId,
        string playerId,
        string reconnectTokenHash,
        CancellationToken cancellationToken);
}

public sealed class NoopMatchPlayerStore : IMatchPlayerStore
{
    public static NoopMatchPlayerStore Instance { get; } = new();

    private NoopMatchPlayerStore()
    {
    }

    public ValueTask SavePlayerSessionAsync(
        string roomId,
        string playerId,
        string seat,
        string reconnectTokenHash,
        CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> HasReconnectTokenHashAsync(
        string roomId,
        string playerId,
        string reconnectTokenHash,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(false);
    }
}

public static class ReconnectTokenHasher
{
    public static string Hash(string reconnectToken)
    {
        var normalized = reconnectToken.Trim();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"sha256:{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}
