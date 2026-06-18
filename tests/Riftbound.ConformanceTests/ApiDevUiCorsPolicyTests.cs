using Riftbound.Api;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ApiDevUiCorsPolicyTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5173")]
    [InlineData("http://127.0.0.1:5179")]
    [InlineData("http://localhost:5175")]
    [InlineData("http://[::1]:5173")]
    [InlineData("http://[::1]:5179")]
    public void DevelopmentCorsAllowsLoopbackViteFallbackPorts(string origin)
    {
        var allowed = DevUiCorsPolicy.IsAllowedOrigin(
            origin,
            [],
            allowLoopbackViteFallback: true);

        Assert.True(allowed);
    }

    [Fact]
    public void ProductionCorsKeepsLoopbackFallbackClosed()
    {
        var allowed = DevUiCorsPolicy.IsAllowedOrigin(
            "http://127.0.0.1:5175",
            DevUiCorsPolicy.DefaultOrigins,
            allowLoopbackViteFallback: false);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData("https://127.0.0.1:5175")]
    [InlineData("http://127.0.0.1:5180")]
    [InlineData("http://example.com:5175")]
    public void DevelopmentCorsRejectsOriginsOutsideLoopbackFallback(string origin)
    {
        var allowed = DevUiCorsPolicy.IsAllowedOrigin(
            origin,
            [],
            allowLoopbackViteFallback: true);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData("HTTP://LOCALHOST:5173")]
    [InlineData("http://127.0.0.1:5088")]
    public void ConfiguredOriginsAllowCaseInsensitiveMatches(string origin)
    {
        var allowed = DevUiCorsPolicy.IsAllowedOrigin(
            origin,
            DevUiCorsPolicy.DefaultOrigins,
            allowLoopbackViteFallback: false);

        Assert.True(allowed);
    }
}
