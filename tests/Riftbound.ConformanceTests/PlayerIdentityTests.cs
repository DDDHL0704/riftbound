using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PlayerIdentityTests
{
    [Fact]
    public async Task FirstClaimRegistersHandleAndAuthenticates()
    {
        var service = new PlayerIdentityService(new InMemoryPlayerIdentityStore());

        var result = await service.AuthenticateAsync("Alice", "alice-secret-key-1234", CancellationToken.None);

        Assert.True(result.Authenticated);
        Assert.Equal(PlayerIdentityStatus.Registered, result.Status);
    }

    [Fact]
    public async Task ReturningWithSameKeyVerifies()
    {
        var service = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        await service.AuthenticateAsync("Alice", "alice-secret-key-1234", CancellationToken.None);

        var result = await service.AuthenticateAsync("Alice", "alice-secret-key-1234", CancellationToken.None);

        Assert.True(result.Authenticated);
        Assert.Equal(PlayerIdentityStatus.Verified, result.Status);
    }

    [Fact]
    public async Task DifferentKeyForClaimedHandleIsRejected()
    {
        var service = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        await service.AuthenticateAsync("Alice", "alice-secret-key-1234", CancellationToken.None);

        var result = await service.AuthenticateAsync("Alice", "an-imposter-key-9999", CancellationToken.None);

        Assert.False(result.Authenticated);
        Assert.Equal(PlayerIdentityStatus.HandleClaimed, result.Status);
    }

    [Fact]
    public async Task HandleClaimIsCaseAndWhitespaceInsensitive()
    {
        var service = new PlayerIdentityService(new InMemoryPlayerIdentityStore());
        await service.AuthenticateAsync("Alice", "alice-secret-key-1234", CancellationToken.None);

        var result = await service.AuthenticateAsync("  alice ", "alice-secret-key-1234", CancellationToken.None);

        Assert.True(result.Authenticated);
        Assert.Equal(PlayerIdentityStatus.Verified, result.Status);
    }

    [Fact]
    public async Task EmptyHandleIsRejected()
    {
        var service = new PlayerIdentityService(new InMemoryPlayerIdentityStore());

        var result = await service.AuthenticateAsync("   ", "alice-secret-key-1234", CancellationToken.None);

        Assert.False(result.Authenticated);
        Assert.Equal(PlayerIdentityStatus.InvalidHandle, result.Status);
    }

    [Fact]
    public async Task ShortKeyIsRejectedAsWeak()
    {
        var service = new PlayerIdentityService(new InMemoryPlayerIdentityStore());

        var result = await service.AuthenticateAsync("Alice", "short", CancellationToken.None);

        Assert.False(result.Authenticated);
        Assert.Equal(PlayerIdentityStatus.WeakKey, result.Status);
    }

    [Fact]
    public void PlayerKeyHasherIsDeterministicAndDoesNotReturnPlaintext()
    {
        const string key = "alice-secret-key-1234";

        var hash = PlayerKeyHasher.Hash(key);

        Assert.Equal(hash, PlayerKeyHasher.Hash($" {key} "));
        Assert.StartsWith("sha256:", hash);
        Assert.DoesNotContain(key, hash, StringComparison.Ordinal);
    }
}
