using Riftbound.Api;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ApiDevUiCorsPolicyTests
{
    [Fact]
    public void DevelopmentCorsAllowsLoopbackViteFallbackPort()
    {
        var allowed = DevUiCorsPolicy.IsAllowedOrigin(
            "http://127.0.0.1:5175",
            DevUiCorsPolicy.DefaultOrigins,
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

    [Fact]
    public void DevelopmentCorsRejectsNonLoopbackOrigins()
    {
        var allowed = DevUiCorsPolicy.IsAllowedOrigin(
            "http://example.com:5175",
            DevUiCorsPolicy.DefaultOrigins,
            allowLoopbackViteFallback: true);

        Assert.False(allowed);
    }
}
