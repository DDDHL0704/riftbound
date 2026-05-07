using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Riftbound.Api;

public static class DevUiCorsPolicy
{
    public static readonly string[] DefaultOrigins =
    [
        "http://127.0.0.1:5173",
        "http://localhost:5173",
        "http://127.0.0.1:5174",
        "http://localhost:5174"
    ];

    public static CorsPolicyBuilder Apply(CorsPolicyBuilder policy, IReadOnlyCollection<string> configuredOrigins, bool allowLoopbackViteFallback)
    {
        return policy
            .SetIsOriginAllowed(origin => IsAllowedOrigin(origin, configuredOrigins, allowLoopbackViteFallback))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }

    public static bool IsAllowedOrigin(string? origin, IReadOnlyCollection<string> configuredOrigins, bool allowLoopbackViteFallback)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (configuredOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!allowLoopbackViteFallback || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && IsLoopbackHost(uri.Host)
            && uri.Port is >= 5173 and <= 5179;
    }

    private static bool IsLoopbackHost(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "[::1]", StringComparison.OrdinalIgnoreCase);
    }
}
