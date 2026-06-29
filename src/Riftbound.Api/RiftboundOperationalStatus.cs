using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Riftbound.Api;

internal sealed record RiftboundDependencyStatus(
    string Name,
    string Mode,
    string Status);

internal sealed record RiftboundOperationalSnapshot(
    string Status,
    string Service,
    string Environment,
    string Dotnet,
    string PersistenceMode,
    string SignalRScaleMode,
    bool RedisBackplaneConfigured,
    string RedisChannelPrefix,
    bool MetricsEnabled,
    int ConfiguredCorsOriginCount,
    bool DevelopmentLoopbackCorsFallback,
    IReadOnlyList<RiftboundDependencyStatus> Dependencies);

internal static class RiftboundOperationalStatus
{
    internal const string ServiceName = "riftbound-dotnet";
    internal const string DefaultRedisChannelPrefix = "riftbound";

    internal static RiftboundOperationalSnapshot Build(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var redisConnectionString = ResolveRedisBackplaneConnectionString(configuration);
        var redisBackplaneConfigured = !string.IsNullOrWhiteSpace(redisConnectionString);
        var persistenceMode = ResolvePersistenceMode(configuration);
        var signalRScaleMode = redisBackplaneConfigured ? "redis-backplane" : "single-instance";

        return new RiftboundOperationalSnapshot(
            Status: "ok",
            Service: ServiceName,
            Environment: environment.EnvironmentName,
            Dotnet: System.Environment.Version.ToString(),
            PersistenceMode: persistenceMode,
            SignalRScaleMode: signalRScaleMode,
            RedisBackplaneConfigured: redisBackplaneConfigured,
            RedisChannelPrefix: ResolveRedisChannelPrefix(configuration),
            MetricsEnabled: MetricsEnabled(configuration),
            ConfiguredCorsOriginCount: ResolveDevUiOrigins(configuration).Count,
            DevelopmentLoopbackCorsFallback: environment.IsDevelopment(),
            Dependencies:
            [
                new RiftboundDependencyStatus(
                    "persistence",
                    persistenceMode,
                    persistenceMode == "postgres" ? "configured" : "ok"),
                new RiftboundDependencyStatus(
                    "signalr-backplane",
                    signalRScaleMode,
                    redisBackplaneConfigured ? "configured" : "ok")
            ]);
    }

    internal static IReadOnlyList<string> ResolveDevUiOrigins(IConfiguration configuration)
    {
        var configured = configuration.GetSection("Riftbound:DevUiOrigins").Get<string[]>();
        return configured is { Length: > 0 }
            ? configured
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Select(origin => origin.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : DevUiCorsPolicy.DefaultOrigins;
    }

    internal static string ResolvePersistenceMode(IConfiguration configuration)
    {
        return string.IsNullOrWhiteSpace(configuration.GetConnectionString("Riftbound"))
            ? "memory"
            : "postgres";
    }

    internal static string? ResolveRedisBackplaneConnectionString(IConfiguration configuration)
    {
        return FirstNonBlank(
            configuration.GetConnectionString("SignalRRedis"),
            configuration["Riftbound:SignalR:Redis:ConnectionString"]);
    }

    internal static string ResolveRedisChannelPrefix(IConfiguration configuration)
    {
        return FirstNonBlank(configuration["Riftbound:SignalR:Redis:ChannelPrefix"]) ?? DefaultRedisChannelPrefix;
    }

    internal static bool MetricsEnabled(IConfiguration configuration)
    {
        return configuration.GetValue("Riftbound:Metrics:Enabled", true);
    }

    internal static string ToPrometheus(RiftboundOperationalSnapshot snapshot)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["service"] = snapshot.Service,
            ["environment"] = snapshot.Environment,
            ["persistence"] = snapshot.PersistenceMode,
            ["signalr_scale"] = snapshot.SignalRScaleMode
        };

        var builder = new StringBuilder();
        builder.AppendLine("# HELP riftbound_health_status Riftbound API health status: 1 means the API process is accepting traffic.");
        builder.AppendLine("# TYPE riftbound_health_status gauge");
        builder.Append("riftbound_health_status");
        AppendLabels(builder, labels);
        builder.AppendLine(" 1");
        builder.AppendLine("# HELP riftbound_configured_cors_origins Number of configured Dev UI CORS origins.");
        builder.AppendLine("# TYPE riftbound_configured_cors_origins gauge");
        builder.Append("riftbound_configured_cors_origins");
        AppendLabels(builder, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["service"] = snapshot.Service,
            ["environment"] = snapshot.Environment
        });
        builder.Append(' ');
        builder.Append(snapshot.ConfiguredCorsOriginCount);
        builder.AppendLine();
        builder.AppendLine("# HELP riftbound_redis_backplane_configured Whether SignalR Redis backplane configuration is present.");
        builder.AppendLine("# TYPE riftbound_redis_backplane_configured gauge");
        builder.Append("riftbound_redis_backplane_configured");
        AppendLabels(builder, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["service"] = snapshot.Service,
            ["environment"] = snapshot.Environment,
            ["signalr_scale"] = snapshot.SignalRScaleMode
        });
        builder.Append(' ');
        builder.Append(snapshot.RedisBackplaneConfigured ? '1' : '0');
        builder.AppendLine();
        return builder.ToString();
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static void AppendLabels(StringBuilder builder, IReadOnlyDictionary<string, string> labels)
    {
        builder.Append('{');
        var first = true;
        foreach (var (name, value) in labels)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append(name);
            builder.Append("=\"");
            builder.Append(EscapeMetricLabel(value));
            builder.Append('"');
        }

        builder.Append('}');
    }

    private static string EscapeMetricLabel(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
