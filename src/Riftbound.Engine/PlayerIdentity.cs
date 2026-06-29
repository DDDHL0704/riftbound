using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Riftbound.Engine;

/// <summary>
/// Persists the secret-key hash claimed for each player handle so a returning player can
/// prove ownership of their name. Lightweight identity binding: a handle is owned by the
/// first key that claims it, and only that key authenticates afterwards.
/// </summary>
public interface IPlayerIdentityStore
{
    /// <summary>
    /// Atomically claims <paramref name="normalizedHandle"/> with <paramref name="keyHash"/> when it is
    /// unclaimed (returning <see cref="PlayerIdentityStatus.Registered"/>), verifies a matching key on a
    /// claimed handle (<see cref="PlayerIdentityStatus.Verified"/>), or rejects a different key
    /// (<see cref="PlayerIdentityStatus.HandleClaimed"/>).
    /// </summary>
    ValueTask<PlayerIdentityStatus> ClaimOrVerifyAsync(
        string normalizedHandle,
        string keyHash,
        CancellationToken cancellationToken);
}

public enum PlayerIdentityStatus
{
    Registered,
    Verified,
    HandleClaimed,
    InvalidHandle,
    WeakKey
}

public sealed record PlayerIdentityResult(bool Authenticated, PlayerIdentityStatus Status, string NormalizedHandle);

public sealed class InMemoryPlayerIdentityStore : IPlayerIdentityStore
{
    private readonly ConcurrentDictionary<string, string> _keyHashByHandle = new(StringComparer.Ordinal);

    public ValueTask<PlayerIdentityStatus> ClaimOrVerifyAsync(
        string normalizedHandle,
        string keyHash,
        CancellationToken cancellationToken)
    {
        // Handles are write-once: the first key to claim one owns it, so a present entry never changes.
        if (_keyHashByHandle.TryAdd(normalizedHandle, keyHash))
        {
            return ValueTask.FromResult(PlayerIdentityStatus.Registered);
        }

        var stored = _keyHashByHandle[normalizedHandle];
        return ValueTask.FromResult(string.Equals(stored, keyHash, StringComparison.Ordinal)
            ? PlayerIdentityStatus.Verified
            : PlayerIdentityStatus.HandleClaimed);
    }
}

public static class PlayerKeyHasher
{
    public static string Hash(string playerKey)
    {
        var normalized = playerKey.Trim();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"sha256:{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}

public sealed class PlayerIdentityService(IPlayerIdentityStore store)
{
    public const int MinimumKeyLength = 16;

    public async ValueTask<PlayerIdentityResult> AuthenticateAsync(
        string? handle,
        string? playerKey,
        CancellationToken cancellationToken)
    {
        var normalizedHandle = NormalizeHandle(handle);
        if (normalizedHandle.Length == 0)
        {
            return new PlayerIdentityResult(false, PlayerIdentityStatus.InvalidHandle, normalizedHandle);
        }

        var trimmedKey = (playerKey ?? string.Empty).Trim();
        if (trimmedKey.Length < MinimumKeyLength)
        {
            return new PlayerIdentityResult(false, PlayerIdentityStatus.WeakKey, normalizedHandle);
        }

        var status = await store.ClaimOrVerifyAsync(normalizedHandle, PlayerKeyHasher.Hash(trimmedKey), cancellationToken)
            .ConfigureAwait(false);
        var authenticated = status is PlayerIdentityStatus.Registered or PlayerIdentityStatus.Verified;
        return new PlayerIdentityResult(authenticated, status, normalizedHandle);
    }

    public static string NormalizeHandle(string? handle)
    {
        return (handle ?? string.Empty).Trim().ToLowerInvariant();
    }
}
